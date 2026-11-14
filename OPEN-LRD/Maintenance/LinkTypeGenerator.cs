using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;

namespace OPEN_LRD.Maintenance
{
    internal static class LinkTypeGenerator
    {
        private sealed record CompositionRule(int FirstRelation, int SecondRelation, int ResultRelation);

        private static readonly CompositionRule[] CompositionRules =
        {
            new CompositionRule(OntologyRelations.PartOf, OntologyRelations.PartOf, OntologyRelations.PartOf),
            new CompositionRule(OntologyRelations.IsA, OntologyRelations.HasProperty, OntologyRelations.HasProperty),
            new CompositionRule(OntologyRelations.HasLocation, OntologyRelations.PartOf, OntologyRelations.HasLocation),
            new CompositionRule(OntologyRelations.ParticipatedIn, OntologyRelations.HasLocation, OntologyRelations.HasLocation)
        };

        public static void Generate(
            LRD_Core core,
            MaintenanceOptions options,
            MaintenanceReport report,
            ISet<int> generatedRelationIds)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNull(generatedRelationIds);

            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();

            Dictionary<Guid, List<Link>> outgoing = BuildOutgoing(core, options, report);
            int generated = 0;
            int pathsExamined = 0;

            if (options.EnableCompositionalInduction)
            {
                bool pathBudgetReached = false;
                foreach (CompositionRule rule in CompositionRules)
                {
                    foreach (KeyValuePair<Guid, List<Link>> pair in outgoing)
                    {
                        foreach (Link first in pair.Value.Where(link => link.ForwardLink.Id == rule.FirstRelation))
                        {
                            if (!outgoing.TryGetValue(first.DestinationID, out List<Link>? nextLinks))
                                continue;

                            foreach (Link second in nextLinks.Where(link => link.ForwardLink.Id == rule.SecondRelation))
                            {
                                if (++pathsExamined > options.MaxPathsToExaminePerCycle)
                                {
                                    pathBudgetReached = true;
                                    report.WorkBudgetExhausted = true;
                                    report.AddDiagnostic("Compositional induction reached its path-scan budget.");
                                    break;
                                }

                                if (report.LinksGenerated + generated >= options.MaxLinksGeneratedPerCycle)
                                {
                                    report.WorkBudgetExhausted = true;
                                    report.AddDiagnostic("Link induction stopped at the per-cycle generation budget.");
                                    report.LinksGenerated += generated;
                                    return;
                                }

                                if (AddInferredLink(core, first.SourceID, second.DestinationID, rule.ResultRelation))
                                    generated++;
                            }

                            if (pathBudgetReached)
                                break;
                        }

                        if (pathBudgetReached)
                            break;
                    }

                    if (pathBudgetReached)
                        break;
                }
            }

