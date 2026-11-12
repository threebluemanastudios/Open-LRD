namespace OPEN_LRD.Nodes.Links
{
    public enum ForwardLinkTypes
    {
        basic,

    }

    public enum InverseLinkTypes
    {
        basic,

    }

    public enum LinkClass
    { 
        empty,
        basic,
        map_locations,
        events,
        actions,
        spacial,
        temporal,
        sources,
        identities,

    }

    public sealed record Link
    {
        public Guid SourceID { get; }
        public Guid DestinationID { get; }
        public ForwardLinkTypes ForwardLink { get; }
        public InverseLinkTypes InverseLink { get; }
        public LinkClass Classification { get; }

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
            : this(sourceID, destinationID, classification, ForwardLinkTypes.basic, InverseLinkTypes.basic)
        {
        }

        public Link(Guid sourceID, Guid destinationID, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, forwardLink, inverseLink)
        {
        }

        public Link(Guid sourceID, Guid destinationID, int forwardLink, int inverseLink, LinkClass classification)
            : this(sourceID, destinationID, classification, FromForwardId(forwardLink), FromInverseId(inverseLink))
        {
        }

        private Link(Guid sourceID, Guid destinationID, LinkClass classification, ForwardLinkTypes forwardLink, InverseLinkTypes inverseLink)
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
        }

        private static ForwardLinkTypes FromForwardId(int id)
        {
            ForwardLinkTypes value = (ForwardLinkTypes)id;
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown forward link type.");

            return value;
        }

        private static InverseLinkTypes FromInverseId(int id)
        {
            InverseLinkTypes value = (InverseLinkTypes)id;
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown inverse link type.");

            return value;
        }

        public Link GetLink()
        {
            return this;
        }
    }
}
