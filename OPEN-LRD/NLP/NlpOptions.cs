using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal sealed record NlpOptions
    {
        public int MaxCharacters { get; init; } = 20000;
        public int MaxSentences { get; init; } = 50;
        public int MaxTokens { get; init; } = 2000;
        public int MaxClausesPerSentence { get; init; } = 16;
        public int MaxChainsPerText { get; init; } = 64;
        public int MaxAnswerCandidates { get; init; } = 5;
        public int MaxQuestionVariants { get; init; } = 12;
        public double AnswerAmbiguityMargin { get; init; } = 0.05;
        public double MinimumAnswerScore { get; init; } = 0.40;
        public bool EnableAnchorFallback { get; init; } = true;
        public bool IncludeEvidenceSentence { get; init; } = true;
        public int MaxAnswerTextLength { get; init; } = 500;
        public int TemporaryNodeTimeToLive { get; init; } = Node.DefaultTimeToLive;
    }
}
