using System;
using System.Collections.Generic;
using System.Globalization;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class TimeAndDateTests
    {
        public static void RunAll()
        {
            Run("Calendar and ISO week helpers", TestCalendarHelpers);
            Run("Business-day and age helpers", TestBusinessDaysAndAge);
            Run("Unix timestamps and time-zone conversion", TestTimestampsAndTimeZones);
            Run("Elapsed time and invariant date text", TestElapsedTimeAndDateText);
            Run("TimeAndDate validation", TestValidation);
        }

        private static void TestCalendarHelpers()
        {
            Assert(TimeAndDate.IsLeapYear(2000), "2000 should be a leap year");
            Assert(!TimeAndDate.IsLeapYear(1900), "1900 should not be a leap year");
            Assert(TimeAndDate.DaysInMonth(2024, 2) == 29, "February 2024 should have 29 days");
            Assert(TimeAndDate.DayOfYear(new DateOnly(2024, 12, 31)) == 366, "leap-year day number");
            Assert(TimeAndDate.IsoWeek(new DateOnly(2021, 1, 1)) == (2020, 53), "ISO week-year boundary");
            Assert(TimeAndDate.IsoWeek(new DateOnly(2021, 1, 4)) == (2021, 1), "first ISO week of 2021");
            Assert(TimeAndDate.DaysBetween(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 4)) == 3, "forward day difference");
            Assert(TimeAndDate.DaysBetween(new DateOnly(2024, 1, 4), new DateOnly(2024, 1, 1)) == -3, "reverse day difference");
        }

        private static void TestBusinessDaysAndAge()
        {
            DateOnly friday = new DateOnly(2024, 1, 5);
            DateOnly mondayHoliday = new DateOnly(2024, 1, 8);
            HashSet<DateOnly> holidays = new HashSet<DateOnly> { mondayHoliday };

            Assert(TimeAndDate.AddBusinessDays(friday, 1) == new DateOnly(2024, 1, 8), "Friday plus one business day");
            Assert(TimeAndDate.AddBusinessDays(friday, 1, holidays) == new DateOnly(2024, 1, 9), "holiday should be skipped");
            Assert(TimeAndDate.AddBusinessDays(friday, -1) == new DateOnly(2024, 1, 4), "Friday minus one business day");
            Assert(TimeAndDate.BusinessDaysBetween(friday, new DateOnly(2024, 1, 10), holidays) == 2, "business days exclude start and holiday");
            Assert(TimeAndDate.BusinessDaysBetween(new DateOnly(2024, 1, 10), friday, holidays) == -2, "reverse business-day count");
            Assert(TimeAndDate.BusinessDaysBetween(friday, friday) == 0, "same-date business-day count");
            Assert(TimeAndDate.BusinessDaysBetween(friday, new DateOnly(2024, 1, 6)) == 0, "weekend endpoint count");
            Assert(TimeAndDate.BusinessDaysBetween(new DateOnly(2024, 1, 6), friday) == 0, "reverse weekend endpoint count");

            Assert(TimeAndDate.Age(new DateOnly(1990, 6, 15), new DateOnly(2024, 6, 14)) == 33, "age before birthday");
            Assert(TimeAndDate.Age(new DateOnly(1990, 6, 15), new DateOnly(2024, 6, 15)) == 34, "age on birthday");
            Assert(TimeAndDate.Age(new DateOnly(2000, 2, 29), new DateOnly(2021, 2, 28)) == 21, "leap-day birthday in a non-leap year");
        }

        private static void TestTimestampsAndTimeZones()
        {
            Assert(TimeAndDate.FromUnixTimeSeconds(0) == DateTimeOffset.UnixEpoch, "Unix epoch conversion");
            DateTimeOffset beforeEpoch = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero);
            Assert(TimeAndDate.ToUnixTimeSeconds(beforeEpoch) == -1, "negative Unix timestamp");

            TimeZoneInfo zone = CreateDaylightSavingTestZone();
            DateTimeOffset beforeTransition = TimeAndDate.ConvertTime(
                new DateTimeOffset(2024, 3, 10, 6, 30, 0, TimeSpan.Zero),
                zone);
            DateTimeOffset afterTransition = TimeAndDate.ConvertTime(
                new DateTimeOffset(2024, 3, 10, 7, 30, 0, TimeSpan.Zero),
                zone);
            Assert(beforeTransition.TimeOfDay == new TimeSpan(1, 30, 0), "pre-transition local time");
            Assert(beforeTransition.Offset == TimeSpan.FromHours(-5), "pre-transition UTC offset");
            Assert(afterTransition.TimeOfDay == new TimeSpan(3, 30, 0), "post-transition local time");
            Assert(afterTransition.Offset == TimeSpan.FromHours(-4), "post-transition UTC offset");
            DateTimeOffset firstAmbiguousHour = TimeAndDate.ConvertTime(
                new DateTimeOffset(2024, 11, 3, 5, 30, 0, TimeSpan.Zero), zone);
            DateTimeOffset secondAmbiguousHour = TimeAndDate.ConvertTime(
                new DateTimeOffset(2024, 11, 3, 6, 30, 0, TimeSpan.Zero), zone);
            Assert(firstAmbiguousHour.TimeOfDay == new TimeSpan(1, 30, 0) &&
                   secondAmbiguousHour.TimeOfDay == new TimeSpan(1, 30, 0) &&
                   firstAmbiguousHour.Offset == TimeSpan.FromHours(-4) &&
                   secondAmbiguousHour.Offset == TimeSpan.FromHours(-5),
                "Fall-back conversion should preserve the distinct offsets of an ambiguous local hour.");
            Assert(TimeAndDate.ConvertTime(DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc) == DateTimeOffset.UnixEpoch, "UTC conversion");
        }

        private static void TestElapsedTimeAndDateText()
        {
            Assert(
                TimeAndDate.ElapsedTime(new TimeOnly(9, 15), new TimeOnly(10, 45)) == TimeSpan.FromMinutes(90),
                "same-day elapsed time");
            Assert(
                TimeAndDate.ElapsedTime(new TimeOnly(23, 30), new TimeOnly(0, 30), true) == TimeSpan.FromHours(1),
                "elapsed time across midnight");

            Assert(TimeAndDate.TryParseDateExact("2024-02-29", "yyyy-MM-dd", out DateOnly parsed), "exact date parsing");
            Assert(parsed == new DateOnly(2024, 2, 29), "parsed date value");
            Assert(TimeAndDate.FormatDate(parsed, "yyyyMMdd") == "20240229", "invariant date formatting");
            Assert(!TimeAndDate.TryParseDateExact("2023-02-29", "yyyy-MM-dd", out _), "invalid calendar date should not parse");

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert(
                    TimeAndDate.TryParseDateExact("February 29, 2024", "MMMM dd, yyyy", out DateOnly invariantParsed)
                        && invariantParsed == parsed,
                    "exact parsing should use invariant culture");
                Assert(
                    TimeAndDate.FormatDate(parsed, "MMMM dd, yyyy") == "February 29, 2024",
                    "formatting should use invariant culture");
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => TimeAndDate.IsLeapYear(0), "invalid leap-year argument");
            AssertThrows<ArgumentOutOfRangeException>(() => TimeAndDate.DaysInMonth(2024, 13), "invalid month");
            AssertThrows<ArgumentOutOfRangeException>(
                () => TimeAndDate.Age(new DateOnly(2024, 1, 2), new DateOnly(2024, 1, 1)),
                "as-of date before birth");
            AssertThrows<ArgumentOutOfRangeException>(
                () => TimeAndDate.AddBusinessDays(DateOnly.MaxValue, 1),
                "date arithmetic overflow");
            AssertThrows<ArgumentOutOfRangeException>(
                () => TimeAndDate.AddBusinessDays(DateOnly.MinValue, -1),
                "date arithmetic underflow");
            AssertThrows<ArgumentException>(
                () => TimeAndDate.ElapsedTime(new TimeOnly(10, 0), new TimeOnly(9, 0)),
                "end time before start time");
            AssertThrows<ArgumentNullException>(
                () => TimeAndDate.ConvertTime(DateTimeOffset.UnixEpoch, null!),
                "null target time zone");
            AssertThrows<ArgumentNullException>(
                () => TimeAndDate.TryParseDateExact(null!, "yyyy-MM-dd", out _),
                "null date text");
            AssertThrows<ArgumentNullException>(
                () => TimeAndDate.TryParseDateExact("2024-01-01", null!, out _),
                "null date format");
            AssertThrows<ArgumentNullException>(
                () => TimeAndDate.FormatDate(new DateOnly(2024, 1, 1), null!),
                "null output format");
        }

        private static TimeZoneInfo CreateDaylightSavingTestZone()
        {
            TimeZoneInfo.TransitionTime daylightStart = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                new DateTime(1, 1, 1, 2, 0, 0),
                3,
                2,
                DayOfWeek.Sunday);
            TimeZoneInfo.TransitionTime daylightEnd = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                new DateTime(1, 1, 1, 2, 0, 0),
                11,
                1,
                DayOfWeek.Sunday);
            TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2024, 1, 1),
                new DateTime(2024, 12, 31),
                TimeSpan.FromHours(1),
                daylightStart,
                daylightEnd);
            return TimeZoneInfo.CreateCustomTimeZone(
                "OPEN-LRD-Test-DST",
                TimeSpan.FromHours(-5),
                "OPEN-LRD DST Test Zone",
                "OPEN-LRD Standard Time",
                "OPEN-LRD Daylight Time",
                new[] { rule });
        }
    }
}
