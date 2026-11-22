using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OPEN_LRD.Persistence
{
    internal enum DiskPartition : byte
    {
        Symbols = 1,
        Words = 2,
        Sentences = 3,
        Senses = 4,
        Usages = 5,
        Groups = 6,
        Sources = 7,
        Functions = 8,
        Nodes = 9,
        Links = 10,
        NodePresentations = 11,
        LayoutSettings = 12
    }

    internal readonly record struct DiskRecord(Guid Key, byte[] Payload);
    internal readonly record struct DiskPartitionDiagnostics(DiskPartition Partition, int RecordCount, long DataBytes, long IndexBytes);
    /// <summary>Validation counts for trusted function provenance integrity in a checkpoint.</summary>
    internal sealed record DiskValidationProvenanceSummary(int VerifiedProvenanceCount, int UnverifiedProvenanceCount, int TamperedProvenanceCount);

    internal sealed record DiskStoreDiagnostics(string? StorePath, int FormatVersion, bool IsPagedReadOnly,
        long CacheHits, long CacheMisses, string? LastValidationError,
        DiskValidationProvenanceSummary? LastValidationProvenanceSummary,
        IReadOnlyList<DiskPartitionDiagnostics> Partitions);

    internal sealed class PartitionedBinaryStore : IDisposable
    {
        private const int CurrentFormatVersion = 3;
        private const int LegacyFormatVersion = 1;
        private const int PreviousFormatVersion = 2;
        private const int MaximumRecordPayloadLength = 64 * 1024 * 1024;
        private const int MaximumRecordsPerPartition = 5_000_000;
        private const int MaximumCachedRecordsPerPartition = 4_096;
        private const int DataMagic = 0x44524C4F; // OLRD, little-endian
        private const int IndexMagic = 0x49524C4F;
        private const int ManifestMagic = 0x4D524C4F;
        private static readonly DiskPartition[] AllPartitions = Enum.GetValues<DiskPartition>();

        private readonly string directoryPath;
        private readonly int maxCachedRecords;
        private readonly Dictionary<DiskPartition, PartitionReader> readers = new Dictionary<DiskPartition, PartitionReader>();
        private bool disposed;
        internal int FormatVersion { get; private set; }
        internal bool IsLegacyFormat => FormatVersion == LegacyFormatVersion;

        private PartitionedBinaryStore(string directoryPath, int maxCachedRecords)
        {
            this.directoryPath = directoryPath;
            this.maxCachedRecords = Math.Max(0, maxCachedRecords);
            OpenAndValidate();
        }

        internal static PartitionedBinaryStore Create(string directoryPath,
            IReadOnlyDictionary<DiskPartition, IEnumerable<DiskRecord>> partitions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            ArgumentNullException.ThrowIfNull(partitions);
            string fullPath = Path.GetFullPath(directoryPath);
            string parent = Path.GetDirectoryName(fullPath) ?? throw new ArgumentException("A parent directory is required.", nameof(directoryPath));
            Directory.CreateDirectory(parent);
            string staging = fullPath + ".staging-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                Dictionary<DiskPartition, PartitionManifestEntry> manifest = new Dictionary<DiskPartition, PartitionManifestEntry>();
                foreach (DiskPartition partition in AllPartitions)
                {
                    IEnumerable<DiskRecord> records = partitions.TryGetValue(partition, out IEnumerable<DiskRecord>? supplied)
                        ? supplied
                        : Array.Empty<DiskRecord>();
                    manifest.Add(partition, WritePartition(staging, partition, records));
                }
                WriteManifest(staging, manifest);

                string backup = fullPath + ".previous-" + Guid.NewGuid().ToString("N");
                bool hadExisting = Directory.Exists(fullPath);
                if (hadExisting) Directory.Move(fullPath, backup);
                try
                {
                    Directory.Move(staging, fullPath);
                    if (hadExisting) Directory.Delete(backup, true);
                }
                catch
                {
                    if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
                    if (hadExisting && Directory.Exists(backup)) Directory.Move(backup, fullPath);
                    throw;
                }
            }
            catch
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                throw;
            }
            return new PartitionedBinaryStore(fullPath, 0);
        }

        internal static PartitionedBinaryStore Open(string directoryPath, int maxCachedRecordsPerPartition = 128)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            if (maxCachedRecordsPerPartition < 0 || maxCachedRecordsPerPartition > MaximumCachedRecordsPerPartition)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCachedRecordsPerPartition),
                    $"Cache limit must be between 0 and {MaximumCachedRecordsPerPartition} records per partition.");
            }
            return new PartitionedBinaryStore(Path.GetFullPath(directoryPath), maxCachedRecordsPerPartition);
        }

        internal bool TryRead(DiskPartition partition, Guid key, out byte[] payload)
        {
            ThrowIfDisposed();
            if (!readers.TryGetValue(partition, out PartitionReader? reader) || !reader.Index.TryGetValue(key, out IndexEntry entry))
            {
                payload = Array.Empty<byte>();
                return false;
            }
            payload = reader.Read(key, entry, maxCachedRecords);
            return true;
        }

        internal IEnumerable<DiskRecord> ReadAll(DiskPartition partition)
        {
            ThrowIfDisposed();
            if (!readers.TryGetValue(partition, out PartitionReader? reader)) yield break;
            foreach (KeyValuePair<Guid, IndexEntry> entry in reader.Index.OrderBy(pair => pair.Value.Offset))
            {
                yield return new DiskRecord(entry.Key, reader.Read(entry.Key, entry.Value, maxCachedRecords));
            }
        }

        internal int GetRecordCount(DiskPartition partition)
        {
            ThrowIfDisposed();
            return readers.TryGetValue(partition, out PartitionReader? reader) ? reader.Index.Count : 0;
        }

        internal IReadOnlyList<DiskPartitionDiagnostics> GetPartitionDiagnostics()
        {
            ThrowIfDisposed();
            List<DiskPartitionDiagnostics> result = new List<DiskPartitionDiagnostics>(AllPartitions.Length);
            foreach (DiskPartition partition in AllPartitions)
            {
                string dataPath = DataPath(directoryPath, partition);
                string indexPath = IndexPath(directoryPath, partition);
                result.Add(new DiskPartitionDiagnostics(partition, GetRecordCount(partition),
                    File.Exists(dataPath) ? new FileInfo(dataPath).Length : 0L,
                    File.Exists(indexPath) ? new FileInfo(indexPath).Length : 0L));
            }
            return result.AsReadOnly();
        }

        internal bool HasPartition(DiskPartition partition)
        {
            ThrowIfDisposed();
            return readers.ContainsKey(partition);
        }

        internal bool Contains(DiskPartition partition, Guid key)
        {
            ThrowIfDisposed();
            return readers.TryGetValue(partition, out PartitionReader? reader) && reader.Index.ContainsKey(key);
        }

        internal void Validate()
        {
            ThrowIfDisposed();
            foreach (KeyValuePair<DiskPartition, PartitionReader> reader in readers)
            {
                foreach (KeyValuePair<Guid, IndexEntry> entry in reader.Value.Index)
                {
                    _ = reader.Value.Read(entry.Key, entry.Value, 0);
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (PartitionReader reader in readers.Values) reader.Dispose();
            readers.Clear();
        }

        private void OpenAndValidate()
        {
            var manifestInfo = ReadManifest(directoryPath);
            FormatVersion = manifestInfo.FormatVersion;
            Dictionary<DiskPartition, PartitionManifestEntry> manifest = manifestInfo.Entries;
            foreach (DiskPartition partition in AllPartitions)
            {
                if (!manifest.TryGetValue(partition, out PartitionManifestEntry item))
                {
                    if (partition == DiskPartition.NodePresentations || partition == DiskPartition.LayoutSettings)
                    {
                        continue;
                    }
                    throw new InvalidDataException($"Manifest is missing partition {partition}.");
                }
                string dataPath = DataPath(directoryPath, partition);
                string indexPath = IndexPath(directoryPath, partition);
                ValidateHash(dataPath, item.DataHash);
                ValidateHash(indexPath, item.IndexHash);
                PartitionReader reader = new PartitionReader(dataPath, indexPath, partition, FormatVersion);
                if (reader.Index.Count != item.RecordCount)
                {
                    reader.Dispose();
                    throw new InvalidDataException($"Record count mismatch in {partition} partition.");
                }
                readers.Add(partition, reader);
            }
            Validate();
        }

        private static PartitionManifestEntry WritePartition(string directory, DiskPartition partition, IEnumerable<DiskRecord> records)
        {
            string dataPath = DataPath(directory, partition);
            string indexPath = IndexPath(directory, partition);
            int count = 0;
            byte[] dataDigest;
            HashSet<Guid> keys = new HashSet<Guid>();
            using (FileStream data = new FileStream(dataPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan))
            using (IncrementalHash dataHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (HashingWriteStream hashingData = new HashingWriteStream(data, dataHash))
            using (BinaryWriter writer = new BinaryWriter(hashingData))
            using (FileStream index = new FileStream(indexPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan))
            using (IncrementalHash indexHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (HashingWriteStream hashingIndex = new HashingWriteStream(index, indexHash))
            using (BinaryWriter indexWriter = new BinaryWriter(hashingIndex))
            {
                writer.Write(DataMagic);
                writer.Write(CurrentFormatVersion);
                writer.Write((byte)partition);
                indexWriter.Write(IndexMagic);
                indexWriter.Write(CurrentFormatVersion);
                indexWriter.Write(0);
                writer.Flush();
                indexWriter.Flush();
                foreach (DiskRecord record in records)
                {
                    if (count >= MaximumRecordsPerPartition)
                        throw new InvalidDataException($"Partition {partition} exceeds the record-count limit.");
                    if (record.Key == Guid.Empty || !keys.Add(record.Key))
                        throw new InvalidDataException($"Partition {partition} contains an empty or duplicate key.");
                    if (record.Payload == null || record.Payload.Length > MaximumRecordPayloadLength)
                        throw new InvalidDataException($"Partition {partition} contains an invalid payload.");
                    long recordOffset = data.Position;
                    writer.Write(record.Key.ToByteArray());
                    writer.Write(record.Payload.Length);
                    writer.Write(record.Payload);
                    writer.Write(Crc32(record.Payload));
                    indexWriter.Write(record.Key.ToByteArray());
                    indexWriter.Write(recordOffset);
                    indexWriter.Write(record.Payload.Length);
                    count++;
                }
                writer.Flush();
                indexWriter.Flush();
                data.Flush(true);
                index.Flush(true);
                dataDigest = dataHash.GetHashAndReset();
            }

            byte[] indexDigest;
            using (FileStream index = new FileStream(indexPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                index.Position = 8;
                index.Write(BitConverter.GetBytes(count));
                index.Flush(true);
                index.Position = 0;
                using IncrementalHash finalIndexHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[65536];
                int read;
                while ((read = index.Read(buffer)) > 0) finalIndexHash.AppendData(buffer, 0, read);
                indexDigest = finalIndexHash.GetHashAndReset();
            }
            return new PartitionManifestEntry(count, dataDigest, indexDigest);
        }

        private static void WriteManifest(string directory, IReadOnlyDictionary<DiskPartition, PartitionManifestEntry> entries)
        {
            string path = Path.Combine(directory, "manifest.lrdmeta");
            using FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(ManifestMagic);
            writer.Write(CurrentFormatVersion);
            writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            writer.Write(entries.Count);
            foreach (DiskPartition partition in AllPartitions)
            {
                PartitionManifestEntry entry = entries[partition];
                writer.Write((byte)partition);
                writer.Write(entry.RecordCount);
                writer.Write(entry.DataHash);
                writer.Write(entry.IndexHash);
            }
            writer.Flush();
            stream.Flush(true);
        }

        private static (int FormatVersion, Dictionary<DiskPartition, PartitionManifestEntry> Entries) ReadManifest(string directory)
        {
            string path = Path.Combine(directory, "manifest.lrdmeta");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new BinaryReader(stream);
            int magic = reader.ReadInt32();
            int formatVersion = reader.ReadInt32();
            if (magic != ManifestMagic || formatVersion is not (LegacyFormatVersion or PreviousFormatVersion or CurrentFormatVersion))
            {
                throw new InvalidDataException("Unsupported or invalid disk-store manifest.");
            }
            _ = reader.ReadInt64();
            int count = reader.ReadInt32();
            int minimumCount = AllPartitions.Length - 2;
            if (count < minimumCount || count > AllPartitions.Length)
                throw new InvalidDataException("Manifest partition count is invalid.");
            Dictionary<DiskPartition, PartitionManifestEntry> result = new Dictionary<DiskPartition, PartitionManifestEntry>();
            for (int i = 0; i < count; i++)
            {
                DiskPartition partition = (DiskPartition)reader.ReadByte();
                int recordCount = reader.ReadInt32();
                byte[] dataHash = reader.ReadBytes(32);
                byte[] indexHash = reader.ReadBytes(32);
                if (!Enum.IsDefined(partition) || recordCount < 0 || recordCount > MaximumRecordsPerPartition ||
                    dataHash.Length != 32 || indexHash.Length != 32 ||
                    !result.TryAdd(partition, new PartitionManifestEntry(recordCount, dataHash, indexHash)))
                {
                    throw new InvalidDataException("Manifest contains an invalid partition entry.");
                }
            }
            foreach (DiskPartition partition in AllPartitions)
            {
                if (partition != DiskPartition.NodePresentations && partition != DiskPartition.LayoutSettings &&
                    !result.ContainsKey(partition))
                {
                    throw new InvalidDataException($"Manifest is missing partition {partition}.");
                }
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Manifest has trailing data.");
            return (formatVersion, result);
        }

        private static void ValidateHash(string path, byte[] expected)
        {
            if (!CryptographicOperations.FixedTimeEquals(ComputeHash(path), expected))
            {
                throw new InvalidDataException($"Checksum validation failed for '{Path.GetFileName(path)}'.");
            }
        }

        private static byte[] ComputeHash(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
            return SHA256.HashData(stream);
        }

        private static uint Crc32(ReadOnlySpan<byte> bytes)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte value in bytes)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = (uint)-(int)(crc & 1);
                    crc = (crc >> 1) ^ (0xEDB88320 & mask);
                }
            }
            return ~crc;
        }

        private static string DataPath(string directory, DiskPartition partition) => Path.Combine(directory, PartitionName(partition) + ".lrdpart");
        private static string IndexPath(string directory, DiskPartition partition) => Path.Combine(directory, PartitionName(partition) + ".lrdidx");
        private static string PartitionName(DiskPartition partition) => partition.ToString().ToLowerInvariant();

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        private readonly record struct IndexEntry(long Offset, int PayloadLength);
        private readonly record struct PartitionManifestEntry(int RecordCount, byte[] DataHash, byte[] IndexHash);

        private sealed class HashingWriteStream : Stream
        {
            private readonly Stream destination;
            private readonly IncrementalHash hash;

            internal HashingWriteStream(Stream destination, IncrementalHash hash)
            {
                this.destination = destination;
                this.hash = hash;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => destination.CanWrite;
            public override long Length => destination.Length;
            public override long Position { get => destination.Position; set => throw new NotSupportedException(); }
            public override void Flush() => destination.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                hash.AppendData(buffer, offset, count);
                destination.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                hash.AppendData(buffer);
                destination.Write(buffer);
            }

            public override void WriteByte(byte value)
            {
                hash.AppendData(new[] { value });
                destination.WriteByte(value);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) destination.Flush();
                base.Dispose(disposing);
            }
        }

        private sealed class PartitionReader : IDisposable
        {
            private const int HeaderLength = 9;
            private readonly FileStream data;
            private readonly Dictionary<Guid, byte[]> cache = new Dictionary<Guid, byte[]>();
            private readonly LinkedList<Guid> lru = new LinkedList<Guid>();
            private readonly Dictionary<Guid, LinkedListNode<Guid>> lruNodes = new Dictionary<Guid, LinkedListNode<Guid>>();
            internal Dictionary<Guid, IndexEntry> Index { get; } = new Dictionary<Guid, IndexEntry>();

            internal PartitionReader(string dataPath, string indexPath, DiskPartition partition, int formatVersion)
            {
                data = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
                try
                {
                    using BinaryReader dataReader = new BinaryReader(data, System.Text.Encoding.UTF8, true);
                    if (dataReader.ReadInt32() != DataMagic || dataReader.ReadInt32() != formatVersion || dataReader.ReadByte() != (byte)partition)
                    {
                        throw new InvalidDataException($"Invalid data header for {partition} partition.");
                    }
                    using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using BinaryReader indexReader = new BinaryReader(indexStream);
                    if (indexReader.ReadInt32() != IndexMagic || indexReader.ReadInt32() != formatVersion)
                    {
                        throw new InvalidDataException($"Invalid index header for {partition} partition.");
                    }
                    int count = indexReader.ReadInt32();
                    if (count < 0 || count > MaximumRecordsPerPartition || count > (indexStream.Length - 12) / 28 ||
                        indexStream.Length != 12L + (long)count * 28)
                    {
                        throw new InvalidDataException($"Invalid index length for {partition} partition.");
                    }
                    for (int i = 0; i < count; i++)
                    {
                        byte[] guidBytes = indexReader.ReadBytes(16);
                        if (guidBytes.Length != 16) throw new EndOfStreamException();
                        Guid key = new Guid(guidBytes);
                        long offset = indexReader.ReadInt64();
                        int payloadLength = indexReader.ReadInt32();
                        if (key == Guid.Empty || offset < HeaderLength || payloadLength < 0 || payloadLength > MaximumRecordPayloadLength ||
                            offset > data.Length - payloadLength - 24 || !Index.TryAdd(key, new IndexEntry(offset, payloadLength)))
                        {
                            throw new InvalidDataException($"Invalid index entry in {partition} partition.");
                        }
                    }
                }
                catch
                {
                    data.Dispose();
                    throw;
                }
            }

            internal byte[] Read(Guid key, IndexEntry entry, int cacheLimit)
            {
                if (cache.TryGetValue(key, out byte[]? cached))
                {
                    Touch(key);
                    return cached;
                }
                lock (data)
                {
                    data.Position = entry.Offset;
                    using BinaryReader reader = new BinaryReader(data, System.Text.Encoding.UTF8, true);
                    byte[] recordKeyBytes = reader.ReadBytes(16);
                    if (recordKeyBytes.Length != 16 || new Guid(recordKeyBytes) != key)
                    {
                        throw new InvalidDataException("Record key does not match its offset index.");
                    }
                    int length = reader.ReadInt32();
                    if (length != entry.PayloadLength || length < 0 || length > MaximumRecordPayloadLength)
                        throw new InvalidDataException("Record length does not match its offset index or exceeds the payload limit.");
                    byte[] payload = reader.ReadBytes(length);
                    if (payload.Length != length || reader.ReadUInt32() != Crc32(payload))
                    {
                        throw new InvalidDataException("Record payload is truncated or has an invalid CRC32 checksum.");
                    }
                    if (cacheLimit > 0)
                    {
                        cache.Add(key, payload);
                        LinkedListNode<Guid> node = lru.AddLast(key);
                        lruNodes.Add(key, node);
                        while (cache.Count > cacheLimit)
                        {
                            Guid oldest = lru.First!.Value;
                            lru.RemoveFirst();
                            lruNodes.Remove(oldest);
                            cache.Remove(oldest);
                        }
                    }
                    return payload;
                }
            }

            private void Touch(Guid key)
            {
                if (lruNodes.TryGetValue(key, out LinkedListNode<Guid>? node))
                {
                    lru.Remove(node);
                    lru.AddLast(node);
                }
            }

            public void Dispose()
            {
                data.Dispose();
                cache.Clear();
                lru.Clear();
                lruNodes.Clear();
            }
        }
    }
}
