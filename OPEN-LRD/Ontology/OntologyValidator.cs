using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Ontology
{
    // Advisory domain/range checks and reality-status resolution. Nothing here
    // blocks ingestion; the matcher enforcement is a later extension.
    internal static class OntologyValidator
    {
        private static readonly Dictionary<string, (string[] Domain, string[] Range)> DomainRange =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["is_a"] = (new[] { "Concept", "Category", "Lexeme" }, new[] { "Concept", "Category", "Lexeme" }),
                ["has_time"] = (new[] { "Event", "Process", "Activity", "State" }, new[] { "Time" }),
                ["has_location"] = (
                    new[] { "Event", "Entity", "Object", "Person", "Location" },
                    new[] { "Location" }),
                ["has_property"] = (
                    new[] { "Entity", "Object", "Person", "PhysicalEntity" },
                    new[] { "Property" }),
                ["is_named"] = (new[] { "Concept", "Sense", "Category" }, new[] { "Lexeme" })
            };

        public static bool IsRelationAllowed(string relationName, string? domainLabel, string? rangeLabel)
        {
            if (!DomainRange.TryGetValue(relationName, out (string[] Domain, string[] Range) rule))
                return true;

            return Matches(rule.Domain, domainLabel) && Matches(rule.Range, rangeLabel);
        }

        private static bool Matches(string[] allowed, string? label)
        {
            if (label is null)
                return true;

            return allowed.Any(candidate => string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase));
        }

        // Walks is_a links from the node. A node is Real unless it reaches one
        // of the non-real reality concepts.
        public static string GetRealityStatus(LRD_Core core, Guid nodeId)
        {
            ArgumentNullException.ThrowIfNull(core);

            Web_Node? start = core.GetExistingNode(nodeId);

            if (start is null)
                return OntologyCatalog.RealLabel;

            HashSet<Guid> visited = new HashSet<Guid>();
            Queue<Web_Node> queue = new Queue<Web_Node>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Web_Node current = queue.Dequeue();

                if (!visited.Add(current.Get_ID()))
                    continue;

                if (current.Label is string label && IsRealityLabel(label))
                    return label;

                foreach (Link link in current.Get_ReferenceLinks())
                {
                    if (!link.IsActive)
                        continue;

                    if (link.SourceID != current.Get_ID() || link.ForwardLink.Id != OntologyRelations.IsA)
                        continue;

                    Web_Node? parent = core.GetExistingNode(link.DestinationID);

                    if (parent is not null)
                        queue.Enqueue(parent);
                }
            }

            return OntologyCatalog.RealLabel;
        }

        private static bool IsRealityLabel(string label)
        {
            foreach (string reality in OntologyCatalog.RealityLabels)
            {
                if (string.Equals(reality, label, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
