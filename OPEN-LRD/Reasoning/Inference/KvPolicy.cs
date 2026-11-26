namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Chooses the default KV cache precision from the context length. Long-context sessions use int8, which
    /// reads a quarter of the KV bytes per token (faster at 16k, and 4x less memory). Short contexts keep fp32.
    /// </summary>
    public static class KvPolicy
    {
        /// <summary>Context length (in positions) at and above which int8 KV is the default.</summary>
        public const int LongContextThreshold = 4096;

        /// <summary>Default KV precision for a cache that holds up to <paramref name="maxContext"/> positions.</summary>
        /// <param name="maxContext">Maximum number of cached positions.</param>
        public static KvMode DefaultFor(int maxContext) =>
            maxContext >= LongContextThreshold ? KvMode.Int8 : KvMode.Fp32;
    }
}
