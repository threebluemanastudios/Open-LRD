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
