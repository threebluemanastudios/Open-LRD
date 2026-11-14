namespace OPEN_LRD.Nodes.Links
{
    public sealed record Link
    {
        public Guid SourceID { get; }
        public Guid DestinationID { get; }
        public ForwardLinkTypes ForwardLink { get; }
        public InverseLinkTypes InverseLink { get; }
        public LinkClass Classification { get; }

        public bool IsActive { get; private set; } = true;

        public LinkOrigin Origins { get; private set; } = LinkOrigin.Explicit;

        public static Link Empty { get; } = new Link();

        private Link()
        {
            Classification = LinkClass.empty;
            ForwardLink = ForwardLinkTypes.basic;
            InverseLink = InverseLinkTypes.basic;
        }

        public Link(Guid sourceID, Guid destinationID)
            : this(sourceID, destinationID, LinkClass.basic)
        {
        }

        public Link(Guid sourceID, Guid destinationID, LinkClass classification)
            : this(sourceID, destinationID, classification, ForwardLinkTypes.basic, InverseLinkTypes.basic,
                LinkOrigin.Explicit)
        {
        }

        public Link(Guid sourceID, Guid destinationID, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, forwardLink, inverseLink, LinkOrigin.Explicit)
        {
        }

        public Link(Guid sourceID, Guid destinationID, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink,
            LinkClass classification, LinkOrigin origin)
            : this(sourceID, destinationID, classification, forwardLink, inverseLink, origin)
        {
        }

        public Link(Guid sourceID, Guid destinationID, int forwardLink, int inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, FromForwardId(forwardLink), FromInverseId(inverseLink),
                LinkOrigin.Explicit)
        {
        }

        public Link(Guid sourceID, Guid destinationID, int forwardLink, int inverseLink, LinkClass classification,
            LinkOrigin origin)
            : this(sourceID, destinationID, classification, FromForwardId(forwardLink), FromInverseId(inverseLink), origin)
        {
        }

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

        private static ForwardLinkTypes FromForwardId(int id)
        {
            if (!ForwardLinkTypes.TryFromId(id, out ForwardLinkTypes value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown forward link type.");

            return value;
        }

        private static InverseLinkTypes FromInverseId(int id)
        {
            if (!InverseLinkTypes.TryFromId(id, out InverseLinkTypes value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown inverse link type.");

            return value;
        }

        public Link GetLink()
        {
            return this;
        }

        public void Invalidate()
        {
            IsActive = false;
        }

        public void Reactivate()
        {
            IsActive = true;
        }

        internal void AddOrigin(LinkOrigin origin)
        {
            Origins |= origin;
        }

        internal LinkOrigin RemoveOrigin(LinkOrigin origin)
        {
            Origins &= ~origin;
            return Origins;
        }

        public bool HasSameIdentity(Link other)
        {
            ArgumentNullException.ThrowIfNull(other);
            return SourceID == other.SourceID
                && DestinationID == other.DestinationID
                && ForwardLink.Id == other.ForwardLink.Id
                && InverseLink.Id == other.InverseLink.Id
                && Classification.Id == other.Classification.Id;
        }

        public bool Equals(Link? other)
        {
            return other is not null && HasSameIdentity(other);
        }

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
