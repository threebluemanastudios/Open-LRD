using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal enum NlpStatus
    {
        Parsed,
        Ingested,
        Answered,
        Ambiguous,
        NoMatch,
        Unsupported,
        RejectedByTrust,
        InvalidInput
    }

    internal enum PartOfSpeech
    {
        Unknown,
        Determiner,
        Noun,
        ProperNoun,
        Pronoun,
        Verb,
        Auxiliary,
        Adjective,
        Adverb,
        Preposition,
        Conjunction,
        WhWord,
        Number,
        Punctuation
    }

    internal enum SentenceType
    {
        Unknown,
        Declarative,
        YesNoQuestion,
        WhQuestion,
        Imperative
    }

    internal sealed record Token(
        string Text,
        string Normalized,
        string Lemma,
        PartOfSpeech PartOfSpeech,
        int StartOffset,
        int Length,
        int SentenceIndex,
        int TokenIndex)
    {
        public int EndOffset => StartOffset + Length;
    }

    internal enum ChunkKind
    {
        Unknown,
        NounPhrase,
        VerbPhrase,
        PrepositionalPhrase,
        AdjectivePhrase,
        AdverbPhrase
    }

    internal enum ClauseRole
    {
        Unknown,
        Subject,
        Predicate,
        Object,
        Complement,
        Qualifier
    }

    internal enum MentionKind
    {
        Unknown,
        Person,
        Organization,
        Location,
        Date,
        Time,
        Quantity,
        Event,
        Concept
    }

    internal sealed record Chunk(
        ChunkKind Kind,
        int SentenceIndex,
        int StartToken,
        int EndToken)
    {
        public int TokenCount => EndToken - StartToken + 1;
    }

    internal sealed record ClauseArgument(
        ClauseRole Role,
        int StartToken,
        int EndToken);

    internal sealed record Clause(
        int Index,
        int StartToken,
        int EndToken,
        IReadOnlyList<Chunk> Chunks,
        IReadOnlyList<ClauseArgument> Arguments);

    internal sealed record Mention(
        string SurfaceText,
        string Lemma,
        NodeTypes NodeType,
        MentionKind Kind,
        int SentenceIndex,
        int StartToken,
        int EndToken,
        double Confidence,
        Guid? ResolvedNodeID = null);

    internal sealed record Sentence(
        int Index,
        string Text,
        int StartOffset,
        int Length,
        SentenceType Type,
        IReadOnlyList<Token> Tokens)
    {
        public int EndOffset => StartOffset + Length;

        public IReadOnlyList<Clause> Clauses { get; init; } = Array.Empty<Clause>();
    }

    internal sealed record ParsedText(
        string OriginalText,
        string NormalizedText,
        IReadOnlyList<Sentence> Sentences,
        IReadOnlyList<Token> Tokens,
        IReadOnlyList<string> Diagnostics)
    {
        public IReadOnlyList<Chunk> Chunks { get; init; } = Array.Empty<Chunk>();

        public IReadOnlyList<Mention> Mentions { get; init; } = Array.Empty<Mention>();
    }

    internal sealed record NlpResult(
        NlpStatus Status,
        IReadOnlyList<string> Diagnostics,
        ParsedText? Parsed = null);
}
