namespace OPEN_LRD.Nodes.Links
{
    public class ForwardLinkTypes
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<int, ForwardLinkTypes> _registry = new();

        public static readonly ForwardLinkTypes basic = Register(0, nameof(basic));

        public int Id { get; }
        public string Name { get; }

        private ForwardLinkTypes(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static ForwardLinkTypes Register(int id, string name)
        {
            ForwardLinkTypes value = new ForwardLinkTypes(id, name);

            lock (_sync)
                _registry[id] = value;

            return value;
        }

        public static IEnumerable<ForwardLinkTypes> GetAll()
        {
            lock (_sync)
                return _registry.Values.ToArray();
        }

        public static ForwardLinkTypes FromId(int id)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out ForwardLinkTypes? linkType) ? linkType : basic;
        }

        public static bool TryFromId(int id, out ForwardLinkTypes linkType)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out linkType!);
        }

        public static ForwardLinkTypes FromName(string name)
        {
            lock (_sync)
            {
                ForwardLinkTypes? match = _registry.Values.FirstOrDefault(
                    linkType => string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));

                return match ?? basic;
            }
        }

        public override string ToString() => Name;
    }
}
