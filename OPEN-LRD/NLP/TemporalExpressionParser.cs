using System.Globalization;
using System.Text.RegularExpressions;

namespace OPEN_LRD.NLP
{
    internal sealed record TemporalRangeQuery(
        DateTimeOffset Start,
        DateTimeOffset End,
        string OriginalExpression,
        bool IsCountQuery = false);

    /// <summary>Parses bounded absolute and relative temporal expressions using a reference time.</summary>
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
            @"^\s*(?:(?:what|which)\s+(?:(?:events?|things?)\s+)?(?:happen(?:ed|s|ing)?|occur(?:red|s|ing)?|took\s+place|will\s+(?:happen|occur)|(?:is|are)\s+going\s+to\s+(?:happen|occur)|(?:is|are)\s+(?:happening|occurring))\b|what\s+did\s+(?:happen|occur)\b|how\s+many\s+events?\s+(?:happen(?:ed|s|ing)?|occur(?:red|s|ing)?|take\s+place|will\s+(?:happen|occur))\b)",
            Options);

        private static readonly Regex frontedEventInquiry = new Regex(
            @"^\s*(?<temporal>(?:yesterday|today|tomorrow|(?:last|this|next)\s+\w+|between\s+.+?\s+and\s+.+?|from\s+.+?\s+to\s+.+?))[,;]\s*(?<question>(?:what|which)\s+.+?)\??\s*$",
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

        private const string DayNames =
            "Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday|Mon|Tue|Tues|Wed|Thu|Thur|Thurs|Fri|Sat|Sun";
        private const string MonthNames =
            "January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Sept|Oct|Nov|Dec";
        private const string SmallNumberWords =
            "a|an|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen";
        private const string NumberWords =
            SmallNumberWords + "|twenty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|thirty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|forty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|fifty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|sixty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|seventy(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|eighty(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?"
            + "|ninety(?:[- ](?:one|two|three|four|five|six|seven|eight|nine))?";

        private static readonly Regex offsetExpression = new Regex(
            @"\b(?<amount>\d+|" + NumberWords + @")\s+(?<unit>seconds?|secs?|minutes?|mins?|hours?|hrs?|days?|weeks?|wks?|months?|mos?|years?|yrs?)\s+(?<direction>ago|in\s+(?:the\s+)?past|past|from\s+now|from\s+today)\b",
            Options);

        private static readonly Regex calendarPeriodExpression = new Regex(
            @"\b(?<modifier>last|this|next)\s+(?<unit>week|month|year)\b",
            Options);

        private static readonly Regex modifiedWeekdayExpression = new Regex(
            @"\b(?<modifier>last|this|next)\s+(?<day>" + DayNames + @")\b",
            Options);

        private static readonly Regex weekdayExpression = new Regex(
            @"\b(?:on\s+)?(?<day>" + DayNames + @")\b",
            Options);

        private static readonly Regex modifiedMonthExpression = new Regex(
            @"\b(?<modifier>last|this|next)\s+(?<month>" + MonthNames + @")\b(?:\s+(?<year>\d{4}))?",
            Options);

        private static readonly Regex prepositionMonthExpression = new Regex(
            @"\b(?:in|during|on)\s+(?<month>" + MonthNames + @")\b(?:\s+(?<year>\d{4}))?",
            Options);

        private static readonly Regex standaloneMonthExpression = new Regex(
            @"^(?<month>" + MonthNames + @")\b(?:\s+(?<year>\d{4}))?$",
            Options);

        private static readonly Dictionary<string, int> monthNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["January"] = 1, ["Jan"] = 1,
            ["February"] = 2, ["Feb"] = 2,
            ["March"] = 3, ["Mar"] = 3,
            ["April"] = 4, ["Apr"] = 4,
            ["May"] = 5,
            ["June"] = 6, ["Jun"] = 6,
            ["July"] = 7, ["Jul"] = 7,
            ["August"] = 8, ["Aug"] = 8,
            ["September"] = 9, ["Sep"] = 9, ["Sept"] = 9,
            ["October"] = 10, ["Oct"] = 10,
            ["November"] = 11, ["Nov"] = 11,
            ["December"] = 12, ["Dec"] = 12
        };

        private static readonly Dictionary<string, int> smallNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 1, ["an"] = 1,
            ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
            ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8,
            ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12,
            ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
            ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18,
            ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
            ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60,
            ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90
        };

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

            string queryText = text.Trim();
            Match frontedInquiry = frontedEventInquiry.Match(queryText);
            if (frontedInquiry.Success)
            {
                string question = frontedInquiry.Groups["question"].Value
                    .Trim()
                    .TrimEnd('?', '.', '!', ';');
                queryText = question + " " + frontedInquiry.Groups["temporal"].Value.Trim() + "?";
            }

            if (!eventInquiry.IsMatch(queryText))
                return false;

            bool futureTense = IsFutureQuestion(queryText);
            bool countQuery = Regex.IsMatch(queryText, @"^\s*how\s+many\s+events?\b", Options);

            Match range = betweenRange.Match(queryText);
            if (!range.Success)
                range = fromRange.Match(queryText);

            if (range.Success
                && TryParseRangeBounds(
                    range.Groups["start"].Value,
                    range.Groups["end"].Value,
                    referenceTime,
                    futureTense,
                    out DateTimeOffset start,
                    out DateTimeOffset end))
            {
                query = new TemporalRangeQuery(start, end, range.Value.Trim(), countQuery);
                return true;
            }

            Match point = pointQuery.Match(queryText);
            if (point.Success
                && TryParsePointWindow(
                    point.Groups["point"].Value,
                    referenceTime,
                    futureTense,
                    out DateTimeOffset? pointStart,
                    out DateTimeOffset? pointEnd)
                && pointStart.HasValue
                && pointEnd.HasValue)
            {
                query = new TemporalRangeQuery(pointStart.Value, pointEnd.Value, point.Value.Trim(), countQuery);
                return true;
            }

            if (TryParseRelativeExpression(
                queryText,
                referenceTime ?? DateTimeOffset.Now,
                futureTense,
                out DateTimeOffset relativeStart,
                out DateTimeOffset relativeEnd,
                out string relativeExpression))
            {
                query = new TemporalRangeQuery(relativeStart, relativeEnd, relativeExpression, countQuery);
                return true;
            }

            return false;
        }

        private static bool IsFutureQuestion(string text)
        {
            return Regex.IsMatch(
                text,
                @"\b(?:will|shall|going\s+to|next)\b",
                Options);
        }

        public static bool TryParseEventWindow(
            string? text,
            DateTimeOffset? referenceTime,
            out DateTimeOffset? startTime,
            out DateTimeOffset? endTime,
            bool futureTense = false)
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
                if (TryParseRelativeExpression(
                    text,
                    basis,
                    futureTense,
                    out DateTimeOffset expressionStart,
                    out _,
                    out _))
                {
                    basis = expressionStart;
                }

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

            if (TryParseRelativeExpression(
                text,
                referenceTime ?? DateTimeOffset.Now,
                futureTense,
                out DateTimeOffset relativeStart,
                out DateTimeOffset relativeEnd,
                out _))
            {
                startTime = relativeStart;
                endTime = relativeEnd;
                return true;
            }

            return false;
        }

        public static bool TryParseRangeBounds(
            string startExpression,
            string endExpression,
            DateTimeOffset? referenceTime,
            bool futureTense,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            DateTimeOffset basis = referenceTime ?? DateTimeOffset.Now;
            if (TryParseWeekdayRangeEndpoint(startExpression, basis, out DateTimeOffset weekdayStart, out _)
                && TryParseWeekdayRangeEndpoint(endExpression, basis, out _, out DateTimeOffset weekdayEnd)
                && weekdayStart <= weekdayEnd)
            {
                start = weekdayStart;
                end = weekdayEnd;
                return true;
            }

            if (!TryParsePointWindow(startExpression, referenceTime, futureTense, out DateTimeOffset? startLow, out _)
                || !TryParsePointWindow(endExpression, referenceTime, futureTense, out _, out DateTimeOffset? endHigh)
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

        private static bool TryParseWeekdayRangeEndpoint(
            string expression,
            DateTimeOffset basis,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            Match match = Regex.Match(
                expression.Trim(),
                @"^(?:(?<modifier>last|this|next)\s+)?(?:on\s+)?(?<day>" + DayNames + @")$",
                Options);
            if (!match.Success || !TryParseDayOfWeek(match.Groups["day"].Value, out DayOfWeek dayOfWeek))
            {
                return false;
            }

            DateTimeOffset monday = StartOfIsoWeek(basis);
            int dayOffset = ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            int weekOffset = match.Groups["modifier"].Value.ToLowerInvariant() switch
            {
                "last" => -1,
                "next" => 1,
                _ => 0
            };
            SetDayWindow(monday.AddDays(weekOffset * 7 + dayOffset), out start, out end);
            return true;
        }

        public static bool TryParsePointWindow(
            string expression,
            DateTimeOffset? referenceTime,
            bool futureTense,
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

            if (TryParseRelativeExpression(
                value,
                referenceTime ?? DateTimeOffset.Now,
                futureTense,
                out DateTimeOffset relativeStart,
                out DateTimeOffset relativeEnd,
                out _))
            {
                start = relativeStart;
                end = relativeEnd;
                return true;
            }

            return false;
        }

        public static bool TryParseRelativeExpression(
            string text,
            DateTimeOffset referenceTime,
            bool futureTense,
            out DateTimeOffset start,
            out DateTimeOffset end,
            out string matchedExpression)
        {
            start = default;
            end = default;
            matchedExpression = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match period = calendarPeriodExpression.Match(text);
            if (period.Success
                && TryCalendarPeriod(
                    period.Groups["modifier"].Value,
                    period.Groups["unit"].Value,
                    referenceTime,
                    out start,
                    out end))
            {
                matchedExpression = period.Value.Trim();
                return true;
            }

            Match offset = offsetExpression.Match(text);
            if (offset.Success
                && TryRelativeOffset(
                    offset.Groups["amount"].Value,
                    offset.Groups["unit"].Value,
                    offset.Groups["direction"].Value,
                    referenceTime,
                    out start,
                    out end))
            {
                matchedExpression = offset.Value.Trim();
                return true;
            }

            Match dayWord = Regex.Match(text, @"\b(?<day>yesterday|today|tomorrow)\b", Options);
            if (dayWord.Success)
            {
                DateTimeOffset day = StartOfDay(referenceTime);
                if (dayWord.Groups["day"].Value.Equals("yesterday", StringComparison.OrdinalIgnoreCase))
                    day = day.AddDays(-1);
                else if (dayWord.Groups["day"].Value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase))
                    day = day.AddDays(1);
                SetDayWindow(day, out start, out end);
                matchedExpression = dayWord.Value;
                return true;
            }

            Match weekday = modifiedWeekdayExpression.Match(text);
            if (weekday.Success)
            {
                if (TryWeekdayWindow(
                    weekday.Groups["day"].Value,
                    weekday.Groups["modifier"].Value,
                    referenceTime,
                    futureTense,
                    out start,
                    out end))
                {
                    matchedExpression = weekday.Value.Trim();
                    return true;
                }
            }

            Match month = modifiedMonthExpression.Match(text);
            if (!month.Success)
                month = prepositionMonthExpression.Match(text);
            if (!month.Success)
                month = standaloneMonthExpression.Match(StripPunctuation(text).Trim());

            if (month.Success
                && TryNamedMonthWindow(
                    month.Groups["month"].Value,
                    month.Groups["modifier"].Success ? month.Groups["modifier"].Value : string.Empty,
                    month.Groups["year"].Success ? month.Groups["year"].Value : null,
                    referenceTime,
                    futureTense,
                    out start,
                    out end))
            {
                matchedExpression = month.Value.Trim();
                return true;
            }

            Match namedDay = weekdayExpression.Match(text);
            if (namedDay.Success
                && TryWeekdayWindow(
                    namedDay.Groups["day"].Value,
                    string.Empty,
                    referenceTime,
                    futureTense,
                    out start,
                    out end))
            {
                matchedExpression = namedDay.Value.Trim();
                return true;
            }

            return false;
        }

        private static bool TryCalendarPeriod(
            string modifier,
            string unit,
            DateTimeOffset basis,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            string normalizedModifier = modifier.ToLowerInvariant();
            string normalizedUnit = unit.ToLowerInvariant();
            DateTimeOffset first;
            DateTimeOffset next;

            switch (normalizedUnit)
            {
                case "week":
                {
                    DateTimeOffset monday = StartOfIsoWeek(basis);
                    int weeks = normalizedModifier switch
                    {
                        "last" => -1,
                        "next" => 1,
                        _ => 0
                    };
                    first = monday.AddDays(weeks * 7);
                    next = first.AddDays(7);
                    break;
                }
                case "month":
                {
                    DateTimeOffset month = new DateTimeOffset(basis.Year, basis.Month, 1, 0, 0, 0, basis.Offset);
                    int months = normalizedModifier switch
                    {
                        "last" => -1,
                        "next" => 1,
                        _ => 0
                    };
                    first = month.AddMonths(months);
                    next = first.AddMonths(1);
                    break;
                }
                case "year":
                {
                    DateTimeOffset year = new DateTimeOffset(basis.Year, 1, 1, 0, 0, 0, basis.Offset);
                    int years = normalizedModifier switch
                    {
                        "last" => -1,
                        "next" => 1,
                        _ => 0
                    };
                    first = year.AddYears(years);
                    next = first.AddYears(1);
                    break;
                }
                default:
                    return false;
            }

            start = first;
            end = next.AddTicks(-1);
            return true;
        }

        private static bool TryWeekdayWindow(
            string dayName,
            string modifier,
            DateTimeOffset basis,
            bool futureTense,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            if (!TryParseDayOfWeek(dayName, out DayOfWeek dayOfWeek))
                return false;

            DateTimeOffset monday = StartOfIsoWeek(basis);
            int dayOffset = ((int)dayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTimeOffset target = monday.AddDays(dayOffset);

            switch (modifier.ToLowerInvariant())
            {
                case "last":
                    target = target.AddDays(-7);
                    break;
                case "next":
                    target = target.AddDays(7);
                    break;
                case "this":
                    break;
                default:
                    if (futureTense && target.Date < basis.Date)
                        target = target.AddDays(7);
                    else if (!futureTense && target.Date > basis.Date)
                        target = target.AddDays(-7);
                    break;
            }

            SetDayWindow(target, out start, out end);
            return true;
        }

        private static bool TryParseDayOfWeek(string value, out DayOfWeek day)
        {
            if (Enum.TryParse(value, ignoreCase: true, out day))
                return true;

            day = value.Trim().ToLowerInvariant() switch
            {
                "mon" => DayOfWeek.Monday,
                "tue" or "tues" => DayOfWeek.Tuesday,
                "wed" => DayOfWeek.Wednesday,
                "thu" or "thur" or "thurs" => DayOfWeek.Thursday,
                "fri" => DayOfWeek.Friday,
                "sat" => DayOfWeek.Saturday,
                "sun" => DayOfWeek.Sunday,
                _ => (DayOfWeek)(-1)
            };

            return Enum.IsDefined(day);
        }

        private static bool TryNamedMonthWindow(
            string monthName,
            string modifier,
            string? explicitYear,
            DateTimeOffset basis,
            bool futureTense,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            if (!monthNumbers.TryGetValue(monthName, out int month))
                return false;

            int year;
            if (int.TryParse(explicitYear, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedYear))
            {
                year = parsedYear;
            }
            else
            {
                year = basis.Year;
                switch (modifier.ToLowerInvariant())
                {
                    case "last":
                        if (month >= basis.Month)
                            year--;
                        break;
                    case "next":
                        if (month <= basis.Month)
                            year++;
                        break;
                    case "this":
                        break;
                    default:
                        if (futureTense && month < basis.Month)
                            year++;
                        else if (!futureTense && month > basis.Month)
                            year--;
                        break;
                }
            }

            if (year < 1 || year > 9999)
                return false;

            DateTimeOffset monthStart = new DateTimeOffset(year, month, 1, 0, 0, 0, basis.Offset);
            DateTimeOffset nextMonth = monthStart.AddMonths(1);
            start = monthStart;
            end = nextMonth.AddTicks(-1);
            return true;
        }

        private static bool TryRelativeOffset(
            string amountText,
            string unitText,
            string directionText,
            DateTimeOffset basis,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = default;
            end = default;
            if (!TryParseAmount(amountText, out int amount) || amount <= 0)
                return false;

            string unit = unitText.Trim().ToLowerInvariant();
            bool negative = directionText.StartsWith("ago", StringComparison.OrdinalIgnoreCase)
                || directionText.StartsWith("in the past", StringComparison.OrdinalIgnoreCase);
            bool fromToday = directionText.EndsWith("from today", StringComparison.OrdinalIgnoreCase);
            int signedAmount = negative ? -amount : amount;
            DateTimeOffset origin = fromToday ? StartOfDay(basis) : basis;

            try
            {
                switch (unit)
                {
                    case "second": case "seconds": case "sec": case "secs":
                        start = end = origin.AddSeconds(signedAmount);
                        return true;
                    case "minute": case "minutes": case "min": case "mins":
                        start = end = origin.AddMinutes(signedAmount);
                        return true;
                    case "hour": case "hours": case "hr": case "hrs":
                        start = end = origin.AddHours(signedAmount);
                        return true;
                    case "day": case "days":
                        SetDayWindow(StartOfDay(origin).AddDays(signedAmount), out start, out end);
                        return true;
                    case "week": case "weeks": case "wk": case "wks":
                        SetDayWindow(StartOfDay(origin).AddDays(signedAmount * 7), out start, out end);
                        return true;
                    case "month": case "months": case "mo": case "mos":
                        SetDayWindow(StartOfDay(origin).AddMonths(signedAmount), out start, out end);
                        return true;
                    case "year": case "years": case "yr": case "yrs":
                        SetDayWindow(StartOfDay(origin).AddYears(signedAmount), out start, out end);
                        return true;
                    default:
                        return false;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryParseAmount(string amountText, out int amount)
        {
            amount = 0;
            string normalized = Regex.Replace(amountText.Trim().ToLowerInvariant(), @"[-\s]+", " ");
            if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out amount))
                return amount > 0;

            string[] parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return smallNumbers.TryGetValue(parts[0], out amount) && amount > 0;
            if (parts.Length == 2
                && smallNumbers.TryGetValue(parts[0], out int tens)
                && tens >= 20
                && tens % 10 == 0
                && smallNumbers.TryGetValue(parts[1], out int ones)
                && ones is >= 1 and <= 9)
            {
                amount = tens + ones;
                return true;
            }

            return false;
        }

        private static DateTimeOffset StartOfDay(DateTimeOffset value)
        {
            return new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);
        }

        private static string StripPunctuation(string value)
        {
            return value.Trim().TrimEnd('.', '!', '?', ',', ';', ':').Trim();
        }

        private static DateTimeOffset StartOfIsoWeek(DateTimeOffset value)
        {
            DateTimeOffset day = StartOfDay(value);
            int daysSinceMonday = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return day.AddDays(-daysSinceMonday);
        }

        private static void SetDayWindow(
            DateTimeOffset day,
            out DateTimeOffset start,
            out DateTimeOffset end)
        {
            start = StartOfDay(day);
            end = start.AddDays(1).AddTicks(-1);
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
