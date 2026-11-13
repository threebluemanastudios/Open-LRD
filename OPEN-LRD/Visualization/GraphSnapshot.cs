using System;
using System.Collections.Generic;
using System.Linq;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Visualization
{
    internal sealed record NodeRecord(
        Guid Id,
        int TypeId,
        string TypeName,
        NodeLifetime Lifetime,
        string? Label,
        string DisplayLabel,
        IReadOnlyList<string> Aliases,
        int Degree);

    internal sealed record LinkRecord(
        int AIndex,
        int BIndex,
        int ForwardTypeId,
        string ForwardTypeName,
        int ClassId,
        string ClassName,
        double Distance);

    internal sealed class GraphSnapshot
    {
        private GraphSnapshot(
            NodeRecord[] nodes,
            LinkRecord[] links,
            int[] nodeTypeIds,
            int[] forwardTypeIds,
            int sourceNodeCount,
            int sourceLinkCount,
            int duplicateLinkCount,
            int danglingLinkCount,
            int selfLinkCount,
            int truncatedNodeCount,
            int truncatedLinkCount)
        {
            Nodes = nodes;
            Links = links;
            NodeTypeIds = nodeTypeIds;
            ForwardTypeIds = forwardTypeIds;
            SourceNodeCount = sourceNodeCount;
            SourceLinkCount = sourceLinkCount;
            DuplicateLinkCount = duplicateLinkCount;
            DanglingLinkCount = danglingLinkCount;
            SelfLinkCount = selfLinkCount;
            TruncatedNodeCount = truncatedNodeCount;
            TruncatedLinkCount = truncatedLinkCount;
            Warnings = BuildWarnings(
                duplicateLinkCount,
                danglingLinkCount,
                selfLinkCount,
                truncatedNodeCount,
                truncatedLinkCount);
        }

        public IReadOnlyList<NodeRecord> Nodes { get; }

        public IReadOnlyList<LinkRecord> Links { get; }

        public IReadOnlyList<int> NodeTypeIds { get; }

        public IReadOnlyList<int> ForwardTypeIds { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int SourceNodeCount { get; }

        public int SourceLinkCount { get; }

        public int DuplicateLinkCount { get; }

        public int DanglingLinkCount { get; }

        public int SelfLinkCount { get; }

        public int TruncatedNodeCount { get; }

        public int TruncatedLinkCount { get; }

        public bool IsTruncated => TruncatedNodeCount > 0 || TruncatedLinkCount > 0;

        public static GraphSnapshot FromCore(LRD_Core core, VisualizerOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(core);

            if (!core.initialized)
                throw new InvalidOperationException("The core must be initialized before taking a graph snapshot.");

            IReadOnlyDictionary<Guid, Web_Node>? dictionary = core.GetDictionary();

            if (dictionary is null)
                throw new InvalidOperationException("The core does not expose a graph dictionary.");

            options ??= new VisualizerOptions();
            options.Validate();

            List<Web_Node> orderedNodes = dictionary.Values
                .OrderBy(node => node.ID)
                .ToList();

            int sourceNodeCount = orderedNodes.Count;
            Dictionary<Guid, int> indexById = new Dictionary<Guid, int>(sourceNodeCount);

            for (int index = 0; index < orderedNodes.Count; index++)
                indexById[orderedNodes[index].ID] = index;

            List<PendingLink> deduplicatedLinks = new List<PendingLink>();
            Dictionary<LinkKey, PendingLink> linkByKey = new Dictionary<LinkKey, PendingLink>();
            int sourceLinkCount = 0;
            int duplicateLinkCount = 0;
            int danglingLinkCount = 0;
            int selfLinkCount = 0;

            for (int sourceIndex = 0; sourceIndex < orderedNodes.Count; sourceIndex++)
            {
                foreach (Link link in core.GetLinks(orderedNodes[sourceIndex].ID))
                {
                    sourceLinkCount++;

                    if (link.SourceID == link.DestinationID)
                    {
                        selfLinkCount++;
                        continue;
                    }

                    if (!indexById.TryGetValue(link.SourceID, out int sourceNodeIndex)
                        || !indexById.TryGetValue(link.DestinationID, out int destinationNodeIndex))
                    {
                        danglingLinkCount++;
                        continue;
                    }

                    int aIndex = Math.Min(sourceNodeIndex, destinationNodeIndex);
                    int bIndex = Math.Max(sourceNodeIndex, destinationNodeIndex);
                    LinkKey key = new LinkKey(
                        orderedNodes[aIndex].ID,
                        orderedNodes[bIndex].ID,
                        link.ForwardLink.Id,
                        link.Classification.Id);

                    if (linkByKey.ContainsKey(key))
                    {
                        duplicateLinkCount++;
                        continue;
                    }

                    PendingLink pending = new PendingLink(
                        aIndex,
                        bIndex,
                        link.ForwardLink.Id,
                        link.ForwardLink.Name,
                        link.Classification.Id,
                        link.Classification.Name,
                        LinkDistanceDefinitions.GetDistance(link));

                    linkByKey.Add(key, pending);
                    deduplicatedLinks.Add(pending);
                }
            }

            int[] preCapDegrees = new int[sourceNodeCount];

            foreach (PendingLink link in deduplicatedLinks)
            {
                preCapDegrees[link.AIndex]++;
                preCapDegrees[link.BIndex]++;
            }

            int[] keptOldIndices = BuildKeptNodeIndices(orderedNodes, preCapDegrees, options.MaxNodes);
            int truncatedNodeCount = sourceNodeCount - keptOldIndices.Length;

            int[] newIndexByOldIndex = new int[sourceNodeCount];
            Array.Fill(newIndexByOldIndex, -1);

            for (int newIndex = 0; newIndex < keptOldIndices.Length; newIndex++)
                newIndexByOldIndex[keptOldIndices[newIndex]] = newIndex;

            List<PendingLink> inducedLinks = new List<PendingLink>();

            foreach (PendingLink link in deduplicatedLinks)
            {
                int newA = newIndexByOldIndex[link.AIndex];
                int newB = newIndexByOldIndex[link.BIndex];

                if (newA < 0 || newB < 0)
                    continue;

                inducedLinks.Add(link with
                {
                    AIndex = Math.Min(newA, newB),
                    BIndex = Math.Max(newA, newB)
                });
            }

            inducedLinks.Sort(CompareLinks);
            int truncatedLinkCount = Math.Max(0, inducedLinks.Count - options.MaxLinks);

            if (truncatedLinkCount > 0)
                inducedLinks.RemoveRange(options.MaxLinks, truncatedLinkCount);

            int[] finalDegrees = new int[keptOldIndices.Length];

            foreach (PendingLink link in inducedLinks)
            {
                finalDegrees[link.AIndex]++;
                finalDegrees[link.BIndex]++;
            }

            NodeRecord[] nodeRecords = new NodeRecord[keptOldIndices.Length];

            for (int newIndex = 0; newIndex < keptOldIndices.Length; newIndex++)
            {
                Web_Node node = orderedNodes[keptOldIndices[newIndex]];
                NodeTypes nodeType = node.Get_NodeType();
                string[] aliases = node.Aliases.ToArray();

                nodeRecords[newIndex] = new NodeRecord(
                    node.ID,
                    nodeType.Id,
                    nodeType.Name,
                    node.Get_Lifetime(),
                    node.Label,
                    BuildDisplayLabel(node, aliases),
                    aliases,
                    finalDegrees[newIndex]);
            }

            LinkRecord[] linkRecords = inducedLinks
                .Select(link => new LinkRecord(
                    link.AIndex,
                    link.BIndex,
                    link.ForwardTypeId,
                    link.ForwardTypeName,
                    link.ClassId,
                    link.ClassName,
                    link.Distance))
                .ToArray();

            int[] nodeTypeIds = nodeRecords
                .Select(node => node.TypeId)
                .Distinct()
                .OrderBy(typeId => typeId)
                .ToArray();

            int[] forwardTypeIds = linkRecords
                .Select(link => link.ForwardTypeId)
                .Distinct()
                .OrderBy(typeId => typeId)
                .ToArray();

            return new GraphSnapshot(
                nodeRecords,
                linkRecords,
                nodeTypeIds,
                forwardTypeIds,
                sourceNodeCount,
                sourceLinkCount,
                duplicateLinkCount,
                danglingLinkCount,
                selfLinkCount,
                truncatedNodeCount,
                truncatedLinkCount);
        }

        private static int[] BuildKeptNodeIndices(
            IReadOnlyList<Web_Node> orderedNodes,
            IReadOnlyList<int> degrees,
            int maxNodes)
        {
            if (orderedNodes.Count <= maxNodes)
                return Enumerable.Range(0, orderedNodes.Count).ToArray();

            int[] kept = Enumerable.Range(0, orderedNodes.Count)
                .OrderByDescending(index => degrees[index])
                .ThenBy(index => orderedNodes[index].ID)
                .Take(maxNodes)
                .ToArray();

            Array.Sort(kept);
            return kept;
        }

        private static int CompareLinks(PendingLink left, PendingLink right)
        {
            int comparison = left.AIndex.CompareTo(right.AIndex);

            if (comparison != 0)
                return comparison;

            comparison = left.BIndex.CompareTo(right.BIndex);

            if (comparison != 0)
                return comparison;

            comparison = left.ForwardTypeId.CompareTo(right.ForwardTypeId);

            if (comparison != 0)
                return comparison;

            return left.ClassId.CompareTo(right.ClassId);
        }

        private static string BuildDisplayLabel(Web_Node node, IReadOnlyList<string> aliases)
        {
            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label;

            string? alias = aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            if (alias is not null)
                return alias;

            return node.ID.ToString("N")[..8];
        }

        private static IReadOnlyList<string> BuildWarnings(
            int duplicateLinkCount,
            int danglingLinkCount,
            int selfLinkCount,
            int truncatedNodeCount,
            int truncatedLinkCount)
        {
            List<string> warnings = new List<string>();

            if (duplicateLinkCount > 0)
            {
                warnings.Add(
                    $"{duplicateLinkCount} reciprocal link record(s) were merged for rendering.");
            }

            if (danglingLinkCount > 0)
                warnings.Add($"{danglingLinkCount} link(s) referenced missing nodes and were skipped.");

            if (selfLinkCount > 0)
                warnings.Add($"{selfLinkCount} self-link(s) were skipped.");

            if (truncatedNodeCount > 0)
                warnings.Add($"{truncatedNodeCount} low-degree node(s) exceeded the node cap.");

            if (truncatedLinkCount > 0)
                warnings.Add($"{truncatedLinkCount} link(s) exceeded the link cap.");

            return warnings;
        }

        private readonly record struct LinkKey(
            Guid AId,
            Guid BId,
            int ForwardTypeId,
            int ClassId);

        private sealed record PendingLink(
            int AIndex,
            int BIndex,
            int ForwardTypeId,
            string ForwardTypeName,
            int ClassId,
            string ClassName,
            double Distance);
    }
}
