using OPEN_LRD.Maintenance;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using OPEN_LRD.Persistence;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.SmokeTests
{
    internal static class MaintenanceTests
    {
        public static void RunAll()
        {
            Program.Run("Maintenance defaults to TTL only", TestDefaultsToTtlOnly);
            Program.Run("Metadata ingestion materializes typed graph facts", TestMetadataMaterialization);
            Program.Run("Metadata reconciliation preserves other link origins", TestMetadataReconciliation);
            Program.Run("Metadata maintenance rescan is opt-in", TestMetadataMaintenanceRescan);
            Program.Run("Metadata value budget prevents partial graph mutation", TestMetadataBudget);
            Program.Run("Maintenance merges equal DataPointers across types", TestEqualPointersMergeAcrossTypes);
            Program.Run("Maintenance blocks distinct DataPointers", TestDistinctPointersBlockMerge);
            Program.Run("Maintenance transfers DataPointer", TestPointerTransfer);
            Program.Run("Maintenance merges synonym-linked nodes", TestSemanticMerge);
            Program.Run("Maintenance applies composition rules", TestComposition);
            Program.Run("Maintenance composition is idempotent", TestCompositionIdempotence);
            Program.Run("Maintenance abstracts repeated patterns", TestPatternAbstraction);
            Program.Run("Maintenance invalidates superseded links", TestPruningAndPersistence);
            Program.Run("Maintenance prunes transitive generic links", TestTransitivePruning);
            Program.Run("Maintenance budgets cap node merges", TestMergeBudget);
        }

        private static void TestDefaultsToTtlOnly()
        {
            LRD_Core core = NewCore();
            Web_Node first = Node(NodeTypes.Concepts, "Same");
            Web_Node second = Node(NodeTypes.Concepts, "Same");
            core.AddExistingNode(first);
            core.AddExistingNode(second);

            MaintenanceReport report = core.RunMaintenanceCycle();
            Assert(core.GetDictionary()!.Count == 2, "default maintenance must not consolidate nodes");
            Assert(report.NodesMerged == 0, "default report should show no reasoning merges");
        }

        private static void TestEqualPointersMergeAcrossTypes()
        {
            LRD_Core core = NewCore();
            Uri location = new Uri("https://example.test/resource/42");
            Web_Node survivor = Node(NodeTypes.Concepts, "Primary", NodeLifetime.Permanent);
            Web_Node duplicate = Node(NodeTypes.Events, "Different label");
            Web_Node neighbor = Node(NodeTypes.Concepts, "Neighbor");
            survivor.Set_DataPointer(new DataPointer(location, "text/plain", 42, "abc"));
            duplicate.Set_DataPointer(new DataPointer(location, "text/plain", 42, "abc"));
            core.AddExistingNode(survivor);
            core.AddExistingNode(duplicate);
            core.AddExistingNode(neighbor);
            core.AddDirectedConnection(duplicate.ID, neighbor.ID, LinkClass.events, OntologyRelations.Causes);
            core.AddDirectedConnection(survivor.ID, neighbor.ID, LinkClass.events, OntologyRelations.Causes);
            core.AddDirectedConnection(duplicate.ID, neighbor.ID, LinkClass.events, OntologyRelations.Causes,
                LinkOrigin.Reasoning);
            core.AddDirectedConnection(survivor.ID, duplicate.ID, LinkClass.basic);

            MaintenanceReport report = core.RunMaintenanceCycle(ReasoningOptions());
            Assert(report.NodesMerged == 1, "equal DataPointers should force a merge across node types");
            Assert(core.GetExistingNode(survivor.ID) is not null, "permanent node should survive");
            Assert(core.GetExistingNode(duplicate.ID) is null, "duplicate node should be removed");
            Assert(survivor.Get_DataPointer() is not null, "survivor should retain its pointer");
            Assert(core.GetLinks(survivor.ID).Any(link => link.DestinationID == neighbor.ID
                && link.ForwardLink.Id == OntologyRelations.Causes), "duplicate's outgoing link should be rewired");
            Link mergedLink = core.GetLinks(survivor.ID).Single(link => link.DestinationID == neighbor.ID
                && link.ForwardLink.Id == OntologyRelations.Causes);
            Assert((mergedLink.Origins & (LinkOrigin.Explicit | LinkOrigin.Reasoning))
                == (LinkOrigin.Explicit | LinkOrigin.Reasoning),
                "rewiring should preserve and union link origins");
            Assert(!survivor.Get_ReferenceLinks().Any(link => link.SourceID == link.DestinationID),
                "merge should discard resulting self-links");
        }

        private static void TestDistinctPointersBlockMerge()
        {
            LRD_Core core = NewCore();
            Web_Node first = Node(NodeTypes.Concepts, "Same label");
            Web_Node second = Node(NodeTypes.Concepts, "Same label");
            first.Set_DataPointer(DataPointerFor("https://example.test/a"));
            second.Set_DataPointer(DataPointerFor("https://example.test/b"));
            core.AddExistingNode(first);
            core.AddExistingNode(second);

            MaintenanceReport report = core.RunMaintenanceCycle(ReasoningOptions());
            Assert(core.GetDictionary()!.Count == 2, "distinct pointers must veto a label-based merge");
            Assert(report.Diagnostics.Any(message => message.Contains("DataPointers differ", StringComparison.Ordinal)),
                "report should explain the DataPointer veto");
        }

        private static void TestMetadataMaterialization()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node owner = Node(NodeTypes.Entities, "Alice", NodeLifetime.Permanent);
            owner.Add_Alias("Ally");
            owner.Set_DataPointer(new DataPointer(
                new Uri("https://example.test/alice"), "text/plain", 5, "abc123"));
            core.AddExistingNode(owner);

            MaintenanceReport first = core.IngestNodeMetadata(owner.ID);
            Assert(first.MetadataFactsAdded == 7, "label, alias, type, and four DataPointer facts should materialize");
            Assert(owner.Get_ReferenceLinks().All(link =>
                (link.Origins & LinkOrigin.NodeMetadata) != 0), "materialized facts should record metadata origin");

            Link labelLink = owner.Get_ReferenceLinks().Single(link => link.IsActive
                && link.ForwardLink.Id == OntologyRelations.IsNamed);
            Web_Node labelValue = core.GetExistingNode(labelLink.DestinationID)!;
            Assert(labelValue.Get_NodeType().Id == OntologyTypes.MetadataValues.Id
                && labelValue.Label == "Label: Alice", "label should use the dedicated metadata value type");

            Link aliasLink = owner.Get_ReferenceLinks().Single(link => link.IsActive
                && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "has_alias");
            Assert(core.GetExistingNode(aliasLink.DestinationID)!.Label == "Alias: Ally",
                "alias should be represented by a reusable value node");

            Link typeLink = owner.Get_ReferenceLinks().Single(link => link.IsActive
                && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "has_node_type");
            Assert(core.GetExistingNode(typeLink.DestinationID)!.Label == $"NodeType: {NodeTypes.Entities.Id}: {NodeTypes.Entities.Name}",
                "NodeType should be represented by ID and name");

            Link resourceLink = owner.Get_ReferenceLinks().Single(link => link.IsActive
                && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "references_data");
            Web_Node resource = core.GetExistingNode(resourceLink.DestinationID)!;
            Assert(resource.Get_DataPointer() is null,
                "generated resource values must not copy the owner's pointer and trigger pointer-based merging");
            Assert(resource.Get_NodeType().Id == OntologyTypes.MetadataValues.Id,
                "resource value should use the dedicated metadata node type");

            MaintenanceReport repeated = core.IngestNodeMetadata(owner.ID);
            Assert(repeated.MetadataFactsAdded == 0 && repeated.MetadataValuesCreated == 0,
                "repeated explicit ingestion should be idempotent");
            Assert(owner.Get_ReferenceLinks().Count(link => (link.Origins & LinkOrigin.NodeMetadata) != 0) == 4,
                "idempotent ingestion should keep one owner link per top-level fact");
            Assert(resource.Get_ReferenceLinks().Count(link => (link.Origins & LinkOrigin.NodeMetadata) != 0) == 3,
                "DataPointer fields should be attached as facts to the resource value node");

            Web_Node otherOwner = Node(NodeTypes.Entities, "Different owner", NodeLifetime.Permanent);
            otherOwner.Set_DataPointer(new DataPointer(
                new Uri("https://example.test/alice"), "text/plain", 5, "different-checksum"));
            core.AddExistingNode(otherOwner);
            core.IngestNodeMetadata(otherOwner.ID);
            Link otherResourceLink = otherOwner.Get_ReferenceLinks().Single(link => link.IsActive
                && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "references_data");
            Assert(otherResourceLink.DestinationID != resource.ID,
                "same URI with different DataPointer metadata should use a distinct resource value node");

            Web_Node resourceVariants = Node(NodeTypes.Entities, "Third owner", NodeLifetime.Permanent);
            resourceVariants.Set_DataPointer(new DataPointer(
                new Uri("https://example.test/alice"), "text/plain", 5, "another-checksum"));
            core.AddExistingNode(resourceVariants);
            core.IngestNodeMetadata(resourceVariants.ID);
            Link variantResourceLink = resourceVariants.Get_ReferenceLinks().Single(link => link.IsActive
                && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "references_data");
            Assert(variantResourceLink.DestinationID != otherResourceLink.DestinationID,
                "resource labels must remain distinct when pointer metadata differs");

            core.RunMaintenanceCycle(ReasoningOptions(metadata: false));
            Assert(core.GetExistingNode(owner.ID) is not null,
                "maintenance node consolidation must not merge owner with generated metadata values");
            Assert(core.GetExistingNode(otherOwner.ID) is not null,
                "metadata resource values must not trigger DataPointer-based owner merges");
            Assert(core.GetExistingNode(resourceVariants.ID) is not null,
                "different DataPointer variants must not merge through generated value labels");
        }

        private static void TestMetadataReconciliation()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node owner = Node(NodeTypes.Entities, "Old label");
            owner.Add_Alias("Old alias");
            owner.Set_DataPointer(DataPointerFor("https://example.test/old-resource"));
            core.AddExistingNode(owner);
            core.IngestNodeMetadata(owner.ID);

            Link oldLabel = owner.Get_ReferenceLinks().Single(link => link.ForwardLink.Id == OntologyRelations.IsNamed);
            Link oldAlias = owner.Get_ReferenceLinks().Single(link =>
                ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "has_alias");
            Link oldResource = owner.Get_ReferenceLinks().Single(link =>
                ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "references_data");

            // This independent assertion shares the label edge with metadata.
            core.AddDirectedConnection(owner.ID, oldLabel.DestinationID,
                OntologyRelations.LinkClassFor("is_named"), OntologyRelations.IsNamed);
            owner.Set_Label("New label");
            owner.Remove_Alias("Old alias");
            owner.Set_DataPointer(DataPointerFor("https://example.test/new-resource"));

            MaintenanceReport reconciled = core.IngestNodeMetadata(owner.ID);
            Assert(reconciled.MetadataFactsInvalidated == 3 && reconciled.LinksInvalidated == 2,
                "stale label, alias, and resource supports should be removed; only unsupported edges are inactivated");
            Assert(oldLabel.IsActive && oldLabel.Origins == LinkOrigin.Explicit,
                "stale label fact with explicit support should remain active");
            Assert(!oldAlias.IsActive && oldAlias.Origins == LinkOrigin.None,
                "stale alias fact with no other support should become inactive and retain no metadata origin");
            Assert(!oldResource.IsActive, "old DataPointer resource link should be inactivated");

            string path = Path.Combine(Path.GetTempPath(), "open-lrd-metadata-" + Guid.NewGuid().ToString("N") + ".lrdlog");
            try
            {
                CoreLogExporter.Export(core, path, includeTransient: false);
                LRD_Core restored = NewCore();
                PersistenceImportResult import = CoreLogImporter.ImportCore(restored, path, ImportMode.Replace);
                Assert(import.Success, "metadata provenance log should import successfully");
                Web_Node restoredOwner = restored.GetExistingNode(owner.ID)!;
                Assert(restoredOwner.Get_ReferenceLinks().Any(link => !link.IsActive
                    && link.Origins == LinkOrigin.None
                    && ForwardLinkTypes.FromId(link.ForwardLink.Id).Name == "has_alias"),
                    "inactive metadata provenance should survive persistence");
                Assert(restoredOwner.Get_ReferenceLinks().Any(link => link.IsActive
                    && (link.Origins & LinkOrigin.NodeMetadata) != 0),
                    "active metadata link origins should survive persistence");
                Assert(restoredOwner.Get_ReferenceLinks().Any(link => link.IsActive
                    && link.Origins == LinkOrigin.Explicit
                    && link.DestinationID == oldLabel.DestinationID),
                    "explicit origin should survive persistence and stale-metadata reconciliation");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void TestMetadataMaintenanceRescan()
        {
            LRD_Core core = NewCore();
            Web_Node owner = Node(NodeTypes.Entities, "Rescanned node");
            core.AddExistingNode(owner);
            MaintenanceOptions options = ReasoningOptions(metadata: true) with
            {
                EnableNodeMerging = false,
                EnableCompositionalInduction = false,
                EnableFrequentPatternAbstraction = false,
                EnableConceptRelationElevation = true,
                EnableSupersededLinkPruning = false
            };

            MaintenanceReport report = core.RunMaintenanceCycle(options);
            Assert(report.MetadataFactsAdded == 2, "maintenance rescan should materialize label and type facts");
            Assert(report.MetadataNodesExamined == 1, "rescan should report its examined owner node");
            Assert(owner.Get_ReferenceLinks().Any(link => link.IsActive
                && (link.Origins & LinkOrigin.NodeMetadata) != 0), "rescan should add metadata-origin links");
        }

        private static void TestMetadataBudget()
        {
            LRD_Core core = NewCore();
            Web_Node owner = Node(NodeTypes.Entities, "Budgeted node");
            core.AddExistingNode(owner);
            MaintenanceOptions options = ReasoningOptions(metadata: true) with
            {
                EnableNodeMerging = false,
                EnableCompositionalInduction = false,
                EnableFrequentPatternAbstraction = false,
                EnableConceptRelationElevation = false,
                EnableSupersededLinkPruning = false,
                MaxMetadataValuesCreatedPerCycle = 0
            };

            MaintenanceReport report = core.RunMaintenanceCycle(options);
            Assert(report.WorkBudgetExhausted, "zero metadata value budget should be reported");
            Assert(core.GetDictionary()!.Count == 1, "metadata budget should prevent value-node creation");
            Assert(core.GetLinks(owner.ID).Count == 0, "metadata budget should prevent partial fact links");

            MaintenanceReport invalid = core.IngestNodeMetadata(Guid.Empty);
            Assert(invalid.Diagnostics.Count > 0, "empty node ID should return a diagnostic");
            Assert(core.GetDictionary()!.Count == 1, "invalid metadata ingestion must not mutate the graph");
        }

        private static void TestPointerTransfer()
        {
            LRD_Core core = NewCore();
            Web_Node first = Node(NodeTypes.Concepts, "Same");
            Web_Node second = Node(NodeTypes.Concepts, "Same");
            first.Set_DataPointer(DataPointerFor("https://example.test/one"));
            core.AddExistingNode(first);
            core.AddExistingNode(second);

            core.RunMaintenanceCycle(ReasoningOptions());
            Assert(core.GetDictionary()!.Count == 1, "same-type label matches should consolidate");
            Web_Node survivor = core.GetDictionary()!.Values.Single();
            Assert(survivor.Get_DataPointer() is not null, "non-null pointer should transfer to the survivor");
        }

        private static void TestSemanticMerge()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node first = Node(NodeTypes.Concepts, "First label");
            Web_Node second = Node(NodeTypes.Concepts, "Second label");
            core.AddExistingNode(first);
            core.AddExistingNode(second);
            core.AddDirectedConnection(first.ID, second.ID, OntologyTypes.lexicon, OntologyRelations.Synonym);

            MaintenanceReport report = core.RunMaintenanceCycle(ReasoningOptions());
            Assert(report.NodesMerged == 1, "synonym-linked compatible nodes should consolidate");
            Assert(core.GetDictionary()!.Count == 1, "semantic merge should remove the duplicate node");
        }

        private static void TestComposition()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node a = Node(NodeTypes.Concepts, "A");
            Web_Node b = Node(NodeTypes.Concepts, "B");
            Web_Node c = Node(NodeTypes.Concepts, "C");
            core.AddExistingNode(a);
            core.AddExistingNode(b);
            core.AddExistingNode(c);
            core.AddDirectedConnection(a.ID, b.ID, OntologyTypes.ontology, OntologyRelations.PartOf);
            core.AddDirectedConnection(b.ID, c.ID, OntologyTypes.ontology, OntologyRelations.PartOf);

            core.RunMaintenanceCycle(ReasoningOptions(
                composition: true,
                patterns: false,
                elevation: false,
                pruning: false));
            Assert(core.GetLinks(a.ID).Any(link => link.DestinationID == c.ID
                && link.ForwardLink.Id == OntologyRelations.PartOf), "part_of chain should compose to a direct link");
        }

        private static void TestCompositionIdempotence()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node a = Node(NodeTypes.Concepts, "A");
            Web_Node b = Node(NodeTypes.Concepts, "B");
            Web_Node c = Node(NodeTypes.Concepts, "C");
            core.AddExistingNode(a);
            core.AddExistingNode(b);
            core.AddExistingNode(c);
            core.AddDirectedConnection(a.ID, b.ID, OntologyTypes.ontology, OntologyRelations.PartOf);
            core.AddDirectedConnection(b.ID, c.ID, OntologyTypes.ontology, OntologyRelations.PartOf);

            core.RunMaintenanceCycle(ReasoningOptions(composition: true, pruning: false));
            MaintenanceReport repeated = core.RunMaintenanceCycle(ReasoningOptions(composition: true, pruning: false));
            Assert(repeated.LinksGenerated == 0, "repeated composition should not add duplicate links");
        }

        private static void TestPatternAbstraction()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            RelationLinkType firstRelation = RelationRegistry.GetOrRegister("maintenance_test_alpha");
            RelationLinkType secondRelation = RelationRegistry.GetOrRegister("maintenance_test_beta");
            LRD_Core core = NewCore();

            for (int index = 0; index < 5; index++)
            {
                Web_Node source = Node(NodeTypes.Concepts, "source " + index);
                Web_Node middle = Node(NodeTypes.Concepts, "middle " + index);
                Web_Node destination = Node(NodeTypes.Concepts, "destination " + index);
                core.AddExistingNode(source);
                core.AddExistingNode(middle);
                core.AddExistingNode(destination);
                core.AddDirectedConnection(source.ID, middle.ID, OntologyTypes.ontology, firstRelation.Id);
                core.AddDirectedConnection(middle.ID, destination.ID, OntologyTypes.ontology, secondRelation.Id);
            }

            core.RunMaintenanceCycle(ReasoningOptions(
                composition: false,
                patterns: true,
                elevation: true,
                pruning: false));

            string compositeName = "pattern_" + firstRelation.Name + "_then_" + secondRelation.Name
                + "_" + firstRelation.Id + "_" + secondRelation.Id;
            Assert(RelationRegistry.TryGet(compositeName, out RelationLinkType composite),
                "frequent pattern should register one synthetic relation type");
            Web_Node relationNode = core.GetDictionary()!.Values.Single(node => node.Get_NodeType().Id == OntologyTypes.Relations.Id
                && string.Equals(node.Label, composite.Name, StringComparison.OrdinalIgnoreCase));
            Assert(relationNode.IsPermanent,
                "generated relation should be elevated to a Relation concept node");
            Web_Node relationRoot = core.FindByLabel("Relation", OntologyTypes.Categories).Single();
            Assert(core.GetLinks(relationNode.ID).Any(link => link.DestinationID == relationRoot.ID
                && link.ForwardLink.Id == OntologyRelations.IsA),
                "generated relation concept should link to the Relation category");
            Assert(core.GetDictionary()!.Values.Any(node => core.GetLinks(node.ID).Any(link =>
                link.ForwardLink.Id == composite.Id)), "repeated paths should gain direct links");
        }

        private static void TestPruningAndPersistence()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node a = Node(NodeTypes.Concepts, "A");
            Web_Node b = Node(NodeTypes.Concepts, "B");
            Web_Node c = Node(NodeTypes.Concepts, "C");
            core.AddExistingNode(a);
            core.AddExistingNode(b);
            core.AddExistingNode(c);
            core.AddDirectedConnection(a.ID, c.ID, LinkClass.basic, ForwardLinkTypes.basic.Id);
            core.AddDirectedConnection(a.ID, c.ID, OntologyTypes.ontology, OntologyRelations.IsA);
            core.AddDirectedConnection(a.ID, b.ID, OntologyTypes.ontology, OntologyRelations.PartOf);
            core.AddDirectedConnection(b.ID, c.ID, OntologyTypes.ontology, OntologyRelations.PartOf);

            core.RunMaintenanceCycle(ReasoningOptions(
                composition: false,
                patterns: false,
                elevation: false,
                pruning: true));

            Link generic = a.Get_ReferenceLinks().Single(link => link.ForwardLink.Id == ForwardLinkTypes.basic.Id);
            Assert(!generic.IsActive, "base link should be invalidated when advanced same-endpoint link exists");
            Assert(generic.Equals(new Link(a.ID, c.ID, ForwardLinkTypes.basic.Id,
                InverseLinkTypes.basic.Id, LinkClass.basic)),
                "active state should not change link identity equality");
            Assert(core.GetLinks(a.ID).All(link => link.ForwardLink.Id != ForwardLinkTypes.basic.Id),
                "active link query must hide invalidated links");
            Assert(GraphSnapshot.FromCore(core).Links.All(link => link.ForwardTypeId != ForwardLinkTypes.basic.Id),
                "visualizer snapshot must exclude invalidated links");

            string path = Path.Combine(Path.GetTempPath(), "open-lrd-maintenance-" + Guid.NewGuid().ToString("N") + ".lrdlog");
            try
            {
                CoreLogExporter.Export(core, path, includeTransient: false);
                LRD_Core restored = NewCore();
                PersistenceImportResult import = CoreLogImporter.ImportCore(restored, path, ImportMode.Replace);
                Assert(import.Success, "maintenance log should import successfully");
                Web_Node restoredA = restored.GetExistingNode(a.ID)!;
                Assert(restoredA.Get_ReferenceLinks().Any(link => !link.IsActive
                    && link.ForwardLink.Id == ForwardLinkTypes.basic.Id), "inactive state should survive persistence");
            Assert(restored.GetLinks(a.ID).All(link => link.ForwardLink.Id != ForwardLinkTypes.basic.Id),
                    "restored active view should keep invalidated link hidden");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void TestTransitivePruning()
        {
            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            LRD_Core core = NewCore();
            Web_Node a = Node(NodeTypes.Concepts, "A");
            Web_Node b = Node(NodeTypes.Concepts, "B");
            Web_Node c = Node(NodeTypes.Concepts, "C");
            core.AddExistingNode(a);
            core.AddExistingNode(b);
            core.AddExistingNode(c);
            core.AddDirectedConnection(a.ID, c.ID, LinkClass.basic, ForwardLinkTypes.basic.Id);
            core.AddDirectedConnection(a.ID, b.ID, OntologyTypes.ontology, OntologyRelations.PartOf);
            core.AddDirectedConnection(b.ID, c.ID, OntologyTypes.ontology, OntologyRelations.PartOf);

            core.RunMaintenanceCycle(ReasoningOptions(
                composition: false,
                patterns: false,
                elevation: false,
                pruning: true));
            Assert(a.Get_ReferenceLinks().Any(link => link.ForwardLink.Id == ForwardLinkTypes.basic.Id && !link.IsActive),
                "bounded advanced path should invalidate the direct generic link");
        }

        private static void TestMergeBudget()
        {
            LRD_Core core = NewCore();
            Web_Node first = Node(NodeTypes.Concepts, "Same");
            Web_Node second = Node(NodeTypes.Concepts, "Same");
            core.AddExistingNode(first);
            core.AddExistingNode(second);
            MaintenanceReport report = core.RunMaintenanceCycle(new MaintenanceOptions
            {
                EnableReasoningMaintenance = true,
                EnableNodeMerging = true,
                EnableNodeMetadataReasoning = false,
                EnableCompositionalInduction = false,
                EnableFrequentPatternAbstraction = false,
                EnableConceptRelationElevation = false,
                EnableSupersededLinkPruning = false,
                MaxMergesPerCycle = 0
            });

            Assert(report.NodesMerged == 0 && report.WorkBudgetExhausted,
                "zero merge budget should stop consolidation and report the limit");
            Assert(core.GetDictionary()!.Count == 2, "merge budget must prevent mutations");
        }

        private static MaintenanceOptions ReasoningOptions(
            bool composition = false,
            bool patterns = false,
            bool elevation = false,
            bool pruning = false,
            bool metadata = false)
        {
            return new MaintenanceOptions
            {
                EnableReasoningMaintenance = true,
                EnableNodeMerging = true,
                EnableNodeMetadataReasoning = metadata,
                EnableCompositionalInduction = composition,
                EnableFrequentPatternAbstraction = patterns,
                EnableConceptRelationElevation = elevation,
                EnableSupersededLinkPruning = pruning
            };
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static Web_Node Node(NodeTypes type, string label, NodeLifetime lifetime = NodeLifetime.Temporary)
        {
            Web_Node node = new Web_Node(Guid.NewGuid(), type, lifetime, 1000);
            node.Set_Label(label);
            return node;
        }

        private static DataPointer DataPointerFor(string uri) => new DataPointer(new Uri(uri));

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
