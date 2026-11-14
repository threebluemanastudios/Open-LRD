using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;

namespace OPEN_LRD.Maintenance
{
    internal static class SupersededLinkPruner
    {
        public static void Prune(LRD_Core core, MaintenanceOptions options, MaintenanceReport report)
        {
            IReadOnlyDictionary<Guid, Web_Node>? graph = core.GetDictionary();
            if (graph is null || options.MaxLinksPrunedPerCycle == 0)
                return;

            Dictionary<string, Link> unique = new Dictionary<string, Link>(StringComparer.Ordinal);
            int linksScanned = 0;
            foreach (Web_Node node in graph.Values.OrderBy(node => node.ID))
            {
                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (!link.IsActive)
                        continue;
                    if (++linksScanned > options.MaxLinksToExaminePerCycle)
                    {
                        report.WorkBudgetExhausted = true;
                        report.AddDiagnostic("Superseded-link scan reached its link budget.");
                        break;
                    }
                    unique.TryAdd(LinkKey(link), link);
                }
                if (linksScanned > options.MaxLinksToExaminePerCycle)
                    break;
            }

            Link[] activeLinks = unique.Values.ToArray();
            Link[] genericLinks = activeLinks.Where(IsGeneric).ToArray();
            Dictionary<Guid, List<Link>> specificOutgoing = activeLinks
                .Where(link => !IsGeneric(link))
                .GroupBy(link => link.SourceID)
                .ToDictionary(group => group.Key, group => group.ToList());
            HashSet<(Guid Source, Guid Destination)> specificEndpoints = activeLinks
                .Where(link => !IsGeneric(link))
                .Select(link => (link.SourceID, link.DestinationID))
                .ToHashSet();

            int examined = 0;
            int pathLinksExamined = 0;
            foreach (Link candidate in genericLinks)
            {
                if (examined >= options.MaxNodesToExaminePerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    report.AddDiagnostic("Superseded-link scan reached its work budget.");
                    break;
                }
                examined++;

                bool superseded = specificEndpoints.Contains((candidate.SourceID, candidate.DestinationID));
                bool pathBudgetExhausted = false;

                if (!superseded && HasSpecificPath(
                    candidate.SourceID,
                    candidate.DestinationID,
                    specificOutgoing,
                    options.MaxTransitivePruneDepth,
                    options.MaxLinksToExaminePerCycle,
                    ref pathLinksExamined,
                    out pathBudgetExhausted))
                {
                    superseded = true;
                }
                else if (!superseded && pathBudgetExhausted)
                {
                    report.WorkBudgetExhausted = true;
                    report.AddDiagnostic("Transitive-pruning search reached its link budget.");
                    break;
                }

                if (!superseded)
                    continue;

                if (report.LinksInvalidated >= options.MaxLinksPrunedPerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    report.AddDiagnostic("Superseded-link invalidation reached its per-cycle budget.");
                    break;
                }

                if (core.InvalidateLink(candidate))
                    report.LinksInvalidated++;
            }

            report.NodesExamined += examined;
        }

        private static bool IsGeneric(Link link)
        {
            return link.ForwardLink.Id == ForwardLinkTypes.basic.Id
                || link.ForwardLink.Id == OntologyRelations.RelatedTo;
        }

        private static bool HasSpecificPath(
            Guid sourceID,
            Guid destinationID,
            IReadOnlyDictionary<Guid, List<Link>> outgoing,
            int maxDepth,
            int maxLinksToExamine,
            ref int linksExamined,
            out bool budgetExhausted)
        {
            budgetExhausted = false;
            if (!outgoing.TryGetValue(sourceID, out List<Link>? firstLinks))
                return false;

            Queue<(Guid NodeID, int Depth)> queue = new Queue<(Guid, int)>();
            HashSet<Guid> visitedAtDepth = new HashSet<Guid> { sourceID };
            foreach (Link link in firstLinks)
            {
                if (link.DestinationID == destinationID && maxDepth >= 1)
                    continue;
                queue.Enqueue((link.DestinationID, 1));
                visitedAtDepth.Add(link.DestinationID);
            }

            while (queue.Count > 0)
            {
                (Guid current, int depth) = queue.Dequeue();
                if (depth >= maxDepth || !outgoing.TryGetValue(current, out List<Link>? nextLinks))
                    continue;

                foreach (Link link in nextLinks)
                {
                    if (++linksExamined > maxLinksToExamine)
                    {
                        budgetExhausted = true;
                        return false;
                    }

                    if (link.DestinationID == destinationID)
                        return true;

                    if (visitedAtDepth.Add(link.DestinationID))
                        queue.Enqueue((link.DestinationID, depth + 1));
                }
            }

            return false;
        }

        private static string LinkKey(Link link)
        {
            return string.Join("|", link.SourceID, link.DestinationID,
                link.ForwardLink.Id, link.InverseLink.Id, link.Classification.Id);
        }
    }
}
