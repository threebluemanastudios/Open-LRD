using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;

namespace OPEN_LRD.Maintenance
{
    internal static class NodeMetadataReasoner
    {
        private const string MetadataValueKeyPrefix = "metadata-value-key:";

        private sealed record MetadataFact(
            string Field,
            string ValueKey,
            string DisplayValue,
            string RelationName,
            LinkClass LinkClass,
            bool IsResourceProperty = false);

        public static MaintenanceReport IngestNode(LRD_Core core, Guid nodeID)
        {
            MaintenanceReport report = new MaintenanceReport();
            MaintenanceOptions options = new MaintenanceOptions
            {
                EnableReasoningMaintenance = true,
                EnableNodeMetadataReasoning = true
            };
            HashSet<int> generatedRelationIds = new HashSet<int>();

            if (!core.initialized)
            {
                report.AddDiagnostic("The core must be initialized before ingesting node metadata.");
            }
            else if (nodeID == Guid.Empty)
            {
                report.AddDiagnostic("A non-empty node ID is required for metadata ingestion.");
            }
            else
            {
                report.NodesExamined = 1;
                report.MetadataNodesExamined = 1;
                ProcessNode(core, nodeID, options, report, generatedRelationIds);
                if (generatedRelationIds.Count > 0)
                    LinkTypeGenerator.ElevateRelations(core, generatedRelationIds, report, options);
            }

            report.Complete();
            return report;
        }

        public static void RunMaintenance(
            LRD_Core core,
            MaintenanceOptions options,
            MaintenanceReport report,
            ISet<int> generatedRelationIds)
        {
            IReadOnlyDictionary<Guid, Web_Node>? graph = core.GetDictionary();
            if (graph is null)
                return;

            Web_Node[] owners = graph.Values
                .Where(node => !node.IsDisposed && node.Get_NodeType().Id != OntologyTypes.MetadataValues.Id)
                .OrderBy(node => node.ID)
                .Take(options.MaxNodesToExaminePerCycle)
                .ToArray();

            int eligibleCount = graph.Values.Count(node => !node.IsDisposed
                && node.Get_NodeType().Id != OntologyTypes.MetadataValues.Id);
            if (owners.Length < eligibleCount)
            {
                report.WorkBudgetExhausted = true;
                report.AddDiagnostic("Node metadata rescan reached its per-cycle node budget.");
            }

            report.NodesExamined += owners.Length;
            foreach (Web_Node owner in owners)
            {
                report.MetadataNodesExamined++;
                ProcessNode(core, owner.ID, options, report, generatedRelationIds);
            }
        }

        internal static string GetLinkKey(Link link)
        {
            return string.Join("|", link.SourceID, link.DestinationID,
                link.ForwardLink.Id, link.InverseLink.Id, link.Classification.Id);
        }

