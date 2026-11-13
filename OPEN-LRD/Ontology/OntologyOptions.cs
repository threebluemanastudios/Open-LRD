namespace OPEN_LRD.Ontology
{
    // Module 4 feature flags. The foundation is opt-in at startup so a new
    // graph stays empty until the user seeds it or imports an ontology log.
    internal static class OntologyOptions
    {
        public static bool AutoSeedFoundation { get; set; }

        // Defaults used by the Tools dialog and by OntologyLoader convenience
        // overloads. A semantic-only subset keeps the first load fast.
        public const int DefaultMaxEntries = 2000;

        public static string DefaultCorpusDirectory
        {
            get
            {
                string repository = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
                return System.IO.Path.Combine(repository, "gcide-0.54", "gcide-0.54");
            }
        }
    }
}
