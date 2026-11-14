using System.Globalization;
using System.Text.RegularExpressions;

namespace OPEN_LRD.NLP
{
    internal sealed record TemporalRangeQuery(
        DateTimeOffset Start,
        DateTimeOffset End,
        string OriginalExpression);

    internal static class TemporalExpressionParser
    {
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        private static readonly Regex betweenRange = new Regex(
            @"\bbetween\s+(?<start>.+?)\s+and\s+(?<end>.+?)(?:[?.!;]|$)",
            Options);

        private static readonly Regex fromRange = new Regex(
            @"\bfrom\s+(?<start>.+?)\s+to\s+(?<end>.+?)(?:[?.!;]|$)",
            Options);

        private static readonly Regex pointQuery = new Regex(
            @"\b(?:on|in|at)\s+(?<point>.+?)(?:[?.!;]|$)",
            Options);

        private static readonly Regex eventInquiry = new Regex(
            @"^\s*(?:(?:what|which)\s+(?:(?:events?|things?)\s+)?(?:happened|occurred|took\s+place)\b|what\s+did\s+(?:happen|occur)\b)",
            Options);

        private static readonly Regex datedClockRange = new Regex(
            @"(?<date>\d{4}-\d{1,2}-\d{1,2}|\d{1,2}/\d{1,2}/\d{2,4}|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4})\s+(?:from|between)\s+(?<start>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)\s+(?:to|and)\s+(?<end>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)",
            Options);

        private static readonly Regex datedClockPoint = new Regex(
            @"(?<date>\d{4}-\d{1,2}-\d{1,2}|\d{1,2}/\d{1,2}/\d{2,4}|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4})\s+(?:at\s+)?(?<time>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)",
            Options);

        private static readonly Regex clockRange = new Regex(
            @"(?:between|from)\s+(?<start>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)\s+(?:and|to)\s+(?<end>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)",
            Options);

        private static readonly Regex clock = new Regex(
            @"^\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?$",
            Options);

        private static readonly string[] dateFormats =
        {
            "yyyy-MM-dd", "yyyy-M-d", "M/d/yyyy", "M/d/yy",
            "MMMM d, yyyy", "MMMM d yyyy", "MMM d, yyyy", "MMM d yyyy",
            "MMMM yyyy", "MMM yyyy", "yyyy"
        };

        private static readonly string[] clockFormats =
        {
            "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss",
            "h:mm tt", "hh:mm tt", "h:mm:ss tt", "hh:mm:ss tt"
        };

        public static bool TryParseQuestionRange(
            string? text,
            DateTimeOffset? referenceTime,
            out TemporalRangeQuery query)
        {
            query = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!eventInquiry.IsMatch(text))
                return false;

            Match range = betweenRange.Match(text);
            if (!range.Success)
                range = fromRange.Match(text);

            if (range.Success
                && TryParseRangeBounds(
                    range.Groups["start"].Value,
                    range.Groups["end"].Value,
                    referenceTime,
                    out DateTimeOffset start,
                    out DateTimeOffset end))
            {
                query = new TemporalRangeQuery(start, end, range.Value.Trim());
                return true;
            }

            Match point = pointQuery.Match(text);
            if (point.Success
                && TryParsePointWindow(
                    point.Groups["point"].Value,
                    referenceTime,
                    out DateTimeOffset? pointStart,
                    out DateTimeOffset? pointEnd)
                && pointStart.HasValue
                && pointEnd.HasValue)
            {
                query = new TemporalRangeQuery(pointStart.Value, pointEnd.Value, point.Value.Trim());
                return true;
            }

            return false;
        }

        public static bool TryParseEventWindow(
            string? text,
            DateTimeOffset? referenceTime,
            out DateTimeOffset? startTime,
            out DateTimeOffset? endTime)
        {
            startTime = null;
            endTime = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match datedRange = datedClockRange.Match(text);
            if (datedRange.Success
                && TryParseDateWindow(datedRange.Groups["date"].Value, referenceTime, out DateTimeOffset dateStart, out _)
                && TryParseClockOnDate(datedRange.Groups["start"].Value, dateStart, out DateTimeOffset start)
                && TryParseClockOnDate(datedRange.Groups["end"].Value, dateStart, out DateTimeOffset end))
            {
                if (end < start)
                    end = end.AddDays(1);
                startTime = start;
                endTime = end;
                return true;
            }

            Match times = clockRange.Match(text);
            if (times.Success)
            {
                DateTimeOffset basis = referenceTime ?? DateTimeOffset.Now;
                if (TryParseClockOnDate(times.Groups["start"].Value, basis, out DateTimeOffset rangeStart)
                    && TryParseClockOnDate(times.Groups["end"].Value, basis, out DateTimeOffset rangeEnd))
                {
                    if (rangeEnd < rangeStart)
                        rangeEnd = rangeEnd.AddDays(1);
                    startTime = rangeStart;
                    endTime = rangeEnd;
                    return true;
                }
            }

            Match datedClock = datedClockPoint.Match(text);
            if (datedClock.Success
                && TryParseDateWindow(datedClock.Groups["date"].Value, referenceTime, out DateTimeOffset day, out _)
                && TryParseClockOnDate(datedClock.Groups["time"].Value, day, out DateTimeOffset datedTime))
            {
                startTime = datedTime;
                endTime = datedTime;
                return true;
            }

            Match isoTimestamp = Regex.Match(
                text,
                @"\b\d{4}-\d{1,2}-\d{1,2}T\d{1,2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})\b",
                Options);
            if (isoTimestamp.Success
                && DateTimeOffset.TryParse(
                    isoTimestamp.Value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset isoTime))
            {
                startTime = isoTime;
                endTime = isoTime;
                return true;
            }

            Match singleClock = Regex.Match(
                text,
                @"\b(?:at\s+)?(?<time>\d{1,2}:\d{2}(?::\d{2})?\s*(?:AM|PM)?)\b",
                Options);
            if (singleClock.Success
                && TryParseClockOnDate(
                    singleClock.Groups["time"].Value,
                    referenceTime ?? DateTimeOffset.Now,
                    out DateTimeOffset oneTime))
            {
                startTime = oneTime;
                endTime = oneTime;
                return true;
            }

            foreach (Match dateMatch in Regex.Matches(
                text,
                @"\b(?:\d{4}-\d{1,2}-\d{1,2}|\d{1,2}/\d{1,2}/\d{2,4}|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}|(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}|\d{4})\b",
                Options))
            {
                if (TryParseDateWindow(
                    dateMatch.Value,
                    referenceTime,
                    out DateTimeOffset parsedDateStart,
                    out DateTimeOffset dateEnd))
                {
                    startTime = parsedDateStart;
                    endTime = dateEnd;
                    return true;
                }
            }

            string normalized = text.Trim().TrimEnd('.', '!', '?', ',', ';', ':').Trim();
            if (normalized is "today" or "tomorrow" or "yesterday")
            {
                DateTimeOffset basis = (referenceTime ?? DateTimeOffset.Now).Date;
                DateTimeOffset relativeDay = normalized switch
                {
                    "tomorrow" => basis.AddDays(1),
                    "yesterday" => basis.AddDays(-1),
                    _ => basis
                };
                startTime = relativeDay;
                endTime = relativeDay.AddDays(1).AddTicks(-1);
                return true;
            }

            return false;
        }

