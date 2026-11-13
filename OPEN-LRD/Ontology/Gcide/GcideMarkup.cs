using System.Text;

namespace OPEN_LRD.Ontology.Gcide
{
    // Decodes the GCIDE SGML-like markup: MICRA \'xx escape positions and
    // <name/ entities, then strips remaining tags. See gcide-0.54 webfont.txt
    // and tagset.txt.
    internal static class GcideMarkup
    {
        private static readonly Dictionary<string, string> NamedEntities = new(StringComparer.OrdinalIgnoreCase)
        {
            ["br"] = " ",
            ["or"] = "or",
            ["and"] = "and",
            ["root"] = "\u221A",
            ["croot"] = "\u221B",
            ["prime"] = "\u2032",
            ["3prime"] = "\u2034",
            ["dagger"] = "\u2020",
            ["ddagger"] = "\u2021",
            ["para"] = "\u00B6",
            ["sec"] = "\u00A7",
            ["copy"] = "\u00A9",
            ["reg"] = "\u00AE",
            ["deg"] = "\u00B0",
            ["times"] = "\u00D7",
            ["div"] = "\u00F7",
            ["plusmn"] = "\u00B1",
            ["emdash"] = "\u2014",
            ["endash"] = "\u2013"
        };

        // MICRA special-font escape positions. Documented and common positions
        // are mapped; unmapped printable codes fall back to ASCII and other
        // unmapped codes are dropped so no mojibake survives.
        private static readonly Dictionary<int, char> Escapes = new()
        {
            [0x81] = '\u00FC',
            [0x82] = '\u00E9',
            [0x84] = '\u00E4',
            [0x85] = '\u00E0',
            [0x86] = '\u00E5',
            [0x87] = '\u00E7',
            [0x88] = '\u00EA',
            [0x8A] = '\u00E8',
            [0x8B] = '\u00EF',
            [0x8C] = '\u00EE',
            [0x8D] = '\u00EC',
            [0x94] = '\u00F6',
            [0x92] = '\u00F4',
            [0x93] = '\u00F2',
            [0x95] = '\u00FB',
            [0x96] = '\u00F9',
            [0x9C] = '\u00A3',
            [0xA5] = '\u00A5',
            [0xBD] = '\u201C',
            [0xB8] = '\u201D'
        };

        public static string DecodeEscapes(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            StringBuilder builder = new StringBuilder(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                char current = input[i];

                if (current == '\\'
                    && i + 3 < input.Length
                    && input[i + 1] == '\''
                    && IsHex(input[i + 2])
                    && IsHex(input[i + 3]))
                {
                    int code = Convert.ToInt32(input.Substring(i + 2, 2), 16);

                    if (Escapes.TryGetValue(code, out char mapped))
                        builder.Append(mapped);
                    else if (code is >= 0x20 and < 0x7F)
                        builder.Append((char)code);

                    i += 3;
                    continue;
                }

                if (current == '<')
                {
                    int slash = input.IndexOf('/', i + 1);

                    if (slash > i)
                    {
                        string name = input.Substring(i + 1, slash - i - 1);

                        if (name.Length > 0 && name.All(char.IsLetterOrDigit))
                        {
                            if (NamedEntities.TryGetValue(name, out string? value))
                                builder.Append(value);

                            i = slash;
                            continue;
                        }
                    }
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        public static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            StringBuilder builder = new StringBuilder(input.Length);
            bool inTag = false;

            foreach (char current in input)
            {
                if (current == '<')
                {
                    inTag = true;
                    continue;
                }

                if (inTag)
                {
                    if (current == '>')
                        inTag = false;

                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return NormalizeWhitespace(StripTags(DecodeEscapes(input)));
        }

        public static string NormalizeWhitespace(string input)
        {
            StringBuilder builder = new StringBuilder(input.Length);
            bool pendingSpace = false;

            foreach (char current in input)
            {
                if (char.IsWhiteSpace(current))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsHex(char value)
        {
            return value is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F';
        }
    }
}
