using System;
using System.Collections.Generic;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Shared pool of fixed-size KV blocks. A block holds <see cref="BlockSize"/> consecutive positions for every
    /// layer and every KV head. Blocks are referenced by <see cref="PagedKvSequence"/> block tables; a block with
    /// more than one reference is shared and is copied before it is written (copy-on-write).
    /// Not thread-safe.
    /// </summary>
    public sealed class KvBlockPool
    {
        private readonly int[] refs;
        private readonly Stack<int> free;

        internal readonly float[][] KeysF = Array.Empty<float[]>();
        internal readonly float[][] ValsF = Array.Empty<float[]>();
        internal readonly sbyte[][] Keys8 = Array.Empty<sbyte[]>();
        internal readonly sbyte[][] Vals8 = Array.Empty<sbyte[]>();
        internal readonly float[][] KeyScale = Array.Empty<float[]>();
        internal readonly float[][] ValScale = Array.Empty<float[]>();

        /// <summary>Creates a pool.</summary>
        /// <param name="config">Model shape.</param>
        /// <param name="mode">KV precision.</param>
        /// <param name="blockSize">Positions per block. Must be a power of two, at most MaxSeq.</param>
        /// <param name="blockCount">Number of physical blocks, positive.</param>
        public KvBlockPool(ModelConfig config, KvMode mode, int blockSize, int blockCount)
        {
            config.Validate();
            if (blockSize < 1 || (blockSize & (blockSize - 1)) != 0 || blockSize > config.MaxSeq)
                throw new ArgumentOutOfRangeException(nameof(blockSize), "Block size must be a power of two, at most MaxSeq.");
            if (blockCount < 1) throw new ArgumentOutOfRangeException(nameof(blockCount));
            Config = config;
            Mode = mode;
            BlockSize = blockSize;
            BlockCount = blockCount;
            refs = new int[blockCount];
            free = new Stack<int>(blockCount);
            for (int b = blockCount - 1; b >= 0; b--) free.Push(b);

            long elems = (long)blockCount * config.KvHeads * blockSize * config.HeadDim;
            long rows = (long)blockCount * config.KvHeads * blockSize;
            if (elems > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(blockCount), "Pool too large for one array per layer.");
            if (mode == KvMode.Fp32)
            {
                KeysF = new float[config.Layers][];
                ValsF = new float[config.Layers][];
                for (int l = 0; l < config.Layers; l++)
                {
                    KeysF[l] = new float[elems];
                    ValsF[l] = new float[elems];
                }
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
            }
        }

        /// <summary>Model shape.</summary>
        public ModelConfig Config { get; }

        /// <summary>KV precision.</summary>
        public KvMode Mode { get; }

        /// <summary>Positions per block.</summary>
        public int BlockSize { get; }

        /// <summary>Total physical blocks.</summary>
        public int BlockCount { get; }

        /// <summary>Blocks not referenced by any sequence.</summary>
        public int FreeCount => free.Count;

        /// <summary>Takes a free block with reference count 1.</summary>
        /// <exception cref="InvalidOperationException">Thrown when no block is free.</exception>
        public int Allocate()
        {
            if (free.Count == 0) throw new InvalidOperationException("KV block pool is exhausted.");
            int b = free.Pop();
            refs[b] = 1;
            return b;
        }

        /// <summary>Adds one reference to a block.</summary>
        /// <param name="block">Block id.</param>
        public void Retain(int block)
        {
            CheckBlock(block);
            if (refs[block] == 0) throw new InvalidOperationException("Cannot retain a free block.");
            refs[block]++;
        }

        /// <summary>Removes one reference. The block returns to the free list when no references remain.</summary>
        /// <param name="block">Block id.</param>
        public void Release(int block)
        {
            CheckBlock(block);
            if (refs[block] == 0) throw new InvalidOperationException("Block is already free.");
            if (--refs[block] == 0) free.Push(block);
        }

        /// <summary>Current reference count of a block.</summary>
        /// <param name="block">Block id.</param>
        public int RefCount(int block)
        {
            CheckBlock(block);
            return refs[block];
        }

        /// <summary>Approximate bytes of value storage held by the pool.</summary>
        public long Bytes
        {
            get
            {
                long rows = (long)BlockCount * Config.KvHeads * BlockSize;
                return Mode == KvMode.Fp32
                    ? rows * Config.HeadDim * 8L * Config.Layers
                    : rows * (Config.HeadDim * 2L + 8L) * Config.Layers;
            }
        }

        /// <summary>Copies every layer and KV head of one block into another. Used by copy-on-write.</summary>
        internal void CopyBlock(int src, int dst)
        {
            int hd = Config.HeadDim;
            int n = Config.KvHeads * BlockSize * hd;
            int rowsN = Config.KvHeads * BlockSize;
            for (int l = 0; l < Config.Layers; l++)
            {
                if (Mode == KvMode.Fp32)
                {
                    Array.Copy(KeysF[l], (long)src * n, KeysF[l], (long)dst * n, n);
                    Array.Copy(ValsF[l], (long)src * n, ValsF[l], (long)dst * n, n);
                }
                else
                {
                    Array.Copy(Keys8[l], (long)src * n, Keys8[l], (long)dst * n, n);
                    Array.Copy(Vals8[l], (long)src * n, Vals8[l], (long)dst * n, n);
                    Array.Copy(KeyScale[l], (long)src * rowsN, KeyScale[l], (long)dst * rowsN, rowsN);
                    Array.Copy(ValScale[l], (long)src * rowsN, ValScale[l], (long)dst * rowsN, rowsN);
                }
            }
        }

        /// <summary>Copies one pool block (all layers, all heads) into a new <see cref="KvBlock"/> for the disk tier.</summary>
        internal KvBlock CaptureBlock(int block, ulong hash, ulong parent, int[] tokens, float[] logits)
        {
            CheckBlock(block);
            KvBlock kb = new KvBlock(hash, parent, Mode, Config, BlockSize);
            Array.Copy(tokens, kb.Tokens, tokens.Length);
            Array.Copy(logits, kb.Logits, logits.Length);
            long n = (long)Config.KvHeads * BlockSize * Config.HeadDim;
            long rows = (long)Config.KvHeads * BlockSize;
            for (int l = 0; l < Config.Layers; l++)
            {
                if (Mode == KvMode.Fp32)
                {
                    Array.Copy(KeysF[l], block * n, kb.KeysF[l], 0, n);
                    Array.Copy(ValsF[l], block * n, kb.ValsF[l], 0, n);
                }
                else
                {
                    Array.Copy(Keys8[l], block * n, kb.Keys8[l], 0, n);
                    Array.Copy(Vals8[l], block * n, kb.Vals8[l], 0, n);
                    Array.Copy(KeyScale[l], block * rows, kb.KeyScale[l], 0, rows);
                    Array.Copy(ValScale[l], block * rows, kb.ValScale[l], 0, rows);
                }
            }
            return kb;
        }

        /// <summary>Writes a block from the disk tier back into a pool block. Exact copy.</summary>
        internal void RestoreBlock(KvBlock kb, int block)
        {
            CheckBlock(block);
            if (kb.Mode != Mode || !kb.Config.Equals(Config) || kb.Count != BlockSize)
                throw new ArgumentException("Block does not match this pool.", nameof(kb));
            long n = (long)Config.KvHeads * BlockSize * Config.HeadDim;
            long rows = (long)Config.KvHeads * BlockSize;
            for (int l = 0; l < Config.Layers; l++)
            {
                if (Mode == KvMode.Fp32)
                {
                    Array.Copy(kb.KeysF[l], 0, KeysF[l], block * n, n);
                    Array.Copy(kb.ValsF[l], 0, ValsF[l], block * n, n);
                }
                else
                {
                    Array.Copy(kb.Keys8[l], 0, Keys8[l], block * n, n);
                    Array.Copy(kb.Vals8[l], 0, Vals8[l], block * n, n);
                    Array.Copy(kb.KeyScale[l], 0, KeyScale[l], block * rows, rows);
                    Array.Copy(kb.ValScale[l], 0, ValScale[l], block * rows, rows);
                }
            }
        }

        private void CheckBlock(int block)
        {
            if ((uint)block >= BlockCount) throw new ArgumentOutOfRangeException(nameof(block));
        }
    }

    /// <summary>
    /// One sequence's view of a <see cref="KvBlockPool"/>: a block table mapping logical block index
    /// (position / BlockSize) to a physical block id. Sequences that share a prefix share its blocks by reference.
    /// Not thread-safe.
    /// </summary>
    public sealed class PagedKvSequence
    {
        private readonly List<int> table = new List<int>();

        /// <summary>Creates an empty sequence on a pool.</summary>
        /// <param name="pool">Backing pool.</param>
        public PagedKvSequence(KvBlockPool pool)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>Backing pool.</summary>
        public KvBlockPool Pool { get; }

        /// <summary>Number of positions written (highest position + 1).</summary>
        public int Length { get; private set; }

        /// <summary>Physical block ids in logical order. Read-only view.</summary>
        public IReadOnlyList<int> Table => table;

        /// <summary>
        /// Writes one key/value vector (KvHeads * HeadDim values) at a position of one layer. Allocates the block
        /// for the position when needed, and copies a shared block before writing (copy-on-write).
        /// Positions must be written in order within a block; the decoder guarantees this.
        /// </summary>
        /// <param name="layer">Layer index.</param>
        /// <param name="pos">Position.</param>
        /// <param name="k">Keys, KvHeads * HeadDim values.</param>
        /// <param name="v">Values, KvHeads * HeadDim values.</param>
        public void Write(int layer, int pos, ReadOnlySpan<float> k, ReadOnlySpan<float> v)
        {
            ModelConfig c = Pool.Config;
            if ((uint)layer >= c.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            if ((uint)pos >= c.MaxSeq) throw new ArgumentOutOfRangeException(nameof(pos));
            if (k.Length != c.KvDim || v.Length != c.KvDim)
                throw new ArgumentException($"Key and value vectors must have {c.KvDim} values.");

            int logical = pos / Pool.BlockSize;
            while (table.Count <= logical) table.Add(Pool.Allocate());
            int block = EnsureWritable(logical);

            int hd = c.HeadDim;
            int off = pos % Pool.BlockSize;
            for (int g = 0; g < c.KvHeads; g++)
            {
                int row = (block * c.KvHeads + g) * Pool.BlockSize + off;
                if (Pool.Mode == KvMode.Fp32)
                {
                    k.Slice(g * hd, hd).CopyTo(Pool.KeysF[layer].AsSpan(row * hd, hd));
                    v.Slice(g * hd, hd).CopyTo(Pool.ValsF[layer].AsSpan(row * hd, hd));
                }
                else
                {
                    Pool.KeyScale[layer][row] = KvCacheQuant.Quantize(k.Slice(g * hd, hd), Pool.Keys8[layer].AsSpan(row * hd, hd));
                    Pool.ValScale[layer][row] = KvCacheQuant.Quantize(v.Slice(g * hd, hd), Pool.Vals8[layer].AsSpan(row * hd, hd));
                }
            }
            if (pos + 1 > Length) Length = pos + 1;
        }

        /// <summary>
        /// Creates a sequence that shares every block of this one (reference counts go up). The fork and this
        /// sequence diverge through copy-on-write when either writes into a shared block.
        /// </summary>
        public PagedKvSequence Fork()
        {
            PagedKvSequence f = new PagedKvSequence(Pool);
            foreach (int b in table)
            {
                Pool.Retain(b);
                f.table.Add(b);
            }
            f.Length = Length;
            return f;
        }

        /// <summary>
        /// Appends an existing physical block as the next full block of this sequence, sharing it by reference.
        /// The sequence must end on a block boundary. The block must hold BlockSize completed positions.
        /// </summary>
        /// <param name="block">Physical block id.</param>
        public void AppendShared(int block)
        {
            if (Length % Pool.BlockSize != 0 || table.Count != Length / Pool.BlockSize)
                throw new InvalidOperationException("Shared blocks can only be appended at a block boundary.");
            Pool.Retain(block);
            table.Add(block);
            Length += Pool.BlockSize;
        }

        /// <summary>Releases all blocks and resets the sequence to empty.</summary>
        public void Clear()
        {
            foreach (int b in table) Pool.Release(b);
            table.Clear();
            Length = 0;
        }

        // Returns the physical block for a logical index, copying it first if it is shared.
        private int EnsureWritable(int logical)
        {
            int block = table[logical];
            if (Pool.RefCount(block) == 1) return block;
            int fresh = Pool.Allocate();
            Pool.CopyBlock(block, fresh);
            Pool.Release(block);
            table[logical] = fresh;
            return fresh;
        }
    }

    /// <summary>Counters for <see cref="PagedPrefixIndex"/>.</summary>
    /// <param name="Hits">Blocks reused from the RAM index.</param>
    /// <param name="DiskHits">Blocks restored from the disk tier into a pool block.</param>
    /// <param name="Misses">Lookups that found no usable block.</param>
    /// <param name="Evictions">Entries removed from the index.</param>
    public readonly record struct PagedPrefixStats(long Hits, long DiskHits, long Misses, long Evictions);

    /// <summary>
    /// Prefix index over a <see cref="KvBlockPool"/>. Each entry names a full pool block by chain hash and holds
    /// one reference to it, so sequences that reuse the prefix share the block without copying. Entries are evicted
    /// least-recently-used once the entry limit is reached, which releases the index's reference.
    /// Every hit checks the stored tokens and parent hash. Not thread-safe.
    /// </summary>
    public sealed class PagedPrefixIndex
    {
        private sealed class Entry
        {
            public Entry(ulong hash, ulong parent, int[] tokens, int block, float[] logits)
            {
                Hash = hash; Parent = parent; Tokens = tokens; Block = block; Logits = logits;
            }
            public ulong Hash { get; }
            public ulong Parent { get; }
            public int[] Tokens { get; }
            public int Block { get; }
            public float[] Logits { get; }
        }

        private readonly Dictionary<ulong, LinkedListNode<Entry>> map = new Dictionary<ulong, LinkedListNode<Entry>>();
        private readonly LinkedList<Entry> lru = new LinkedList<Entry>();
        private readonly int maxEntries;
        private readonly PrefixCache? spill;
        private long hits, diskHits, misses, evictions;

        /// <summary>Creates an index for one pool.</summary>
        /// <param name="pool">Backing pool. Its block size is the prefix block size.</param>
        /// <param name="maxEntries">Maximum number of indexed blocks, positive. Each one pins a pool block.</param>
        /// <param name="spill">
        /// Optional disk tier. Evicted entries are copied out and written to it; lookups that miss in RAM check it and,
        /// on a hit, restore the block into a free pool block. Must match the pool's shape, precision, and block size.
        /// </param>
        public PagedPrefixIndex(KvBlockPool pool, int maxEntries, PrefixCache? spill = null)
        {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
            if (spill is not null &&
                (spill.Mode != pool.Mode || !spill.Config.Equals(pool.Config) || spill.BlockTokens != pool.BlockSize))
                throw new ArgumentException("Spill store must match the pool's shape, precision, and block size.", nameof(spill));
            this.maxEntries = maxEntries;
            this.spill = spill;
        }

        /// <summary>Backing pool.</summary>
        public KvBlockPool Pool { get; }

        /// <summary>Indexed blocks.</summary>
        public int Count => map.Count;

        /// <summary>Current counters.</summary>
        public PagedPrefixStats Stats => new PagedPrefixStats(hits, diskHits, misses, evictions);

        /// <summary>
        /// Looks up a block. Returns its physical id and logits only if the parent hash and tokens match.
        /// </summary>
        /// <param name="hash">Chain hash of the block.</param>
        /// <param name="tokens">Tokens of the block.</param>
        /// <param name="parent">Chain hash of the preceding prefix.</param>
        /// <param name="block">Physical block id on a hit.</param>
        /// <param name="logits">Logits after the block's last token on a hit (a copy).</param>
        public bool TryGet(ulong hash, ReadOnlySpan<int> tokens, ulong parent, out int block, out float[] logits)
        {
            block = -1;
            logits = Array.Empty<float>();
            if (map.TryGetValue(hash, out LinkedListNode<Entry>? node)
                && node.Value.Parent == parent && tokens.SequenceEqual(node.Value.Tokens))
            {
                lru.Remove(node);
                lru.AddFirst(node);
                hits++;
                block = node.Value.Block;
                logits = (float[])node.Value.Logits.Clone();
                return true;
            }
            if (spill is not null && Pool.FreeCount > 0)
            {
                KvBlock? kb = spill.Lookup(hash, tokens, parent);
                if (kb is not null)
                {
                    // Restore into a fresh pool block. Insert retains it for the index; then drop our allocation reference.
                    int b = Pool.Allocate();
                    Pool.RestoreBlock(kb, b);
                    Insert(hash, parent, tokens, b, kb.Logits);
                    Pool.Release(b);
                    diskHits++;
                    block = map[hash].Value.Block;
                    logits = (float[])kb.Logits.Clone();
                    return true;
                }
            }
            misses++;
            return false;
        }

        /// <summary>
        /// Indexes a full block of a sequence. Retains the block, so it outlives the sequence that wrote it.
        /// Inserting a hash that is already indexed does nothing.
        /// </summary>
        /// <param name="hash">Chain hash of the block.</param>
        /// <param name="parent">Chain hash of the preceding prefix.</param>
        /// <param name="tokens">Tokens of the block (copied).</param>
        /// <param name="block">Physical block id, which must hold a complete block.</param>
        /// <param name="logits">Logits after the block's last token (copied).</param>
        public void Insert(ulong hash, ulong parent, ReadOnlySpan<int> tokens, int block, ReadOnlySpan<float> logits)
        {
            if (tokens.Length != Pool.BlockSize) throw new ArgumentException("Indexed blocks must hold a full block of tokens.", nameof(tokens));
            if (map.ContainsKey(hash)) return;
            Pool.Retain(block);
            Entry e = new Entry(hash, parent, tokens.ToArray(), block, logits.ToArray());
            map[hash] = lru.AddFirst(e);
            while (map.Count > maxEntries)
            {
                Entry victim = lru.Last!.Value;
                lru.RemoveLast();
                map.Remove(victim.Hash);
                spill?.Insert(Pool.CaptureBlock(victim.Block, victim.Hash, victim.Parent, victim.Tokens, victim.Logits));
                Pool.Release(victim.Block);
                evictions++;
            }
        }

        /// <summary>Drops every entry and releases their references. Entries are not spilled; the disk tier is kept.</summary>
        public void Clear()
        {
            foreach (Entry e in lru) Pool.Release(e.Block);
            map.Clear();
            lru.Clear();
        }
    }

    /// <summary>Quantization helper shared by the contiguous and paged caches.</summary>
    internal static class KvCacheQuant
    {
        /// <summary>Symmetric int8 quantization. Returns the dequantization scale (max|x| / 127).</summary>
        public static float Quantize(ReadOnlySpan<float> src, Span<sbyte> dst)
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
    }
}
