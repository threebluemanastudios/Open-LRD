namespace OPEN_LRD.Nodes.Links
{
    public class InverseLinkTypes
    {
        private static readonly object _sync = new();
        private static readonly Dictionary<int, InverseLinkTypes> _registry = new();

        public static readonly InverseLinkTypes basic = Register(0, nameof(basic));

        public int Id { get; }
        public string Name { get; }

        private InverseLinkTypes(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public static InverseLinkTypes Register(int id, string name)
        {
            InverseLinkTypes value = new InverseLinkTypes(id, name);

            lock (_sync)
                _registry[id] = value;

            return value;
        }

        public static IEnumerable<InverseLinkTypes> GetAll()
        {
            lock (_sync)
                return _registry.Values.ToArray();
        }

        public static InverseLinkTypes FromId(int id)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out InverseLinkTypes? linkType) ? linkType : basic;
        }

        public static bool TryFromId(int id, out InverseLinkTypes linkType)
        {
            lock (_sync)
                return _registry.TryGetValue(id, out linkType!);
        }

        public static InverseLinkTypes FromName(string name)
        {
            lock (_sync)
            {
                InverseLinkTypes? match = _registry.Values.FirstOrDefault(
                    linkType => string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));

                return match ?? basic;
            }
        }

        public override string ToString() => Name;
    }
}
