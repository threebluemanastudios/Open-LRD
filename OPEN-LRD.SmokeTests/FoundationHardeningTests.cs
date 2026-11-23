using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using OPEN_LRD.Foundations;
using OPEN_LRD.Persistence;
using OPEN_LRD.Skills;
using Vector2 = OPEN_LRD.Foundations.Vector2;
using Vector3 = OPEN_LRD.Foundations.Vector3;

namespace OPEN_LRD.SmokeTests
{
    internal static class FoundationHardeningTests
    {
        internal static void RunAll()
        {
            Program.Run("Foundation value validation and constructors", ValueValidationAndConstructors());
            Program.Run("Trusted functions validate and freeze definitions", TrustedFunctionLifecycle());
            Program.Run("Node label identity and canonical lookup", NodeLabelIdentity());
            Program.Run("Fraction BigInteger value semantics", FractionSemantics());
            Program.Run("Vector value semantics and finite boundaries", VectorSemantics());
            Program.Run("SymbolBox distinguishes assigned NUL", SymbolCharacterState());
            Program.Run("Conversation ranges are atomic and overflow-safe", ConversationRangeSemantics());
            Program.Run("Expression AST and parser boundaries", ExpressionBoundaries());
            Program.Run("Repeated-element promotion and GetWordByGuid", RepeatedElementPromotionAndWordLookup());
            Program.Run("Trusted link weight updates keep assertion-key state consistent", TrustedLinkWeightUpdateConsistency());
            Program.Run("Extended function scalar checkpoint payloads round-trip", ExtendedFunctionScalarPersistence());
        }

        private static Action ValueValidationAndConstructors()
        {
            return () =>
            {
                Program.AssertThrows<ArgumentException>(() => new FunctionVariable("x", 1, typeof(string)),
                    "Function variables must validate values against their declared types.");
                Program.AssertThrows<ArgumentException>(() => new FunctionVariable("x", null, typeof(int)),
                    "Non-nullable value types must reject null.");
                Program.AssertThrows<ArgumentException>(() => new FunctionVariable("x", null, typeof(void)),
                    "Function variables must reject void types.");
                Program.AssertThrows<ArgumentException>(() => new FunctionVariable("x", null, typeof(List<>)),
                    "Function variables must reject open generic types.");
                Program.Assert(new FunctionVariable("x", null, typeof(int?)).Value == null,
                    "Nullable value types should accept null.");
                FunctionBox variableBox = new FunctionBox(typeof(double));
                variableBox.AddVariable(new FunctionVariable("x", 1, typeof(int)));
                Program.AssertThrows<ArgumentException>(() => variableBox.AddVariable(new FunctionVariable("x", 2, typeof(int))),
                    "FunctionBox should reject duplicate variable names using ordinal comparison.");
                variableBox.SetVariable(new FunctionVariable("x", 3, typeof(int)));
                Program.Assert((int)variableBox.GetVariable("x")!.Value! == 3 && variableBox.RemoveVariable("x"),
                    "Untrusted FunctionBox variables should support set/get/remove operations.");

                Guid senseId = Guid.NewGuid();
                Guid usageId = Guid.NewGuid();
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    new WordBox(Guid.NewGuid(), senseId, usageId, new List<Guid>(), (PartOfSpeech)999),
                    "WordBox constructors must reject undefined part-of-speech values.");
                WordBox word = new WordBox(Guid.NewGuid(), new List<Guid>());
                Program.AssertThrows<ArgumentOutOfRangeException>(() => word.SetPartOfSpeech((PartOfSpeech)999),
                    "WordBox setters must reject undefined part-of-speech values.");

                WordBox first = new WordBox(Guid.NewGuid(), new List<Guid>());
                WordBox second = new WordBox(Guid.NewGuid(), new List<Guid>());
                Dictionary<int, WordBox> positionedWords = new Dictionary<int, WordBox>
                {
                    [8] = second,
                    [2] = first
                };
                SentenceBox sentence = new SentenceBox(Guid.NewGuid(), null, positionedWords);
                Program.Assert(sentence.GetWords()!.SequenceEqual(new[] { first.GetWordID(), second.GetWordID() }),
                    "Sentence dictionary constructors must order words by non-negative position keys.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    new SentenceBox(Guid.NewGuid(), null, new Dictionary<int, WordBox> { [-1] = first }),
                    "Sentence dictionary constructors must reject negative positions.");
                Program.AssertThrows<ArgumentException>(() =>
                    new GroupBox(Guid.NewGuid(), new Dictionary<Guid, SentenceBox>
                    {
                        [Guid.NewGuid()] = new SentenceBox(Guid.NewGuid(), new List<Guid>())
                    }), "Group dictionary constructors must require matching keys and sentence IDs.");

                GroupBox group = new GroupBox("  ");
                SourceBox source = new SourceBox(Guid.NewGuid(), "  source  ");
                Program.Assert(group.GetGroupingName() == null && source.GetSourceName() == "source",
                    "Optional Foundation names should be trimmed and whitespace normalized to null.");
                source.SetSourceName("   ");
                Program.Assert(source.GetSourceName() == null,
                    "SourceBox.SetSourceName should normalize whitespace to null.");
                LRD_Core.Instance.InitializeCore();
                Program.AssertThrows<InvalidDataException>(() =>
                    new SourceBox(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() }).GetGroupings(),
                    "A missing trusted grouping reference should be reported as corruption.");

