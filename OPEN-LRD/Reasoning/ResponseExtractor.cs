using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal static class ResponseExtractor
    {
        public static IReadOnlyList<GraphPath> Extract(
            QueryGraph query,
            SubgraphMatch match,
            GraphSnapshot snapshot,
            TrustScale trust,
            IReadOnlySet<string>? invalidLinkKeys = null,
            ReasoningOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(match);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(trust);

            options ??= new ReasoningOptions();

            if (snapshot.Nodes.Count == 0 || match.NodeMap.Count == 0)
                return Array.Empty<GraphPath>();

            Guid? terminalQueryNodeID = FindTerminalQueryNode(query);

            if (terminalQueryNodeID.HasValue
                && match.NodeMap.TryGetValue(terminalQueryNodeID.Value, out Guid terminalGraphNodeID))
            {
                return ExtractFrom(
                    terminalGraphNodeID,
                    query,
                    match,
                    snapshot,
                    trust,
                    invalidLinkKeys,
                    options);
            }

            return ExtractFrom(
                match.AnchorGraphNodeID,
                query,
                match,
                snapshot,
                trust,
                invalidLinkKeys,
                options);
        }

        public static LinkedList<Chain_Node>? ToResponseChain(GraphPath path, GraphSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(path);
            ArgumentNullException.ThrowIfNull(snapshot);

            if (path.NodeIDs.Count == 0)
                return null;

            List<Chain_Node> chainNodes = new List<Chain_Node>(path.NodeIDs.Count);

            foreach (Guid nodeID in path.NodeIDs)
            {
                if (!snapshot.TryGetNode(nodeID, out GraphNodeSnapshot graphNode))
                    return null;

                Chain_Node chainNode = new Chain_Node(
                    graphNode.ID,
                    graphNode.Type,
                    Node.DefaultTimeToLive);

                chainNode.Set_Label(graphNode.Label);

                foreach (string alias in graphNode.Aliases)
                    chainNode.Add_Alias(alias);

                chainNodes.Add(chainNode);
            }

            for (int i = 0; i < path.NodeIDs.Count - 1; i++)
            {
                Link traversalLink = GetTraversalLink(path, i);
                chainNodes[i].Set_LinkNext(traversalLink);
                chainNodes[i + 1].Set_LinkPrevious(traversalLink);
            }

            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();

            foreach (Chain_Node chainNode in chainNodes)
                chain.AddLast(chainNode);

            return chain;
        }

        private static IReadOnlyList<GraphPath> ExtractFrom(
            Guid startGraphNodeID,
            QueryGraph query,
            SubgraphMatch match,
            GraphSnapshot snapshot,
            TrustScale trust,
            IReadOnlySet<string>? invalidLinkKeys,
            ReasoningOptions options)
        {
            if (!snapshot.ContainsNode(startGraphNodeID))
                return Array.Empty<GraphPath>();

            List<GraphPath> paths = new List<GraphPath>();
            List<Guid> currentPath = new List<Guid>();
            List<Link> currentLinks = new List<Link>();
            HashSet<Guid> currentPathSet = new HashSet<Guid>();
            int expandedNodes = 0;
            int maxDepth = Math.Max(0, options.MaxTraversalDepth);

            void Walk(Guid currentNodeID)
            {
                if (expandedNodes >= options.MaxExpandedNodes)
                    return;

                expandedNodes++;

                currentPath.Add(currentNodeID);
                currentPathSet.Add(currentNodeID);

                if (currentPath.Count > 0)
                {
                    GraphPath path = CreatePath(
                        currentPath,
                        currentLinks,
                        query,
                        snapshot,
                        trust,
                        options);

                    paths.Add(path);
                }

                if (currentLinks.Count >= maxDepth)
                {
                    currentPath.RemoveAt(currentPath.Count - 1);
                    currentPathSet.Remove(currentNodeID);
                    return;
                }

                List<Link> incident = snapshot.GetIncidentLinks(currentNodeID)
                    .Where(link => link.SourceID != link.DestinationID)
                    .Where(link => invalidLinkKeys is null
                        || !invalidLinkKeys.Contains(GraphSnapshot.GetLinkKey(link)))
                    .OrderBy(link => link.SourceID == currentNodeID
                        ? link.DestinationID
                        : link.SourceID)
                    .ToList();

                foreach (Link link in incident)
                {
                    Guid nextNodeID = link.SourceID == currentNodeID
                        ? link.DestinationID
                        : link.SourceID;

                    if (currentPathSet.Contains(nextNodeID))
                        continue;

                    currentLinks.Add(link);
                    Walk(nextNodeID);
                    currentLinks.RemoveAt(currentLinks.Count - 1);
                }

                currentPath.RemoveAt(currentPath.Count - 1);
                currentPathSet.Remove(currentNodeID);
            }

            Walk(startGraphNodeID);

            Dictionary<string, GraphPath> deduplicated =
                new Dictionary<string, GraphPath>(StringComparer.Ordinal);

            foreach (GraphPath path in paths)
            {
                string key = string.Join(">", path.NodeIDs);

                if (!deduplicated.TryGetValue(key, out GraphPath? existing)
                    || path.Score > existing.Score)
                {
                    deduplicated[key] = path;
                }
            }

            return deduplicated.Values
                .OrderByDescending(path => path.Score)
                .ThenBy(path => path.Depth)
                .Take(Math.Max(1, options.MaxResponsePaths))
                .ToList();
        }

        private static GraphPath CreatePath(
            IReadOnlyList<Guid> nodeIDs,
            IReadOnlyList<Link> links,
            QueryGraph query,
            GraphSnapshot snapshot,
            TrustScale trust,
            ReasoningOptions options)
        {
            double queryOverlap = CalculateQueryOverlap(links, query);
            double trustOrWeight = CalculateTrustOrWeight(nodeIDs, snapshot, trust);
            double linkPriority = CalculateLinkPriority(links, options);
            double permanenceRatio = CalculatePermanenceRatio(nodeIDs, snapshot);
            double depthFactor = options.MaxTraversalDepth <= 0
                ? 1.0
                : 1.0 - Math.Min(1.0, (double)links.Count / options.MaxTraversalDepth);

            double score =
                (0.35 * queryOverlap)
                + (0.25 * trustOrWeight)
                + (0.20 * linkPriority)
                + (0.10 * permanenceRatio)
                + (0.10 * depthFactor);

            score = Math.Clamp(score, 0.0, 1.0);

            return new GraphPath(
                nodeIDs.ToArray(),
                links.ToArray(),
                score,
                trustOrWeight,
                links.Count);
        }

        private static Guid? FindTerminalQueryNode(QueryGraph query)
        {
            if (query.Edges.Count == 0)
                return query.RootQueryNodeID;

            HashSet<Guid> hasOutgoing = query.Edges
                .Select(edge => edge.FromQueryNodeID)
                .ToHashSet();

            List<Guid> terminals = query.Nodes
                .Where(node => !hasOutgoing.Contains(node.QueryNodeID))
                .Select(node => node.QueryNodeID)
                .ToList();

            if (terminals.Count == 0)
                return null;

            if (terminals.Contains(query.RootQueryNodeID) && terminals.Count > 1)
                return terminals.First(id => id != query.RootQueryNodeID);

            return terminals[0];
        }

        private static double CalculateQueryOverlap(
            IReadOnlyList<Link> links,
            QueryGraph query)
        {
            if (query.Edges.Count == 0)
                return 0.0;

            int matches = 0;

            foreach (Link link in links)
            {
                bool matchesQuery = query.Edges.Any(edge =>
                    edge.Classification == link.Classification
                    && (link.ForwardLink.Id == edge.ForwardLinkType
                        || link.ForwardLink.Id == edge.InverseLinkType));

                if (matchesQuery)
                    matches++;
            }

            return Math.Clamp((double)matches / query.Edges.Count, 0.0, 1.0);
        }

        private static double CalculateTrustOrWeight(
            IReadOnlyList<Guid> nodeIDs,
            GraphSnapshot snapshot,
            TrustScale trust)
        {
            if (nodeIDs.Count == 0)
                return 0.0;

            double total = 0.0;

            foreach (Guid nodeID in nodeIDs)
            {
                double trustScore = trust.Assess(nodeID, null).Score;

                if (trustScore > 0.0)
                {
                    total += Math.Clamp(trustScore, 0.0, 1.0);
                    continue;
                }

                if (snapshot.TryGetNode(nodeID, out GraphNodeSnapshot node))
                {
                    total += Math.Clamp(
                        node.WeightUp / Node.PromotionWeightThreshold,
                        0.0,
                        1.0);
                }
            }

            return total / nodeIDs.Count;
        }

        private static double CalculateLinkPriority(
            IReadOnlyList<Link> links,
            ReasoningOptions options)
        {
            if (links.Count == 0)
                return 0.0;

            double total = 0.0;

            foreach (Link link in links)
            {
                total += options.LinkClassPriority.TryGetValue(link.Classification, out double priority)
                    ? priority
                    : 0.5;
            }

            return Math.Clamp(total / links.Count, 0.0, 1.0);
        }

        private static double CalculatePermanenceRatio(
            IReadOnlyList<Guid> nodeIDs,
            GraphSnapshot snapshot)
        {
            if (nodeIDs.Count == 0)
                return 0.0;

            int permanentCount = 0;

            foreach (Guid nodeID in nodeIDs)
            {
                if (snapshot.TryGetNode(nodeID, out GraphNodeSnapshot node)
                    && node.Lifetime == NodeLifetime.Permanent)
                {
                    permanentCount++;
                }
            }

            return (double)permanentCount / nodeIDs.Count;
        }

        private static Link GetTraversalLink(GraphPath path, int index)
        {
            Guid currentID = path.NodeIDs[index];
            Guid nextID = path.NodeIDs[index + 1];
            Link link = path.Links[index];

            if (link.SourceID == currentID && link.DestinationID == nextID)
                return link;

            return new Link(
                currentID,
                nextID,
                link.InverseLink.Id,
                link.ForwardLink.Id,
                link.Classification);
        }
    }
}
