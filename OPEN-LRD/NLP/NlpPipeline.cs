using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    internal sealed class NlpPipeline : IDisposable
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;
        private readonly ReasoningEngine reasoning;
        private readonly Guid pipelineID;
        private readonly Guid conversationID;
        private readonly ConversationContext conversation;

        public NlpPipeline(
            LRD_Core core,
            NlpOptions? options = null,
            ReasoningEngine? reasoning = null,
            Guid? conversationID = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before creating an NLP pipeline.");

            this.options = options ?? new NlpOptions();
            this.reasoning = reasoning ?? new ReasoningEngine(core);
            this.pipelineID = Guid.NewGuid();
            this.conversation = core.ResolveConversation(conversationID, this.options);
            this.conversationID = this.conversation.ConversationID;

            if (!core.RegisterPipelineConversation(this.pipelineID, this.conversationID))
                throw new InvalidOperationException("The pipeline could not be attached to its conversation.");
        }

        public Guid PipelineID => pipelineID;

        public Guid ConversationID => conversationID;

        public ConversationContext Conversation => conversation;

        public IReadOnlyList<SessionProvenanceEntry> Provenance => conversation.Provenance;

        public void ResetConversation() => conversation.Clear();

        public void Dispose()
        {
            core.UnregisterPipelineConversation(pipelineID);
        }

        public NlpResult Analyze(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new NlpResult(NlpStatus.InvalidInput, new[] { "Text is required." });

            if (text.Length > options.MaxCharacters)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum length of {options.MaxCharacters} characters." });
            }

            string normalizedText = TextNormalizer.Normalize(text);
            ParsedText parsed = TextTokenizer.Tokenize(normalizedText, text, options);

            if (parsed.Tokens.Count == 0)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    parsed.Diagnostics.Count > 0
                        ? parsed.Diagnostics
                        : new[] { "No analyzable text was found." },
                    parsed);
            }

            if (parsed.Tokens.All(token => token.PartOfSpeech == PartOfSpeech.Punctuation))
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { "Text does not contain any analyzable words." },
                    parsed);
            }

            if (parsed.Sentences.Count > options.MaxSentences)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum of {options.MaxSentences} sentences." },
                    parsed);
            }

            if (parsed.Tokens.Count > options.MaxTokens)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum of {options.MaxTokens} tokens." },
                    parsed);
            }

            ParsedText tagged = PosTagger.Tag(parsed);
            ParsedText shallow = ShallowParser.Parse(tagged);
            ParsedText recognized = NamedEntityRecognizer.Recognize(shallow);
            RelationExtractionResult extraction = RelationExtractor.Extract(recognized, options);

            List<string> diagnostics = new List<string>(recognized.Diagnostics.Count + extraction.Diagnostics.Count);
            diagnostics.AddRange(recognized.Diagnostics);
            diagnostics.AddRange(extraction.Diagnostics);

            return new NlpResult(NlpStatus.Parsed, diagnostics, recognized)
            {
                Frames = extraction.Frames
            };
        }

        public NlpResult Ask(string? text)
        {
            NlpResult analysis = Analyze(text);

            if (analysis.Status == NlpStatus.InvalidInput || analysis.Parsed is null)
                return CreateInvalidAskResult(analysis);

            if (CommandRecognizer.TryRecognize(analysis.Parsed, out NlpCommand command))
                return ExecuteCommand(command, analysis);

            conversation.BeginTurn();
            return AnswerAnalyzed(analysis, updateConversation: true);
        }

        private NlpResult ExecuteCommand(NlpCommand command, NlpResult analysis)
        {
            List<string> diagnostics = analysis.Diagnostics
                .Where(message => !message.Contains(
                    "Imperative input is parsed but not ingested.",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            diagnostics.AddRange(command.Diagnostics);

            if (command.NeedsClarification)
            {
                NlpAnswer clarification = CommandProcessor.CreateClarification(command);
                diagnostics.AddRange(clarification.Diagnostics);

                return new NlpResult(
                    NlpStatus.Ambiguous,
                    Deduplicate(diagnostics),
                    analysis.Parsed)
                {
                    Frames = analysis.Frames,
                    Answer = clarification,
                    Command = command
                };
            }

            if (command.TargetKind == CommandTargetKind.Query)
            {
                NlpResult queryAnalysis = Analyze(command.TargetText);
                NlpResult result = queryAnalysis.Status == NlpStatus.InvalidInput
                    || queryAnalysis.Parsed is null
                    ? CreateInvalidAskResult(queryAnalysis)
                    : AnswerAnalyzed(queryAnalysis, updateConversation: false);

                return result with
                {
                    Diagnostics = Deduplicate(diagnostics.Concat(result.Diagnostics).ToList()),
                    Command = command
                };
            }

            NlpAnswer answer = CommandProcessor.ExecuteFixed(
                command,
                core,
                conversation,
                options);
            diagnostics.AddRange(answer.Diagnostics);

            return new NlpResult(
                MapAnswerStatus(answer.Status),
                Deduplicate(diagnostics),
                analysis.Parsed)
            {
                Frames = analysis.Frames,
                Answer = answer,
                Command = command
            };
        }

        private NlpResult AnswerAnalyzed(NlpResult analysis, bool updateConversation)
        {
            EntityResolver resolver = new EntityResolver(core, conversation, options);
            QuestionAnalysis question = QuestionAnalyzer.Analyze(analysis.Parsed!, resolver, options);

            List<string> diagnostics = new List<string>();
            diagnostics.AddRange(analysis.Diagnostics);
            diagnostics.AddRange(question.Diagnostics);

            List<QuestionEvaluation> evaluations = new List<QuestionEvaluation>();

            if (!question.AnchorIsAmbiguous && question.Supported)
            {
                foreach (QuestionVariant variant in question.Variants)
                {
                    ReasoningResult result = reasoning.Answer(variant.Chain);
                    evaluations.Add(new QuestionEvaluation(variant, result));
                    diagnostics.AddRange(result.Diagnostics);

                    if (question.Type == QuestionType.YesNo
                        && result.Status == ReasoningStatus.Answered
                        && result.SelectedMatch is not null
                        && result.Matches.Count == 1)
                    {
                        break;
                    }
                }
            }

            NlpAnswer answer = AnswerGenerator.Generate(question, evaluations, core, options, conversation);
            diagnostics.AddRange(answer.Diagnostics);

            if (updateConversation
                && answer.Status is AnswerStatus.Answered or AnswerStatus.Yes or AnswerStatus.No)
            {
                UpdateAskConversation(question, answer);
            }

            NlpStatus status = MapAnswerStatus(answer.Status);

            return new NlpResult(status, Deduplicate(diagnostics), analysis.Parsed)
            {
                Frames = analysis.Frames,
                Question = question,
                Answer = answer,
                ReasoningResults = evaluations.Select(evaluation => evaluation.Result).ToArray()
            };
        }

        private static NlpResult CreateInvalidAskResult(NlpResult analysis)
        {
            IReadOnlyList<string> invalidDiagnostics = Deduplicate(analysis.Diagnostics);
            string reason = invalidDiagnostics.FirstOrDefault() ?? "Text is required.";
            NlpAnswer invalid = new NlpAnswer(
                AnswerStatus.InvalidInput,
                AnswerKind.None,
                "I can't answer that question yet. " + reason,
                null,
                null,
                Array.Empty<AnswerCandidate>(),
                Array.Empty<GraphPath>(),
                null,
                null,
                false,
                0.0,
                invalidDiagnostics);

            return new NlpResult(NlpStatus.InvalidInput, invalidDiagnostics, analysis.Parsed)
            {
                Answer = invalid
            };
        }

        public NlpResult Ingest(
            string? text,
            Guid? sourceNodeID = null,
            Guid? topicNodeID = null)
        {
            NlpResult analysis = Analyze(text);

            if (analysis.Status == NlpStatus.InvalidInput || analysis.Parsed is null)
                return analysis;

            conversation.BeginTurn();

            List<string> diagnostics = new List<string>(analysis.Diagnostics);
            List<RelationFrame> ingestibleFrames = analysis.Frames
                .Where(frame => frame.Ingestible || IsContextPronounFrame(frame))
                .ToList();

            if (analysis.Frames.Count == 0)
            {
                diagnostics.Add("No supported relation frames were found.");
                return new NlpResult(NlpStatus.Unsupported, diagnostics, analysis.Parsed);
            }

            if (ingestibleFrames.Count == 0)
            {
                diagnostics.Add("No ingestible relation frames were found.");
                return new NlpResult(NlpStatus.Unsupported, diagnostics, analysis.Parsed)
                {
                    Frames = analysis.Frames
                };
            }

            if (sourceNodeID.HasValue && sourceNodeID.Value != Guid.Empty)
            {
                TrustAssessment trust = reasoning.GetTrust(sourceNodeID.Value, topicNodeID);

                if (trust.Decision == TrustDecision.Discredited)
                {
                    diagnostics.Add(
                        $"The source is discredited (score {trust.Score:F3}, evidence {trust.TotalEvidence:F1}).");
                    List<SessionProvenanceEntry> rejectedProvenance = RecordRejectedProvenance(
                        ingestibleFrames,
                        analysis.Parsed.OriginalText,
                        sourceNodeID,
                        topicNodeID,
                        diagnostics);

                    return new NlpResult(NlpStatus.RejectedByTrust, diagnostics, analysis.Parsed)
                    {
                        Frames = analysis.Frames,
                        Provenance = rejectedProvenance
                    };
                }
            }

            EntityResolver resolver = new EntityResolver(core, conversation, options);
            List<LinkedList<Chain_Node>> chains = new List<LinkedList<Chain_Node>>();
            List<RelationFrame> chainFrames = new List<RelationFrame>();

            foreach (RelationFrame frame in ingestibleFrames)
            {
                ChainBuildResult built = ChainBuilder.Build(frame, resolver, options);
                diagnostics.AddRange(built.Diagnostics);

                foreach (LinkedList<Chain_Node> chain in built.Chains)
                {
                    if (chains.Count >= options.MaxChainsPerText)
                    {
                        diagnostics.Add(
                            $"The maximum of {options.MaxChainsPerText} chains per text was reached.");
                        break;
                    }

                    chains.Add(chain);
                    chainFrames.Add(frame);
                }

                if (chains.Count >= options.MaxChainsPerText)
                    break;
            }

            if (chains.Count == 0)
            {
                diagnostics.Add("No relation chain could be built from the ingestible frames.");
                return new NlpResult(NlpStatus.Unsupported, diagnostics, analysis.Parsed)
                {
                    Frames = analysis.Frames
                };
            }

            List<ReasoningResult> results = new List<ReasoningResult>(chains.Count);

            foreach (LinkedList<Chain_Node> chain in chains)
                results.Add(IngestChain(chain, sourceNodeID, topicNodeID, diagnostics));

            List<SessionProvenanceEntry> provenance = new List<SessionProvenanceEntry>();

            for (int index = 0; index < chains.Count; index++)
            {
                if (!options.EnableSessionProvenance)
                    break;

                SessionProvenanceEntry entry = BuildProvenanceEntry(
                    chains[index],
                    chainFrames[index],
                    results[index],
                    sourceNodeID,
                    topicNodeID,
                    analysis.Parsed.OriginalText);

                SessionProvenanceEntry recorded = conversation.RecordProvenance(entry);

                if (recorded.Sequence > 0)
                    provenance.Add(recorded);
            }

            for (int index = 0; index < chains.Count; index++)
            {
                if (IsSuccessfulIngestion(results[index].Status))
                    UpdateIngestionConversation(chains[index], chainFrames[index], results[index]);
            }

            NlpStatus status = AggregateStatus(results);

            return new NlpResult(status, diagnostics, analysis.Parsed)
            {
                Frames = analysis.Frames,
                Chains = chains,
                ReasoningResults = results,
                Provenance = provenance
            };
        }

        private ReasoningResult IngestChain(
            LinkedList<Chain_Node> chain,
            Guid? sourceNodeID,
            Guid? topicNodeID,
            List<string> diagnostics)
        {
            Chain_Node anchor = chain.First!.Value;

            if (!core.TryGetNode(anchor.ID, out _))
            {
                Web_Node node = new Web_Node(
                    anchor.ID,
                    anchor.Get_NodeType(),
                    NodeLifetime.Temporary,
                    options.TemporaryNodeTimeToLive);
                node.Set_Label(anchor.Label);

                foreach (string alias in anchor.Aliases)
                    node.Add_Alias(alias);

                if (core.AddExistingNode(node))
                {
                    diagnostics.Add(
                        $"Anchor node '{anchor.Label ?? anchor.ID.ToString()}' was created as temporary.");
                }
            }

            return reasoning.Ingest(chain, sourceNodeID, topicNodeID);
        }

        private bool IsContextPronounFrame(RelationFrame frame)
        {
            return options.EnableConversationContext
                && options.EnablePronounResolution
                && !frame.Negated
                && frame.Modal is null
                && frame.Voice != Voice.Unknown
                && frame.Arguments.Any(argument => argument.IsPronoun);
        }

        private List<SessionProvenanceEntry> RecordRejectedProvenance(
            IReadOnlyList<RelationFrame> frames,
            string sourceText,
            Guid? sourceNodeID,
            Guid? topicNodeID,
            IReadOnlyList<string> diagnostics)
        {
            List<SessionProvenanceEntry> entries = new List<SessionProvenanceEntry>();

            if (!options.EnableSessionProvenance || frames.Count == 0)
                return entries;

            foreach (RelationFrame frame in frames)
            {
                SessionProvenanceEntry entry = new SessionProvenanceEntry(
                    0,
                    conversation.TurnIndex,
                    frame.SentenceIndex,
                    sourceText,
                    ReasoningStatus.RejectedByTrust,
                    frame.RelationName,
                    frame.LinkClass,
                    null,
                    null,
                    null,
                    null,
                    null,
                    sourceNodeID,
                    topicNodeID,
                    Array.Empty<Guid>(),
                    Array.Empty<Guid>(),
                    false,
                    false,
                    diagnostics);

                SessionProvenanceEntry recorded = conversation.RecordProvenance(entry);

                if (recorded.Sequence > 0)
                    entries.Add(recorded);
            }

            return entries;
        }

        private SessionProvenanceEntry BuildProvenanceEntry(
            LinkedList<Chain_Node> chain,
            RelationFrame frame,
            ReasoningResult result,
            Guid? sourceNodeID,
            Guid? topicNodeID,
            string sourceText)
        {
            Chain_Node? first = chain.First?.Value;
            Chain_Node? last = chain.Last?.Value;
            Link? link = first?.Get_LinkNext();
            LinkClass linkClass = link?.Classification ?? frame.LinkClass;
            Guid? subjectNodeID = first is null ? null : MapGraphNodeID(first.ID, result);
            Guid? objectNodeID = last is null ? null : MapGraphNodeID(last.ID, result);

            if (subjectNodeID == Guid.Empty)
                subjectNodeID = null;

            if (objectNodeID == Guid.Empty)
                objectNodeID = null;

            bool targetIsQualifier = linkClass == LinkClass.spatial
                || linkClass == LinkClass.temporal
                || linkClass == LinkClass.basic
                || linkClass == LinkClass.empty;
            List<Guid> nodeIDs = chain
                .Select(node => MapGraphNodeID(node.ID, result))
                .Where(nodeID => nodeID != Guid.Empty)
                .Distinct()
                .ToList();
            List<Guid> temporaryNodeIDs = nodeIDs
                .Where(nodeID => core.TryGetNode(nodeID, out Web_Node? node)
                    && node is not null
                    && node.IsTemporary)
                .ToList();

            return new SessionProvenanceEntry(
                0,
                conversation.TurnIndex,
                frame.SentenceIndex,
                sourceText,
                result.Status,
                link?.ForwardLink.Name ?? frame.RelationName,
                linkClass,
                link?.ForwardLink.Id,
                link?.InverseLink.Id,
                subjectNodeID,
                objectNodeID,
                targetIsQualifier ? objectNodeID : null,
                sourceNodeID,
                topicNodeID,
                nodeIDs,
                temporaryNodeIDs,
                result.GraphMutated,
                IsSuccessfulIngestion(result.Status),
                result.Diagnostics);
        }

        private void UpdateIngestionConversation(
            LinkedList<Chain_Node> chain,
            RelationFrame frame,
            ReasoningResult result)
        {
            if (!options.EnableConversationContext)
                return;

            Chain_Node? first = chain.First?.Value;
            Chain_Node? last = chain.Last?.Value;

            if (first is null)
                return;

            Guid firstNodeID = MapGraphNodeID(first.ID, result);

            if (firstNodeID == Guid.Empty)
                return;

            Link? link = first.Get_LinkNext();
            FrameRole targetRole = RoleForLinkClass(link?.Classification ?? frame.LinkClass);
            List<ConversationMention> mentions = new List<ConversationMention>();
            AddChainMention(mentions, firstNodeID, first.Label, first.Get_NodeType(), FrameRole.Subject);

            Guid lastNodeID = Guid.Empty;

            if (last is not null && last.ID != first.ID)
            {
                lastNodeID = MapGraphNodeID(last.ID, result);

                if (lastNodeID != Guid.Empty)
                    AddChainMention(mentions, lastNodeID, last.Label, last.Get_NodeType(), targetRole);
            }

            conversation.RecordMentions(mentions);

            Guid? focus = lastNodeID != Guid.Empty
                ? lastNodeID
                : firstNodeID;
            conversation.SetFocus(focus, targetRole, false);
        }

        private void UpdateAskConversation(
            QuestionAnalysis question,
            NlpAnswer answer)
        {
            if (!options.EnableConversationContext)
                return;

            List<ConversationMention> mentions = new List<ConversationMention>();

            if (question.AnchorNodeID.HasValue)
            {
                FrameArgument? anchorArgument = question.AnchorRole switch
                {
                    FrameRole.Object => question.Object,
                    FrameRole.Qualifier => question.Qualifier,
                    _ => question.Subject
                };

                AddQuestionMention(
                    mentions,
                    question.AnchorNodeID.Value,
                    anchorArgument?.NodeType ?? NodeTypes.Empties,
                    question.AnchorRole,
                    anchorArgument?.SurfaceText ?? string.Empty,
                    false);
            }

            Guid? focusNodeID = question.AnchorNodeID;
            FrameRole focusRole = question.AnchorRole;
            bool fromAnswer = false;

            if (answer.Status == AnswerStatus.Answered && answer.AnswerNodeID.HasValue)
            {
                FrameRole answerRole = RoleForAnswerKind(answer.Kind);
                AddQuestionMention(
                    mentions,
                    answer.AnswerNodeID.Value,
                    NodeTypes.Empties,
                    answerRole,
                    answer.ShortAnswer ?? string.Empty,
                    true);
                focusNodeID = answer.AnswerNodeID;
                focusRole = answerRole;
                fromAnswer = true;
            }

            conversation.RecordMentions(mentions);

            if (focusNodeID.HasValue)
                conversation.SetFocus(focusNodeID, focusRole, fromAnswer);
        }

        private void AddChainMention(
            List<ConversationMention> mentions,
            Guid nodeID,
            string? fallbackLabel,
            NodeTypes fallbackType,
            FrameRole role)
        {
            string fallback = fallbackLabel ?? string.Empty;
            string label = GetNodeLabel(nodeID, fallback);
            string surface = string.IsNullOrWhiteSpace(fallback) ? label : fallback;
            NodeTypes nodeType = fallbackType;

            if (core.TryGetNode(nodeID, out Web_Node? graphNode) && graphNode is not null)
                nodeType = graphNode.Get_NodeType();

            mentions.Add(new ConversationMention(
                nodeID,
                label,
                surface,
                nodeType,
                role,
                conversation.TurnIndex,
                0,
                false));
        }

        private static Guid MapGraphNodeID(Guid chainNodeID, ReasoningResult result)
        {
            if (result.MergedNodeMap is not null
                && result.MergedNodeMap.TryGetValue(chainNodeID, out Guid mergedNodeID)
                && mergedNodeID != Guid.Empty)
            {
                return mergedNodeID;
            }

            if (result.SelectedMatch is not null
                && result.SelectedMatch.NodeMap.TryGetValue(chainNodeID, out Guid graphNodeID)
                && graphNodeID != Guid.Empty)
            {
                return graphNodeID;
            }

            return chainNodeID;
        }

        private void AddQuestionMention(
            List<ConversationMention> mentions,
            Guid nodeID,
            NodeTypes fallbackType,
            FrameRole role,
            string fallbackSurface,
            bool fromAnswer)
        {
            string label = GetNodeLabel(nodeID, fallbackSurface);
            string surface = string.IsNullOrWhiteSpace(fallbackSurface) ? label : fallbackSurface;
            NodeTypes nodeType = fallbackType;

            if (core.TryGetNode(nodeID, out Web_Node? node) && node is not null)
                nodeType = node.Get_NodeType();

            mentions.Add(new ConversationMention(
                nodeID,
                label,
                surface,
                nodeType,
                role,
                conversation.TurnIndex,
                0,
                fromAnswer));
        }

        private string GetNodeLabel(Guid nodeID, string fallback)
        {
            if (!core.TryGetNode(nodeID, out Web_Node? node) || node is null)
                return fallback;

            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            string? alias = node.Aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return alias ?? fallback;
        }

        private static bool IsSuccessfulIngestion(ReasoningStatus status)
        {
            return status is ReasoningStatus.Answered or ReasoningStatus.Contradiction;
        }

        private static FrameRole RoleForLinkClass(LinkClass linkClass)
        {
            if (linkClass == LinkClass.identities || linkClass == LinkClass.actions)
                return FrameRole.Object;

            if (linkClass == LinkClass.spatial)
                return FrameRole.Location;

            if (linkClass == LinkClass.temporal)
                return FrameRole.Time;

            return FrameRole.Qualifier;
        }

        private static FrameRole RoleForAnswerKind(AnswerKind kind)
        {
            return kind switch
            {
                AnswerKind.Location => FrameRole.Location,
                AnswerKind.Time => FrameRole.Time,
                AnswerKind.Quantity => FrameRole.Qualifier,
                _ => FrameRole.Object
            };
        }

        private static NlpStatus MapAnswerStatus(AnswerStatus status)
        {
            return status switch
            {
                AnswerStatus.Answered or AnswerStatus.Yes or AnswerStatus.No => NlpStatus.Answered,
                AnswerStatus.Ambiguous => NlpStatus.Ambiguous,
                AnswerStatus.NoMatch or AnswerStatus.Unknown => NlpStatus.NoMatch,
                AnswerStatus.Unsupported => NlpStatus.Unsupported,
                AnswerStatus.RejectedByTrust => NlpStatus.RejectedByTrust,
                AnswerStatus.InvalidInput => NlpStatus.InvalidInput,
                _ => NlpStatus.Unsupported
            };
        }

        private static IReadOnlyList<string> Deduplicate(IReadOnlyList<string> diagnostics)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> result = new List<string>(diagnostics.Count);

            foreach (string diagnostic in diagnostics)
            {
                if (!string.IsNullOrWhiteSpace(diagnostic) && seen.Add(diagnostic))
                    result.Add(diagnostic);
            }

            return result;
        }

        private static NlpStatus AggregateStatus(IReadOnlyList<ReasoningResult> results)
        {
            bool hasIngested = false;
            bool hasRejected = false;
            bool hasAmbiguous = false;
            bool hasInvalid = false;
            bool hasNoMatch = false;

            foreach (ReasoningResult result in results)
            {
                switch (result.Status)
                {
                    case ReasoningStatus.Answered:
                    case ReasoningStatus.Contradiction:
                        hasIngested = true;
                        break;

                    case ReasoningStatus.RejectedByTrust:
                        hasRejected = true;
                        break;

                    case ReasoningStatus.Ambiguous:
                        hasAmbiguous = true;
                        break;

                    case ReasoningStatus.InvalidInput:
                        hasInvalid = true;
                        break;

                    case ReasoningStatus.NoMatch:
                        hasNoMatch = true;
                        break;
                }
            }

            if (hasIngested)
                return NlpStatus.Ingested;

            if (hasRejected)
                return NlpStatus.RejectedByTrust;

            if (hasAmbiguous)
                return NlpStatus.Ambiguous;

            if (hasInvalid)
                return NlpStatus.InvalidInput;

            if (hasNoMatch)
                return NlpStatus.NoMatch;

            return NlpStatus.Unsupported;
        }
    }
}
