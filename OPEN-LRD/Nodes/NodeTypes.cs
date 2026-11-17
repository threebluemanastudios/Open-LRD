namespace OPEN_LRD.Nodes
{
    /// <summary>Describes a registered semantic node category with a stable numeric identifier.</summary>
    public class NodeTypes
    {

        private static readonly Dictionary<int, NodeTypes> _registry = new();

        /// <summary>Gets the fallback empty node type.</summary>
        public static readonly NodeTypes Empties = Register(1, "Emtpy");
        /// <summary>Gets the generic basic node type.</summary>
        public static readonly NodeTypes Basics = Register(2, "Basic");
        /// <summary>Gets the dictionary node type.</summary>
        public static readonly NodeTypes Dictionaries = Register(3, "Dictionary");
        /// <summary>Gets the object node type.</summary>
        public static readonly NodeTypes Objects = Register(4, "Object");
        /// <summary>Gets the concept node type.</summary>
        public static readonly NodeTypes Concepts = Register(5, "Concept");
        /// <summary>Gets the entity node type.</summary>
        public static readonly NodeTypes Entities = Register(6, "Entity");
        /// <summary>Gets the name node type.</summary>
        public static readonly NodeTypes Names = Register(7, "Name");
        /// <summary>Gets the source node type.</summary>
        public static readonly NodeTypes Sources = Register(8, "Source");
        /// <summary>Gets the location node type.</summary>
        public static readonly NodeTypes Locations = Register(9, "Location");
        /// <summary>Gets the event node type.</summary>
        public static readonly NodeTypes Events = Register(10, "Event");
        /// <summary>Gets the raw-data node type.</summary>
        public static readonly NodeTypes RawData = Register(11, "Raw Data");

        /// <summary>Gets the stable numeric identifier of this type.</summary>
        public int Id { get; }
        /// <summary>Gets the canonical display name of this type.</summary>
        public string Name { get; }

        /// <summary>Initializes a node type descriptor with an identifier and display name.</summary>
        private NodeTypes(int id, string name)
        {
            Id = id;
            Name = name;
        }

        /// <summary>Registers or replaces a node type by stable identifier.</summary>
        /// <param name="id">The stable numeric identifier.</param>
        /// <param name="name">The canonical node type name.</param>
        /// <returns>The registered node type.</returns>
        public static NodeTypes Register(int id, string name)
        {
            var status = new NodeTypes(id, name);
            _registry[id] = status;
            return status;
        }

        /// <summary>Returns the currently registered node types.</summary>
        public static IEnumerable<NodeTypes> GetAll() => _registry.Values;

        /// <summary>Resolves an identifier, using the empty type when it is unknown.</summary>
        /// <param name="id">The type identifier.</param>
        /// <returns>The matching node type or <see cref="Empties"/>.</returns>
        public static NodeTypes FromId(int id) => _registry.TryGetValue(id, out var nodeType) ? nodeType : Empties;

        /// <summary>Returns the canonical display name.</summary>
        public override string ToString() => Name;

        /// <summary>Resolves a registered node type name without regard to case.</summary>
        /// <param name="name">The name to resolve.</param>
        /// <returns>The matching node type or <see cref="Empties"/>.</returns>
        public static NodeTypes FromName(string name)
        {
            NodeTypes? match = _registry.Values.FirstOrDefault(
                nodeType => string.Equals(nodeType.Name, name, StringComparison.OrdinalIgnoreCase));

            return match ?? Empties;
        }

    }
}
