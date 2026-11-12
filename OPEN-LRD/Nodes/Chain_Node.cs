using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    internal class Chain_Node : Node
    {
        Link? linkNext, linkPrevious;

        public Chain_Node() : base()
        {
        }

        public Chain_Node(Guid ID, int TTL)
            : base(ID, NodeTypes.Basics, TTL)
        {
        }

        public Chain_Node(Guid ID, int TYPE, int TTL)
            : base(ID, NodeTypes.FromId(TYPE), TTL)
        {
        }

        public Chain_Node(Guid ID, NodeTypes TYPE, int TTL)
            : base(ID, TYPE, TTL)
        {
        }

        public Chain_Node(Guid ID, int TYPE, int TTL, Link SOURCE, Link DESTINATION)
            : base(ID, NodeTypes.FromId(TYPE), TTL)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        public Chain_Node(Guid ID, NodeTypes TYPE, int TTL, Link SOURCE, Link DESTINATION)
            : base(ID, TYPE, TTL)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        public Chain_Node(Guid ID, Link SOURCE, Link DESTINATION)
            : base(ID, NodeTypes.Basics, DefaultTimeToLive)
        {
            linkPrevious = SOURCE;
            linkNext = DESTINATION;
        }

        public Link? Get_LinkNext()
        {
            return linkNext;
        }

        public void Set_LinkNext(Link? LINK)
        {
            linkNext = LINK;
        }

        public Link? Get_LinkPrevious()
        {
            return linkPrevious;
        }

        public void Set_LinkPrevious(Link? LINK)
        {
            linkPrevious = LINK;
        }

    }
}
