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
        /// Computes the day number of date within its year.
        /// </summary>

        public static int DaysInMonth(int year, int month)
        {
            return DateTime.DaysInMonth(year, month);
        }

        public static int DayOfYear(DateOnly date)
        {
            return date.DayOfYear;
        }

        public static (int WeekYear, int WeekNumber) IsoWeek(DateOnly date)
        {
            DateTime value = date.ToDateTime(TimeOnly.MinValue);
            return (ISOWeek.GetYear(value), ISOWeek.GetWeekOfYear(value));
        }

        public static int DaysBetween(DateOnly start, DateOnly end)
        {
            return end.DayNumber - start.DayNumber;
        }

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
        /// Converts instant to Unix epoch seconds.
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

        public static long ToUnixTimeSeconds(DateTimeOffset instant)
        {
            return instant.ToUnixTimeSeconds();
        }

        public static DateTimeOffset FromUnixTimeSeconds(long seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        public static DateTimeOffset ConvertTime(DateTimeOffset instant, TimeZoneInfo targetZone)
        {
            if (targetZone == null)
            {
                throw new ArgumentNullException(nameof(targetZone));
            }
            return TimeZoneInfo.ConvertTime(instant, targetZone);
        }

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

        public static string FormatDate(DateOnly date, string format)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            return date.ToString(format, CultureInfo.InvariantCulture);
        }

        private static bool IsBusinessDay(DateOnly date, IReadOnlySet<DateOnly>? holidays)
        {
            return date.DayOfWeek != DayOfWeek.Saturday
                && date.DayOfWeek != DayOfWeek.Sunday
                && (holidays == null || !holidays.Contains(date));
        }
    }
}
