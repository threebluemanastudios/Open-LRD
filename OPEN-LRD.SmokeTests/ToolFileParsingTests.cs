using System.IO;
using System.Text;
using OPEN_LRD.Tools;

namespace OPEN_LRD.SmokeTests
{
    internal static class ToolFileParsingTests
    {
        internal static void RunAll()
        {
            Program.Run("Text files parse into row-column character grids", ParsesRowsAndPreservesLineEndings());
            Program.Run("Binary fallback preserves bytes and wraps at square-root width", PreservesBinaryBytesLosslessly());
            Program.Run("Empty and invalid file inputs are handled", HandlesEmptyAndInvalidInputs());
            Program.Run("Grid documents round-trip detected text encodings byte-for-byte", RoundTripsTextEncodings());
            Program.Run("Grid documents preserve ambiguous Latin-1 bytes and logical row lengths", RoundTripsLatin1AndPadding());
            Program.Run("Grid writer validates encodings before replacing a destination", WriterRejectsUnrepresentableCharacters());
        }

        private static Action RoundTripsTextEncodings()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                string outputPath = TemporaryFilePath();
                string text = "A\r\nΩ\n𝄞";
                (byte[] Bytes, FileGridEncoding Encoding)[] fixtures =
                {
                    (new UTF8Encoding(false).GetBytes(text), FileGridEncoding.Utf8),
                    (WithPreamble(new UTF8Encoding(true), text), FileGridEncoding.Utf8),
                    (WithPreamble(new UnicodeEncoding(false, true, true), text), FileGridEncoding.Utf16LittleEndian),
                    (WithPreamble(new UnicodeEncoding(true, true, true), text), FileGridEncoding.Utf16BigEndian),
                    (WithPreamble(new UTF32Encoding(false, true, true), text), FileGridEncoding.Utf32LittleEndian),
                    (WithPreamble(new UTF32Encoding(true, true, true), text), FileGridEncoding.Utf32BigEndian)
                };

                try
                {
                    foreach ((byte[] bytes, FileGridEncoding encoding) in fixtures)
                    {
                        File.WriteAllBytes(path, bytes);
                        FileGridDocument document = Tool_1.ReadFileToCharGridDocument(path);
                        Program.Assert(document.Encoding == encoding,
                            $"The reader should preserve detected encoding {encoding}.");
                        Tool_1.WriteFileGridDocument(document, outputPath);
                        Program.Assert(BytesEqual(File.ReadAllBytes(outputPath), bytes),
                            $"The writer should reproduce {encoding} input bytes exactly.");
                    }

                    File.WriteAllBytes(path, Array.Empty<byte>());
                    FileGridDocument empty = Tool_1.ReadFileToCharGridDocument(path);
                    Tool_1.WriteFileGridDocument(empty, outputPath);
                    Program.Assert(File.ReadAllBytes(outputPath).Length == 0,
                        "An empty input file should round-trip as an empty file.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }
            };
        }

