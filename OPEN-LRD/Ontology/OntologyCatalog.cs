namespace OPEN_LRD.Ontology
{
    internal sealed record OntologyConcept(string Label, string? ParentLabel);

    // The hardcoded upper-ontology concept set. Every concept becomes a
    // permanent Category node; each non-root concept is joined to its parent
    // with an is_a link.
    internal static class OntologyCatalog
    {
        public const string RootLabel = "Entity";
        public const string RealLabel = "Real";

        public static readonly IReadOnlyList<OntologyConcept> Concepts = new OntologyConcept[]
        {
            new OntologyConcept("Entity", null),
            new OntologyConcept("PhysicalEntity", "Entity"),
            new OntologyConcept("Object", "PhysicalEntity"),
            new OntologyConcept("Artifact", "Object"),
            new OntologyConcept("Substance", "PhysicalEntity"),
            new OntologyConcept("LivingThing", "PhysicalEntity"),
            new OntologyConcept("Person", "LivingThing"),
            new OntologyConcept("Location", "PhysicalEntity"),
            new OntologyConcept("AbstractEntity", "Entity"),
            new OntologyConcept("Organization", "AbstractEntity"),
            new OntologyConcept("Concept", "AbstractEntity"),
            new OntologyConcept("Quantity", "AbstractEntity"),
            new OntologyConcept("Property", "AbstractEntity"),
            new OntologyConcept("Relation", "AbstractEntity"),
            new OntologyConcept("Time", "AbstractEntity"),
            new OntologyConcept("Event", "AbstractEntity"),
            new OntologyConcept("Process", "Event"),
            new OntologyConcept("Activity", "Process"),
            new OntologyConcept("State", "Event"),
            new OntologyConcept("Lexeme", "AbstractEntity")
        };

        public static readonly IReadOnlyList<string> PartOfSpeechLabels = new[]
        {
            "Noun",
            "Verb",
            "Adjective",
            "Adverb",
            "Pronoun",
            "Preposition",
            "Conjunction",
            "Interjection"
        };

        public static readonly IReadOnlyList<string> RealityLabels = new[]
        {
            "Real",
            "Fictional",
            "Hypothetical",
            "Conditional"
        };

        // Maps a GCIDE <pos> abbreviation to a canonical catalog label.
        public static string? PartOfSpeechFromAbbreviation(string abbreviation)
        {
            string value = abbreviation.ToLowerInvariant().Replace(".", " ").Trim();

            while (value.Contains("  ", StringComparison.Ordinal))
                value = value.Replace("  ", " ", StringComparison.Ordinal);

            return value switch
            {
                "n" or "n pl" or "plural" => "Noun",
                "v" or "v t" or "v i" or "vt" or "vi" => "Verb",
                "a" or "adj" => "Adjective",
                "adv" => "Adverb",
                "pron" => "Pronoun",
                "prep" => "Preposition",
                "conj" => "Conjunction",
                "interj" => "Interjection",
                _ => null
            };
        }
    }
}
