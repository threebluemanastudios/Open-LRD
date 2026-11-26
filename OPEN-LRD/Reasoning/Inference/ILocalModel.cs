namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// A local language model that advances one token at a time. Implemented by <see cref="Decoder"/>.
    /// RC decoding depends on this interface, so it can be tested with a fake model.
    /// </summary>
    public interface ILocalModel
    {
        /// <summary>Vocabulary size. Length of the logits returned by <see cref="Forward"/>.</summary>
        int VocabSize { get; }

        /// <summary>Maximum number of positions the model can hold.</summary>
        int MaxContext { get; }

        /// <summary>Number of positions already processed since the last <see cref="Reset"/>.</summary>
        int Length { get; }

        /// <summary>
        /// Processes one token at <paramref name="position"/>, which must equal <see cref="Length"/>.
        /// Returns logits for the next token. The array is valid until the next call.
        /// </summary>
        /// <param name="token">Input token id, in [0, VocabSize).</param>
        /// <param name="position">Position of this token. Must be sequential.</param>
        float[] Forward(int token, int position);

        /// <summary>Discards all processed positions so a new sequence can start.</summary>
        void Reset();
    }
}
