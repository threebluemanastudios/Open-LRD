namespace OPEN_LRD.Nodes
{
    public class NodeTypes
    {

        /*
         * public enum NodeTypes
    {
        Basic,      // string, int, char, float, etc.
        Dictionary, // Word definitions
        Object,     // physical items - non-individual / non-specific
        Concept,    // non-physical ideas
        Entity,     // specific person, specific company, specific building, specific pet, specific object etc.
        Name,       // proper names
        Source,     // base code, multi-media, web, newspaper, magazine, dictionary, encyclopedia, book (fiction/non-fiction), library, research paper, person,
        Location,   // coordinates, address, geo marker,
        Event,      // time and location specific, global, galactic, universal, scope and duration?
        RawData,    // images, sounds, videos, documents, etc.

    }
         * 
         */

        private static readonly Dictionary<int, NodeTypes> _registry = new();

        public static readonly NodeTypes Empties = Register(1, "Emtpy");
        public static readonly NodeTypes Basics = Register(2, "Basic");
        public static readonly NodeTypes Dictionaries = Register(3, "Dictionary");
        public static readonly NodeTypes Objects = Register(4, "Object");
        public static readonly NodeTypes Concepts = Register(5, "Concept");
        public static readonly NodeTypes Entities = Register(6, "Entity");
        public static readonly NodeTypes Names = Register(7, "Name");
        public static readonly NodeTypes Sources = Register(8, "Source");
        public static readonly NodeTypes Locations = Register(9, "Location");
        public static readonly NodeTypes Events = Register(10, "Event");
        public static readonly NodeTypes RawData = Register(11, "Raw Data");

        public int Id { get; }
        public string Name { get; }

        private NodeTypes(int id, string name)
        {
            Id = id;
            Name = name;
        }

        // Register predefined or runtime-added statuses
        public static NodeTypes Register(int id, string name)
        {
            var status = new NodeTypes(id, name);
            _registry[id] = status;
            return status;
        }

        public static IEnumerable<NodeTypes> GetAll() => _registry.Values;

        public static NodeTypes FromId(int id) => _registry.TryGetValue(id, out var nodeType) ? nodeType : Empties;

        public override string ToString() => Name;

        public static NodeTypes FromName(string name)
        {
            NodeTypes? match = _registry.Values.FirstOrDefault(
                nodeType => string.Equals(nodeType.Name, name, StringComparison.OrdinalIgnoreCase));

            return match ?? Empties;
        }

    }
}
