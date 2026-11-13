using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.NLP
{
    internal sealed record RelationLinkType(
        int Id,
        string Name,
        string InverseName,
        ForwardLinkTypes Forward,
        InverseLinkTypes Inverse);

    internal static class RelationRegistry
    {
        public const int FirstPredicateLinkTypeId = 1000;
        public const int LastReservedOntologyId = 999;
        public const int AntonymLinkTypeId = 2000000;
        public const string AntonymRelationName = "antonym";

        private static readonly object sync = new object();
        private static readonly Dictionary<string, RelationLinkType> byName =
            new Dictionary<string, RelationLinkType>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, RelationLinkType> byId =
            new Dictionary<int, RelationLinkType>();
        private static long nextId = FirstPredicateLinkTypeId;

        static RelationRegistry()
        {
            foreach (string name in new[]
            {
                "is", "has", "does",
                "at", "in", "on", "by", "for", "with", "of", "from", "to",
                "during", "after", "before"
            })
            {
                GetOrRegister(name);
            }

            TryRestore(AntonymLinkTypeId, AntonymRelationName, "inverse_antonym", out _);
        }

        public static RelationLinkType GetOrRegister(string relationName)
        {
            string name = NormalizeRelationName(relationName);

            lock (sync)
            {
                return GetOrRegisterLocked(name);
            }
        }

        public static bool TryGet(string relationName, out RelationLinkType relation)
        {
            relation = null!;

            if (string.IsNullOrWhiteSpace(relationName))
                return false;

            string name = NormalizeRelationName(relationName);

            lock (sync)
            {
                return byName.TryGetValue(name, out relation!);
            }
        }

        public static bool TryGetById(int id, out RelationLinkType relation)
        {
            lock (sync)
            {
                return byId.TryGetValue(id, out relation!);
            }
        }

        internal static IReadOnlyList<RelationLinkType> GetAll()
        {
            lock (sync)
            {
                return byId.Values
                    .OrderBy(relation => relation.Id)
                    .ToArray();
            }
        }

        internal static bool TryRestore(
            int id,
            string name,
            string inverseName,
            out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(inverseName))
            {
                error = "Relation names cannot be empty.";
                return false;
            }

            string relationName = name.Trim();
            string inverse = inverseName.Trim();

            if (id <= LastReservedOntologyId && !IsOntologyRelationRegistered(id, relationName, inverse))
            {
                error = $"Relation ID {id} falls in the reserved ontology range.";
                return false;
            }

            lock (sync)
            {
                if (byId.TryGetValue(id, out RelationLinkType? existing))
                {
                    if (string.Equals(existing.Name, relationName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.InverseName, inverse, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    error = $"Relation ID {id} is already registered as '{existing.Name}'.";
                    return false;
                }

                if (byName.TryGetValue(relationName, out RelationLinkType? existingByName))
                {
                    error = $"Relation '{relationName}' is already registered with ID {existingByName.Id}.";
                    return false;
                }

                ForwardLinkTypes? existingForward = null;

                if (ForwardLinkTypes.TryFromId(id, out ForwardLinkTypes forwardMatch))
                {
                    if (!string.Equals(forwardMatch.Name, relationName, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Forward link type ID {id} is already registered as '{forwardMatch.Name}'.";
                        return false;
                    }

                    existingForward = forwardMatch;
                }

                InverseLinkTypes? existingInverse = null;

                if (InverseLinkTypes.TryFromId(id, out InverseLinkTypes inverseMatch))
                {
                    if (!string.Equals(inverseMatch.Name, inverse, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Inverse link type ID {id} is already registered as '{inverseMatch.Name}'.";
                        return false;
                    }

                    existingInverse = inverseMatch;
                }

                ForwardLinkTypes forward = existingForward ?? ForwardLinkTypes.Register(id, relationName);
                InverseLinkTypes inverseType = existingInverse ?? InverseLinkTypes.Register(id, inverse);
                RelationLinkType relation = new RelationLinkType(id, relationName, inverse, forward, inverseType);

                byName[relationName] = relation;
                byId[id] = relation;

                if (id >= nextId)
                    nextId = (long)id + 1;

                return true;
            }
        }

        // Registers a fixed structural relation from the reserved ontology range
        // (IDs 1-999). Unlike predicate registration, this deliberately permits
        // IDs at or below LastReservedOntologyId.
        internal static bool RegisterOntologyRelation(
            int id,
            string name,
            string inverseName,
            out string? error)
        {
            error = null;

            if (id <= 0 || id > LastReservedOntologyId)
            {
                error = $"Ontology relation ID {id} is outside the reserved range 1-{LastReservedOntologyId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(inverseName))
            {
                error = "Relation names cannot be empty.";
                return false;
            }

            string relationName = name.Trim();
            string inverse = inverseName.Trim();

            lock (sync)
            {
                if (byId.TryGetValue(id, out RelationLinkType? existing))
                {
                    if (string.Equals(existing.Name, relationName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.InverseName, inverse, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    error = $"Relation ID {id} is already registered as '{existing.Name}'.";
                    return false;
                }

                if (byName.TryGetValue(relationName, out RelationLinkType? existingByName))
                {
                    error = $"Relation '{relationName}' is already registered with ID {existingByName.Id}.";
                    return false;
                }

                ForwardLinkTypes forward = ForwardLinkTypes.Register(id, relationName);
                InverseLinkTypes inverseType = InverseLinkTypes.Register(id, inverse);
                RelationLinkType relation = new RelationLinkType(id, relationName, inverse, forward, inverseType);

                byName[relationName] = relation;
                byId[id] = relation;
                return true;
            }
        }

        // Returns true when the supplied ID names an already-registered ontology
        // relation, either in this registry or in the forward/inverse registries.
        internal static bool IsOntologyRelationRegistered(int id, string? name, string? inverseName)
        {
            if (id <= 0 || id > LastReservedOntologyId)
                return false;

            string relationName = name?.Trim() ?? string.Empty;
            string inverse = inverseName?.Trim() ?? string.Empty;

            lock (sync)
            {
                if (byId.TryGetValue(id, out RelationLinkType? existing))
                {
                    return string.Equals(existing.Name, relationName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(existing.InverseName, inverse, StringComparison.OrdinalIgnoreCase);
                }
            }

            return ForwardLinkTypes.TryFromId(id, out ForwardLinkTypes forward)
                && string.Equals(forward.Name, relationName, StringComparison.OrdinalIgnoreCase)
                && InverseLinkTypes.TryFromId(id, out InverseLinkTypes inverseType)
                && string.Equals(inverseType.Name, inverse, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string lemma, string? particle = null)
        {
            if (string.IsNullOrWhiteSpace(lemma))
                throw new ArgumentException("A relation name is required.", nameof(lemma));

            string canonical = CanonicalVerb(NormalizeToken(lemma));

            if (!string.IsNullOrWhiteSpace(particle))
            {
                string normalizedParticle = NormalizeToken(particle);

                if (normalizedParticle.Length > 0)
                    canonical = canonical + "_" + normalizedParticle;
            }

            return canonical.Length == 0 ? "unknown_relation" : canonical;
        }

        private static RelationLinkType GetOrRegisterLocked(string name)
        {
            if (byName.TryGetValue(name, out RelationLinkType? existing))
                return existing;

            int id = AllocateIdLocked();
            string inverseName = "inverse_" + name;
            ForwardLinkTypes forward = ForwardLinkTypes.Register(id, name);
            InverseLinkTypes inverse = InverseLinkTypes.Register(id, inverseName);
            RelationLinkType relation = new RelationLinkType(id, name, inverseName, forward, inverse);

            byName[name] = relation;
            byId[id] = relation;
            return relation;
        }

        private static int AllocateIdLocked()
        {
            while (nextId <= int.MaxValue)
            {
                int candidate = (int)nextId;
                nextId++;

                if (candidate <= LastReservedOntologyId)
                    continue;

                if (ForwardLinkTypes.TryFromId(candidate, out _)
                    || InverseLinkTypes.TryFromId(candidate, out _))
                {
                    continue;
                }

                return candidate;
            }

            throw new InvalidOperationException(
                "The relation registry exhausted its available link-type ID range.");
        }

        private static string NormalizeRelationName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A relation name is required.", nameof(value));

            string normalized = NormalizeToken(value);
            return normalized.Length == 0 ? "unknown_relation" : normalized;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string lower = value.Trim().ToLowerInvariant();
            System.Text.StringBuilder builder = new System.Text.StringBuilder(lower.Length);

            foreach (char character in lower)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    continue;
                }

                if (character is ' ' or '-' or '_')
                {
                    if (builder.Length > 0 && builder[^1] != '_')
                        builder.Append('_');
                }
            }

            return builder.ToString().Trim('_');
        }

        private static string CanonicalVerb(string value)
        {
            return value switch
            {
                "be" or "am" or "is" or "are" or "was" or "were" or "been" or "being" => "is",
                "have" or "has" or "had" or "having" => "has",
                "do" or "does" or "did" or "done" or "doing" => "does",
                _ => value
            };
        }
    }
}