            if (options.EnableFrequentPatternAbstraction
                && report.LinksGenerated + generated < options.MaxLinksGeneratedPerCycle)
            {
                Dictionary<(int First, int Second), HashSet<(Guid Source, Guid Destination)>> patterns =
                    new Dictionary<(int First, int Second), HashSet<(Guid Source, Guid Destination)>>();
                foreach (KeyValuePair<Guid, List<Link>> pair in outgoing)
                {
                    foreach (Link first in pair.Value)
                    {
                        if (first.ForwardLink.Id <= 0
                            || !RelationRegistry.TryGetById(first.ForwardLink.Id, out RelationLinkType firstRelation)
                            || firstRelation.Name.StartsWith("pattern_", StringComparison.OrdinalIgnoreCase)
                            || !outgoing.TryGetValue(first.DestinationID, out List<Link>? nextLinks))
                            continue;

                        foreach (Link second in nextLinks)
                        {
                            if (second.ForwardLink.Id <= 0 || first.SourceID == second.DestinationID
                                || !RelationRegistry.TryGetById(second.ForwardLink.Id, out RelationLinkType secondRelation)
                                || secondRelation.Name.StartsWith("pattern_", StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (++pathsExamined > options.MaxPathsToExaminePerCycle)
                            {
                                report.WorkBudgetExhausted = true;
                                report.AddDiagnostic("Frequent-pattern path scan reached its work budget.");
                                break;
                            }

                            (int First, int Second) key = (first.ForwardLink.Id, second.ForwardLink.Id);
                            if (!patterns.TryGetValue(key, out HashSet<(Guid Source, Guid Destination)>? endpoints))
                            {
                                endpoints = new HashSet<(Guid Source, Guid Destination)>();
                                patterns.Add(key, endpoints);
                            }
                            endpoints.Add((first.SourceID, second.DestinationID));
                        }

                        if (pathsExamined > options.MaxPathsToExaminePerCycle)
                            break;
                    }

                    if (pathsExamined > options.MaxPathsToExaminePerCycle)
                        break;
                }

                report.RelationPatternsExamined += patterns.Count;
                foreach (KeyValuePair<(int First, int Second), HashSet<(Guid Source, Guid Destination)>> pattern in patterns
                    .OrderBy(item => item.Key.First)
                    .ThenBy(item => item.Key.Second))
                {
                    if (pattern.Value.Count < options.MinPatternFrequency)
                        continue;

                    if (!RelationRegistry.TryGetById(pattern.Key.First, out RelationLinkType firstRelation)
                        || !RelationRegistry.TryGetById(pattern.Key.Second, out RelationLinkType secondRelation))
                    {
                        continue;
                    }

                    string relationName = CreatePatternName(
                        firstRelation.Name,
                        secondRelation.Name,
                        pattern.Key.First,
                        pattern.Key.Second);
                    bool existed = RelationRegistry.TryGet(relationName, out RelationLinkType _);
                    if (!existed && report.RelationTypesRegistered >= options.MaxRelationTypesGeneratedPerCycle)
                    {
                        report.WorkBudgetExhausted = true;
                        report.AddDiagnostic("Relation induction reached its per-cycle type-registration budget.");
                        break;
                    }
                    RelationLinkType relation = RelationRegistry.GetOrRegister(relationName);
                    if (!existed)
                        report.RelationTypesRegistered++;
                    generatedRelationIds.Add(relation.Id);

                    foreach ((Guid sourceID, Guid destinationID) in pattern.Value.OrderBy(pair => pair.Source).ThenBy(pair => pair.Destination))
                    {
                        if (report.LinksGenerated + generated >= options.MaxLinksGeneratedPerCycle)
                        {
                            report.WorkBudgetExhausted = true;
                            report.AddDiagnostic("Link induction stopped at the per-cycle generation budget.");
                            break;
                        }

                        if (AddInferredLink(core, sourceID, destinationID, relation.Id))
                            generated++;
                    }
                }
            }

            report.LinksGenerated += generated;
        }

        public static void ElevateRelations(
            LRD_Core core,
            IEnumerable<int> relationIds,
            MaintenanceReport report,
            MaintenanceOptions? options = null)
        {
            options ??= new MaintenanceOptions { EnableReasoningMaintenance = true };
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            Guid? relationRootID = UpperOntologyBootstrap.FindCategory(core, "Relation");
            if (!relationRootID.HasValue)
            {
                int created = 0;
                relationRootID = UpperOntologyBootstrap.GetOrCreateCategory(core, "Relation", ref created);
                report.NodesCreated += created;
            }

            foreach (int relationID in relationIds.Distinct().OrderBy(id => id))
            {
                if (!RelationRegistry.TryGetById(relationID, out RelationLinkType relation))
                    continue;

                IReadOnlyList<Web_Node> existing = core.FindByLabel(relation.Name, OntologyTypes.Relations);
                Guid relationNodeID;
                if (existing.Count > 0)
                {
                    relationNodeID = existing[0].ID;
                    existing[0].PromoteToPermanent();
                }
                else
                {
                    Web_Node relationNode = new Web_Node(
                        Guid.NewGuid(),
                        OntologyTypes.Relations,
                        NodeLifetime.Permanent,
                        0);
                    relationNode.Set_Label(relation.Name);
                    core.AddExistingNode(relationNode);
                    relationNodeID = relationNode.ID;
                    report.RelationNodesCreated++;
                    report.NodesCreated++;
                }

                if (relationRootID.HasValue)
                {
                    Link isA = new Link(relationNodeID, relationRootID.Value,
                        OntologyRelations.IsA, OntologyRelations.IsA, OntologyTypes.ontology,
                        LinkOrigin.Reasoning);
                    Web_Node? relationNode = core.GetExistingNode(relationNodeID);
                    bool hasReasoningSupport = relationNode?.Get_ReferenceLinks().Any(link =>
                        link.HasSameIdentity(isA) && link.IsActive
                        && (link.Origins & LinkOrigin.Reasoning) != 0) == true;
                    if (!hasReasoningSupport)
                    {
                        if (report.LinksGenerated >= options.MaxLinksGeneratedPerCycle)
                        {
                            report.WorkBudgetExhausted = true;
                            report.AddDiagnostic("Relation elevation reached the link-generation budget.");
                            continue;
                        }
                        core.AddDirectedConnection(relationNodeID, relationRootID.Value,
                            OntologyTypes.ontology, OntologyRelations.IsA, LinkOrigin.Reasoning);
                        report.LinksGenerated++;
                    }
                }
            }
        }

        private static Dictionary<Guid, List<Link>> BuildOutgoing(
            LRD_Core core,
            MaintenanceOptions options,
            MaintenanceReport report)
        {
            Dictionary<Guid, List<Link>> outgoing = new Dictionary<Guid, List<Link>>();
            IReadOnlyDictionary<Guid, Web_Node>? graph = core.GetDictionary();
            if (graph is null)
                return outgoing;

            int examined = 0;
            int linksExamined = 0;
            foreach (Web_Node node in graph.Values.OrderBy(node => node.ID))
            {
                if (examined >= options.MaxNodesToExaminePerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    break;
                }
                examined++;

                List<Link> active = new List<Link>();
                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (!link.IsActive)
                        continue;
                    if (++linksExamined > options.MaxLinksToExaminePerCycle)
                    {
                        report.WorkBudgetExhausted = true;
                        report.AddDiagnostic("Relation induction reached its link-scan budget.");
                        break;
                    }
                    if (link.SourceID == node.ID && link.DestinationID != node.ID)
                        active.Add(link);
                }
                if (active.Count > 0)
                    outgoing[node.ID] = active;
                if (linksExamined > options.MaxLinksToExaminePerCycle)
                    break;
            }

            report.NodesExamined += examined;
            return outgoing;
        }

        private static bool AddInferredLink(LRD_Core core, Guid sourceID, Guid destinationID, int relationID)
        {
            if (sourceID == destinationID || core.GetExistingNode(sourceID) is null
                || core.GetExistingNode(destinationID) is null)
            {
                return false;
            }

            if (!core.TryGetNode(sourceID, out Web_Node? source) || source is null)
                return false;

            Link inferredLink = new Link(sourceID, destinationID, relationID, relationID,
                OntologyTypes.ontology, LinkOrigin.Reasoning);
            bool alreadySupported = source.Get_ReferenceLinks().Any(link => link.HasSameIdentity(inferredLink)
                && link.IsActive
                && (link.Origins & LinkOrigin.Reasoning) != 0);

            core.AddDirectedConnection(sourceID, destinationID, OntologyTypes.ontology, relationID,
                LinkOrigin.Reasoning);
            return !alreadySupported;
        }

        private static string CreatePatternName(string firstName, string secondName, int firstID, int secondID)
        {
            string suffix = $"_{firstID}_{secondID}";
            string value = $"pattern_{firstName}_then_{secondName}";
            int prefixLength = Math.Min(value.Length, 120 - suffix.Length);
            return value[..prefixLength] + suffix;
        }
    }
}
