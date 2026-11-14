using System;
using System.Collections.Generic;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    internal sealed record NodeMeshBatch(int NodeTypeId, string NodeTypeName, MeshData Mesh);

    internal sealed record LinkMeshBatch(
        int ForwardTypeId,
        string ForwardTypeName,
        int ClassId,
        MeshData Mesh);

    internal sealed class SceneGeometry
    {
        public SceneGeometry(
            GraphSnapshot snapshot,
            LayoutResult layout,
            IReadOnlyList<NodeMeshBatch> nodeBatches,
            IReadOnlyList<LinkMeshBatch> linkBatches,
            float radius)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(nodeBatches);
            ArgumentNullException.ThrowIfNull(linkBatches);

            if (layout.NodeCount != snapshot.Nodes.Count)
            {
                throw new ArgumentException(
                    "Layout positions must match the snapshot node count.",
                    nameof(layout));
            }

            if (!float.IsFinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            Snapshot = snapshot;
            Layout = layout;
            NodeBatches = nodeBatches;
            LinkBatches = linkBatches;
            Radius = radius;
        }

        public GraphSnapshot Snapshot { get; }

        public LayoutResult Layout { get; }

        public IReadOnlyList<NodeMeshBatch> NodeBatches { get; }

        public IReadOnlyList<LinkMeshBatch> LinkBatches { get; }

        public Vector3 Center => Layout.Centroid;

        public float Radius { get; }

        public int NodeCount => Snapshot.Nodes.Count;

        public int LinkCount => Snapshot.Links.Count;

        public bool HasNodeGeometry
        {
            get
            {
                foreach (NodeMeshBatch batch in NodeBatches)
                {
                    if (!batch.Mesh.IsEmpty)
                        return true;
                }

                return false;
            }
        }
    }

    internal static class VisualizerSceneBuilder
    {
        public static SceneGeometry Build(
            GraphSnapshot snapshot,
            LayoutResult layout,
            VisualizerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(layout);

            options ??= new VisualizerOptions();
            options.Validate();

            if (layout.NodeCount != snapshot.Nodes.Count)
            {
                throw new ArgumentException(
                    "Layout positions must match the snapshot node count.",
                    nameof(layout));
            }

            List<NodeMeshBatch> nodeBatches = new List<NodeMeshBatch>(snapshot.NodeTypeIds.Count);
            Dictionary<int, MeshData> nodeMeshes = GraphMeshBuilder.BuildNodeCubesByType(
                snapshot,
                layout.Positions,
                options.CubeSize);
            Dictionary<int, string> nodeNames = new Dictionary<int, string>();
            foreach (NodeRecord node in snapshot.Nodes)
            {
                if (!nodeNames.ContainsKey(node.TypeId))
                    nodeNames.Add(node.TypeId, node.TypeName);
            }

            foreach (int nodeTypeId in snapshot.NodeTypeIds)
            {
                if (!nodeMeshes.TryGetValue(nodeTypeId, out MeshData? mesh))
                    continue;

                if (mesh.IsEmpty)
                    continue;

                nodeBatches.Add(new NodeMeshBatch(
                    nodeTypeId,
                    nodeNames.TryGetValue(nodeTypeId, out string? nodeName) ? nodeName : nodeTypeId.ToString(),
                    mesh));
            }

            Dictionary<int, int> representativeClass = new Dictionary<int, int>();
            Dictionary<int, string> forwardNames = new Dictionary<int, string>();

            foreach (LinkRecord link in snapshot.Links)
            {
                if (!forwardNames.ContainsKey(link.ForwardTypeId))
                    forwardNames.Add(link.ForwardTypeId, link.ForwardTypeName);

                if (!representativeClass.TryGetValue(link.ForwardTypeId, out int classId)
                    || link.ClassId < classId)
                {
                    representativeClass[link.ForwardTypeId] = link.ClassId;
                }
            }

            List<LinkMeshBatch> linkBatches = new List<LinkMeshBatch>(snapshot.ForwardTypeIds.Count);
            Dictionary<int, MeshData> linkMeshes = GraphMeshBuilder.BuildLinkLinesByType(
                snapshot,
                layout.Positions,
                options.LineWidth,
                options.CubeSize);

            foreach (int forwardTypeId in snapshot.ForwardTypeIds)
            {
                if (!linkMeshes.TryGetValue(forwardTypeId, out MeshData? mesh))
                    continue;

                if (mesh.IsEmpty)
                    continue;

                linkBatches.Add(new LinkMeshBatch(
                    forwardTypeId,
                    forwardNames.TryGetValue(forwardTypeId, out string? forwardName) ? forwardName : forwardTypeId.ToString(),
                    representativeClass[forwardTypeId],
                    mesh));
            }

            float radius = Math.Max(layout.Radius, (float)options.CubeSize * 1.5f);

            return new SceneGeometry(snapshot, layout, nodeBatches, linkBatches, radius);
        }

    }
}
