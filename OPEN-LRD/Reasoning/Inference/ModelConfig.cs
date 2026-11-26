using System;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Shape of a decoder-only transformer (GQA, SwiGLU, RoPE). Values are validated on construction
    /// through <see cref="Validate"/>.
    /// </summary>
    /// <param name="Hidden">Model width. Must equal Heads * HeadDim.</param>
    /// <param name="Layers">Number of transformer layers.</param>
    /// <param name="Heads">Query heads.</param>
    /// <param name="KvHeads">Key/value heads. Must divide Heads.</param>
    /// <param name="HeadDim">Per-head width. Must be a multiple of 16 and even.</param>
    /// <param name="Ffn">SwiGLU hidden width. Must be a multiple of 16.</param>
    /// <param name="Vocab">Vocabulary size.</param>
    /// <param name="MaxSeq">Maximum number of cached positions.</param>
    public sealed record ModelConfig(int Hidden, int Layers, int Heads, int KvHeads, int HeadDim, int Ffn, int Vocab, int MaxSeq)
    {
        /// <summary>Query heads per key/value head.</summary>
        public int Group => Heads / KvHeads;

        /// <summary>Width of the key (and value) projection: KvHeads * HeadDim.</summary>
        public int KvDim => KvHeads * HeadDim;

        /// <summary>Benchmark shape used for the Phase 0 and Phase 1 measurements (2B-class, random weights).</summary>
        public static ModelConfig Benchmark { get; } = new ModelConfig(2560, 28, 20, 4, 128, 6912, 32000, 16384);

        /// <summary>Checks that the shape is internally consistent.</summary>
        /// <exception cref="ArgumentException">Thrown when a constraint is violated.</exception>
        public void Validate()
        {
            if (Layers < 1 || Heads < 1 || KvHeads < 1 || Vocab < 1 || MaxSeq < 1)
                throw new ArgumentException("Layers, heads, vocab, and max sequence must be positive.");
            if (Heads % KvHeads != 0)
                throw new ArgumentException("KvHeads must divide Heads.");
            if (HeadDim < 16 || HeadDim % 16 != 0)
                throw new ArgumentException("HeadDim must be a multiple of 16.");
            if (Hidden != Heads * HeadDim)
                throw new ArgumentException("Hidden must equal Heads * HeadDim.");
            if (Ffn < 16 || Ffn % 16 != 0)
                throw new ArgumentException("Ffn must be a multiple of 16.");
        }
    }
}
