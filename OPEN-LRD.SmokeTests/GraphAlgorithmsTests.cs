using System;
using System.Collections.Generic;
using System.Linq;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class GraphAlgorithmsTests
    {
        public static void RunAll()
        {
            Run("Breadth-first traversal", TestBreadthFirstTraversal);
            Run("Depth-first traversal", TestDepthFirstTraversal);
            Run("Dijkstra shortest paths", TestDijkstra);
            Run("Bellman-Ford shortest paths", TestBellmanFord);
            Run("Floyd-Warshall shortest paths", TestFloydWarshall);
            Run("Topological ordering and cycles", TestTopologicalOrdering);
            Run("Connected components and bipartiteness", TestComponentsAndBipartiteness);
            Run("Strongly connected components", TestStronglyConnectedComponents);
            Run("Minimum spanning tree", TestMinimumSpanningTree);
            Run("Maximum flow", TestMaximumFlow);
            Run("PageRank distribution", TestPageRank);
            Run("Graph results are isolated and confidence-weighted", TestImmutableResultsAndWeightedPageRank);
            Run("Graph validation", TestGraphValidation);
        }

        private static Graph<string> NewUndirectedGraph()
        {
            Graph<string> graph = new Graph<string>(false);
            graph.AddEdge("A", "B", 1.0);
            graph.AddEdge("A", "C", 4.0);
            graph.AddEdge("B", "C", 2.0);
            graph.AddEdge("B", "D", 5.0);
            graph.AddEdge("C", "D", 1.0);
            graph.AddEdge("D", "E", 3.0);
            graph.AddNode("F");
            return graph;
        }

        private static void TestBreadthFirstTraversal()
        {
            Graph<string> graph = NewUndirectedGraph();
            GraphTraversal<string> traversal = GraphAlgorithms.BreadthFirstTraversal(graph, "A");
            Assert(string.Join(",", traversal.Order) == "A,B,C,D,E", "breadth-first order");
            Assert(traversal.Parents["D"] == "B", "breadth-first parent");
        }

        private static void TestDepthFirstTraversal()
        {
            Graph<string> graph = NewUndirectedGraph();
            GraphTraversal<string> traversal = GraphAlgorithms.DepthFirstTraversal(graph, "A");
            Assert(string.Join(",", traversal.Order) == "A,B,D,E,C", "depth-first order");
            Assert(traversal.Parents["E"] == "D", "depth-first parent");
        }

        private static void TestDijkstra()
        {
            Graph<string> graph = NewUndirectedGraph();
            ShortestPathResult<string> result = GraphAlgorithms.Dijkstra(graph, "A");
            AssertEqual(result.Distances["D"], 4.0, "Dijkstra distance");
            Assert(result.TryGetPath("D", out IReadOnlyList<string> path), "Dijkstra path should exist");
            Assert(string.Join(",", path) == "A,B,C,D", "Dijkstra path");
            Assert(!result.HasNegativeCycle, "Dijkstra should not report a negative cycle");

            Graph<int> negative = new Graph<int>(true);
            negative.AddEdge(0, 1, -1.0);
            AssertThrows<InvalidOperationException>(
                () => GraphAlgorithms.Dijkstra(negative, 0),
                "Dijkstra should reject negative weights");
        }

        private static void TestBellmanFord()
        {
            Graph<string> graph = NewUndirectedGraph();
            ShortestPathResult<string> result = GraphAlgorithms.BellmanFord(graph, "A");
            AssertEqual(result.Distances["D"], 4.0, "Bellman-Ford distance");
            Assert(!result.HasNegativeCycle, "Bellman-Ford should not report a negative cycle");

            Graph<int> negativeCycle = new Graph<int>(true);
            negativeCycle.AddEdge(0, 1, 1.0);
            negativeCycle.AddEdge(1, 2, -2.0);
            negativeCycle.AddEdge(2, 1, 0.5);
            Assert(GraphAlgorithms.BellmanFord(negativeCycle, 0).HasNegativeCycle, "negative cycle should be detected");
        }

        private static void TestFloydWarshall()
        {
            Graph<string> graph = NewUndirectedGraph();
            AllPairsShortestPathResult<string> result = GraphAlgorithms.FloydWarshall(graph);
            AssertEqual(result.Distances["A"]["D"], 4.0, "Floyd-Warshall distance");
            AssertEqual(result.Distances["A"]["F"], double.PositiveInfinity, "Floyd-Warshall unreachable distance");
            Assert(!result.HasNegativeCycle, "Floyd-Warshall should not report a negative cycle");
            AssertThrows<NotSupportedException>(() => ((IDictionary<string, double>)result.Distances["A"]).Clear(),
                "all-pairs rows should be defensive read-only copies");
        }

        private static void TestTopologicalOrdering()
        {
            Graph<string> graph = new Graph<string>(true);
            graph.AddEdge("A", "B");
            graph.AddEdge("A", "C");
            graph.AddEdge("B", "D");
            graph.AddEdge("C", "D");

            TopologicalSortResult<string> result = GraphAlgorithms.TopologicalSort(graph);
            Assert(result.Success, "topological sort should succeed");
            Assert(string.Join(",", result.Order) == "A,B,C,D", "topological order");
            AssertThrows<NotSupportedException>(() => ((IList<string>)result.Order).Clear(),
                "topological order should be immutable");
            Assert(!GraphAlgorithms.HasCycle(graph), "DAG should not have a cycle");

            graph.AddEdge("D", "A");
            Assert(GraphAlgorithms.HasCycle(graph), "directed cycle should be detected");
            Assert(!GraphAlgorithms.TopologicalSort(graph).Success, "cyclic graph should fail topological sort");

            AssertThrows<InvalidOperationException>(
                () => GraphAlgorithms.TopologicalSort(NewUndirectedGraph()),
                "undirected topological sort should be rejected");
        }

        private static void TestComponentsAndBipartiteness()
        {
            Graph<string> graph = NewUndirectedGraph();
            IReadOnlyList<IReadOnlyList<string>> components = GraphAlgorithms.ConnectedComponents(graph);
            Assert(components.Count == 2, "undirected component count");
            Assert(string.Join(",", components[0]) == "A,B,C,D,E", "undirected component members");
            AssertThrows<NotSupportedException>(() => ((IList<string>)components[0]).Clear(),
                "component members should be immutable snapshots");
            Assert(GraphAlgorithms.HasCycle(graph), "undirected cycle should be detected");
            Assert(!GraphAlgorithms.IsBipartite(graph), "triangle graph should not be bipartite");

            Graph<string> tree = new Graph<string>(false);
            tree.AddEdge("A", "B");
            tree.AddEdge("A", "C");
            tree.AddEdge("B", "D");
            Assert(!GraphAlgorithms.HasCycle(tree), "tree should not have a cycle");
            Assert(GraphAlgorithms.IsBipartite(tree), "tree should be bipartite");
        }

        private static void TestStronglyConnectedComponents()
        {
            Graph<int> graph = new Graph<int>(true);
            graph.AddEdge(0, 1);
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 0);
            graph.AddEdge(2, 3);

            IReadOnlyList<IReadOnlyList<int>> components = GraphAlgorithms.StronglyConnectedComponents(graph);
            Assert(components.Count == 2, "strongly connected component count");
            Assert(components.Any(component => component.Count == 3), "cycle should form a three-node component");
        }

        private static void TestMinimumSpanningTree()
        {
            Graph<string> graph = NewUndirectedGraph();
            MinimumSpanningTreeResult<string> result = GraphAlgorithms.MinimumSpanningTree(graph);
            AssertEqual(result.TotalWeight, 7.0, "minimum spanning tree weight");
            Assert(result.Edges.Count == 4, "minimum spanning tree edge count");

            AssertThrows<InvalidOperationException>(
                () => GraphAlgorithms.MinimumSpanningTree(new Graph<string>(true)),
                "directed minimum spanning tree should be rejected");
        }

        private static void TestMaximumFlow()
        {
            Graph<string> graph = new Graph<string>(true);
            graph.AddEdge("S", "A", 3.0);
            graph.AddEdge("S", "B", 2.0);
            graph.AddEdge("A", "B", 1.0);
            graph.AddEdge("A", "T", 2.0);
            graph.AddEdge("B", "T", 3.0);

            MaximumFlowResult<string> result = GraphAlgorithms.MaximumFlow(graph, "S", "T");
            AssertEqual(result.MaximumFlow, 5.0, "maximum flow value");
            AssertEqual(result.Flows[("S", "A")] + result.Flows[("S", "B")], 5.0, "source flow conservation");
        }

        private static void TestPageRank()
        {
            Graph<string> graph = new Graph<string>(true);
            graph.AddEdge("A", "B");
            graph.AddEdge("A", "C");
            graph.AddEdge("B", "D");
            graph.AddEdge("C", "D");
            graph.AddEdge("D", "A");

            IReadOnlyDictionary<string, double> ranks = GraphAlgorithms.PageRank(graph);
            AssertEqual(ranks.Values.Sum(), 1.0, "PageRank should sum to one", 1e-7);
            Assert(ranks.All(pair => pair.Value > 0.0), "PageRank values should be positive");
        }

        private static void TestImmutableResultsAndWeightedPageRank()
        {
            Graph<string> graph = new Graph<string>(false);
            graph.AddEdge("self", "self", 2.0);
            Assert(graph.EdgeCount == 1 && graph.GetOutgoingEdges("self").Count == 1 && graph.GetIncomingEdges("self").Count == 1,
                "an undirected self-loop should be represented once in each adjacency list");
            graph.AddEdge("A", "B", 9.0);
            graph.AddEdge("A", "C", 1.0);

            GraphTraversal<string> traversal = GraphAlgorithms.BreadthFirstTraversal(graph, "A");
            AssertThrows<NotSupportedException>(() => ((IList<string>)traversal.Order).Add("mutated"),
                "traversal order should be a defensive read-only copy");
            AssertThrows<NotSupportedException>(() => ((IDictionary<string, string>)traversal.Parents).Clear(),
                "traversal parents should be a defensive read-only copy");

            IReadOnlyDictionary<string, double> uniform = GraphAlgorithms.PageRank(graph, confidenceWeighted: false);
            IReadOnlyDictionary<string, double> weighted = GraphAlgorithms.PageRank(graph, confidenceWeighted: true);
            Assert(weighted["B"] > weighted["C"] && Math.Abs(uniform["B"] - uniform["C"]) < 1e-8,
                "weighted PageRank should follow normalized positive edge confidence");
            AssertThrows<NotSupportedException>(() => ((IDictionary<string, double>)weighted).Clear(),
                "PageRank results should be immutable snapshots");
            Assert(graph.EdgeCount == 3 && !graph.ContainsNode("mutated"),
                "mutating algorithm result views must not alter the source graph");

            Graph<int> negativeConfidence = new Graph<int>();
            negativeConfidence.AddEdge(0, 1, -0.5);
            AssertThrows<InvalidOperationException>(() => GraphAlgorithms.PageRank(negativeConfidence, confidenceWeighted: true),
                "confidence-weighted PageRank must reject negative traversal weights");

            Graph<int> sparse = new Graph<int>();
            for (int node = 0; node < 1_500; node++) sparse.AddEdge(node, node + 1, 1.0);
            AssertEqual(GraphAlgorithms.MaximumFlow(sparse, 0, 1_500).MaximumFlow, 1.0,
                "maximum flow should use sparse residual storage on large low-degree graphs");

            Graph<int> tooLargeForAllPairs = new Graph<int>();
            for (int node = 0; node <= 512; node++) tooLargeForAllPairs.AddNode(node);
            AssertThrows<InvalidOperationException>(() => GraphAlgorithms.FloydWarshall(tooLargeForAllPairs),
                "Floyd-Warshall should reject node counts beyond its documented memory limit");
        }

        private static void TestGraphValidation()
        {
            Graph<string> graph = new Graph<string>(false);
            graph.AddEdge("A", "B");
            Assert(graph.EdgeCount == 1, "undirected edge should be stored once in the edge list");
            Assert(graph.ContainsNode("A") && graph.ContainsNode("B"), "edge should add both nodes");
            Assert(graph.GetNeighbors("A").SequenceEqual(new[] { "B" }), "neighbor lookup");
            AssertThrows<NotSupportedException>(() => ((IList<string>)graph.Nodes).Add("C"),
                "node snapshots should not expose a mutable backing list");
            AssertThrows<NotSupportedException>(() => ((IList<GraphEdge<string>>)graph.Edges).Clear(),
                "edge snapshots should not expose a mutable backing list");
            AssertThrows<NotSupportedException>(() => ((IList<GraphEdge<string>>)graph.GetOutgoingEdges("A")).Clear(),
                "adjacency views should not expose mutable backing lists");
            graph.AddEdge("A", "B");
            Assert(graph.EdgeCount == 2 && graph.GetOutgoingEdges("A").Count == 2,
                "Parallel and exact duplicate insertions should be retained as separate edge instances.");
            AssertThrows<ArgumentOutOfRangeException>(
                () => graph.AddEdge("A", "C", double.NaN),
                "non-finite weight should be rejected");
            AssertThrows<ArgumentException>(
                () => graph.GetNeighbors("missing"),
                "missing neighbor lookup should be rejected");
        }
    }
}
