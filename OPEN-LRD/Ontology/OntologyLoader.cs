using OPEN_LRD.Ontology.Gcide;
using OPEN_LRD.Persistence;

namespace OPEN_LRD.Ontology
{
    // Facade over the Module 4 halves: the permanent upper ontology and the
    // GCIDE ETL. Callers keep the core, so the loader never owns application
    // lifecycle.
    internal static class OntologyLoader
    {
        public static int BuildFoundation(LRD_Core core)
        {
            return UpperOntologyBootstrap.Populate(core);
        }

        public static GcideLoadResult LoadCorpus(LRD_Core core, GcideLoadOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            if (!core.initialized)
                throw new InvalidOperationException(
                    "The core must be initialized before loading the GCIDE corpus.");

            OntologyRelations.EnsureRegistered();

            if (OntologyOptions.AutoSeedFoundation)
                UpperOntologyBootstrap.Populate(core);

            return GcideCorpusLoader.Load(core, options);
        }

        // Loads the corpus and immediately writes a core .lrdlog so later runs
        // can restore through the existing Tools import path.
        public static (GcideLoadResult Load, CoreLogExportResult Export) LoadAndExport(
            LRD_Core core,
            GcideLoadOptions options,
            string logPath,
            bool includeTransient = false)
        {
            GcideLoadResult load = LoadCorpus(core, options);
            CoreLogExportResult export = CoreLogExporter.Export(
                core,
                logPath,
                includeTransient);

            return (load, export);
        }

        public static PersistenceImportResult LoadFromLog(
            LRD_Core core,
            string logPath,
            ImportMode mode = ImportMode.Merge)
        {
            return CoreLogImporter.ImportCore(core, logPath, mode);
        }
    }
}
