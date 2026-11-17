namespace OPEN_LRD.Nodes.Links
{
    /// <summary>Represents a registered forward link type with a stable numeric identifier.</summary>
    public class ForwardLinkTypes
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<int, ForwardLinkTypes> _registry = new();

        /// <summary>Gets the default forward link type.</summary>
        public static readonly ForwardLinkTypes basic = Register(0, nameof(basic));

        /// <summary>Gets the stable identifier of this link type.</summary>
        public int Id { get; }
        /// <summary>Gets the canonical name of this link type.</summary>
        public string Name { get; }

        private ForwardLinkTypes(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>Registers or replaces a forward link type by identifier.</summary>
        /// <param name="id">The stable numeric identifier.</param>
        /// <param name="name">The canonical type name.</param>
        /// <returns>The registered link type.</returns>
        public static ForwardLinkTypes Register(int id, string name)
        {
            ForwardLinkTypes value = new ForwardLinkTypes(id, name);

            lock (_sync)
                _registry[id] = value;

            return value;
        }

        /// <summary>Returns a snapshot of all registered forward link types.</summary>
        public static IEnumerable<ForwardLinkTypes> GetAll()
        {
            lock (_sync)
                return _registry.Values.ToArray();
        }

        /// <summary>Resolves an identifier, falling back to the basic type when it is unknown.</summary>
        /// <param name="id">The registered identifier to resolve.</param>
        /// <returns>The matching type or the basic type.</returns>
        public static ForwardLinkTypes FromId(int id)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out ForwardLinkTypes? linkType) ? linkType : basic;
        }

        /// <summary>Attempts to resolve an identifier without applying a fallback.</summary>
        /// <param name="id">The identifier to resolve.</param>
        /// <param name="linkType">Receives the matching type when it is registered.</param>
        /// <returns><see langword="true"/> when the identifier is registered.</returns>
        public static bool TryFromId(int id, out ForwardLinkTypes linkType)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out linkType!);
        }

        /// <summary>Resolves a registered type name without regard to case.</summary>
        /// <param name="name">The name to resolve.</param>
        /// <returns>The matching type or the basic type.</returns>
        public static ForwardLinkTypes FromName(string name)
        {
            lock (_sync)
            {
                ForwardLinkTypes? match = _registry.Values.FirstOrDefault(
                    linkType => string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));

                return match ?? basic;
            }
        }

        /// <summary>Returns the canonical link type name.</summary>
        public override string ToString() => Name;
    }
}
