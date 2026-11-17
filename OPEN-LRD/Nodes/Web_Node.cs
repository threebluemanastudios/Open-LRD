using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    /// <summary>Extends a node with outgoing typed links and reference-link maintenance.</summary>
    internal class Web_Node : Node
    {
        private const int DefaultWebNodeTimeToLive = 32760;
        private readonly List<Link> referenceLinks = new List<Link>();

        /// <summary>Creates a temporary basic web node with a generated identifier and default web-node TTL.</summary>
        public Web_Node()
            : base(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        /// <summary>Creates a temporary basic web node with the specified identifier and default web-node TTL.</summary>
        public Web_Node(Guid ID)
            : base(ID, NodeTypes.Basics, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        /// <summary>Creates a temporary web node of the specified type and identifier.</summary>
        public Web_Node(NodeTypes TYPE, Guid ID)
            : base(ID, TYPE, NodeLifetime.Temporary, DefaultWebNodeTimeToLive)
        {
        }

        /// <summary>Creates a temporary basic web node with the specified identifier and TTL.</summary>
        public Web_Node(Guid ID, int TTL)
            : base(ID, NodeTypes.Basics, NodeLifetime.Temporary, TTL)
        {
        }

        /// <summary>Creates a temporary web node with the specified identifier, numeric type, and TTL.</summary>
        public Web_Node(Guid ID, int TYPE, int TTL)
            : base(ID, NodeTypes.FromId(TYPE), NodeLifetime.Temporary, TTL)
        {
        }

        /// <summary>Creates a temporary web node with the specified identifier, type, and TTL.</summary>
        public Web_Node(Guid ID, NodeTypes TYPE, int TTL)
            : base(ID, TYPE, NodeLifetime.Temporary, TTL)
        {
        }

        /// <summary>Creates a web node with the specified identifier, type, lifetime, and TTL.</summary>
        public Web_Node(Guid ID, NodeTypes TYPE, NodeLifetime lifetime, int TTL)
            : base(ID, TYPE, lifetime, TTL)
        {
        }

        /// <summary>Adds a reference link or merges origins and reactivation state into an existing matching link.</summary>
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

        /// <summary>Adds a reference link to this node.</summary>
        public void AddLink(Link link)
        {
            Add_Link(link);
        }

        /// <summary>Adds each link in the supplied sequence to this node.</summary>
        public void Add_LinkList(IEnumerable<Link> links)
        {
            ArgumentNullException.ThrowIfNull(links);

            foreach (Link link in links)
                Add_Link(link);
        }

        /// <summary>Adds each link in the supplied sequence to this node.</summary>
        public void AddLinkList(IEnumerable<Link> links)
        {
            Add_LinkList(links);
        }

        /// <summary>Removes a matching reference link from this node.</summary>
        public bool Remove_Link(Link link)
        {
            ArgumentNullException.ThrowIfNull(link);
            return referenceLinks.Remove(link);
        }

        /// <summary>Removes a matching reference link from this node.</summary>
        public bool RemoveLink(Link link)
        {
            return Remove_Link(link);
        }

        /// <summary>Removes a matching source reference link from this node.</summary>
        public bool Remove_Sorce_Link(Link link)
        {
            return Remove_Link(link);
        }

        /// <summary>Removes a matching source reference link from this node.</summary>
        public bool Remove_Source_Link(Link link)
        {
            return Remove_Link(link);
        }

        /// <summary>Removes all reference links originating from the specified source node.</summary>
        public bool Remove_Source_Link(Guid sourceID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == sourceID) > 0;
        }

        /// <summary>Removes all reference links originating from the specified source node.</summary>
        public bool RemoveSourceLink(Guid sourceID)
        {
            return Remove_Source_Link(sourceID);
        }

        /// <summary>Removes a matching destination reference link from this node.</summary>
        public bool Remove_Destination_Link(Link link)
        {
            return Remove_Link(link);
        }

        /// <summary>Removes all reference links targeting the specified destination node.</summary>
        public bool Remove_Destination_Link(Guid destinationID)
        {
            return referenceLinks.RemoveAll(p => p.DestinationID == destinationID) > 0;
        }

        /// <summary>Removes all reference links targeting the specified destination node.</summary>
        public bool RemoveDestinationLink(Guid destinationID)
        {
            return Remove_Destination_Link(destinationID);
        }

        /// <summary>Removes all reference links connecting the specified source and destination nodes.</summary>
        public bool RemoveLinksBetween(Guid sourceID, Guid destinationID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == sourceID && p.DestinationID == destinationID) > 0;
        }

        /// <summary>Removes all reference links that reference the specified node as source or destination.</summary>
        public int RemoveLinksFor(Guid nodeID)
        {
            return referenceLinks.RemoveAll(p => p.SourceID == nodeID || p.DestinationID == nodeID);
        }

        /// <summary>Removes stale reference links associated with the specified node.</summary>
        public bool Remove_Stale_Links(Guid nodeID)
        {
            return RemoveLinksFor(nodeID) > 0;
        }

        /// <summary>Returns a read-only view of the node's reference links.</summary>
        public IReadOnlyList<Link> Get_ReferenceLinks()
        {
            return referenceLinks.AsReadOnly();
        }

        /// <summary>Returns a read-only view of the node's reference links.</summary>
        public IReadOnlyList<Link> GetReferenceLinks()
        {
            return Get_ReferenceLinks();
        }
    }
}
