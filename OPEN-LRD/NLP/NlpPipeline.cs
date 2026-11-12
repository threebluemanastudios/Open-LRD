using OPEN_LRD.Nodes;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    internal sealed class NlpPipeline
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;
        private readonly ReasoningEngine reasoning;

        public NlpPipeline(
            LRD_Core core,
            NlpOptions? options = null,
            ReasoningEngine? reasoning = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before creating an NLP pipeline.");

            this.options = options ?? new NlpOptions();
            this.reasoning = reasoning ?? new ReasoningEngine(core);
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

            EntityResolver resolver = new EntityResolver(core);
            QuestionAnalysis question = QuestionAnalyzer.Analyze(analysis.Parsed, resolver, options);

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

            NlpAnswer answer = AnswerGenerator.Generate(question, evaluations, core, options);
            diagnostics.AddRange(answer.Diagnostics);

            NlpStatus status = MapAnswerStatus(answer.Status);

            return new NlpResult(status, Deduplicate(diagnostics), analysis.Parsed)
            {
                Frames = analysis.Frames,
                Question = question,
                Answer = answer,
                ReasoningResults = evaluations.Select(evaluation => evaluation.Result).ToArray()
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

            List<string> diagnostics = new List<string>(analysis.Diagnostics);

            if (analysis.Frames.Count == 0)
            {
                diagnostics.Add("No supported relation frames were found.");
                return new NlpResult(NlpStatus.Unsupported, diagnostics, analysis.Parsed);
            }

            if (!analysis.Frames.Any(frame => frame.Ingestible))
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
                    return new NlpResult(NlpStatus.RejectedByTrust, diagnostics, analysis.Parsed)
                    {
                        Frames = analysis.Frames
                    };
                }
            }

            EntityResolver resolver = new EntityResolver(core);
            List<LinkedList<Chain_Node>> chains = new List<LinkedList<Chain_Node>>();

            foreach (RelationFrame frame in analysis.Frames)
            {
                if (!frame.Ingestible)
                    continue;

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

            NlpStatus status = AggregateStatus(results);

            return new NlpResult(status, diagnostics, analysis.Parsed)
            {
                Frames = analysis.Frames,
                Chains = chains,
                ReasoningResults = results
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
