using OPEN_LRD.Nodes;

namespace OPEN_LRD.Ontology
{
    // Creates the permanent upper-ontology foundation. Idempotent: existing
    // Category nodes are reused by label, so repeated calls only add what is
    // missing.
    internal static class UpperOntologyBootstrap
    {
        public static int Populate(LRD_Core core)
        {
            ArgumentNullException.ThrowIfNull(core);

            if (!core.initialized)
                throw new InvalidOperationException(
                    "The core must be initialized before seeding the ontology.");

            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();

            Dictionary<string, Guid> categories = new(StringComparer.OrdinalIgnoreCase);
            int created = 0;

            foreach (OntologyConcept concept in OntologyCatalog.Concepts)
                categories[concept.Label] = GetOrCreateCategory(core, concept.Label, ref created);

            foreach (string partOfSpeech in OntologyCatalog.PartOfSpeechLabels)
                categories[partOfSpeech] = GetOrCreateCategory(core, partOfSpeech, ref created);

            foreach (string reality in OntologyCatalog.RealityLabels)
                categories[reality] = GetOrCreateCategory(core, reality, ref created);

            foreach (OntologyConcept concept in OntologyCatalog.Concepts)
            {
                if (concept.ParentLabel is null)
                    continue;

                if (categories.TryGetValue(concept.Label, out Guid child)
                    && categories.TryGetValue(concept.ParentLabel, out Guid parent))
                {
                    core.AddDirectedConnection(child, parent, OntologyTypes.ontology, OntologyRelations.IsA);
                }
            }

            if (categories.TryGetValue(OntologyCatalog.RealLabel, out Guid real))
            {
                HashSet<string> realityLabels = new HashSet<string>(
                    OntologyCatalog.RealityLabels,
                    StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, Guid> category in categories)
                {
                    // Reality nodes are statuses themselves, not entities with
                    // a reality status, so they stay outside the Real marking.
                    if (category.Value == real || realityLabels.Contains(category.Key))
                        continue;

                    core.AddDirectedConnection(
                        category.Value,
                        real,
                        OntologyTypes.ontology,
                        OntologyRelations.HasReality);
                }
            }

            return created;
        }

        internal static Guid GetOrCreateCategory(LRD_Core core, string label, ref int created)
        {
            IReadOnlyList<Web_Node> existing = core.FindByLabel(label, OntologyTypes.Categories);

            if (existing.Count > 0)
                return existing[0].Get_ID();

            Web_Node node = new Web_Node(Guid.NewGuid(), OntologyTypes.Categories, NodeLifetime.Permanent, 0);
            node.Set_Label(label);
            core.AddExistingNode(node);
            created++;
            return node.Get_ID();
        }

        internal static Guid? FindCategory(LRD_Core core, string label)
        {
            IReadOnlyList<Web_Node> existing = core.FindByLabel(label, OntologyTypes.Categories);
            return existing.Count > 0 ? existing[0].Get_ID() : null;
        }
    }
}