        private static void ProcessNode(
            LRD_Core core,
            Guid nodeID,
            MaintenanceOptions options,
            MaintenanceReport report,
            ISet<int> generatedRelationIds)
        {
            if (!core.TryGetNode(nodeID, out Web_Node? owner) || owner is null || owner.IsDisposed)
            {
                report.AddDiagnostic($"Node '{nodeID}' is missing or disposed; metadata was not processed.");
                return;
            }

            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            if (owner.Get_NodeType().Id == OntologyTypes.MetadataValues.Id)
                return;

            if (owner.Aliases.Count > options.MaxMetadataFactsPerNode)
            {
                report.WorkBudgetExhausted = true;
                report.AddDiagnostic(
                    $"Node '{owner.ID}' has {owner.Aliases.Count} aliases, exceeding the metadata fact limit of {options.MaxMetadataFactsPerNode}; it was left unchanged.");
                return;
            }

            List<MetadataFact> facts = BuildFacts(owner);
            if (report.MetadataFactsExamined + facts.Count > options.MaxMetadataFactsExaminedPerCycle)
            {
                report.WorkBudgetExhausted = true;
                report.AddDiagnostic("Node metadata rescan reached its per-cycle fact-examination budget.");
                return;
            }
            report.MetadataFactsExamined += facts.Count;
            if (facts.Count > options.MaxMetadataFactsPerNode)
            {
                report.WorkBudgetExhausted = true;
                report.AddDiagnostic(
                    $"Node '{owner.ID}' has {facts.Count} metadata facts, exceeding the per-node limit of {options.MaxMetadataFactsPerNode}; it was left unchanged.");
                return;
            }

            Dictionary<string, Guid> desiredLinks = new Dictionary<string, Guid>(StringComparer.Ordinal);
            Guid? resourceNodeID = null;
            bool complete = true;

            foreach (MetadataFact fact in facts)
            {
                RelationLinkType relation;
                bool relationExisted = RelationRegistry.TryGet(fact.RelationName, out relation!);
                if (!relationExisted
                    && report.RelationTypesRegistered >= options.MaxRelationTypesGeneratedPerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    report.AddDiagnostic("Metadata predicate registration reached its per-cycle budget.");
                    complete = false;
                    break;
                }

                relation = RelationRegistry.GetOrRegister(fact.RelationName);
                if (!relationExisted)
                {
                    report.RelationTypesRegistered++;
                    report.MetadataRelationsRegistered++;
                }
                if (!string.Equals(fact.RelationName, "is_named", StringComparison.OrdinalIgnoreCase))
                    generatedRelationIds.Add(relation.Id);

                Web_Node factOwner = owner;
                Guid sourceID = owner.ID;
                if (fact.IsResourceProperty)
                {
                    if (!resourceNodeID.HasValue
                        || !core.TryGetNode(resourceNodeID.Value, out Web_Node? resourceOwner)
                        || resourceOwner is null)
                    {
                        report.AddDiagnostic($"Node '{owner.ID}' has a pointer field without a resource value node.");
                        complete = false;
                        break;
                    }

                    factOwner = resourceOwner;
                    sourceID = resourceOwner.ID;
                }

                if (!core.TryGetNode(sourceID, out Web_Node? sourceNode) || sourceNode is null)
                {
                    report.AddDiagnostic($"Metadata fact source '{sourceID}' disappeared during processing.");
                    complete = false;
                    break;
                }

                string stableKey = CreateStableValueKey(fact.ValueKey);
                Guid valueID = CreateStableValueID(stableKey);
                Link desiredLink = new Link(
                    sourceID,
                    valueID,
                    relation.Forward,
                    relation.Inverse,
                    fact.LinkClass,
                    LinkOrigin.NodeMetadata);
                string linkKey = GetLinkKey(desiredLink);
                if (!fact.IsResourceProperty)
                    desiredLinks[linkKey] = valueID;

                Link? existing = sourceNode.Get_ReferenceLinks()
                    .FirstOrDefault(link => link.HasSameIdentity(desiredLink));
                bool hadMetadataSupport = existing is not null
                    && (existing.Origins & LinkOrigin.NodeMetadata) != 0;
                bool needsSupport = !hadMetadataSupport || existing is null || !existing.IsActive;

                if (needsSupport && report.LinksGenerated >= options.MaxLinksGeneratedPerCycle)
                {
                    report.WorkBudgetExhausted = true;
                    report.AddDiagnostic("Metadata link creation reached the shared per-cycle generation budget.");
                    complete = false;
                    break;
                }

                Web_Node? valueNode = GetOrCreateValueNode(
                    core,
                    factOwner,
                    fact,
                    stableKey,
                    valueID,
                    options,
                    report);
                if (valueNode is null)
                {
                    complete = false;
                    break;
                }
                if (fact.Field == "data-pointer")
                    resourceNodeID = valueNode.ID;

                sourceNode.Add_Link(desiredLink);
                if (needsSupport)
                {
                    report.LinksGenerated++;
                    report.MetadataFactsAdded++;
                }
            }

            if (!complete)
                return;

            HashSet<string> expectedLinkKeys = new HashSet<string>(desiredLinks.Keys, StringComparer.Ordinal);
            (int factsRemoved, int linksInvalidated) = core.ReconcileMetadataLinks(owner.ID, expectedLinkKeys);
            report.MetadataFactsInvalidated += factsRemoved;
            report.LinksInvalidated += linksInvalidated;
        }

        private static Web_Node? GetOrCreateValueNode(
            LRD_Core core,
            Web_Node owner,
            MetadataFact fact,
            string stableKey,
            Guid valueID,
            MaintenanceOptions options,
            MaintenanceReport report)
        {
            string metadataAlias = MetadataValueKeyPrefix + stableKey;

            if (core.TryGetNode(valueID, out Web_Node? existing) && existing is not null)
            {
                if (existing.IsDisposed)
                    core.RemoveExistingNode(valueID);
                else if (existing.Get_NodeType().Id != OntologyTypes.MetadataValues.Id
                    || !existing.Has_Alias(metadataAlias))
                {
                    report.AddDiagnostic(
                        $"Metadata value ID collision for field '{fact.Field}' on node '{owner.ID}'; no fact was created.");
                    return null;
                }
                else
                {
                    if (owner.IsPermanent)
                        existing.PromoteToPermanent();
                    return existing;
                }
            }

            if (report.MetadataValuesCreated >= options.MaxMetadataValuesCreatedPerCycle)
            {
                report.WorkBudgetExhausted = true;
                report.AddDiagnostic("Metadata value-node creation reached its per-cycle budget.");
                return null;
            }

            NodeLifetime lifetime = owner.IsPermanent ? NodeLifetime.Permanent : NodeLifetime.Temporary;
            int ttl = lifetime == NodeLifetime.Permanent ? 0 : options.MetadataValueNodeTimeToLive;
            Web_Node valueNode = new Web_Node(valueID, OntologyTypes.MetadataValues, lifetime, ttl);
            valueNode.Set_Label(fact.DisplayValue);
            valueNode.Add_Alias(metadataAlias);

            if (!core.AddExistingNode(valueNode))
            {
                report.AddDiagnostic($"Metadata value node '{valueID}' could not be added to the core.");
                return null;
            }

            report.MetadataValuesCreated++;
            report.NodesCreated++;
            return valueNode;
        }

