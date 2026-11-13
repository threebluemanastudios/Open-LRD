using System.IO;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.NLP;
using OPEN_LRD.Ontology;
using OPEN_LRD.Ontology.Gcide;
using OPEN_LRD.Persistence;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class OntologyTests
    {
        private const string DogBlock = """
            <p><ent>dog</ent><br/>
            <hw>dog</hw> <pos>n.</pos> <sn>1.</sn> <def>A domesticated carnivorous mammal.</def><br/>
            <sn>2.</sn> <def>A contemptible person.</def><br/>
            <syn>Syn. -- canine, hound</syn>
            <ant>cat</ant>
            <stype>poodle</stype>
            <isa>canine</isa>
            <fld>zool.</fld></p>
            """;

        private const string CanineBlock = """
            <p><ent>canine</ent><br/>
            <hw>canine</hw> <pos>a.</pos> <def>Of or pertaining to dogs.</def></p>
            """;

        private const string CatBlock = """
            <p><ent>cat</ent><br/>
            <hw>cat</hw> <pos>n.</pos> <def>A small carnivorous mammal.</def><br/>
            <ant>dog</ant></p>
            """;

        private const string PoodleBlock = """
            <p><ent>poodle</ent><br/>
            <hw>poodle</hw> <pos>n.</pos> <def>A curly-haired dog.</def></p>
            """;

        private const string PlainBlock = """
            <p><ent>zebra</ent><br/>
            <hw>zebra</hw> <pos>n.</pos> <def>An African striped mammal.</def></p>
            """;

        public static void RunAll()
        {
            Program.Run("Ontology foundation bootstrap", TestFoundationBootstrap);
            Program.Run("Ontology relation registry", TestRelationRegistry);
            Program.Run("Ontology link construction", TestLinkConstruction);
            Program.Run("GCIDE markup decoding", TestMarkupDecoding);
            Program.Run("GCIDE entry parsing", TestEntryParsing);
            Program.Run("GCIDE corpus mapping", TestCorpusMapping);
            Program.Run("GCIDE idempotent reload", TestIdempotentReload);
            Program.Run("GCIDE subset policies", TestSubsetPolicies);
            Program.Run("GCIDE bundled corpus sample", TestBundledCorpusSample);
            Program.Run("Ontology persistence round trip", TestPersistenceRoundTrip);
            Program.Run("Ontology validation and reality", TestValidationAndReality);
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static string NewTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-gcide-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string WriteCorpus(params (string File, string Content)[] files)
        {
            string directory = NewTempDirectory();

            foreach ((string file, string content) in files)
                File.WriteAllText(Path.Combine(directory, file), content);

            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        private static Web_Node Node(LRD_Core core, string label, NodeTypes type)
        {
            IReadOnlyList<Web_Node> nodes = core.FindByLabel(label, type);
            Assert(nodes.Count > 0, $"expected a {type.Name} node labelled '{label}'");
            return nodes[0];
        }

        private static Web_Node? Find(LRD_Core core, string label, NodeTypes type)
        {
            IReadOnlyList<Web_Node> nodes = core.FindByLabel(label, type);
            return nodes.Count > 0 ? nodes[0] : null;
        }

        private static int TotalLinks(LRD_Core core)
        {
            int total = 0;

            foreach (Web_Node node in core.FindNodes())
                total += node.Get_ReferenceLinks().Count;

            return total;
        }

        private static bool HasLink(Guid sourceID, Guid destinationID, int relationId)
        {
            Web_Node? source = LRD_Core.Instance.GetExistingNode(sourceID);

            if (source is null)
                return false;

            foreach (Link link in source.Get_ReferenceLinks())
            {
                if (link.DestinationID == destinationID && link.ForwardLink.Id == relationId)
                    return true;
            }

            return false;
        }

        private static void TestFoundationBootstrap()
        {
            LRD_Core core = NewCore();

            int created = UpperOntologyBootstrap.Populate(core);
            Assert(created > 0, "the first bootstrap should create nodes");

            Web_Node entity = Node(core, "Entity", OntologyTypes.Categories);
            Web_Node location = Node(core, "Location", OntologyTypes.Categories);
            Web_Node physical = Node(core, "PhysicalEntity", OntologyTypes.Categories);
            Node(core, "Noun", OntologyTypes.Categories);
            Node(core, "Real", OntologyTypes.Categories);

            Assert(entity.IsPermanent, "foundation nodes should be permanent");
            Assert(
                HasLink(location.Get_ID(), physical.Get_ID(), OntologyRelations.IsA),
                "Location should be a subtype of PhysicalEntity");
            Assert(
                HasLink(physical.Get_ID(), entity.Get_ID(), OntologyRelations.IsA),
                "PhysicalEntity should be a subtype of Entity");
            Assert(
                HasLink(location.Get_ID(), Node(core, "Real", OntologyTypes.Categories).Get_ID(), OntologyRelations.HasReality),
                "foundation nodes should be marked Real");

            int nodeCount = core.FindNodes().Count;
            int second = UpperOntologyBootstrap.Populate(core);

            Assert(second == 0, "a repeated bootstrap should create no nodes");
            Assert(core.FindNodes().Count == nodeCount, "a repeated bootstrap should not add nodes");
            Assert(core.FindByLabel("Location", OntologyTypes.Categories).Count == 1, "categories should not duplicate");
        }

        private static void TestRelationRegistry()
        {
            OntologyRelations.EnsureRegistered();

            foreach (OntologyRelation relation in OntologyRelations.All)
            {
                Assert(
                    relation.Id >= 1 && relation.Id <= RelationRegistry.LastReservedOntologyId,
                    $"relation '{relation.Name}' should sit in the reserved range");
                Assert(
                    RelationRegistry.TryGetById(relation.Id, out RelationLinkType registered),
                    $"relation '{relation.Name}' should resolve by ID");
                Assert(registered.Name == relation.Name, $"relation {relation.Id} should keep its name");
                Assert(registered.InverseName == relation.InverseName, $"relation {relation.Id} should keep its inverse");
                Assert(
                    registered.Forward.Id == relation.Id && registered.Inverse.Id == relation.Id,
                    "forward and inverse link types should share the relation ID");
            }

            Assert(
                OntologyRelations.Antonym == RelationRegistry.AntonymLinkTypeId,
                "antonym should stay at the Module 3 reservation");
            Assert(
                RelationRegistry.TryGetById(RelationRegistry.AntonymLinkTypeId, out RelationLinkType antonym)
                    && antonym.Name == RelationRegistry.AntonymRelationName,
                "antonym should remain registered");

            Assert(
                !RelationRegistry.TryRestore(500, "not_an_ontology_relation", "inverse_x", out _),
                "unregistered reserved IDs should still be rejected");
        }

        private static void TestLinkConstruction()
        {
            LRD_Core core = NewCore();
            OntologyRelations.EnsureRegistered();

            Web_Node left = new Web_Node(Guid.NewGuid(), OntologyTypes.Categories, NodeLifetime.Permanent, 0);
            left.Set_Label("Left");
            Web_Node right = new Web_Node(Guid.NewGuid(), OntologyTypes.Categories, NodeLifetime.Permanent, 0);
            right.Set_Label("Right");
            Assert(core.AddExistingNode(left), "left node should be added");
            Assert(core.AddExistingNode(right), "right node should be added");

            Assert(
                core.AddDirectedConnection(left.Get_ID(), right.Get_ID(), OntologyTypes.ontology, OntologyRelations.IsA),
                "an ontology link should be constructible after registration");

            Link link = left.Get_ReferenceLinks().Single();
            Assert(link.ForwardLink.Id == OntologyRelations.IsA, "forward link should use the ontology ID");
            Assert(link.ForwardLink.Name == "is_a", "forward link should keep its name");
            Assert(link.InverseLink.Name == "has_subtype", "inverse link should keep its name");
        }

        private static void TestMarkupDecoding()
        {
            Assert(GcideMarkup.DecodeEscapes("\\'94") == "\u00F6", "\\'94 should decode to o-umlaut");
            Assert(GcideMarkup.DecodeEscapes("<br/") == " ", "<br/ should decode to a space");
            Assert(GcideMarkup.DecodeEscapes("<or/") == "or", "<or/ should decode to the word 'or'");
            Assert(
                GcideMarkup.CleanText("cats <or/ dogs") == "cats or dogs",
                "entity decoding should not swallow neighbouring text");
            Assert(
                GcideMarkup.CleanText("  spaced \n\t text  ") == "spaced text",
                "clean text should normalise whitespace");
        }

        private static void TestEntryParsing()
        {
            GcideEntry? entry = GcideEntryReader.ParseEntry(DogBlock);

            Assert(entry is not null, "the dog block should parse");
            Assert(entry!.Headword == "dog", "the headword should be captured");
            Assert(entry.Index == "dog", "the ASCII index form should be captured");
            Assert(
                entry.PartsOfSpeech.Count == 1 && entry.PartsOfSpeech[0] == "Noun",
                "the part of speech should be canonicalised");
            Assert(entry.Senses.Count == 2, "both senses should be captured");
            Assert(entry.Senses[0].Number == "1.", "the first sense number should be captured");
            Assert(
                entry.Senses[0].Definition == "A domesticated carnivorous mammal.",
                "the first definition should be cleaned");
            Assert(entry.Senses[1].Field is null, "sense-level fields should stay optional");
            Assert(
                entry.Synonyms.Count == 2
                    && entry.Synonyms.Contains("canine")
                    && entry.Synonyms.Contains("hound"),
                "the Syn. -- prefix should be stripped and split");
            Assert(entry.Antonyms.Count == 1 && entry.Antonyms[0] == "cat", "antonyms should be captured");
            Assert(entry.Subtypes.Count == 1 && entry.Subtypes[0] == "poodle", "subtypes should be captured");
            Assert(entry.Supertypes.Count == 1 && entry.Supertypes[0] == "canine", "supertypes should be captured");
            Assert(entry.Fields.Count == 1 && entry.Fields[0] == "zool.", "fields should be captured");

            IEnumerable<GcideEntry> unbalanced = GcideEntryReader.ParseDocument(
                "<p><ent>fox</ent><hw>fox</hw><def>animal");
            Assert(unbalanced.Count() == 1, "an unterminated paragraph should still yield one entry");
            Assert(unbalanced.First().Headword == "fox", "the unterminated entry should keep its headword");
        }

        private static void TestCorpusMapping()
        {
            LRD_Core core = NewCore();
            string directory = WriteCorpus(
                ("CIDE.A", CanineBlock + CatBlock + PoodleBlock + DogBlock));

            try
            {
                GcideLoadResult result = OntologyLoader.LoadCorpus(
                    core,
                    new GcideLoadOptions
                    {
                        CorpusDirectory = directory,
                        Subset = GcideSubset.All,
                        MaxEntries = 0
                    });

                Assert(result.EntriesLoaded == 4, "all four entries should load");
                Assert(result.WordNodes == 4, "four lexemes should be created");
                Assert(result.SenseNodes == 5, "five senses should be created");
                Assert(result.LinksCreated > 0, "the load should create links");

                Web_Node dog = Node(core, "dog", OntologyTypes.Lexemes);
                Web_Node canine = Node(core, "canine", OntologyTypes.Lexemes);
                Web_Node cat = Node(core, "cat", OntologyTypes.Lexemes);
                Web_Node poodle = Node(core, "poodle", OntologyTypes.Lexemes);

                Assert(dog.IsPermanent, "lexemes should be permanent");
                Assert(
                    HasLink(dog.Get_ID(), canine.Get_ID(), OntologyRelations.Synonym)
                        && HasLink(canine.Get_ID(), dog.Get_ID(), OntologyRelations.Synonym),
                    "synonyms should be symmetric");
                Assert(
                    HasLink(dog.Get_ID(), cat.Get_ID(), OntologyRelations.Antonym)
                        && HasLink(cat.Get_ID(), dog.Get_ID(), OntologyRelations.Antonym),
                    "antonyms should be symmetric at the reserved ID");
                Assert(
                    HasLink(dog.Get_ID(), canine.Get_ID(), OntologyRelations.IsA),
                    "<isa> should make the headword a subtype of the target");
                Assert(
                    HasLink(poodle.Get_ID(), dog.Get_ID(), OntologyRelations.IsA),
                    "<stype> should make the target a subtype of the headword");
                Assert(
                    HasLink(dog.Get_ID(), Node(core, "Noun", OntologyTypes.Categories).Get_ID(), OntologyRelations.HasPos),
                    "the lexeme should link to its part of speech");

                Link senseLink = dog.Get_ReferenceLinks()
                    .First(link => link.ForwardLink.Id == OntologyRelations.HasSense);
                Web_Node sense = core.GetExistingNode(senseLink.DestinationID)!;

                Assert(sense.Get_NodeType() == OntologyTypes.Senses, "has_sense should target a sense");
                Assert(sense.Label == "A domesticated carnivorous mammal.", "the sense should show its definition");
                Assert(
                    HasLink(sense.Get_ID(), dog.Get_ID(), OntologyRelations.IsNamed),
                    "the sense should name its lexeme");
                Assert(
                    HasLink(sense.Get_ID(), Node(core, "zool.", OntologyTypes.Fields).Get_ID(), OntologyRelations.HasField),
                    "the sense should link to its field");
                Assert(
                    result.SkippedRelations == 2,
                    "the unresolved 'hound' synonym and the early 'dog' antonym should be skipped");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestIdempotentReload()
        {
            LRD_Core core = NewCore();
            string directory = WriteCorpus(("CIDE.A", CanineBlock + CatBlock + PoodleBlock + DogBlock));

            try
            {
                GcideLoadOptions options = new GcideLoadOptions
                {
                    CorpusDirectory = directory,
                    Subset = GcideSubset.All,
                    MaxEntries = 0
                };

                GcideLoadResult first = OntologyLoader.LoadCorpus(core, options);
                int nodeCount = core.FindNodes().Count;
                int linkCount = TotalLinks(core);

                GcideLoadResult second = OntologyLoader.LoadCorpus(core, options);

                Assert(second.EntriesLoaded == first.EntriesLoaded, "the reload should read the same entries");
                Assert(second.WordNodes == 0, "the reload should create no lexemes");
                Assert(second.SenseNodes == 0, "the reload should create no senses");
                Assert(second.LinksCreated == 0, "the reload should create no links");
                Assert(core.FindNodes().Count == nodeCount, "the reload should not grow the graph");
                Assert(TotalLinks(core) == linkCount, "links should stay stable");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestSubsetPolicies()
        {
            LRD_Core core = NewCore();
            string directory = WriteCorpus(
                ("CIDE.A", DogBlock),
                ("CIDE.B", PlainBlock));

            try
            {
                GcideLoadResult semantic = OntologyLoader.LoadCorpus(
                    core,
                    new GcideLoadOptions
                    {
                        CorpusDirectory = directory,
                        Subset = GcideSubset.SemanticOnly,
                        MaxEntries = 0
                    });

                Assert(semantic.EntriesRead == 2, "the reader should see both entries");
                Assert(semantic.EntriesLoaded == 1, "SemanticOnly should skip the plain entry");
                Assert(Find(core, "zebra", OntologyTypes.Lexemes) is null, "the plain entry should not load");
                Assert(Find(core, "dog", OntologyTypes.Lexemes) is not null, "the tagged entry should load");

                LRD_Core cappedCore = NewCore();
                GcideLoadResult capped = OntologyLoader.LoadCorpus(
                    cappedCore,
                    new GcideLoadOptions
                    {
                        CorpusDirectory = directory,
                        Subset = GcideSubset.All,
                        MaxEntries = 1
                    });

                Assert(capped.EntriesLoaded == 1, "MaxEntries should cap the load");
                Assert(cappedCore.FindNodes().Count > 0, "the capped load should still create nodes");

                LRD_Core letterCore = NewCore();
                GcideLoadResult byLetter = OntologyLoader.LoadCorpus(
                    letterCore,
                    new GcideLoadOptions
                    {
                        CorpusDirectory = directory,
                        Subset = GcideSubset.ByLetter,
                        Letter = 'B',
                        MaxEntries = 0
                    });

                Assert(byLetter.EntriesRead == 1, "ByLetter should read only the matching file");
                Assert(Find(letterCore, "zebra", OntologyTypes.Lexemes) is not null, "the B file entry should load");
                Assert(Find(letterCore, "dog", OntologyTypes.Lexemes) is null, "the A file entry should not load");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestBundledCorpusSample()
        {
            string directory = OntologyOptions.DefaultCorpusDirectory;

            if (!Directory.Exists(directory))
                return;

            LRD_Core core = NewCore();

            GcideLoadResult result = OntologyLoader.LoadCorpus(
                core,
                new GcideLoadOptions
                {
                    CorpusDirectory = directory,
                    Subset = GcideSubset.SemanticOnly,
                    MaxEntries = 25
                });

            Assert(result.EntriesLoaded > 0, "the bundled corpus should yield tagged entries");
            Assert(result.WordNodes > 0, "the bundled corpus should create lexemes");
            Assert(
                core.FindByType(OntologyTypes.Lexemes).Count == result.WordNodes,
                "lexeme count should match the report");
        }

        private static void TestPersistenceRoundTrip()        {
            LRD_Core core = NewCore();
            UpperOntologyBootstrap.Populate(core);

            string directory = NewTempDirectory();
            string path = Path.Combine(directory, "ontology.lrdlog");

            try
            {
                CoreLogExportResult export = CoreLogExporter.Export(core, path, includeTransient: false);

                Assert(File.Exists(path), "the ontology log should be written");
                Assert(export.NodeCount > 0, "the ontology log should contain nodes");
                Assert(export.LinkCount > 0, "the ontology log should contain links");

                PersistenceImportResult import = OntologyLoader.LoadFromLog(core, path);

                Assert(import.Success, "the ontology log should import: " + string.Join("; ", import.Errors));
                Assert(
                    RelationRegistry.TryGetById(OntologyRelations.IsA, out RelationLinkType isA)
                        && isA.Name == "is_a",
                    "ontology relations should survive the round trip");
                Assert(
                    NodeTypes.FromName("Lexeme").Id == OntologyTypes.Lexemes.Id,
                    "ontology node types should survive the round trip");
                Assert(
                    LinkClass.FromName("lexicon").Id == OntologyTypes.lexicon.Id,
                    "ontology link classes should survive the round trip");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestValidationAndReality()
        {
            Assert(
                OntologyValidator.IsRelationAllowed("has_time", "Event", "Time"),
                "a valid domain/range pair should pass");
            Assert(
                !OntologyValidator.IsRelationAllowed("has_time", "Event", "Location"),
                "an invalid range should be flagged");
            Assert(
                !OntologyValidator.IsRelationAllowed("is_named", "Location", "Lexeme"),
                "an invalid domain should be flagged");
            Assert(
                OntologyValidator.IsRelationAllowed("unknown_relation", "Anything", "Anything"),
                "unknown relations are advisory and pass");

            LRD_Core core = NewCore();
            OntologyRelations.EnsureRegistered();

            Web_Node fictional = new Web_Node(Guid.NewGuid(), OntologyTypes.Categories, NodeLifetime.Permanent, 0);
            fictional.Set_Label("Fictional");
            Web_Node unicorn = new Web_Node(Guid.NewGuid(), OntologyTypes.Categories, NodeLifetime.Permanent, 0);
            unicorn.Set_Label("Unicorn");
            Assert(core.AddExistingNode(fictional), "the reality node should be added");
            Assert(core.AddExistingNode(unicorn), "the concept node should be added");
            Assert(
                core.AddDirectedConnection(unicorn.Get_ID(), fictional.Get_ID(), OntologyTypes.ontology, OntologyRelations.IsA),
                "the fictional subtree link should be added");

            Assert(
                OntologyValidator.GetRealityStatus(core, unicorn.Get_ID()) == "Fictional",
                "a fictional subtree should resolve to Fictional");
            Assert(
                OntologyValidator.GetRealityStatus(core, fictional.Get_ID()) == "Fictional",
                "the reality node itself should resolve to Fictional");
        }
    }
}
