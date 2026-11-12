namespace OPEN_LRD.Nodes.Links
{
    public class LinkClass
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<int, LinkClass> _registry = new();

        public static readonly LinkClass empty = Register(0, nameof(empty));
        public static readonly LinkClass basic = Register(1, nameof(basic));
        public static readonly LinkClass map_locations = Register(2, nameof(map_locations));
        public static readonly LinkClass events = Register(3, nameof(events));
        public static readonly LinkClass actions = Register(4, nameof(actions));
        public static readonly LinkClass spatial = Register(5, nameof(spatial));
        public static readonly LinkClass temporal = Register(6, nameof(temporal));
        public static readonly LinkClass sources = Register(7, nameof(sources));
        public static readonly LinkClass identities = Register(8, nameof(identities));

        public int Id { get; }
        public string Name { get; }

        private LinkClass(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static LinkClass Register(int id, string name)
        {
            LinkClass value = new LinkClass(id, name);

            lock (_sync)
                _registry[id] = value;

            return value;
        }

        public static IEnumerable<LinkClass> GetAll()
        {
            lock (_sync)
                return _registry.Values.ToArray();
        }

        public static LinkClass FromId(int id)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out LinkClass? linkClass) ? linkClass : empty;
        }

        public static bool TryFromId(int id, out LinkClass linkClass)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out linkClass!);
        }

        public static LinkClass FromName(string name)
        {
            lock (_sync)
            {
                LinkClass? match = _registry.Values.FirstOrDefault(
                    linkClass => string.Equals(linkClass.Name, name, StringComparison.OrdinalIgnoreCase));

                return match ?? empty;
            }
        }

        public override string ToString() => Name;
    }
}