        private static Action RoundTripsLatin1AndPadding()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                string outputPath = TemporaryFilePath();
                byte[] bytes = { 0xC3, 0xA9, 0x00, 0x0D, 0x0A, 0xFF, 0x80 };
                try
                {
                    File.WriteAllBytes(path, bytes);
                    FileGridDocument document = Tool_1.ReadFileToCharGridDocument(
                        path, encodingOverride: FileGridEncoding.Latin1);
                    Program.Assert(document.Encoding == FileGridEncoding.Latin1,
                        "An explicit Latin-1 override should be recorded in the document.");
                    Program.Assert(document.Characters[0, 0] == '\u00C3' && document.Characters[0, 1] == '\u00A9',
                        "Bytes that are valid UTF-8 together must remain separate Latin-1 characters when forced.");

                    Tool_1.WriteFileGridDocument(document, outputPath);
                    Program.Assert(BytesEqual(File.ReadAllBytes(outputPath), bytes),
                        "Latin-1 output should preserve all bytes and omit only grid padding.");

                    FileGridDocument detectedBinary = Tool_1.ReadFileToCharGridDocument(path);
                    Program.Assert(detectedBinary.Encoding == FileGridEncoding.Latin1,
                        "Invalid UTF-8 input should retain its Latin-1 byte-mapping format.");
                    Tool_1.WriteFileGridDocument(detectedBinary, outputPath);
                    Program.Assert(BytesEqual(File.ReadAllBytes(outputPath), bytes),
                        "Automatically detected binary input should round-trip byte-for-byte.");

                    File.WriteAllText(path, "ab\nc", new UTF8Encoding(false));
                    FileGridDocument raggedText = Tool_1.ReadFileToCharGridDocument(path);
                    Program.Assert(raggedText.Characters.GetLength(0) == 2 && raggedText.Characters.GetLength(1) == 3 &&
                        raggedText.RowLengths[0] == 3 && raggedText.RowLengths[1] == 1,
                        "The document should retain the logical lengths of ragged rows.");
                    Tool_1.WriteFileGridDocument(raggedText, outputPath);
                    Program.Assert(BytesEqual(File.ReadAllBytes(outputPath), File.ReadAllBytes(path)),
                        "Ragged row padding must not be emitted as file content.");

                    File.WriteAllText(path, "abcdef", new UTF8Encoding(false));
                    FileGridDocument wrappedText = Tool_1.ReadFileToCharGridDocument(path, 2);
                    Tool_1.WriteFileGridDocument(wrappedText, outputPath);
                    Program.Assert(BytesEqual(File.ReadAllBytes(outputPath), File.ReadAllBytes(path)),
                        "Explicit grid wrapping must not add separators or lose characters when writing.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                }
            };
        }

        private static Action WriterRejectsUnrepresentableCharacters()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                byte[] original = { 0x11, 0x22, 0x33 };
                try
                {
                    File.WriteAllBytes(path, original);
                    char[,] grid = { { '\u0100' } };
                    Program.AssertThrows<EncoderFallbackException>(
                        () => Tool_1.WriteCharGridToFile(grid, path, FileGridEncoding.Latin1),
                        "Latin-1 output must reject characters outside its byte range.");
                    Program.Assert(BytesEqual(File.ReadAllBytes(path), original),
                        "A failed encoding must leave the destination file unchanged.");

                    char[,] malformedUtf32 = { { '\uD800' } };
                    Program.AssertThrows<EncoderFallbackException>(
                        () => Tool_1.WriteCharGridToFile(malformedUtf32, path, FileGridEncoding.Utf32LittleEndian),
                        "UTF-32 output must reject an unpaired surrogate.");
                    Program.Assert(BytesEqual(File.ReadAllBytes(path), original),
                        "A second failed encoding must also leave the destination unchanged.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            };
        }

        private static byte[] WithPreamble(Encoding encoding, string text)
        {
            byte[] preamble = encoding.GetPreamble();
            byte[] content = encoding.GetBytes(text);
            byte[] result = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);
            return result;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }

