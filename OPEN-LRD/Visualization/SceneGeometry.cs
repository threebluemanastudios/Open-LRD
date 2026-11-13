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

            foreach (int nodeTypeId in snapshot.NodeTypeIds)
            {
                MeshData mesh = GraphMeshBuilder.BuildNodeCubesForType(
                    snapshot,
                    layout.Positions,
                    options.CubeSize,
                    nodeTypeId);

                if (mesh.IsEmpty)
                    continue;

                nodeBatches.Add(new NodeMeshBatch(
                    nodeTypeId,
                    ResolveNodeTypeName(snapshot, nodeTypeId),
                    mesh));
            }

            Dictionary<int, int> representativeClass = new Dictionary<int, int>();

            foreach (LinkRecord link in snapshot.Links)
            {
                if (!representativeClass.TryGetValue(link.ForwardTypeId, out int classId)
                    || link.ClassId < classId)
                {
                    representativeClass[link.ForwardTypeId] = link.ClassId;
                }
            }

            List<LinkMeshBatch> linkBatches = new List<LinkMeshBatch>(snapshot.ForwardTypeIds.Count);

            foreach (int forwardTypeId in snapshot.ForwardTypeIds)
            {
                MeshData mesh = GraphMeshBuilder.BuildLinkLinesForType(
                    snapshot,
                    layout.Positions,
                    options.LineWidth,
                    options.CubeSize,
                    forwardTypeId);

                if (mesh.IsEmpty)
                    continue;

                linkBatches.Add(new LinkMeshBatch(
                    forwardTypeId,
                    ResolveForwardTypeName(snapshot, forwardTypeId),
                    representativeClass[forwardTypeId],
                    mesh));
            }

            float radius = Math.Max(layout.Radius, (float)options.CubeSize * 1.5f);

            return new SceneGeometry(snapshot, layout, nodeBatches, linkBatches, radius);
        }

        private static string ResolveNodeTypeName(GraphSnapshot snapshot, int nodeTypeId)
        {
            foreach (NodeRecord node in snapshot.Nodes)
            {
                if (node.TypeId == nodeTypeId)
                    return node.TypeName;
            }

            return nodeTypeId.ToString();
        }

        private static string ResolveForwardTypeName(GraphSnapshot snapshot, int forwardTypeId)
        {
            foreach (LinkRecord link in snapshot.Links)
            {
                if (link.ForwardTypeId == forwardTypeId)
                    return link.ForwardTypeName;
            }

            return forwardTypeId.ToString();
        }
    }
}
