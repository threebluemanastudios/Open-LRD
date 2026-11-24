using System;
using System.Collections.Generic;
using System.Globalization;

namespace OPEN_LRD.Skills
{
    internal static class TimeAndDate
    {
        /// <summary>
        /// Determines whether year is a leap year.
        /// </summary>
        public static bool IsLeapYear(int year)
        {
            return DateTime.IsLeapYear(year);
        }
        /// <summary>
        /// Returns the number of days in the given month of the given year.
        /// </summary>

        public static int DaysInMonth(int year, int month)
        {
            return DateTime.DaysInMonth(year, month);
        }

        /// <summary>
        /// Returns the one-based day of the year for date.
        /// </summary>
        public static int DayOfYear(DateOnly date)
        {
            return date.DayOfYear;
        }

        /// <summary>
        /// Returns the ISO 8601 week-numbering year and week number for date.
        /// </summary>
        public static (int WeekYear, int WeekNumber) IsoWeek(DateOnly date)
        {
            DateTime value = date.ToDateTime(TimeOnly.MinValue);
            return (ISOWeek.GetYear(value), ISOWeek.GetWeekOfYear(value));
        }

        /// <summary>
        /// Returns the number of calendar days from start to end (negative if end is earlier than start).
        /// </summary>
        public static int DaysBetween(DateOnly start, DateOnly end)
        {
            return end.DayNumber - start.DayNumber;
        }

        /// <summary>
        /// Adds businessDays weekdays to date, skipping weekends and any dates in holidays. Negative values move backward.
        /// </summary>
        public static DateOnly AddBusinessDays(DateOnly date, int businessDays, IReadOnlySet<DateOnly>? holidays = null)
        {
            int remaining = businessDays;
            while (remaining != 0)
            {
                int direction = remaining > 0 ? 1 : -1;
                date = date.AddDays(direction);
                if (IsBusinessDay(date, holidays))
                {
                    remaining -= direction;
                }
            }
            return date;
        }

        /// <summary>
        /// Counts business days between start and end, excluding weekends and holidays. Returns a negative count if end is earlier than start.
        /// </summary>
        public static int BusinessDaysBetween(DateOnly start, DateOnly end, IReadOnlySet<DateOnly>? holidays = null)
        {
            int dayDifference = end.DayNumber - start.DayNumber;
            if (dayDifference == 0)
            {
                return 0;
            }
            if (dayDifference < 0)
            {
                return -BusinessDaysBetween(end, start, holidays);
            }

            int businessDays = 0;
            DateOnly current = start;
            while (current != end)
            {
                current = current.AddDays(1);
                if (IsBusinessDay(current, holidays))
                {
                    businessDays++;
                }
            }
            return businessDays;
        }

        /// <summary>
        /// Computes the age in whole years on asOfDate for a person born on birthDate. Throws if asOfDate is earlier than birthDate.
        /// </summary>
        public static int Age(DateOnly birthDate, DateOnly asOfDate)
        {
            if (asOfDate < birthDate)
            {
                throw new ArgumentOutOfRangeException(nameof(asOfDate), "The as-of date cannot be earlier than the birth date.");
            }

            int age = asOfDate.Year - birthDate.Year;
            if (asOfDate < birthDate.AddYears(age))
            {
                age--;
            }
            return age;
        }

        /// <summary>
        /// Converts instant to Unix epoch seconds.
        /// </summary>
        public static long ToUnixTimeSeconds(DateTimeOffset instant)
        {
            return instant.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Converts Unix epoch seconds to a DateTimeOffset.
        /// </summary>
        public static DateTimeOffset FromUnixTimeSeconds(long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        /// <summary>
        /// Converts instant to the time zone targetZone. Throws ArgumentNullException if targetZone is null.
        /// </summary>
        public static DateTimeOffset ConvertTime(DateTimeOffset instant, TimeZoneInfo targetZone)
        {
            if (targetZone == null)
            {
                throw new ArgumentNullException(nameof(targetZone));
            }
            return TimeZoneInfo.ConvertTime(instant, targetZone);
        }

        /// <summary>
        /// Returns the time elapsed from start to end. If crossesMidnight is true, an end earlier than start is treated as the next day.
        /// </summary>
        public static TimeSpan ElapsedTime(TimeOnly start, TimeOnly end, bool crossesMidnight = false)
        {
            if (end >= start)
            {
                return end - start;
            }
            if (!crossesMidnight)
            {
                throw new ArgumentException("The end time cannot precede the start time unless the interval crosses midnight.", nameof(end));
            }
            return end.ToTimeSpan() + TimeSpan.FromDays(1) - start.ToTimeSpan();
        }

        /// <summary>
        /// Attempts to parse text as a date using the exact format with the invariant culture. Returns false if parsing fails.
        /// </summary>
        public static bool TryParseDateExact(string text, string format, out DateOnly date)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            return DateOnly.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        /// <summary>
        /// Formats date using the given format string with the invariant culture.
        /// </summary>
        public static string FormatDate(DateOnly date, string format)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            return date.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Determines whether date is a weekday that is not in holidays.
        /// </summary>
        private static bool IsBusinessDay(DateOnly date, IReadOnlySet<DateOnly>? holidays)
        {
            return date.DayOfWeek != DayOfWeek.Saturday
                && date.DayOfWeek != DayOfWeek.Sunday
                && (holidays == null || !holidays.Contains(date));
        }
    }
}
