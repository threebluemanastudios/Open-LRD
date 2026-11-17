namespace OPEN_LRD.Nodes
{
    /// <summary>Describes an event's temporal position relative to a reference time.</summary>
    public enum EventTemporalStatus
    {
        /// <summary>The event has no usable time window.</summary>
        Unknown,
        /// <summary>The event ended before the reference time.</summary>
        Past,
        /// <summary>The event overlaps the reference time.</summary>
        Active,
        /// <summary>The event begins after the reference time.</summary>
        Future
    }

    /// <summary>Contains calculated start, end, duration, and display information for an event window.</summary>
    /// <param name="ReferenceTime">The instant used to calculate the variances.</param>
    /// <param name="StartVariance">The signed difference between reference time and event start, when present.</param>
    /// <param name="EndVariance">The signed difference between reference time and event end, when present.</param>
    /// <param name="Duration">The event duration when both endpoints are present.</param>
    /// <param name="Status">The event's temporal classification.</param>
    /// <param name="FriendlyVariance">A concise human-readable description of the time window.</param>
    public sealed record EventTimeVariance(
        DateTimeOffset ReferenceTime,
        TimeSpan? StartVariance,
        TimeSpan? EndVariance,
        TimeSpan? Duration,
        EventTemporalStatus Status,
        string FriendlyVariance);

    /// <summary>Calculates event status, endpoint variance, duration, and friendly relative-time text.</summary>
    internal static class EventTimeVarianceCalculator
    {
        public static EventTimeVariance Calculate(
            Web_Node eventNode,
            DateTimeOffset? referenceTime = null)
        {
            ArgumentNullException.ThrowIfNull(eventNode);
            DateTimeOffset now = referenceTime ?? DateTimeOffset.UtcNow;

            if (!EventNodeHelper.TryGetWindow(eventNode, out DateTimeOffset? start, out DateTimeOffset? end))
            {
                return new EventTimeVariance(
                    now,
                    null,
                    null,
                    null,
                    EventTemporalStatus.Unknown,
                    "time unknown");
            }

            TimeSpan? startVariance = start.HasValue ? now - start.Value : null;
            TimeSpan? endVariance = end.HasValue ? now - end.Value : null;
            TimeSpan? duration = start.HasValue && end.HasValue ? end.Value - start.Value : null;
            EventTemporalStatus status;
            string friendly;

            if (start.HasValue && start.Value > now)
            {
                status = EventTemporalStatus.Future;
                friendly = "starts " + FormatDistance(start.Value - now) + " from now";
            }
            else if (end.HasValue && end.Value < now)
            {
                status = EventTemporalStatus.Past;
                friendly = "ended " + FormatDistance(now - end.Value) + " ago";
            }
            else if (start.HasValue && end.HasValue)
            {
                status = EventTemporalStatus.Active;
                friendly = "ongoing (started " + FormatDistance(now - start.Value) + " ago)";
            }
            else if (start.HasValue)
            {
                status = EventTemporalStatus.Active;
                friendly = "started " + FormatDistance(now - start.Value) + " ago";
            }
            else if (end.HasValue)
            {
                status = end.Value >= now ? EventTemporalStatus.Future : EventTemporalStatus.Past;
                friendly = end.Value >= now
                    ? "ends in " + FormatDistance(end.Value - now)
                    : "ended " + FormatDistance(now - end.Value) + " ago";
            }
            else
            {
                status = EventTemporalStatus.Unknown;
                friendly = "time unknown";
            }

            return new EventTimeVariance(now, startVariance, endVariance, duration, status, friendly);
        }

        private static string FormatDistance(TimeSpan distance)
        {
            if (distance < TimeSpan.Zero)
                distance = distance.Negate();

            if (distance.TotalSeconds < 10)
                return "just now";
            if (distance.TotalMinutes < 1)
                return FormatCount((int)Math.Round(distance.TotalSeconds), "second", "seconds");
            if (distance.TotalHours < 1)
                return FormatCount((int)Math.Round(distance.TotalMinutes), "minute", "minutes");
            if (distance.TotalDays < 1)
                return FormatCount((int)Math.Round(distance.TotalHours), "hour", "hours");
            if (distance.TotalDays < 30)
                return FormatCount((int)Math.Round(distance.TotalDays), "day", "days");
            if (distance.TotalDays < 365)
                return FormatCount((int)Math.Round(distance.TotalDays / 30.0), "month", "months");

            return FormatCount((int)Math.Round(distance.TotalDays / 365.0), "year", "years");
        }

        private static string FormatCount(int count, string singular, string plural)
        {
            int safeCount = Math.Max(1, count);
            return safeCount + " " + (safeCount == 1 ? singular : plural);
        }
    }
}
