using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Visualization
{
    internal static class LinkDistanceDefinitions
    {
        public const double FallbackDistance = 1.0;

        private static readonly object sync = new();
        private static readonly Dictionary<int, double> classDefaults = new Dictionary<int, double>
        {
            [LinkClass.identities.Id] = 0.50,
            [LinkClass.map_locations.Id] = 0.75,
            [LinkClass.basic.Id] = 1.00,
            [LinkClass.events.Id] = 1.00,
            [LinkClass.actions.Id] = 1.25,
            [LinkClass.temporal.Id] = 1.25,
            [LinkClass.spatial.Id] = 1.50,
            [LinkClass.sources.Id] = 1.75,
            [LinkClass.empty.Id] = 2.00
        };
        private static readonly Dictionary<int, double> forwardOverrides = new();

        public static double GetDistance(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return GetDistance(link.Classification, link.ForwardLink);
        }

        public static double GetDistance(
            LinkClass linkClass,
            ForwardLinkTypes? forwardLinkType = null)
        {
            ArgumentNullException.ThrowIfNull(linkClass);

            lock (sync)
            {
                if (forwardLinkType is not null
                    && forwardOverrides.TryGetValue(forwardLinkType.Id, out double forwardDistance))
                {
                    return forwardDistance;
                }

                return classDefaults.TryGetValue(linkClass.Id, out double classDistance)
                    ? classDistance
                    : FallbackDistance;
            }
        }

        public static double GetClassDefault(LinkClass linkClass)
        {
            ArgumentNullException.ThrowIfNull(linkClass);

            lock (sync)
            {
                return classDefaults.TryGetValue(linkClass.Id, out double distance)
                    ? distance
                    : FallbackDistance;
            }
        }

        public static bool TryGetForwardOverride(
            ForwardLinkTypes forwardLinkType,
            out double distance)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);

            lock (sync)
                return forwardOverrides.TryGetValue(forwardLinkType.Id, out distance);
        }

        public static void SetClassDefault(LinkClass linkClass, double distance)
        {
            ArgumentNullException.ThrowIfNull(linkClass);
            ValidateDistance(distance, nameof(distance));

            lock (sync)
                classDefaults[linkClass.Id] = distance;
        }

        public static bool TrySetClassDefault(LinkClass linkClass, double distance)
        {
            if (linkClass is null || !IsValidDistance(distance))
                return false;

            lock (sync)
                classDefaults[linkClass.Id] = distance;

            return true;
        }

        public static void SetForwardOverride(ForwardLinkTypes forwardLinkType, double distance)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);
            ValidateDistance(distance, nameof(distance));

            lock (sync)
                forwardOverrides[forwardLinkType.Id] = distance;
        }

        public static bool TrySetForwardOverride(ForwardLinkTypes forwardLinkType, double distance)
        {
            if (forwardLinkType is null || !IsValidDistance(distance))
                return false;

            lock (sync)
                forwardOverrides[forwardLinkType.Id] = distance;

            return true;
        }

        public static bool RemoveForwardOverride(ForwardLinkTypes forwardLinkType)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);

            lock (sync)
                return forwardOverrides.Remove(forwardLinkType.Id);
        }

        public static IReadOnlyDictionary<int, double> GetClassDefaults()
        {
            lock (sync)
                return new Dictionary<int, double>(classDefaults);
        }

        public static IReadOnlyDictionary<int, double> GetForwardOverrides()
        {
            lock (sync)
                return new Dictionary<int, double>(forwardOverrides);
        }

        public static bool IsValidDistance(double distance)
        {
            return double.IsFinite(distance) && distance > 0d;
        }

        private static void ValidateDistance(double distance, string parameterName)
        {
            if (!IsValidDistance(distance))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    distance,
                    "Link distances must be finite and greater than zero.");
            }
        }
    }
}
