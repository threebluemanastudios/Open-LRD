using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    /// <summary>Describes the outcome of an NLP parse, ingestion, or Ask operation.</summary>
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

    /// <summary>Identifies the lexical part of speech assigned to a token.</summary>
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

    /// <summary>Classifies a parsed sentence by its communicative form.</summary>
    internal enum SentenceType
    {
        Unknown,
        Declarative,
        YesNoQuestion,
        WhQuestion,
        Imperative
    }

    /// <summary>Stores a token's source span, normalized text, lemma, and grammatical tag.</summary>
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

    /// <summary>Identifies a syntactic chunk category produced by shallow parsing.</summary>
    internal enum ChunkKind
    {
        Unknown,
        NounPhrase,
        VerbPhrase,
        PrepositionalPhrase,
        AdjectivePhrase,
        AdverbPhrase
    }

    /// <summary>Identifies the grammatical role of a clause argument.</summary>
    internal enum ClauseRole
    {
        Unknown,
        Subject,
        Predicate,
        Object,
        Complement,
        Qualifier
    }

    /// <summary>Classifies a named or typed entity mention.</summary>
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

    /// <summary>Stores the token span and syntactic kind of a parsed phrase.</summary>
    internal sealed record Chunk(
        ChunkKind Kind,
        int SentenceIndex,
        int StartToken,
        int EndToken)
    {
        public int TokenCount => EndToken - StartToken + 1;
    }

    /// <summary>Stores a role-labeled token span within a clause.</summary>
    internal sealed record ClauseArgument(
        ClauseRole Role,
        int StartToken,
        int EndToken);

    /// <summary>Stores clause boundaries, phrase chunks, and extracted arguments.</summary>
    internal sealed record Clause(
        int Index,
        int StartToken,
        int EndToken,
        IReadOnlyList<Chunk> Chunks,
        IReadOnlyList<ClauseArgument> Arguments);

    /// <summary>Stores a typed mention and its confidence and source span.</summary>
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

    /// <summary>Stores tokenized and parsed data for one sentence.</summary>
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

    /// <summary>Stores normalized text, sentences, tokens, parsed chunks, mentions, and diagnostics.</summary>
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

    /// <summary>Identifies the semantic role of an argument in a relation frame.</summary>
    internal enum FrameRole
    {
        Subject,
        Object,
        Recipient,
        Time,
        Location,
        Qualifier
    }

    /// <summary>Identifies the grammatical tense of a relation frame.</summary>
    internal enum Tense
    {
        Unknown,
        Present,
        Past,
        Future
    }

    /// <summary>Identifies the grammatical voice of a relation frame.</summary>
    internal enum Voice
    {
        Unknown,
        Active,
        Passive
    }

    /// <summary>Describes conjunction or disjunction across coordinated arguments.</summary>
    internal enum CoordinationKind
    {
        None,
        Conjunction,
        Disjunction
    }

    /// <summary>Describes the scope of negation in a relation frame.</summary>
    internal enum NegationKind
    {
        None,
        Predicate,
        Complement
    }

    /// <summary>Stores a typed relation argument and the span from which it was extracted.</summary>
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

    /// <summary>Stores an extracted predicate, typed arguments, qualifiers, and polarity.</summary>
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

    /// <summary>Contains extracted relation frames and parser diagnostics.</summary>
    internal sealed record RelationExtractionResult(
        IReadOnlyList<RelationFrame> Frames,
        IReadOnlyList<string> Diagnostics);

    /// <summary>Contains NLP chains prepared for reasoning and any build diagnostics.</summary>
    internal sealed record ChainBuildResult(
        IReadOnlyList<LinkedList<Chain_Node>> Chains,
        IReadOnlyList<string> Diagnostics);

    /// <summary>Classifies a question by its expected answer position and shape.</summary>
    internal enum QuestionType
    {
        Unknown,
        YesNo,
        WhSubject,
        WhObject,
        WhQualifier,
        WhQuantity
    }

    /// <summary>Identifies the interrogative word class used by a question.</summary>
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

    /// <summary>Identifies the semantic form expected for an answer.</summary>
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

    /// <summary>Describes the result of a read-only answer operation.</summary>
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

    /// <summary>Stores one bounded graph-query alternative for an analyzed question.</summary>
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

    /// <summary>Stores the normalized answer intent, anchor, and query variants for a question.</summary>
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

    /// <summary>Stores a ranked graph node candidate and its supporting path.</summary>
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

    /// <summary>Stores an answer, candidate evidence, confidence, and diagnostics.</summary>
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

    /// <summary>Stores the result and all optional parse, frame, chain, reasoning, and answer details.</summary>
    internal sealed record NlpResult(
        NlpStatus Status,
        IReadOnlyList<string> Diagnostics,
        ParsedText? Parsed = null)
    {
        public string? OriginalInput { get; init; }

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

    /// <summary>Provides a read-only snapshot of an NLP Workbench session and its conversation state.</summary>
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
