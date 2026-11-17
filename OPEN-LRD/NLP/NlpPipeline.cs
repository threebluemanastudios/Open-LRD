using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    /// <summary>Coordinates NLP parsing, conversational context, reasoning ingestion, and read-only Ask.</summary>
    internal sealed class NlpPipeline : IDisposable
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;
        private readonly ReasoningEngine reasoning;
        private readonly Guid pipelineID;
        private readonly Guid conversationID;
        private readonly ConversationContext conversation;

        /// <summary>Creates a pipeline attached to an initialized graph and conversation.</summary>
        public NlpPipeline(
            LRD_Core core,
            NlpOptions? options = null,
            ReasoningEngine? reasoning = null,
            Guid? conversationID = null,
            ReasoningOptions? reasoningOptions = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before creating an NLP pipeline.");

            this.options = options ?? new NlpOptions();
            this.reasoning = reasoning ?? new ReasoningEngine(core, reasoningOptions);
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

        /// <summary>Clears conversational mentions, focus, provenance, and turn state.</summary>
        public void ResetConversation() => conversation.Clear();

        /// <summary>Detaches the pipeline from its graph-managed conversation.</summary>
        public void Dispose()
        {
            core.UnregisterPipelineConversation(pipelineID);
        }

        /// <summary>Parses text into tokens, clauses, mentions, and relation frames without graph mutation.</summary>
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

        /// <summary>Answers a supported natural-language query without mutating graph knowledge.</summary>
        public NlpResult Ask(string? text)
        {
            NlpResult initialAnalysis = Analyze(text);

            if (initialAnalysis.Status == NlpStatus.InvalidInput || initialAnalysis.Parsed is null)
                return CreateInvalidAskResult(initialAnalysis) with { OriginalInput = text };

            if (options.EnableSocialDialogue
                && SocialDialogueRecognizer.TryRecognize(text, out NlpAnswer socialAnswer))
            {
                return new NlpResult(
                    NlpStatus.Answered,
                    socialAnswer.Diagnostics,
                    initialAnalysis.Parsed)
                {
                    Answer = socialAnswer,
                    OriginalInput = text
                };
            }

            if (TemporalExpressionParser.TryParseQuestionRange(
                text,
                options.ReferenceTime ?? DateTimeOffset.Now,
                out TemporalRangeQuery temporalQuery))
            {
                conversation.BeginTurn();
                NlpAnswer temporalAnswer = AnswerGenerator.GenerateTemporalRangeAnswer(
                    temporalQuery,
                    core,
                    options);
                return new NlpResult(
                    MapAnswerStatus(temporalAnswer.Status),
                    temporalAnswer.Diagnostics,
                    initialAnalysis.Parsed)
                {
                    Answer = temporalAnswer,
                    OriginalInput = text
                };
            }

            ConversationalQuery conversationalQuery = ConversationalQueryPreprocessor.Process(
                text!,
                options.EnableConversationalPreambles,
                conversation.LastQuestionTemplate);

            if (conversationalQuery.IsExploration)
            {
                NlpResult exploration = AnswerEntityExploration(
                    conversationalQuery.ExplorationEntity!,
                    conversationalQuery.Diagnostics,
                    text!);
                return exploration;
            }

            NlpResult analysis = string.Equals(
                    conversationalQuery.QueryText,
                    initialAnalysis.Parsed.NormalizedText,
                    StringComparison.Ordinal)
                ? initialAnalysis
                : Analyze(conversationalQuery.QueryText);

            if (analysis.Status == NlpStatus.InvalidInput || analysis.Parsed is null)
                return CreateInvalidAskResult(analysis) with { OriginalInput = text };

            analysis = analysis with
            {
                Diagnostics = Deduplicate(conversationalQuery.Diagnostics.Concat(analysis.Diagnostics).ToList()),
                OriginalInput = text
            };

            if (CommandRecognizer.TryRecognize(analysis.Parsed, out NlpCommand command))
                return ExecuteCommand(command, analysis) with { OriginalInput = text };

            conversation.BeginTurn();
            return AnswerAnalyzed(analysis, updateConversation: true) with { OriginalInput = text };
        }

        /// <summary>Evaluates an entity-exploration follow-up request and updates conversational focus.</summary>
        private NlpResult AnswerEntityExploration(
            string entityText,
            IReadOnlyList<string> preprocessingDiagnostics,
            string originalInput)
        {
            string questionText = "Who is " + entityText.Trim() + "?";
            NlpResult analysis = Analyze(questionText);
            if (analysis.Status == NlpStatus.InvalidInput || analysis.Parsed is null)
                return CreateInvalidAskResult(analysis) with { OriginalInput = originalInput };

            EntityResolver resolver = new EntityResolver(core, conversation, options);
            QuestionAnalysis question = QuestionAnalyzer.Analyze(analysis.Parsed, resolver, options);
            conversation.BeginTurn();

            List<string> diagnostics = preprocessingDiagnostics
                .Concat(question.Diagnostics)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            NlpAnswer answer = question.AnchorIsAmbiguous
                ? AnswerGenerator.Generate(
                    question,
                    Array.Empty<QuestionEvaluation>(),
                    core,
                    options,
                    conversation)
                : AnswerGenerator.GenerateExploration(question, core, options, conversation);
            diagnostics.AddRange(answer.Diagnostics);
            diagnostics = Deduplicate(diagnostics).ToList();

            if (answer.Status == AnswerStatus.Answered)
                UpdateAskConversation(question, answer);

            return new NlpResult(MapAnswerStatus(answer.Status), diagnostics, analysis.Parsed)
            {
                Question = question,
                Answer = answer,
                OriginalInput = originalInput
            };
        }

        /// <summary>Executes a recognized natural-language command or embedded command query.</summary>
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
                if (TemporalExpressionParser.TryParseQuestionRange(
                    command.TargetText,
                    options.ReferenceTime ?? DateTimeOffset.Now,
                    out TemporalRangeQuery temporalQuery))
                {
                    NlpAnswer temporalAnswer = AnswerGenerator.GenerateTemporalRangeAnswer(
                        temporalQuery,
                        core,
                        options);
                    diagnostics.AddRange(temporalAnswer.Diagnostics);
                    return new NlpResult(
                        MapAnswerStatus(temporalAnswer.Status),
                        Deduplicate(diagnostics),
                        analysis.Parsed)
                    {
                        Frames = analysis.Frames,
                        Answer = temporalAnswer,
                        Command = command,
                        OriginalInput = analysis.OriginalInput
                    };
                }

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

        /// <summary>Evaluates an analyzed question against the graph and optionally updates conversation state.</summary>
        private NlpResult AnswerAnalyzed(NlpResult analysis, bool updateConversation)
        {
            EntityResolver resolver = new EntityResolver(core, conversation, options);
            QuestionAnalysis question = QuestionAnalyzer.Analyze(analysis.Parsed!, resolver, options);

            List<string> diagnostics = new List<string>();
            diagnostics.AddRange(analysis.Diagnostics);
            diagnostics.AddRange(question.Diagnostics);

            if (question.Supported && question.Wh == WhKind.When)
            {
                IReadOnlyList<Web_Node> matchingEvents = FindTemporalQuestionEvents(
                    analysis.Parsed!,
                    question,
                    resolver);
                if (matchingEvents.Count > 0)
                {
                    NlpAnswer eventAnswer = AnswerGenerator.GenerateEventTimeAnswers(
                        question,
                        matchingEvents,
                        core,
                        options);
                    diagnostics.AddRange(eventAnswer.Diagnostics);
                    if (updateConversation
                        && eventAnswer.Status is AnswerStatus.Answered or AnswerStatus.Yes or AnswerStatus.No)
                    {
                        UpdateAskConversation(question, eventAnswer);
                        conversation.SetLastQuestionTemplate(
                            analysis.Parsed!.NormalizedText,
                            question.Subject is { IsPronoun: false } eventSubject
                                ? eventSubject.SurfaceText
                                : null);
                    }

                    return new NlpResult(
                        MapAnswerStatus(eventAnswer.Status),
                        Deduplicate(diagnostics),
                        analysis.Parsed)
                    {
                        Question = question,
                        Answer = eventAnswer,
                        OriginalInput = analysis.OriginalInput
                    };
                }
            }

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
                conversation.SetLastQuestionTemplate(
                    analysis.Parsed!.NormalizedText,
                    question.Subject is { IsPronoun: false } subject
                        ? subject.SurfaceText
                        : null);
            }

            NlpStatus status = MapAnswerStatus(answer.Status);

            return new NlpResult(status, Deduplicate(diagnostics), analysis.Parsed)
            {
                Frames = analysis.Frames,
                Question = question,
                Answer = answer,
                ReasoningResults = evaluations.Select(evaluation => evaluation.Result).ToArray(),
                OriginalInput = analysis.OriginalInput
            };
        }

        /// <summary>Creates an invalid-input Ask result with an explanatory answer message.</summary>
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

        /// <summary>Parses text and merges supported relation chains into the graph with source/topic trust.</summary>
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
            List<ReasoningResult> results = new List<ReasoningResult>();

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
                    results.Add(IngestChain(chain, sourceNodeID, topicNodeID, diagnostics));
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

            int eventsCreated = ExtractTemporalEvents(
                analysis.Parsed,
                chains,
                chainFrames,
                results,
                resolver,
                diagnostics);
            if (eventsCreated > 0)
                diagnostics.Add($"Created {eventsCreated} Event node(s) from temporal statements.");

            NlpStatus status = AggregateStatus(results);

            return new NlpResult(status, diagnostics, analysis.Parsed)
            {
                Frames = analysis.Frames,
                Chains = chains,
                ReasoningResults = results,
                Provenance = provenance
            };
        }

        /// <summary>Finds timed event nodes matching a when-question's anchor or subject, action, object, and location.</summary>
        private IReadOnlyList<Web_Node> FindTemporalQuestionEvents(
            ParsedText parsed,
            QuestionAnalysis question,
            EntityResolver resolver)
        {
            if (question.AnchorNodeID.HasValue
                && core.TryGetNode(question.AnchorNodeID.Value, out Web_Node? anchor)
                && anchor is not null
                && anchor.Get_NodeType() == NodeTypes.Events)
            {
                return EventNodeHelper.TryGetWindow(anchor, out _, out _)
                    ? new[] { anchor }
                    : Array.Empty<Web_Node>();
            }

            if (question.Subject is null || string.IsNullOrWhiteSpace(question.PredicateLemma))
                return Array.Empty<Web_Node>();

            EntityResolution subject = resolver.Resolve(question.Subject);
            if (!subject.ResolvedNodeID.HasValue || subject.IsAmbiguous)
                return Array.Empty<Web_Node>();

            FrameArgument? objectArgument = FindTemporalQuestionObject(parsed, question);
            Guid? objectID = ResolveEventArgument(objectArgument, resolver);
            FrameArgument? locationArgument = FindTemporalQuestionLocation(parsed, question);
            Guid? locationID = ResolveEventArgument(locationArgument, resolver);

            return core.FindEventsFor(
                subject.ResolvedNodeID.Value,
                question.PredicateLemma,
                objectID,
                locationID);
        }

        /// <summary>Finds a non-temporal direct object mention in a when-question sentence.</summary>
        private static FrameArgument? FindTemporalQuestionObject(
            ParsedText parsed,
            QuestionAnalysis question)
        {
            if (question.SentenceIndex < 0 || question.SentenceIndex >= parsed.Sentences.Count)
                return null;

            Sentence sentence = parsed.Sentences[question.SentenceIndex];
            int predicateIndex = sentence.Tokens
                .FirstOrDefault(token => string.Equals(
                    token.Lemma,
                    question.PredicateLemma,
                    StringComparison.OrdinalIgnoreCase))?.TokenIndex ?? -1;
            int subjectEnd = question.Subject?.EndToken ?? -1;
            List<Chunk> prepositions = sentence.Clauses
                .SelectMany(clause => clause.Chunks)
                .Where(chunk => chunk.Kind == ChunkKind.PrepositionalPhrase)
                .ToList();

            Mention? mention = parsed.Mentions
                .Where(candidate => candidate.SentenceIndex == sentence.Index
                    && candidate.StartToken > Math.Max(predicateIndex, subjectEnd)
                    && candidate.Kind is not (MentionKind.Date or MentionKind.Time))
                .Where(candidate => !prepositions.Any(phrase =>
                    phrase.StartToken <= candidate.StartToken
                    && candidate.EndToken <= phrase.EndToken))
                .OrderBy(candidate => candidate.StartToken)
                .FirstOrDefault();

            if (mention is null)
                return null;

            return ToFrameArgument(mention, FrameRole.Object, null);
        }

        /// <summary>Finds a spatial location qualifier in a when-question sentence.</summary>
        private static FrameArgument? FindTemporalQuestionLocation(
            ParsedText parsed,
            QuestionAnalysis question)
        {
            if (question.Qualifier is { } qualifier
                && (qualifier.NodeType == NodeTypes.Locations
                    || RelationExtractor.IsSpatialPreposition(qualifier.Preposition ?? string.Empty)))
            {
                return qualifier with { Role = FrameRole.Location };
            }

            if (question.SentenceIndex < 0 || question.SentenceIndex >= parsed.Sentences.Count)
                return null;

            Sentence sentence = parsed.Sentences[question.SentenceIndex];
            HashSet<(int Start, int End)> prepositionalRanges = sentence.Clauses
                .SelectMany(clause => clause.Chunks)
                .Where(chunk => chunk.Kind == ChunkKind.PrepositionalPhrase)
                .Select(chunk => (chunk.StartToken, chunk.EndToken))
                .ToHashSet();

            Mention? location = parsed.Mentions
                .Where(mention => mention.SentenceIndex == sentence.Index
                    && mention.NodeType == NodeTypes.Locations)
                .Where(mention => prepositionalRanges.Any(range =>
                    range.Start <= mention.StartToken && mention.EndToken <= range.End))
                .OrderBy(mention => mention.StartToken)
                .FirstOrDefault();

            return location is null
                ? null
                : ToFrameArgument(location, FrameRole.Location, null);
        }

        /// <summary>Resolves an optional event role argument to an unambiguous graph node ID.</summary>
        private static Guid? ResolveEventArgument(FrameArgument? argument, EntityResolver resolver)
        {
            if (argument is null)
                return null;

            EntityResolution resolution = resolver.Resolve(argument);
            return resolution.IsAmbiguous ? null : resolution.ResolvedNodeID;
        }

        /// <summary>Converts a recognized mention into a frame argument with the specified semantic role.</summary>
        private static FrameArgument ToFrameArgument(
            Mention mention,
            FrameRole role,
            string? preposition)
        {
            return new FrameArgument(
                role,
                mention.SurfaceText,
                mention.Lemma,
                mention.NodeType,
                mention.Kind,
                mention.SentenceIndex,
                mention.StartToken,
                mention.EndToken,
                Lexicon.IsPronoun(mention.SurfaceText),
                preposition,
                mention.Confidence);
        }

        /// <summary>Creates timed event nodes from successfully ingested statements containing temporal expressions.</summary>
        private int ExtractTemporalEvents(
            ParsedText parsed,
            IReadOnlyList<LinkedList<Chain_Node>> chains,
            IReadOnlyList<RelationFrame> chainFrames,
            IReadOnlyList<ReasoningResult> results,
            EntityResolver resolver,
            List<string> diagnostics)
        {
            HashSet<(int Sentence, int Clause, int Start, int End, string Predicate)> seenFrames =
                new HashSet<(int Sentence, int Clause, int Start, int End, string Predicate)>();
            int created = 0;

            for (int index = 0; index < chains.Count; index++)
            {
                RelationFrame frame = chainFrames[index];
                var frameKey = (
                    frame.SentenceIndex,
                    frame.ClauseIndex,
                    frame.StartToken,
                    frame.EndToken,
                    frame.PredicateLemma);
                if (!seenFrames.Add(frameKey) || !IsSuccessfulIngestion(results[index].Status))
                    continue;

                if (frame.SentenceIndex < 0 || frame.SentenceIndex >= parsed.Sentences.Count)
                    continue;

                Sentence sentence = parsed.Sentences[frame.SentenceIndex];
                if (!TemporalExpressionParser.TryParseEventWindow(
                    sentence.Text,
                    options.ReferenceTime ?? DateTimeOffset.Now,
                    out DateTimeOffset? startTime,
                    out DateTimeOffset? endTime,
                    futureTense: frame.Tense == Tense.Future))
                {
                    continue;
                }

                FrameArgument? subject = frame.Arguments.FirstOrDefault(argument => argument.Role == FrameRole.Subject);
                FrameArgument? target = frame.Arguments.FirstOrDefault(argument => argument.Role == FrameRole.Object);
                if (subject is null || chains[index].First is null)
                    continue;

                Guid subjectID = MapGraphNodeID(chains[index].First!.Value.ID, results[index]);
                if (!core.TryGetNode(subjectID, out _))
                    continue;

                Guid? locationID = null;
                FrameArgument? location = frame.Arguments.FirstOrDefault(argument => argument.Role == FrameRole.Location);
                bool targetIsLocation = target is not null && IsLocationTarget(frame, target);
                bool targetIsTime = target is not null
                    && (target.Role == FrameRole.Time
                        || (target.NodeType == NodeTypes.RawData
                            && (target.Kind is MentionKind.Date or MentionKind.Time
                                || Lexicon.IsNumberOrDate(target.SurfaceText))));
                Guid? objectID = null;

                if (target is not null && chains[index].Last is not null)
                {
                    Guid targetID = MapGraphNodeID(chains[index].Last!.Value.ID, results[index]);
                    if (targetIsLocation)
                        locationID = targetID;
                    else if (!targetIsTime)
                        objectID = targetID;
                }

                if (location is not null)
                {
                    EntityResolution resolution = resolver.Resolve(location);
                    if (resolution.ResolvedNodeID.HasValue
                        && core.TryGetNode(resolution.ResolvedNodeID.Value, out _))
                    {
                        locationID = resolution.ResolvedNodeID;
                    }
                }

                FrameArgument? eventObject = target is not null && !targetIsLocation && !targetIsTime
                    ? target
                    : null;
                FrameArgument? eventLocation = location ?? (targetIsLocation ? target : null);
                string eventName = BuildEventName(sentence, frame, subject, eventObject, eventLocation);
                Web_Node? existing = core.FindMatchingEvent(
                    eventName,
                    startTime,
                    endTime,
                    subjectID,
                    frame.PredicateLemma,
                    objectID,
                    locationID);
                if (existing is not null)
                    continue;

                try
                {
                    core.CreateEvent(
                        eventName,
                        startTime,
                        endTime,
                        subjectID,
                        frame.PredicateLemma,
                        objectID,
                        locationID);
                    created++;
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"Temporal Event extraction was skipped: {ex.Message}");
                }
            }

            return created;
        }

        /// <summary>Builds a concise display name for an extracted event from its subject, verb, object, and location.</summary>
        private static string BuildEventName(
            Sentence sentence,
            RelationFrame frame,
            FrameArgument subject,
            FrameArgument? target,
            FrameArgument? location)
        {
            Token? verb = sentence.Tokens.FirstOrDefault(token =>
                token.TokenIndex >= frame.StartToken
                && token.TokenIndex <= frame.EndToken
                && token.Lemma.Equals(frame.PredicateLemma, StringComparison.OrdinalIgnoreCase)
                && token.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary);
            string action = verb?.Text ?? frame.PredicateLemma;
            string name = subject.SurfaceText + " " + action;
            if (target is not null)
                name += " " + target.SurfaceText;
            if (location is not null)
                name += " in " + location.SurfaceText;
            return name.Trim();
        }

        /// <summary>Checks whether a relation frame's target argument represents a spatial location.</summary>
        private static bool IsLocationTarget(RelationFrame frame, FrameArgument target)
        {
            if (target.Role is FrameRole.Location or FrameRole.Qualifier)
                return target.NodeType == NodeTypes.Locations;

            if (target.NodeType != NodeTypes.Locations)
                return false;

            string finalRelationPart = frame.RelationName.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault() ?? string.Empty;
            return RelationExtractor.IsSpatialPreposition(finalRelationPart);
        }

        /// <summary>Ensures a chain's anchor node exists in the graph and ingests the chain through reasoning.</summary>
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

        /// <summary>Checks whether a pronoun-bearing relation frame can be ingested using conversation context.</summary>
        private bool IsContextPronounFrame(RelationFrame frame)
        {
            return options.EnableConversationContext
                && options.EnablePronounResolution
                && !frame.Negated
                && frame.Modal is null
                && frame.Voice != Voice.Unknown
                && frame.Arguments.Any(argument => argument.IsPronoun);
        }

        /// <summary>Records session provenance entries for frames rejected due to discredited source trust.</summary>
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

        /// <summary>Builds a session provenance entry from an ingested chain, relation frame, and reasoning result.</summary>
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
                result.Diagnostics,
                frame.Coordination,
                frame.CoordinationGroup,
                frame.Negation);
        }

        /// <summary>Records conversational mentions and focus for a successfully ingested fact chain.</summary>
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

        /// <summary>Records conversational mentions and focus from an answered question and its selected node.</summary>
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

        /// <summary>Appends a conversation mention for an ingested chain endpoint node.</summary>
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

        /// <summary>Maps a transient chain node ID to its merged or matched graph node ID.</summary>
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

        /// <summary>Appends a conversation mention for a question anchor or answer node.</summary>
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

        /// <summary>Resolves the display label for a graph node ID or returns the supplied fallback.</summary>
        private string GetNodeLabel(Guid nodeID, string fallback)
        {
            if (!core.TryGetNode(nodeID, out Web_Node? node) || node is null)
                return fallback;

            if (node.Get_NodeType() == NodeTypes.Events)
                return EventNodeHelper.GetDisplayName(node);

            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            string? alias = node.Aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return alias ?? fallback;
        }

        /// <summary>Checks whether a reasoning status represents a successful fact ingestion.</summary>
        private static bool IsSuccessfulIngestion(ReasoningStatus status)
        {
            return status is ReasoningStatus.Answered or ReasoningStatus.Contradiction;
        }

        /// <summary>Maps a link classification to the semantic role of its target argument.</summary>
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

        /// <summary>Maps an answer kind to its corresponding conversational frame role.</summary>
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

        /// <summary>Maps an answer status to its top-level NLP pipeline status.</summary>
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

        /// <summary>Returns distinct non-empty diagnostic messages in their original order.</summary>
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

        /// <summary>Aggregates per-chain reasoning outcomes into a single ingestion status.</summary>
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
