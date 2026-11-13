using OPEN_LRD;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class NlpTests
    {
        public static void RunAll()
        {
            Program.Run("NLP pipeline parsing", TestNlpPipelineParsing);
            Program.Run("NLP normalization and determinism", TestNlpNormalizationAndDeterminism);
            Program.Run("NLP sentence and token boundaries", TestNlpSentenceAndTokenBoundaries);
            Program.Run("NLP part of speech and lemmas", TestNlpPartOfSpeechAndLemmas);
            Program.Run("NLP shallow parsing", TestNlpShallowParsing);
            Program.Run("NLP named entity recognition", TestNlpNamedEntityRecognition);
            Program.Run("NLP input limits", TestNlpInputLimits);
            Program.Run("NLP relation registry", TestNlpRelationRegistry);
            Program.Run("NLP relation extraction patterns", TestNlpRelationExtractionPatterns);
            Program.Run("NLP relation extraction exclusions", TestNlpRelationExtractionExclusions);
            Program.Run("NLP coordination and negation", TestNlpCoordinationAndNegation);
            Program.Run("NLP chain building", TestNlpChainBuilding);
            Program.Run("NLP reasoning ingestion", TestNlpReasoningIngestion);
            Program.Run("NLP ingestion safety", TestNlpIngestionSafety);
            Program.Run("NLP ingestion idempotence", TestNlpIngestionIdempotence);
            Program.Run("NLP question analysis", TestNlpQuestionAnalysis);
            Program.Run("NLP answer generation", TestNlpAnswerGeneration);
            Program.Run("NLP yes-no answers", TestNlpYesNoAnswers);
            Program.Run("NLP answer ambiguity and no-match", TestNlpAnswerAmbiguityAndNoMatch);
            Program.Run("NLP answer diagnostics", TestNlpAnswerDiagnostics);
            Program.Run("NLP question read-only", TestNlpQuestionReadOnly);
            Program.Run("NLP question determinism", TestNlpQuestionDeterminism);
            Program.Run("NLP temporary evidence", TestNlpTemporaryEvidence);
            Program.Run("NLP conversation pronoun ingestion", TestNlpConversationPronounIngestion);
            Program.Run("NLP conversation follow-up questions", TestNlpConversationFollowUpQuestions);
            Program.Run("NLP conversation ambiguity and sharing", TestNlpConversationAmbiguityAndSharing);
            Program.Run("NLP conversation registry", TestNlpConversationRegistry);
            Program.Run("NLP conversation focus", TestNlpConversationFocus);
            Program.Run("NLP session provenance", TestNlpSessionProvenance);
            Program.Run("NLP session provenance caveats", TestNlpSessionProvenanceCaveats);
            Program.Run("NLP conversation read-only and determinism", TestNlpConversationReadOnlyAndDeterminism);
            Program.Run("NLP first-person speaker identity", TestNlpFirstPersonSpeakerIdentity);
            Program.Run("NLP command recognition", TestNlpCommandRecognition);
            Program.Run("NLP command clarification and read-only", TestNlpCommandClarificationAndReadOnly);
            Program.Run("NLP workbench lifecycle", TestNlpWorkbenchLifecycle);
            Program.Run("NLP workbench operations", TestNlpWorkbenchOperations);
            Program.Run("NLP workbench state and stale sessions", TestNlpWorkbenchStateAndStaleSessions);
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static ParsedText Parse(NlpPipeline pipeline, string text)
        {
            NlpResult result = pipeline.Analyze(text);
            Assert(
                result.Status == NlpStatus.Parsed,
                $"expected parsed status for '{text}' but got {result.Status}: {string.Join("; ", result.Diagnostics)}");
            Assert(result.Parsed is not null, "parsed result should include parsed data");
            return result.Parsed!;
        }

        private static void TestNlpPipelineParsing()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult empty = pipeline.Analyze("   ");
            Assert(empty.Status == NlpStatus.InvalidInput, "blank input should be rejected");
            Assert(empty.Diagnostics.Count > 0, "blank input should include a diagnostic");

            NlpResult oversized = pipeline.Analyze(new string('a', 20001));
            Assert(oversized.Status == NlpStatus.InvalidInput, "oversized input should be rejected");
            Assert(oversized.Diagnostics.Count > 0, "oversized input should include a diagnostic");

            const string statement = "Alice works at Acme.";
            ParsedText parsed = Parse(pipeline, statement);

            Assert(parsed.OriginalText == statement, "original text should be preserved");
            Assert(parsed.NormalizedText == statement, "already-normal text should be unchanged");
            Assert(parsed.Sentences.Count == 1, "one statement should produce one sentence");
            Assert(parsed.Tokens.Count == 5, "statement should contain five tokens");
            Assert(parsed.Sentences[0].Type == SentenceType.Declarative, "statement should be declarative");

            AssertToken(parsed, 0, "Alice", PartOfSpeech.ProperNoun, "alice");
            AssertToken(parsed, 1, "works", PartOfSpeech.Verb, "work");
            AssertToken(parsed, 2, "at", PartOfSpeech.Preposition, "at");
            AssertToken(parsed, 3, "Acme", PartOfSpeech.ProperNoun, "acme");
            AssertToken(parsed, 4, ".", PartOfSpeech.Punctuation, ".");

            Assert(parsed.Tokens[0].StartOffset == 0, "first token should start at zero");
            Assert(parsed.Tokens[1].StartOffset == 6, "second token offset should account for the first word and space");
            Assert(parsed.Tokens[4].EndOffset == statement.Length, "last token should end at the input length");
            Assert(parsed.Tokens[4].TokenIndex == 4 && parsed.Tokens[4].SentenceIndex == 0, "token positions should be populated");

            ParsedText question = Parse(pipeline, "Did Alice visit Acme?");
            Assert(question.Sentences[0].Type == SentenceType.YesNoQuestion, "auxiliary-first question should be a yes/no question");
            AssertToken(question, 0, "Did", PartOfSpeech.Auxiliary, "do");
            AssertToken(question, 2, "visit", PartOfSpeech.Verb, "visit");

            ParsedText whQuestion = Parse(pipeline, "What did Alice see?");
            Assert(whQuestion.Sentences[0].Type == SentenceType.WhQuestion, "wh-first question should be a wh question");
            AssertToken(whQuestion, 0, "What", PartOfSpeech.WhWord, "what");
            AssertToken(whQuestion, 3, "see", PartOfSpeech.Verb, "see");

            ParsedText imperative = Parse(pipeline, "Please open the door.");
            Assert(imperative.Sentences[0].Type == SentenceType.Imperative, "please-first sentence should be imperative");
            AssertToken(imperative, 1, "open", PartOfSpeech.Verb, "open");

            ParsedText adjectives = Parse(pipeline, "The quick brown fox runs.");
            AssertToken(adjectives, 1, "quick", PartOfSpeech.Adjective, "quick");
            AssertToken(adjectives, 2, "brown", PartOfSpeech.Adjective, "brown");
            AssertToken(adjectives, 3, "fox", PartOfSpeech.Noun, "fox");
            AssertToken(adjectives, 4, "runs", PartOfSpeech.Verb, "run");

            AssertThrows<ArgumentNullException>(
                () => new NlpPipeline(null!),
                "a null core should be rejected");
        }

        private static void TestNlpNormalizationAndDeterminism()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            const string source = "  Alice\u00A0said \u201Chello\u201D.  ";
            ParsedText parsed = Parse(pipeline, source);

            Assert(parsed.OriginalText == source, "normalization should preserve the original text");
            Assert(parsed.NormalizedText == "Alice said \"hello\".", "normalization should map unicode spacing and quotes");

            ParsedText first = Parse(pipeline, "Alice works quickly.");
            ParsedText second = Parse(pipeline, "Alice works quickly.");

            Assert(Signature(first) == Signature(second), "the same input should produce deterministic parses");
        }

        private static void TestNlpSentenceAndTokenBoundaries()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            ParsedText parsed = Parse(pipeline, "Dr. Smith arrived at 3.14. He left.");

            Assert(parsed.Sentences.Count == 2, "abbreviations and decimals should not split sentences");
            Assert(parsed.Tokens.Count(token => token.Text == "3.14") == 1, "decimal values should stay in one token");
            Assert(
                parsed.Tokens.Single(token => token.Text == "3.14").PartOfSpeech == PartOfSpeech.Number,
                "decimal values should be tagged as numbers");
            Assert(parsed.Sentences[0].Tokens[0].Text == "Dr", "abbreviation should tokenize into a word and period");
            Assert(parsed.Sentences[1].Tokens[0].Normalized == "he", "second sentence should start after the decimal boundary");
            AssertToken(parsed, 0, "Dr", PartOfSpeech.ProperNoun, "dr");

            ParsedText newline = Parse(pipeline, "One.\nTwo.");
            Assert(newline.Sentences.Count == 2, "newlines should separate sentences");

            ParsedText contraction = Parse(pipeline, "Alice can't go.");
            AssertToken(contraction, 1, "can't", PartOfSpeech.Auxiliary, "can");
            AssertToken(contraction, 2, "go", PartOfSpeech.Verb, "go");

            ParsedText possessive = Parse(pipeline, "Alice's dog runs.");
            AssertToken(possessive, 0, "Alice's", PartOfSpeech.ProperNoun, "alice");
            AssertToken(possessive, 1, "dog", PartOfSpeech.Noun, "dog");
        }

        private static void TestNlpPartOfSpeechAndLemmas()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            ParsedText parsed = Parse(pipeline, "She was running quickly and the tired children slept.");

            AssertToken(parsed, 0, "She", PartOfSpeech.Pronoun, "she");
            AssertToken(parsed, 1, "was", PartOfSpeech.Auxiliary, "be");
            AssertToken(parsed, 2, "running", PartOfSpeech.Verb, "run");
            AssertToken(parsed, 3, "quickly", PartOfSpeech.Adverb, "quick");
            AssertToken(parsed, 4, "and", PartOfSpeech.Conjunction, "and");
            AssertToken(parsed, 5, "the", PartOfSpeech.Determiner, "the");
            AssertToken(parsed, 6, "tired", PartOfSpeech.Adjective, "tired");
            AssertToken(parsed, 7, "children", PartOfSpeech.Noun, "child");
            AssertToken(parsed, 8, "slept", PartOfSpeech.Verb, "sleep");

            ParsedText regular = Parse(pipeline, "Alice walked home and studied.");
            AssertToken(regular, 1, "walked", PartOfSpeech.Verb, "walk");
            AssertToken(regular, 4, "studied", PartOfSpeech.Verb, "study");

            ParsedText comparative = Parse(pipeline, "The biggest dogs ran.");
            AssertToken(comparative, 1, "biggest", PartOfSpeech.Adjective, "big");
            AssertToken(comparative, 2, "dogs", PartOfSpeech.Noun, "dog");
            AssertToken(comparative, 3, "ran", PartOfSpeech.Verb, "run");
        }

        private static void TestNlpShallowParsing()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            ParsedText statement = Parse(pipeline, "Alice works at Acme.");
            Clause clause = SingleClause(statement);

            AssertChunk(clause, ChunkKind.NounPhrase, 0, 0, "Alice should be a noun phrase");
            AssertChunk(clause, ChunkKind.VerbPhrase, 1, 1, "works should be a verb phrase");
            AssertChunk(clause, ChunkKind.PrepositionalPhrase, 2, 3, "at Acme should be a prepositional phrase");
            AssertChunk(clause, ChunkKind.NounPhrase, 3, 3, "Acme should be a noun phrase inside the prepositional phrase");
            AssertArgument(clause, ClauseRole.Subject, 0, 0, "Alice should be the subject");
            AssertArgument(clause, ClauseRole.Predicate, 1, 1, "works should be the predicate");
            AssertArgument(clause, ClauseRole.Qualifier, 2, 3, "at Acme should be a qualifier");
            Assert(!clause.Arguments.Any(argument => argument.Role == ClauseRole.Object), "a prepositional object should not become a direct object");

            ParsedText transitive = Parse(pipeline, "Alice sees Bob.");
            Clause transitiveClause = SingleClause(transitive);
            AssertArgument(transitiveClause, ClauseRole.Subject, 0, 0, "Alice should be the subject");
            AssertArgument(transitiveClause, ClauseRole.Predicate, 1, 1, "sees should be the predicate");
            AssertArgument(transitiveClause, ClauseRole.Object, 2, 2, "Bob should be the object");

            ParsedText copula = Parse(pipeline, "Alice is a doctor.");
            Clause copulaClause = SingleClause(copula);
            AssertArgument(copulaClause, ClauseRole.Predicate, 1, 1, "is should be the predicate");
            AssertArgument(copulaClause, ClauseRole.Complement, 2, 3, "a doctor should be the copula complement");

            ParsedText modified = Parse(pipeline, "The quick brown fox runs.");
            Clause modifiedClause = SingleClause(modified);
            AssertChunk(modifiedClause, ChunkKind.NounPhrase, 0, 3, "determiner, adjectives, and noun should form one noun phrase");
            AssertArgument(modifiedClause, ClauseRole.Subject, 0, 3, "the full noun phrase should be the subject");
            AssertArgument(modifiedClause, ClauseRole.Predicate, 4, 4, "runs should be the predicate");

            ParsedText coordinated = Parse(pipeline, "Alice works and Bob plays.");
            Assert(coordinated.Sentences[0].Clauses.Count == 2, "coordinated clauses with subjects should be split");
            AssertArgument(coordinated.Sentences[0].Clauses[0], ClauseRole.Subject, 0, 0, "first clause should keep Alice as subject");
            AssertArgument(coordinated.Sentences[0].Clauses[1], ClauseRole.Subject, 3, 3, "second clause should keep Bob as subject");
        }

        private static void TestNlpNamedEntityRecognition()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            ParsedText travel = Parse(pipeline, "Alice visited Paris in 2024-01-05.");
            AssertMention(travel, "Alice", "alice", NodeTypes.Entities, MentionKind.Person);
            AssertMention(travel, "Paris", "paris", NodeTypes.Locations, MentionKind.Location);
            AssertMention(travel, "2024-01-05", "2024-01-05", NodeTypes.RawData, MentionKind.Date);

            ParsedText organization = Parse(pipeline, "Acme Inc hired Bob on Monday.");
            AssertMention(organization, "Acme Inc", "inc", NodeTypes.Entities, MentionKind.Organization);
            AssertMention(organization, "Bob", "bob", NodeTypes.Entities, MentionKind.Person);
            AssertMention(organization, "Monday", "monday", NodeTypes.RawData, MentionKind.Date);

            ParsedText eventParsed = Parse(pipeline, "The meeting started.");
            AssertMention(eventParsed, "The meeting", "meeting", NodeTypes.Events, MentionKind.Event);

            ParsedText concept = Parse(pipeline, "The doctor arrived.");
            AssertMention(concept, "The doctor", "doctor", NodeTypes.Concepts, MentionKind.Concept);

            ParsedText quantity = Parse(pipeline, "The value is 42.");
            AssertMention(quantity, "42", "42", NodeTypes.RawData, MentionKind.Quantity);
        }

        private static Clause SingleClause(ParsedText parsed)
        {
            Assert(parsed.Sentences.Count > 0, "expected at least one sentence");
            Assert(parsed.Sentences[0].Clauses.Count == 1, "expected exactly one clause");
            return parsed.Sentences[0].Clauses[0];
        }

        private static void AssertChunk(
            Clause clause,
            ChunkKind kind,
            int startToken,
            int endToken,
            string message)
        {
            Assert(
                clause.Chunks.Any(chunk =>
                    chunk.Kind == kind
                    && chunk.StartToken == startToken
                    && chunk.EndToken == endToken),
                message);
        }

        private static void AssertArgument(
            Clause clause,
            ClauseRole role,
            int startToken,
            int endToken,
            string message)
        {
            Assert(
                clause.Arguments.Any(argument =>
                    argument.Role == role
                    && argument.StartToken == startToken
                    && argument.EndToken == endToken),
                message);
        }

        private static void AssertMention(
            ParsedText parsed,
            string surfaceText,
            string lemma,
            NodeTypes nodeType,
            MentionKind kind)
        {
            Mention? mention = parsed.Mentions.FirstOrDefault(candidate =>
                candidate.SurfaceText == surfaceText);

            Assert(mention is not null, $"missing mention '{surfaceText}'");
            Assert(mention!.Lemma == lemma, $"mention '{surfaceText}' should have lemma '{lemma}' but was '{mention.Lemma}'");
            Assert(
                mention.NodeType == nodeType,
                $"mention '{surfaceText}' should have type {nodeType} but was {mention.NodeType}");
            Assert(
                mention.Kind == kind,
                $"mention '{surfaceText}' should have kind {kind} but was {mention.Kind}");
        }

        private static void TestNlpInputLimits()
        {
            LRD_Core core = NewCore();

            NlpResult tooManySentences = new NlpPipeline(
                core,
                new NlpOptions { MaxSentences = 2 }).Analyze("One. Two. Three.");
            Assert(tooManySentences.Status == NlpStatus.InvalidInput, "sentence limit should reject the input");
            Assert(
                tooManySentences.Diagnostics.Any(message => message.Contains("sentences", StringComparison.Ordinal)),
                "sentence limit should produce a sentence diagnostic");

            NlpResult tooManyTokens = new NlpPipeline(
                core,
                new NlpOptions { MaxTokens = 2 }).Analyze("One two three.");
            Assert(tooManyTokens.Status == NlpStatus.InvalidInput, "token limit should reject the input");
            Assert(
                tooManyTokens.Diagnostics.Any(message => message.Contains("tokens", StringComparison.Ordinal)),
                "token limit should produce a token diagnostic");

            NlpResult tooLong = new NlpPipeline(
                core,
                new NlpOptions { MaxCharacters = 5 }).Analyze("abcdef");
            Assert(tooLong.Status == NlpStatus.InvalidInput, "character limit should reject the input");

            NlpResult punctuation = new NlpPipeline(core).Analyze("... !!! ???");
            Assert(punctuation.Status == NlpStatus.InvalidInput, "punctuation-only input should be rejected");
        }

        private static void TestNlpRelationRegistry()
        {
            RelationLinkType copula = RelationRegistry.GetOrRegister("is");

            Assert(copula.Id >= RelationRegistry.FirstPredicateLinkTypeId, "predicate IDs should stay above the reserved range");
            Assert(copula.Id > RelationRegistry.LastReservedOntologyId, "predicate IDs should not reuse ontology IDs");
            Assert(copula.Forward.Id == copula.Id, "forward link type should share the relation ID");
            Assert(copula.Inverse.Id == copula.Id, "inverse link type should share the relation ID");
            Assert(copula.InverseName == "inverse_is", "inverse relation name should use the conventional prefix");
            Assert(RelationRegistry.Normalize("was") == "is", "copula forms should normalize to is");
            Assert(RelationRegistry.Normalize("having") == "has", "possession forms should normalize to has");
            Assert(RelationRegistry.Normalize("did") == "does", "do forms should normalize to does");
            Assert(RelationRegistry.Normalize("work", "at") == "work_at", "verb particles should join with an underscore");

            RelationLinkType workAt = RelationRegistry.GetOrRegister(RelationRegistry.Normalize("work", "at"));
            RelationLinkType workAtAgain = RelationRegistry.GetOrRegister("work_at");

            Assert(ReferenceEquals(workAt, workAtAgain), "identical relation names should return the same registration");
            Assert(workAt.InverseName == "inverse_work_at", "particle relations should register an inverse name");
            Assert(RelationRegistry.TryGet("work_at", out RelationLinkType fetched), "registered relations should be retrievable by name");
            Assert(fetched.Id == workAt.Id, "name lookup should return the registered relation");
            Assert(RelationRegistry.TryGetById(workAt.Id, out RelationLinkType byId), "registered relations should be retrievable by ID");
            Assert(byId.Name == "work_at", "ID lookup should return the registered relation");
            Assert(RelationRegistry.TryGet("at", out RelationLinkType spatial), "spatial prepositions should be seeded");
            Assert(spatial.Id != workAt.Id, "distinct relations should have distinct IDs");

            Assert(
                RelationRegistry.TryGet(RelationRegistry.AntonymRelationName, out RelationLinkType antonym),
                "the antonym relation should be reserved for Module 4");
            Assert(antonym.Id == RelationRegistry.AntonymLinkTypeId, "the antonym relation should keep its reserved ID");
        }

        private static void TestNlpRelationExtractionPatterns()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult transitive = pipeline.Analyze("Alice sees Bob.");
            Assert(transitive.Status == NlpStatus.Parsed, "transitive input should parse");
            Assert(transitive.Frames.Count == 1, "transitive input should produce one frame");
            RelationFrame transitiveFrame = transitive.Frames[0];
            Assert(transitiveFrame.RelationName == "see", $"expected relation 'see' but got '{transitiveFrame.RelationName}'");
            Assert(transitiveFrame.LinkClass == LinkClass.actions, "transitive relations should use the actions class");
            Assert(transitiveFrame.Voice == Voice.Active, "transitive relations should be active voice");
            Assert(transitiveFrame.Ingestible, "transitive relations should be ingestible");
            AssertArgument(transitiveFrame, FrameRole.Subject, "Alice");
            AssertArgument(transitiveFrame, FrameRole.Object, "Bob");

            NlpResult verbPreposition = pipeline.Analyze("Alice works at Acme.");
            Assert(verbPreposition.Frames.Count == 1, "verb plus preposition should produce one frame");
            RelationFrame workAtFrame = verbPreposition.Frames[0];
            Assert(workAtFrame.RelationName == "work_at", $"expected relation 'work_at' but got '{workAtFrame.RelationName}'");
            AssertArgument(workAtFrame, FrameRole.Object, "Acme");
            AssertArgument(workAtFrame, FrameRole.Location, "Acme");

            NlpResult copula = pipeline.Analyze("Alice is a doctor.");
            Assert(copula.Frames.Count == 1, "copula input should produce one frame");
            RelationFrame copulaFrame = copula.Frames[0];
            Assert(copulaFrame.RelationName == "is", $"expected relation 'is' but got '{copulaFrame.RelationName}'");
            Assert(copulaFrame.LinkClass == LinkClass.identities, "copula relations should use the identities class");
            AssertArgument(copulaFrame, FrameRole.Object, "a doctor");

            NlpResult possession = pipeline.Analyze("Alice has a dog.");
            Assert(possession.Frames.Count == 1, "possession input should produce one frame");
            RelationFrame possessionFrame = possession.Frames[0];
            Assert(possessionFrame.RelationName == "has", $"expected relation 'has' but got '{possessionFrame.RelationName}'");
            AssertArgument(possessionFrame, FrameRole.Object, "a dog");

            NlpResult coordinated = pipeline.Analyze("Alice sees Bob and Carol.");
            Assert(coordinated.Frames.Count == 2, "coordinated objects should produce one frame per object");
            Assert(
                coordinated.Frames.Any(frame => frame.Arguments.Any(
                    argument => argument.Role == FrameRole.Object && argument.SurfaceText == "Bob")),
                "the first coordinated object should be extracted");
            Assert(
                coordinated.Frames.Any(frame => frame.Arguments.Any(
                    argument => argument.Role == FrameRole.Object && argument.SurfaceText == "Carol")),
                "the second coordinated object should be extracted");

            NlpResult passive = pipeline.Analyze("Bob was seen by Alice.");
            Assert(passive.Frames.Count == 1, "passive input should produce one frame");
            RelationFrame passiveFrame = passive.Frames[0];
            Assert(passiveFrame.Voice == Voice.Passive, "passive input should be marked passive");
            Assert(passiveFrame.RelationName == "see", $"expected passive relation 'see' but got '{passiveFrame.RelationName}'");
            AssertArgument(passiveFrame, FrameRole.Subject, "Alice");
            AssertArgument(passiveFrame, FrameRole.Object, "Bob");
            Assert(
                !passiveFrame.Arguments.Any(argument =>
                    argument.Role == FrameRole.Location
                    && argument.SurfaceText == "Alice"),
                "the passive agent should not also be a location qualifier");
        }

        private static void TestNlpRelationExtractionExclusions()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult negated = pipeline.Analyze("Alice does not see Bob.");
            Assert(negated.Frames.Count == 1, "negated input should still produce a frame");
            Assert(negated.Frames[0].Negated, "negation should be recorded");
            Assert(!negated.Frames[0].Ingestible, "negated relations should not be ingested");
            Assert(
                negated.Diagnostics.Any(message => message.Contains("negated", StringComparison.OrdinalIgnoreCase)),
                "negation should produce a diagnostic");

            NlpResult modal = pipeline.Analyze("Alice can see Bob.");
            Assert(modal.Frames.Count == 1, "modal input should still produce a frame");
            Assert(modal.Frames[0].Modal == "can", "the modal should be recorded");
            Assert(!modal.Frames[0].Ingestible, "modal relations should not be ingested");

            NlpResult pronoun = pipeline.Analyze("She sees Bob.");
            Assert(pronoun.Frames.Count == 1, "pronoun input should still produce a frame");
            Assert(!pronoun.Frames[0].Ingestible, "pronoun subjects should not be ingested");

            NlpResult intransitive = pipeline.Analyze("Alice sleeps.");
            Assert(intransitive.Frames.Count == 0, "intransitive input should not produce a relation frame");
            Assert(
                intransitive.Diagnostics.Any(message => message.Contains("Intransitive", StringComparison.OrdinalIgnoreCase)),
                "intransitive input should produce a diagnostic");

            NlpResult question = pipeline.Analyze("Did Alice see Bob?");
            Assert(question.Frames.Count == 0, "questions should not be ingested as assertions");

            NlpResult imperative = pipeline.Analyze("Please open the door.");
            Assert(imperative.Frames.Count == 0, "imperative input should not be ingested as assertions");
        }

        private static void TestNlpCoordinationAndNegation()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult subjectCoordination = pipeline.Analyze("Alice and Bob play chess.");
            Assert(subjectCoordination.Frames.Count == 2, "coordinated subjects should produce one frame per subject");
            Assert(
                subjectCoordination.Frames.Any(frame => frame.Arguments.Any(
                    argument => argument.Role == FrameRole.Subject && argument.SurfaceText == "Alice")),
                "the first coordinated subject should be extracted");
            Assert(
                subjectCoordination.Frames.Any(frame => frame.Arguments.Any(
                    argument => argument.Role == FrameRole.Subject && argument.SurfaceText == "Bob")),
                "the second coordinated subject should be extracted");
            Assert(
                subjectCoordination.Frames.All(frame => frame.Coordination == CoordinationKind.Conjunction),
                "subject coordination should be marked as a conjunction");

            NlpResult verbCoordination = pipeline.Analyze("Alice buys and sells a car.");
            Assert(verbCoordination.Frames.Count == 2, "coordinated verbs should produce one frame per verb");
            Assert(
                verbCoordination.Frames.Any(frame => frame.RelationName == "buy"),
                "the first coordinated verb should be extracted");
            Assert(
                verbCoordination.Frames.Any(frame => frame.RelationName == "sell"),
                "the second coordinated verb should be extracted");

            NlpResult disjunction = pipeline.Analyze("Alice sees Bob or Carol.");
            Assert(disjunction.Frames.Count == 2, "disjunction should produce one frame per alternative");
            Assert(
                disjunction.Frames.All(frame => frame.Coordination == CoordinationKind.Disjunction),
                "object alternatives should be marked as disjunctions");
            Assert(
                disjunction.Frames.Select(frame => frame.CoordinationGroup).Distinct().Count() == 1,
                "disjunction alternatives should share a coordination group");

            NlpResult combined = pipeline.Analyze("Alice and Bob see Carol or Dave.");
            Assert(combined.Frames.Count == 4, "combined subject and object coordination should produce a cross product");
            Assert(
                combined.Frames.All(frame => frame.Coordination == CoordinationKind.Disjunction),
                "combined coordination should be marked as a disjunction");

            NlpResult negatedComplement = pipeline.Analyze("Alice is not hot.");
            Assert(negatedComplement.Frames.Count == 1, "negated complement should produce one frame");
            RelationFrame negatedFrame = negatedComplement.Frames[0];
            Assert(negatedFrame.Negation == NegationKind.Complement, "complement negation should be recorded");
            Assert(negatedFrame.Ingestible, "negated complements should be ingestible");
            AssertArgument(negatedFrame, FrameRole.Object, "not hot");

            NlpResult negatedLocation = pipeline.Analyze("The cat is not inside the house.");
            Assert(negatedLocation.Frames.Count == 1, "negated location complement should produce one frame");
            Assert(
                negatedLocation.Frames[0].Negation == NegationKind.Complement,
                "negated location should be recorded as a complement negation");
            AssertArgument(negatedLocation.Frames[0], FrameRole.Object, "not inside");

            LRD_Core ingestCore = NewCore();
            NlpPipeline ingestPipeline = new NlpPipeline(ingestCore);
            Assert(
                ingestPipeline.Ingest("Alice is not hot.").Status == NlpStatus.Ingested,
                "negated complement should ingest");
            Assert(
                ingestCore.FindByLabel("not hot").Any(),
                "a negated concept node should be created");
            Assert(
                !ingestCore.FindByLabel("hot").Any(),
                "the negated concept should not merge with the positive concept");

            NlpResult negatedVerb = pipeline.Analyze("Alice does not see Bob.");
            Assert(negatedVerb.Frames.Count == 1, "negated verb should still produce a frame");
            Assert(negatedVerb.Frames[0].Negation == NegationKind.Predicate, "verb negation should be recorded");
            Assert(!negatedVerb.Frames[0].Ingestible, "negated verbs should not be ingested");
        }

        private static void TestNlpChainBuilding()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            NlpResult analysis = pipeline.Analyze("Alice works at Acme.");
            RelationFrame frame = analysis.Frames[0];

            ChainBuildResult built = ChainBuilder.Build(frame, new EntityResolver(core));
            Assert(built.Chains.Count == 2, $"expected a main chain and a qualifier chain but got {built.Chains.Count}");

            LinkedList<Chain_Node> main = built.Chains[0];
            Assert(main.Count == 2, "the main chain should contain a subject and object");
            Link mainLink = main.First!.Value.Get_LinkNext()!;
            RelationLinkType workAt = RelationRegistry.GetOrRegister("work_at");
            Assert(mainLink.SourceID == main.First.Value.ID, "link source should be the chain root");
            Assert(mainLink.DestinationID == main.Last!.Value.ID, "link destination should be the chain target");
            Assert(mainLink.ForwardLink.Id == workAt.Id, "forward link should use the relation ID");
            Assert(mainLink.InverseLink.Id == workAt.Id, "inverse link should use the relation ID");
            Assert(main.Last.Value.Get_LinkPrevious() == mainLink, "the target should point back to the link");

            LinkedList<Chain_Node> qualifier = built.Chains[1];
            Link qualifierLink = qualifier.First!.Value.Get_LinkNext()!;
            Assert(qualifierLink.Classification == LinkClass.spatial, "location qualifiers should be spatial");
            Assert(qualifierLink.ForwardLink.Name == "at", "the qualifier relation should use its preposition");
            Assert(
                qualifierLink.DestinationID != qualifierLink.SourceID,
                "chain links must not be self-loops");
        }

        private static void TestNlpReasoningIngestion()
        {
            LRD_Core core = NewCore();
            Guid alice = AddNode(core, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult result = pipeline.Ingest("Alice sees Bob.");
            Assert(result.Status == NlpStatus.Ingested, $"expected Ingested but got {result.Status}: {string.Join("; ", result.Diagnostics)}");
            Assert(result.Chains.Count == 1, "one supported relation should produce one chain");
            Assert(result.ReasoningResults.Count == 1, "each chain should be ingested by the reasoning engine");
            Assert(result.ReasoningResults[0].Status == ReasoningStatus.Answered, "ingestion should answer");
            Assert(core.FindByLabel("Alice", NodeTypes.Entities).Count == 1, "the existing Alice node should be reused");
            Assert(core.TryGetNode(alice, out Web_Node? aliceNode) && aliceNode!.IsPermanent, "the existing Alice node should remain permanent");

            IReadOnlyList<Web_Node> bobNodes = core.FindByLabel("Bob", NodeTypes.Entities);
            Assert(bobNodes.Count == 1, "Bob should be created once");
            Assert(
                core.GetLinks(alice).Any(link =>
                    link.DestinationID == bobNodes[0].ID
                    && link.ForwardLink.Name == "see"),
                "the relation link should be added to the graph");
        }

        private static void TestNlpIngestionSafety()
        {
            LRD_Core core = NewCore();
            Guid source = AddNode(core, NodeTypes.Sources, "BadSource", NodeLifetime.Permanent);
            ReasoningEngine engine = new ReasoningEngine(core);
            engine.RecordTrustEvidence(source, null, false, 100);

            NlpPipeline rejected = new NlpPipeline(core, reasoning: engine);
            NlpResult rejectedResult = rejected.Ingest("Alice sees Bob.", source);
            Assert(rejectedResult.Status == NlpStatus.RejectedByTrust, $"expected RejectedByTrust but got {rejectedResult.Status}");
            Assert(rejectedResult.Chains.Count == 0, "a discredited source should not build chains");
            Assert(core.FindByLabel("Alice").Count == 0, "a discredited source should not create an anchor node");
            Assert(core.FindByLabel("Bob").Count == 0, "a discredited source should not create target nodes");

            LRD_Core trustedCore = NewCore();
            Guid trustedSource = AddNode(trustedCore, NodeTypes.Sources, "GoodSource", NodeLifetime.Permanent);
            ReasoningEngine trustedEngine = new ReasoningEngine(trustedCore);
            trustedEngine.RecordTrustEvidence(trustedSource, null, true, 50);

            NlpPipeline trusted = new NlpPipeline(trustedCore, reasoning: trustedEngine);
            NlpResult trustedResult = trusted.Ingest("Alice sees Bob.", trustedSource);
            Assert(trustedResult.Status == NlpStatus.Ingested, $"expected Ingested for trusted source but got {trustedResult.Status}");

            IReadOnlyList<Web_Node> trustedBob = trustedCore.FindByLabel("Bob", NodeTypes.Entities);
            Assert(trustedBob.Count == 1, "trusted ingestion should create Bob");
            Assert(trustedBob[0].IsPermanent, "trusted ingestion should promote new nodes to permanent");
            Assert(
                trustedResult.ReasoningResults[0].PromotedNodeIDs.Contains(trustedBob[0].ID),
                "promotion should be reported for the new node");

            LRD_Core ambiguousCore = NewCore();
            AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            NlpPipeline ambiguous = new NlpPipeline(ambiguousCore);
            NlpResult ambiguousResult = ambiguous.Ingest("Alice sees Bob.");
            Assert(ambiguousResult.Status == NlpStatus.Unsupported, $"expected Unsupported for ambiguity but got {ambiguousResult.Status}");
            Assert(ambiguousResult.Chains.Count == 0, "ambiguous endpoints should not build chains");
            Assert(ambiguousCore.FindByLabel("Alice").Count == 0, "ambiguous ingestion should not create an anchor");
            Assert(
                ambiguousResult.Diagnostics.Any(message => message.Contains("Ambiguous", StringComparison.OrdinalIgnoreCase)),
                "ambiguity should produce a diagnostic");

            LRD_Core pronounCore = NewCore();
            NlpPipeline pronounPipeline = new NlpPipeline(pronounCore);
            NlpResult pronounResult = pronounPipeline.Ingest("She sees Bob.");
            Assert(pronounResult.Status == NlpStatus.Unsupported, $"expected Unsupported for pronouns but got {pronounResult.Status}");
            Assert(pronounCore.FindByLabel("Bob").Count == 0, "pronoun ingestion should not create nodes");
        }

        private static void TestNlpIngestionIdempotence()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult first = pipeline.Ingest("Alice sees Bob.");
            Assert(first.Status == NlpStatus.Ingested, $"first ingestion failed with {first.Status}");

            int countAfterFirst = core.GetDictionary()!.Count;
            IReadOnlyList<Web_Node> aliceFirst = core.FindByLabel("Alice", NodeTypes.Entities);
            IReadOnlyList<Web_Node> bobFirst = core.FindByLabel("Bob", NodeTypes.Entities);
            Assert(aliceFirst.Count == 1 && bobFirst.Count == 1, "the first ingestion should create one node per entity");

            NlpResult second = pipeline.Ingest("Alice sees Bob.");
            Assert(second.Status == NlpStatus.Ingested, $"second ingestion failed with {second.Status}");
            Assert(core.GetDictionary()!.Count == countAfterFirst, "repeated ingestion should not create duplicate nodes");
            Assert(core.FindByLabel("Alice", NodeTypes.Entities).Count == 1, "Alice should remain unique");
            Assert(core.FindByLabel("Bob", NodeTypes.Entities).Count == 1, "Bob should remain unique");
            Assert(
                core.GetLinks(aliceFirst[0].ID).Count(link => link.DestinationID == bobFirst[0].ID) == 1,
                "repeated ingestion should not duplicate links");
        }

        private static QuestionAnalysis AnalyzeQuestion(
            NlpPipeline pipeline,
            LRD_Core core,
            string text)
        {
            ParsedText parsed = Parse(pipeline, text);
            return QuestionAnalyzer.Analyze(parsed, new EntityResolver(core));
        }

        private static void TestNlpQuestionAnalysis()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            QuestionAnalysis whoWorks = AnalyzeQuestion(pipeline, core, "Who works at Acme?");
            Assert(whoWorks.Type == QuestionType.WhSubject, $"expected WhSubject but got {whoWorks.Type}");
            Assert(whoWorks.Wh == WhKind.Who, $"expected Who but got {whoWorks.Wh}");
            Assert(whoWorks.ExpectedAnswer == AnswerKind.Entity, "who questions should expect an entity");
            Assert(whoWorks.AnchorRole == FrameRole.Qualifier, "the anchor of 'Who works at Acme?' should be a qualifier");
            Assert(whoWorks.RelationName == "work_at", $"expected work_at but got {whoWorks.RelationName}");
            Assert(
                whoWorks.Variants.Any(variant => variant.ExpectedAnswerType == NodeTypes.Entities),
                "who questions should include an Entities variant");
            Assert(whoWorks.Variants.Count > 0, "the question should produce variants");

            QuestionVariant first = whoWorks.Variants[0];
            Assert(first.Chain.First!.Value.Label is null, "the wh query node should have no label");
            Assert(
                string.Equals(first.Chain.Last!.Value.Label, "Acme", StringComparison.OrdinalIgnoreCase),
                "the terminal chain node should be the known anchor");
            Assert(first.Chain.First!.Value.ID != first.Chain.Last!.Value.ID, "query chains must not contain self-loops");

            QuestionAnalysis whatSaw = AnalyzeQuestion(pipeline, core, "What did Alice see?");
            Assert(whatSaw.Type == QuestionType.WhObject, $"expected WhObject but got {whatSaw.Type}");
            Assert(
                string.Equals(whatSaw.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the wh-object anchor should be Alice");
            Assert(whatSaw.RelationName == "see", $"expected see but got {whatSaw.RelationName}");
            Assert(
                whatSaw.Variants.Any(variant => variant.ExpectedAnswerType == NodeTypes.Entities)
                    && whatSaw.Variants.Any(variant => variant.ExpectedAnswerType == NodeTypes.Concepts),
                "what questions should include Entities and Concepts variants");

            RelationLinkType see = RelationRegistry.GetOrRegister("see");
            QuestionVariant seeVariant = whatSaw.Variants.First(variant =>
                variant.RelationName == "see" && variant.ExpectedAnswerType == NodeTypes.Entities);
            Link seeLink = seeVariant.Chain.First!.Value.Get_LinkNext()!;
            Assert(
                seeLink.ForwardLink.Id == see.Inverse.Id,
                "wh-object chains should use the inverse relation orientation");
            Assert(
                string.Equals(seeLink.InverseLink.Name, "inverse_see", StringComparison.OrdinalIgnoreCase),
                $"expected inverse_see but got {seeLink.InverseLink.Name}");

            QuestionAnalysis whereWorks = AnalyzeQuestion(pipeline, core, "Where does Alice work?");
            Assert(whereWorks.Type == QuestionType.WhQualifier, $"expected WhQualifier but got {whereWorks.Type}");
            Assert(
                string.Equals(whereWorks.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the where-question anchor should be Alice");
            Assert(
                whereWorks.Variants.Any(variant => variant.RelationName == "work_at"),
                "where questions should include the verb+preposition relation");
            Assert(
                whereWorks.Variants.Any(variant => variant.RelationName == "at"),
                "where questions should include the bare preposition relation");
            Assert(
                whereWorks.Variants.Any(variant => variant.ExpectedAnswerType == NodeTypes.Locations)
                    && whereWorks.Variants.Any(variant => variant.ExpectedAnswerType == NodeTypes.Entities),
                "where questions should include Locations and Entities types");

            QuestionAnalysis isDoctor = AnalyzeQuestion(pipeline, core, "Is Alice a doctor?");
            Assert(isDoctor.Type == QuestionType.YesNo, $"expected YesNo but got {isDoctor.Type}");
            Assert(
                string.Equals(isDoctor.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the yes/no subject should be Alice");
            Assert(
                isDoctor.Object?.SurfaceText.Contains("doctor", StringComparison.OrdinalIgnoreCase) == true,
                "the yes/no complement should contain doctor");
            Assert(isDoctor.RelationName == "is", $"expected is but got {isDoctor.RelationName}");
            Assert(isDoctor.LinkClass == LinkClass.identities, "copula questions should use identities");

            QuestionAnalysis doesWork = AnalyzeQuestion(pipeline, core, "Does Alice work at Acme?");
            Assert(doesWork.Type == QuestionType.YesNo, $"expected YesNo but got {doesWork.Type}");
            Assert(
                string.Equals(doesWork.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the yes/no subject should be Alice");
            Assert(
                string.Equals(doesWork.Object?.SurfaceText, "Acme", StringComparison.OrdinalIgnoreCase),
                "the yes/no object should be Acme");
            Assert(doesWork.RelationName == "work_at", $"expected work_at but got {doesWork.RelationName}");

            QuestionAnalysis passive = AnalyzeQuestion(pipeline, core, "Who was seen by Alice?");
            Assert(passive.IsPassive, "the question should be detected as passive");
            Assert(passive.RelationName == "see", $"expected see but got {passive.RelationName}");
            Assert(
                string.Equals(passive.AnchorNodeID is null ? passive.Subject?.SurfaceText : "Alice", "Alice", StringComparison.OrdinalIgnoreCase),
                "the passive agent should be Alice");
            Assert(passive.Variants.Count > 0, "the passive question should produce variants");
            Assert(passive.Variants[0].AnswerRole == FrameRole.Object, "the passive wh should answer the object role");

            QuestionAnalysis piedPiping = AnalyzeQuestion(pipeline, core, "To whom did Alice speak?");
            Assert(piedPiping.RelationName == "speak_to", $"expected speak_to but got {piedPiping.RelationName}");
            Assert(
                string.Equals(piedPiping.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the pied-piping anchor should be Alice");

            QuestionAnalysis stranded = AnalyzeQuestion(pipeline, core, "Who does Alice work with?");
            Assert(stranded.RelationName == "work_with", $"expected work_with but got {stranded.RelationName}");
            Assert(
                string.Equals(stranded.Subject?.SurfaceText, "Alice", StringComparison.OrdinalIgnoreCase),
                "the stranded-preposition anchor should be Alice");
        }

        private static void TestNlpAnswerGeneration()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult works = pipeline.Ingest("Alice works at Acme.");
            Assert(works.Status == NlpStatus.Ingested, $"ingestion failed with {works.Status}: {string.Join("; ", works.Diagnostics)}");

            Guid alice = core.FindByLabel("Alice", NodeTypes.Entities).Single().ID;
            NlpResult who = pipeline.Ask("Who works at Acme?");
            Assert(who.Status == NlpStatus.Answered, $"expected Answered but got {who.Status}: {string.Join("; ", who.Diagnostics)}");
            Assert(who.Answer is not null, "answered questions should include an answer");
            Assert(who.Answer!.Status == AnswerStatus.Answered, $"expected Answered status but got {who.Answer.Status}");
            Assert(who.Answer.Kind == AnswerKind.Entity, $"expected Entity but got {who.Answer.Kind}");
            Assert(who.Answer.ShortAnswer == "Alice", $"expected Alice but got {who.Answer.ShortAnswer}");
            Assert(who.Answer.AnswerNodeID == alice, "the answer node should be the ingested Alice node");
            Assert(who.Answer.Confidence > 0.0, "answered questions should have positive confidence");

            NlpResult where = pipeline.Ask("Where does Alice work?");
            Assert(where.Status == NlpStatus.Answered, $"expected Answered but got {where.Status}: {string.Join("; ", where.Diagnostics)}");
            Assert(where.Answer?.ShortAnswer == "Acme", $"expected Acme but got {where.Answer?.ShortAnswer}");

            LRD_Core seeCore = NewCore();
            NlpPipeline seePipeline = new NlpPipeline(seeCore);
            Assert(seePipeline.Ingest("Alice sees Bob.").Status == NlpStatus.Ingested, "sees ingestion failed");

            NlpResult what = seePipeline.Ask("What did Alice see?");
            Assert(what.Status == NlpStatus.Answered, $"expected Answered but got {what.Status}: {string.Join("; ", what.Diagnostics)}");
            Assert(what.Answer?.ShortAnswer == "Bob", $"expected Bob but got {what.Answer?.ShortAnswer}");

            NlpResult whom = seePipeline.Ask("Whom did Alice see?");
            Assert(whom.Answer?.ShortAnswer == "Bob", $"expected Bob but got {whom.Answer?.ShortAnswer}");

            LRD_Core copulaCore = NewCore();
            NlpPipeline copulaPipeline = new NlpPipeline(copulaCore);
            Assert(copulaPipeline.Ingest("Alice is a doctor.").Status == NlpStatus.Ingested, "copula ingestion failed");

            NlpResult copula = copulaPipeline.Ask("What is Alice?");
            Assert(copula.Status == NlpStatus.Answered, $"expected Answered but got {copula.Status}: {string.Join("; ", copula.Diagnostics)}");
            Assert(
                copula.Answer?.ShortAnswer?.Contains("doctor", StringComparison.OrdinalIgnoreCase) == true,
                $"expected doctor but got {copula.Answer?.ShortAnswer}");

            LRD_Core timeCore = NewCore();
            NlpPipeline timePipeline = new NlpPipeline(timeCore);
            Assert(timePipeline.Ingest("Alice arrived on Monday.").Status == NlpStatus.Ingested, "time ingestion failed");

            NlpResult time = timePipeline.Ask("When did Alice arrive?");
            Assert(time.Status == NlpStatus.Answered, $"expected Answered but got {time.Status}: {string.Join("; ", time.Diagnostics)}");
            Assert(
                time.Answer?.ShortAnswer?.Contains("Monday", StringComparison.OrdinalIgnoreCase) == true,
                $"expected Monday but got {time.Answer?.ShortAnswer}");

            LRD_Core passiveCore = NewCore();
            NlpPipeline passivePipeline = new NlpPipeline(passiveCore);
            Assert(passivePipeline.Ingest("Bob was seen by Alice.").Status == NlpStatus.Ingested, "passive ingestion failed");

            NlpResult passive = passivePipeline.Ask("Who was seen by Alice?");
            Assert(passive.Status == NlpStatus.Answered, $"expected Answered but got {passive.Status}: {string.Join("; ", passive.Diagnostics)}");
            Assert(passive.Answer?.ShortAnswer == "Bob", $"expected Bob but got {passive.Answer?.ShortAnswer}");
        }

        private static void TestNlpYesNoAnswers()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            Assert(pipeline.Ingest("Alice is a doctor.").Status == NlpStatus.Ingested, "copula ingestion failed");

            NlpResult yes = pipeline.Ask("Is Alice a doctor?");
            Assert(yes.Status == NlpStatus.Answered, $"expected Answered but got {yes.Status}: {string.Join("; ", yes.Diagnostics)}");
            Assert(yes.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {yes.Answer?.Status}");
            Assert(yes.Answer?.ShortAnswer == "Yes", $"expected Yes but got {yes.Answer?.ShortAnswer}");
            Assert(yes.Answer?.Kind == AnswerKind.Boolean, "yes/no answers should use the Boolean kind");

            NlpResult no = pipeline.Ask("Is Alice a teacher?");
            Assert(no.Status == NlpStatus.Answered, $"expected Answered but got {no.Status}: {string.Join("; ", no.Diagnostics)}");
            Assert(no.Answer?.Status == AnswerStatus.No, $"expected No but got {no.Answer?.Status}");
            Assert(no.Answer?.ShortAnswer == "No", $"expected No but got {no.Answer?.ShortAnswer}");
            Assert(
                no.Answer?.Text.Contains("don't have a fact", StringComparison.OrdinalIgnoreCase) == true,
                "negative answers should state that no matching fact exists");

            LRD_Core workCore = NewCore();
            NlpPipeline workPipeline = new NlpPipeline(workCore);
            Assert(workPipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "work ingestion failed");

            NlpResult workYes = workPipeline.Ask("Does Alice work at Acme?");
            Assert(workYes.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {workYes.Answer?.Status}: {string.Join("; ", workYes.Diagnostics)}");

            NlpResult workNo = workPipeline.Ask("Does Alice work at Globex?");
            Assert(workNo.Answer?.Status == AnswerStatus.No, $"expected No but got {workNo.Answer?.Status}: {string.Join("; ", workNo.Diagnostics)}");

            LRD_Core dogCore = NewCore();
            NlpPipeline dogPipeline = new NlpPipeline(dogCore);
            Assert(dogPipeline.Ingest("Alice has a dog.").Status == NlpStatus.Ingested, "have ingestion failed");

            NlpResult dog = dogPipeline.Ask("Does Alice have a dog?");
            Assert(
                dog.Answer?.Status == AnswerStatus.Yes,
                $"expected Yes but got {dog.Answer?.Status}: {string.Join("; ", dog.Diagnostics)}");
        }

        private static void TestNlpAnswerAmbiguityAndNoMatch()
        {
            LRD_Core ambiguousCore = NewCore();
            Guid alice = AddNode(ambiguousCore, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            Guid bob1 = AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            Guid bob2 = AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            RelationLinkType see = RelationRegistry.GetOrRegister("see");
            Assert(
                ambiguousCore.AddDirectedConnection(alice, bob1, LinkClass.actions, see.Forward.Id),
                "the first see link should be added");
            Assert(
                ambiguousCore.AddDirectedConnection(alice, bob2, LinkClass.actions, see.Forward.Id),
                "the second see link should be added");

            NlpPipeline ambiguous = new NlpPipeline(ambiguousCore);
            NlpResult ambiguousAnswer = ambiguous.Ask("What did Alice see?");
            Assert(
                ambiguousAnswer.Answer?.Status == AnswerStatus.Ambiguous,
                $"expected Ambiguous but got {ambiguousAnswer.Answer?.Status}: {string.Join("; ", ambiguousAnswer.Diagnostics)}");
            Assert(ambiguousAnswer.Answer?.ShortAnswer is null, "ambiguous answers should not have a short answer");
            Assert(
                ambiguousAnswer.Answer?.Text.Contains("multiple", StringComparison.OrdinalIgnoreCase) == true,
                "ambiguous answers should explain that multiple answers were found");

            LRD_Core noMatchCore = NewCore();
            AddNode(noMatchCore, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            AddNode(noMatchCore, NodeTypes.Entities, "Acme", NodeLifetime.Permanent);
            NlpPipeline noMatch = new NlpPipeline(noMatchCore);

            NlpResult noFact = noMatch.Ask("Who works at Acme?");
            Assert(noFact.Answer?.Status == AnswerStatus.NoMatch, $"expected NoMatch but got {noFact.Answer?.Status}");
            Assert(noFact.Answer?.ShortAnswer is null, "no-match answers should not have a short answer");

            NlpResult noBoolean = noMatch.Ask("Does Alice work at Acme?");
            Assert(noBoolean.Answer?.Status == AnswerStatus.No, $"expected No but got {noBoolean.Answer?.Status}");

            NlpResult unknownAnchor = noMatch.Ask("Who works at Globex?");
            Assert(
                unknownAnchor.Answer?.Status == AnswerStatus.NoMatch,
                $"expected NoMatch but got {unknownAnchor.Answer?.Status}: {string.Join("; ", unknownAnchor.Diagnostics)}");
        }

        private static void TestNlpAnswerDiagnostics()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "ingestion failed");

            NlpResult answer = pipeline.Ask("Who works at Acme?");
            Assert(
                answer.Diagnostics.Any(message => message.Contains("Selected answer", StringComparison.Ordinal)),
                "answer diagnostics should describe the selected answer");

            LRD_Core pronounCore = NewCore();
            NlpPipeline pronounPipeline = new NlpPipeline(pronounCore);
            NlpResult pronoun = pronounPipeline.Ask("Where does she work?");
            Assert(pronoun.Status == NlpStatus.Unsupported, $"expected Unsupported but got {pronoun.Status}");
            Assert(
                pronoun.Diagnostics.Any(message =>
                    message.Contains("antecedent", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("conversation context", StringComparison.OrdinalIgnoreCase)),
                "pronoun questions should include a no-antecedent or context diagnostic");

            NlpResult modal = pipeline.Ask("Can Alice see Bob?");
            Assert(modal.Status == NlpStatus.Unsupported, $"expected Unsupported but got {modal.Status}");
            Assert(
                modal.Diagnostics.Any(message => message.Contains("Modal", StringComparison.OrdinalIgnoreCase)),
                "modal questions should include a modal diagnostic");

            NlpResult negated = pipeline.Ask("Does Alice not see Bob?");
            Assert(negated.Status == NlpStatus.Unsupported, $"expected Unsupported but got {negated.Status}");
            Assert(
                negated.Diagnostics.Any(message => message.Contains("Negated", StringComparison.OrdinalIgnoreCase)),
                "negated questions should include a negation diagnostic");

            NlpResult why = pipeline.Ask("Why does Alice work?");
            Assert(why.Status == NlpStatus.Unsupported, $"expected Unsupported but got {why.Status}");
            Assert(
                why.Diagnostics.Any(message => message.Contains("Why and manner", StringComparison.OrdinalIgnoreCase)),
                "why questions should include a why/manner diagnostic");

            NlpResult locative = pipeline.Ask("Where is Alice?");
            Assert(
                locative.Status is NlpStatus.Unsupported or NlpStatus.NoMatch,
                $"expected Unsupported or NoMatch but got {locative.Status}");
            Assert(
                locative.Diagnostics.Any(message => message.Contains("Locative copula", StringComparison.OrdinalIgnoreCase)),
                "locative copula questions should include a diagnostic");

            NlpResult statement = pipeline.Ask("Alice works at Acme.");
            Assert(statement.Status == NlpStatus.Unsupported, $"expected Unsupported but got {statement.Status}");
            Assert(
                statement.Diagnostics.Any(message => message.Contains("Only questions", StringComparison.OrdinalIgnoreCase)),
                "statements should be rejected with the questions-only diagnostic");

            NlpResult blank = pipeline.Ask("   ");
            Assert(blank.Status == NlpStatus.InvalidInput, $"expected InvalidInput but got {blank.Status}");
            Assert(blank.Answer?.Status == AnswerStatus.InvalidInput, "blank input should produce an invalid-input answer");

            NlpResult punctuation = pipeline.Ask("...!!!");
            Assert(punctuation.Status == NlpStatus.InvalidInput, $"expected InvalidInput but got {punctuation.Status}");
        }

        private static void TestNlpQuestionReadOnly()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "ingestion failed");

            int nodeCount = core.GetDictionary()!.Count;
            int linkCount = core.GetDictionary()!.Values.Sum(node => core.GetLinks(node.ID).Count);

            NlpResult answer = pipeline.Ask("Who works at Acme?");
            Assert(answer.Status == NlpStatus.Answered, $"expected Answered but got {answer.Status}: {string.Join("; ", answer.Diagnostics)}");

            Assert(core.GetDictionary()!.Count == nodeCount, "Ask must not add graph nodes");
            Assert(
                core.GetDictionary()!.Values.Sum(node => core.GetLinks(node.ID).Count) == linkCount,
                "Ask must not add graph links");

            foreach (QuestionVariant variant in answer.Question!.Variants)
            {
                if (variant.IsAnchorOnly)
                    continue;

                Guid whNodeID = variant.Chain.First!.Value.ID;
                Assert(
                    !core.TryGetNode(whNodeID, out _),
                    "query-only wh node IDs must not be inserted into the graph");
            }
        }

        private static void TestNlpQuestionDeterminism()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "ingestion failed");

            NlpResult first = pipeline.Ask("Who works at Acme?");
            NlpResult second = pipeline.Ask("Who works at Acme?");

            Assert(first.Status == second.Status, "repeated questions should return the same status");
            Assert(first.Answer?.Status == second.Answer?.Status, "repeated questions should return the same answer status");
            Assert(first.Answer?.Kind == second.Answer?.Kind, "repeated questions should return the same answer kind");
            Assert(first.Answer?.ShortAnswer == second.Answer?.ShortAnswer, "repeated questions should return the same short answer");
            Assert(first.Answer?.AnswerNodeID == second.Answer?.AnswerNodeID, "repeated questions should return the same answer node");
            Assert(first.Answer?.Text == second.Answer?.Text, "repeated questions should return the same text");
        }

        private static void TestNlpTemporaryEvidence()
        {
            LRD_Core core = NewCore();
            NlpOptions temporaryOptions = new NlpOptions { UseSessionProvenanceInAnswers = false };
            NlpPipeline pipeline = new NlpPipeline(core, temporaryOptions);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "neutral ingestion failed");

            NlpResult neutral = pipeline.Ask("Who works at Acme?");
            Assert(neutral.Answer?.Status == AnswerStatus.Answered, $"expected Answered but got {neutral.Answer?.Status}");
            Assert(neutral.Answer?.UsesTemporaryEvidence == true, "neutral ingestion should produce temporary evidence");
            Assert(
                neutral.Answer?.Text.Contains("not yet verified", StringComparison.OrdinalIgnoreCase) == true,
                "temporary evidence should add a verification caveat");

            LRD_Core trustedCore = NewCore();
            Guid source = AddNode(trustedCore, NodeTypes.Sources, "GoodSource", NodeLifetime.Permanent);
            ReasoningEngine engine = new ReasoningEngine(trustedCore);
            engine.RecordTrustEvidence(source, null, true, 50);
            NlpPipeline trustedPipeline = new NlpPipeline(trustedCore, temporaryOptions, engine);
            Assert(
                trustedPipeline.Ingest("Alice works at Acme.", source).Status == NlpStatus.Ingested,
                "trusted ingestion failed");

            NlpResult trusted = trustedPipeline.Ask("Who works at Acme?");
            Assert(trusted.Answer?.Status == AnswerStatus.Answered, $"expected Answered but got {trusted.Answer?.Status}");
            Assert(trusted.Answer?.UsesTemporaryEvidence == false, "trusted evidence should not be marked temporary");
            Assert(
                trusted.Answer?.Text.Contains("not yet verified", StringComparison.OrdinalIgnoreCase) == false,
                "permanent evidence should not add the temporary caveat");
        }

        private static void TestNlpConversationPronounIngestion()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult first = pipeline.Ingest("Alice works at Acme.");
            Assert(first.Status == NlpStatus.Ingested, $"first ingestion failed with {first.Status}: {string.Join("; ", first.Diagnostics)}");

            NlpResult second = pipeline.Ingest("She works at Globex.");
            Assert(second.Status == NlpStatus.Ingested, $"pronoun ingestion failed with {second.Status}: {string.Join("; ", second.Diagnostics)}");
            Assert(core.FindByLabel("she").Count == 0, "pronoun ingestion must not create a pronoun node");
            Assert(core.FindByLabel("She").Count == 0, "pronoun ingestion must not create a pronoun node");

            Guid alice = core.FindByLabel("Alice", NodeTypes.Entities).Single().ID;
            Guid globex = core.FindByLabel("Globex", NodeTypes.Entities).Single().ID;
            Assert(
                core.GetLinks(alice).Any(link => link.DestinationID == globex && link.ForwardLink.Name == "work_at"),
                "the resolved pronoun should connect Alice to Globex");
            Assert(
                pipeline.Conversation.Mentions.Count(mention => mention.NodeID == alice) == 1,
                "the Alice mention should be updated rather than duplicated");

            NlpResult sees = pipeline.Ingest("Alice sees Bob.");
            Assert(sees.Status == NlpStatus.Ingested, $"sees ingestion failed with {sees.Status}: {string.Join("; ", sees.Diagnostics)}");

            NlpResult resolved = pipeline.Ingest("She sees him.");
            Assert(resolved.Status == NlpStatus.Ingested, $"resolved pronoun ingestion failed with {resolved.Status}: {string.Join("; ", resolved.Diagnostics)}");
            Assert(core.FindByLabel("him").Count == 0, "object pronouns must not create nodes");

            Guid bob = core.FindByLabel("Bob", NodeTypes.Entities).Single().ID;
            Assert(
                core.GetLinks(alice).Any(link => link.DestinationID == bob && link.ForwardLink.Name == "see"),
                "the resolved pronouns should connect Alice to Bob");

            LRD_Core freshCore = NewCore();
            NlpPipeline fresh = new NlpPipeline(freshCore);
            NlpResult unresolved = fresh.Ingest("She works at Acme.");
            Assert(unresolved.Status == NlpStatus.Unsupported, $"expected Unsupported but got {unresolved.Status}");
            Assert(
                unresolved.Diagnostics.Any(message => message.Contains("antecedent", StringComparison.OrdinalIgnoreCase)),
                "a fresh statement should report a missing antecedent");
            Assert(CountNodes(freshCore) == 0 && CountLinks(freshCore) == 0, "an unresolved pronoun statement must not mutate the graph");
            Assert(freshCore.FindByLabel("she").Count == 0, "an unresolved pronoun must not create a placeholder node");

            NlpResult firstPerson = fresh.Ingest("I work at Acme.");
            Assert(firstPerson.Status == NlpStatus.Ingested, $"expected Ingested but got {firstPerson.Status}: {string.Join("; ", firstPerson.Diagnostics)}");
            Assert(freshCore.FindByLabel("User", NodeTypes.Entities).Count == 1, "first-person ingestion should create one speaker node");
            Assert(freshCore.FindByLabel("I").Count == 0, "first-person ingestion must not create a literal pronoun node");

            NlpResult secondPerson = fresh.Ingest("You work at Globex.");
            Assert(secondPerson.Status == NlpStatus.Unsupported, $"expected Unsupported but got {secondPerson.Status}");
            Assert(
                secondPerson.Diagnostics.Any(message => message.Contains("Second-person", StringComparison.OrdinalIgnoreCase)),
                "second-person statement pronouns should be unsupported");

            int nodesBeforeIndefinite = CountNodes(freshCore);
            int linksBeforeIndefinite = CountLinks(freshCore);
            NlpResult indefinite = fresh.Ingest("Someone works at Acme.");
            Assert(indefinite.Status == NlpStatus.Unsupported, $"expected Unsupported but got {indefinite.Status}");
            Assert(
                indefinite.Diagnostics.Any(message => message.Contains("Indefinite", StringComparison.OrdinalIgnoreCase)),
                "indefinite statement pronouns should be unsupported");
            Assert(
                CountNodes(freshCore) == nodesBeforeIndefinite && CountLinks(freshCore) == linksBeforeIndefinite,
                "an indefinite statement must not mutate the graph");
        }

        private static void TestNlpConversationFollowUpQuestions()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "work ingestion failed");

            int nodeCount = CountNodes(core);
            int linkCount = CountLinks(core);
            NlpResult where = pipeline.Ask("Where does she work?");
            Assert(where.Status == NlpStatus.Answered, $"expected Answered but got {where.Status}: {string.Join("; ", where.Diagnostics)}");
            Assert(where.Answer?.ShortAnswer == "Acme", $"expected Acme but got {where.Answer?.ShortAnswer}");
            Assert(CountNodes(core) == nodeCount && CountLinks(core) == linkCount, "pronoun questions must not mutate the graph");

            LRD_Core seeCore = NewCore();
            NlpPipeline seePipeline = new NlpPipeline(seeCore);
            Assert(seePipeline.Ingest("Alice sees Bob.").Status == NlpStatus.Ingested, "see ingestion failed");

            NlpResult who = seePipeline.Ask("Who did she see?");
            Assert(who.Status == NlpStatus.Answered, $"expected Answered but got {who.Status}: {string.Join("; ", who.Diagnostics)}");
            Assert(who.Answer?.ShortAnswer == "Bob", $"expected Bob but got {who.Answer?.ShortAnswer}");

            NlpResult does = seePipeline.Ask("Does she see Bob?");
            Assert(does.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {does.Answer?.Status}: {string.Join("; ", does.Diagnostics)}");

            NlpResult him = seePipeline.Ask("Does Alice see him?");
            Assert(him.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {him.Answer?.Status}: {string.Join("; ", him.Diagnostics)}");

            LRD_Core copulaCore = NewCore();
            NlpPipeline copulaPipeline = new NlpPipeline(copulaCore);
            Assert(copulaPipeline.Ingest("Alice is a doctor.").Status == NlpStatus.Ingested, "copula ingestion failed");

            NlpResult copula = copulaPipeline.Ask("Is she a doctor?");
            Assert(copula.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {copula.Answer?.Status}: {string.Join("; ", copula.Diagnostics)}");

            LRD_Core freshCore = NewCore();
            NlpPipeline fresh = new NlpPipeline(freshCore);
            NlpResult noAntecedent = fresh.Ask("Where does she work?");
            Assert(noAntecedent.Status == NlpStatus.Unsupported, $"expected Unsupported but got {noAntecedent.Status}");
            Assert(
                noAntecedent.Diagnostics.Any(message => message.Contains("antecedent", StringComparison.OrdinalIgnoreCase)),
                "a fresh session should report a missing antecedent");
            Assert(CountNodes(freshCore) == 0, "failed pronoun questions must not create nodes");

            NlpResult secondPerson = fresh.Ask("Where do you work?");
            Assert(secondPerson.Status == NlpStatus.Unsupported, $"expected Unsupported but got {secondPerson.Status}");
            Assert(
                secondPerson.Diagnostics.Any(message => message.Contains("Second-person", StringComparison.OrdinalIgnoreCase)),
                "second-person pronouns should be rejected with an explicit diagnostic");
        }

        private static void TestNlpConversationAmbiguityAndSharing()
        {
            LRD_Core ambiguousCore = NewCore();
            Guid alice = AddNode(ambiguousCore, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            Guid bob = AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            NlpPipeline ambiguous = new NlpPipeline(ambiguousCore);
            ConversationContext context = ambiguous.Conversation;
            context.BeginTurn();
            context.RecordMentions(new[]
            {
                new ConversationMention(alice, "Alice", "Alice", NodeTypes.Entities, FrameRole.Subject, context.TurnIndex, 0, false),
                new ConversationMention(bob, "Bob", "Bob", NodeTypes.Entities, FrameRole.Subject, context.TurnIndex, 0, false)
            });

            int nodeCount = CountNodes(ambiguousCore);
            int linkCount = CountLinks(ambiguousCore);
            NlpResult ambiguousAnswer = ambiguous.Ask("Where does she work?");
            Assert(
                ambiguousAnswer.Status is NlpStatus.Ambiguous or NlpStatus.Unsupported,
                $"expected Ambiguous or Unsupported but got {ambiguousAnswer.Status}");
            Assert(
                ambiguousAnswer.Diagnostics.Any(message =>
                    message.Contains("multiple", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("ambig", StringComparison.OrdinalIgnoreCase)),
                "an ambiguous pronoun should produce an ambiguity diagnostic");
            Assert(CountNodes(ambiguousCore) == nodeCount && CountLinks(ambiguousCore) == linkCount, "ambiguous pronouns must not mutate the graph");

            LRD_Core sharedCore = NewCore();
            NlpOptions options = new NlpOptions();
            NlpPipeline pipelineA = new NlpPipeline(sharedCore, options);
            Assert(pipelineA.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "pipeline A ingestion failed");

            NlpPipeline pipelineB = new NlpPipeline(sharedCore, options);
            Assert(pipelineA.ConversationID != pipelineB.ConversationID, "pipelines without IDs should receive distinct conversations");

            Guid? focusBeforeFailure = pipelineA.Conversation.FocusNodeID;
            int mentionsBeforeFailure = pipelineA.Conversation.Mentions.Count;
            NlpResult isolated = pipelineB.Ask("Where does she work?");
            Assert(isolated.Status == NlpStatus.Unsupported, $"expected Unsupported but got {isolated.Status}");
            Assert(
                isolated.Diagnostics.Any(message => message.Contains("antecedent", StringComparison.OrdinalIgnoreCase)),
                "an isolated conversation should not resolve another session's pronoun");
            Assert(pipelineA.Conversation.FocusNodeID == focusBeforeFailure, "a failed question in another conversation must not change focus");
            Assert(pipelineA.Conversation.Mentions.Count == mentionsBeforeFailure, "a failed question in another conversation must not add mentions");

            NlpResult own = pipelineA.Ask("Where does she work?");
            Assert(own.Status == NlpStatus.Answered, $"expected Answered but got {own.Status}: {string.Join("; ", own.Diagnostics)}");

            NlpPipeline pipelineC = new NlpPipeline(sharedCore, options, conversationID: pipelineA.ConversationID);
            NlpResult shared = pipelineC.Ask("Where does she work?");
            Assert(shared.Status == NlpStatus.Answered, $"expected Answered but got {shared.Status}: {string.Join("; ", shared.Diagnostics)}");
            Assert(shared.Answer?.ShortAnswer == "Acme", $"expected Acme but got {shared.Answer?.ShortAnswer}");

            Assert(ReferenceEquals(pipelineA.Conversation, pipelineC.Conversation), "attached pipelines should share one context instance");

            Guid? sharedFocus = pipelineC.Conversation.FocusNodeID;
            int sharedMentions = pipelineC.Conversation.Mentions.Count;
            NlpResult sharedFailure = pipelineA.Ask("Where do you work?");
            Assert(sharedFailure.Status == NlpStatus.Unsupported, $"expected Unsupported but got {sharedFailure.Status}");
            Assert(pipelineC.Conversation.FocusNodeID == sharedFocus, "a failed shared question must not change focus");
            Assert(pipelineC.Conversation.Mentions.Count == sharedMentions, "a failed shared question must not add mentions");
            NlpResult explicitFollowUp = pipelineC.Ask("Where does Alice work?");
            Assert(explicitFollowUp.Status == NlpStatus.Answered, "a valid explicit follow-up should still work after a failed pronoun question");

            pipelineC.ResetConversation();
            NlpResult afterReset = pipelineA.Ask("Where does she work?");
            Assert(afterReset.Status == NlpStatus.Unsupported, $"expected Unsupported after reset but got {afterReset.Status}");
            Assert(
                afterReset.Diagnostics.Any(message => message.Contains("antecedent", StringComparison.OrdinalIgnoreCase)),
                "a reset shared conversation should have no antecedents");
        }

        private static void TestNlpConversationRegistry()
        {
            LRD_Core core = NewCore();
            NlpOptions options = new NlpOptions();
            NlpPipeline first = new NlpPipeline(core, options);
            NlpPipeline second = new NlpPipeline(core, options);
            Assert(first.ConversationID != second.ConversationID, "default pipelines should get distinct conversation IDs");

            Guid conversationID = core.CreateConversation(options);
            NlpPipeline third = new NlpPipeline(core, options, conversationID: conversationID);
            NlpPipeline fourth = new NlpPipeline(core, options, conversationID: conversationID);
            Assert(ReferenceEquals(third.Conversation, fourth.Conversation), "explicit conversation IDs should share context");
            Assert(core.GetConversations()!.ContainsKey(conversationID), "the created conversation should be registered");

            AssertThrows<InvalidOperationException>(
                () => new NlpPipeline(core, options, conversationID: Guid.NewGuid()),
                "an unknown conversation ID should be rejected");

            int nodeCount = CountNodes(core);
            int linkCount = CountLinks(core);
            Assert(core.RemoveConversation(conversationID), "the conversation should be removed");
            Assert(!core.TryGetConversation(conversationID, out _), "a removed conversation should not be found");
            Assert(!core.TryGetPipelineConversation(third.PipelineID, out _), "attachments to a removed conversation should be gone");
            Assert(CountNodes(core) == nodeCount && CountLinks(core) == linkCount, "conversation removal must not change the graph");

            core.ClearConversations();
            Assert(core.GetConversations()!.Count == 0, "the conversation registry should be empty");
            Assert(!core.TryGetPipelineConversation(first.PipelineID, out _), "clearing should remove pipeline attachments");
            Assert(CountNodes(core) == nodeCount && CountLinks(core) == linkCount, "clearing conversations must not change the graph");

            Guid resetID = core.CreateConversation(options);
            NlpPipeline resetPipeline = new NlpPipeline(core, options, conversationID: resetID);
            Assert(core.TryGetPipelineConversation(resetPipeline.PipelineID, out Guid mapped) && mapped == resetID, "pipeline mapping should be recorded");

            core.InitializeCore();
            Assert(core.GetConversations()!.Count == 0, "reinitializing the core should clear conversations");
            Assert(!core.TryGetPipelineConversation(resetPipeline.PipelineID, out _), "reinitializing the core should clear pipeline mappings");

            Guid readOnlyID = core.CreateConversation(options);
            IReadOnlyDictionary<Guid, ConversationContext> view = core.GetConversations()!;
            Assert(
                view is System.Collections.ObjectModel.ReadOnlyDictionary<Guid, ConversationContext>,
                "the conversation view should be read-only");
            Assert(view.ContainsKey(readOnlyID), "the read-only view should expose the session");

            NlpPipeline disposable = new NlpPipeline(core, options, conversationID: readOnlyID);
            NlpPipeline sibling = new NlpPipeline(core, options, conversationID: readOnlyID);
            disposable.Dispose();
            Assert(!core.TryGetPipelineConversation(disposable.PipelineID, out _), "dispose should remove the pipeline mapping");
            Assert(core.TryGetPipelineConversation(sibling.PipelineID, out _), "dispose should not remove sibling mappings");
            Assert(core.TryGetConversation(readOnlyID, out _), "dispose should not delete the shared conversation");
        }

        private static void TestNlpConversationFocus()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "work ingestion failed");

            Guid alice = core.FindByLabel("Alice", NodeTypes.Entities).Single().ID;
            Guid acme = core.FindByLabel("Acme", NodeTypes.Entities).Single().ID;
            Assert(pipeline.Conversation.LastSubjectNodeID == alice, "Alice should be the last subject");
            Assert(pipeline.Conversation.LastObjectOrQualifierNodeID == acme, "Acme should be the last object or qualifier");
            Assert(pipeline.Conversation.FocusNodeID == acme, "focus should move to the chain target");

            NlpResult where = pipeline.Ask("Where does Alice work?");
            Assert(where.Status == NlpStatus.Answered, $"expected Answered but got {where.Status}");
            Assert(pipeline.Conversation.FocusNodeID == acme, "the answer node should become the focus");
            Assert(pipeline.Conversation.LastAnswerNodeID == acme, "the answer node should be recorded");

            Guid? previousAnswer = pipeline.Conversation.LastAnswerNodeID;
            Assert(pipeline.Ingest("Alice is a doctor.").Status == NlpStatus.Ingested, "copula ingestion failed");
            NlpResult yes = pipeline.Ask("Is Alice a doctor?");
            Assert(yes.Answer?.Status == AnswerStatus.Yes, $"expected Yes but got {yes.Answer?.Status}");
            Assert(pipeline.Conversation.FocusNodeID == alice, "yes/no questions should focus on the anchor");
            Assert(pipeline.Conversation.LastAnswerNodeID == previousAnswer, "yes/no questions should not replace the last answer");

            Guid? focus = pipeline.Conversation.FocusNodeID;
            Guid? lastSubject = pipeline.Conversation.LastSubjectNodeID;
            Guid? lastObject = pipeline.Conversation.LastObjectNodeID;
            Guid? lastQualifier = pipeline.Conversation.LastQualifierNodeID;
            int mentionCount = pipeline.Conversation.Mentions.Count;

            NlpResult unsupported = pipeline.Ask("Where do you work?");
            Assert(unsupported.Status == NlpStatus.Unsupported, $"expected Unsupported but got {unsupported.Status}");
            Assert(pipeline.Conversation.FocusNodeID == focus, "failed questions should not change focus");
            Assert(pipeline.Conversation.LastSubjectNodeID == lastSubject, "failed questions should not change subject focus");
            Assert(pipeline.Conversation.LastObjectNodeID == lastObject, "failed questions should not change object focus");
            Assert(pipeline.Conversation.LastQualifierNodeID == lastQualifier, "failed questions should not change qualifier focus");
            Assert(pipeline.Conversation.Mentions.Count == mentionCount, "failed questions should not add mentions");

            NlpResult failedIngest = pipeline.Ingest("You work at Globex.");
            Assert(failedIngest.Status == NlpStatus.Unsupported, $"expected Unsupported but got {failedIngest.Status}");
            Assert(pipeline.Conversation.FocusNodeID == focus, "failed ingestion should not change focus");
            Assert(pipeline.Conversation.Mentions.Count == mentionCount, "failed ingestion should not add mentions");
        }

        private static void TestNlpSessionProvenance()
        {
            LRD_Core core = NewCore();
            Guid source = AddNode(core, NodeTypes.Sources, "Source", NodeLifetime.Permanent);
            Guid topic = AddNode(core, NodeTypes.Entities, "Topic", NodeLifetime.Permanent);
            NlpPipeline pipeline = new NlpPipeline(core);

            NlpResult result = pipeline.Ingest("Alice works at Acme.", source, topic);
            Assert(result.Status == NlpStatus.Ingested, $"ingestion failed with {result.Status}: {string.Join("; ", result.Diagnostics)}");
            Assert(result.Provenance.Count > 0, "ingestion should produce provenance entries");
            Assert(pipeline.Provenance.Count >= result.Provenance.Count, "the pipeline should expose shared provenance");

            SessionProvenanceEntry entry = result.Provenance[0];
            Assert(entry.SourceText == "Alice works at Acme.", "provenance should preserve the source text");
            Assert(entry.Status == ReasoningStatus.Answered, $"expected Answered but got {entry.Status}");
            Assert(entry.RelationName is not null, "provenance should record the relation");
            Assert(entry.ForwardLinkTypeId.HasValue && entry.InverseLinkTypeId.HasValue, "provenance should record link IDs");
            Assert(entry.SubjectNodeID.HasValue && entry.ObjectNodeID.HasValue, "provenance should record endpoints");
            Assert(entry.TrustSourceNodeID == source, "provenance should record the trust source");
            Assert(entry.TopicNodeID == topic, "provenance should record the topic");
            Assert(entry.GraphMutated, "the first ingestion should report a graph mutation");
            Assert(entry.IsUsableForAnswer, "successful ingestion should be usable for answers");
            Assert(result.Provenance.Any(item => item.TemporaryNodeIDs.Count > 0), "temporary node IDs should be recorded");

            int linkCount = CountLinks(core);
            NlpResult repeated = pipeline.Ingest("Alice works at Acme.");
            Assert(repeated.Status == NlpStatus.Ingested, "repeated ingestion failed");
            Assert(repeated.Provenance.Count > 0, "repeated ingestion should also record provenance");
            Assert(CountLinks(core) == linkCount, "repeated ingestion should not duplicate graph links");

            int provenanceCount = pipeline.Provenance.Count;
            NlpResult question = pipeline.Ask("Who works at Acme?");
            Assert(question.Status == NlpStatus.Answered, $"expected Answered but got {question.Status}");
            Assert(pipeline.Provenance.Count == provenanceCount, "Ask must not create provenance entries");
            Assert(question.Provenance.Count == 0, "Ask should not return provenance entries");

            LRD_Core boundedCore = NewCore();
            NlpOptions boundedOptions = new NlpOptions { MaxSessionProvenanceEntries = 1 };
            NlpPipeline bounded = new NlpPipeline(boundedCore, boundedOptions);
            Assert(bounded.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "bounded ingestion failed");
            Assert(bounded.Ingest("Bob works at Globex.").Status == NlpStatus.Ingested, "second bounded ingestion failed");
            Assert(bounded.Provenance.Count == 1, "provenance should respect the configured bound");
            Assert(bounded.Provenance[0].SourceText == "Bob works at Globex.", "the oldest provenance entry should be evicted first");

            LRD_Core rejectedCore = NewCore();
            Guid badSource = AddNode(rejectedCore, NodeTypes.Sources, "BadSource", NodeLifetime.Permanent);
            ReasoningEngine rejectingEngine = new ReasoningEngine(rejectedCore);
            rejectingEngine.RecordTrustEvidence(badSource, null, false, 100);
            NlpPipeline rejecting = new NlpPipeline(rejectedCore, new NlpOptions(), rejectingEngine);
            NlpResult rejected = rejecting.Ingest("Alice works at Acme.", badSource);
            Assert(rejected.Status == NlpStatus.RejectedByTrust, $"expected RejectedByTrust but got {rejected.Status}");
            Assert(rejected.Provenance.Count > 0, "rejected trust ingestion should still record provenance");
            Assert(rejected.Provenance.All(entry => !entry.IsUsableForAnswer), "rejected provenance should not be usable for answers");
            Assert(rejected.Provenance.All(entry => !entry.GraphMutated), "rejected provenance should not report graph mutation");
            Assert(rejecting.Conversation.Mentions.Count == 0, "rejected trust ingestion should not add mentions");
            Assert(rejecting.Conversation.FocusNodeID is null, "rejected trust ingestion should not change focus");
            Assert(rejectedCore.FindByLabel("Alice").Count == 0, "rejected trust ingestion should not create entity nodes");

            LRD_Core sharedCore = NewCore();
            NlpOptions options = new NlpOptions();
            NlpPipeline pipelineA = new NlpPipeline(sharedCore, options);
            Assert(pipelineA.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "shared ingestion failed");
            NlpPipeline pipelineB = new NlpPipeline(sharedCore, options, conversationID: pipelineA.ConversationID);
            Assert(pipelineB.Provenance.Count == pipelineA.Provenance.Count, "sibling pipelines should see shared provenance");

            pipelineB.ResetConversation();
            Assert(pipelineA.Provenance.Count == 0, "reset should clear shared provenance");
        }

        private static void TestNlpSessionProvenanceCaveats()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "ingestion failed");

            NlpResult session = pipeline.Ask("Where does Alice work?");
            Assert(session.Status == NlpStatus.Answered, $"expected Answered but got {session.Status}");
            Assert(session.Answer?.UsesSessionProvenance == true, "session-backed answers should set the provenance flag");
            Assert(
                session.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == true,
                "session-backed answers should include the session caveat");
            Assert(
                session.Answer?.Text.Contains("not yet verified", StringComparison.OrdinalIgnoreCase) == false,
                "the session caveat should take precedence over the temporary caveat");

            LRD_Core directCore = NewCore();
            Guid alice = AddNode(directCore, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            Guid acme = AddNode(directCore, NodeTypes.Entities, "Acme", NodeLifetime.Permanent);
            RelationLinkType workAt = RelationRegistry.GetOrRegister("work_at");
            Assert(
                directCore.AddDirectedConnection(alice, acme, LinkClass.actions, workAt.Forward.Id),
                "the direct work link should be added");
            NlpPipeline direct = new NlpPipeline(directCore);
            NlpResult directAnswer = direct.Ask("Where does Alice work?");
            Assert(directAnswer.Status == NlpStatus.Answered, $"expected Answered but got {directAnswer.Status}");
            Assert(directAnswer.Answer?.UsesSessionProvenance == false, "direct graph facts should not use session provenance");
            Assert(
                directAnswer.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == false,
                "direct graph facts should not include the session caveat");

            LRD_Core disabledCore = NewCore();
            NlpOptions disabledOptions = new NlpOptions { UseSessionProvenanceInAnswers = false };
            NlpPipeline disabled = new NlpPipeline(disabledCore, disabledOptions);
            Assert(disabled.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "disabled-caveat ingestion failed");
            NlpResult disabledAnswer = disabled.Ask("Where does Alice work?");
            Assert(disabledAnswer.Answer?.UsesSessionProvenance == false, "the caveat flag should respect the option");
            Assert(
                disabledAnswer.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == false,
                "the caveat text should respect the option");
            Assert(disabled.Provenance.Count > 0, "provenance should still be stored when answer wording is disabled");

            LRD_Core temporaryCore = NewCore();
            Guid temporaryAlice = AddNode(temporaryCore, NodeTypes.Entities, "Alice");
            Guid temporaryAcme = AddNode(temporaryCore, NodeTypes.Entities, "Acme");
            Assert(
                temporaryCore.AddDirectedConnection(temporaryAlice, temporaryAcme, LinkClass.actions, workAt.Forward.Id),
                "the temporary work link should be added");
            NlpPipeline temporary = new NlpPipeline(temporaryCore);
            NlpResult temporaryAnswer = temporary.Ask("Where does Alice work?");
            Assert(temporaryAnswer.Answer?.UsesSessionProvenance == false, "temporary graph facts should not use session provenance");
            Assert(temporaryAnswer.Answer?.UsesTemporaryEvidence == true, "temporary graph facts should remain temporary evidence");
            Assert(
                temporaryAnswer.Answer?.Text.Contains("not yet verified", StringComparison.OrdinalIgnoreCase) == true,
                "non-session temporary facts should keep the temporary caveat");

            NlpResult noMatch = pipeline.Ask("Where does Bob work?");
            Assert(noMatch.Answer?.Status == AnswerStatus.NoMatch, $"expected NoMatch but got {noMatch.Answer?.Status}");
            Assert(noMatch.Answer?.UsesSessionProvenance == false, "no-match answers should not use session provenance");
            Assert(
                noMatch.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == false,
                "no-match answers should not include the session caveat");

            LRD_Core ambiguousCore = NewCore();
            Guid ambiguousAlice = AddNode(ambiguousCore, NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            Guid ambiguousBob = AddNode(ambiguousCore, NodeTypes.Entities, "Bob", NodeLifetime.Permanent);
            NlpPipeline ambiguousPipeline = new NlpPipeline(ambiguousCore);
            ambiguousPipeline.Conversation.BeginTurn();
            ambiguousPipeline.Conversation.RecordMentions(new[]
            {
                new ConversationMention(ambiguousAlice, "Alice", "Alice", NodeTypes.Entities, FrameRole.Subject, ambiguousPipeline.Conversation.TurnIndex, 0, false),
                new ConversationMention(ambiguousBob, "Bob", "Bob", NodeTypes.Entities, FrameRole.Subject, ambiguousPipeline.Conversation.TurnIndex, 0, false)
            });
            NlpResult ambiguous = ambiguousPipeline.Ask("Where does she work?");
            Assert(ambiguous.Answer?.UsesSessionProvenance == false, "ambiguous answers should not use session provenance");
            Assert(
                ambiguous.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == false,
                "ambiguous answers should not include the session caveat");

            LRD_Core failedCore = NewCore();
            NlpPipeline failed = new NlpPipeline(failedCore);
            NlpResult failedAnswer = failed.Ask("Where does she work?");
            Assert(failedAnswer.Answer?.UsesSessionProvenance == false, "failed answers should not use session provenance");
            Assert(
                failedAnswer.Answer?.Text.Contains("provided in this session", StringComparison.OrdinalIgnoreCase) == false,
                "failed answers should not include the session caveat");
        }

        private static void TestNlpConversationReadOnlyAndDeterminism()
        {
            LRD_Core core = NewCore();
            NlpOptions options = new NlpOptions();
            NlpPipeline pipeline = new NlpPipeline(core, options);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "ingestion failed");

            int nodeCount = CountNodes(core);
            int linkCount = CountLinks(core);
            NlpResult first = pipeline.Ask("Where does she work?");
            Assert(first.Status == NlpStatus.Answered, $"expected Answered but got {first.Status}");
            Assert(CountNodes(core) == nodeCount, "Ask must not add graph nodes");
            Assert(CountLinks(core) == linkCount, "Ask must not add graph links");

            foreach (QuestionVariant variant in first.Question!.Variants)
            {
                if (variant.IsAnchorOnly)
                    continue;

                Assert(
                    !core.TryGetNode(variant.Chain.First!.Value.ID, out _),
                    "query-only IDs must not be inserted into the graph");
            }

            NlpResult second = pipeline.Ask("Where does she work?");
            Assert(first.Status == second.Status, "repeated pronoun questions should return the same status");
            Assert(first.Answer?.Kind == second.Answer?.Kind, "repeated pronoun questions should return the same kind");
            Assert(first.Answer?.ShortAnswer == second.Answer?.ShortAnswer, "repeated pronoun questions should return the same short answer");
            Assert(first.Answer?.AnswerNodeID == second.Answer?.AnswerNodeID, "repeated pronoun questions should return the same answer node");
            Assert(first.Answer?.Text == second.Answer?.Text, "repeated pronoun questions should return the same text");

            Assert(
                first.Diagnostics.Distinct(StringComparer.Ordinal).Count() == first.Diagnostics.Count,
                "diagnostics should be deduplicated");

            NlpPipeline shared = new NlpPipeline(core, options, conversationID: pipeline.ConversationID);
            Assert(ReferenceEquals(shared.Conversation, pipeline.Conversation), "shared pipelines should use the same context");
            Assert(shared.Conversation.TurnIndex == pipeline.Conversation.TurnIndex, "shared pipelines should observe the same turn counter");
            Assert(shared.Conversation.FocusNodeID == pipeline.Conversation.FocusNodeID, "shared pipelines should observe the same focus");
            Assert(
                shared.Conversation.Mentions.Select(mention => mention.Sequence).SequenceEqual(
                    pipeline.Conversation.Mentions.Select(mention => mention.Sequence)),
                "shared pipelines should observe the same mention sequence");

            NlpPipeline isolated = new NlpPipeline(core, options);
            Assert(isolated.Conversation.TurnIndex == 0, "a new conversation should start with a zero turn counter");
            Assert(isolated.Conversation.Mentions.Count == 0, "a new conversation should start with no mentions");
            Assert(isolated.Conversation.Provenance.Count == 0, "a new conversation should start with no provenance");
        }

        private static void TestNlpFirstPersonSpeakerIdentity()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            NlpResult ingested = pipeline.Ingest("I work at Acme. I live in Paris.");
            Assert(
                ingested.Status == NlpStatus.Ingested,
                $"first-person ingestion failed with {ingested.Status}: {string.Join("; ", ingested.Diagnostics)}");

            List<Web_Node> users = core.FindByLabel("User", NodeTypes.Entities).ToList();
            List<Web_Node> acmes = core.FindByLabel("Acme").ToList();
            List<Web_Node> parises = core.FindByLabel("Paris").ToList();
            Assert(users.Count == 1, $"expected one User node but found {users.Count}; labels={string.Join(", ", core.FindNodes().Select(node => node.Label ?? "(none)"))}");
            Assert(acmes.Count == 1, $"expected one Acme node but found {acmes.Count}; labels={string.Join(", ", core.FindNodes().Select(node => node.Label ?? "(none)"))}");
            Assert(parises.Count == 1, $"expected one Paris node but found {parises.Count}; labels={string.Join(", ", core.FindNodes().Select(node => node.Label ?? "(none)"))}");
            Guid user = users[0].ID;
            Guid acme = acmes[0].ID;
            Guid paris = parises[0].ID;
            Assert(core.FindByLabel("I").Count == 0, "the literal pronoun I must not become a node");
            Assert(core.FindByLabel("i").Count == 0, "the lowercase pronoun i must not become a node");
            Assert(
                core.GetLinks(user).Any(link => link.DestinationID == acme && link.ForwardLink.Name == "work_at"),
                "I should connect the speaker to Acme");
            Assert(
                core.GetLinks(user).Any(link => link.DestinationID == paris && link.ForwardLink.Name == "live_in"),
                "both first-person frames should use the same speaker node");

            NlpResult whereWork = pipeline.Ask("Where do I work?");
            Assert(
                whereWork.Status == NlpStatus.Answered,
                $"expected Answered but got {whereWork.Status}: {string.Join("; ", whereWork.Diagnostics)}");
            Assert(whereWork.Answer?.ShortAnswer == "Acme", $"expected Acme but got {whereWork.Answer?.ShortAnswer}");

            LRD_Core pluralCore = NewCore();
            NlpPipeline plural = new NlpPipeline(pluralCore);
            NlpResult pluralIngest = plural.Ingest("We work at Globex.");
            Assert(
                pluralIngest.Status == NlpStatus.Ingested,
                $"plural first-person ingestion failed with {pluralIngest.Status}: {string.Join("; ", pluralIngest.Diagnostics)}");
            Assert(pluralCore.FindByLabel("User", NodeTypes.Entities).Count == 1, "plural first person should use the speaker node");
            Assert(pluralCore.FindByLabel("We").Count == 0 && pluralCore.FindByLabel("we").Count == 0, "the literal pronoun we must not become a node");

            LRD_Core customCore = NewCore();
            NlpOptions customOptions = new NlpOptions { FirstPersonSpeakerLabel = "Speaker" };
            NlpPipeline custom = new NlpPipeline(customCore, customOptions);
            NlpResult customIngest = custom.Ingest("I work at Initech.");
            Assert(
                customIngest.Status == NlpStatus.Ingested,
                $"custom speaker ingestion failed with {customIngest.Status}: {string.Join("; ", customIngest.Diagnostics)}");
            Assert(customCore.FindByLabel("Speaker", NodeTypes.Entities).Count == 1, "the configured speaker label should be used");
            Assert(customCore.FindByLabel("User", NodeTypes.Entities).Count == 0, "the default label should not be created when configured otherwise");

            LRD_Core freshCore = NewCore();
            NlpPipeline fresh = new NlpPipeline(freshCore);
            int nodeCount = CountNodes(freshCore);
            int linkCount = CountLinks(freshCore);
            NlpResult beforeIngest = fresh.Ask("Where do I work?");
            Assert(
                beforeIngest.Status is NlpStatus.NoMatch or NlpStatus.Unsupported,
                $"expected NoMatch or Unsupported but got {beforeIngest.Status}");
            Assert(
                CountNodes(freshCore) == nodeCount && CountLinks(freshCore) == linkCount,
                "a first-person question must not create the speaker node");

            LRD_Core collisionCore = NewCore();
            AddNode(collisionCore, NodeTypes.Entities, "User", NodeLifetime.Permanent);
            AddNode(collisionCore, NodeTypes.Entities, "User", NodeLifetime.Permanent);
            NlpPipeline collision = new NlpPipeline(collisionCore);
            int collisionNodes = CountNodes(collisionCore);
            int collisionLinks = CountLinks(collisionCore);
            NlpResult ambiguous = collision.Ingest("I work at Acme.");
            Assert(ambiguous.Status == NlpStatus.Unsupported, $"expected Unsupported but got {ambiguous.Status}");
            Assert(
                ambiguous.Diagnostics.Any(message => message.Contains("multiple", StringComparison.OrdinalIgnoreCase)),
                "a speaker label collision should produce an ambiguity diagnostic");
            Assert(
                CountNodes(collisionCore) == collisionNodes && CountLinks(collisionCore) == collisionLinks,
                "an ambiguous speaker identity must not mutate the graph");
        }

        private static void TestNlpCommandRecognition()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "command fixture ingestion failed");

            foreach (string verb in new[] { "Output", "Print", "Respond", "Reply" })
            {
                NlpResult query = pipeline.Ask($"{verb} where does Alice work.");
                Assert(
                    query.Status == NlpStatus.Answered,
                    $"{verb} query command failed with {query.Status}: {string.Join("; ", query.Diagnostics)}");
                Assert(query.Answer?.ShortAnswer == "Acme", $"{verb} query should answer Acme");
                Assert(query.Command?.TargetKind == CommandTargetKind.Query, $"{verb} should be recognized as a query command");
            }

            NlpResult facts = pipeline.Ask("Print facts.");
            Assert(facts.Status == NlpStatus.Answered, $"facts command failed with {facts.Status}");
            Assert(facts.Command?.TargetKind == CommandTargetKind.Facts, "facts should be a fixed command target");
            Assert(facts.Answer?.Text.Contains("Alice", StringComparison.OrdinalIgnoreCase) == true, "facts output should include Alice");
            Assert(facts.Answer?.Text.Contains("work_at", StringComparison.OrdinalIgnoreCase) == true, "facts output should include the relation");

            NlpResult graph = pipeline.Ask("Output graph.");
            Assert(graph.Status == NlpStatus.Answered, $"graph command failed with {graph.Status}");
            Assert(graph.Command?.TargetKind == CommandTargetKind.Graph, "graph should be a fixed command target");
            Assert(graph.Answer?.Text.Contains("Graph:", StringComparison.OrdinalIgnoreCase) == true, "graph output should include the graph summary");

            NlpResult conversation = pipeline.Ask("Respond conversation.");
            Assert(conversation.Status == NlpStatus.Answered, $"conversation command failed with {conversation.Status}");
            Assert(conversation.Command?.TargetKind == CommandTargetKind.Conversation, "conversation should be a fixed command target");
            Assert(
                conversation.Answer?.Text.Contains("Conversation", StringComparison.OrdinalIgnoreCase) == true,
                "conversation output should include the conversation summary");

            NlpResult summary = pipeline.Ask("Reply summary.");
            Assert(summary.Status == NlpStatus.Answered, $"summary command failed with {summary.Status}");
            Assert(summary.Command?.TargetKind == CommandTargetKind.Summary, "summary should be a fixed command target");
            Assert(summary.Answer?.Text.Contains("Graph:", StringComparison.OrdinalIgnoreCase) == true, "summary should include the graph summary");
            Assert(
                summary.Answer?.Text.Contains("Conversation", StringComparison.OrdinalIgnoreCase) == true,
                "summary should include the conversation summary");

            Guid alice = core.FindByLabel("Alice", NodeTypes.Entities).Single().ID;
            NlpResult node = pipeline.Ask($"Output node {alice}.");
            Assert(node.Status == NlpStatus.Answered, $"node command failed with {node.Status}");
            Assert(node.Command?.TargetKind == CommandTargetKind.Node, "node should be a fixed command target");
            Assert(node.Answer?.AnswerNodeID == alice, "node output should identify the node");
            Assert(node.Answer?.Text.Contains("Alice", StringComparison.OrdinalIgnoreCase) == true, "node output should include the label");

            NlpResult please = pipeline.Ask("Please output with where does Alice work.");
            Assert(please.Status == NlpStatus.Answered, $"please command failed with {please.Status}");
            Assert(please.Answer?.ShortAnswer == "Acme", "please should be an optional command prefix");

            NlpResult notCommand = pipeline.Ask("Alice output facts.");
            Assert(notCommand.Command is null, "a subject before the verb must not be recognized as a command");

            NlpResult ingestCommand = pipeline.Ingest("Output facts.");
            Assert(ingestCommand.Command is null, "Ingest must not recognize commands");
            Assert(ingestCommand.Status == NlpStatus.Unsupported, $"expected Unsupported but got {ingestCommand.Status}");
        }

        private static void TestNlpCommandClarificationAndReadOnly()
        {
            LRD_Core core = NewCore();
            NlpPipeline pipeline = new NlpPipeline(core);
            Assert(pipeline.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "command fixture ingestion failed");

            int nodeCount = CountNodes(core);
            int linkCount = CountLinks(core);
            int turn = pipeline.Conversation.TurnIndex;
            int mentions = pipeline.Conversation.Mentions.Count;
            int provenance = pipeline.Conversation.Provenance.Count;
            Guid? focus = pipeline.Conversation.FocusNodeID;
            Guid? lastSubject = pipeline.Conversation.LastSubjectNodeID;
            Guid? lastObject = pipeline.Conversation.LastObjectNodeID;
            Guid? lastQualifier = pipeline.Conversation.LastQualifierNodeID;
            Guid? lastAnswer = pipeline.Conversation.LastAnswerNodeID;

            NlpResult missingTarget = pipeline.Ask("Output");
            Assert(missingTarget.Status == NlpStatus.Ambiguous, $"expected Ambiguous but got {missingTarget.Status}");
            Assert(missingTarget.Command?.NeedsClarification == true, "a missing target should request clarification");
            Assert(
                missingTarget.Answer?.Text.Contains("What should I", StringComparison.OrdinalIgnoreCase) == true,
                "a missing target should ask what to output");

            NlpResult missingNodeID = pipeline.Ask("Print node");
            Assert(missingNodeID.Status == NlpStatus.Ambiguous, $"expected Ambiguous but got {missingNodeID.Status}");
            Assert(
                missingNodeID.Answer?.Text.Contains("Which node ID", StringComparison.OrdinalIgnoreCase) == true,
                "a node command without an ID should ask for one");

            NlpResult invalidNodeID = pipeline.Ask("Output node not-a-guid");
            Assert(invalidNodeID.Status == NlpStatus.Ambiguous, $"expected Ambiguous but got {invalidNodeID.Status}");
            Assert(
                invalidNodeID.Answer?.Text.Contains("Which node ID", StringComparison.OrdinalIgnoreCase) == true,
                "an invalid node ID should ask for a valid one");

            NlpResult unknownTarget = pipeline.Ask("Output banana");
            Assert(unknownTarget.Status == NlpStatus.Ambiguous, $"expected Ambiguous but got {unknownTarget.Status}");
            Assert(
                unknownTarget.Answer?.Text.Contains("What should I", StringComparison.OrdinalIgnoreCase) == true,
                "an unknown target should request clarification");

            Guid absent = Guid.NewGuid();
            NlpResult absentNode = pipeline.Ask($"Output node {absent}");
            Assert(absentNode.Status == NlpStatus.NoMatch, $"expected NoMatch but got {absentNode.Status}");
            Assert(
                absentNode.Answer?.Text.Contains(absent.ToString(), StringComparison.OrdinalIgnoreCase) == true,
                "a missing node should identify the requested ID");

            NlpResult query = pipeline.Ask("Print where does Alice work.");
            Assert(query.Status == NlpStatus.Answered, $"query command failed with {query.Status}");
            Assert(query.Answer?.ShortAnswer == "Acme", "a query command should return the answer");

            NlpResult repeated = pipeline.Ask("Print where does Alice work.");
            Assert(repeated.Answer?.Text == query.Answer?.Text, "command output should be deterministic");

            Assert(CountNodes(core) == nodeCount && CountLinks(core) == linkCount, "commands must not mutate the graph");
            Assert(pipeline.Conversation.TurnIndex == turn, "commands must not begin a conversation turn");
            Assert(pipeline.Conversation.Mentions.Count == mentions, "commands must not add mentions");
            Assert(pipeline.Conversation.Provenance.Count == provenance, "commands must not add provenance");
            Assert(pipeline.Conversation.FocusNodeID == focus, "commands must not change focus");
            Assert(pipeline.Conversation.LastSubjectNodeID == lastSubject, "commands must not change subject focus");
            Assert(pipeline.Conversation.LastObjectNodeID == lastObject, "commands must not change object focus");
            Assert(pipeline.Conversation.LastQualifierNodeID == lastQualifier, "commands must not change qualifier focus");
            Assert(pipeline.Conversation.LastAnswerNodeID == lastAnswer, "commands must not change the last answer");
        }

        private static void TestNlpWorkbenchLifecycle()
        {
            LRD_Core uninitializedCore = LRD_Core.Instance;
            uninitializedCore.initialized = false;

            using (NlpWorkbench uninitialized = new NlpWorkbench(uninitializedCore))
            {
                Assert(!uninitialized.IsCoreInitialized, "an uninitialized core should be reported");
                Assert(!uninitialized.HasSession, "an uninitialized workbench should not have a session");
                Assert(!uninitialized.StartNewConversation(), "starting a session without a core should fail");
                NlpWorkbenchState state = uninitialized.RefreshState();
                Assert(!state.CoreInitialized && !state.HasSession, "refresh should report a no-session state");
            }

            LRD_Core core = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(core))
            {
                Assert(workbench.StartNewConversation(), "a new conversation should start");
                NlpWorkbenchState state = workbench.RefreshState();
                Assert(state.HasSession, "the state should report a session");
                Assert(state.PipelineID.HasValue && state.PipelineID.Value != Guid.Empty, "the pipeline ID should be set");
                Assert(state.ConversationID.HasValue && state.ConversationID.Value != Guid.Empty, "the conversation ID should be set");
                Assert(workbench.PipelineID == state.PipelineID, "the workbench should expose the state pipeline ID");
                Assert(workbench.ConversationID == state.ConversationID, "the workbench should expose the state conversation ID");
                Assert(state.GraphNodeCount == 0 && state.GraphLinkCount == 0, "a fresh core should have no graph data");

                Guid firstConversation = state.ConversationID!.Value;
                Assert(workbench.StartNewConversation(), "a second conversation should start");
                Assert(workbench.ConversationID != firstConversation, "a new conversation should have a new ID");
                Assert(
                    workbench.TryAttachConversation(firstConversation, out string? attachError),
                    attachError ?? "attaching the first conversation should succeed");
                Assert(workbench.ConversationID == firstConversation, "the attached conversation ID should match");

                Guid currentConversation = workbench.ConversationID!.Value;
                NlpWorkbenchState beforeUnknown = workbench.RefreshState();
                Assert(
                    !workbench.TryAttachConversation(Guid.NewGuid(), out string? unknownError),
                    "an unknown conversation should not attach");
                Assert(!string.IsNullOrWhiteSpace(unknownError), "an unknown conversation should report an error");
                Assert(workbench.ConversationID == currentConversation, "a failed attach should keep the current conversation");
                NlpWorkbenchState afterUnknown = workbench.RefreshState();
                Assert(afterUnknown.ConversationID == beforeUnknown.ConversationID, "a failed attach should not change the state");
                Assert(afterUnknown.TurnIndex == beforeUnknown.TurnIndex, "a failed attach should not change the turn count");

                Guid pipelineID = workbench.PipelineID!.Value;
                int nodeCount = CountNodes(core);
                int linkCount = CountLinks(core);
                Assert(workbench.RemoveCurrentConversation(), "the current conversation should be removed");
                Assert(!workbench.HasSession, "removing a conversation should clear the session");
                Assert(!core.TryGetConversation(currentConversation, out _), "the removed conversation should be gone");
                Assert(!core.TryGetPipelineConversation(pipelineID, out _), "the removed pipeline should be gone");
                Assert(CountNodes(core) == nodeCount && CountLinks(core) == linkCount, "removal should not change the graph");
            }

            LRD_Core clearCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(clearCore))
            {
                Assert(workbench.StartNewConversation(), "the workbench conversation should start");
                clearCore.CreateConversation(new NlpOptions());
                int nodeCount = CountNodes(clearCore);
                int linkCount = CountLinks(clearCore);
                workbench.ClearAllConversations();
                Assert(!workbench.HasSession, "clearing conversations should clear the session");
                Assert(clearCore.GetConversations()?.Count == 0, "clearing conversations should empty the registry");
                Assert(
                    CountNodes(clearCore) == nodeCount && CountLinks(clearCore) == linkCount,
                    "clearing conversations should not change the graph");

                Assert(workbench.StartNewConversation(), "a session should start after clearing");
                Guid conversationID = workbench.ConversationID!.Value;
                Guid pipelineID = workbench.PipelineID!.Value;
                workbench.Dispose();
                workbench.Dispose();
                Assert(!clearCore.TryGetPipelineConversation(pipelineID, out _), "dispose should remove the pipeline");
                Assert(clearCore.TryGetConversation(conversationID, out _), "dispose should keep the conversation");

                using NlpWorkbench sibling = new NlpWorkbench(clearCore);
                Assert(
                    sibling.TryAttachConversation(conversationID, out string? siblingError),
                    siblingError ?? "a sibling should reattach the conversation");
            }
        }

        private static void TestNlpWorkbenchOperations()
        {
            LRD_Core core = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(core))
            {
                Assert(workbench.StartNewConversation(), "a conversation should start");
                int nodeCount = CountNodes(core);
                int linkCount = CountLinks(core);
                NlpResult ingest = workbench.Ingest("Alice works at Acme.");
                Assert(
                    ingest.Status == NlpStatus.Ingested,
                    $"expected Ingested but got {ingest.Status}: {string.Join("; ", ingest.Diagnostics)}");

                NlpWorkbenchState afterIngest = workbench.RefreshState();
                Assert(afterIngest.GraphNodeCount > nodeCount, "ingestion should add graph nodes");
                Assert(afterIngest.GraphLinkCount > linkCount, "ingestion should add graph links");
                Assert(
                    afterIngest.Mentions.Any(mention => string.Equals(mention.Label, "Alice", StringComparison.OrdinalIgnoreCase)),
                    "the session should mention Alice");
                Assert(
                    afterIngest.Mentions.Any(mention => string.Equals(mention.Label, "Acme", StringComparison.OrdinalIgnoreCase)),
                    "the session should mention Acme");
                Assert(afterIngest.ProvenanceCount > 0, "ingestion should record provenance");

                int nodesBeforeAsk = afterIngest.GraphNodeCount;
                int linksBeforeAsk = afterIngest.GraphLinkCount;
                int turnBeforeAsk = afterIngest.TurnIndex;
                NlpResult ask = workbench.Ask("Where does she work?");
                Assert(
                    ask.Status == NlpStatus.Answered,
                    $"expected Answered but got {ask.Status}: {string.Join("; ", ask.Diagnostics)}");
                Assert(
                    string.Equals(ask.Answer?.ShortAnswer, "Acme", StringComparison.OrdinalIgnoreCase),
                    $"expected short answer Acme but got {ask.Answer?.ShortAnswer}");

                NlpWorkbenchState afterAsk = workbench.RefreshState();
                Assert(afterAsk.TurnIndex == turnBeforeAsk + 1, "Ask should advance the turn counter");
                Assert(afterAsk.GraphNodeCount == nodesBeforeAsk, "Ask must not add graph nodes");
                Assert(afterAsk.GraphLinkCount == linksBeforeAsk, "Ask must not add graph links");
            }

            LRD_Core sourcedCore = NewCore();
            Guid source = AddNode(sourcedCore, NodeTypes.Sources, "WorkbenchSource", NodeLifetime.Permanent);
            Guid topic = AddNode(sourcedCore, NodeTypes.Entities, "WorkbenchTopic", NodeLifetime.Permanent);
            using (NlpWorkbench workbench = new NlpWorkbench(sourcedCore))
            {
                Assert(workbench.StartNewConversation(), "the sourced conversation should start");
                NlpResult ingest = workbench.Ingest("Alice works at Acme.", source, topic);
                Assert(
                    ingest.Status == NlpStatus.Ingested,
                    $"expected Ingested but got {ingest.Status}: {string.Join("; ", ingest.Diagnostics)}");
                Assert(
                    ingest.Provenance.Any(entry => entry.TrustSourceNodeID == source && entry.TopicNodeID == topic),
                    "the result should record the source and topic");

                NlpWorkbenchState state = workbench.RefreshState();
                Assert(
                    state.Provenance.Any(entry => entry.TrustSourceNodeID == source && entry.TopicNodeID == topic),
                    "the state should expose source and topic provenance");
            }

            LRD_Core invalidCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(invalidCore))
            {
                Assert(workbench.StartNewConversation(), "the invalid-input conversation should start");
                int nodeCount = CountNodes(invalidCore);
                int linkCount = CountLinks(invalidCore);
                int turnIndex = workbench.RefreshState().TurnIndex;

                foreach (string? text in new string?[] { null, string.Empty, "   " })
                {
                    NlpResult result = workbench.Ingest(text);
                    Assert(result.Status == NlpStatus.InvalidInput, "blank ingest should be InvalidInput");
                }

                NlpResult ask = workbench.Ask(null);
                Assert(ask.Status == NlpStatus.InvalidInput, "blank ask should be InvalidInput");

                NlpWorkbenchState state = workbench.RefreshState();
                Assert(state.TurnIndex == turnIndex, "invalid input should not advance the turn counter");
                Assert(state.MentionCount == 0 && state.ProvenanceCount == 0, "invalid input should not add session data");
                Assert(
                    CountNodes(invalidCore) == nodeCount && CountLinks(invalidCore) == linkCount,
                    "invalid input should not change the graph");
            }

            LRD_Core failedCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(failedCore))
            {
                Assert(workbench.StartNewConversation(), "the failed-pronoun conversation should start");
                int nodeCount = CountNodes(failedCore);
                int linkCount = CountLinks(failedCore);

                NlpResult ingest = workbench.Ingest("She works at Acme.");
                Assert(ingest.Status == NlpStatus.Unsupported, $"expected Unsupported but got {ingest.Status}");
                NlpWorkbenchState afterIngest = workbench.RefreshState();
                Assert(afterIngest.MentionCount == 0, "failed pronoun ingestion should not add mentions");
                Assert(
                    CountNodes(failedCore) == nodeCount && CountLinks(failedCore) == linkCount,
                    "failed pronoun ingestion should not change the graph");

                NlpResult ask = workbench.Ask("Where does she work?");
                Assert(ask.Status == NlpStatus.Unsupported, $"expected Unsupported but got {ask.Status}");
                NlpWorkbenchState afterAsk = workbench.RefreshState();
                Assert(afterAsk.MentionCount == 0, "failed pronoun questions should not add mentions");
                Assert(afterAsk.FocusNodeID is null, "failed pronoun questions should not set focus");
                Assert(
                    CountNodes(failedCore) == nodeCount && CountLinks(failedCore) == linkCount,
                    "failed pronoun questions should not change the graph");
            }

            LRD_Core sharedCore = NewCore();
            using NlpWorkbench first = new NlpWorkbench(sharedCore);
            using NlpWorkbench second = new NlpWorkbench(sharedCore);
            Assert(first.StartNewConversation(), "the shared conversation should start");
            Assert(
                first.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested,
                "the shared ingestion should succeed");
            Guid sharedID = first.ConversationID!.Value;
            NlpWorkbenchState firstState = first.RefreshState();
            Assert(second.TryAttachConversation(sharedID, out string? attachError), attachError ?? "the second workbench should attach");
            NlpWorkbenchState secondState = second.RefreshState();
            Assert(secondState.MentionCount == firstState.MentionCount, "sibling workbenches should share mentions");
            Assert(secondState.ProvenanceCount == firstState.ProvenanceCount, "sibling workbenches should share provenance");

            int turnBeforeSharedAsk = firstState.TurnIndex;
            NlpResult sharedAsk = second.Ask("Where does she work?");
            Assert(
                sharedAsk.Status == NlpStatus.Answered,
                $"expected Answered but got {sharedAsk.Status}: {string.Join("; ", sharedAsk.Diagnostics)}");
            NlpWorkbenchState firstAfter = first.RefreshState();
            Assert(firstAfter.TurnIndex > turnBeforeSharedAsk, "the first workbench should observe the shared turn");
            Assert(firstAfter.Mentions.Any(mention => mention.FromAnswer), "the first workbench should observe answer mentions");

            LRD_Core resetCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(resetCore))
            {
                Assert(workbench.StartNewConversation(), "the reset conversation should start");
                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "reset ingestion failed");
                Assert(workbench.Ask("Where does she work?").Status == NlpStatus.Answered, "reset question failed");
                int nodeCount = CountNodes(resetCore);
                int linkCount = CountLinks(resetCore);

                Assert(workbench.ResetCurrentConversation(), "reset should succeed");
                NlpWorkbenchState state = workbench.RefreshState();
                Assert(state.TurnIndex == 0, "reset should clear the turn counter");
                Assert(state.MentionCount == 0 && state.ProvenanceCount == 0, "reset should clear session data");
                Assert(state.FocusNodeID is null, "reset should clear focus");
                Assert(state.LastSubjectNodeID is null, "reset should clear the last subject");
                Assert(state.LastObjectNodeID is null, "reset should clear the last object");
                Assert(state.LastQualifierNodeID is null, "reset should clear the last qualifier");
                Assert(state.LastAnswerNodeID is null, "reset should clear the last answer");
                Assert(
                    CountNodes(resetCore) == nodeCount && CountLinks(resetCore) == linkCount,
                    "reset should not change the graph");

                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "post-reset ingestion failed");
                Assert(workbench.Ask("Where does she work?").Status == NlpStatus.Answered, "post-reset question failed");
            }

            LRD_Core readOnlyCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(readOnlyCore))
            {
                Assert(workbench.StartNewConversation(), "the read-only conversation should start");
                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "read-only ingestion failed");
                int nodeCount = CountNodes(readOnlyCore);
                int linkCount = CountLinks(readOnlyCore);
                NlpResult result = workbench.Ask("Where does she work?");
                Assert(result.Status == NlpStatus.Answered, $"expected Answered but got {result.Status}");
                Assert(CountNodes(readOnlyCore) == nodeCount, "workbench Ask must not add graph nodes");
                Assert(CountLinks(readOnlyCore) == linkCount, "workbench Ask must not add graph links");

                foreach (QuestionVariant variant in result.Question!.Variants)
                {
                    if (variant.IsAnchorOnly)
                        continue;

                    Assert(
                        !readOnlyCore.TryGetNode(variant.Chain.First!.Value.ID, out _),
                        "workbench query-only IDs must not be inserted into the graph");
                }
            }
        }

        private static void TestNlpWorkbenchStateAndStaleSessions()
        {
            LRD_Core core = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(core))
            {
                Assert(workbench.StartNewConversation(), "the snapshot conversation should start");
                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "snapshot ingestion failed");
                Assert(workbench.Ask("Where does she work?").Status == NlpStatus.Answered, "snapshot question failed");

                NlpWorkbenchState state = workbench.RefreshState();
                ConversationContext context = core.GetConversation(workbench.ConversationID!.Value);
                Assert(state.GraphNodeCount == CountNodes(core), "state should report the graph node count");
                Assert(state.GraphLinkCount == CountLinks(core), "state should report the directed link count");
                Assert(state.TurnIndex == context.TurnIndex, "state should report the turn index");
                Assert(state.FocusNodeID == context.FocusNodeID, "state should report focus");
                Assert(state.LastSubjectNodeID == context.LastSubjectNodeID, "state should report the last subject");
                Assert(state.LastObjectNodeID == context.LastObjectNodeID, "state should report the last object");
                Assert(state.LastQualifierNodeID == context.LastQualifierNodeID, "state should report the last qualifier");
                Assert(state.LastAnswerNodeID == context.LastAnswerNodeID, "state should report the last answer");
                Assert(state.MentionCount == context.Mentions.Count, "state should report the mention count");
                Assert(state.ProvenanceCount == context.Provenance.Count, "state should report the provenance count");
                Assert(state.Mentions.SequenceEqual(context.Mentions), "state mentions should preserve context order");
                Assert(state.Provenance.SequenceEqual(context.Provenance), "state provenance should preserve context order");

                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "stale ingestion failed");
                Guid pipelineID = workbench.PipelineID!.Value;
                core.InitializeCore();
                NlpWorkbenchState stale = workbench.RefreshState();
                Assert(!stale.CoreInitialized || !stale.HasSession, "a reinitialized core should invalidate the session");
                Assert(!stale.HasSession, "the stale session should be cleared");
                Assert(!core.TryGetPipelineConversation(pipelineID, out _), "the stale pipeline mapping should be absent");
                Assert(stale.GraphNodeCount == 0 && stale.GraphLinkCount == 0, "the new core should be empty");

                AssertThrows<InvalidOperationException>(
                    () => workbench.Ingest("Alice works at Acme."),
                    "ingest after stale detection should be rejected");
                AssertThrows<InvalidOperationException>(
                    () => workbench.Ask("Who works at Acme?"),
                    "ask after stale detection should be rejected");
            }

            LRD_Core stableCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(stableCore))
            {
                Assert(workbench.StartNewConversation(), "the stable conversation should start");
                Assert(workbench.Ingest("Alice works at Acme.").Status == NlpStatus.Ingested, "stable ingestion failed");
                NlpWorkbenchState before = workbench.RefreshState();
                int nodeCount = CountNodes(stableCore);
                int linkCount = CountLinks(stableCore);

                for (int index = 0; index < 3; index++)
                {
                    NlpWorkbenchState refreshed = workbench.RefreshState();
                    Assert(refreshed.GraphNodeCount == before.GraphNodeCount, "refresh should not change nodes");
                    Assert(refreshed.GraphLinkCount == before.GraphLinkCount, "refresh should not change links");
                    Assert(refreshed.TurnIndex == before.TurnIndex, "refresh should not change turns");
                    Assert(refreshed.MentionCount == before.MentionCount, "refresh should not change mentions");
                    Assert(refreshed.ProvenanceCount == before.ProvenanceCount, "refresh should not change provenance");
                }

                Assert(CountNodes(stableCore) == nodeCount, "repeated refresh should not add nodes");
                Assert(CountLinks(stableCore) == linkCount, "repeated refresh should not add links");
            }

            LRD_Core firstCore = NewCore();
            NlpStatus firstIngestStatus;
            NlpStatus firstAskStatus;
            string? firstShortAnswer;
            IReadOnlyList<string> firstDiagnostics;
            int firstMentions;
            int firstProvenance;

            using (NlpWorkbench workbench = new NlpWorkbench(firstCore))
            {
                Assert(workbench.StartNewConversation(), "the determinism conversation should start");
                firstIngestStatus = workbench.Ingest("Alice works at Acme.").Status;
                NlpResult answer = workbench.Ask("Where does she work?");
                firstAskStatus = answer.Status;
                firstShortAnswer = answer.Answer?.ShortAnswer;
                firstDiagnostics = answer.Diagnostics;
                NlpWorkbenchState state = workbench.RefreshState();
                firstMentions = state.MentionCount;
                firstProvenance = state.ProvenanceCount;
            }

            LRD_Core secondCore = NewCore();
            using (NlpWorkbench workbench = new NlpWorkbench(secondCore))
            {
                Assert(workbench.StartNewConversation(), "the second determinism conversation should start");
                NlpStatus secondIngestStatus = workbench.Ingest("Alice works at Acme.").Status;
                NlpResult answer = workbench.Ask("Where does she work?");
                NlpWorkbenchState state = workbench.RefreshState();
                Assert(firstIngestStatus == secondIngestStatus, "ingest statuses should be deterministic");
                Assert(firstAskStatus == answer.Status, "answer statuses should be deterministic");
                Assert(firstShortAnswer == answer.Answer?.ShortAnswer, "short answers should be deterministic");
                Assert(firstDiagnostics.SequenceEqual(answer.Diagnostics), "diagnostics should be deterministic");
                Assert(firstMentions == state.MentionCount, "mention counts should be deterministic");
                Assert(firstProvenance == state.ProvenanceCount, "provenance counts should be deterministic");
            }
        }

        private static int CountNodes(LRD_Core core)
        {
            return core.GetDictionary()?.Count ?? 0;
        }

        private static int CountLinks(LRD_Core core)
        {
            IReadOnlyDictionary<Guid, Web_Node>? nodes = core.GetDictionary();

            if (nodes is null)
                return 0;

            return nodes.Values.Sum(node => core.GetLinks(node.ID).Count);
        }

        private static void AssertArgument(RelationFrame frame, FrameRole role, string surfaceText)
        {
            Assert(
                frame.Arguments.Any(argument =>
                    argument.Role == role
                    && string.Equals(argument.SurfaceText, surfaceText, StringComparison.OrdinalIgnoreCase)),
                $"missing {role} argument '{surfaceText}' in relation '{frame.RelationName}'");
        }

        private static Guid AddNode(
            LRD_Core core,
            NodeTypes type,
            string? label = null,
            NodeLifetime lifetime = NodeLifetime.Temporary,
            params string[] aliases)
        {
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, type, lifetime, 100);
            node.Set_Label(label);

            foreach (string alias in aliases)
                node.Add_Alias(alias);

            Assert(core.AddExistingNode(node), "node should be added");
            return nodeID;
        }

        private static void AssertToken(
            ParsedText parsed,
            int index,
            string text,
            PartOfSpeech partOfSpeech,
            string lemma)
        {
            Assert(index < parsed.Tokens.Count, $"missing token at index {index}");
            Token token = parsed.Tokens[index];

            Assert(token.Text == text, $"token {index} should be '{text}' but was '{token.Text}'");
            Assert(
                token.PartOfSpeech == partOfSpeech,
                $"token '{token.Text}' should be {partOfSpeech} but was {token.PartOfSpeech}");
            Assert(token.Lemma == lemma, $"token '{token.Text}' should have lemma '{lemma}' but was '{token.Lemma}'");
        }

        private static string Signature(ParsedText parsed)
        {
            return string.Join(
                "|",
                parsed.Tokens.Select(token =>
                    $"{token.Text}:{token.StartOffset}:{token.PartOfSpeech}:{token.Lemma}"));
        }
    }
}
