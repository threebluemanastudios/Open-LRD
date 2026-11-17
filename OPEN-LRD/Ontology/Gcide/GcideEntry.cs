namespace OPEN_LRD.Ontology.Gcide
{
    /// <summary>Stores one parsed dictionary sense and its lexical metadata.</summary>
    internal sealed record GcideSense(
        string Number,
        string Definition,
        string? Field,
        string? Source);

    /// <summary>Stores a parsed headword and its senses from a GCIDE corpus entry.</summary>
    internal sealed record GcideEntry(
        string Headword,
        string Index,
        IReadOnlyList<string> PartsOfSpeech,
        IReadOnlyList<GcideSense> Senses,
        IReadOnlyList<string> Synonyms,
        IReadOnlyList<string> AlternativeNames,
        IReadOnlyList<string> AlternativeSpellings,
        IReadOnlyList<string> Antonyms,
        IReadOnlyList<string> Subtypes,
        IReadOnlyList<string> Supertypes,
        IReadOnlyList<string> Fields,
        IReadOnlyList<string> Taxa)
    {
        public bool HasSemanticRelations =>
            Synonyms.Count > 0
            || AlternativeNames.Count > 0
            || Antonyms.Count > 0
            || Subtypes.Count > 0
            || Supertypes.Count > 0;
    }
}
