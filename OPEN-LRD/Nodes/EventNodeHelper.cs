using System.Globalization;

namespace OPEN_LRD.Nodes
{
    /// <summary>Parses, formats, and updates event time-window metadata.</summary>
    internal static class EventNodeHelper
    {
        private const string LabelPrefix = "OPEN-LRD:EVENT-WINDOW:v1|";

        public static string? FormatWindowLabel(DateTimeOffset? startTime, DateTimeOffset? endTime)
        {
            ValidateWindow(startTime, endTime);

            if (!startTime.HasValue && !endTime.HasValue)
                return null;

            string start = startTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
            string end = endTime?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
            return LabelPrefix + start + "|" + end;
        }

        public static bool TryParseWindow(
            string? label,
            out DateTimeOffset? startTime,
            out DateTimeOffset? endTime)
        {
            startTime = null;
            endTime = null;

            if (string.IsNullOrWhiteSpace(label)
                || !label.StartsWith(LabelPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = label[LabelPrefix.Length..].Split('|');
            if (parts.Length != 2)
                return false;

            if (!TryParseEndpoint(parts[0], out startTime)
                || !TryParseEndpoint(parts[1], out endTime))
            {
                startTime = null;
                endTime = null;
                return false;
            }

            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            {
                startTime = null;
                endTime = null;
                return false;
            }

            return startTime.HasValue || endTime.HasValue;
        }

        public static bool TryGetWindow(
            Web_Node node,
            out DateTimeOffset? startTime,
            out DateTimeOffset? endTime)
        {
            ArgumentNullException.ThrowIfNull(node);
            startTime = null;
            endTime = null;
            if (node.Get_NodeType() != NodeTypes.Events)
                return false;

            return TryParseWindow(node.Label, out startTime, out endTime);
        }

        public static void SetWindow(
            Web_Node node,
            DateTimeOffset? startTime,
            DateTimeOffset? endTime)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (node.Get_NodeType() != NodeTypes.Events)
                throw new ArgumentException("An event window can only be assigned to an Event node.", nameof(node));

            node.Set_Label(FormatWindowLabel(startTime, endTime));
        }

        public static bool AddName(Web_Node node, string? name)
        {
            ArgumentNullException.ThrowIfNull(node);
            if (node.Get_NodeType() != NodeTypes.Events || string.IsNullOrWhiteSpace(name))
                return false;

            return node.Add_Alias(name.Trim());
        }

        public static string GetDisplayName(Web_Node node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (node.Get_NodeType() == NodeTypes.Events)
            {
                string? name = node.Aliases.FirstOrDefault(alias => !string.IsNullOrWhiteSpace(alias));
                if (name is not null)
                    return name;

                if (!string.IsNullOrWhiteSpace(node.Label)
                    && !node.Label.StartsWith(LabelPrefix, StringComparison.Ordinal))
                {
                    return node.Label!;
                }

                if (TryGetWindow(node, out DateTimeOffset? start, out DateTimeOffset? end))
                {
                    if (start.HasValue)
                        return "Event on " + start.Value.ToString("d", CultureInfo.CurrentCulture);
                    if (end.HasValue)
                        return "Event ending " + end.Value.ToString("d", CultureInfo.CurrentCulture);
                }

                return "Event";
            }

            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            return node.Aliases.FirstOrDefault(alias => !string.IsNullOrWhiteSpace(alias)) ?? string.Empty;
        }

        public static void ValidateWindow(DateTimeOffset? startTime, DateTimeOffset? endTime)
        {
            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            {
                throw new ArgumentException("Event start time cannot be after its end time.");
            }
        }

        private static bool TryParseEndpoint(string value, out DateTimeOffset? endpoint)
        {
            if (value.Length == 0)
            {
                endpoint = null;
                return true;
            }

            if (DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset parsed))
            {
                endpoint = parsed;
                return true;
            }

            endpoint = null;
            return false;
        }
    }
}
