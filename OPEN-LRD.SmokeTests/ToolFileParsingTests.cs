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