        private static List<MetadataFact> BuildFacts(Web_Node owner)
        {
            List<MetadataFact> facts = new List<MetadataFact>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            LinkClass nameClass = OntologyTypes.lexicon;
            LinkClass ontologyClass = OntologyTypes.ontology;

            if (!string.IsNullOrWhiteSpace(owner.Label))
            {
                string label = owner.Label.Trim();
                facts.Add(new MetadataFact(
                    "label",
                    "label|" + Normalize(label),
                    "Label: " + label,
                    RelationRegistry.TryGetById(OntologyRelations.IsNamed, out RelationLinkType named)
                        ? named.Name
                        : "is_named",
                    nameClass));
            }

            foreach (string alias in owner.Aliases.OrderBy(value => Normalize(value), StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;
                string normalized = Normalize(alias);
                if (!seen.Add(normalized))
                    continue;
                facts.Add(new MetadataFact(
                    "alias",
                    "alias|" + normalized,
                    "Alias: " + alias.Trim(),
                    "has_alias",
                    nameClass));
            }

            NodeTypes type = owner.Get_NodeType();
            facts.Add(new MetadataFact(
                "node-type",
                $"node-type|{type.Id}|{Normalize(type.Name)}",
                $"NodeType: {type.Id}: {type.Name}",
                "has_node_type",
                ontologyClass));

            DataPointer? pointer = owner.Get_DataPointer();
            if (pointer is not null)
            {
                string pointerDocument = JsonSerializer.Serialize(new
                {
                    location = pointer.Location.AbsoluteUri,
                    mediaType = pointer.MediaType,
                    byteLength = pointer.ByteLength,
                    checksum = pointer.Checksum
                });
                string resourceKey = "resource|" + Hash(pointerDocument);
                facts.Add(new MetadataFact(
                    "data-pointer",
                    resourceKey,
                    "DataPointer: " + pointer.Location.AbsoluteUri + " [" + resourceKey["resource|".Length..] + "]",
                    "references_data",
                    ontologyClass));

                if (!string.IsNullOrWhiteSpace(pointer.MediaType))
                    facts.Add(ScalarFact("media-type", pointer.MediaType, "has_media_type", ontologyClass,
                        isResourceProperty: true));
                if (pointer.ByteLength.HasValue)
                {
                    string value = pointer.ByteLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    facts.Add(ScalarFact("byte-length", value, "has_byte_length", ontologyClass,
                        isResourceProperty: true));
                }
                if (!string.IsNullOrWhiteSpace(pointer.Checksum))
                    facts.Add(ScalarFact("checksum", pointer.Checksum, "has_checksum", ontologyClass,
                        isResourceProperty: true));
            }

            return facts
                .OrderBy(fact => fact.Field == "data-pointer" ? 0 : fact.IsResourceProperty ? 2 : 1)
                .ThenBy(fact => fact.Field, StringComparer.Ordinal)
                .ThenBy(fact => fact.ValueKey, StringComparer.Ordinal)
                .ToList();
        }

        private static MetadataFact ScalarFact(
            string field,
            string value,
            string relation,
            LinkClass linkClass,
            bool isResourceProperty = false)
        {
            string trimmed = value.Trim();
            return new MetadataFact(
                field,
                field + "|" + Normalize(trimmed),
                field + ": " + trimmed,
                relation,
                linkClass,
                isResourceProperty);
        }

        private static string CreateStableValueKey(string valueKey) => Hash(valueKey);

        private static Guid CreateStableValueID(string stableKey)
        {
            byte[] bytes = Convert.FromHexString(stableKey);
            bytes[0] |= 1;
            return new Guid(bytes.AsSpan(0, 16));
        }

        private static string Hash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private static string Normalize(string value)
        {
            return value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        }
    }
}
