using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace OPEN_LRD.Tools
{
    internal enum FileGridEncoding
    {
        Utf8,
        Utf16LittleEndian,
        Utf16BigEndian,
        Utf32LittleEndian,
        Utf32BigEndian,
        Latin1
    }

    /// <summary>
    /// A parsed character grid and the metadata required to write its logical
    /// characters back using the same encoding and row layout.
    /// </summary>
    internal sealed class FileGridDocument
    {
        private readonly int[] rowLengths;

        internal FileGridDocument(char[,] characters, FileGridEncoding encoding, bool hasPreamble, int[] rowLengths)
        {
            Characters = characters ?? throw new ArgumentNullException(nameof(characters));
            Encoding = encoding;
            HasPreamble = hasPreamble;
            this.rowLengths = rowLengths == null ? throw new ArgumentNullException(nameof(rowLengths)) : (int[])rowLengths.Clone();
            if (this.rowLengths.Length != characters.GetLength(0))
            {
                throw new ArgumentException("There must be one logical row length for every grid row.", nameof(rowLengths));
            }
            for (int row = 0; row < this.rowLengths.Length; row++)
            {
                if (this.rowLengths[row] < 0 || this.rowLengths[row] > characters.GetLength(1))
                {
                    throw new ArgumentOutOfRangeException(nameof(rowLengths), "Logical row lengths must fit within the character grid.");
                }
            }
        }

        internal char[,] Characters { get; }
        internal FileGridEncoding Encoding { get; }
        internal bool HasPreamble { get; }
        internal IReadOnlyList<int> RowLengths => Array.AsReadOnly(rowLengths);

        internal int GetRowLength(int row) => rowLengths[row];
    }

    internal class Tool_1
    {
        /// <summary>
        /// Reads a file into a rectangular character grid, discarding encoding
        /// and logical-row metadata. Use ReadFileToCharGridDocument when the
        /// grid must later be written back without losing source bytes.
        /// </summary>
        public static char[,] ParseFileToCharGrid(string filePath, int? maxColumns = null)
        {
            return ReadFileToCharGridDocument(filePath, maxColumns).Characters;
        }

        /// <summary>
        /// Reads a file into a character grid while retaining its detected
        /// encoding, preamble, and logical row lengths for lossless writing.
        /// An explicit encoding can be supplied when automatic detection is
        /// ambiguous, particularly for Latin-1 bytes that are also valid UTF-8.
        /// The first grid dimension is the row and the second is the column.
        /// </summary>
        public static FileGridDocument ReadFileToCharGridDocument(
            string filePath,
            int? maxColumns = null,
            FileGridEncoding? encodingOverride = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            if (maxColumns.HasValue && maxColumns.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxColumns), "Maximum columns must be positive when specified.");
            }

            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("The input file could not be found.", fullPath);
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length == 0)
            {
                FileGridEncoding emptyEncoding = encodingOverride ?? FileGridEncoding.Utf8;
                return new FileGridDocument(new char[0, 0], emptyEncoding, false, Array.Empty<int>());
            }

            bool isText;
            string text;
            FileGridEncoding fileEncoding;
            bool hasPreamble;
            if (encodingOverride.HasValue)
            {
                fileEncoding = encodingOverride.Value;
                hasPreamble = HasEncodingPreamble(bytes, fileEncoding);
                text = DecodeWithEncoding(bytes, fileEncoding, hasPreamble);
                isText = fileEncoding != FileGridEncoding.Latin1;
            }
            else
            {
                isText = TryDecodeText(bytes, out text, out fileEncoding, out hasPreamble);
                if (!isText)
                {
                    fileEncoding = FileGridEncoding.Latin1;
                    hasPreamble = false;
                    text = Encoding.Latin1.GetString(bytes);
                }
            }

            char[] characters = text.ToCharArray();
            int effectiveMaxColumns = maxColumns ??
                (isText ? 0 : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(bytes.Length))));

            List<string> rows = new List<string>();
            StringBuilder currentRow = new StringBuilder();
            for (int index = 0; index < characters.Length; index++)
            {
                char value = characters[index];
                if (value == '\r')
                {
                    AppendWrapped(currentRow, value, effectiveMaxColumns, rows);
                    if (index + 1 < characters.Length && characters[index + 1] == '\n')
                    {
                        AppendWrapped(currentRow, characters[++index], effectiveMaxColumns, rows);
                    }
                    FlushRow(currentRow, rows);
                }
                else if (value == '\n')
                {
                    AppendWrapped(currentRow, value, effectiveMaxColumns, rows);
                    FlushRow(currentRow, rows);
                }
                else
                {
                    AppendWrapped(currentRow, value, effectiveMaxColumns, rows);
                }
            }

            FlushRow(currentRow, rows);
            if (rows.Count == 0)
            {
                return new FileGridDocument(new char[0, 0], fileEncoding, hasPreamble, Array.Empty<int>());
            }

            int columnCount = 0;
            int[] rowLengths = new int[rows.Count];
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                string row = rows[rowIndex];
                columnCount = Math.Max(columnCount, row.Length);
                rowLengths[rowIndex] = row.Length;
            }

            char[,] grid = new char[rows.Count, columnCount];
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                string row = rows[rowIndex];
                for (int columnIndex = 0; columnIndex < row.Length; columnIndex++)
                {
                    grid[rowIndex, columnIndex] = row[columnIndex];
                }
            }
            return new FileGridDocument(grid, fileEncoding, hasPreamble, rowLengths);
        }

        /// <summary>
        /// Writes all logical grid characters in row-major order using the
        /// metadata captured by ReadFileToCharGridDocument. Padding cells are
        /// omitted; embedded nulls and line-ending characters are preserved.
        /// </summary>
        public static void WriteFileGridDocument(FileGridDocument document, string filePath)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            string fullPath = Path.GetFullPath(filePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
            string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                byte[] bytes = EncodeDocument(document);
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Writes a caller-provided grid. Every cell is treated as content;
        /// callers that need padding omitted should write a parsed document.
        /// Latin-1 is byte-mapped, while Unicode formats use strict encoding.
        /// </summary>
        public static void WriteCharGridToFile(
            char[,] characters,
            string filePath,
            FileGridEncoding encoding,
            bool writePreamble = true)
        {
            ArgumentNullException.ThrowIfNull(characters);
            int rowCount = characters.GetLength(0);
            int[] rowLengths = new int[rowCount];
            Array.Fill(rowLengths, characters.GetLength(1));
            FileGridDocument document = new FileGridDocument(characters, encoding, writePreamble, rowLengths);
            WriteFileGridDocument(document, filePath);
        }

        private static byte[] EncodeDocument(FileGridDocument document)
        {
            StringBuilder text = new StringBuilder();
            char[,] characters = document.Characters;
            for (int row = 0; row < characters.GetLength(0); row++)
            {
                int rowLength = document.GetRowLength(row);
                for (int column = 0; column < rowLength; column++)
                {
                    text.Append(characters[row, column]);
                }
            }

            if (document.Encoding == FileGridEncoding.Latin1)
            {
                byte[] bytes = new byte[text.Length];
                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    if (value > 0x00FF)
                    {
                        throw new EncoderFallbackException($"Character U+{(int)value:X4} cannot be represented in Latin-1.");
                    }
                    bytes[index] = (byte)value;
                }
                return bytes;
            }

            Encoding encoding = CreateStrictEncoding(document.Encoding, document.HasPreamble);
            byte[] preamble = document.HasPreamble ? encoding.GetPreamble() : Array.Empty<byte>();
            byte[] content = encoding.GetBytes(text.ToString());
            byte[] result = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
            return result;
        }

        private static bool TryDecodeText(
            byte[] bytes,
            out string text,
            out FileGridEncoding fileEncoding,
            out bool hasPreamble)
        {
            try
            {
                if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
                {
                    text = new UTF32Encoding(true, true, true).GetString(bytes, 4, bytes.Length - 4);
                    fileEncoding = FileGridEncoding.Utf32BigEndian;
                    hasPreamble = true;
                    return true;
                }
                if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
                {
                    text = new UTF32Encoding(false, true, true).GetString(bytes, 4, bytes.Length - 4);
                    fileEncoding = FileGridEncoding.Utf32LittleEndian;
                    hasPreamble = true;
                    return true;
                }
                if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
                {
                    text = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
                    if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException("Null characters indicate binary input.");
                    fileEncoding = FileGridEncoding.Utf8;
                    hasPreamble = true;
                    return true;
                }
                if (HasPrefix(bytes, 0xFE, 0xFF))
                {
                    text = new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2);
                    fileEncoding = FileGridEncoding.Utf16BigEndian;
                    hasPreamble = true;
                    return true;
                }
                if (HasPrefix(bytes, 0xFF, 0xFE))
                {
                    text = new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
                    fileEncoding = FileGridEncoding.Utf16LittleEndian;
                    hasPreamble = true;
                    return true;
                }

                text = new UTF8Encoding(false, true).GetString(bytes);
                if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException("Null characters indicate binary input.");
                fileEncoding = FileGridEncoding.Utf8;
                hasPreamble = false;
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = string.Empty;
                fileEncoding = FileGridEncoding.Latin1;
                hasPreamble = false;
                return false;
            }
        }

        private static string DecodeWithEncoding(byte[] bytes, FileGridEncoding encoding, bool hasPreamble)
        {
            if (encoding == FileGridEncoding.Latin1) return Encoding.Latin1.GetString(bytes);
            Encoding strictEncoding = CreateStrictEncoding(encoding, hasPreamble);
            int preambleLength = hasPreamble ? strictEncoding.GetPreamble().Length : 0;
            if (preambleLength > bytes.Length)
            {
                throw new DecoderFallbackException("The file is shorter than its encoding preamble.");
            }
            return strictEncoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        }

        private static bool HasEncodingPreamble(byte[] bytes, FileGridEncoding encoding)
        {
            return encoding switch
            {
                FileGridEncoding.Utf8 => HasPrefix(bytes, 0xEF, 0xBB, 0xBF),
                FileGridEncoding.Utf16LittleEndian => HasPrefix(bytes, 0xFF, 0xFE),
                FileGridEncoding.Utf16BigEndian => HasPrefix(bytes, 0xFE, 0xFF),
                FileGridEncoding.Utf32LittleEndian => HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00),
                FileGridEncoding.Utf32BigEndian => HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF),
                FileGridEncoding.Latin1 => false,
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), "Unsupported file-grid encoding.")
            };
        }

        private static Encoding CreateStrictEncoding(FileGridEncoding encoding, bool includePreamble)
        {
            return encoding switch
            {
                FileGridEncoding.Utf8 => new UTF8Encoding(includePreamble, true),
                FileGridEncoding.Utf16LittleEndian => new UnicodeEncoding(false, includePreamble, true),
                FileGridEncoding.Utf16BigEndian => new UnicodeEncoding(true, includePreamble, true),
                FileGridEncoding.Utf32LittleEndian => new UTF32Encoding(false, includePreamble, true),
                FileGridEncoding.Utf32BigEndian => new UTF32Encoding(true, includePreamble, true),
                FileGridEncoding.Latin1 => throw new ArgumentException("Latin-1 is encoded by direct byte mapping.", nameof(encoding)),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), "Unsupported file-grid encoding.")
            };
        }

        private static bool HasPrefix(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (int index = 0; index < prefix.Length; index++)
            {
                if (bytes[index] != prefix[index]) return false;
            }
            return true;
        }

        private static void AppendWrapped(StringBuilder row, char value, int maxColumns, List<string> rows)
        {
            if (maxColumns > 0 && row.Length == maxColumns)
            {
                FlushRow(row, rows);
            }
            row.Append(value);
            if (maxColumns > 0 && row.Length == maxColumns)
            {
                FlushRow(row, rows);
            }
        }

        private static void FlushRow(StringBuilder row, List<string> rows)
        {
            if (row.Length == 0) return;
            rows.Add(row.ToString());
            row.Clear();
        }
    }
}
