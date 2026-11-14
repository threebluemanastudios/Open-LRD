using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

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

    internal enum FrameRole
    {
        Subject,
        Object,
        Recipient,
        Time,
        Location,
        Qualifier
    }

    internal enum Tense
    {
        Unknown,
        Present,
        Past,
        Future
    }

    internal enum Voice
    {
        Unknown,
        Active,
        Passive
    }

    internal enum CoordinationKind
    {
        None,
        Conjunction,
        Disjunction
    }

    internal enum NegationKind
    {
        None,
        Predicate,
        Complement
    }

    internal sealed record FrameArgument(
        FrameRole Role,
        string SurfaceText,
        string Lemma,
        NodeTypes NodeType,
        MentionKind Kind,
        int SentenceIndex,
        int StartToken,
        int EndToken,
        bool IsPronoun,
        string? Preposition,
        double Confidence);

    internal sealed record RelationFrame(
        string PredicateLemma,
        string RelationName,
        LinkClass LinkClass,
        IReadOnlyList<FrameArgument> Arguments,
        bool Negated,
        string? Modal,
        Tense Tense,
        Voice Voice,
        int SentenceIndex,
        int ClauseIndex,
        int StartToken,
        int EndToken,
        double Confidence,
        bool Ingestible,
        IReadOnlyList<string> Diagnostics,
        CoordinationKind Coordination = CoordinationKind.None,
        int CoordinationGroup = 0,
        NegationKind Negation = NegationKind.None);

    internal sealed record RelationExtractionResult(
        IReadOnlyList<RelationFrame> Frames,
        IReadOnlyList<string> Diagnostics);

    internal sealed record ChainBuildResult(
        IReadOnlyList<LinkedList<Chain_Node>> Chains,
        IReadOnlyList<string> Diagnostics);

    internal enum QuestionType
    {
        Unknown,
        YesNo,
        WhSubject,
        WhObject,
        WhQualifier,
        WhQuantity
    }

    internal enum WhKind
    {
        None,
        Who,
        Whom,
        Whose,
        What,
        Which,
        When,
        Where,
        Why,
        How
    }

    internal enum AnswerKind
    {
        None,
        Entity,
        Location,
        Time,
        Date,
        Quantity,
        Boolean,
        Description
    }

    internal enum AnswerStatus
    {
        Unknown,
        Answered,
        Yes,
        No,
        Ambiguous,
        NoMatch,
        Unsupported,
        RejectedByTrust,
        InvalidInput
    }

    internal sealed record QuestionVariant(
        string Name,
        LinkedList<Chain_Node> Chain,
        NodeTypes ExpectedAnswerType,
        string RelationName,
        int ForwardLinkTypeId,
        int InverseLinkTypeId,
        LinkClass LinkClass,
        FrameRole AnswerRole,
        bool IsAnchorOnly,
        bool IsBoolean);

    internal sealed record QuestionAnalysis(
        QuestionType Type,
        WhKind Wh,
        AnswerKind ExpectedAnswer,
        string? WhSurface,
        FrameArgument? WhArgument,
        FrameArgument? Subject,
        FrameArgument? Object,
        FrameArgument? Qualifier,
        string? PredicateLemma,
        string? RelationName,
        LinkClass LinkClass,
        Tense Tense,
        Voice Voice,
        bool Negated,
        bool IsPassive,
        bool Supported,
        bool AnchorIsAmbiguous,
        Guid? AnchorNodeID,
        IReadOnlyList<Guid> AnchorCandidateNodeIDs,
        FrameRole AnchorRole,
        IReadOnlyList<QuestionVariant> Variants,
        IReadOnlyList<string> Diagnostics,
        int SentenceIndex,
        int WhStartToken,
        int WhEndToken);

    internal sealed record AnswerCandidate(
        Guid NodeID,
        string Label,
        NodeTypes NodeType,
        double Score,
        int Depth,
        int RelationLinkTypeId,
        LinkClass LinkClass,
        IReadOnlyList<Guid> PathNodeIDs,
        bool UsesTemporaryEvidence);

    internal sealed record NlpAnswer(
        AnswerStatus Status,
        AnswerKind Kind,
        string Text,
        string? ShortAnswer,
        Guid? AnswerNodeID,
        IReadOnlyList<AnswerCandidate> Candidates,
        IReadOnlyList<GraphPath> EvidencePaths,
        QuestionAnalysis? Question,
        ReasoningResult? Reasoning,
        bool UsesTemporaryEvidence,
        double Confidence,
        IReadOnlyList<string> Diagnostics,
        bool UsesSessionProvenance = false);

    internal sealed record NlpResult(
        NlpStatus Status,
        IReadOnlyList<string> Diagnostics,
        ParsedText? Parsed = null)
    {
        public IReadOnlyList<RelationFrame> Frames { get; init; } =
            Array.Empty<RelationFrame>();

        public IReadOnlyList<LinkedList<Chain_Node>> Chains { get; init; } =
            Array.Empty<LinkedList<Chain_Node>>();

        public IReadOnlyList<ReasoningResult> ReasoningResults { get; init; } =
            Array.Empty<ReasoningResult>();

        public QuestionAnalysis? Question { get; init; }

        public NlpAnswer? Answer { get; init; }

        public NlpCommand? Command { get; init; }

        public IReadOnlyList<SessionProvenanceEntry> Provenance { get; init; } =
            Array.Empty<SessionProvenanceEntry>();
    }

    internal sealed record NlpWorkbenchState(
        bool CoreInitialized,
        bool HasSession,
        Guid? PipelineID,
        Guid? ConversationID,
        int TurnIndex,
        Guid? FocusNodeID,
        Guid? LastSubjectNodeID,
        Guid? LastObjectNodeID,
        Guid? LastQualifierNodeID,
        Guid? LastAnswerNodeID,
        int GraphNodeCount,
        int GraphLinkCount,
        int MentionCount,
        int ProvenanceCount,
        IReadOnlyList<ConversationMention> Mentions,
        IReadOnlyList<SessionProvenanceEntry> Provenance);
}
