using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Ontology
{
    // Additive node-type and link-class registrations used by Module 4. IDs
    // continue after the Module 1-3 ranges so existing values are untouched.
    internal static class OntologyTypes
    {
        public static readonly NodeTypes Categories = NodeTypes.Register(12, "Category");
        public static readonly NodeTypes Lexemes = NodeTypes.Register(13, "Lexeme");
        public static readonly NodeTypes Senses = NodeTypes.Register(14, "Sense");
        public static readonly NodeTypes Properties = NodeTypes.Register(15, "Property");
        public static readonly NodeTypes Times = NodeTypes.Register(16, "Time");
        public static readonly NodeTypes Fields = NodeTypes.Register(17, "Field");
        public static readonly NodeTypes Taxa = NodeTypes.Register(18, "Taxon");
        public static readonly NodeTypes Realities = NodeTypes.Register(19, "Reality");
        public static readonly NodeTypes Relations = NodeTypes.Register(20, "Relation");

        public static readonly LinkClass ontology = LinkClass.Register(9, nameof(ontology));
        public static readonly LinkClass lexicon = LinkClass.Register(10, nameof(lexicon));
        public static readonly LinkClass taxonomy = LinkClass.Register(11, nameof(taxonomy));
        public static readonly LinkClass properties = LinkClass.Register(12, nameof(properties));

        // Forces the static initializers above to run. Callers use this as an
        // explicit, deterministic registration entry point.
        public static void EnsureRegistered()
        {
            _ = Categories;
        }
    }
}
