namespace OPEN_LRD.Nodes.Links
{
    /// <summary>Represents one directed, typed graph edge and its lifecycle metadata.</summary>
    public sealed record Link
    {
        /// <summary>Gets the source node identifier.</summary>
        public Guid SourceID { get; }
        /// <summary>Gets the destination node identifier.</summary>
        public Guid DestinationID { get; }
        /// <summary>Gets the registered forward relationship type.</summary>
        public ForwardLinkTypes ForwardLink { get; }
        /// <summary>Gets the registered inverse relationship type.</summary>
        public InverseLinkTypes InverseLink { get; }
        /// <summary>Gets the semantic link classification.</summary>
        public LinkClass Classification { get; }

        /// <summary>Gets whether the edge is active for reasoning and visualization.</summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>Gets the combined origins that contributed this edge.</summary>
        public LinkOrigin Origins { get; private set; } = LinkOrigin.Explicit;

        /// <summary>Gets the sentinel empty link value.</summary>
        public static Link Empty { get; } = new Link();

        /// <summary>Creates the sentinel empty link instance.</summary>
        private Link()
        {
            Classification = LinkClass.empty;
            ForwardLink = ForwardLinkTypes.basic;
            InverseLink = InverseLinkTypes.basic;
        }

        /// <summary>Creates a basic-class link using default relationship types.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        public Link(Guid sourceID, Guid destinationID)
            : this(sourceID, destinationID, LinkClass.basic)
        {
        }

        /// <summary>Creates a link with the specified class and default relationship types.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        /// <param name="classification">The semantic link class.</param>
        public Link(Guid sourceID, Guid destinationID, LinkClass classification)
            : this(sourceID, destinationID, classification, ForwardLinkTypes.basic, InverseLinkTypes.basic,
                LinkOrigin.Explicit)
        {
        }

        /// <summary>Creates a link with explicit registered relationship types and class.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        /// <param name="forwardLink">The forward relationship type.</param>
        /// <param name="inverseLink">The inverse relationship type.</param>
        /// <param name="classification">The semantic link class.</param>
        public Link(Guid sourceID, Guid destinationID, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, forwardLink, inverseLink, LinkOrigin.Explicit)
        {
        }

        /// <summary>Creates a link with explicit relationship types, class, and origin.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        /// <param name="forwardLink">The forward relationship type.</param>
        /// <param name="inverseLink">The inverse relationship type.</param>
        /// <param name="classification">The semantic link class.</param>
        /// <param name="origin">The process that created the edge.</param>
        public Link(Guid sourceID, Guid destinationID, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink,
            LinkClass classification, LinkOrigin origin)
            : this(sourceID, destinationID, classification, forwardLink, inverseLink, origin)
        {
        }

        /// <summary>Creates a link from registered numeric relationship identifiers.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        /// <param name="forwardLink">The forward relationship type identifier.</param>
        /// <param name="inverseLink">The inverse relationship type identifier.</param>
        /// <param name="classification">The semantic link class.</param>
        public Link(Guid sourceID, Guid destinationID, int forwardLink, int inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, FromForwardId(forwardLink), FromInverseId(inverseLink),
                LinkOrigin.Explicit)
        {
        }

        /// <summary>Creates a link from registered numeric relationship identifiers and an origin.</summary>
        /// <param name="sourceID">The source node identifier.</param>
        /// <param name="destinationID">The destination node identifier.</param>
        /// <param name="forwardLink">The forward relationship type identifier.</param>
        /// <param name="inverseLink">The inverse relationship type identifier.</param>
        /// <param name="classification">The semantic link class.</param>
        /// <param name="origin">The process that created the edge.</param>
        public Link(Guid sourceID, Guid destinationID, int forwardLink, int inverseLink, LinkClass classification,
            LinkOrigin origin)
            : this(sourceID, destinationID, classification, FromForwardId(forwardLink), FromInverseId(inverseLink), origin)
        {
        }

        /// <summary>Validates endpoints and initializes a link with the supplied types, classification, and origin.</summary>
        private Link(Guid sourceID, Guid destinationID, LinkClass classification, ForwardLinkTypes forwardLink,
            InverseLinkTypes inverseLink, LinkOrigin origin)
        {
            if (sourceID == Guid.Empty)
                throw new ArgumentException("Link source ID cannot be empty.", nameof(sourceID));

            if (destinationID == Guid.Empty)
                throw new ArgumentException("Link destination ID cannot be empty.", nameof(destinationID));

            SourceID = sourceID;
            DestinationID = destinationID;
            Classification = classification;
            ForwardLink = forwardLink;
            InverseLink = inverseLink;
            Origins = origin;
        }

        /// <summary>Resolves a registered forward link type by identifier or throws when it is unknown.</summary>
        private static ForwardLinkTypes FromForwardId(int id)
        {
            if (!ForwardLinkTypes.TryFromId(id, out ForwardLinkTypes value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown forward link type.");

            return value;
        }

        /// <summary>Resolves a registered inverse link type by identifier or throws when it is unknown.</summary>
        private static InverseLinkTypes FromInverseId(int id)
        {
            if (!InverseLinkTypes.TryFromId(id, out InverseLinkTypes value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown inverse link type.");

            return value;
        }

        /// <summary>Returns this immutable link value.</summary>
        public Link GetLink()
        {
            return this;
        }

        /// <summary>Marks this link inactive without removing its history.</summary>
        public void Invalidate()
        {
            IsActive = false;
        }

        /// <summary>Marks this link active again.</summary>
        public void Reactivate()
        {
            IsActive = true;
        }

        /// <summary>Adds one or more origin flags to this link.</summary>
        internal void AddOrigin(LinkOrigin origin)
        {
            Origins |= origin;
        }

        /// <summary>Removes one or more origin flags from this link and returns the remaining origins.</summary>
        internal LinkOrigin RemoveOrigin(LinkOrigin origin)
        {
            Origins &= ~origin;
            return Origins;
        }

        /// <summary>Checks whether another link has the same endpoints, type identifiers, and class.</summary>
        /// <param name="other">The candidate link to compare.</param>
        /// <returns><see langword="true"/> when both links have the same identity.</returns>
        public bool HasSameIdentity(Link other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return SourceID == other.SourceID
                && DestinationID == other.DestinationID
                && ForwardLink.Id == other.ForwardLink.Id
                && InverseLink.Id == other.InverseLink.Id
                && Classification.Id == other.Classification.Id;
        }

        /// <summary>Compares this link with another link by semantic identity.</summary>
        /// <param name="other">The candidate link.</param>
        /// <returns><see langword="true"/> when the links have the same identity.</returns>
        public bool Equals(Link? other)
        {
            return other is not null && HasSameIdentity(other);
        }

        /// <summary>Computes a hash from the link's identity fields.</summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(
                SourceID,
                DestinationID,
                ForwardLink.Id,
                InverseLink.Id,
                Classification.Id);
        }
    }
}
