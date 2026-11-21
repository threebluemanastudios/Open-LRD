using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OPEN_LRD.Tools
{
    internal class Tool_1
    {
        /// <summary>
        /// Reads a UTF-8 text file, or maps non-text bytes losslessly through
        /// Latin-1, into a rectangular character grid. The first dimension is
        /// the row and the second is the column. Binary fallback rows default
        /// to the ceiling of the square root of the file's byte length.
        /// </summary>
        public static char[,] ParseFileToCharGrid(string filePath, int? maxColumns = null)
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
                return new char[0, 0];
            }

            bool isText = TryDecodeText(bytes, out string text);
            char[] characters = isText ? text.ToCharArray() : Encoding.Latin1.GetChars(bytes);
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
                return new char[0, 0];
            }

            int columnCount = 0;
            foreach (string row in rows)
            {
                columnCount = Math.Max(columnCount, row.Length);
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
            return grid;
        }

        private static bool TryDecodeText(byte[] bytes, out string text)
        {
            try
            {
                if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
                {
                    text = new UTF32Encoding(true, true, true).GetString(bytes, 4, bytes.Length - 4);
                    return true;
                }
                if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
                {
                    text = new UTF32Encoding(false, true, true).GetString(bytes, 4, bytes.Length - 4);
                    return true;
                }
                if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
                {
                    text = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
                    if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException("Null characters indicate binary input.");
                    return true;
                }
                if (HasPrefix(bytes, 0xFE, 0xFF))
                {
                    text = new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2);
                    return true;
                }
                if (HasPrefix(bytes, 0xFF, 0xFE))
                {
                    text = new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
                    return true;
                }

                text = new UTF8Encoding(false, true).GetString(bytes);
                if (text.IndexOf('\0') >= 0) throw new DecoderFallbackException("Null characters indicate binary input.");
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = string.Empty;
                return false;
            }
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
