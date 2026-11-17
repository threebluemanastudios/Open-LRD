namespace OPEN_LRD.Nodes.Links
{
    /// <summary>Classifies graph links into registered semantic groups.</summary>
    public class LinkClass
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<int, LinkClass> _registry = new();

        /// <summary>Gets the empty link class.</summary>
        public static readonly LinkClass empty = Register(0, nameof(empty));
        /// <summary>Gets the general-purpose link class.</summary>
        public static readonly LinkClass basic = Register(1, nameof(basic));
        /// <summary>Gets the location-mapping link class.</summary>
        public static readonly LinkClass map_locations = Register(2, nameof(map_locations));
        /// <summary>Gets the event link class.</summary>
        public static readonly LinkClass events = Register(3, nameof(events));
        /// <summary>Gets the action link class.</summary>
        public static readonly LinkClass actions = Register(4, nameof(actions));
        /// <summary>Gets the spatial link class.</summary>
        public static readonly LinkClass spatial = Register(5, nameof(spatial));
        /// <summary>Gets the temporal link class.</summary>
        public static readonly LinkClass temporal = Register(6, nameof(temporal));
        /// <summary>Gets the source/provenance link class.</summary>
        public static readonly LinkClass sources = Register(7, nameof(sources));
        /// <summary>Gets the identity link class.</summary>
        public static readonly LinkClass identities = Register(8, nameof(identities));

        /// <summary>Gets the stable numeric identifier of this class.</summary>
        public int Id { get; }
        /// <summary>Gets the canonical name of this class.</summary>
        public string Name { get; }

        private LinkClass(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>Registers or replaces a link class by stable identifier.</summary>
        /// <param name="id">The stable numeric identifier.</param>
        /// <param name="name">The canonical class name.</param>
        /// <returns>The registered link class.</returns>
        public static LinkClass Register(int id, string name)
        {
            LinkClass value = new LinkClass(id, name);

            lock (_sync)
                _registry[id] = value;

            return value;
        }

        /// <summary>Returns a snapshot of the registered link classes.</summary>
        public static IEnumerable<LinkClass> GetAll()
        {
            lock (_sync)
                return _registry.Values.ToArray();
        }

        /// <summary>Resolves an identifier, using the empty class when it is unknown.</summary>
        /// <param name="id">The class identifier.</param>
        /// <returns>The matching class or <see cref="empty"/>.</returns>
        public static LinkClass FromId(int id)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out LinkClass? linkClass) ? linkClass : empty;
        }

        /// <summary>Attempts to resolve an identifier without applying a fallback.</summary>
        /// <param name="id">The class identifier.</param>
        /// <param name="linkClass">Receives the matching class when registered.</param>
        /// <returns><see langword="true"/> when the identifier is registered.</returns>
        public static bool TryFromId(int id, out LinkClass linkClass)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out linkClass!);
        }

        /// <summary>Resolves a registered class name without regard to case.</summary>
        /// <param name="name">The name to resolve.</param>
        /// <returns>The matching class or <see cref="empty"/>.</returns>
        public static LinkClass FromName(string name)
        {
            lock (_sync)
            {
                LinkClass? match = _registry.Values.FirstOrDefault(
                    linkClass => string.Equals(linkClass.Name, name, StringComparison.OrdinalIgnoreCase));

                return match ?? empty;
            }
        }

        /// <summary>Returns the canonical class name.</summary>
        public override string ToString() => Name;
    }
}
