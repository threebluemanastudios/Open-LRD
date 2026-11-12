using OPEN_LRD;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
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
