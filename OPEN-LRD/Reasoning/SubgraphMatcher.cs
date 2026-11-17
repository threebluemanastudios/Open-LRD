using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    /// <summary>Matches bounded query graphs to candidate subgraphs and scores their evidence.</summary>
    internal static class SubgraphMatcher
    {
        /// <summary>Stores a selected graph edge and its match score during search.</summary>
        private readonly record struct MatchedEdge(Link Link, double Score);

        /// <summary>Finds and ranks subgraph matches for a query graph within a graph snapshot.</summary>
        public static IReadOnlyList<SubgraphMatch> FindMatches(
            QueryGraph query,
            GraphSnapshot graph,
            ReasoningOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(graph);

            options ??= new ReasoningOptions();

            if (query.Nodes.Count == 0 || graph.Nodes.Count == 0)
                return Array.Empty<SubgraphMatch>();

            Dictionary<Guid, QueryNode> queryNodesByID = query.Nodes.ToDictionary(node => node.QueryNodeID);
            Dictionary<Guid, List<GraphNodeSnapshot>> candidates =
                BuildCandidateIndex(query, graph, options);

            if (!candidates.TryGetValue(query.RootQueryNodeID, out List<GraphNodeSnapshot>? rootCandidates)
                || rootCandidates.Count == 0)
            {
                return Array.Empty<SubgraphMatch>();
            }

            Dictionary<Guid, List<(Guid NeighborID, QueryEdge Edge)>> adjacency = BuildAdjacency(query);
            List<QueryNode> orderedNodes = OrderQueryNodes(query, adjacency, candidates);
            Dictionary<(Guid, Guid), List<Link>> linksByPair = BuildLinkIndex(graph);

            List<SubgraphMatch> results = new List<SubgraphMatch>();
            HashSet<string> seenMappings = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<Guid, Guid> mapping = new Dictionary<Guid, Guid>();
            List<MatchedEdge> matchedEdges = new List<MatchedEdge>();
            HashSet<string> matchedLinkKeys = new HashSet<string>(StringComparer.Ordinal);
            List<Guid> unmatchedQueryNodeIDs = new List<Guid>();
            int searchSteps = 0;
            bool searchLimitReached = false;

            // Recursively assigns candidate graph nodes to ordered query nodes and matches connecting edges.
            void Search(int index)
            {
                if (searchLimitReached)
                    return;

                searchSteps++;

                if (searchSteps > options.MaxSearchSteps)
                {
                    searchLimitReached = true;
                    return;
                }

                if (index >= orderedNodes.Count)
                {
                    EvaluateMatch();
                    return;
                }

                QueryNode queryNode = orderedNodes[index];
                bool isRoot = queryNode.QueryNodeID == query.RootQueryNodeID;

                if (!isRoot)
                {
                    unmatchedQueryNodeIDs.Add(queryNode.QueryNodeID);
                    Search(index + 1);
                    unmatchedQueryNodeIDs.RemoveAt(unmatchedQueryNodeIDs.Count - 1);
                }

                if (!candidates.TryGetValue(queryNode.QueryNodeID, out List<GraphNodeSnapshot>? globalCandidates))
                    return;

                IReadOnlyList<GraphNodeSnapshot> nodeCandidates = GetCandidatesForSearch(
                    queryNode,
                    adjacency,
                    mapping,
                    graph,
                    globalCandidates,
                    options);

                foreach (GraphNodeSnapshot candidate in nodeCandidates)
                {
                    if (mapping.ContainsValue(candidate.ID))
                        continue;

                    mapping[queryNode.QueryNodeID] = candidate.ID;

                    List<MatchedEdge> addedEdges = new List<MatchedEdge>();
                    List<string> addedKeys = new List<string>();

                    foreach (QueryEdge edge in query.Edges)
                    {
                        Guid otherQueryNodeID;

                        if (edge.FromQueryNodeID == queryNode.QueryNodeID)
                            otherQueryNodeID = edge.ToQueryNodeID;
                        else if (edge.ToQueryNodeID == queryNode.QueryNodeID)
                            otherQueryNodeID = edge.FromQueryNodeID;
                        else
                            continue;

                        if (!mapping.TryGetValue(otherQueryNodeID, out _))
                            continue;

                        Guid fromGraphNodeID = mapping[edge.FromQueryNodeID];
                        Guid toGraphNodeID = mapping[edge.ToQueryNodeID];

                        if (!TryMatchEdge(
                            edge,
                            fromGraphNodeID,
                            toGraphNodeID,
                            linksByPair,
                            options,
                            out Link? matchedLink,
                            out double edgeScore))
                        {
                            continue;
                        }

                        string linkKey = GetLinkKey(matchedLink!);

                        if (matchedLinkKeys.Contains(linkKey) || addedKeys.Contains(linkKey))
                            continue;

                        addedKeys.Add(linkKey);
                        addedEdges.Add(new MatchedEdge(matchedLink!, edgeScore));
                    }

                    foreach (MatchedEdge edge in addedEdges)
                    {
                        matchedEdges.Add(edge);
                        matchedLinkKeys.Add(GetLinkKey(edge.Link));
                    }

                    Search(index + 1);

                    foreach (MatchedEdge edge in addedEdges)
                    {
                        matchedEdges.RemoveAt(matchedEdges.Count - 1);
                        matchedLinkKeys.Remove(GetLinkKey(edge.Link));
                    }

                    mapping.Remove(queryNode.QueryNodeID);
                }
            }

            // Scores the current node and edge mapping and records it if it meets match thresholds.
            void EvaluateMatch()
            {
                if (mapping.Count == 0 || !mapping.ContainsKey(query.RootQueryNodeID))
                    return;

                string mappingKey = string.Join(
                    "|",
                    mapping
                        .OrderBy(pair => pair.Key)
                        .Select(pair => $"{pair.Key}:{pair.Value}"));

                if (!seenMappings.Add(mappingKey))
                    return;

                double nodeScoreSum = 0.0;

                foreach (KeyValuePair<Guid, Guid> pair in mapping)
                {
                    if (!queryNodesByID.TryGetValue(pair.Key, out QueryNode? queryNode)
                        || !graph.TryGetNode(pair.Value, out GraphNodeSnapshot graphNode))
                    {
                        return;
                    }

                    nodeScoreSum += ScoreNode(queryNode, graphNode, options);
                }

                double nodeScore = nodeScoreSum / mapping.Count;
                double edgeScore = query.Edges.Count == 0
                    ? 1.0
                    : matchedEdges.Sum(edge => edge.Score) / query.Edges.Count;
                double nodeCoverage = (double)mapping.Count / query.Nodes.Count;
                double edgeCoverage = query.Edges.Count == 0
                    ? 1.0
                    : (double)matchedEdges.Count / query.Edges.Count;
                double coverage = (nodeCoverage + edgeCoverage) / 2.0;

                if (nodeCoverage < options.MinimumPartialMatchCoverage
                    && mapping.Count < query.Nodes.Count)
                {
                    return;
                }

                double overallScore =
                    (options.NodeWeight * nodeScore)
                    + (options.EdgeWeight * edgeScore)
                    + (options.CoverageWeight * coverage);

                bool complete = mapping.Count == query.Nodes.Count
                    && matchedEdges.Count == query.Edges.Count;

                double minimumScore = complete
                    ? options.MinimumMatchScore
                    : options.MinimumPartialMatchScore;

                if (overallScore < minimumScore)
                    return;

                List<Guid> unmatched = query.Nodes
                    .Where(node => !mapping.ContainsKey(node.QueryNodeID))
                    .Select(node => node.QueryNodeID)
                    .ToList();

                List<string> diagnostics = new List<string>();

                if (searchLimitReached)
                    diagnostics.Add("The matcher reached its search-step limit.");

                results.Add(new SubgraphMatch(
                    mapping[query.RootQueryNodeID],
                    new Dictionary<Guid, Guid>(mapping),
                    matchedEdges.Select(edge => edge.Link).Distinct().ToList(),
                    Array.Empty<LinkConflict>(),
                    unmatched,
                    nodeScore,
                    edgeScore,
                    coverage,
                    overallScore,
                    diagnostics));
            }

            Search(0);

            List<SubgraphMatch> ordered = results
                .OrderByDescending(match => match.OverallScore)
                .ThenByDescending(match => match.CoverageScore)
                .ToList();

            List<SubgraphMatch> filtered = new List<SubgraphMatch>();

            foreach (SubgraphMatch candidate in ordered)
            {
                bool dominated = filtered.Any(existing =>
                    existing.NodeMap.Count > candidate.NodeMap.Count
                    && candidate.NodeMap.All(pair =>
                        existing.NodeMap.TryGetValue(pair.Key, out Guid mappedID)
                        && mappedID == pair.Value));

                if (!dominated)
                    filtered.Add(candidate);
            }

            return filtered.Take(Math.Max(1, options.MaxMatches)).ToList();
        }

        /// <summary>Builds the ranked candidate graph node list for each query node.</summary>
        private static Dictionary<Guid, List<GraphNodeSnapshot>> BuildCandidateIndex(
            QueryGraph query,
            GraphSnapshot graph,
            ReasoningOptions options)
        {
            Dictionary<Guid, List<GraphNodeSnapshot>> candidates =
                new Dictionary<Guid, List<GraphNodeSnapshot>>();

            foreach (QueryNode queryNode in query.Nodes)
            {
                Dictionary<Guid, GraphNodeSnapshot> matches =
                    new Dictionary<Guid, GraphNodeSnapshot>();

                if (!string.IsNullOrWhiteSpace(queryNode.Label))
                {
                    foreach (GraphNodeSnapshot node in graph.FindByLabel(queryNode.Label, queryNode.Type))
                        matches[node.ID] = node;
                }

                foreach (string alias in queryNode.Aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                        continue;

                    foreach (GraphNodeSnapshot node in graph.FindByLabel(alias, queryNode.Type))
                        matches[node.ID] = node;
                }

                bool hasLabel = !string.IsNullOrWhiteSpace(queryNode.Label)
                    || queryNode.Aliases.Any(alias => !string.IsNullOrWhiteSpace(alias));

                if (!hasLabel || options.AllowTypeOnlyCandidates)
                {
                    foreach (GraphNodeSnapshot node in graph.GetNodesByType(queryNode.Type))
                        matches[node.ID] = node;
                }

                candidates[queryNode.QueryNodeID] = matches.Values
                    .OrderByDescending(node => ScoreNode(queryNode, node, options))
                    .ThenBy(node => node.ID)
                    .Take(Math.Max(1, options.MaxCandidateNodesPerQueryNode))
                    .ToList();
            }

            return candidates;
        }

        /// <summary>Builds an undirected adjacency list of query nodes and their connecting query edges.</summary>
        private static Dictionary<Guid, List<(Guid NeighborID, QueryEdge Edge)>> BuildAdjacency(QueryGraph query)
        {
            Dictionary<Guid, List<(Guid NeighborID, QueryEdge Edge)>> adjacency =
                query.Nodes.ToDictionary(node => node.QueryNodeID, _ => new List<(Guid, QueryEdge)>());

            foreach (QueryEdge edge in query.Edges)
            {
                adjacency[edge.FromQueryNodeID].Add((edge.ToQueryNodeID, edge));
                adjacency[edge.ToQueryNodeID].Add((edge.FromQueryNodeID, edge));
            }

            return adjacency;
        }

        /// <summary>Orders query nodes for search starting from the root or most constrained labeled node.</summary>
        private static List<QueryNode> OrderQueryNodes(
            QueryGraph query,
            Dictionary<Guid, List<(Guid NeighborID, QueryEdge Edge)>> adjacency,
            Dictionary<Guid, List<GraphNodeSnapshot>> candidates)
        {
            Dictionary<Guid, QueryNode> byID = query.Nodes.ToDictionary(node => node.QueryNodeID);
            List<QueryNode> ordered = new List<QueryNode>(query.Nodes.Count);
            HashSet<Guid> visited = new HashSet<Guid>();
            Queue<Guid> queue = new Queue<Guid>();
            Guid searchStart = query.RootQueryNodeID;
            QueryNode rootNode = byID[query.RootQueryNodeID];
            bool rootHasLabel = !string.IsNullOrWhiteSpace(rootNode.Label)
                || rootNode.Aliases.Any(alias => !string.IsNullOrWhiteSpace(alias));
            if (!rootHasLabel)
            {
                QueryNode? constrainedStart = query.Nodes
                    .Where(node => !string.IsNullOrWhiteSpace(node.Label)
                        || node.Aliases.Any(alias => !string.IsNullOrWhiteSpace(alias)))
                    .OrderBy(node => candidates.TryGetValue(
                        node.QueryNodeID,
                        out List<GraphNodeSnapshot>? list)
                        ? list.Count
                        : int.MaxValue)
                    .ThenByDescending(node => adjacency.TryGetValue(
                        node.QueryNodeID,
                        out List<(Guid NeighborID, QueryEdge Edge)>? edges)
                        ? edges.Count
                        : 0)
                    .FirstOrDefault();
                if (constrainedStart is not null)
                    searchStart = constrainedStart.QueryNodeID;
            }
            queue.Enqueue(searchStart);

            while (queue.Count > 0)
            {
                Guid current = queue.Dequeue();

                if (!visited.Add(current))
                    continue;

                if (byID.TryGetValue(current, out QueryNode? node))
                    ordered.Add(node);

                foreach ((Guid neighborID, QueryEdge _) in adjacency[current])
                {
                    if (!visited.Contains(neighborID))
                        queue.Enqueue(neighborID);
                }
            }

            foreach (QueryNode node in query.Nodes)
            {
                if (visited.Add(node.QueryNodeID))
                    ordered.Add(node);
            }

            return ordered;
        }

        /// <summary>Selects candidate graph nodes for a query node, prioritizing neighbors of already-mapped nodes.</summary>
        private static IReadOnlyList<GraphNodeSnapshot> GetCandidatesForSearch(
            QueryNode queryNode,
            Dictionary<Guid, List<(Guid NeighborID, QueryEdge Edge)>> adjacency,
            IReadOnlyDictionary<Guid, Guid> mapping,
            GraphSnapshot graph,
            IReadOnlyList<GraphNodeSnapshot> globalCandidates,
            ReasoningOptions options)
        {
            if (!adjacency.TryGetValue(
                    queryNode.QueryNodeID,
                    out List<(Guid NeighborID, QueryEdge Edge)>? neighbors))
            {
                return globalCandidates;
            }

            List<Guid> mappedNeighbors = neighbors
                .Where(neighbor => mapping.ContainsKey(neighbor.NeighborID))
                .Select(neighbor => mapping[neighbor.NeighborID])
                .Distinct()
                .ToList();
            if (mappedNeighbors.Count == 0)
                return globalCandidates;

            Dictionary<Guid, GraphNodeSnapshot> connectedCandidates = new Dictionary<Guid, GraphNodeSnapshot>();
            foreach (Guid mappedNeighborID in mappedNeighbors)
            {
                foreach (Link link in graph.GetIncidentLinks(mappedNeighborID))
                {
                    Guid candidateID = link.SourceID == mappedNeighborID
                        ? link.DestinationID
                        : link.SourceID;
                    if (candidateID == mappedNeighborID
                        || !graph.TryGetNode(candidateID, out GraphNodeSnapshot candidate)
                        || !MatchesCandidateConstraints(queryNode, candidate, options))
                    {
                        continue;
                    }

                    connectedCandidates[candidateID] = candidate;
                }
            }

            List<GraphNodeSnapshot> orderedCandidates = connectedCandidates.Values
                .OrderByDescending(candidate => ScoreNode(queryNode, candidate, options))
                .ThenBy(candidate => candidate.ID)
                .ToList();
            HashSet<Guid> included = connectedCandidates.Keys.ToHashSet();
            foreach (GraphNodeSnapshot candidate in globalCandidates)
            {
                if (included.Add(candidate.ID))
                    orderedCandidates.Add(candidate);
            }

            return orderedCandidates
                .Take(Math.Max(1, options.MaxCandidateNodesPerQueryNode))
                .ToList();
        }

        /// <summary>Determines whether a graph node satisfies the type and label constraints of a query node.</summary>
        private static bool MatchesCandidateConstraints(
            QueryNode queryNode,
            GraphNodeSnapshot candidate,
            ReasoningOptions options)
        {
            if (!TypesEqual(queryNode.Type, candidate.Type))
                return false;

            if (options.AllowTypeOnlyCandidates)
                return true;

            bool hasLabel = !string.IsNullOrWhiteSpace(queryNode.Label)
                || queryNode.Aliases.Any(alias => !string.IsNullOrWhiteSpace(alias));
            if (!hasLabel)
                return true;

            if (!string.IsNullOrWhiteSpace(queryNode.Label)
                && (string.Equals(queryNode.Label, candidate.Label, StringComparison.OrdinalIgnoreCase)
                    || candidate.Aliases.Any(alias => string.Equals(
                        alias,
                        queryNode.Label,
                        StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            return queryNode.Aliases.Any(alias =>
                !string.IsNullOrWhiteSpace(alias)
                && (string.Equals(alias, candidate.Label, StringComparison.OrdinalIgnoreCase)
                    || candidate.Aliases.Any(candidateAlias => string.Equals(
                        candidateAlias,
                        alias,
                        StringComparison.OrdinalIgnoreCase))));
        }

        /// <summary>Indexes snapshot links by ordered endpoint node ID pairs for fast lookup.</summary>
        private static Dictionary<(Guid, Guid), List<Link>> BuildLinkIndex(GraphSnapshot graph)
        {
            Dictionary<(Guid, Guid), List<Link>> linksByPair =
                new Dictionary<(Guid, Guid), List<Link>>();

            foreach (Link link in graph.Links)
            {
                (Guid, Guid) key = link.SourceID.CompareTo(link.DestinationID) <= 0
                    ? (link.SourceID, link.DestinationID)
                    : (link.DestinationID, link.SourceID);

                if (!linksByPair.TryGetValue(key, out List<Link>? links))
                {
                    links = new List<Link>();
                    linksByPair[key] = links;
                }

                links.Add(link);
            }

            return linksByPair;
        }

        /// <summary>Attempts to find the highest-scoring link between two graph nodes that matches a query edge.</summary>
        private static bool TryMatchEdge(
            QueryEdge queryEdge,
            Guid fromGraphNodeID,
            Guid toGraphNodeID,
            Dictionary<(Guid, Guid), List<Link>> linksByPair,
            ReasoningOptions options,
            out Link? matchedLink,
            out double score)
        {
            matchedLink = null;
            score = 0.0;

            if (fromGraphNodeID == toGraphNodeID)
                return false;

            (Guid, Guid) key = fromGraphNodeID.CompareTo(toGraphNodeID) <= 0
                ? (fromGraphNodeID, toGraphNodeID)
                : (toGraphNodeID, fromGraphNodeID);

            if (!linksByPair.TryGetValue(key, out List<Link>? links))
                return false;

            double bestScore = -1.0;
            Link? bestLink = null;

            foreach (Link link in links)
            {
                bool direct = link.SourceID == fromGraphNodeID && link.DestinationID == toGraphNodeID;
                bool inverse = link.SourceID == toGraphNodeID && link.DestinationID == fromGraphNodeID;

                if (!direct && !inverse)
                    continue;

                double candidateScore = ScoreEdge(queryEdge, link, direct, options);

                if (candidateScore > bestScore)
                {
                    bestScore = candidateScore;
                    bestLink = link;
                }
            }

            if (bestLink is null || bestScore < 0.0)
                return false;

            matchedLink = bestLink;
            score = bestScore;
            return true;
        }

        /// <summary>Scores how well a candidate graph link matches a query edge and its direction.</summary>
        private static double ScoreEdge(
            QueryEdge queryEdge,
            Link link,
            bool direct,
            ReasoningOptions options)
        {
            bool classMatches = link.Classification == queryEdge.Classification;

            if (!classMatches)
            {
                return options.AllowClassOnlyEdgeMatching
                    ? options.ClassOnlyEdgeScore * 0.5
                    : -1.0;
            }

            if (direct)
            {
                if (link.ForwardLink.Id == queryEdge.ForwardLinkType)
                    return options.ExactEdgeScore;

                return options.AllowClassOnlyEdgeMatching
                    ? options.ClassOnlyEdgeScore
                    : -1.0;
            }

            if (link.ForwardLink.Id == queryEdge.InverseLinkType)
                return options.InverseEdgeScore;

            return options.AllowClassOnlyEdgeMatching
                ? options.ClassOnlyEdgeScore
                : -1.0;
        }

        /// <summary>Scores how well a candidate graph node matches the type, label, and aliases of a query node.</summary>
        private static double ScoreNode(
            QueryNode queryNode,
            GraphNodeSnapshot graphNode,
            ReasoningOptions options)
        {
            if (!TypesEqual(queryNode.Type, graphNode.Type))
                return 0.0;

            if (!string.IsNullOrWhiteSpace(queryNode.Label))
            {
                if (string.Equals(queryNode.Label, graphNode.Label, StringComparison.OrdinalIgnoreCase))
                    return options.ExactNodeScore;

                if (graphNode.Aliases.Any(alias =>
                    string.Equals(alias, queryNode.Label, StringComparison.OrdinalIgnoreCase)))
                {
                    return options.AliasNodeScore;
                }
            }

            foreach (string alias in queryNode.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                if (string.Equals(alias, graphNode.Label, StringComparison.OrdinalIgnoreCase)
                    || graphNode.Aliases.Any(existing =>
                        string.Equals(existing, alias, StringComparison.OrdinalIgnoreCase)))
                {
                    return options.AliasNodeScore;
                }
            }

            return options.TypeOnlyNodeScore;
        }

        /// <summary>Determines whether two node type descriptors represent the same node type.</summary>
        private static bool TypesEqual(NodeTypes left, NodeTypes right)
        {
            return ReferenceEquals(left, right) || left.Id == right.Id;
        }

        /// <summary>Builds a canonical identity key for a graph link.</summary>
        private static string GetLinkKey(Link link)
        {
            return string.Join(
                "|",
                link.SourceID,
                link.DestinationID,
                link.Classification.Id,
                link.ForwardLink.Id,
                link.InverseLink.Id);
        }
    }
}
