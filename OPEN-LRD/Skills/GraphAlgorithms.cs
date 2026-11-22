using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OPEN_LRD.Skills
{
    internal readonly record struct GraphEdge<TNode>(TNode From, TNode To, double Weight, bool Directed)
        where TNode : notnull
    {
        public override string ToString()
        {
            return $"{From} -> {To} ({Weight})";
        }
    }

    internal sealed class Graph<TNode>
        where TNode : notnull
    {
        private readonly Dictionary<TNode, List<GraphEdge<TNode>>> outgoingEdges = new Dictionary<TNode, List<GraphEdge<TNode>>>();
        private readonly Dictionary<TNode, List<GraphEdge<TNode>>> incomingEdges = new Dictionary<TNode, List<GraphEdge<TNode>>>();
        private readonly List<TNode> nodes = new List<TNode>();
        private readonly List<GraphEdge<TNode>> edges = new List<GraphEdge<TNode>>();

        public Graph(bool directed = true)
        {
            IsDirected = directed;
        }

        public bool IsDirected { get; }

        public int NodeCount => nodes.Count;

        public int EdgeCount => edges.Count;

        public IReadOnlyList<TNode> Nodes => new ReadOnlyCollection<TNode>(new List<TNode>(nodes));

        public IReadOnlyList<GraphEdge<TNode>> Edges => new ReadOnlyCollection<GraphEdge<TNode>>(new List<GraphEdge<TNode>>(edges));

        public void AddNode(TNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            if (outgoingEdges.ContainsKey(node))
            {
                return;
            }
            nodes.Add(node);
            outgoingEdges.Add(node, new List<GraphEdge<TNode>>());
            incomingEdges.Add(node, new List<GraphEdge<TNode>>());
        }

        /// <summary>
        /// Adds one finite-weight edge. Negative weights and parallel/duplicate
        /// insertions are retained; algorithms that cannot support negative
        /// weights validate their own preconditions. For undirected graphs,
        /// EdgeCount and Edges count each inserted edge once while adjacency
        /// contains the generated reverse view.
        /// </summary>
        public void AddEdge(TNode from, TNode to, double weight = 1.0)
        {
            if (!double.IsFinite(weight))
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "The edge weight must be finite.");
            }
            AddNode(from);
            AddNode(to);
            GraphEdge<TNode> edge = new GraphEdge<TNode>(from, to, weight, IsDirected);
            edges.Add(edge);
            outgoingEdges[from].Add(edge);
            incomingEdges[to].Add(edge);
            if (!IsDirected)
            {
                GraphEdge<TNode> reverse = new GraphEdge<TNode>(to, from, weight, false);
                outgoingEdges[to].Add(reverse);
                incomingEdges[from].Add(reverse);
            }
        }

        public bool ContainsNode(TNode node)
        {
            if (node is null)
            {
                return false;
            }
            return outgoingEdges.ContainsKey(node);
        }

        public IReadOnlyList<GraphEdge<TNode>> GetOutgoingEdges(TNode node)
        {
            if (node is null || !outgoingEdges.TryGetValue(node, out List<GraphEdge<TNode>>? result) || result is null)
            {
                throw new ArgumentException("The node is not present in the graph.", nameof(node));
            }
            return result.AsReadOnly();
        }

        public IReadOnlyList<GraphEdge<TNode>> GetIncomingEdges(TNode node)
        {
            if (node is null || !incomingEdges.TryGetValue(node, out List<GraphEdge<TNode>>? result) || result is null)
            {
                throw new ArgumentException("The node is not present in the graph.", nameof(node));
            }
            return result.AsReadOnly();
        }

        public IReadOnlyList<TNode> GetNeighbors(TNode node)
        {
            IReadOnlyList<GraphEdge<TNode>> outgoing = GetOutgoingEdges(node);
            List<TNode> result = new List<TNode>(outgoing.Count);
            foreach (GraphEdge<TNode> edge in outgoing)
            {
                result.Add(edge.To);
            }
            return result.AsReadOnly();
        }
    }

    internal sealed record GraphTraversal<TNode>(IReadOnlyList<TNode> Order, IReadOnlyDictionary<TNode, TNode> Parents)
        where TNode : notnull;

    internal sealed record ShortestPathResult<TNode>(
        IReadOnlyDictionary<TNode, double> Distances,
        IReadOnlyDictionary<TNode, TNode> Previous,
        bool HasNegativeCycle)
        where TNode : notnull
    {
        public bool TryGetDistance(TNode node, out double distance)
        {
            return Distances.TryGetValue(node, out distance);
        }

        public bool TryGetPath(TNode target, out IReadOnlyList<TNode> path)
        {
            if (!Distances.ContainsKey(target))
            {
                path = Array.Empty<TNode>();
                return false;
            }
            List<TNode> reversed = new List<TNode>();
            HashSet<TNode> seen = new HashSet<TNode>();
            TNode current = target;
            while (true)
            {
                if (!seen.Add(current))
                {
                    path = Array.Empty<TNode>();
                    return false;
                }
                reversed.Add(current);
                if (!Previous.TryGetValue(current, out TNode? parent) || parent is null)
                {
                    break;
                }
                current = parent;
            }
            reversed.Reverse();
            path = reversed;
            return true;
        }
    }

    internal sealed record AllPairsShortestPathResult<TNode>(
        IReadOnlyDictionary<TNode, IReadOnlyDictionary<TNode, double>> Distances,
        bool HasNegativeCycle)
        where TNode : notnull
    {
        public bool TryGetDistance(TNode from, TNode to, out double distance)
        {
            if (Distances.TryGetValue(from, out IReadOnlyDictionary<TNode, double>? row) && row is not null
                && row.TryGetValue(to, out distance))
            {
                return true;
            }
            distance = double.PositiveInfinity;
            return false;
        }
    }

    internal sealed record TopologicalSortResult<TNode>(bool Success, IReadOnlyList<TNode> Order)
        where TNode : notnull;

    internal sealed record MinimumSpanningTreeResult<TNode>(IReadOnlyList<GraphEdge<TNode>> Edges, double TotalWeight)
        where TNode : notnull;

    internal sealed record MaximumFlowResult<TNode>(
        double MaximumFlow,
        IReadOnlyDictionary<(TNode From, TNode To), double> Flows)
        where TNode : notnull;

    internal static class GraphAlgorithms
    {
        private const double Tolerance = 1e-9;

        public static GraphTraversal<TNode> BreadthFirstTraversal<TNode>(Graph<TNode> graph, TNode start)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            EnsureNode(graph, start, nameof(start));
            List<TNode> order = new List<TNode>();
            Dictionary<TNode, TNode> parents = new Dictionary<TNode, TNode>();
            HashSet<TNode> visited = new HashSet<TNode>();
            Queue<TNode> queue = new Queue<TNode>();
            visited.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                TNode node = queue.Dequeue();
                order.Add(node);
                foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                {
                    if (visited.Add(edge.To))
                    {
                        parents[edge.To] = node;
                        queue.Enqueue(edge.To);
                    }
                }
            }
            return new GraphTraversal<TNode>(order, parents);
        }

        public static GraphTraversal<TNode> DepthFirstTraversal<TNode>(Graph<TNode> graph, TNode start)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            EnsureNode(graph, start, nameof(start));
            List<TNode> order = new List<TNode>();
            Dictionary<TNode, TNode> parents = new Dictionary<TNode, TNode>();
            HashSet<TNode> visited = new HashSet<TNode>();
            Stack<TNode> stack = new Stack<TNode>();
            visited.Add(start);
            stack.Push(start);
            while (stack.Count > 0)
            {
                TNode node = stack.Pop();
                order.Add(node);
                IReadOnlyList<GraphEdge<TNode>> outgoing = graph.GetOutgoingEdges(node);
                for (int index = outgoing.Count - 1; index >= 0; index--)
                {
                    TNode neighbor = outgoing[index].To;
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        parents[neighbor] = node;
                        stack.Push(neighbor);
                    }
                }
            }
            return new GraphTraversal<TNode>(order, parents);
        }

        public static ShortestPathResult<TNode> Dijkstra<TNode>(Graph<TNode> graph, TNode start)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            EnsureNode(graph, start, nameof(start));
            foreach (GraphEdge<TNode> edge in graph.Edges)
            {
                if (edge.Weight < -Tolerance)
                {
                    throw new InvalidOperationException("Dijkstra's algorithm requires non-negative edge weights.");
                }
            }
            Dictionary<TNode, double> distances = new Dictionary<TNode, double>();
            Dictionary<TNode, TNode> previous = new Dictionary<TNode, TNode>();
            HashSet<TNode> settled = new HashSet<TNode>();
            PriorityQueue<TNode, (double Distance, long Sequence)> queue = new PriorityQueue<TNode, (double Distance, long Sequence)>();
            long sequence = 0L;
            distances[start] = 0.0;
            queue.Enqueue(start, (0.0, sequence++));
            while (queue.TryDequeue(out TNode? node, out (double Distance, long Sequence) priority))
            {
                if (node is null || !settled.Add(node))
                {
                    continue;
                }
                foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                {
                    if (settled.Contains(edge.To))
                    {
                        continue;
                    }
                    double candidate = priority.Distance + edge.Weight;
                    if (!distances.TryGetValue(edge.To, out double current) || candidate < current - Tolerance)
                    {
                        distances[edge.To] = candidate;
                        previous[edge.To] = node;
                        queue.Enqueue(edge.To, (candidate, sequence++));
                    }
                }
            }
            return new ShortestPathResult<TNode>(distances, previous, false);
        }

        public static ShortestPathResult<TNode> BellmanFord<TNode>(Graph<TNode> graph, TNode start)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            EnsureNode(graph, start, nameof(start));
            Dictionary<TNode, double> distances = new Dictionary<TNode, double>();
            Dictionary<TNode, TNode> previous = new Dictionary<TNode, TNode>();
            distances[start] = 0.0;
            for (int iteration = 0; iteration < graph.NodeCount - 1; iteration++)
            {
                bool updated = false;
                foreach (TNode node in graph.Nodes)
                {
                    if (!distances.TryGetValue(node, out double distance))
                    {
                        continue;
                    }
                    foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                    {
                        double candidate = distance + edge.Weight;
                        if (!distances.TryGetValue(edge.To, out double current) || candidate < current - Tolerance)
                        {
                            distances[edge.To] = candidate;
                            previous[edge.To] = node;
                            updated = true;
                        }
                    }
                }
                if (!updated)
                {
                    break;
                }
            }
            bool hasNegativeCycle = false;
            foreach (TNode node in graph.Nodes)
            {
                if (!distances.TryGetValue(node, out double distance))
                {
                    continue;
                }
                foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                {
                    if (!distances.TryGetValue(edge.To, out double current) || distance + edge.Weight < current - Tolerance)
                    {
                        hasNegativeCycle = true;
                        break;
                    }
                }
                if (hasNegativeCycle)
                {
                    break;
                }
            }
            return new ShortestPathResult<TNode>(distances, previous, hasNegativeCycle);
        }

        public static AllPairsShortestPathResult<TNode> FloydWarshall<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            Dictionary<TNode, Dictionary<TNode, double>> distances = new Dictionary<TNode, Dictionary<TNode, double>>();
            foreach (TNode node in graph.Nodes)
            {
                Dictionary<TNode, double> row = new Dictionary<TNode, double>();
                foreach (TNode other in graph.Nodes)
                {
                    row[other] = EqualityComparer<TNode>.Default.Equals(node, other) ? 0.0 : double.PositiveInfinity;
                }
                distances[node] = row;
            }
            foreach (TNode node in graph.Nodes)
            {
                foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                {
                    if (edge.Weight < distances[node][edge.To])
                    {
                        distances[node][edge.To] = edge.Weight;
                    }
                }
            }
            foreach (TNode pivot in graph.Nodes)
            {
                Dictionary<TNode, double> pivotRow = distances[pivot];
                foreach (TNode from in graph.Nodes)
                {
                    Dictionary<TNode, double> fromRow = distances[from];
                    double firstLeg = fromRow[pivot];
                    if (double.IsPositiveInfinity(firstLeg))
                    {
                        continue;
                    }
                    foreach (TNode to in graph.Nodes)
                    {
                        double candidate = firstLeg + pivotRow[to];
                        if (candidate < fromRow[to] - Tolerance)
                        {
                            fromRow[to] = candidate;
                        }
                    }
                }
            }
            bool hasNegativeCycle = false;
            foreach (TNode node in graph.Nodes)
            {
                if (distances[node][node] < -Tolerance)
                {
                    hasNegativeCycle = true;
                    break;
                }
            }
            Dictionary<TNode, IReadOnlyDictionary<TNode, double>> result = new Dictionary<TNode, IReadOnlyDictionary<TNode, double>>();
            foreach (TNode node in graph.Nodes)
            {
                result[node] = distances[node];
            }
            return new AllPairsShortestPathResult<TNode>(result, hasNegativeCycle);
        }

        public static TopologicalSortResult<TNode> TopologicalSort<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            if (!graph.IsDirected)
            {
                throw new InvalidOperationException("Topological sorting requires a directed graph.");
            }
            Dictionary<TNode, int> inDegree = new Dictionary<TNode, int>();
            foreach (TNode node in graph.Nodes)
            {
                inDegree[node] = graph.GetIncomingEdges(node).Count;
            }
            Queue<TNode> queue = new Queue<TNode>();
            foreach (TNode node in graph.Nodes)
            {
                if (inDegree[node] == 0)
                {
                    queue.Enqueue(node);
                }
            }
            List<TNode> order = new List<TNode>();
            while (queue.Count > 0)
            {
                TNode node = queue.Dequeue();
                order.Add(node);
                foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                {
                    inDegree[edge.To]--;
                    if (inDegree[edge.To] == 0)
                    {
                        queue.Enqueue(edge.To);
                    }
                }
            }
            return new TopologicalSortResult<TNode>(order.Count == graph.NodeCount, order);
        }

        public static bool HasCycle<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            if (graph.IsDirected)
            {
                return !TopologicalSort(graph).Success;
            }
            HashSet<TNode> visited = new HashSet<TNode>();
            foreach (TNode start in graph.Nodes)
            {
                if (!visited.Add(start))
                {
                    continue;
                }
                Stack<(TNode Node, TNode Parent, bool HasParent)> stack = new Stack<(TNode Node, TNode Parent, bool HasParent)>();
                stack.Push((start, start, false));
                while (stack.Count > 0)
                {
                    (TNode node, TNode parent, bool hasParent) = stack.Pop();
                    foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
                    {
                        TNode neighbor = edge.To;
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            stack.Push((neighbor, node, true));
                        }
                        else if (!hasParent || !EqualityComparer<TNode>.Default.Equals(neighbor, parent))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static IReadOnlyList<IReadOnlyList<TNode>> ConnectedComponents<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            HashSet<TNode> visited = new HashSet<TNode>();
            List<IReadOnlyList<TNode>> components = new List<IReadOnlyList<TNode>>();
            foreach (TNode start in graph.Nodes)
            {
                if (visited.Contains(start))
                {
                    continue;
                }
                List<TNode> component = new List<TNode>();
                Queue<TNode> queue = new Queue<TNode>();
                visited.Add(start);
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    TNode node = queue.Dequeue();
                    component.Add(node);
                    foreach (TNode neighbor in GetWeakNeighbors(graph, node))
                    {
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                components.Add(component);
            }
            return components;
        }

        public static IReadOnlyList<IReadOnlyList<TNode>> StronglyConnectedComponents<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            if (!graph.IsDirected)
            {
                return ConnectedComponents(graph);
            }
            HashSet<TNode> visited = new HashSet<TNode>();
            List<TNode> finishOrder = new List<TNode>();
            foreach (TNode start in graph.Nodes)
            {
                if (visited.Contains(start))
                {
                    continue;
                }
                Stack<(TNode Node, int Index)> stack = new Stack<(TNode Node, int Index)>();
                visited.Add(start);
                stack.Push((start, 0));
                while (stack.Count > 0)
                {
                    (TNode node, int index) = stack.Pop();
                    IReadOnlyList<GraphEdge<TNode>> outgoing = graph.GetOutgoingEdges(node);
                    while (index < outgoing.Count && visited.Contains(outgoing[index].To))
                    {
                        index++;
                    }
                    if (index < outgoing.Count)
                    {
                        TNode next = outgoing[index].To;
                        stack.Push((node, index + 1));
                        visited.Add(next);
                        stack.Push((next, 0));
                    }
                    else
                    {
                        finishOrder.Add(node);
                    }
                }
            }
            HashSet<TNode> assigned = new HashSet<TNode>();
            List<IReadOnlyList<TNode>> components = new List<IReadOnlyList<TNode>>();
            for (int index = finishOrder.Count - 1; index >= 0; index--)
            {
                TNode start = finishOrder[index];
                if (!assigned.Add(start))
                {
                    continue;
                }
                List<TNode> component = new List<TNode>();
                Stack<TNode> stack = new Stack<TNode>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    TNode node = stack.Pop();
                    component.Add(node);
                    foreach (GraphEdge<TNode> edge in graph.GetIncomingEdges(node))
                    {
                        if (assigned.Add(edge.From))
                        {
                            stack.Push(edge.From);
                        }
                    }
                }
                components.Add(component);
            }
            return components;
        }

        public static MinimumSpanningTreeResult<TNode> MinimumSpanningTree<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            if (graph.IsDirected)
            {
                throw new InvalidOperationException("Minimum spanning trees require an undirected graph.");
            }
            Dictionary<TNode, int> indices = BuildNodeIndices(graph);
            List<(GraphEdge<TNode> Edge, int From, int To)> candidates = new List<(GraphEdge<TNode> Edge, int From, int To)>();
            foreach (GraphEdge<TNode> edge in graph.Edges)
            {
                candidates.Add((edge, indices[edge.From], indices[edge.To]));
            }
            candidates.Sort((first, second) =>
            {
                int comparison = first.Edge.Weight.CompareTo(second.Edge.Weight);
                if (comparison != 0)
                {
                    return comparison;
                }
                comparison = first.From.CompareTo(second.From);
                return comparison != 0 ? comparison : first.To.CompareTo(second.To);
            });
            UnionFind unionFind = new UnionFind(graph.NodeCount);
            List<GraphEdge<TNode>> selected = new List<GraphEdge<TNode>>();
            double totalWeight = 0.0;
            foreach ((GraphEdge<TNode> edge, int from, int to) in candidates)
            {
                if (unionFind.Union(from, to))
                {
                    selected.Add(edge);
                    totalWeight += edge.Weight;
                }
            }
            return new MinimumSpanningTreeResult<TNode>(selected, totalWeight);
        }

        public static MaximumFlowResult<TNode> MaximumFlow<TNode>(Graph<TNode> graph, TNode source, TNode sink)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            EnsureNode(graph, source, nameof(source));
            EnsureNode(graph, sink, nameof(sink));
            if (EqualityComparer<TNode>.Default.Equals(source, sink))
            {
                throw new ArgumentException("The source and sink must be different nodes.");
            }
            Dictionary<TNode, int> indices = BuildNodeIndices(graph);
            int size = graph.NodeCount;
            int sourceIndex = indices[source];
            int sinkIndex = indices[sink];
            double[,] residual = new double[size, size];
            foreach (GraphEdge<TNode> edge in graph.Edges)
            {
                if (edge.Weight < -Tolerance)
                {
                    throw new InvalidOperationException("Maximum flow requires non-negative capacities.");
                }
                int from = indices[edge.From];
                int to = indices[edge.To];
                residual[from, to] += Math.Max(edge.Weight, 0.0);
                if (!graph.IsDirected)
                {
                    residual[to, from] += Math.Max(edge.Weight, 0.0);
                }
            }
            double[,] flow = new double[size, size];
            double maximumFlow = 0.0;
            while (true)
            {
                int[] parent = new int[size];
                for (int index = 0; index < size; index++)
                {
                    parent[index] = -1;
                }
                parent[sourceIndex] = sourceIndex;
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(sourceIndex);
                while (queue.Count > 0 && parent[sinkIndex] < 0)
                {
                    int node = queue.Dequeue();
                    for (int next = 0; next < size; next++)
                    {
                        if (parent[next] < 0 && residual[node, next] > Tolerance)
                        {
                            parent[next] = node;
                            queue.Enqueue(next);
                        }
                    }
                }
                if (parent[sinkIndex] < 0)
                {
                    break;
                }
                double bottleneck = double.PositiveInfinity;
                int current = sinkIndex;
                while (current != sourceIndex)
                {
                    int previous = parent[current];
                    bottleneck = Math.Min(bottleneck, residual[previous, current]);
                    current = previous;
                }
                current = sinkIndex;
                while (current != sourceIndex)
                {
                    int previous = parent[current];
                    residual[previous, current] -= bottleneck;
                    residual[current, previous] += bottleneck;
                    flow[previous, current] += bottleneck;
                    current = previous;
                }
                maximumFlow += bottleneck;
            }
            Dictionary<(TNode From, TNode To), double> flows = new Dictionary<(TNode From, TNode To), double>();
            for (int from = 0; from < size; from++)
            {
                for (int to = 0; to < size; to++)
                {
                    if (flow[from, to] > Tolerance)
                    {
                        TNode fromNode = graph.Nodes[from];
                        TNode toNode = graph.Nodes[to];
                        (TNode From, TNode To) key = (fromNode, toNode);
                        flows[key] = flows.TryGetValue(key, out double existing) ? existing + flow[from, to] : flow[from, to];
                    }
                }
            }
            return new MaximumFlowResult<TNode>(maximumFlow, flows);
        }

        public static IReadOnlyDictionary<TNode, double> PageRank<TNode>(
            Graph<TNode> graph,
            double dampingFactor = 0.85,
            double tolerance = 1e-9,
            int maxIterations = 100)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            if (dampingFactor < 0.0 || dampingFactor >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(dampingFactor), "The damping factor must be in the range [0, 1).");
            }
            if (tolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "The tolerance must be positive.");
            }
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations), "The iteration count must be positive.");
            }
            int count = graph.NodeCount;
            if (count == 0)
            {
                return new Dictionary<TNode, double>();
            }
            Dictionary<TNode, int> indices = BuildNodeIndices(graph);
            int[] outDegree = new int[count];
            double[] ranks = new double[count];
            for (int index = 0; index < count; index++)
            {
                outDegree[index] = graph.GetOutgoingEdges(graph.Nodes[index]).Count;
                ranks[index] = 1.0 / count;
            }
            double initial = (1.0 - dampingFactor) / count;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] next = new double[count];
                for (int index = 0; index < count; index++)
                {
                    next[index] = initial;
                }
                double dangling = 0.0;
                for (int index = 0; index < count; index++)
                {
                    if (outDegree[index] == 0)
                    {
                        dangling += ranks[index];
                    }
                }
                double danglingShare = dampingFactor * dangling / count;
                for (int index = 0; index < count; index++)
                {
                    next[index] += danglingShare;
                }
                for (int index = 0; index < count; index++)
                {
                    if (outDegree[index] == 0)
                    {
                        continue;
                    }
                    double share = dampingFactor * ranks[index] / outDegree[index];
                    foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(graph.Nodes[index]))
                    {
                        next[indices[edge.To]] += share;
                    }
                }
                double difference = 0.0;
                for (int index = 0; index < count; index++)
                {
                    difference += Math.Abs(next[index] - ranks[index]);
                }
                ranks = next;
                if (difference <= tolerance)
                {
                    break;
                }
            }
            Dictionary<TNode, double> result = new Dictionary<TNode, double>();
            for (int index = 0; index < count; index++)
            {
                result[graph.Nodes[index]] = ranks[index];
            }
            return result;
        }

        public static bool IsBipartite<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            EnsureGraph(graph, nameof(graph));
            Dictionary<TNode, int> colors = new Dictionary<TNode, int>();
            foreach (TNode start in graph.Nodes)
            {
                if (colors.ContainsKey(start))
                {
                    continue;
                }
                colors[start] = 0;
                Queue<TNode> queue = new Queue<TNode>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    TNode node = queue.Dequeue();
                    int color = colors[node];
                    foreach (TNode neighbor in GetWeakNeighbors(graph, node))
                    {
                        if (!colors.TryGetValue(neighbor, out int neighborColor))
                        {
                            colors[neighbor] = 1 - color;
                            queue.Enqueue(neighbor);
                        }
                        else if (neighborColor == color)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static Dictionary<TNode, int> BuildNodeIndices<TNode>(Graph<TNode> graph)
            where TNode : notnull
        {
            Dictionary<TNode, int> result = new Dictionary<TNode, int>();
            for (int index = 0; index < graph.NodeCount; index++)
            {
                result[graph.Nodes[index]] = index;
            }
            return result;
        }

        private static List<TNode> GetWeakNeighbors<TNode>(Graph<TNode> graph, TNode node)
            where TNode : notnull
        {
            List<TNode> result = new List<TNode>();
            foreach (GraphEdge<TNode> edge in graph.GetOutgoingEdges(node))
            {
                result.Add(edge.To);
            }
            foreach (GraphEdge<TNode> edge in graph.GetIncomingEdges(node))
            {
                result.Add(edge.From);
            }
            return result;
        }

        private static void EnsureGraph<TNode>(Graph<TNode> graph, string parameterName)
            where TNode : notnull
        {
            if (graph is null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        private static void EnsureNode<TNode>(Graph<TNode> graph, TNode node, string parameterName)
            where TNode : notnull
        {
            if (node is null || !graph.ContainsNode(node))
            {
                throw new ArgumentException("The node is not present in the graph.", parameterName);
            }
        }

        private sealed class UnionFind
        {
            private readonly int[] parents;
            private readonly int[] ranks;

            public UnionFind(int size)
            {
                parents = new int[size];
                ranks = new int[size];
                for (int index = 0; index < size; index++)
                {
                    parents[index] = index;
                }
            }

            public int Find(int value)
            {
                if (parents[value] != value)
                {
                    parents[value] = Find(parents[value]);
                }
                return parents[value];
            }

            public bool Union(int first, int second)
            {
                int firstRoot = Find(first);
                int secondRoot = Find(second);
                if (firstRoot == secondRoot)
                {
                    return false;
                }
                if (ranks[firstRoot] < ranks[secondRoot])
                {
                    parents[firstRoot] = secondRoot;
                }
                else if (ranks[firstRoot] > ranks[secondRoot])
                {
                    parents[secondRoot] = firstRoot;
                }
                else
                {
                    parents[secondRoot] = firstRoot;
                    ranks[firstRoot]++;
                }
                return true;
            }
        }
    }
}
