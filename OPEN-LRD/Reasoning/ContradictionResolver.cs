using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal static class ContradictionResolver
    {
        public static ContradictionResolution Resolve(
            Link proposedLink,
            LinkClass classification,
            IReadOnlyList<Link> existingOutgoingLinks,
            TrustAssessment proposedTrust,
            Func<Link, TrustAssessment> existingTrustProvider,
            ReasoningOptions options)
        {
            ArgumentNullException.ThrowIfNull(proposedLink);
            ArgumentNullException.ThrowIfNull(existingOutgoingLinks);
            ArgumentNullException.ThrowIfNull(proposedTrust);
            ArgumentNullException.ThrowIfNull(existingTrustProvider);
            ArgumentNullException.ThrowIfNull(options);

            if (!options.IsFunctional(classification))
                return new ContradictionResolution(true, Array.Empty<LinkConflict>(), Array.Empty<Link>());

            List<Link> conflicts = existingOutgoingLinks
                .Where(link => link.Classification == classification
                    && link.ForwardLink == proposedLink.ForwardLink
                    && link.DestinationID != proposedLink.DestinationID)
                .ToList();

            if (conflicts.Count == 0)
                return new ContradictionResolution(true, Array.Empty<LinkConflict>(), Array.Empty<Link>());

            List<LinkConflict> conflictRecords = new List<LinkConflict>();
            List<Link> invalidatedLinks = new List<Link>();
            bool acceptProposed = proposedTrust.Decision != TrustDecision.Discredited;

            foreach (Link existingLink in conflicts)
            {
                TrustAssessment existingTrust = existingTrustProvider(existingLink);
                bool proposedClearlyStronger =
                    proposedTrust.Decision == TrustDecision.Trusted
                    && proposedTrust.Score >= existingTrust.Score + options.TrustMargin;

                bool rejectProposed;

                if (proposedTrust.Decision == TrustDecision.Discredited)
                {
                    rejectProposed = true;
                }
                else if (existingTrust.Decision == TrustDecision.Trusted && !proposedClearlyStronger)
                {
                    rejectProposed = true;
                }
                else if (proposedClearlyStronger)
                {
                    rejectProposed = false;
                }
                else
                {
                    rejectProposed = true;
                }

                if (rejectProposed)
                    acceptProposed = false;
                else
                    invalidatedLinks.Add(existingLink);

                conflictRecords.Add(new LinkConflict(
                    existingLink,
                    proposedLink,
                    classification,
                    proposedLink.SourceID,
                    existingLink.DestinationID,
                    proposedLink.DestinationID,
                    existingTrust.Score,
                    proposedTrust.Score,
                    rejectProposed ? "RejectedProposed" : "AcceptedProposed"));
            }

            if (!acceptProposed)
            {
                invalidatedLinks.Clear();
                conflictRecords = conflictRecords
                    .Select(conflict => conflict with { Resolution = "RejectedProposed" })
                    .ToList();
            }

            return new ContradictionResolution(acceptProposed, conflictRecords, invalidatedLinks);
        }
    }
}