        public static bool TryParseRangeBounds(
            string startExpression,
            string endExpression,
            DateTimeOffset? referenceTime,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            if (!TryParsePointWindow(startExpression, referenceTime, out DateTimeOffset? startLow, out _)
                || !TryParsePointWindow(endExpression, referenceTime, out _, out DateTimeOffset? endHigh)
                || !startLow.HasValue
                || !endHigh.HasValue
                || startLow.Value > endHigh.Value)
            {
                return false;
            }

            start = startLow.Value;
            end = endHigh.Value;
            return true;
        }

        public static bool TryParsePointWindow(
            string expression,
            DateTimeOffset? referenceTime,
            out DateTimeOffset? start,
            out DateTimeOffset? end)
        {
            start = null;
            end = null;
            string value = expression.Trim().TrimEnd('.', '!', '?', ',', ';', ':').Trim();
            value = Regex.Replace(value, @"^(?:on|in|at)\s+", string.Empty, Options);

            if (TryParseDateWindow(value, referenceTime, out DateTimeOffset dateStart, out DateTimeOffset dateEnd))
            {
                start = dateStart;
                end = dateEnd;
                return true;
            }

            if (clock.IsMatch(value)
                && TryParseClockOnDate(value, referenceTime ?? DateTimeOffset.Now, out DateTimeOffset time))
            {
                start = time;
                end = time;
                return true;
            }

            if ((value.Contains('T') || value.Contains('t')
                    || (value.Contains(':') && Regex.IsMatch(value, @"\d{4}[-/]\d{1,2}[-/]\d{1,2}")))
                && DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out DateTimeOffset exact))
            {
                start = exact;
                end = exact;
                return true;
            }

            return false;
        }

        private static bool TryParseDateWindow(
            string value,
            DateTimeOffset? referenceTime,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            string cleaned = value.Trim().TrimEnd('.', ',', ';', ':');
            DateTimeOffset basis = referenceTime ?? DateTimeOffset.Now;

            if (cleaned.Equals("today", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("tomorrow", StringComparison.OrdinalIgnoreCase)
                || cleaned.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
            {
                DateTimeOffset day = basis.Date;
                if (cleaned.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
                    day = day.AddDays(1);
                else if (cleaned.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
                    day = day.AddDays(-1);
                start = day;
                end = day.AddDays(1).AddTicks(-1);
                return true;
            }

            if (!DateTime.TryParseExact(
                cleaned,
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime parsed))
            {
                return false;
            }

            bool yearOnly = Regex.IsMatch(cleaned, @"^\d{4}$", RegexOptions.CultureInvariant);
            bool monthOnly = Regex.IsMatch(
                cleaned,
                @"^(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}$",
                Options);
            DateTime firstDay = yearOnly
                ? new DateTime(parsed.Year, 1, 1)
                : monthOnly
                    ? new DateTime(parsed.Year, parsed.Month, 1)
                    : parsed.Date;
            DateTime lastDay = yearOnly
                ? new DateTime(parsed.Year, 12, 31)
                : monthOnly
                    ? new DateTime(parsed.Year, parsed.Month, DateTime.DaysInMonth(parsed.Year, parsed.Month))
                    : parsed.Date;

            start = new DateTimeOffset(firstDay, basis.Offset);
            end = new DateTimeOffset(lastDay, basis.Offset).AddDays(1).AddTicks(-1);
            return true;
        }

        private static bool TryParseClockOnDate(
            string value,
            DateTimeOffset date,
            out DateTimeOffset result)
        {
            result = default;
            string cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
            if (!DateTime.TryParseExact(
                cleaned,
                clockFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime parsed))
            {
                return false;
            }

            result = new DateTimeOffset(
                date.Year,
                date.Month,
                date.Day,
                parsed.Hour,
                parsed.Minute,
                parsed.Second,
                parsed.Millisecond,
                date.Offset);
            return true;
        }
    }
}
