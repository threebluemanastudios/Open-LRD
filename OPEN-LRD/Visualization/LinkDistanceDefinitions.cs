using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Visualization
{
    /// <summary>Resolves and validates semantic rest distances for graph link classes and types.</summary>
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

        /// <summary>Returns the configured rest distance for the specified link.</summary>
        public static double GetDistance(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return GetDistance(link.Classification, link.ForwardLink);
        }

        /// <summary>Returns the rest distance for a link class and optional forward link type override.</summary>
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

        /// <summary>Returns the default rest distance for a link class.</summary>
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

        /// <summary>Attempts to retrieve an explicit rest-distance override for a forward link type.</summary>
        public static bool TryGetForwardOverride(
            ForwardLinkTypes forwardLinkType,
            out double distance)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);

            lock (sync)
                return forwardOverrides.TryGetValue(forwardLinkType.Id, out distance);
        }

        /// <summary>Sets the default rest distance for a link class.</summary>
        public static void SetClassDefault(LinkClass linkClass, double distance)
        {
            ArgumentNullException.ThrowIfNull(linkClass);
            ValidateDistance(distance, nameof(distance));

            lock (sync)
                classDefaults[linkClass.Id] = distance;
        }

        /// <summary>Attempts to set the default rest distance for a link class when valid.</summary>
        public static bool TrySetClassDefault(LinkClass linkClass, double distance)
        {
            if (linkClass is null || !IsValidDistance(distance))
                return false;

            lock (sync)
                classDefaults[linkClass.Id] = distance;

            return true;
        }

        /// <summary>Sets a rest-distance override for a forward link type.</summary>
        public static void SetForwardOverride(ForwardLinkTypes forwardLinkType, double distance)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);
            ValidateDistance(distance, nameof(distance));

            lock (sync)
                forwardOverrides[forwardLinkType.Id] = distance;
        }

        /// <summary>Attempts to set a rest-distance override for a forward link type when valid.</summary>
        public static bool TrySetForwardOverride(ForwardLinkTypes forwardLinkType, double distance)
        {
            if (forwardLinkType is null || !IsValidDistance(distance))
                return false;

            lock (sync)
                forwardOverrides[forwardLinkType.Id] = distance;

            return true;
        }

        /// <summary>Removes any rest-distance override registered for a forward link type.</summary>
        public static bool RemoveForwardOverride(ForwardLinkTypes forwardLinkType)
        {
            ArgumentNullException.ThrowIfNull(forwardLinkType);

            lock (sync)
                return forwardOverrides.Remove(forwardLinkType.Id);
        }

        /// <summary>Returns a snapshot of the configured link-class default distances.</summary>
        public static IReadOnlyDictionary<int, double> GetClassDefaults()
        {
            lock (sync)
                return new Dictionary<int, double>(classDefaults);
        }

        /// <summary>Returns a snapshot of the configured forward-link distance overrides.</summary>
        public static IReadOnlyDictionary<int, double> GetForwardOverrides()
        {
            lock (sync)
                return new Dictionary<int, double>(forwardOverrides);
        }

        /// <summary>Determines whether a link rest distance is finite and positive.</summary>
        public static bool IsValidDistance(double distance)
        {
            return double.IsFinite(distance) && distance > 0d;
        }

        /// <summary>Throws when a link rest distance is not finite and greater than zero.</summary>
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
