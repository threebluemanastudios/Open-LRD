using System.Text.Json;
using System.Text.Json.Serialization;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Persistence
{
    /// <summary>Defines stable format identifiers and schema version numbers for persisted logs.</summary>
    internal static class CoreLogFormat
    {
        public const string Core = "open-lrd-core-log";
        public const string Transient = "open-lrd-transient-log";
        public const string CoreManifest = "open-lrd-core-log-manifest";
        public const string TransientManifest = "open-lrd-transient-log-manifest";
        public const string CorePart = "open-lrd-core-log-part";
        public const string TransientPart = "open-lrd-transient-log-part";
        public const int Version = 1;
    }

    /// <summary>Provides shared JSON serializer settings for log documents and records.</summary>
    internal static class LogJson
    {
        public static JsonSerializerOptions Options { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static JsonSerializerOptions CompactOptions { get; } = new JsonSerializerOptions(Options)
        {
            WriteIndented = false
        };
    }

    /// <summary>Represents a complete unsegmented core graph log document.</summary>
    internal sealed class CoreLogDocument
    {
        public string Format { get; set; } = CoreLogFormat.Core;

        public int Version { get; set; } = CoreLogFormat.Version;

        public DateTimeOffset ExportedAtUtc { get; set; }

        public string? TransientFile { get; set; }

        public TypeDefinitionLog TypeDefinitions { get; set; } = new TypeDefinitionLog();

        public List<NodeLog> Nodes { get; set; } = new List<NodeLog>();

        public List<LinkLog> Links { get; set; } = new List<LinkLog>();

        public List<ConversationLog> Conversations { get; set; } = new List<ConversationLog>();
    }

    /// <summary>Describes a segmented core export and its part manifest.</summary>
    internal sealed class CoreLogManifest
    {
        public string Format { get; set; } = CoreLogFormat.CoreManifest;
        public int Version { get; set; } = CoreLogFormat.Version;
        public Guid ExportId { get; set; }
        public DateTimeOffset ExportedAtUtc { get; set; }
        public string? TransientFile { get; set; }
        public TypeDefinitionLog TypeDefinitions { get; set; } = new TypeDefinitionLog();
        public int NodeCount { get; set; }
        public int LinkCount { get; set; }
        public int ConversationCount { get; set; }
        public List<LogPartDefinition> Parts { get; set; } = new List<LogPartDefinition>();
    }

    /// <summary>Describes a segmented transient-chain export and its part manifest.</summary>
    internal sealed class TransientLogManifest
    {
        public string Format { get; set; } = CoreLogFormat.TransientManifest;
        public int Version { get; set; } = CoreLogFormat.Version;
        public Guid ExportId { get; set; }
        public DateTimeOffset ExportedAtUtc { get; set; }
        public string? CoreFile { get; set; }
        public TypeDefinitionLog TypeDefinitions { get; set; } = new TypeDefinitionLog();
        public int QueryChainCount { get; set; }
        public int ResponseChainCount { get; set; }
        public int QueryNodeCount { get; set; }
        public int ResponseNodeCount { get; set; }
        public List<LogPartDefinition> Parts { get; set; } = new List<LogPartDefinition>();
    }

    /// <summary>Describes one numbered log part, including its length, checksum, and record count.</summary>
    internal sealed class LogPartDefinition
    {
        public int Index { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public long ByteLength { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public int RecordCount { get; set; }
    }

    /// <summary>Contains one typed record in a segmented core log.</summary>
    internal sealed class CoreLogPartRecord
    {
        public string? Format { get; set; }
        public int? Version { get; set; }
        public string Kind { get; set; } = string.Empty;

        public Guid ExportId { get; set; }

        public int PartIndex { get; set; }

        public string? PartKind { get; set; }

        public NodeLog? Node { get; set; }
        public LinkLog? Link { get; set; }
        public ConversationLog? Conversation { get; set; }
    }

    /// <summary>Contains one positioned chain-node record in a segmented transient log.</summary>
    internal sealed class TransientLogPartRecord
    {
        public string? Format { get; set; }
        public int? Version { get; set; }
        public string Kind { get; set; } = string.Empty;

        public Guid ExportId { get; set; }

        public int PartIndex { get; set; }

        public string? PartKind { get; set; }

        public int ChainIndex { get; set; }
        public int Position { get; set; }
        public NodeLog? Node { get; set; }
        public LinkLog? NextLink { get; set; }
    }

    /// <summary>Stores runtime node, link-class, relationship, and inverse-type definitions.</summary>
    internal sealed class TypeDefinitionLog
    {
        public List<TypeNameLog> NodeTypes { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> LinkClasses { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> ForwardLinkTypes { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> InverseLinkTypes { get; set; } = new List<TypeNameLog>();

        public List<RelationTypeLog> Relations { get; set; } = new List<RelationTypeLog>();
    }

    /// <summary>Stores a stable numeric identifier and name for a registered graph type.</summary>
    internal sealed class TypeNameLog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Stores the name and inverse name of a relationship type.</summary>
    internal sealed class RelationTypeLog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string InverseName { get; set; } = string.Empty;
    }

    /// <summary>Stores one graph node and all persisted metadata fields.</summary>
    internal sealed class NodeLog
    {
        public Guid Id { get; set; }

        public int NodeTypeId { get; set; }

        public string NodeTypeName { get; set; } = string.Empty;

        public NodeLifetime Lifetime { get; set; }

        public int Ttl { get; set; }

        public float WeightUp { get; set; }

        public float WeightDown { get; set; }

        public double? TrustRating { get; set; }

        public string? Label { get; set; }

        public List<string> Aliases { get; set; } = new List<string>();

        public DataPointerLog? DataPointer { get; set; }
    }

    /// <summary>Stores a data pointer using URI and optional payload metadata.</summary>
    internal sealed class DataPointerLog
    {
        public string Location { get; set; } = string.Empty;

        public string? MediaType { get; set; }

        public long? ByteLength { get; set; }

        public string? Checksum { get; set; }
    }

    /// <summary>Stores one directed edge, its type identifiers, class, and active state.</summary>
    internal sealed class LinkLog
    {
        public Guid SourceId { get; set; }

        public Guid DestinationId { get; set; }

        public int ForwardTypeId { get; set; }

        public string ForwardTypeName { get; set; } = string.Empty;

        public int InverseTypeId { get; set; }

        public string InverseTypeName { get; set; } = string.Empty;

        public int LinkClassId { get; set; }

        public string LinkClassName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public LinkOrigin Origins { get; set; } = LinkOrigin.Explicit;
    }

    /// <summary>Stores conversational focus, options, mentions, and provenance.</summary>
    internal sealed class ConversationLog
    {
        public Guid Id { get; set; }

        public NlpOptionsLog Options { get; set; } = new NlpOptionsLog();

        public int TurnIndex { get; set; }

        public int Sequence { get; set; }

        public Guid? FocusNodeId { get; set; }

        public Guid? LastSubjectNodeId { get; set; }

        public Guid? LastObjectNodeId { get; set; }

        public Guid? LastQualifierNodeId { get; set; }

        public Guid? LastAnswerNodeId { get; set; }

        public string? LastQuestionTemplate { get; set; }

        public List<MentionLog> Mentions { get; set; } = new List<MentionLog>();

        public List<ProvenanceLog> Provenance { get; set; } = new List<ProvenanceLog>();
    }

    /// <summary>Stores a mention and its resolved node, role, and turn metadata.</summary>
    internal sealed class MentionLog
    {
        public Guid NodeId { get; set; }

        public string Label { get; set; } = string.Empty;

        public string SurfaceText { get; set; } = string.Empty;

        public int NodeTypeId { get; set; }

        public string NodeTypeName { get; set; } = string.Empty;

        public int Role { get; set; }

        public int TurnIndex { get; set; }

        public int Sequence { get; set; }

        public bool FromAnswer { get; set; }
    }

    /// <summary>Stores session provenance for one ingested relation or outcome.</summary>
    internal sealed class ProvenanceLog
    {
        public int Sequence { get; set; }

        public int TurnIndex { get; set; }

        public int? SentenceIndex { get; set; }

        public string SourceText { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? RelationName { get; set; }

        public int LinkClassId { get; set; }

        public string LinkClassName { get; set; } = string.Empty;

        public int? ForwardLinkTypeId { get; set; }

        public int? InverseLinkTypeId { get; set; }

        public Guid? SubjectNodeId { get; set; }

        public Guid? ObjectNodeId { get; set; }

        public Guid? QualifierNodeId { get; set; }

        public Guid? TrustSourceNodeId { get; set; }

        public Guid? TopicNodeId { get; set; }

        public List<Guid> NodeIds { get; set; } = new List<Guid>();

        public List<Guid> TemporaryNodeIds { get; set; } = new List<Guid>();

        public bool GraphMutated { get; set; }

        public bool IsUsableForAnswer { get; set; }

        public List<string> Diagnostics { get; set; } = new List<string>();

        public int Coordination { get; set; }

        public int CoordinationGroup { get; set; }

        public int Negation { get; set; }
    }

    /// <summary>Converts NLP options to and from the persistence DTO schema.</summary>
    internal static class NlpOptionsLogMapper
    {
        /// <summary>Creates a persistence DTO from runtime NLP options.</summary>
        public static NlpOptionsLog ToLog(NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new NlpOptionsLog
            {
                MaxCharacters = options.MaxCharacters,
                MaxSentences = options.MaxSentences,
                MaxTokens = options.MaxTokens,
                MaxClausesPerSentence = options.MaxClausesPerSentence,
                MaxChainsPerText = options.MaxChainsPerText,
                MaxAnswerCandidates = options.MaxAnswerCandidates,
                MaxQuestionVariants = options.MaxQuestionVariants,
                AnswerAmbiguityMargin = options.AnswerAmbiguityMargin,
                MinimumAnswerScore = options.MinimumAnswerScore,
                EnableAnchorFallback = options.EnableAnchorFallback,
                IncludeEvidenceSentence = options.IncludeEvidenceSentence,
                MaxAnswerTextLength = options.MaxAnswerTextLength,
                TemporaryNodeTimeToLive = options.TemporaryNodeTimeToLive,
                EnableConversationContext = options.EnableConversationContext,
                EnablePronounResolution = options.EnablePronounResolution,
                FirstPersonSpeakerLabel = options.FirstPersonSpeakerLabel,
                EnableSessionProvenance = options.EnableSessionProvenance,
                UseSessionProvenanceInAnswers = options.UseSessionProvenanceInAnswers,
                EnableSocialDialogue = options.EnableSocialDialogue,
                EnableConversationalPreambles = options.EnableConversationalPreambles,
                EnableMultiAnswerSynthesis = options.EnableMultiAnswerSynthesis,
                ReferenceTime = options.ReferenceTime,
                MaxConversationMentions = options.MaxConversationMentions,
                MaxSessionProvenanceEntries = options.MaxSessionProvenanceEntries,
                MaxPronounCandidates = options.MaxPronounCandidates,
                PronounRecencyWindow = options.PronounRecencyWindow,
                PronounAmbiguityMargin = options.PronounAmbiguityMargin
            };
        }

        /// <summary>Creates runtime NLP options from a persistence DTO.</summary>
        public static NlpOptions FromLog(NlpOptionsLog options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new NlpOptions
            {
                MaxCharacters = options.MaxCharacters,
                MaxSentences = options.MaxSentences,
                MaxTokens = options.MaxTokens,
                MaxClausesPerSentence = options.MaxClausesPerSentence,
                MaxChainsPerText = options.MaxChainsPerText,
                MaxAnswerCandidates = options.MaxAnswerCandidates,
                MaxQuestionVariants = options.MaxQuestionVariants,
                AnswerAmbiguityMargin = options.AnswerAmbiguityMargin,
                MinimumAnswerScore = options.MinimumAnswerScore,
                EnableAnchorFallback = options.EnableAnchorFallback,
                IncludeEvidenceSentence = options.IncludeEvidenceSentence,
                MaxAnswerTextLength = options.MaxAnswerTextLength,
                TemporaryNodeTimeToLive = options.TemporaryNodeTimeToLive,
                EnableConversationContext = options.EnableConversationContext,
                EnablePronounResolution = options.EnablePronounResolution,
                FirstPersonSpeakerLabel = options.FirstPersonSpeakerLabel,
                EnableSessionProvenance = options.EnableSessionProvenance,
                UseSessionProvenanceInAnswers = options.UseSessionProvenanceInAnswers,
                EnableSocialDialogue = options.EnableSocialDialogue,
                EnableConversationalPreambles = options.EnableConversationalPreambles,
                EnableMultiAnswerSynthesis = options.EnableMultiAnswerSynthesis,
                ReferenceTime = options.ReferenceTime,
                MaxConversationMentions = options.MaxConversationMentions,
                MaxSessionProvenanceEntries = options.MaxSessionProvenanceEntries,
                MaxPronounCandidates = options.MaxPronounCandidates,
                PronounRecencyWindow = options.PronounRecencyWindow,
                PronounAmbiguityMargin = options.PronounAmbiguityMargin
            };
        }
    }

    /// <summary>Stores versioned NLP option values in a core-log document.</summary>
    internal sealed class NlpOptionsLog
    {
        public int MaxCharacters { get; set; } = 20000;

        public int MaxSentences { get; set; } = 50;

        public int MaxTokens { get; set; } = 2000;

        public int MaxClausesPerSentence { get; set; } = 16;

        public int MaxChainsPerText { get; set; } = 64;

        public int MaxAnswerCandidates { get; set; } = 5;

        public int MaxQuestionVariants { get; set; } = 12;

        public double AnswerAmbiguityMargin { get; set; } = 0.05;

        public double MinimumAnswerScore { get; set; } = 0.40;

        public bool EnableAnchorFallback { get; set; } = true;

        public bool IncludeEvidenceSentence { get; set; } = true;

        public int MaxAnswerTextLength { get; set; } = 500;

        public int TemporaryNodeTimeToLive { get; set; } = 6000;

        public bool EnableConversationContext { get; set; } = true;

        public bool EnablePronounResolution { get; set; } = true;

        public string FirstPersonSpeakerLabel { get; set; } = "User";

        public bool EnableSessionProvenance { get; set; } = true;

        public bool UseSessionProvenanceInAnswers { get; set; } = true;

        public bool EnableSocialDialogue { get; set; } = true;

        public bool EnableConversationalPreambles { get; set; } = true;

        public bool EnableMultiAnswerSynthesis { get; set; } = true;

        public DateTimeOffset? ReferenceTime { get; set; }

        public int MaxConversationMentions { get; set; } = 32;

        public int MaxSessionProvenanceEntries { get; set; } = 256;

        public int MaxPronounCandidates { get; set; } = 8;

        public int PronounRecencyWindow { get; set; } = 8;

        public double PronounAmbiguityMargin { get; set; } = 0.05;
    }
}
