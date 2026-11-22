using System.Threading;
using System.Threading.Tasks;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.SmokeTests
{
    internal static class CoreIdentityTests
    {
        internal static void RunAll()
        {
            Program.Run("Core initialization lifecycle", InitializationLifecycleIsExplicit());
            Program.Run("Concurrent initialization is idempotent", ConcurrentInitializationIsIdempotent());
            Program.Run("Core identity assignment and freezing", IdentityFieldsAreAssignedOnceAndFrozen());
            Program.Run("Trusted word identity uniqueness", TrustedWordsUseTheCompleteIdentityTuple());
            Program.Run("Duplicate word promotion rejection", DuplicatePromotionLeavesWordUntrusted());
            Program.Run("Incomplete node promotion rejection", IncompleteNodesCannotBecomeTrusted());
            Program.Run("Sentence and symbol semantic indexes", FactoriesUseSenseAwareIdentities());
            Program.Run("Untrusted box construction avoids trusted ID collisions", UntrustedBoxConstructionAvoidsTrustedIdCollisions());
            Program.Run("Untrusted function ID collisions do not replace staged definitions", UntrustedFunctionIdCollisionsAreRejected());
            Program.Run("Concurrent graph mutations", ConcurrentAddsPreserveIdentityIndexes());
            Program.Run("Concurrent duplicate promotions are atomic", ConcurrentDuplicatePromotionsAreAtomic());
        }

        private static Action InitializationLifecycleIsExplicit()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                Program.Assert(!core.initialized, "ResetCore should leave the core uninitialized.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSense(new SenseBox(Guid.NewGuid())),
                    "Registry mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSymbol(new SymbolBox(Guid.NewGuid(), 'x')),
                    "Symbol mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedWord(new WordBox(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())),
                    "Word mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSentence(new SentenceBox(Guid.NewGuid())),
                    "Sentence mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedUsage(new UsageBox(Guid.NewGuid())),
                    "Usage mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedGroup(new GroupBox(Guid.NewGuid(), new List<Guid>())),
                    "Group mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSource(new SourceBox(Guid.NewGuid(), new List<Guid>())),
                    "Source mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedFunction(new FunctionBox(Guid.NewGuid(), typeof(double))),
                    "Function mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSymbol(new SymbolBox()),
                    "Untrusted Symbol mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedWord(new WordBox(new List<Guid>())),
                    "Untrusted Word mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSentence(new SentenceBox()),
                    "Untrusted Sentence mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedFunction(new FunctionBox(typeof(double))),
                    "Untrusted Function mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedNode(new Node(NodeTargetKind.Symbol, Guid.NewGuid())),
                    "Untrusted Node mutations should require initialization.");
                Program.AssertThrows<InvalidOperationException>(() => core.GetTrustedSenseBox(Guid.NewGuid()),
                    "Registry queries should require initialization.");
                core.InitializeCore();
                Program.Assert(core.initialized, "InitializeCore should establish the ready state.");
                Guid senseId = Guid.NewGuid();
                Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Initialized core should accept registry mutations.");
                core.InitializeCore();
                Program.Assert(core.GetTrustedSenseBox(senseId) != null,
                    "InitializeCore should be idempotent after initialization.");
                core.ResetCore();
                Program.Assert(!core.initialized, "ResetCore should clear initialized status.");
                Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedUsage(new UsageBox(Guid.NewGuid())),
                    "Registry mutations should remain disabled after ResetCore.");
            };
        }

        private static Action ConcurrentInitializationIsIdempotent()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                Parallel.For(0, 32, _ => core.InitializeCore());
                Program.Assert(core.initialized, "Concurrent initialization should leave the core initialized.");
                Program.Assert(core.AddTrustedSense(new SenseBox(Guid.NewGuid())),
                    "The initialized core should accept a mutation after concurrent initialization.");
                core.ResetCore();
            };
        }

        private static Action IdentityFieldsAreAssignedOnceAndFrozen()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);

                SymbolBox symbol = new SymbolBox();
                Program.Assert(core.SetUntrustedSymbol(symbol), "Default symbol should be accepted as untrusted.");
                Program.Assert(core.SetUntrustedSymbolChar(symbol.GetSymbolID(), 'x'), "Symbol character should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSymbolChar(symbol.GetSymbolID(), 'y'), "Symbol character must not be reassigned.");
                Program.Assert(core.SetUntrustedSymbolSenseID(symbol.GetSymbolID(), senseId), "Symbol sense should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSymbolSenseID(symbol.GetSymbolID(), senseId), "Symbol sense must not be reassigned.");
                Program.Assert(core.ConvertUntrustedSymbolToTrusted(symbol.GetSymbolID()), "Complete symbol should promote.");
                Program.AssertThrows<InvalidOperationException>(() => symbol.SetSymbolChar('y'), "Trusted symbol character must be frozen.");
                Program.AssertThrows<InvalidOperationException>(() => symbol.SetSenseID(senseId), "Trusted symbol sense must be frozen.");

                WordBox word = new WordBox(senseId, usageId);
                Program.Assert(core.SetUntrustedWord(word), "Partial word should be accepted as untrusted.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedWordSenseID(word.GetWordID(), senseId), "Word sense assigned by its constructor must not be reassigned.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedWordUsageID(word.GetWordID(), usageId), "Word usage assigned by its constructor must not be reassigned.");
                Program.Assert(core.SetUntrustedWordSymbols(word.GetWordID(), new List<Guid> { symbol.GetSymbolID() }), "Word symbols should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedWordSymbols(word.GetWordID(), new List<Guid>()), "Word symbols must not be reassigned.");
                Program.Assert(core.SetUntrustedWordPartOfSpeech(word.GetWordID(), PartOfSpeech.Noun), "Part of speech should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedWordPartOfSpeech(word.GetWordID(), PartOfSpeech.Verb), "Part of speech must not be reassigned.");
                Program.Assert(core.ConvertUntrustedWordToTrusted(word.GetWordID()), "Complete word should promote.");
                Program.Assert(word.IsIdentityFrozen, "Promoted word should be frozen.");
                Program.AssertThrows<InvalidOperationException>(() => word.SetSymbols(new List<Guid>()), "Trusted word sequence must be frozen.");
                Program.AssertThrows<InvalidOperationException>(() => word.SetPartOfSpeech(PartOfSpeech.Verb), "Trusted word part of speech must be frozen.");

                SentenceBox sentence = new SentenceBox();
                Program.Assert(core.SetUntrustedSentence(sentence), "Partial sentence should be accepted as untrusted.");
                Program.Assert(core.SetUntrustedSentenceWords(sentence.GetSentenceID(), new List<Guid> { word.GetWordID() }), "Sentence words should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSentenceWords(sentence.GetSentenceID(), new List<Guid>()), "Sentence words must not be reassigned.");
                Program.Assert(core.SetUntrustedSentenceSenseID(sentence.GetSentenceID(), senseId), "Sentence sense should be assignable once.");
                Program.AssertThrows<InvalidOperationException>(() => core.SetUntrustedSentenceSenseID(sentence.GetSentenceID(), senseId), "Sentence sense must not be reassigned.");
                Program.Assert(core.ConvertUntrustedSentenceToTrusted(sentence.GetSentenceID()), "Complete sentence should promote.");
                Program.AssertThrows<InvalidOperationException>(() => sentence.SetWords(new List<Guid>()), "Trusted sentence sequence must be frozen.");
            };
        }

        private static Action TrustedWordsUseTheCompleteIdentityTuple()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid otherSenseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid otherUsageId = AddUsage(core);
                Guid firstSymbolId = AddTrustedSymbol(core, senseId, 'a');
                Guid secondSymbolId = AddTrustedSymbol(core, senseId, 'b');

                List<Guid> symbols = new List<Guid> { firstSymbolId, secondSymbolId };
                WordBox original = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(core.AddTrustedWord(original), "Initial word identity should be accepted.");

                WordBox duplicate = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(!core.AddTrustedWord(duplicate), "An exact identity duplicate should be rejected.");
                Program.Assert(!duplicate.IsIdentityFrozen, "Rejected duplicate should remain mutable and untrusted.");

                Program.Assert(
                    object.ReferenceEquals(core.GetNewWordBox(symbols, senseId, usageId, PartOfSpeech.Noun), original),
                    "Factory should return the existing canonical word for an exact identity match.");

                Program.Assert(core.AddTrustedWord(new WordBox(Guid.NewGuid(), otherSenseId, usageId, symbols, PartOfSpeech.Noun)),
                    "A different sense should produce a distinct trusted word.");
                Program.Assert(core.AddTrustedWord(new WordBox(Guid.NewGuid(), senseId, otherUsageId, symbols, PartOfSpeech.Noun)),
                    "A different usage should produce a distinct trusted word.");
                Program.Assert(core.AddTrustedWord(new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Verb)),
                    "A different part of speech should produce a distinct trusted word.");
                Program.Assert(core.AddTrustedWord(new WordBox(Guid.NewGuid(), senseId, usageId,
                    new List<Guid> { secondSymbolId, firstSymbolId }, PartOfSpeech.Noun)),
                    "A different symbol order should produce a distinct trusted word.");

                List<Guid> copiedSymbols = original.GetSymbols()!;
                copiedSymbols.Clear();
                Program.Assert(original.GetSymbols()!.Count == 2, "Mutating a returned sequence must not change word identity.");
            };
        }

        private static Action DuplicatePromotionLeavesWordUntrusted()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'c');
                List<Guid> symbols = new List<Guid> { symbolId };
                WordBox trustedWord = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(core.AddTrustedWord(trustedWord), "Initial trusted word should be accepted.");

                WordBox duplicate = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(core.SetUntrustedWord(duplicate), "Duplicate candidate should be accepted as untrusted.");
                Program.Assert(!core.ConvertUntrustedWordToTrusted(duplicate.GetWordID()), "Exact identity duplicate promotion should fail.");
                Program.Assert(core.GetUntrustedWordBox(duplicate.GetWordID()) == duplicate, "Rejected promotion should retain the candidate as untrusted.");
                Program.Assert(core.GetTrustedWordBox(duplicate.GetWordID()) == null, "Rejected promotion must not create a trusted entry.");

                WordBox distinct = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Adjective);
                Program.Assert(core.SetUntrustedWord(distinct), "Distinct candidate should be accepted as untrusted.");
                Program.Assert(core.ConvertUntrustedWordToTrusted(distinct.GetWordID()), "Distinct identity should promote.");
                Program.Assert(core.GetUntrustedWordBox(distinct.GetWordID()) == null, "Successful promotion should remove the untrusted entry.");
            };
        }

        private static Action IncompleteNodesCannotBecomeTrusted()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'd');

                WordBox incompleteWord = new WordBox(new List<Guid> { symbolId });
                Program.Assert(core.SetUntrustedWord(incompleteWord), "Incomplete word should be storable as untrusted.");
                Program.Assert(!core.AddTrustedWord(incompleteWord), "Direct trusted insertion should reject incomplete identity.");
                Program.Assert(!core.ConvertUntrustedWordToTrusted(incompleteWord.GetWordID()), "Promotion should reject incomplete identity.");
                Program.Assert(core.GetUntrustedWordBox(incompleteWord.GetWordID()) == incompleteWord, "Failed promotion should leave the word untrusted.");

                WordBox danglingWord = new WordBox(Guid.NewGuid(), senseId, usageId,
                    new List<Guid> { Guid.NewGuid() }, PartOfSpeech.Noun);
                Program.Assert(core.SetUntrustedWord(danglingWord), "Word with an unresolved symbol should be storable as untrusted.");
                Program.Assert(!core.ConvertUntrustedWordToTrusted(danglingWord.GetWordID()), "Promotion should reject unresolved symbol references.");
                Program.Assert(core.GetUntrustedWordBox(danglingWord.GetWordID()) == danglingWord, "Reference failure should retain the untrusted word.");

                WordBox emptySequenceWord = new WordBox(senseId, usageId);
                emptySequenceWord.SetSymbols(new List<Guid>());
                emptySequenceWord.SetPartOfSpeech(PartOfSpeech.Function);
                Program.Assert(core.AddTrustedWord(emptySequenceWord), "An explicitly assigned empty sequence is complete identity.");

                SentenceBox emptySentence = new SentenceBox();
                Program.Assert(core.SetUntrustedSentence(emptySentence), "Incomplete sentence should be storable as untrusted.");
                Program.Assert(core.SetUntrustedSentenceWords(emptySentence.GetSentenceID(), new List<Guid>()), "Empty sentence sequence should be assignable.");
                Program.Assert(core.SetUntrustedSentenceSenseID(emptySentence.GetSentenceID(), senseId), "Sentence sense should be assignable.");
                Program.Assert(core.ConvertUntrustedSentenceToTrusted(emptySentence.GetSentenceID()), "Explicitly assigned empty sentence should promote.");
            };
        }

        private static Action FactoriesUseSenseAwareIdentities()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid firstSense = AddSense(core);
                Guid secondSense = AddSense(core);
                Guid usageId = AddUsage(core);

                Guid firstSymbolId = AddTrustedSymbol(core, firstSense, 'm');
                Guid secondSymbolId = AddTrustedSymbol(core, secondSense, 'm');
                Program.Assert(core.GetTrustedSymbolIDs('m').Count == 2, "Character lookup should return every sense-specific symbol.");
                Program.Assert(core.GetTrustedSymbolID('m') == null, "Single-result character lookup should report ambiguous characters.");
                Program.Assert(
                    object.ReferenceEquals(core.GetNewSymbolBox('m', firstSense), core.GetTrustedSymbolBox(firstSymbolId)),
                    "Symbol factory should find the character-and-sense identity.");
                Program.Assert(
                    object.ReferenceEquals(core.GetNewSymbolBox('m', secondSense), core.GetTrustedSymbolBox(secondSymbolId)),
                    "Symbol factory should distinguish the same character by sense.");

                WordBox word = new WordBox(Guid.NewGuid(), firstSense, usageId,
                    new List<Guid> { firstSymbolId }, PartOfSpeech.Noun);
                Program.Assert(core.AddTrustedWord(word), "Sentence fixture word should be trusted.");
                List<Guid> wordIds = new List<Guid> { word.GetWordID() };
                SentenceBox firstSentence = new SentenceBox(Guid.NewGuid(), firstSense, wordIds);
                SentenceBox secondSentence = new SentenceBox(Guid.NewGuid(), secondSense, wordIds);
                Program.Assert(core.AddTrustedSentence(firstSentence), "First sentence identity should be accepted.");
                Program.Assert(core.AddTrustedSentence(secondSentence), "A different sentence sense should be distinct.");
                Program.Assert(
                    object.ReferenceEquals(core.GetNewSentenceBox(wordIds, firstSense), firstSentence),
                    "Sentence factory should reuse the exact word-sequence-and-sense identity.");
                Program.Assert(
                    object.ReferenceEquals(core.GetNewSentenceBox(wordIds, secondSense), secondSentence),
                    "Sentence factory should distinguish identical word sequences by sense.");
                Program.Assert(!core.AddTrustedSentence(new SentenceBox(Guid.NewGuid(), firstSense, wordIds)),
                    "Exact sentence identity duplicates should be rejected.");
            };
        }

        private static Action UntrustedBoxConstructionAvoidsTrustedIdCollisions()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'k');
                List<Guid> symbols = new List<Guid> { symbolId };

                Guid trustedWordId = Guid.NewGuid();
                WordBox trustedWord = new WordBox(trustedWordId, senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(core.AddTrustedWord(trustedWord), "Trusted word should be registered.");

                Guid trustedSentenceId = Guid.NewGuid();
                SentenceBox trustedSentence = new SentenceBox(trustedSentenceId, senseId, new List<Guid> { trustedWordId });
                Program.Assert(core.AddTrustedSentence(trustedSentence), "Trusted sentence should be registered.");

                Guid trustedGroupId = Guid.NewGuid();
                Program.Assert(core.AddTrustedGroup(new GroupBox(trustedGroupId, new List<Guid> { trustedSentenceId })),
                    "Trusted group should be registered.");

                Guid trustedSourceId = Guid.NewGuid();
                Program.Assert(core.AddTrustedSource(new SourceBox(trustedSourceId, new List<Guid> { trustedGroupId })),
                    "Trusted source should be registered.");

                Guid trustedFunctionId = Guid.NewGuid();
                FunctionBox trustedFunction = new FunctionBox(trustedFunctionId, typeof(double), "Math_2.CalculateCircleArea", "m²");
                trustedFunction.AddVariable(new FunctionVariable("radius", 2.0, typeof(double), "m"));
                trustedFunction.StoreResult(new FunctionResult(Math.PI * 4.0, typeof(double), "m²"));
                Program.Assert(core.AddTrustedFunction(trustedFunction), "Trusted function should be registered.");

                Guid trustedNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, trustedWordId)!.GetNodeID();

                Guid textConversationId = Guid.NewGuid();
                Program.Assert(core.AddTextConversation(new ConversationText(textConversationId)),
                    "Text conversation should be registered.");

                Program.AssertThrows<ArgumentException>(() => new WordBox(trustedWordId, senseId, usageId),
                    "WordBox constructor must reject an ID matching a registered trusted WordBox.");
                Program.AssertThrows<ArgumentException>(() => new WordBox(trustedWordId, symbols),
                    "WordBox symbol-list constructor must reject an ID matching a registered trusted WordBox.");
                Program.AssertThrows<ArgumentException>(() => new WordBox(trustedWordId, senseId, usageId, symbols),
                    "WordBox 4-arg constructor must reject an ID matching a registered trusted WordBox.");
                Program.AssertThrows<ArgumentException>(() => new WordBox(trustedWordId, senseId, usageId, symbols, PartOfSpeech.Verb),
                    "WordBox 5-arg constructor must reject an ID matching a registered trusted WordBox.");

                Program.AssertThrows<ArgumentException>(() => new SymbolBox(symbolId, 'z'),
                    "SymbolBox constructor must reject an ID matching a registered trusted SymbolBox.");
                Program.AssertThrows<ArgumentException>(() => new SentenceBox(trustedSentenceId),
                    "SentenceBox constructor must reject an ID matching a registered trusted SentenceBox.");
                Program.AssertThrows<ArgumentException>(() => new SenseBox(senseId),
                    "SenseBox constructor must reject an ID matching a registered trusted SenseBox.");
                Program.AssertThrows<ArgumentException>(() => new UsageBox(usageId),
                    "UsageBox constructor must reject an ID matching a registered trusted UsageBox.");
                Program.AssertThrows<ArgumentException>(() => new GroupBox(trustedGroupId),
                    "GroupBox constructor must reject an ID matching a registered trusted GroupBox.");
                Program.AssertThrows<ArgumentException>(() => new SourceBox(trustedSourceId),
                    "SourceBox constructor must reject an ID matching a registered trusted SourceBox.");
                Program.AssertThrows<ArgumentException>(() => new FunctionBox(trustedFunctionId, typeof(double)),
                    "FunctionBox constructor must reject an ID matching a registered trusted FunctionBox.");
                Program.AssertThrows<ArgumentException>(() => new Node(trustedNodeId, NodeTargetKind.Word, Guid.NewGuid()),
                    "Node constructor must reject an ID matching a registered trusted Node.");
                Program.AssertThrows<ArgumentException>(() => new ConversationText(textConversationId),
                    "ConversationText constructor must reject an ID matching a registered conversation.");
                Program.AssertThrows<ArgumentException>(() => new ConversationSymbolic(textConversationId),
                    "ConversationSymbolic constructor must reject an ID matching a registered conversation.");

                Guid expectedFreeWordId = Guid.NewGuid();
                int wordGenAttempt = 0;
                Guid generatedWordId = core.GenerateUntrustedId(NodeTargetKind.Word, () =>
                    wordGenAttempt++ == 0 ? trustedWordId : expectedFreeWordId);
                Program.Assert(generatedWordId == expectedFreeWordId && wordGenAttempt == 2,
                    "GenerateUntrustedId must skip registered trusted WordBox IDs.");

                WordBox autoWord = core.GetNewWordBox(symbols, senseId, usageId, PartOfSpeech.Adverb);
                Program.Assert(autoWord.GetWordID() != trustedWordId && !core.IsTrustedIdRegistered(NodeTargetKind.Word, autoWord.GetWordID()),
                    "GetNewWordBox must generate an untrusted WordID that does not conflict with trusted WordBoxes.");
            };
        }

        private static Action UntrustedFunctionIdCollisionsAreRejected()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid functionId = Guid.NewGuid();
                FunctionBox original = new FunctionBox(functionId, typeof(double), "Math_1.AddDouble");
                FunctionBox collision = new FunctionBox(functionId, typeof(double), "Math_1.SubtractDouble");

                Program.Assert(core.SetUntrustedFunction(original), "The first function definition should be staged.");
                Program.Assert(!core.SetUntrustedFunction(collision), "A different definition must not replace a staged function ID.");
                Program.Assert(ReferenceEquals(core.GetUntrustedFunctionBox(functionId), original),
                    "The first staged function definition should remain canonical for its ID.");
            };
        }

        private static Action ConcurrentAddsPreserveIdentityIndexes()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                int acceptedSymbols = 0;
                Parallel.For(0, 32, _ =>
                {
                    SymbolBox symbol = new SymbolBox(Guid.NewGuid(), 'q');
                    symbol.SetSenseID(senseId);
                    if (core.AddTrustedSymbol(symbol))
                    {
                        Interlocked.Increment(ref acceptedSymbols);
                    }
                });
                Program.Assert(acceptedSymbols == 1, "Concurrent duplicate symbols should yield one canonical trusted node.");

                Guid symbolId = core.GetTrustedSymbolIDs('q')[0];
                int acceptedWords = 0;
                Parallel.For(0, 32, _ =>
                {
                    WordBox word = new WordBox(Guid.NewGuid(), senseId, usageId,
                        new List<Guid> { symbolId }, PartOfSpeech.Noun);
                    if (core.AddTrustedWord(word))
                    {
                        Interlocked.Increment(ref acceptedWords);
                    }
                });
                Program.Assert(acceptedWords == 1, "Concurrent duplicate words should yield one canonical trusted node.");
            };
        }

        private static Action ConcurrentDuplicatePromotionsAreAtomic()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'r');
                List<Guid> symbols = new List<Guid> { symbolId };
                WordBox first = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                WordBox second = new WordBox(Guid.NewGuid(), senseId, usageId, symbols, PartOfSpeech.Noun);
                Program.Assert(core.SetUntrustedWord(first) && core.SetUntrustedWord(second),
                    "Both semantically duplicate words should be staged before promotion.");

                int promoted = 0;
                Parallel.Invoke(
                    () => { if (core.ConvertUntrustedWordToTrusted(first.GetWordID())) Interlocked.Increment(ref promoted); },
                    () => { if (core.ConvertUntrustedWordToTrusted(second.GetWordID())) Interlocked.Increment(ref promoted); });

                Program.Assert(promoted == 1,
                    "Concurrent promotions of the same semantic identity should have exactly one winner.");
                Program.Assert((core.GetTrustedWordBox(first.GetWordID()) != null) !=
                               (core.GetTrustedWordBox(second.GetWordID()) != null),
                    "Exactly one duplicate should be trusted after the race.");
                Program.Assert((core.GetUntrustedWordBox(first.GetWordID()) != null) !=
                               (core.GetUntrustedWordBox(second.GetWordID()) != null),
                    "The losing duplicate should remain staged after the race.");
                ResetCore();
            };
        }

        private static Guid AddSense(LRD_Core core)
        {
            Guid senseId = Guid.NewGuid();
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Test sense should be registered.");
            return senseId;
        }

        private static Guid AddUsage(LRD_Core core)
        {
            Guid usageId = Guid.NewGuid();
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Test usage should be registered.");
            return usageId;
        }

        private static Guid AddTrustedSymbol(LRD_Core core, Guid senseId, char value)
        {
            SymbolBox symbol = new SymbolBox(Guid.NewGuid(), value);
            symbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(symbol), "Test symbol should be registered.");
            return symbol.GetSymbolID();
        }

        private static void ResetCore()
        {
            LRD_Core.Instance.ResetCore();
            LRD_Core.Instance.InitializeCore();
        }
    }
}
