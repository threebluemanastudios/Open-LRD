using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    /// <summary>Represents a node in an ordered query or response chain.</summary>
    internal class Chain_Node : Node
    {
        Link? linkNext, linkPrevious;

        /// <summary>Creates a temporary basic chain node with a generated identifier and default TTL.</summary>
        public Chain_Node() : base()
        {
        }

        /// <summary>Creates a temporary basic chain node with the specified identifier and TTL.</summary>
        public Chain_Node(Guid ID, int TTL)
            : base(ID, NodeTypes.Basics, TTL)
        {
        }

        /// <summary>Creates a temporary chain node with the specified identifier, numeric type, and TTL.</summary>
        public Chain_Node(Guid ID, int TYPE, int TTL)
            : base(ID, NodeTypes.FromId(TYPE), TTL)
        {
        }

        /// <summary>Creates a temporary chain node with the specified identifier, type, and TTL.</summary>
        public Chain_Node(Guid ID, NodeTypes TYPE, int TTL)
            : base(ID, TYPE, TTL)
        {
        }

        /// <summary>Creates a chain node with the specified identifier, type, lifetime, and TTL.</summary>
        public Chain_Node(Guid ID, NodeTypes TYPE, NodeLifetime lifetime, int TTL)
            : base(ID, TYPE, lifetime, TTL)
        {
        }

        /// <summary>Creates a temporary chain node with adjacent links and a numeric type identifier.</summary>
        public Chain_Node(Guid ID, int TYPE, int TTL, Link SOURCE, Link DESTINATION)
            : base(ID, NodeTypes.FromId(TYPE), TTL)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        /// <summary>Creates a temporary chain node with adjacent links and an explicit node type.</summary>
        public Chain_Node(Guid ID, NodeTypes TYPE, int TTL, Link SOURCE, Link DESTINATION)
            : base(ID, TYPE, TTL)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        /// <summary>Creates a temporary basic chain node with adjacent links and the default TTL.</summary>
        public Chain_Node(Guid ID, Link SOURCE, Link DESTINATION)
            : base(ID, NodeTypes.Basics, DefaultTimeToLive)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        /// <summary>Returns the link to the next node in the chain.</summary>
        public Link? Get_LinkNext()
        {
            return linkNext;
        }

        /// <summary>Sets the link to the next node in the chain.</summary>
        public void Set_LinkNext(Link? LINK)
        {
            linkNext = LINK;
        }

        /// <summary>Returns the link to the previous node in the chain.</summary>
        public Link? Get_LinkPrevious()
        {
            return linkPrevious;
        }

        /// <summary>Sets the link to the previous node in the chain.</summary>
        public void Set_LinkPrevious(Link? LINK)
        {
            linkPrevious = LINK;
        }

    }
}
