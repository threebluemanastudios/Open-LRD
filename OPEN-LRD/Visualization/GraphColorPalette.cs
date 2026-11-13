using System;
using System.Collections.Generic;
using System.Linq;

namespace OPEN_LRD.Visualization
{
    internal readonly record struct RgbColor(byte R, byte G, byte B)
    {
        public override string ToString()
        {
            return $"#{R:X2}{G:X2}{B:X2}";
        }
    }

    internal sealed record ColorChoice(string Name, RgbColor Color);

    internal static class GraphColorPalette
    {
        private static readonly ColorChoice[] palette = new[]
        {
            new ColorChoice("Red", new RgbColor(220, 60, 60)),
            new ColorChoice("Coral", new RgbColor(240, 110, 90)),
            new ColorChoice("Orange", new RgbColor(240, 150, 60)),
            new ColorChoice("Amber", new RgbColor(240, 190, 70)),
            new ColorChoice("Gold", new RgbColor(225, 205, 80)),
            new ColorChoice("Yellow", new RgbColor(235, 225, 90)),
            new ColorChoice("Lime", new RgbColor(150, 210, 80)),
            new ColorChoice("Green", new RgbColor(80, 180, 100)),
            new ColorChoice("Teal", new RgbColor(60, 170, 150)),
            new ColorChoice("Turquoise", new RgbColor(70, 200, 190)),
            new ColorChoice("Cyan", new RgbColor(80, 215, 225)),
            new ColorChoice("Sky", new RgbColor(100, 175, 235)),
            new ColorChoice("Blue", new RgbColor(90, 130, 225)),
            new ColorChoice("Indigo", new RgbColor(110, 100, 205)),
            new ColorChoice("Violet", new RgbColor(150, 110, 220)),
            new ColorChoice("Purple", new RgbColor(175, 100, 195)),
            new ColorChoice("Magenta", new RgbColor(215, 90, 180)),
            new ColorChoice("Pink", new RgbColor(235, 120, 165)),
            new ColorChoice("Rose", new RgbColor(230, 105, 135)),
            new ColorChoice("Brown", new RgbColor(165, 115, 80)),
            new ColorChoice("Tan", new RgbColor(205, 175, 130)),
            new ColorChoice("White", new RgbColor(240, 240, 240)),
            new ColorChoice("LightGray", new RgbColor(180, 180, 180)),
            new ColorChoice("DarkGray", new RgbColor(110, 110, 110))
        };

        private static readonly Dictionary<string, ColorChoice> byName =
            palette.ToDictionary(choice => choice.Name, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<RgbColor, string> byColor = BuildColorLookup();

        public static IReadOnlyList<ColorChoice> Palette => palette;

        public static ColorChoice GetDefaultNodeColor(int nodeTypeId)
        {
            return palette[GetDefaultIndex(nodeTypeId)];
        }

        public static ColorChoice GetDefaultLinkColor(int forwardTypeId)
        {
            return palette[GetDefaultIndex(forwardTypeId)];
        }

        public static ColorChoice GetDefaultClassColor(int classId)
        {
            return palette[GetDefaultIndex(classId)];
        }

        public static bool TryGet(string name, out ColorChoice choice)
        {
            if (name is not null
                && byName.TryGetValue(name, out ColorChoice? found)
                && found is not null)
            {
                choice = found;
                return true;
            }

            choice = palette[0];
            return false;
        }

        public static bool TryGetName(RgbColor color, out string? name)
        {
            return byColor.TryGetValue(color, out name);
        }

        public static int IndexOf(string name)
        {
            if (name is null)
                return -1;

            for (int index = 0; index < palette.Length; index++)
            {
                if (string.Equals(palette[index].Name, name, StringComparison.OrdinalIgnoreCase))
                    return index;
            }

            return -1;
        }

        private static int GetDefaultIndex(int typeId)
        {
            long mapped = ((long)typeId * 5L + 7L) % palette.Length;

            if (mapped < 0L)
                mapped += palette.Length;

            return (int)mapped;
        }

        private static Dictionary<RgbColor, string> BuildColorLookup()
        {
            Dictionary<RgbColor, string> lookup = new Dictionary<RgbColor, string>();

            foreach (ColorChoice choice in palette)
                lookup[choice.Color] = choice.Name;

            return lookup;
        }
    }
}
