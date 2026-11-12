using System.Text.Json;
using System.Text.Json.Serialization;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;

namespace OPEN_LRD.Persistence
{
    internal static class CoreLogFormat
    {
        public const string Core = "open-lrd-core-log";
        public const string Transient = "open-lrd-transient-log";
        public const int Version = 1;
    }

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
    }

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

    internal sealed class TypeDefinitionLog
    {
        public List<TypeNameLog> NodeTypes { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> LinkClasses { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> ForwardLinkTypes { get; set; } = new List<TypeNameLog>();

        public List<TypeNameLog> InverseLinkTypes { get; set; } = new List<TypeNameLog>();

        public List<RelationTypeLog> Relations { get; set; } = new List<RelationTypeLog>();
    }

    internal sealed class TypeNameLog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class RelationTypeLog
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string InverseName { get; set; } = string.Empty;
    }

    internal sealed class NodeLog
    {
        public Guid Id { get; set; }

        public int NodeTypeId { get; set; }

        public string NodeTypeName { get; set; } = string.Empty;

        public NodeLifetime Lifetime { get; set; }

        public int Ttl { get; set; }

        public float WeightUp { get; set; }

        public float WeightDown { get; set; }

        public string? Label { get; set; }

        public List<string> Aliases { get; set; } = new List<string>();

        public DataPointerLog? DataPointer { get; set; }
    }

    internal sealed class DataPointerLog
    {
        public string Location { get; set; } = string.Empty;

        public string? MediaType { get; set; }

        public long? ByteLength { get; set; }

        public string? Checksum { get; set; }
    }

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
    }

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

        public List<MentionLog> Mentions { get; set; } = new List<MentionLog>();

        public List<ProvenanceLog> Provenance { get; set; } = new List<ProvenanceLog>();
    }

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
    }

    internal static class NlpOptionsLogMapper
    {
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
                MaxConversationMentions = options.MaxConversationMentions,
                MaxSessionProvenanceEntries = options.MaxSessionProvenanceEntries,
                MaxPronounCandidates = options.MaxPronounCandidates,
                PronounRecencyWindow = options.PronounRecencyWindow,
                PronounAmbiguityMargin = options.PronounAmbiguityMargin
            };
        }

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
                MaxConversationMentions = options.MaxConversationMentions,
                MaxSessionProvenanceEntries = options.MaxSessionProvenanceEntries,
                MaxPronounCandidates = options.MaxPronounCandidates,
                PronounRecencyWindow = options.PronounRecencyWindow,
                PronounAmbiguityMargin = options.PronounAmbiguityMargin
            };
        }
    }

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

        public int MaxConversationMentions { get; set; } = 32;

        public int MaxSessionProvenanceEntries { get; set; } = 256;

        public int MaxPronounCandidates { get; set; } = 8;

        public int PronounRecencyWindow { get; set; } = 8;

        public double PronounAmbiguityMargin { get; set; } = 0.05;
    }
}
