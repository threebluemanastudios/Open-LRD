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
        Links = 10
    }

    internal readonly record struct DiskRecord(Guid Key, byte[] Payload);

    internal sealed class PartitionedBinaryStore : IDisposable
    {
        private const int FormatVersion = 1;
        private const int DataMagic = 0x44524C4F; // OLRD, little-endian
        private const int IndexMagic = 0x49524C4F;
        private const int ManifestMagic = 0x4D524C4F;
        private static readonly DiskPartition[] AllPartitions = Enum.GetValues<DiskPartition>();

        private readonly string directoryPath;
        private readonly int maxCachedRecords;
        private readonly Dictionary<DiskPartition, PartitionReader> readers = new Dictionary<DiskPartition, PartitionReader>();
        private bool disposed;

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
            if (maxCachedRecordsPerPartition < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCachedRecordsPerPartition));
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
            Dictionary<DiskPartition, PartitionManifestEntry> manifest = ReadManifest(directoryPath);
            foreach (DiskPartition partition in AllPartitions)
            {
                if (!manifest.TryGetValue(partition, out PartitionManifestEntry item))
                {
                    throw new InvalidDataException($"Manifest is missing partition {partition}.");
                }
                string dataPath = DataPath(directoryPath, partition);
                string indexPath = IndexPath(directoryPath, partition);
                ValidateHash(dataPath, item.DataHash);
                ValidateHash(indexPath, item.IndexHash);
                PartitionReader reader = new PartitionReader(dataPath, indexPath, partition);
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
            HashSet<Guid> keys = new HashSet<Guid>();
            using (FileStream data = new FileStream(dataPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan))
            using (BinaryWriter writer = new BinaryWriter(data))
            {
                writer.Write(DataMagic);
                writer.Write(FormatVersion);
                writer.Write((byte)partition);
                using FileStream index = new FileStream(indexPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan);
                using BinaryWriter indexWriter = new BinaryWriter(index);
                indexWriter.Write(IndexMagic);
                indexWriter.Write(FormatVersion);
                indexWriter.Write(0);
                foreach (DiskRecord record in records)
                {
                    if (record.Key == Guid.Empty || !keys.Add(record.Key))
                    {
                        throw new InvalidDataException($"Partition {partition} contains an empty or duplicate key.");
                    }
                    if (record.Payload == null || record.Payload.Length > int.MaxValue - 28)
                    {
                        throw new InvalidDataException($"Partition {partition} contains an invalid payload.");
                    }
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
                data.Flush(true);
                indexWriter.Flush();
                index.Position = 8;
                indexWriter.Write(count);
                indexWriter.Flush();
                index.Flush(true);
            }
            return new PartitionManifestEntry(count, ComputeHash(dataPath), ComputeHash(indexPath));
        }

        private static void WriteManifest(string directory, IReadOnlyDictionary<DiskPartition, PartitionManifestEntry> entries)
        {
            string path = Path.Combine(directory, "manifest.lrdmeta");
            using FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(ManifestMagic);
            writer.Write(FormatVersion);
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

        private static Dictionary<DiskPartition, PartitionManifestEntry> ReadManifest(string directory)
        {
            string path = Path.Combine(directory, "manifest.lrdmeta");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using BinaryReader reader = new BinaryReader(stream);
            if (reader.ReadInt32() != ManifestMagic || reader.ReadInt32() != FormatVersion)
            {
                throw new InvalidDataException("Unsupported or invalid disk-store manifest.");
            }
            _ = reader.ReadInt64();
            int count = reader.ReadInt32();
            if (count != AllPartitions.Length) throw new InvalidDataException("Manifest partition count is invalid.");
            Dictionary<DiskPartition, PartitionManifestEntry> result = new Dictionary<DiskPartition, PartitionManifestEntry>();
            for (int i = 0; i < count; i++)
            {
                DiskPartition partition = (DiskPartition)reader.ReadByte();
                int recordCount = reader.ReadInt32();
                byte[] dataHash = reader.ReadBytes(32);
                byte[] indexHash = reader.ReadBytes(32);
                if (!Enum.IsDefined(partition) || recordCount < 0 || dataHash.Length != 32 || indexHash.Length != 32 ||
                    !result.TryAdd(partition, new PartitionManifestEntry(recordCount, dataHash, indexHash)))
                {
                    throw new InvalidDataException("Manifest contains an invalid partition entry.");
                }
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Manifest has trailing data.");
            return result;
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

        private sealed class PartitionReader : IDisposable
        {
            private const int HeaderLength = 9;
            private readonly FileStream data;
            private readonly Dictionary<Guid, byte[]> cache = new Dictionary<Guid, byte[]>();
            private readonly LinkedList<Guid> lru = new LinkedList<Guid>();
            private readonly Dictionary<Guid, LinkedListNode<Guid>> lruNodes = new Dictionary<Guid, LinkedListNode<Guid>>();
            internal Dictionary<Guid, IndexEntry> Index { get; } = new Dictionary<Guid, IndexEntry>();

            internal PartitionReader(string dataPath, string indexPath, DiskPartition partition)
            {
                data = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
                try
                {
                    using BinaryReader dataReader = new BinaryReader(data, System.Text.Encoding.UTF8, true);
                    if (dataReader.ReadInt32() != DataMagic || dataReader.ReadInt32() != FormatVersion || dataReader.ReadByte() != (byte)partition)
                    {
                        throw new InvalidDataException($"Invalid data header for {partition} partition.");
                    }
                    using FileStream indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using BinaryReader indexReader = new BinaryReader(indexStream);
                    if (indexReader.ReadInt32() != IndexMagic || indexReader.ReadInt32() != FormatVersion)
                    {
                        throw new InvalidDataException($"Invalid index header for {partition} partition.");
                    }
                    int count = indexReader.ReadInt32();
                    if (count < 0 || count > (indexStream.Length - 12) / 28 || indexStream.Length != 12L + (long)count * 28)
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
                        if (key == Guid.Empty || offset < HeaderLength || payloadLength < 0 ||
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
                    if (length != entry.PayloadLength) throw new InvalidDataException("Record length does not match its offset index.");
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