                using MemoryStream oversizedRecord = new MemoryStream();
                using (BinaryWriter writer = new BinaryWriter(oversizedRecord, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write(true);
                    writer.Write((byte)0x81);
                    writer.Write((byte)0x80);
                    writer.Write((byte)0x40);
                }
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeGroup(oversizedRecord.ToArray()),
                    "Oversized serialized strings should be rejected before allocation.");
            };
        }

        private static Action TrustedFunctionLifecycle()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                core.InitializeCore();
                try
                {
                    FunctionBox incomplete = new FunctionBox(typeof(double), "Math_2.CalculateCircleArea", "m²");
                    incomplete.AddVariable(new FunctionVariable("radius", 2.0, typeof(double), "m"));
                    Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedFunction(incomplete),
                        "FunctionBox Result must be present before promotion to trusted.");

                    FunctionBox function = new FunctionBox(typeof(double), "Math_2.CalculateCircleArea", "m²");
                    function.AddVariable(new FunctionVariable("radius", 2.0, typeof(double), "m"));
                    function.StoreResult(new FunctionResult(Math.PI * 4.0, typeof(double), "m²"));
                    Program.Assert(core.SetUntrustedFunction(function), "A valid function should enter untrusted staging.");
                    Program.Assert(core.GetUntrustedFunctionBox(function.FunctionID) == function,
                        "The untrusted FunctionBox should be retrievable by ID.");
                    Program.AssertThrows<InvalidOperationException>(() => core.ConvertUntrustedFunctionToTrusted(function.FunctionID),
                        "Manual results are unverified and cannot be trusted without dispatch verification.");
                    MathFunctionDispatcher.Dispatch(function);
                    Program.Assert(core.ConvertUntrustedFunctionToTrusted(function.FunctionID),
                        "A dispatch-verified function should promote to trusted.");
                    FunctionBox trusted = core.GetTrustedFunctionBox(function.FunctionID)!;
                    FunctionResult original = trusted.Result!;
                    Program.AssertThrows<InvalidOperationException>(() => trusted.AddVariable(new FunctionVariable("other", 1.0, typeof(double))),
                        "Trusted FunctionBox variables must be immutable.");
                    Program.AssertThrows<InvalidOperationException>(() => trusted.SetVariable(new FunctionVariable("radius", 3.0, typeof(double), "m")),
                        "Trusted FunctionBox variables must not be replaceable.");
                    Program.AssertThrows<InvalidOperationException>(() => trusted.RemoveVariable("radius"),
                        "Trusted FunctionBox variables must not be removable.");
                    Program.AssertThrows<InvalidOperationException>(() => trusted.StoreResult(original),
                        "Trusted FunctionBox Result must be immutable.");

                    FunctionResult dispatched = MathFunctionDispatcher.Dispatch(trusted);
                    Program.Assert(Math.Abs((double)dispatched.Value! - (Math.PI * 4.0)) < 1e-10,
                        "Trusted FunctionBox dispatch should return the computed value.");
                    Program.Assert(ReferenceEquals(trusted.Result, original),
                        "Dispatch must not mutate the immutable trusted Result.");
                    Program.Assert(core.RemoveUntrustedFunction(function.FunctionID) == false,
                        "Promotion should remove the function from untrusted staging.");
                }
                finally
                {
                    core.ResetCore();
                }
            };
        }

        private static Action NodeLabelIdentity()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-labels-" + Guid.NewGuid().ToString("N"));
                core.ResetCore();
                core.InitializeCore();
                try
                {
                    Guid senseId = Guid.NewGuid();
                    SenseBox sense = new SenseBox(senseId);
                    Program.Assert(core.AddTrustedSense(sense), "Sense should be trusted.");
                Program.AssertThrows<InvalidOperationException>(() => sense.AddSentence(Guid.NewGuid()),
                    "Trusted SenseBox memberships can only be changed through the core.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() => sense.SetAverageSpread(float.NaN),
                    "Sense metrics must reject non-finite values.");
                    UsageBox usage = new UsageBox(Guid.NewGuid());
                    Program.Assert(core.AddTrustedUsage(usage), "Usage should be trusted.");
                    Program.AssertThrows<InvalidOperationException>(() => usage.AddSentence(Guid.NewGuid()),
                        "Trusted UsageBox memberships can only be changed through the core.");
                    Guid symbolId = Guid.NewGuid();
                    SymbolBox symbol = new SymbolBox(symbolId, 'a');
                    symbol.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(symbol), "Symbol should be trusted.");
                    Guid wordId = Guid.NewGuid();
                    WordBox word = new WordBox(wordId, senseId, usage.GetUsageID(), new List<Guid> { symbolId }, PartOfSpeech.Noun);
                    Program.Assert(core.AddTrustedWord(word), "Word should be trusted.");
                    Guid sentenceId = Guid.NewGuid();
                    SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId });
                    Program.Assert(core.AddTrustedSentence(sentence), "Sentence should be trusted.");
                    Guid duplicateSymbolId = Guid.NewGuid();
                    SymbolBox duplicateSymbol = new SymbolBox(duplicateSymbolId, 'a');
                    duplicateSymbol.SetSenseID(senseId);
                    Program.Assert(core.SetUntrustedSymbol(duplicateSymbol), "Duplicate symbol should enter staging.");
                    Guid duplicateWordId = Guid.NewGuid();
                    Program.Assert(core.SetUntrustedWord(new WordBox(duplicateWordId, senseId, usage.GetUsageID(),
                        new List<Guid> { duplicateSymbolId }, PartOfSpeech.Noun)), "Duplicate word should enter staging.");
                    Guid duplicateSentenceId = Guid.NewGuid();
                    Program.Assert(core.SetUntrustedSentence(new SentenceBox(duplicateSentenceId, senseId,
                        new List<Guid> { duplicateWordId })), "Duplicate sentence should enter staging.");
                    Program.Assert(core.ResolveOrPromoteUntrustedSentenceSubgraph(duplicateSentenceId) == sentenceId &&
                                   core.GetUntrustedSymbolBox(duplicateSymbolId) == null &&
                                   core.GetUntrustedWordBox(duplicateWordId) == null &&
                                   core.GetUntrustedSentenceBox(duplicateSentenceId) == null,
                        "Sentence-subgraph canonical resolution should rebind duplicate symbols and words.");
                    GroupBox group = new GroupBox(Guid.NewGuid(), new List<Guid> { sentenceId });
                    Program.Assert(core.AddTrustedGroup(group), "Group should be trusted.");
                    SourceBox source = new SourceBox(Guid.NewGuid(), new List<Guid> { group.GetGroupingID() });
                    Program.Assert(core.AddTrustedSource(source), "Source should be trusted.");
                    Program.Assert(sense.GetSentenceCount() == 1 && sense.ContainsSentence(sentenceId) &&
                                   usage.GetSentenceCount() == 1 && usage.ContainsSentence(sentenceId) &&
                                   group.GetSentenceCount() == 1 && group.ContainsSentence(sentenceId) &&
                                   source.GetGroupingCount() == 1 && source.ContainsGrouping(group.GetGroupingID()),
                        "Foundation membership count and contains APIs should reflect core-managed appends.");
                    Program.Assert(source.GetGroupings()!.ContainsKey(group.GetGroupingID()),
                        "Source grouping queries should return resolved trusted groups.");
                    Node canonical = core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!;

                    Node untrusted = new Node(NodeTargetKind.Symbol, symbolId, " draft ");
                    Program.Assert(core.AddUntrustedNode(untrusted), "A labeled node may coexist with its canonical target node.");
                    untrusted.SetLabel(" alternate ");
                    Program.Assert(core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId, "draft") == null &&
                                   core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId, "alternate") == untrusted,
                        "Changing an untrusted label must keep identity indexes synchronized.");
                    Program.Assert(core.ConvertUntrustedNodeToTrusted(untrusted.GetNodeID()),
                        "A labeled node for a trusted target should promote independently.");

                    Node labeled = core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId, "alternate")!;
                    Program.Assert(labeled.GetNodeID() != canonical.GetNodeID(),
                        "Canonical and labeled nodes should have distinct identities.");
                    Program.Assert(core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!.GetNodeID() == canonical.GetNodeID(),
                        "The target-only lookup should return the canonical node.");
                    Program.Assert(core.GetTrustedNodesByTarget(NodeTargetKind.Symbol, symbolId).Count == 2,
                        "Target enumeration should return canonical and labeled variants.");
                    Program.Assert(core.GetTrustedNodeIds(NodeTargetKind.Symbol).Count >= 2 &&
                                   core.GetTrustedNodeCount() == core.GetTrustedNodes().Count &&
                                   core.GetTrustedNodeSummary(labeled)!.StartsWith("alternate:", StringComparison.Ordinal) &&
                                   core.GetTrustedLinkCount() == core.GetAllTrustedLinks().Count,
                        "Bulk inspection, counts, and display summaries should be consistent.");
                    Program.Assert(core.GetOutgoingLinks(labeled.GetNodeID()).Count == 0,
                        "Labeled nodes should not receive automatically wired structural relations.");
                    Program.AssertThrows<InvalidOperationException>(() => labeled.SetLabel("changed"),
                        "Trusted labels must be immutable.");

                    core.StreamTrustedDictionariesToDisk(directory);
                    core.StreamTrustedDictionariesFromDisk(directory, true, false, 1);
                    Program.Assert(core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId, "alternate")?.GetNodeID() == labeled.GetNodeID() &&
                                   core.GetTrustedNodesByTarget(NodeTargetKind.Symbol, symbolId).Count == 2 &&
                                   core.FindTrustedNodes(NodeTargetKind.Symbol, canonicalOnly: true).Any(node => node.GetTargetID() == symbolId) &&
                                   core.GetWordByGuid(wordId)?.GetWordID() == wordId,
                        "Full node identity and canonical indexes should survive disk paging.");

                Node counterNode = new Node(NodeTargetKind.Symbol, Guid.NewGuid());
                counterNode.RegisterVerifiedReference(int.MaxValue);
                Program.AssertThrows<OverflowException>(() => counterNode.RegisterVerifiedReference(),
                    "Node reference counters must fail on overflow.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            };
        }

        private static Action FractionSemantics()
        {
            return () =>
            {
                Fraction half = new Fraction(new BigInteger(2), new BigInteger(4));
                Fraction sameHalf = new Fraction(new BigInteger(-3), new BigInteger(-6));
                Program.Assert(half == sameHalf && half.ToString() == "1/2",
                    "Fractions should reduce and compare by canonical numeric value.");
                Fraction zero = new Fraction(BigInteger.Zero, new BigInteger(-10));
                Program.Assert(zero.ToString() == "0/1", "Zero should have canonical denominator one.");
                Fraction huge = new Fraction(BigInteger.Parse("1000000000000000000000000000000"), new BigInteger(3));
                Fraction parsed = Fraction.Parse(huge.ToString());
                Program.Assert(parsed == huge && Fraction.TryParse("-6/-8", out Fraction? parsedThreeQuarters) &&
                               parsedThreeQuarters == new Fraction(new BigInteger(3), new BigInteger(4)),
                    "BigInteger fractions should parse and round-trip invariantly.");
                Fraction negative = new Fraction(new BigInteger(-7), new BigInteger(3));
                Program.Assert(Fraction.Parse(negative.ToString()) == negative && negative.CompareTo(null) > 0,
                    "Negative fractions should round-trip and compare greater than a null reference.");
                Program.Assert((half + half) == new Fraction(1, 1), "Fraction arithmetic should remain exact.");
                Program.AssertThrows<DivideByZeroException>(() => _ = half / zero,
                    "Fraction division by zero should throw.");
                Program.AssertThrows<DivideByZeroException>(() => _ = new Fraction(BigInteger.One, BigInteger.Zero),
                    "A zero denominator should throw.");
                Program.Assert(!Fraction.TryParse(null, out _), "Null fraction text should not parse.");
                Program.Assert(!Fraction.TryParse("   ", out _), "Blank fraction text should not parse.");
                Program.Assert(!Fraction.TryParse("1/2/3", out _), "Fraction text with multiple separators should not parse.");
                Program.Assert(!Fraction.TryParse("1/x", out _), "Fraction text with a non-numeric denominator should not parse.");
                Program.Assert(!Fraction.TryParse("1/0", out _), "Fraction text with a zero denominator should not parse.");
                Program.Assert(Fraction.TryParse("42", out Fraction? whole) && whole == new Fraction(42, 1),
                    "An integer-only fraction string should parse with denominator one.");
            };
        }

        private static Action VectorSemantics()
        {
            return () =>
            {
                Vector2 vector = new Vector2(3.0, 4.0);
                Program.Assert(vector.Length() == 5.0 && vector.Normalize() == new Vector2(0.6, 0.8),
                    "Vector2 should support length, normalization, and value equality.");
                Program.Assert(Vector2.Zero.Distance(vector) == 5.0,
                    "Vector2 distance should be Euclidean.");
                Program.Assert(Vector2.UnitX.Angle(Vector2.UnitY) == Math.PI / 2.0,
                    "Vector2 angle should be in radians.");
                Program.Assert(Vector2.Lerp(Vector2.Zero, Vector2.UnitX, 0.5) == new Vector2(0.5, 0.0),
                    "Vector2 Lerp should interpolate values.");
                Program.Assert(Vector2.Lerp(Vector2.Zero, Vector2.UnitX, 2.0) == new Vector2(2.0, 0.0) &&
                               Vector2.Lerp(Vector2.Zero, Vector2.UnitX, -1.0) == new Vector2(-1.0, 0.0),
                    "Finite interpolation parameters outside [0, 1] should extrapolate linearly.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() => Vector2.Lerp(Vector2.Zero, Vector2.UnitX, double.NaN),
                    "Interpolation should reject non-finite parameters.");
                Program.Assert(new Vector2(1e200, 0.0).Normalize() == Vector2.UnitX,
                    "Vector normalization should remain stable for large finite components.");
                Program.Assert(Vector3.UnitX.Cross(Vector3.UnitY) == Vector3.UnitZ,
                    "Vector3 cross product should follow the right-hand rule.");
                Program.Assert(new Vector2(1.0, 2.0).ToString() == "(1, 2)",
                    "Vector formatting should be invariant and round-trip friendly.");
                Program.AssertThrows<InvalidOperationException>(() => Vector2.Zero.Normalize(),
                    "Zero-length vectors should not be normalized.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() => new Vector3(double.NaN, 0.0, 0.0),
                    "Vectors should reject non-finite components.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() => new Vector3(double.PositiveInfinity, 0.0, 0.0),
                    "Vectors should reject positive infinity components.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() => new Vector2(double.NegativeInfinity, 0.0),
                    "Vectors should reject negative infinity components.");
                Program.AssertThrows<DivideByZeroException>(() => _ = vector / 0.0,
                    "Vector division by zero should throw.");
            };
        }

        private static Action SymbolCharacterState()
        {
            return () =>
            {
                SymbolBox unassigned = new SymbolBox();
                Program.Assert(!unassigned.HasSymbolChar && unassigned.GetSymbolChar() == '\0',
                    "A default SymbolBox should distinguish its unassigned character.");
                unassigned.SetSymbolChar('\0');
                Program.Assert(unassigned.HasSymbolChar && unassigned.GetSymbolChar() == '\0',
                    "An explicitly assigned NUL should be distinguishable from an unassigned character.");
                SymbolBox surrogate = new SymbolBox(Guid.NewGuid(), '\uD800');
                Program.Assert(surrogate.HasSymbolChar && surrogate.GetSymbolChar() == '\uD800',
                    "SymbolBox should accept isolated UTF-16 surrogate code units.");
                SymbolBox assignedNul = new SymbolBox(Guid.NewGuid(), '\0');
                assignedNul.SetSenseID(Guid.NewGuid());
                SymbolBox restored = FoundationBinarySerializer.DeserializeSymbol(
                    FoundationBinarySerializer.SerializeSymbol(assignedNul));
                Program.Assert(restored.HasSymbolChar && restored.GetSymbolChar() == '\0',
                    "Binary serialization should preserve an assigned NUL character.");
                SymbolBox storedSurrogate = new SymbolBox(Guid.NewGuid(), '\uD800');
                storedSurrogate.SetSenseID(Guid.NewGuid());
                SymbolBox restoredSurrogate = FoundationBinarySerializer.DeserializeSymbol(
                    FoundationBinarySerializer.SerializeSymbol(storedSurrogate));
                Program.Assert(restoredSurrogate.GetSymbolChar() == '\uD800',
                    "Binary serialization should preserve isolated UTF-16 surrogate code units.");
            };
        }

        private static Action ConversationRangeSemantics()
        {
            return () =>
            {
                ConversationText text = new ConversationText();
                Program.AssertThrows<ArgumentException>(() => text.AddTextRange(new List<string> { "valid", null! }),
                    "A text range containing null should be rejected atomically.");
                Program.Assert(text.GetEntryCount() == 0 && text.GetCurrentTextCount() == 0,
                    "Rejected text ranges should leave state unchanged.");
                text.AddTextRange(new List<string> { "first", "second" });
                Program.Assert(text.GetEntryCount() == 2 && text.GetCurrentTextCount() == 2 &&
                               text.ContainsId(0) && text.GetTextById(1) == "second",
                    "Text sequential ranges should update IDs and counts.");
                text.AddTextRange(new Dictionary<UInt128, string> { [1] = "updated", [5] = "fifth" });
                Program.Assert(text.GetTextById(1) == "updated" && text.GetCurrentTextCount() == 6,
                    "Text dictionary ranges should upsert existing IDs and advance the next ID.");
                Program.Assert(text.Remove(0) && text.GetEntryCount() == 2,
                    "Text entries should support removal without reusing IDs.");
                Program.Assert(!text.Remove(99), "Removing a missing text ID should return false without changing entries.");
                text.Clear();
                Program.Assert(text.GetEntryCount() == 0 && text.GetCurrentTextCount() == 0,
                    "Clear should reset text entries and ID allocation.");
                ConversationText exhaustedText = new ConversationText(new Dictionary<UInt128, string>
                {
                    [UInt128.MaxValue] = "last"
                });
                Program.Assert(exhaustedText.IsIdSpaceExhausted,
                    "A conversation containing UInt128.MaxValue should mark its ID space exhausted.");
                Program.AssertThrows<OverflowException>(() => exhaustedText.AddText("overflow"),
                    "Text ID advancement must not wrap past UInt128.MaxValue.");

                ConversationSymbolic symbolic = new ConversationSymbolic();
                Guid symbolId = Guid.NewGuid();
                List<Guid> symbols = new List<Guid> { symbolId };
                Program.AssertThrows<ArgumentException>(() => symbolic.AddSymbolicRange(new List<List<Guid>>
                {
                    symbols,
                    new List<Guid> { Guid.Empty }
                }), "Invalid symbolic range entries should be rejected atomically.");
                Program.Assert(symbolic.GetEntryCount() == 0 && symbolic.GetCurrentSymbolicCount() == 0,
                    "Rejected symbolic ranges should leave state unchanged.");
                symbolic.AddSymbolicRange(new List<List<Guid>> { symbols });
                symbols.Clear();
                Program.Assert(symbolic.GetSymbolicById(0)!.SequenceEqual(new[] { symbolId }) &&
                               symbolic.ContainsId(0) && symbolic.GetEntryCount() == 1,
                    "Symbolic ranges should snapshot nested lists and expose count/contains APIs.");
                symbolic.AddSymbolicRange(new Dictionary<UInt128, List<Guid>>
                {
                    [0] = new List<Guid> { symbolId },
                    [3] = new List<Guid>()
                });
                Program.Assert(symbolic.GetCurrentSymbolicCount() == 4 && symbolic.GetEntryCount() == 2,
                    "Symbolic dictionary ranges should upsert existing IDs and advance safely.");
                Program.Assert(symbolic.Remove(0), "Symbolic entries should support removal.");
                symbolic.Clear();
                Program.Assert(symbolic.GetEntryCount() == 0 && symbolic.GetCurrentSymbolicCount() == 0,
                    "Clear should reset symbolic entries and ID allocation.");
                ConversationSymbolic exhaustedSymbolic = new ConversationSymbolic(
                    new Dictionary<UInt128, List<Guid>> { [UInt128.MaxValue] = new List<Guid>() });
                Program.AssertThrows<OverflowException>(() => exhaustedSymbolic.AddSymbolic(new List<Guid>()),
                    "Symbolic ID advancement must not wrap past UInt128.MaxValue.");
            };
        }

        private static Action RepeatedElementPromotionAndWordLookup()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                core.InitializeCore();
                try
                {
                    Guid senseId = Guid.NewGuid();
                    Guid usageId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");

                    Guid symbolO = Guid.NewGuid();
                    Guid symbolL = Guid.NewGuid();
                    SymbolBox o = new SymbolBox(symbolO, 'o'); o.SetSenseID(senseId);
                    SymbolBox l = new SymbolBox(symbolL, 'l'); l.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(o) && core.AddTrustedSymbol(l), "Symbols should be trusted.");

                    Guid wordId = Guid.NewGuid();
                    WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolL, symbolO, symbolO, symbolL }, PartOfSpeech.Noun);
                    Program.Assert(core.SetUntrustedWord(word), "Repeated-letter word should stage.");
                    Guid? promotedWord = core.ResolveOrPromoteUntrustedWord(wordId);
                    Program.Assert(promotedWord == wordId && core.GetTrustedWordBox(wordId) != null,
                        "Repeated-letter words should promote without deduplication failure.");
                    Program.Assert(core.GetWordByGuid(wordId) != null, "GetWordByGuid should resolve trusted words.");
                    Program.Assert(core.GetWordByGuid(Guid.Empty) == null, "GetWordByGuid should reject Guid.Empty.");

                    Guid untrustedWordId = Guid.NewGuid();
                    WordBox stagedWord = new WordBox(untrustedWordId, senseId, usageId, new List<Guid> { symbolO }, PartOfSpeech.Noun);
                    Program.Assert(core.SetUntrustedWord(stagedWord), "Untrusted word should stage.");
                    Program.Assert(ReferenceEquals(core.GetWordByGuid(untrustedWordId), stagedWord),
                        "GetWordByGuid should resolve staged untrusted words.");
                    Node labeledUntrustedWord = new Node(NodeTargetKind.Word, untrustedWordId, "draft");
                    Program.Assert(core.AddUntrustedNode(labeledUntrustedWord), "labeled untrusted node should be accepted");
                    Program.Assert(core.RemoveUntrustedWord(untrustedWordId) &&
                                   core.GetUntrustedNode(labeledUntrustedWord.GetNodeID()) == null &&
                                   core.GetUntrustedNodeByTarget(NodeTargetKind.Word, untrustedWordId, "draft") == null,
                        "removing a box should clean up its canonical and labeled untrusted nodes");

                    Guid sentenceId = Guid.NewGuid();
                    SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId, wordId });
                    Program.Assert(core.SetUntrustedSentence(sentence), "Repeated-word sentence should stage.");
                    Guid? promotedSentence = core.ResolveOrPromoteUntrustedSentenceSubgraph(sentenceId);
                    Program.Assert(promotedSentence == sentenceId && core.GetTrustedSentenceBox(sentenceId) != null,
                        "Repeated-word sentences should promote without deduplication failure.");
                }
                finally
                {
                    core.ResetCore();
                }
            };
        }

        private static Action TrustedLinkWeightUpdateConsistency()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                core.InitializeCore();
                try
                {
                    Guid senseId = Guid.NewGuid();
                    Guid usageId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");
                    Guid s1 = Guid.NewGuid(); Guid s2 = Guid.NewGuid();
                    SymbolBox sb1 = new SymbolBox(s1, 'a'); sb1.SetSenseID(senseId);
                    SymbolBox sb2 = new SymbolBox(s2, 'b'); sb2.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(sb1) && core.AddTrustedSymbol(sb2), "Symbols should be trusted.");
                    Guid w1 = Guid.NewGuid(); Guid w2 = Guid.NewGuid();
                    Program.Assert(core.AddTrustedWord(new WordBox(w1, senseId, usageId, new List<Guid> { s1 }, PartOfSpeech.Noun)), "Word should be trusted.");
                    Program.Assert(core.AddTrustedWord(new WordBox(w2, senseId, usageId, new List<Guid> { s2 }, PartOfSpeech.Noun)), "Word should be trusted.");
                    Guid s3 = Guid.NewGuid(); SymbolBox sb3 = new SymbolBox(s3, 'c'); sb3.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(sb3), "Symbol should be trusted.");
                    Guid w3 = Guid.NewGuid();
                    Program.Assert(core.AddTrustedWord(new WordBox(w3, senseId, usageId, new List<Guid> { s3 }, PartOfSpeech.Noun)), "Word should be trusted.");
                    Guid n1 = core.GetTrustedNodeByTarget(NodeTargetKind.Word, w1)!.GetNodeID();
                    Guid n2 = core.GetTrustedNodeByTarget(NodeTargetKind.Word, w2)!.GetNodeID();
                    Guid n3 = core.GetTrustedNodeByTarget(NodeTargetKind.Word, w3)!.GetNodeID();

                    NodeLinkProvenance provenance = new NodeLinkProvenance(
                        NodeLinkProvenance.CreateForLink(n1, n2, NodeRelationType.Synonym, "pair", NodeLinkOrigin.User, 0.5).AssertionId,
                        NodeLinkOrigin.User, DateTimeOffset.UtcNow, sourceTargetId: w1, confidence: 0.5);
                    Program.Assert(core.AddTrustedLink(new NodeLink(n1, n2, NodeRelationType.Synonym, 0.5, "pair", provenance)),
                        "Synonym link should be added.");
                    Program.Assert(core.UpdateTrustedLinkWeight(provenance.AssertionId, 0.9),
                        "Assertion weight update should succeed.");
                    List<NodeLink> byProvenance = core.GetTrustedLinksByProvenanceTarget(w1);
                    Program.Assert(byProvenance.Count == 1 && byProvenance[0].Weight == 0.9,
                        "Provenance lookup should see updated weight after assertion update.");
                    Program.AssertThrows<ArgumentException>(() => core.AddTrustedLink(
                        new NodeLink(n2, n3, NodeRelationType.Synonym, 0.5, "pair", provenance)),
                        "reusing an assertion ID for a different endpoint pair should be rejected in memory");
                }
                finally
                {
                    core.ResetCore();
                }
            };
        }

        private static Action ExtendedFunctionScalarPersistence()
        {
            return () =>
            {
                LRD_Core core = LRD_Core.Instance;
                core.ResetCore();
                core.InitializeCore();
                try
                {
                    BigInteger huge = BigInteger.Pow(10, 150) + 987654321;
                    AssertFunctionValueRoundTrip(huge, typeof(BigInteger), huge.Equals);
                    Fraction fraction = new Fraction(huge, new BigInteger(37));
                    AssertFunctionValueRoundTrip(fraction, typeof(Fraction), value => fraction.Equals((Fraction)value!));
                    Vector2 vector2 = new Vector2(1.25, -8.5);
                    AssertFunctionValueRoundTrip(vector2, typeof(Vector2), value => vector2.Equals((Vector2)value!));
                    Vector3 vector3 = new Vector3(1.25, -8.5, 3.0);
                    AssertFunctionValueRoundTrip(vector3, typeof(Vector3), value => vector3.Equals((Vector3)value!));
                    Complex complex = new Complex(-2.0, 7.5);
                    AssertFunctionValueRoundTrip(complex, typeof(Complex), value => complex.Equals((Complex)value!));
                    double[] doubles = { 1.0, -2.5, 9.0 };
                    AssertFunctionValueRoundTrip(doubles, typeof(double[]), value => doubles.SequenceEqual((double[])value!));
                    int[] integers = { int.MinValue, 0, int.MaxValue };
                    AssertFunctionValueRoundTrip(integers, typeof(int[]), value => integers.SequenceEqual((int[])value!));
                }
                finally
                {
                    core.ResetCore();
                }
            };
        }

        private static void AssertFunctionValueRoundTrip(object value, Type valueType, Func<object?, bool> equals)
        {
            FunctionBox box = new FunctionBox(valueType);
            box.AddVariable(new FunctionVariable("value", value, valueType));
            box.StoreResult(new FunctionResult(value, valueType, null));
            FunctionBox restored = FoundationBinarySerializer.DeserializeFunction(
                FoundationBinarySerializer.SerializeFunction(box));
            Program.Assert(restored.Variables.Count == 1 && equals(restored.Variables[0].Value) &&
                           restored.Result != null && equals(restored.Result.Value),
                $"FunctionBox scalar value of type {valueType.Name} should round-trip.");
        }

        private static Action ExpressionBoundaries()
        {
            return () =>
            {
                Program.AssertThrows<ArgumentOutOfRangeException>(() => new ConstantExpression(double.NaN),
                    "Expression constants must reject NaN.");
                Program.AssertThrows<ArgumentException>(() => new VariableExpression("  "),
                    "Expression variables must reject empty names.");
                Program.Assert(new VariableExpression(" x ").Name == "x",
                    "Expression variable names should be trimmed.");
                Program.AssertThrows<ArgumentNullException>(() => new NegateExpression(null!),
                    "Expression unary nodes must reject null operands.");
                Program.AssertThrows<ArgumentNullException>(() => new BinaryExpression(BinaryOperator.Add, null!, new ConstantExpression(1.0)),
                    "Expression binary nodes must reject null operands.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    new BinaryExpression((BinaryOperator)999, new ConstantExpression(1.0), new ConstantExpression(2.0)),
                    "Expression binary nodes must reject undefined operators.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    new FunctionExpression((FunctionKind)999, new ConstantExpression(1.0)),
                    "Expression function nodes must reject undefined functions.");

                Program.Assert(ExpressionParser.Parse(".5") is ConstantExpression,
                    "A leading-decimal numeric literal should remain valid.");
                Program.Assert(ExpressionParser.Parse("1.") is ConstantExpression,
                    "A trailing-decimal numeric literal should remain valid.");
                foreach (string invalid in new[] { "1..2", "2e", "1e999", "α" })
                {
                    Program.AssertThrows<FormatException>(() => ExpressionParser.Parse(invalid),
                        $"Malformed expression '{invalid}' should be rejected.");
                }
                string tooLong = new string('1', ExpressionParser.MaximumInputLength + 1);
                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse(tooLong),
                    "Expression input length should be bounded.");
                string tooDeep = new string('(', ExpressionParser.MaximumNestingDepth + 1) + "1" +
                                 new string(')', ExpressionParser.MaximumNestingDepth + 1);
                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse(tooDeep),
                    "Expression nesting depth should be bounded.");

                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse($"sum(i,1,{int.MaxValue},i)"),
                    "Indexed expansion should reject ranges that overflow iteration bounds.");
                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse("sum(i,0,1001,i)"),
                    "Indexed expansion should enforce iteration count limits.");
                string flatChain = string.Join("+", Enumerable.Repeat("1", 5000));
                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse(flatChain),
                    "Long flat chains should be rejected by AST node/depth limits.");

                Program.Assert(SymbolicCalculus.DependsOn(new VariableExpression("x"), " x "),
                    "DependsOn should normalize variable identifiers.");
                Program.Assert((double)((ConstantExpression)Algebra.Substitute(new VariableExpression("x"), " x ", 2.0)).Value == 2.0,
                    "Substitute should normalize variable identifiers.");

                // Entry-point AST/identifier/literal caps across ExpressionParser, Math_3, Math_4, Algebra, and Fraction.Parse
                string hugeLiteral = "0." + new string('1', ExpressionParser.MaximumNumericLiteralLength + 5);
                Program.AssertThrows<FormatException>(() => ExpressionParser.Parse(hugeLiteral),
                    "Numeric literals exceeding MaximumNumericLiteralLength must be rejected.");
                string oversizedIdentifier = new string('x', ExpressionParser.MaximumIdentifierLength + 1);
                Program.AssertThrows<FormatException>(() => new VariableExpression(oversizedIdentifier),
                    "VariableExpression must reject identifiers exceeding MaximumIdentifierLength.");
                Program.AssertThrows<FormatException>(() => new CallExpression("min",
                    Enumerable.Range(0, ExpressionParser.MaximumFunctionArguments + 1).Select(i => (Expression)new ConstantExpression(i)).ToList()),
                    "CallExpression must reject argument lists exceeding MaximumFunctionArguments.");

                Expression deepManualAst = new ConstantExpression(1.0);
                for (int i = 0; i < ExpressionParser.MaximumAstDepth + 2; i++)
                {
                    deepManualAst = new NegateExpression(deepManualAst);
                }
                Program.AssertThrows<FormatException>(() => Math_3.Simplify(deepManualAst),
                    "Math_3.Simplify(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_3.Differentiate(deepManualAst, "x"),
                    "Math_3.Differentiate(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_3.Integrate(deepManualAst, "x"),
                    "Math_3.Integrate(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_3.Evaluate(deepManualAst, "x", 1.0),
                    "Math_3.Evaluate(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_4.PartialDerivative(deepManualAst, "x"),
                    "Math_4.PartialDerivative(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_4.Gradient(deepManualAst, "x", "y"),
                    "Math_4.Gradient(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_4.Evaluate(deepManualAst, new Dictionary<string, double> { ["x"] = 1.0 }),
                    "Math_4.Evaluate(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Algebra.Expand(deepManualAst),
                    "Algebra.Expand(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Algebra.Substitute(deepManualAst, "x", 1.0),
                    "Algebra.Substitute(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Algebra.Factor(deepManualAst, "x"),
                    "Algebra.Factor(Expression) must enforce AST depth limits.");
                Program.AssertThrows<FormatException>(() => Math_3.Differentiate(new VariableExpression("x"), oversizedIdentifier),
                    "Symbolic differentiation must reject oversized variable identifiers.");

                string hugeFractionComponent = new string('9', Fraction.MaximumComponentDigitLength + 10) + "/2";
                Program.Assert(!Fraction.TryParse(hugeFractionComponent, out _),
                    "Fraction.TryParse must reject component digits exceeding MaximumComponentDigitLength.");
                Program.AssertThrows<FormatException>(() => Fraction.Parse(hugeFractionComponent),
                    "Fraction.Parse must reject component digits exceeding MaximumComponentDigitLength.");

                Program.Assert(new ConstantExpression(1.2345678901234567).ToString() ==
                               1.2345678901234567.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                    "Expression constants should use round-trip invariant formatting.");
            };
        }
    }
}
