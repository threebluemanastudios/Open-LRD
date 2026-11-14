using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;

namespace OPEN_LRD.Maintenance
{
    internal static class OverlappingNodeConsolidator
    {
        private sealed record Candidate(Guid First, Guid Second, bool SharedDataPointer);

        private sealed class NameBucket
        {
            private readonly Dictionary<DataPointer, Web_Node> byPointer = new Dictionary<DataPointer, Web_Node>();

            public Web_Node? LastWithoutPointer { get; private set; }
            public Web_Node? LastAny { get; private set; }

            public Web_Node? FindCompatible(Web_Node node)
            {
                DataPointer? pointer = node.Get_DataPointer();
                if (pointer is not null && byPointer.TryGetValue(pointer, out Web_Node? samePointer))
                    return samePointer;
                return LastWithoutPointer ?? (pointer is null ? LastAny : null);
            }

            public void Add(Web_Node node)
            {
                LastAny = node;
                DataPointer? pointer = node.Get_DataPointer();
                if (pointer is null)
                    LastWithoutPointer = node;
                else
                    byPointer[pointer] = node;
            }
        }

        public static void Consolidate(LRD_Core core, MaintenanceOptions options, MaintenanceReport report)
        {
            IReadOnlyDictionary<Guid, Web_Node>? graph = core.GetDictionary();
            if (graph is null || graph.Count == 0)
                return;

            Web_Node[] nodes = graph.Values
                .Where(node => !node.IsDisposed)
                .OrderBy(node => node.ID)
                .Take(options.MaxNodesToExaminePerCycle)
                .ToArray();
            report.NodesExamined += nodes.Length;
            if (nodes.Length < graph.Values.Count(node => !node.IsDisposed))
                report.WorkBudgetExhausted = true;

            Dictionary<(Guid, Guid), Candidate> candidates = new Dictionary<(Guid, Guid), Candidate>();
            Dictionary<DataPointer, List<Web_Node>> byPointer = new Dictionary<DataPointer, List<Web_Node>>();
            Dictionary<(int TypeID, string Value), NameBucket> byName =
                new Dictionary<(int TypeID, string Value), NameBucket>();

            foreach (Web_Node node in nodes)
            {
                DataPointer? pointer = node.Get_DataPointer();
                if (pointer is not null)
                {
                    if (!byPointer.TryGetValue(pointer, out List<Web_Node>? pointerBucket))
                    {
                        pointerBucket = new List<Web_Node>();
                        byPointer.Add(pointer, pointerBucket);
                    }

                    if (pointerBucket.Count > 0)
                        AddCandidate(candidates, pointerBucket[^1], node, sharedDataPointer: true, report);
                    pointerBucket.Add(node);
                }

                foreach (string value in GetNames(node).Distinct(StringComparer.Ordinal))
                {
                    (int, string) key = (node.Get_NodeType().Id, value);
                    if (!byName.TryGetValue(key, out NameBucket? nameBucket))
                    {
                        nameBucket = new NameBucket();
                        byName.Add(key, nameBucket);
                    }

                    Web_Node? previous = nameBucket.FindCompatible(node);
                    if (previous is not null)
                        AddCandidate(candidates, previous, node, sharedDataPointer: false, report);
                    else if (nameBucket.LastAny is not null)
                        AddCandidate(candidates, nameBucket.LastAny, node, sharedDataPointer: false, report);
                    nameBucket.Add(node);
                }
            }

            OntologyRelations.EnsureRegistered();
            Dictionary<Guid, List<Web_Node>> namedSourcesByTarget = new Dictionary<Guid, List<Web_Node>>();
            int linksExamined = 0;
            foreach (Web_Node source in nodes)
            {
                foreach (Link link in source.Get_ReferenceLinks())
                {
                    if (!link.IsActive)
                        continue;
                    if (++linksExamined > options.MaxLinksToExaminePerCycle)
                    {
                        report.WorkBudgetExhausted = true;
                        report.AddDiagnostic("Semantic-equivalence scan reached its link budget.");
                        break;
                    }

                    bool synonym = link.ForwardLink.Id == OntologyRelations.Synonym;
                    bool explicitIdentity = link.Classification.Id == LinkClass.identities.Id;
                    if ((synonym || explicitIdentity)
                        && graph.TryGetValue(link.DestinationID, out Web_Node? target)
                        && target is not null
                        && source.Get_NodeType().Id == target.Get_NodeType().Id)
                    {
                        AddCandidate(candidates, source, target, sharedDataPointer: false, report);
                    }

                    if (link.ForwardLink.Id == OntologyRelations.IsNamed)
                    {
                        if (!namedSourcesByTarget.TryGetValue(link.DestinationID, out List<Web_Node>? sources))
                        {
                            sources = new List<Web_Node>();
                            namedSourcesByTarget.Add(link.DestinationID, sources);
                        }
                        sources.Add(source);
                    }
                }
                if (linksExamined > options.MaxLinksToExaminePerCycle)
                    break;
            }

            foreach (List<Web_Node> sources in namedSourcesByTarget.Values)
            {
                Dictionary<int, NameBucket> byType = new Dictionary<int, NameBucket>();
                foreach (Web_Node current in sources.DistinctBy(node => node.ID).OrderBy(node => node.ID))
                {
                    int typeID = current.Get_NodeType().Id;
                    if (!byType.TryGetValue(typeID, out NameBucket? bucket))
                    {
                        bucket = new NameBucket();
                        byType.Add(typeID, bucket);
                    }

                    Web_Node? previous = bucket.FindCompatible(current);
                    if (previous is not null)
                        AddCandidate(candidates, previous, current, sharedDataPointer: false, report);
                    else if (bucket.LastAny is not null)
                        AddCandidate(candidates, bucket.LastAny, current, sharedDataPointer: false, report);
                    bucket.Add(current);
                }
            }

            IEnumerable<Candidate> orderedCandidates = candidates.Values
                .OrderByDescending(candidate => candidate.SharedDataPointer)
                .ThenBy(candidate => candidate.First)
                .ThenBy(candidate => candidate.Second);

            foreach (Candidate candidate in orderedCandidates)
            {
                if (report.NodesMerged >= options.MaxMergesPerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    break;
                }

                if (!core.TryGetNode(candidate.First, out Web_Node? first) || first is null
                    || !core.TryGetNode(candidate.Second, out Web_Node? second) || second is null)
                {
                    continue;
                }

                if (!CanPair(first, second, candidate.SharedDataPointer))
                    continue;

                Web_Node survivor = ChooseSurvivor(core, first, second);
                Web_Node duplicate = survivor.ID == first.ID ? second : first;
                if (!core.MergeNodes(survivor.ID, duplicate.ID))
                {
                    report.AddDiagnostic($"Could not merge '{first.ID}' and '{second.ID}' after validation.");
                    continue;
                }

                report.NodesMerged++;
                report.NodesRemoved++;
            }
        }