        private static Action PreservesBinaryBytesLosslessly()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                byte[] bytes = { 0x41, 0x80, 0xFE, 0xFF, 0x00, 0x42, 0xC3, 0x28, 0x7F, 0x90 };
                try
                {
                    File.WriteAllBytes(path, bytes);
                    char[,] grid = Tool_1.ParseFileToCharGrid(path);
                    Program.Assert(grid.GetLength(0) == 3 && grid.GetLength(1) == 4,
                        "Ten binary bytes should wrap at ceil(sqrt(10)) = 4 columns.");
                    for (int index = 0; index < bytes.Length; index++)
                    {
                        Program.Assert(grid[index / 4, index % 4] == (char)bytes[index],
                            $"Byte 0x{bytes[index]:X2} should map directly to the same-valued char.");
                    }
                    Program.Assert(grid[2, 2] == '\0' && grid[2, 3] == '\0',
                        "Unused cells in the final binary row should be null-padded.");

                    File.WriteAllText(path, "abcdef", new UTF8Encoding(false));
                    char[,] explicitlyWrapped = Tool_1.ParseFileToCharGrid(path, 2);
                    Program.Assert(explicitlyWrapped.GetLength(0) == 3 && explicitlyWrapped.GetLength(1) == 2 &&
                        explicitlyWrapped[2, 0] == 'e' && explicitlyWrapped[2, 1] == 'f',
                        "Explicit maxColumns should wrap valid text as well.");
                    Program.AssertThrows<ArgumentOutOfRangeException>(() => Tool_1.ParseFileToCharGrid(path, 0),
                        "Non-positive maxColumns should be rejected.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            };
        }

        private static Action ParsesRowsAndPreservesLineEndings()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                try
                {
                    File.WriteAllText(path, "ab\r\nc\nxy", new UTF8Encoding(false));
                    char[,] grid = Tool_1.ParseFileToCharGrid(path);
                    Program.Assert(grid.GetLength(0) == 3 && grid.GetLength(1) == 4,
                        "Array dimensions should be rows by maximum line width, including preserved line-ending characters.");
                    Program.Assert(grid[0, 0] == 'a' && grid[0, 1] == 'b' && grid[0, 2] == '\r' && grid[0, 3] == '\n',
                        "CRLF should be preserved as two distinct characters.");
                    Program.Assert(grid[1, 0] == 'c' && grid[1, 1] == '\n' && grid[1, 2] == '\0' && grid[1, 3] == '\0',
                        "Short rows should preserve newline and pad with null characters.");
                    Program.Assert(grid[2, 0] == 'x' && grid[2, 1] == 'y' && grid[2, 2] == '\0' && grid[2, 3] == '\0',
                        "Final unterminated rows should be copied and null-padded.");

                    File.WriteAllText(path, "a\n", new UTF8Encoding(false));
                    char[,] trailingNewline = Tool_1.ParseFileToCharGrid(path);
                    Program.Assert(trailingNewline.GetLength(0) == 1 && trailingNewline.GetLength(1) == 2 &&
                        trailingNewline[0, 0] == 'a' && trailingNewline[0, 1] == '\n',
                        "A terminal newline should not create a spurious extra row.");

                    File.WriteAllText(path, "A\rB", new UTF8Encoding(false));
                    char[,] loneCarriageReturn = Tool_1.ParseFileToCharGrid(path);
                    Program.Assert(loneCarriageReturn.GetLength(0) == 2 && loneCarriageReturn.GetLength(1) == 2 &&
                        loneCarriageReturn[0, 0] == 'A' && loneCarriageReturn[0, 1] == '\r' &&
                        loneCarriageReturn[1, 0] == 'B', "A lone carriage return should be preserved as its own character.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }
            };
        }

        private static Action HandlesEmptyAndInvalidInputs()
        {
            return () =>
            {
                string path = TemporaryFilePath();
                try
                {
                    File.WriteAllBytes(path, Array.Empty<byte>());
                    char[,] empty = Tool_1.ParseFileToCharGrid(path);
                    Program.Assert(empty.GetLength(0) == 0 && empty.GetLength(1) == 0,
                        "An empty file should produce a 0-by-0 grid.");
                }
                finally
                {
                    if (File.Exists(path)) File.Delete(path);
                }

                Program.AssertThrows<ArgumentException>(() => Tool_1.ParseFileToCharGrid(" "),
                    "Whitespace paths should be rejected.");
                Program.AssertThrows<ArgumentException>(() => Tool_1.ParseFileToCharGrid(null!),
                    "Null paths should be rejected.");
                Program.AssertThrows<FileNotFoundException>(() => Tool_1.ParseFileToCharGrid(
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))),
                    "Missing files should report FileNotFoundException.");
            };
        }

        private static string TemporaryFilePath() => Path.Combine(Path.GetTempPath(), "open-lrd-grid-" + Guid.NewGuid().ToString("N"));
    }
}
