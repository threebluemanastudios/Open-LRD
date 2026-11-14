using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    internal class Web_Node : Node
    {
        private const int DefaultWebNodeTimeToLive = 32760;
        private readonly List<Link> referenceLinks = new List<Link>();

        public Web_Node()
            : base(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        public Web_Node(Guid ID)
            : base(ID, NodeTypes.Basics, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        public Web_Node(NodeTypes TYPE, Guid ID)
            : base(ID, TYPE, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        public Web_Node(Guid ID, int TTL)
            : base(ID, NodeTypes.Basics, NodeLifetime.Temporary, TTL)
        {
        }

        public Web_Node(Guid ID, int TYPE, int TTL)
            : base(ID, NodeTypes.FromId(TYPE), NodeLifetime.Temporary, TTL)
        {
        }

        public Web_Node(Guid ID, NodeTypes TYPE, int TTL)
            : base(ID, TYPE, NodeLifetime.Temporary, TTL)
        {
        }

        public Web_Node(Guid ID, NodeTypes TYPE, NodeLifetime lifetime, int TTL)
            : base(ID, TYPE, lifetime, TTL)
        {
        }

        public void Add_Link(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);

            Link? existing = referenceLinks.FirstOrDefault(candidate => candidate.HasSameIdentity(link));
            if (existing is null)
            {
                referenceLinks.Add(link);
            }
            else
            {
                existing.AddOrigin(link.Origins);
                if (link.IsActive)
                    existing.Reactivate();
            }
        }

        public void AddLink(Link link)
        {
            Add_Link(link);
        }

        public void Add_LinkList(IEnumerable<Link> links)
        {
            ArgumentNullException.ThrowIfNull(links);

            foreach (Link link in links)
                Add_Link(link);
        }

        public void AddLinkList(IEnumerable<Link> links)
        {
            Add_LinkList(links);
        }

        public bool Remove_Link(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return referenceLinks.Remove(link);
        }

        public bool RemoveLink(Link link)
        {
            return Remove_Link(link);
        }

        public bool Remove_Sorce_Link(Link link)
        {
            return Remove_Link(link);
        }

        public bool Remove_Source_Link(Link link)
        {
            return Remove_Link(link);
        }

        public bool Remove_Source_Link(Guid sourceID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == sourceID) > 0;
        }

        public bool RemoveSourceLink(Guid sourceID)
        {
            return Remove_Source_Link(sourceID);
        }

        public bool Remove_Destination_Link(Link link)
        {
            return Remove_Link(link);
        }

        public bool Remove_Destination_Link(Guid destinationID)
        {
            return referenceLinks.RemoveAll(p => p.DestinationID == destinationID) > 0;
        }

        public bool RemoveDestinationLink(Guid destinationID)
        {
            return Remove_Destination_Link(destinationID);
        }

        public bool RemoveLinksBetween(Guid sourceID, Guid destinationID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == sourceID && p.DestinationID == destinationID) > 0;
        }

        public int RemoveLinksFor(Guid nodeID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == nodeID || p.DestinationID == nodeID);
        }

        public bool Remove_Stale_Links(Guid nodeID)
        {
            return RemoveLinksFor(nodeID) > 0;
        }

        public IReadOnlyList<Link> Get_ReferenceLinks()
        {
            return referenceLinks.AsReadOnly();
        }

        public IReadOnlyList<Link> GetReferenceLinks()
        {
            return Get_ReferenceLinks();
        }
    }
}
