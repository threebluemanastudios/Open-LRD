using System;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>Storage precision for cached keys and values.</summary>
    public enum KvMode
    {
        /// <summary>32-bit floats.</summary>
        Fp32,
        /// <summary>Symmetric int8 with one float scale per (layer, KV head, position) row.</summary>
        Int8,
    }

    /// <summary>
    /// Key/value cache in managed arrays, one pair per (layer, KV head). Each array holds MaxSeq rows of HeadDim
    /// values, so rows for consecutive positions are contiguous.
    /// </summary>
    public sealed class KvCache
    {
        private readonly float[][] keysF = Array.Empty<float[]>(), valsF = Array.Empty<float[]>();   // Fp32 mode, one array per layer
        private readonly sbyte[][] keys8 = Array.Empty<sbyte[]>(), vals8 = Array.Empty<sbyte[]>();   // Int8 mode, one array per layer
        private readonly float[][] keyScale = Array.Empty<float[]>(), valScale = Array.Empty<float[]>();

        /// <summary>Creates an empty cache sized for <paramref name="config"/>.</summary>
        /// <param name="config">Model shape. Validated.</param>
        /// <param name="mode">Storage precision.</param>
        public KvCache(ModelConfig config, KvMode mode)
        {
            config.Validate();
            Config = config;
            Mode = mode;
            int layers = config.Layers;
            int layerElems = config.KvHeads * config.MaxSeq * config.HeadDim;
            int layerRows = config.KvHeads * config.MaxSeq;
            if (mode == KvMode.Fp32)
            {
                keysF = new float[layers][];
                valsF = new float[layers][];
                for (int i = 0; i < layers; i++)
                {
                    keysF[i] = new float[layerElems];
                    valsF[i] = new float[layerElems];
                }
            }
            else
            {
                keys8 = new sbyte[layers][];
                vals8 = new sbyte[layers][];
                keyScale = new float[layers][];
                valScale = new float[layers][];
                for (int i = 0; i < layers; i++)
                {
                    keys8[i] = new sbyte[layerElems];
                    vals8[i] = new sbyte[layerElems];
                    keyScale[i] = new float[layerRows];
                    valScale[i] = new float[layerRows];
                }
            }
        }

        /// <summary>Model shape this cache was built for.</summary>
        public ModelConfig Config { get; }

        /// <summary>Storage precision.</summary>
        public KvMode Mode { get; }

        /// <summary>Approximate bytes of value storage (excludes array headers).</summary>
        public long Bytes
        {
            get
            {
                long rows = (long)Config.Layers * Config.KvHeads * Config.MaxSeq;
                return Mode == KvMode.Fp32
                    ? rows * Config.HeadDim * 8L
                    : rows * (Config.HeadDim * 2L + 8L);
            }
        }

        /// <summary>
        /// Writes key and value vectors (KvDim values each) at one position of one layer.
        /// Int8 mode quantizes each KV head row separately.
        /// </summary>
        /// <param name="layer">Layer index.</param>
        /// <param name="pos">Position, within MaxSeq.</param>
        /// <param name="k">Keys, KvHeads * HeadDim values.</param>
        /// <param name="v">Values, KvHeads * HeadDim values.</param>
        public void Write(int layer, int pos, ReadOnlySpan<float> k, ReadOnlySpan<float> v)
        {
            if ((uint)layer >= Config.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            if ((uint)pos >= Config.MaxSeq) throw new ArgumentOutOfRangeException(nameof(pos));
            if (k.Length != Config.KvDim || v.Length != Config.KvDim)
                throw new ArgumentException($"Key and value vectors must have {Config.KvDim} values.");
            int hd = Config.HeadDim;
            for (int g = 0; g < Config.KvHeads; g++)
            {
                int row = g * Config.MaxSeq + pos;
                if (Mode == KvMode.Fp32)
                {
                    k.Slice(g * hd, hd).CopyTo(keysF[layer].AsSpan(row * hd, hd));
                    v.Slice(g * hd, hd).CopyTo(valsF[layer].AsSpan(row * hd, hd));
                }
                else
                {
                    keyScale[layer][row] = Quantize(k.Slice(g * hd, hd), keys8[layer].AsSpan(row * hd, hd));
                    valScale[layer][row] = Quantize(v.Slice(g * hd, hd), vals8[layer].AsSpan(row * hd, hd));
                }
            }
        }

        /// <summary>Reads one cached row back as floats (dequantized in int8 mode).</summary>
        /// <param name="layer">Layer index.</param>
        /// <param name="kvHead">KV head index.</param>
        /// <param name="pos">Position.</param>
        /// <param name="value">True for values, false for keys.</param>
        /// <param name="dst">Destination, HeadDim values.</param>
        public void ReadRow(int layer, int kvHead, int pos, bool value, Span<float> dst)
        {
            if (dst.Length < Config.HeadDim) throw new ArgumentException("Destination too short.", nameof(dst));
            int hd = Config.HeadDim;
            int row = kvHead * Config.MaxSeq + pos;
            if (Mode == KvMode.Fp32)
            {
                (value ? valsF[layer] : keysF[layer]).AsSpan(row * hd, hd).CopyTo(dst);
            }
            else
            {
                sbyte[] src = (value ? vals8[layer] : keys8[layer]);
                float scale = (value ? valScale[layer] : keyScale[layer])[row];
                for (int d = 0; d < hd; d++) dst[d] = src[row * hd + d] * scale;
            }
        }

        // Symmetric int8 quantization. Returns the dequantization scale (max|x| / 127).
        private static float Quantize(ReadOnlySpan<float> src, Span<sbyte> dst)
        {
            float maxAbs = 0f;
            for (int i = 0; i < src.Length; i++) maxAbs = MathF.Max(maxAbs, MathF.Abs(src[i]));
            if (maxAbs == 0f)
            {
                dst.Clear();
                return 0f;
            }
            float inv = 127f / maxAbs;
            for (int i = 0; i < src.Length; i++)
            {
                int q = (int)MathF.Round(src[i] * inv);
                dst[i] = (sbyte)Math.Clamp(q, -127, 127);
            }
            return maxAbs / 127f;
        }

        // Internal accessors for the attention kernels. Rows are indexed by kvHead * MaxSeq + position.
        internal float[] KeysF(int layer) => keysF[layer];
        internal float[] ValsF(int layer) => valsF[layer];
        internal sbyte[] Keys8(int layer) => keys8[layer];
        internal sbyte[] Vals8(int layer) => vals8[layer];
        internal float[] KeyScale(int layer) => keyScale[layer];
        internal float[] ValScale(int layer) => valScale[layer];
    }
}
