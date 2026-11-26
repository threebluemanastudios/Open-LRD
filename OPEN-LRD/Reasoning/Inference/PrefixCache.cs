using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// KV state for one full block of tokens, for all layers and KV heads. Block arrays are laid out per layer as
    /// [kvHead][position in block][headDim]. Int8 blocks also keep one scale per row.
    /// </summary>
    public sealed class KvBlock
    {
        /// <summary>Creates an empty block with storage for <paramref name="count"/> positions.</summary>
        /// <param name="hash">Chain hash of this block (see <see cref="PrefixCache.ChainHash"/>).</param>
        /// <param name="parent">Chain hash of the preceding prefix.</param>
        /// <param name="mode">Storage precision of the captured cache.</param>
        /// <param name="config">Model shape.</param>
        /// <param name="count">Positions in this block.</param>
        public KvBlock(ulong hash, ulong parent, KvMode mode, ModelConfig config, int count)
        {
            Hash = hash;
            Parent = parent;
            Mode = mode;
            Config = config;
            Count = count;
            Tokens = new int[count];
            Logits = new float[config.Vocab];
            int elems = config.KvHeads * count * config.HeadDim;
            int rows = config.KvHeads * count;
            if (mode == KvMode.Fp32)
            {
                KeysF = new float[config.Layers][];
                ValsF = new float[config.Layers][];
                for (int l = 0; l < config.Layers; l++)
                {
                    KeysF[l] = new float[elems];
                    ValsF[l] = new float[elems];
                }
                Keys8 = Array.Empty<sbyte[]>();
                Vals8 = Array.Empty<sbyte[]>();
                KeyScale = Array.Empty<float[]>();
                ValScale = Array.Empty<float[]>();
            }
            else
            {
                Keys8 = new sbyte[config.Layers][];
                Vals8 = new sbyte[config.Layers][];
                KeyScale = new float[config.Layers][];
                ValScale = new float[config.Layers][];
                for (int l = 0; l < config.Layers; l++)
                {
                    Keys8[l] = new sbyte[elems];
                    Vals8[l] = new sbyte[elems];
                    KeyScale[l] = new float[rows];
                    ValScale[l] = new float[rows];
                }
                KeysF = Array.Empty<float[]>();
                ValsF = Array.Empty<float[]>();
            }
        }

        /// <summary>Chain hash of this block.</summary>
        public ulong Hash { get; }

        /// <summary>Chain hash of the preceding prefix (0 seed for the first block).</summary>
        public ulong Parent { get; }

        /// <summary>Storage precision.</summary>
        public KvMode Mode { get; }

        /// <summary>Model shape.</summary>
        public ModelConfig Config { get; }

        /// <summary>Positions in this block.</summary>
        public int Count { get; }

        /// <summary>Token ids of this block, checked on every hit.</summary>
        public int[] Tokens { get; }

        /// <summary>Logits after the last token of this block.</summary>
        public float[] Logits { get; }

        /// <summary>Keys, fp32 mode. [layer][kvHead][pos][headDim].</summary>
        public float[][] KeysF { get; }

        /// <summary>Values, fp32 mode.</summary>
        public float[][] ValsF { get; }

        /// <summary>Keys, int8 mode.</summary>
        public sbyte[][] Keys8 { get; }

        /// <summary>Values, int8 mode.</summary>
        public sbyte[][] Vals8 { get; }

        /// <summary>Key row scales, int8 mode. [layer][kvHead][pos].</summary>
        public float[][] KeyScale { get; }

        /// <summary>Value row scales, int8 mode.</summary>
        public float[][] ValScale { get; }

        /// <summary>Approximate bytes held by this block.</summary>
        public long Bytes
        {
            get
            {
                long kv = Mode == KvMode.Fp32
                    ? (long)Config.Layers * Config.KvHeads * Count * Config.HeadDim * 8
                    : (long)Config.Layers * Config.KvHeads * Count * (Config.HeadDim * 2 + 8);
                return kv + Logits.Length * 4L + Tokens.Length * 4L;
            }
        }
    }

    /// <summary>Counters for <see cref="PrefixCache"/>.</summary>
    /// <param name="Hits">Blocks served from the RAM tier.</param>
    /// <param name="DiskHits">Blocks served from the disk tier (and promoted to RAM).</param>
    /// <param name="Misses">Lookups that found no usable block.</param>
    /// <param name="Evictions">Blocks removed from the RAM tier.</param>
    /// <param name="DiskWrites">Blocks written to the disk tier.</param>
    /// <param name="CorruptFiles">Disk files rejected by validation (treated as misses).</param>
    public readonly record struct PrefixCacheStats(long Hits, long DiskHits, long Misses, long Evictions, long DiskWrites, long CorruptFiles);

    /// <summary>
    /// Automatic prefix cache with two tiers below the live decoder cache:
    /// tier 1 is RAM (LRU, byte budget), tier 2 is a disk directory (optional, unbounded here).
    /// Blocks are keyed by a chained hash of their tokens and their parent prefix. Every hit also checks the
    /// stored tokens, parent hash, mode, and shape, so a hash collision cannot return wrong state.
    /// Evicted blocks go to disk when a directory is configured, and disk hits are promoted back to RAM.
    /// Not thread-safe.
    /// </summary>
    public sealed class PrefixCache
    {
        /// <summary>Seed for the first block's parent hash (FNV-1a offset basis).</summary>
        public const ulong Seed = 0xcbf29ce484222325UL;

        private const string FileExtension = ".kvb";
        private const int FileVersion = 1;
        private static readonly byte[] Magic = { (byte)'K', (byte)'V', (byte)'B', (byte)'1' };

        private readonly Dictionary<ulong, LinkedListNode<KvBlock>> hot = new Dictionary<ulong, LinkedListNode<KvBlock>>();
        private readonly LinkedList<KvBlock> lru = new LinkedList<KvBlock>();
        private readonly string? diskDir;
        private long hits, diskHits, misses, evictions, diskWrites, corrupt;

        /// <summary>Creates a prefix cache for one model shape and precision.</summary>
        /// <param name="config">Model shape. Blocks from other shapes are never used.</param>
        /// <param name="mode">KV precision. Blocks from other precisions are never used.</param>
        /// <param name="blockTokens">Tokens per block, positive and at most MaxSeq.</param>
        /// <param name="hotBudgetBytes">RAM budget for tier 1. Must be positive.</param>
        /// <param name="diskDirectory">Directory for tier 2, or null to disable the disk tier.</param>
        public PrefixCache(ModelConfig config, KvMode mode, int blockTokens, long hotBudgetBytes, string? diskDirectory = null)
        {
            config.Validate();
            if (blockTokens < 1 || blockTokens > config.MaxSeq) throw new ArgumentOutOfRangeException(nameof(blockTokens));
            if (hotBudgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(hotBudgetBytes));
            Config = config;
            Mode = mode;
            BlockTokens = blockTokens;
            HotBudgetBytes = hotBudgetBytes;
            diskDir = diskDirectory;
            if (diskDir is not null) Directory.CreateDirectory(diskDir);
        }

        /// <summary>Model shape.</summary>
        public ModelConfig Config { get; }

        /// <summary>KV precision.</summary>
        public KvMode Mode { get; }

        /// <summary>Tokens per block.</summary>
        public int BlockTokens { get; }

        /// <summary>RAM tier budget in bytes.</summary>
        public long HotBudgetBytes { get; }

        /// <summary>Bytes currently held in the RAM tier.</summary>
        public long HotBytes { get; private set; }

        /// <summary>Blocks currently held in the RAM tier.</summary>
        public int HotCount => hot.Count;

        /// <summary>Current counters.</summary>
        public PrefixCacheStats Stats => new PrefixCacheStats(hits, diskHits, misses, evictions, diskWrites, corrupt);

        /// <summary>Extends a prefix hash with one block of tokens. Deterministic (FNV-1a over parent hash and tokens).</summary>
        /// <param name="parent">Hash of the preceding prefix, or <see cref="Seed"/> for none.</param>
        /// <param name="tokens">Tokens of the block.</param>
        public static ulong ChainHash(ulong parent, ReadOnlySpan<int> tokens)
        {
            ulong h = Seed;
            Mix(ref h, parent);
            for (int i = 0; i < tokens.Length; i++) Mix(ref h, (uint)tokens[i]);
            return h;
        }

        /// <summary>
        /// Looks up a block by hash. Returns it only if its parent, tokens, mode, and shape all match.
        /// Checks RAM first, then disk. A disk hit is promoted into RAM.
        /// </summary>
        /// <param name="hash">Chain hash of the block.</param>
        /// <param name="tokens">Tokens of the block the caller wants.</param>
        /// <param name="parent">Chain hash of the caller's preceding prefix.</param>
        /// <returns>The block, or null on a miss.</returns>
        public KvBlock? Lookup(ulong hash, ReadOnlySpan<int> tokens, ulong parent)
        {
            if (hot.TryGetValue(hash, out LinkedListNode<KvBlock>? node))
            {
                if (Matches(node.Value, hash, tokens, parent))
                {
                    lru.Remove(node);
                    lru.AddFirst(node);
                    hits++;
                    return node.Value;
                }
                misses++;
                return null;
            }

            KvBlock? fromDisk = ReadDisk(hash);
            if (fromDisk is not null && Matches(fromDisk, hash, tokens, parent))
            {
                diskHits++;
                Insert(fromDisk);
                return fromDisk;
            }
            misses++;
            return null;
        }

        /// <summary>
        /// Adds a block to the RAM tier. Evicts least-recently-used blocks to disk (or drops them without a disk
        /// directory) until the budget is met. Adding a block whose hash is already present does nothing.
        /// </summary>
        /// <param name="block">Block to store. Must match this cache's shape and mode.</param>
        public void Insert(KvBlock block)
        {
            if (block is null) throw new ArgumentNullException(nameof(block));
            if (block.Mode != Mode || !block.Config.Equals(Config))
                throw new ArgumentException("Block shape or precision does not match this cache.", nameof(block));
            if (hot.ContainsKey(block.Hash)) return;

            LinkedListNode<KvBlock> node = lru.AddFirst(block);
            hot[block.Hash] = node;
            HotBytes += block.Bytes;
            while (HotBytes > HotBudgetBytes && lru.Last is not null)
            {
                KvBlock victim = lru.Last.Value;
                lru.RemoveLast();
                hot.Remove(victim.Hash);
                HotBytes -= victim.Bytes;
                evictions++;
                if (diskDir is not null) WriteDisk(victim);
            }
        }

        /// <summary>Removes all RAM blocks. Disk files are kept.</summary>
        public void ClearHot()
        {
            hot.Clear();
            lru.Clear();
            HotBytes = 0;
        }

        private static bool Matches(KvBlock b, ulong hash, ReadOnlySpan<int> tokens, ulong parent)
        {
            if (b.Hash != hash || b.Parent != parent || b.Count != tokens.Length) return false;
            return tokens.SequenceEqual(b.Tokens);
        }

        private static void Mix(ref ulong h, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (byte)(value >> (8 * i));
                h *= 0x100000001b3UL;
            }
        }

        private string FilePath(ulong hash) => Path.Combine(diskDir!, hash.ToString("X16") + FileExtension);

        private void WriteDisk(KvBlock b)
        {
            string path = FilePath(b.Hash);
            if (File.Exists(path)) return;
            using FileStream fs = File.Create(path);
            using BinaryWriter w = new BinaryWriter(fs);
            w.Write(Magic);
            w.Write(FileVersion);
            w.Write((byte)b.Mode);
            w.Write(b.Config.Hidden);
            w.Write(b.Config.Layers);
            w.Write(b.Config.KvHeads);
            w.Write(b.Config.HeadDim);
            w.Write(b.Config.Vocab);
            w.Write(b.Count);
            w.Write(b.Hash);
            w.Write(b.Parent);
            w.Write(MemoryMarshal.AsBytes(b.Tokens.AsSpan()));
            w.Write(MemoryMarshal.AsBytes(b.Logits.AsSpan()));
            for (int l = 0; l < b.Config.Layers; l++)
            {
                if (b.Mode == KvMode.Fp32)
                {
                    w.Write(MemoryMarshal.AsBytes(b.KeysF[l].AsSpan()));
                    w.Write(MemoryMarshal.AsBytes(b.ValsF[l].AsSpan()));
                }
                else
                {
                    w.Write(MemoryMarshal.AsBytes(b.Keys8[l].AsSpan()));
                    w.Write(MemoryMarshal.AsBytes(b.Vals8[l].AsSpan()));
                    w.Write(MemoryMarshal.AsBytes(b.KeyScale[l].AsSpan()));
                    w.Write(MemoryMarshal.AsBytes(b.ValScale[l].AsSpan()));
                }
            }
            diskWrites++;
        }

        // Returns null for a missing file. A file that fails validation counts as corrupt and is treated as a miss.
        private KvBlock? ReadDisk(ulong hash)
        {
            if (diskDir is null) return null;
            string path = FilePath(hash);
            if (!File.Exists(path)) return null;
            try
            {
                using FileStream fs = File.OpenRead(path);
                using BinaryReader r = new BinaryReader(fs);
                byte[] magic = r.ReadBytes(4);
                if (!magic.AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("magic");
                if (r.ReadInt32() != FileVersion) throw new InvalidDataException("version");
                var mode = (KvMode)r.ReadByte();
                int hidden = r.ReadInt32(), layers = r.ReadInt32(), kvHeads = r.ReadInt32(), headDim = r.ReadInt32(), vocab = r.ReadInt32();
                if (mode != Mode || hidden != Config.Hidden || layers != Config.Layers || kvHeads != Config.KvHeads
                    || headDim != Config.HeadDim || vocab != Config.Vocab)
                    throw new InvalidDataException("shape");
                int count = r.ReadInt32();
                if (count < 1 || count > Config.MaxSeq) throw new InvalidDataException("count");
                ulong storedHash = r.ReadUInt64();
                ulong parent = r.ReadUInt64();
                if (storedHash != hash) throw new InvalidDataException("hash");

                KvBlock b = new KvBlock(storedHash, parent, mode, Config, count);
                ReadExact(r, MemoryMarshal.AsBytes(b.Tokens.AsSpan()));
                ReadExact(r, MemoryMarshal.AsBytes(b.Logits.AsSpan()));
                for (int l = 0; l < Config.Layers; l++)
                {
                    if (mode == KvMode.Fp32)
                    {
                        ReadExact(r, MemoryMarshal.AsBytes(b.KeysF[l].AsSpan()));
                        ReadExact(r, MemoryMarshal.AsBytes(b.ValsF[l].AsSpan()));
                    }
                    else
                    {
                        ReadExact(r, MemoryMarshal.AsBytes(b.Keys8[l].AsSpan()));
                        ReadExact(r, MemoryMarshal.AsBytes(b.Vals8[l].AsSpan()));
                        ReadExact(r, MemoryMarshal.AsBytes(b.KeyScale[l].AsSpan()));
                        ReadExact(r, MemoryMarshal.AsBytes(b.ValScale[l].AsSpan()));
                    }
                }
                if (fs.Position != fs.Length) throw new InvalidDataException("trailing bytes");
                return b;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is EndOfStreamException)
            {
                corrupt++;
                return null;
            }
        }

        private static void ReadExact(BinaryReader r, Span<byte> dst)
        {
            int got = 0;
            while (got < dst.Length)
            {
                int n = r.Read(dst.Slice(got));
                if (n == 0) throw new EndOfStreamException();
                got += n;
            }
        }
    }

    /// <summary>Copies KV state between a live <see cref="KvCache"/> and a <see cref="KvBlock"/>.</summary>
    internal static class KvBlockCodec
    {
        /// <summary>Copies positions [start, start + count) of every layer and KV head into a new block.</summary>
        public static KvBlock Capture(KvCache cache, int start, int count, ulong hash, ulong parent, ReadOnlySpan<int> tokens, ReadOnlySpan<float> logits)
        {
            ModelConfig c = cache.Config;
            KvBlock b = new KvBlock(hash, parent, cache.Mode, c, count);
            tokens.CopyTo(b.Tokens);
            logits.CopyTo(b.Logits);
            int hd = c.HeadDim;
            for (int l = 0; l < c.Layers; l++)
            {
                for (int g = 0; g < c.KvHeads; g++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int src = g * c.MaxSeq + start + i;
                        int dst = g * count + i;
                        if (cache.Mode == KvMode.Fp32)
                        {
                            Array.Copy(cache.KeysF(l), src * hd, b.KeysF[l], dst * hd, hd);
                            Array.Copy(cache.ValsF(l), src * hd, b.ValsF[l], dst * hd, hd);
                        }
                        else
                        {
                            Array.Copy(cache.Keys8(l), src * hd, b.Keys8[l], dst * hd, hd);
                            Array.Copy(cache.Vals8(l), src * hd, b.Vals8[l], dst * hd, hd);
                            b.KeyScale[l][dst] = cache.KeyScale(l)[src];
                            b.ValScale[l][dst] = cache.ValScale(l)[src];
                        }
                    }
                }
            }
            return b;
        }

        /// <summary>Writes a block's rows into positions [start, start + block.Count) of a live cache. Exact copy.</summary>
        public static void Restore(KvBlock b, KvCache cache, int start)
        {
            ModelConfig c = cache.Config;
            int hd = c.HeadDim;
            for (int l = 0; l < c.Layers; l++)
            {
                for (int g = 0; g < c.KvHeads; g++)
                {
                    for (int i = 0; i < b.Count; i++)
                    {
                        int dst = g * c.MaxSeq + start + i;
                        int src = g * b.Count + i;
                        if (cache.Mode == KvMode.Fp32)
                        {
                            Array.Copy(b.KeysF[l], src * hd, cache.KeysF(l), dst * hd, hd);
                            Array.Copy(b.ValsF[l], src * hd, cache.ValsF(l), dst * hd, hd);
                        }
                        else
                        {
                            Array.Copy(b.Keys8[l], src * hd, cache.Keys8(l), dst * hd, hd);
                            Array.Copy(b.Vals8[l], src * hd, cache.Vals8(l), dst * hd, hd);
                            cache.KeyScale(l)[dst] = b.KeyScale[l][src];
                            cache.ValScale(l)[dst] = b.ValScale[l][src];
                        }
                    }
                }
            }
        }
    }
}
