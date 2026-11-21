using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Persistence
{
    internal static class FoundationBinarySerializer
    {
        private static readonly Type[] SupportedTypes =
        {
            typeof(object), typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(short),
            typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float),
            typeof(double), typeof(decimal), typeof(char), typeof(Guid), typeof(DateTime),
            typeof(DateTimeOffset), typeof(TimeSpan)
        };

        internal static byte[] SerializeSymbol(SymbolBox box) => Write(writer =>
        {
            writer.Write(box.GetSymbolID().ToByteArray());
            writer.Write(box.GetSymbolChar());
            WriteNullableGuid(writer, box.GetSenseID());
        });

        internal static SymbolBox DeserializeSymbol(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            char value = reader.ReadChar();
            Guid? senseId = ReadNullableGuid(reader);
            SymbolBox box = new SymbolBox(id, value);
            if (senseId.HasValue) box.SetSenseID(senseId.Value);
            box.FreezeIdentity();
            return box;
        });

        internal static byte[] SerializeWord(WordBox box) => Write(writer =>
        {
            writer.Write(box.GetWordID().ToByteArray());
            WriteNullableGuid(writer, box.GetSenseID());
            WriteNullableGuid(writer, box.GetUsageID());
            WriteGuidList(writer, box.GetSymbols());
            writer.Write(box.GetPartOfSpeech().HasValue);
            if (box.GetPartOfSpeech().HasValue) writer.Write((int)box.GetPartOfSpeech()!.Value);
        });

        internal static WordBox DeserializeWord(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            Guid? sense = ReadNullableGuid(reader);
            Guid? usage = ReadNullableGuid(reader);
            List<Guid>? symbols = ReadGuidList(reader);
            bool hasPartOfSpeech = reader.ReadBoolean();
            PartOfSpeech? partOfSpeech = hasPartOfSpeech ? (PartOfSpeech)reader.ReadInt32() : null;
            if (partOfSpeech.HasValue && !Enum.IsDefined(partOfSpeech.Value))
                throw new InvalidDataException("Stored part-of-speech value is invalid.");
            WordBox box;
            if (sense.HasValue && usage.HasValue && symbols != null && partOfSpeech.HasValue)
            {
                box = new WordBox(id, sense.Value, usage.Value, symbols, partOfSpeech.Value);
            }
            else
            {
                box = new WordBox(id, symbols ?? new List<Guid>());
                if (sense.HasValue) box.SetSenseID(sense.Value);
                if (usage.HasValue) box.SetUsageID(usage.Value);
                if (symbols == null) box.SetSymbols(new List<Guid>());
                if (partOfSpeech.HasValue) box.SetPartOfSpeech(partOfSpeech.Value);
            }
            box.FreezeIdentity();
            return box;
        });

        internal static byte[] SerializeSentence(SentenceBox box) => Write(writer =>
        {
            writer.Write(box.GetSentenceID().ToByteArray());
            WriteNullableGuid(writer, box.GetSenseID());
            WriteGuidList(writer, box.GetWords());
        });

        internal static SentenceBox DeserializeSentence(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            Guid? sense = ReadNullableGuid(reader);
            List<Guid>? words = ReadGuidList(reader);
            SentenceBox box = sense.HasValue && words != null
                ? new SentenceBox(id, sense.Value, words)
                : new SentenceBox(id);
            if (sense.HasValue && words == null) box.SetSenseID(sense.Value);
            if (words != null && !sense.HasValue) box.SetWords(words);
            box.FreezeIdentity();
            return box;
        });

        internal static byte[] SerializeSense(SenseBox box) => Write(writer =>
        {
            writer.Write(box.GetSenseID().ToByteArray());
            WriteGuidList(writer, box.GetSentences());
            WriteGuidList(writer, box.GetLeadingWords());
            WriteGuidList(writer, box.GetTrailingWords());
            writer.Write(box.GetAverageWordPosition());
            writer.Write(box.GetAverageSentencePosition());
            writer.Write(box.GetAverageSpread());
        });

        internal static SenseBox DeserializeSense(byte[] payload) => Read(payload, reader =>
        {
            SenseBox box = new SenseBox(ReadGuid(reader));
            foreach (Guid id in ReadGuidList(reader) ?? new List<Guid>()) box.AddSentence(id);
            foreach (Guid id in ReadGuidList(reader) ?? new List<Guid>()) box.AddLeadingWord(id);
            foreach (Guid id in ReadGuidList(reader) ?? new List<Guid>()) box.AddTrailingWord(id);
            box.UpdateAverageMetrics(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            return box;
        });

        internal static byte[] SerializeUsage(UsageBox box) => Write(writer =>
        {
            writer.Write(box.GetUsageID().ToByteArray());
            WriteGuidList(writer, box.GetSentences());
        });

        internal static UsageBox DeserializeUsage(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            return new UsageBox(id, ReadGuidList(reader) ?? new List<Guid>());
        });

        internal static byte[] SerializeGroup(GroupBox box) => Write(writer =>
        {
            writer.Write(box.GetGroupingID().ToByteArray());
            WriteNullableString(writer, box.GetGroupingName());
            WriteGuidList(writer, box.GetSentenceIDs());
        });

        internal static GroupBox DeserializeGroup(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            string? name = ReadNullableString(reader);
            return new GroupBox(id, name, ReadGuidList(reader) ?? new List<Guid>());
        });

        internal static byte[] SerializeSource(SourceBox box) => Write(writer =>
        {
            writer.Write(box.GetSourceID().ToByteArray());
            WriteNullableString(writer, box.GetSourceName());
            WriteGuidList(writer, box.GetGroupingIDs());
        });

        internal static SourceBox DeserializeSource(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            string? name = ReadNullableString(reader);
            return new SourceBox(id, name, ReadGuidList(reader) ?? new List<Guid>());
        });

        internal static byte[] SerializeFunction(FunctionBox box) => Write(writer =>
        {
            writer.Write(box.GetFunctionID().ToByteArray());
            WriteType(writer, box.ExpectedResultType);
            WriteNullableString(writer, box.FunctionKey);
            WriteNullableString(writer, box.ExpectedResultUnitTitle);
            IReadOnlyList<FunctionVariable> variables = box.Variables;
            writer.Write(variables.Count);
            foreach (FunctionVariable variable in variables)
            {
                writer.Write(variable.Name);
                WriteScalar(writer, variable.Value);
                WriteType(writer, variable.DataType);
                WriteNullableString(writer, variable.UnitTitle);
            }
            FunctionResult? result = box.Result;
            writer.Write(result != null);
            if (result != null)
            {
                WriteScalar(writer, result.Value);
                WriteType(writer, result.DataType);
                WriteNullableString(writer, result.UnitTitle);
            }
        });

        internal static FunctionBox DeserializeFunction(byte[] payload) => Read(payload, reader =>
        {
            Guid id = ReadGuid(reader);
            FunctionBox box = new FunctionBox(id, ReadType(reader), ReadNullableString(reader), ReadNullableString(reader));
            int variableCount = ReadCount(reader);
            for (int i = 0; i < variableCount; i++)
            {
                string name = reader.ReadString();
                object? value = ReadScalar(reader);
                Type dataType = ReadType(reader);
                string? unit = ReadNullableString(reader);
                box.AddVariable(new FunctionVariable(name, value, dataType, unit));
            }
            if (reader.ReadBoolean())
            {
                object? value = ReadScalar(reader);
                Type dataType = ReadType(reader);
                string? unit = ReadNullableString(reader);
                box.StoreResult(new FunctionResult(value, dataType, unit));
            }
            return box;
        });

        internal static byte[] SerializeNode(Node node) => Write(writer =>
        {
            writer.Write(node.GetNodeID().ToByteArray());
            writer.Write((int)node.GetTargetKind());
            writer.Write(node.GetTargetID().ToByteArray());
            WriteNullableString(writer, node.GetLabel());
            writer.Write(node.GetVerifiedReferences());
            writer.Write(node.GetInvalidReferences());
            writer.Write(node.IsTrusted);
        });

        internal static Node DeserializeNode(byte[] payload, bool readOnly) => Read(payload, reader =>
        {
            Guid nodeId = ReadGuid(reader);
            NodeTargetKind kind = (NodeTargetKind)reader.ReadInt32();
            Guid targetId = ReadGuid(reader);
            string? label = ReadNullableString(reader);
            int verified = reader.ReadInt32();
            int invalid = reader.ReadInt32();
            bool trusted = reader.ReadBoolean();
            if (verified < 0 || invalid < 0) throw new InvalidDataException("Node reference counts cannot be negative.");
            Node node = new Node(nodeId, kind, targetId, label);
            if (verified > 0) node.RegisterVerifiedReference(verified);
            if (invalid > 0) node.RegisterInvalidReference(invalid);
            if (trusted) node.MarkTrusted();
            if (readOnly) node.MarkReadOnly();
            return node;
        });

        internal static byte[] SerializeLink(NodeLink link) => Write(writer =>
        {
            writer.Write(link.SourceNodeId.ToByteArray());
            writer.Write(link.TargetNodeId.ToByteArray());
            writer.Write((int)link.RelationType);
            writer.Write(link.Weight);
            WriteNullableString(writer, link.Label);
        });

        internal static NodeLink DeserializeLink(byte[] payload) => Read(payload, reader =>
            new NodeLink(ReadGuid(reader), ReadGuid(reader), (NodeRelationType)reader.ReadInt32(),
                reader.ReadDouble(), ReadNullableString(reader)));

        private static byte[] Write(Action<BinaryWriter> write)
        {
            using MemoryStream stream = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true)) write(writer);
            return stream.ToArray();
        }

        private static T Read<T>(byte[] payload, Func<BinaryReader, T> read)
        {
            using MemoryStream stream = new MemoryStream(payload, false);
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            T result = read(reader);
            if (stream.Position != stream.Length) throw new InvalidDataException("Serialized record contains trailing bytes.");
            return result;
        }

        private static Guid ReadGuid(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(16);
            if (bytes.Length != 16) throw new EndOfStreamException();
            Guid id = new Guid(bytes);
            if (id == Guid.Empty) throw new InvalidDataException("Stored identifiers cannot be Guid.Empty.");
            return id;
        }

        private static void WriteNullableGuid(BinaryWriter writer, Guid? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue) writer.Write(value.Value.ToByteArray());
        }

        private static Guid? ReadNullableGuid(BinaryReader reader) => reader.ReadBoolean() ? ReadGuid(reader) : null;

        private static void WriteGuidList(BinaryWriter writer, IReadOnlyCollection<Guid>? values)
        {
            writer.Write(values?.Count ?? -1);
            if (values == null) return;
            foreach (Guid id in values)
            {
                if (id == Guid.Empty) throw new InvalidDataException("Stored identifier sequences cannot contain Guid.Empty.");
                writer.Write(id.ToByteArray());
            }
        }

        private static List<Guid>? ReadGuidList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count == -1) return null;
            if (count < 0 || count > 100_000_000 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 16)
                throw new InvalidDataException("Stored sequence length is invalid.");
            List<Guid> result = new List<Guid>(count);
            for (int i = 0; i < count; i++) result.Add(ReadGuid(reader));
            return result;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 1_000_000) throw new InvalidDataException("Stored item count is invalid.");
            return count;
        }

        private static void WriteNullableString(BinaryWriter writer, string? value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        private static string? ReadNullableString(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;

        private static void WriteType(BinaryWriter writer, Type type)
        {
            int index = Array.IndexOf(SupportedTypes, type);
            if (index < 0) throw new NotSupportedException($"Function type '{type.FullName}' is not supported by the binary disk store.");
            writer.Write((byte)index);
        }

        private static Type ReadType(BinaryReader reader)
        {
            int index = reader.ReadByte();
            if (index >= SupportedTypes.Length) throw new InvalidDataException("Stored runtime type is unsupported.");
            return SupportedTypes[index];
        }

        private static void WriteScalar(BinaryWriter writer, object? value)
        {
            switch (value)
            {
                case null: writer.Write((byte)0); break;
                case string item: writer.Write((byte)1); writer.Write(item); break;
                case bool item: writer.Write((byte)2); writer.Write(item); break;
                case byte item: writer.Write((byte)3); writer.Write(item); break;
                case sbyte item: writer.Write((byte)4); writer.Write(item); break;
                case short item: writer.Write((byte)5); writer.Write(item); break;
                case ushort item: writer.Write((byte)6); writer.Write(item); break;
                case int item: writer.Write((byte)7); writer.Write(item); break;
                case uint item: writer.Write((byte)8); writer.Write(item); break;
                case long item: writer.Write((byte)9); writer.Write(item); break;
                case ulong item: writer.Write((byte)10); writer.Write(item); break;
                case float item: writer.Write((byte)11); writer.Write(item); break;
                case double item: writer.Write((byte)12); writer.Write(item); break;
                case decimal item: writer.Write((byte)13); writer.Write(item); break;
                case char item: writer.Write((byte)14); writer.Write(item); break;
                case Guid item: writer.Write((byte)15); writer.Write(item.ToByteArray()); break;
                case DateTime item: writer.Write((byte)16); writer.Write(item.Ticks); writer.Write((byte)item.Kind); break;
                case DateTimeOffset item: writer.Write((byte)17); writer.Write(item.Ticks); writer.Write((short)item.Offset.TotalMinutes); break;
                case TimeSpan item: writer.Write((byte)18); writer.Write(item.Ticks); break;
                default: throw new NotSupportedException($"Value type '{value.GetType().FullName}' is not supported by the binary disk store.");
            }
        }

        private static object? ReadScalar(BinaryReader reader)
        {
            return reader.ReadByte() switch
            {
                0 => null,
                1 => reader.ReadString(),
                2 => reader.ReadBoolean(),
                3 => reader.ReadByte(),
                4 => reader.ReadSByte(),
                5 => reader.ReadInt16(),
                6 => reader.ReadUInt16(),
                7 => reader.ReadInt32(),
                8 => reader.ReadUInt32(),
                9 => reader.ReadInt64(),
                10 => reader.ReadUInt64(),
                11 => reader.ReadSingle(),
                12 => reader.ReadDouble(),
                13 => reader.ReadDecimal(),
                14 => reader.ReadChar(),
                15 => ReadGuid(reader),
                16 => new DateTime(reader.ReadInt64(), (DateTimeKind)reader.ReadByte()),
                17 => new DateTimeOffset(reader.ReadInt64(), TimeSpan.FromMinutes(reader.ReadInt16())),
                18 => TimeSpan.FromTicks(reader.ReadInt64()),
                _ => throw new InvalidDataException("Stored scalar tag is invalid.")
            };
        }
    }
}