        private static void AddCandidate(
            IDictionary<(Guid, Guid), Candidate> candidates,
            Web_Node first,
            Web_Node second,
            bool sharedDataPointer,
            MaintenanceReport report)
        {
            if (first.ID == second.ID)
                return;

            if (!CanPair(first, second, sharedDataPointer))
            {
                if (first.Get_DataPointer() is not null && second.Get_DataPointer() is not null
                    && !Equals(first.Get_DataPointer(), second.Get_DataPointer()))
                {
                    report.AddDiagnostic(
                        $"Merge blocked for '{first.ID}' and '{second.ID}': DataPointers differ.");
                }
                return;
            }

            Guid low = first.ID.CompareTo(second.ID) < 0 ? first.ID : second.ID;
            Guid high = low == first.ID ? second.ID : first.ID;
            (Guid, Guid) key = (low, high);
            if (!candidates.TryGetValue(key, out Candidate? existing) || sharedDataPointer)
                candidates[key] = new Candidate(low, high, sharedDataPointer || existing?.SharedDataPointer == true);
        }

        private static bool CanPair(Web_Node first, Web_Node second, bool sharedDataPointer)
        {
            DataPointer? firstPointer = first.Get_DataPointer();
            DataPointer? secondPointer = second.Get_DataPointer();
            if (firstPointer is not null && secondPointer is not null)
            {
                if (!Equals(firstPointer, secondPointer))
                    return false;
                return sharedDataPointer || Equals(firstPointer, secondPointer);
            }

            return first.Get_NodeType().Id == second.Get_NodeType().Id;
        }

        private static Web_Node ChooseSurvivor(LRD_Core core, Web_Node first, Web_Node second)
        {
            int comparison = first.IsPermanent.CompareTo(second.IsPermanent);
            if (comparison != 0)
                return comparison > 0 ? first : second;

            comparison = first.Get_WeightUp().CompareTo(second.Get_WeightUp());
            if (comparison != 0)
                return comparison > 0 ? first : second;

            comparison = core.GetLinks(first.ID).Count.CompareTo(core.GetLinks(second.ID).Count);
            if (comparison != 0)
                return comparison > 0 ? first : second;

            comparison = (first.Get_DataPointer() is not null).CompareTo(second.Get_DataPointer() is not null);
            if (comparison != 0)
                return comparison > 0 ? first : second;

            return first.ID.CompareTo(second.ID) <= 0 ? first : second;
        }

        private static IEnumerable<string> GetNames(Web_Node node)
        {
            if (!string.IsNullOrWhiteSpace(node.Label))
                yield return node.Label.Trim().ToLowerInvariant();

            foreach (string alias in node.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                    yield return alias.Trim().ToLowerInvariant();
            }
        }
    }
}
