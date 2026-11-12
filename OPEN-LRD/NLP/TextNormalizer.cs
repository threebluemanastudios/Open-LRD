using System.Text;

namespace OPEN_LRD.NLP
{
    internal static class TextNormalizer
    {
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string normalized = text.Normalize(NormalizationForm.FormKC)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            StringBuilder builder = new StringBuilder(normalized.Length);
            bool previousWasSpace = false;

            foreach (char source in normalized)
            {
                if (source == '\n')
                {
                    if (builder.Length > 0 && builder[^1] != '\n')
                        builder.Append('\n');

                    previousWasSpace = false;
                    continue;
                }

                char value = MapCharacter(source);

                if (char.IsWhiteSpace(value))
                {
                    if (builder.Length > 0 && builder[^1] != '\n' && !previousWasSpace)
                        builder.Append(' ');

                    previousWasSpace = true;
                    continue;
                }

                if (char.IsControl(value))
                    continue;

                builder.Append(value);
                previousWasSpace = false;
            }

            return builder.ToString().Trim();
        }

        private static char MapCharacter(char value)
        {
            return value switch
            {
                '\u2018' or '\u2019' or '\u201A' or '\u201B' => '\'',
                '\u201C' or '\u201D' or '\u201E' or '\u201F' => '"',
                '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2015' or '\u2212' => '-',
                '\u00A0' or '\u1680' or '\u2000' or '\u2001' or '\u2002' or '\u2003' or '\u2004'
                    or '\u2005' or '\u2006' or '\u2007' or '\u2008' or '\u2009' or '\u200A'
                    or '\u202F' or '\u205F' or '\u3000' => ' ',
                '\u200B' or '\u200C' or '\u200D' or '\uFEFF' => '\0',
                _ => value
            };
        }
    }
}
