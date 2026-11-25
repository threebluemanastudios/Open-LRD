using System.Collections;
using System.Collections.ObjectModel;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using OPEN_LRD.Foundations;
using OPEN_LRD.Reasoning;
using OPEN_LRD.Skills;
using OPEN_LRD.IngestionPipeline;
using OPEN_LRD.Persistence;


namespace OPEN_LRD
{
    /// <summary>Owns the graph, runtime registries, conversations, and persistence hooks.</summary>
    internal class LRD_Core
    {
        private sealed class PresentationUpdateScope : IDisposable
        {
            private readonly LRD_Core owner;
            private int disposed;

            internal PresentationUpdateScope(LRD_Core owner) => this.owner = owner;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    owner.ResumePresentationRecalculation();
            }
        }

        /// <summary>
        /// Stages reasoning foundation boxes, nodes, and assertions for atomic two-phase commit
        /// with a single presentation recalculation pass and incremental rollback without full graph rebuilds.
        /// </summary>
        public sealed class ReasoningTransaction : IDisposable
        {
            private readonly LRD_Core owner;
            private readonly List<SenseBox> stagedSenses = new List<SenseBox>();
            private readonly List<UsageBox> stagedUsages = new List<UsageBox>();
            private readonly List<SymbolBox> stagedSymbols = new List<SymbolBox>();
            private readonly List<WordBox> stagedWords = new List<WordBox>();
            private readonly List<SentenceBox> stagedSentences = new List<SentenceBox>();
            private readonly List<GroupBox> stagedGroups = new List<GroupBox>();
            private readonly List<SourceBox> stagedSources = new List<SourceBox>();
            private readonly List<FunctionBox> stagedFunctions = new List<FunctionBox>();
            private readonly List<Node> stagedNodes = new List<Node>();
            private readonly List<NodeLink> pendingLinks = new List<NodeLink>();
            private bool completed;

            internal ReasoningTransaction(LRD_Core owner) => this.owner = owner;

            /// <summary>Stages a sense box to be promoted to trusted during transaction commit.</summary>
            public void StageSense(SenseBox senseBox)
            {
                ArgumentNullException.ThrowIfNull(senseBox);
                EnsureOpen();
                stagedSenses.Add(senseBox);
            }

            /// <summary>Stages a usage box to be promoted to trusted during transaction commit.</summary>
            public void StageUsage(UsageBox usageBox)
            {
                ArgumentNullException.ThrowIfNull(usageBox);
                EnsureOpen();
                stagedUsages.Add(usageBox);
            }

            /// <summary>Stages a symbol box to be promoted to trusted during transaction commit.</summary>
            public void StageSymbol(SymbolBox symbolBox)
            {
                ArgumentNullException.ThrowIfNull(symbolBox);
                EnsureOpen();
                stagedSymbols.Add(symbolBox);
            }

            /// <summary>Stages a word box to be promoted to trusted during transaction commit.</summary>
            public void StageWord(WordBox wordBox)
            {
                ArgumentNullException.ThrowIfNull(wordBox);
                EnsureOpen();
                stagedWords.Add(wordBox);
            }

            /// <summary>Stages a sentence box to be promoted to trusted during transaction commit.</summary>
            public void StageSentence(SentenceBox sentenceBox)
            {
                ArgumentNullException.ThrowIfNull(sentenceBox);
                EnsureOpen();
                stagedSentences.Add(sentenceBox);
            }

            /// <summary>Stages a group box to be promoted to trusted during transaction commit.</summary>
            public void StageGroup(GroupBox groupBox)
            {
                ArgumentNullException.ThrowIfNull(groupBox);
                EnsureOpen();
                stagedGroups.Add(groupBox);
            }

            /// <summary>Stages a source box to be promoted to trusted during transaction commit.</summary>
            public void StageSource(SourceBox sourceBox)
            {
                ArgumentNullException.ThrowIfNull(sourceBox);
                EnsureOpen();
                stagedSources.Add(sourceBox);
            }

            /// <summary>Stages a dispatch-verified deterministic function box to be promoted to trusted during commit.</summary>
            public void StageFunction(FunctionBox functionBox)
            {
                ArgumentNullException.ThrowIfNull(functionBox);
                EnsureOpen();
                stagedFunctions.Add(functionBox);
            }

            /// <summary>Stages a node to be promoted to trusted during transaction commit.</summary>
            public void StageNode(Node node)
            {
                ArgumentNullException.ThrowIfNull(node);
                EnsureOpen();
                stagedNodes.Add(node);
            }

            /// <summary>Stages one assertion without mutating trusted graph state until commit.</summary>
            public void AddReasoningLink(Guid sourceNodeId, Guid targetNodeId, NodeRelationType relationType,
                double confidence = 1.0, string? label = null, string? author = null,
                Guid? sourceTargetId = null, Guid? evidenceTargetId = null,
                DateTimeOffset? validFrom = null, DateTimeOffset? validUntil = null,
                Guid? sourceGroupId = null)
            {
                EnsureOpen();
                NodeLinkProvenance provenance = NodeLinkProvenance.CreateReasoning(sourceNodeId, targetNodeId,
                    relationType, label, confidence, author, sourceTargetId, evidenceTargetId, validFrom, validUntil, sourceGroupId);
                pendingLinks.Add(new NodeLink(sourceNodeId, targetNodeId, relationType, confidence, label, provenance));
            }

            /// <summary>Atomically applies all staged boxes, nodes, and assertions or restores pre-commit graph state.</summary>
            public void Commit()
            {
                EnsureOpen();
                owner.CommitReasoningTransaction(
                    stagedSenses, stagedUsages, stagedSymbols, stagedWords, stagedSentences,
                    stagedGroups, stagedSources, stagedFunctions, stagedNodes, pendingLinks);
                completed = true;
                ClearStaged();
            }

            /// <summary>Disposes the transaction and discards any uncommitted staged items.</summary>
            public void Dispose()
            {
                completed = true;
                ClearStaged();
            }

            private void ClearStaged()
            {
                stagedSenses.Clear();
                stagedUsages.Clear();
                stagedSymbols.Clear();
                stagedWords.Clear();
                stagedSentences.Clear();
                stagedGroups.Clear();
                stagedSources.Clear();
                stagedFunctions.Clear();
                stagedNodes.Clear();
                pendingLinks.Clear();
            }

            private void EnsureOpen()
            {
                if (completed) throw new InvalidOperationException("The reasoning transaction is already completed.");
            }
        }

        /// <summary>Stages reasoning assertions, boxes, and nodes for atomic, single-layout batch commit.</summary>
        public sealed class ReasoningWriteBatch : IDisposable
        {
            private readonly ReasoningTransaction inner;

            internal ReasoningWriteBatch(LRD_Core owner) => inner = new ReasoningTransaction(owner);

            /// <summary>Stages a sense box to be promoted to trusted during batch commit.</summary>
            public void StageSense(SenseBox senseBox) => inner.StageSense(senseBox);

            /// <summary>Stages a usage box to be promoted to trusted during batch commit.</summary>
            public void StageUsage(UsageBox usageBox) => inner.StageUsage(usageBox);

            /// <summary>Stages a symbol box to be promoted to trusted during batch commit.</summary>
            public void StageSymbol(SymbolBox symbolBox) => inner.StageSymbol(symbolBox);

            /// <summary>Stages a word box to be promoted to trusted during batch commit.</summary>
            public void StageWord(WordBox wordBox) => inner.StageWord(wordBox);

            /// <summary>Stages a sentence box to be promoted to trusted during batch commit.</summary>
            public void StageSentence(SentenceBox sentenceBox) => inner.StageSentence(sentenceBox);

            /// <summary>Stages a group box to be promoted to trusted during batch commit.</summary>
            public void StageGroup(GroupBox groupBox) => inner.StageGroup(groupBox);

            /// <summary>Stages a source box to be promoted to trusted during batch commit.</summary>
            public void StageSource(SourceBox sourceBox) => inner.StageSource(sourceBox);

            /// <summary>Stages a dispatch-verified deterministic function box to be promoted to trusted during batch commit.</summary>
            public void StageFunction(FunctionBox functionBox) => inner.StageFunction(functionBox);

            /// <summary>Stages a node to be promoted to trusted during batch commit.</summary>
            public void StageNode(Node node) => inner.StageNode(node);

            /// <summary>Stages one assertion without mutating trusted graph state until commit.</summary>
            public void AddReasoningLink(Guid sourceNodeId, Guid targetNodeId, NodeRelationType relationType,
                double confidence = 1.0, string? label = null, string? author = null,
                Guid? sourceTargetId = null, Guid? evidenceTargetId = null,
                DateTimeOffset? validFrom = null, DateTimeOffset? validUntil = null,
                Guid? sourceGroupId = null) =>
                inner.AddReasoningLink(sourceNodeId, targetNodeId, relationType, confidence, label, author,
                    sourceTargetId, evidenceTargetId, validFrom, validUntil, sourceGroupId);

            /// <summary>Atomically applies all staged items or restores the pre-commit graph state.</summary>
            public void Commit() => inner.Commit();

            /// <summary>Disposes the batch and discards any uncommitted staged items.</summary>
            public void Dispose() => inner.Dispose();
        }

        private sealed class OrderedGuidIndexList : IEnumerable<Guid>
        {
            private readonly List<Guid> ordered = new List<Guid>();
            private readonly HashSet<Guid> membership = new HashSet<Guid>();

            internal int Count => ordered.Count;
            internal Guid this[int index] => ordered[index];

            internal bool Add(Guid value)
            {
                if (!membership.Add(value))
                {
                    return false;
                }
                ordered.Add(value);
                return true;
            }

            internal bool Remove(Guid value)
            {
                if (!membership.Remove(value))
                {
                    return false;
                }
                ordered.Remove(value);
                return true;
            }

            internal List<Guid> Snapshot() => new List<Guid>(ordered);
            public IEnumerator<Guid> GetEnumerator() => ordered.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        
        private static readonly LRD_Core _instance = new LRD_Core();
        /// <summary>Stores Initialization Status.</summary>
        private bool _initialized;
        public bool initialized => GraphSynchronization.Execute(() => _initialized);
        /// <summary>Stores SourceBox Dictionary.</summary>
        private readonly Dictionary<Guid, SourceBox> SourceDictionary = new Dictionary<Guid, SourceBox>();
        /// <summary>Stores GroupBox Dictionary.</summary>
        private readonly Dictionary<Guid, ConversationText> TextConversationDictionary = new Dictionary<Guid, ConversationText>();
        /// <summary>Stores ConversationSymbolic Dictionary.</summary>
        private readonly Dictionary<Guid, ConversationSymbolic> SymbolicConversationDictionary = new Dictionary<Guid, ConversationSymbolic>();
        /// <summary>Stores Trusted Symbols Dictionary.</summary>
        private readonly Dictionary<Guid, SymbolBox> TrustedSymbolDictionary = new Dictionary<Guid, SymbolBox>();
        /// <summary>Stores Trusted Usage Boxes Dictionary.</summary>
        private readonly Dictionary<Guid, UsageBox> TrustedUsageDictionary = new Dictionary<Guid, UsageBox>();
        /// <summary>Stores Trusted Sense Boxes Dictionary.</summary>
        private readonly Dictionary<Guid, SenseBox> TrustedSenseDictionary = new Dictionary<Guid, SenseBox>();
        /// <summary>Stores Trusted Words Dictionary.</summary>
        private readonly Dictionary<Guid, WordBox> TrustedWordDictionary = new Dictionary<Guid, WordBox>();
        /// <summary>Stores Trusted Sentence / Phrase Dictionary.</summary>
        private readonly Dictionary<Guid, SentenceBox> TrustedSentenceDictionary = new Dictionary<Guid, SentenceBox>();
        /// <summary>Stores Untrusted Symbols Dictionary.</summary>
        private readonly Dictionary<Guid, SymbolBox> UntrustedSymbolDictionary = new Dictionary<Guid, SymbolBox>();
        /// <summary>Stores Untrusted Words Dictionary.</summary>
        private readonly Dictionary<Guid, WordBox> UntrustedWordDictionary = new Dictionary<Guid, WordBox>();
        /// <summary>Stores Untrusted Sentence / Phrase Dictionary.</summary>
        private readonly Dictionary<Guid, SentenceBox> UntrustedSentenceDictionary = new Dictionary<Guid, SentenceBox>();
        private readonly Dictionary<Guid, FunctionBox> UntrustedFunctionDictionary = new Dictionary<Guid, FunctionBox>();
        private readonly Dictionary<Guid, GroupBox> TrustedGroupDictionary = new Dictionary<Guid, GroupBox>();
        private readonly Dictionary<Guid, FunctionBox> TrustedFunctionDictionary = new Dictionary<Guid, FunctionBox>();
        private readonly Dictionary<WordIdentityKey, Guid> TrustedWordIdentityIndex = new Dictionary<WordIdentityKey, Guid>();
        private readonly Dictionary<SentenceIdentityKey, Guid> TrustedSentenceIdentityIndex = new Dictionary<SentenceIdentityKey, Guid>();
        /// <summary>Maps untrusted sentence identity keys to their staged sentence identifiers. Mirrors <see cref="TrustedSentenceIdentityIndex"/>.</summary>
        private readonly Dictionary<SentenceIdentityKey, Guid> UntrustedSentenceIdentityIndex = new Dictionary<SentenceIdentityKey, Guid>();
        /// <summary>Records the identity key each indexed untrusted sentence was registered under, so removal does not depend on the box's current state.</summary>
        private readonly Dictionary<Guid, SentenceIdentityKey> UntrustedSentenceIndexedKeyById = new Dictionary<Guid, SentenceIdentityKey>();
        private readonly Dictionary<SymbolIdentityKey, Guid> TrustedSymbolIdentityIndex = new Dictionary<SymbolIdentityKey, Guid>();
        private readonly Dictionary<char, OrderedGuidIndexList> TrustedSymbolCharIndex = new Dictionary<char, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> SymbolsBySenseIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> WordsBySenseIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> SentencesBySenseIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> WordsByUsageIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> WordsBySymbolIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> SentencesByWordIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> GroupsBySentenceIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, OrderedGuidIndexList> SourcesByGroupIndex = new Dictionary<Guid, OrderedGuidIndexList>();
        private readonly Dictionary<Guid, Node> TrustedNodeDictionary = new Dictionary<Guid, Node>();
        private readonly Dictionary<Guid, Node> UntrustedNodeDictionary = new Dictionary<Guid, Node>();
        private readonly Dictionary<UInt128, NodeRepresentation> NodePresentation = new Dictionary<UInt128, NodeRepresentation>();
        private readonly Dictionary<Guid, UInt128> NodePresentationKeyByNodeId = new Dictionary<Guid, UInt128>();
        private UInt128 _nextNodePresentationId;
        private bool _nodePresentationIdSpaceExhausted;
        private NodeLayoutOptions _nodeLayoutOptions = new NodeLayoutOptions();
        private int _nodePresentationRecalculationSuspendCount;
        private long _graphEpoch;

        /// <summary>Gets the current monotonic trusted graph epoch counter.</summary>
        public long CurrentGraphEpoch => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return _graphEpoch; });
        /// <summary>Well-known trusted sense used when a caller does not supply one. Registered by <see cref="InitializeCore"/>.</summary>
        public static readonly Guid DefaultSenseId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000301");
        /// <summary>Well-known trusted usage used for words created by text conversion. Registered by <see cref="InitializeCore"/>.</summary>
        public static readonly Guid DefaultUsageId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000302");
        private static readonly Guid DefaultDictionarySourceId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000001");
        private static readonly Guid DefaultUserSourceId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000002");
        private static readonly Guid DefaultImportSourceId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000003");
        private static readonly Guid DefaultUnknownSourceId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000004");
        private static readonly Guid DefaultGcideGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000101");
        private static readonly Guid DefaultUserTextBoxGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000102");
        private static readonly Guid DefaultUserFileLoadGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000103");
        private static readonly Guid DefaultImportFileLoadGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000104");
        private static readonly Guid DefaultUnknownTextBoxGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000105");
        private static readonly Guid DefaultUnknownFileLoadGroupId = Guid.Parse("f4c4b814-5eed-4d1c-8ac3-000000000106");
        private static readonly HashSet<NodeTargetKind> SplitNodePresentationKinds = new HashSet<NodeTargetKind>
        {
            NodeTargetKind.Symbol,
            NodeTargetKind.Word,
            NodeTargetKind.Sentence,
            NodeTargetKind.Function
        };
        private readonly Dictionary<NodeIdentityKey, Guid> TrustedNodeByIdentityIndex = new Dictionary<NodeIdentityKey, Guid>();
        private readonly Dictionary<NodeIdentityKey, Guid> UntrustedNodeByIdentityIndex = new Dictionary<NodeIdentityKey, Guid>();
        // These target-only indexes point exclusively to the canonical null-label node.
        private readonly Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> TrustedNodeByTargetIndex = new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
        private readonly Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> UntrustedNodeByTargetIndex = new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
        private readonly Dictionary<Guid, List<NodeLink>> TrustedOutgoingLinks = new Dictionary<Guid, List<NodeLink>>();
        private readonly Dictionary<Guid, List<NodeLink>> TrustedIncomingLinks = new Dictionary<Guid, List<NodeLink>>();
        private readonly HashSet<NodeLink> TrustedLinkKeys = new HashSet<NodeLink>();
        private readonly Dictionary<Guid, List<NodeLink>> LinksByAssertionId = new Dictionary<Guid, List<NodeLink>>();
        private readonly Dictionary<Guid, List<NodeLink>> LinksBySourceTargetId = new Dictionary<Guid, List<NodeLink>>();
        private readonly Dictionary<Guid, List<NodeLink>> LinksByEvidenceTargetId = new Dictionary<Guid, List<NodeLink>>();
        private Graph<Guid> TrustedSkillsGraph = new Graph<Guid>(true);
        private PartitionedBinaryStore? _diskStore;
        private string? _diskStorePath;
        private bool _diskPagedReadOnly;
        private bool _legacyDiskStoreReadOnly;
        private long _pagedCacheHits;
        private long _pagedCacheMisses;
        private string? _lastDiskStoreValidationError;
        private DiskValidationProvenanceSummary? _lastDiskStoreValidationProvenanceSummary;
        private int _maxPagedObjects = 1024;
        private readonly Dictionary<(DiskPartition Partition, Guid Id), object> _pagedObjectCache = new Dictionary<(DiskPartition Partition, Guid Id), object>();
        private readonly Dictionary<(DiskPartition Partition, Guid Id), LinkedListNode<(DiskPartition Partition, Guid Id)>> _pagedObjectLruNodes = new Dictionary<(DiskPartition Partition, Guid Id), LinkedListNode<(DiskPartition Partition, Guid Id)>>();
        private readonly LinkedList<(DiskPartition Partition, Guid Id)> _pagedObjectLru = new LinkedList<(DiskPartition Partition, Guid Id)>();


        /// <summary>Initializes the singleton core type.</summary>
        static LRD_Core() { }

        /// <summary>Prevents external instantiation of the singleton core.</summary>
        private LRD_Core() { }

        public static LRD_Core Instance => _instance;

        /// <summary>Initializes the singleton graph with its default source catalog.</summary>
        public void InitializeCore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_initialized)
                {
                    return;
                }
                ClearAllDictionaries();
                _initialized = true;
                try
                {
                    InitializeDefaultSources();
                }
                catch
                {
                    ClearAllDictionaries();
                    throw;
                }
            });
        }

        private void InitializeDefaultSources()
        {
            if (!AddTrustedSense(new SenseBox(DefaultSenseId, true)))
            {
                throw new InvalidOperationException("Failed to initialize default sense.");
            }
            if (!AddTrustedUsage(new UsageBox(DefaultUsageId, true)))
            {
                throw new InvalidOperationException("Failed to initialize default usage.");
            }

            GroupBox[] defaultGroups =
            {
                new GroupBox(DefaultGcideGroupId, "GCIDE", Array.Empty<Guid>(), true),
                new GroupBox(DefaultUserTextBoxGroupId, "TextBox", Array.Empty<Guid>(), true),
                new GroupBox(DefaultUserFileLoadGroupId, "FileLoad", Array.Empty<Guid>(), true),
                new GroupBox(DefaultImportFileLoadGroupId, "FileLoad", Array.Empty<Guid>(), true),
                new GroupBox(DefaultUnknownTextBoxGroupId, "TextBox", Array.Empty<Guid>(), true),
                new GroupBox(DefaultUnknownFileLoadGroupId, "FileLoad", Array.Empty<Guid>(), true)
            };
            foreach (GroupBox group in defaultGroups)
            {
                if (!AddTrustedGroup(group))
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize default group '{group.GetGroupingName()}'.");
                }
            }

            SourceBox[] defaultSources =
            {
                new SourceBox(DefaultDictionarySourceId, "Dictionary",
                    new[] { DefaultGcideGroupId }, true),
                new SourceBox(DefaultUserSourceId, "User",
                    new[] { DefaultUserTextBoxGroupId, DefaultUserFileLoadGroupId }, true),
                new SourceBox(DefaultImportSourceId, "Import",
                    new[] { DefaultImportFileLoadGroupId }, true),
                new SourceBox(DefaultUnknownSourceId, "Unknown",
                    new[] { DefaultUnknownTextBoxGroupId, DefaultUnknownFileLoadGroupId }, true)
            };
            foreach (SourceBox source in defaultSources)
            {
                if (!AddTrustedSource(source))
                {
                    throw new InvalidOperationException(
                        $"Failed to initialize default source '{source.GetSourceName()}'.");
                }
            }
        }

        /// <summary>
        ///  Returns the current core instance
        /// </summary>
        public LRD_Core GetInstance()
        {
            return _instance;
        }

        /// <summary>
        /// Resets the core to stop all current actions and clear all stored information. ! DANGER EXIT BUTTON ! 
        /// </summary>
        public void ResetCore()
        {
            GraphSynchronization.Execute(() =>
            {
                ClearAllDictionaries();
                _initialized = false;
            });
        }

        /// <summary>Gets a trusted symbol box by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="symbolId">Symbol identifier to resolve.</param>
        /// <returns>The matching <see cref="SymbolBox"/>, or <see langword="null"/> if not found.</returns>
        public SymbolBox? GetTrustedSymbolBox(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return TrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbol)
                    ? symbol
                    : GetPagedObject(DiskPartition.Symbols, symbolId,
                        payload => FoundationBinarySerializer.DeserializeSymbol(payload, _diskStore?.IsLegacyFormat ?? false), box => { });
            });
        }

        /// <summary>Gets a trusted usage box by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="usageId">Usage identifier to resolve.</param>
        /// <returns>The matching <see cref="UsageBox"/>, or <see langword="null"/> if not found.</returns>
        public UsageBox? GetTrustedUsageBox(Guid usageId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedUsageDictionary.TryGetValue(usageId, out UsageBox? usage)
                    ? usage
                    : GetPagedObject(DiskPartition.Usages, usageId, FoundationBinarySerializer.DeserializeUsage, box => box.MarkReadOnly());
            });
        }

        /// <summary>Gets a trusted sense box by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="senseId">Sense identifier to resolve.</param>
        /// <returns>The matching <see cref="SenseBox"/>, or <see langword="null"/> if not found.</returns>
        public SenseBox? GetTrustedSenseBox(Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedSenseDictionary.TryGetValue(senseId, out SenseBox? sense)
                    ? sense
                    : GetPagedObject(DiskPartition.Senses, senseId, FoundationBinarySerializer.DeserializeSense, box => box.MarkReadOnly());
            });
        }

        /// <summary>Gets a trusted word box by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="wordId">Word identifier to resolve.</param>
        /// <returns>The matching <see cref="WordBox"/>, or <see langword="null"/> if not found.</returns>
        public WordBox? GetTrustedWordBox(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox)
                    ? wordBox
                    : GetPagedObject(DiskPartition.Words, wordId, FoundationBinarySerializer.DeserializeWord, box => { });
            });
        }

        /// <summary>Gets a trusted sentence box by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="sentenceId">Sentence identifier to resolve.</param>
        /// <returns>The matching <see cref="SentenceBox"/>, or <see langword="null"/> if not found.</returns>
        public SentenceBox? GetTrustedSentenceBox(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox)
                    ? sentenceBox
                    : GetPagedObject(DiskPartition.Sentences, sentenceId, FoundationBinarySerializer.DeserializeSentence, box => { });
            });
        }

        /// <summary>Gets an untrusted staged symbol box by identifier.</summary>
        /// <param name="symbolId">Symbol identifier to resolve.</param>
        /// <returns>The matching untrusted <see cref="SymbolBox"/>, or <see langword="null"/> if not staged.</returns>
        public SymbolBox? GetUntrustedSymbolBox(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ? symbolBox : null;
            });
        }

        /// <summary>
        /// Returns a trusted or untrusted word by identifier, preferring trusted state.
        /// </summary>
        /// <param name="wordId">Word identifier to resolve.</param>
        /// <returns>The matching <see cref="WordBox"/>, or <see langword="null"/> if not found.</returns>
        public WordBox? GetWordByGuid(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (wordId == Guid.Empty)
                {
                    return null;
                }
                return GetTrustedWordBox(wordId) ?? (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ? wordBox : null);
            });
        }

        /// <summary>Gets an untrusted staged word box by identifier.</summary>
        /// <param name="wordId">Word identifier to resolve.</param>
        /// <returns>The matching untrusted <see cref="WordBox"/>, or <see langword="null"/> if not staged.</returns>
        public WordBox? GetUntrustedWordBox(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ? wordBox : null;
            });
        }

        /// <summary>Gets an untrusted staged sentence box by identifier.</summary>
        /// <param name="sentenceId">Sentence identifier to resolve.</param>
        /// <returns>The matching untrusted <see cref="SentenceBox"/>, or <see langword="null"/> if not staged.</returns>
        public SentenceBox? GetUntrustedSentenceBox(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ? sentenceBox : null;
            });
        }

        /// <summary>Gets a trusted source box by identifier across in-memory and disk-paged modes.</summary>
        public SourceBox? GetTrustedSourceBox(Guid sourceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                SourceDictionary.TryGetValue(sourceId, out SourceBox? sourceBox)
                    ? sourceBox
                    : GetPagedObject(DiskPartition.Sources, sourceId, FoundationBinarySerializer.DeserializeSource, box => box.MarkReadOnly());
            });
        }

        /// <summary>Gets a trusted group box by identifier across in-memory and disk-paged modes.</summary>
        public GroupBox? GetTrustedGroupBox(Guid groupId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedGroupDictionary.TryGetValue(groupId, out GroupBox? groupBox)
                    ? groupBox
                    : GetPagedObject(DiskPartition.Groups, groupId, FoundationBinarySerializer.DeserializeGroup, box => box.MarkReadOnly());
            });
        }

        /// <summary>Gets a trusted function box by identifier across in-memory and disk-paged modes.</summary>
        public FunctionBox? GetTrustedFunctionBox(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                    TrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? functionBox)
                    ? functionBox
                    : GetPagedObject(DiskPartition.Functions, functionId,
                        payload => FoundationBinarySerializer.DeserializeFunction(payload, _diskStore?.IsLegacyFormat ?? false),
                        box => box.MarkReadOnly());
            });
        }

        /// <summary>Gets a read-only dispatch-provenance audit for a trusted or staged function box.</summary>
        /// <param name="functionId">The function-box identity to inspect.</param>
        /// <returns>An immutable audit snapshot, or <see langword="null"/> if the function is not registered.</returns>
        public FunctionDispatchAudit? GetFunctionDispatchAudit(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                FunctionBox? functionBox = UntrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? staged)
                    ? staged
                    : TrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? trusted)
                        ? trusted
                        : GetPagedObject(DiskPartition.Functions, functionId,
                            payload => FoundationBinarySerializer.DeserializeFunction(payload, _diskStore?.IsLegacyFormat ?? false),
                            box => box.MarkReadOnly());
                if (functionBox == null)
                {
                    return null;
                }

                return new FunctionDispatchAudit(
                    functionId,
                    functionBox.IsTrusted,
                    functionBox.IsResultDispatchVerified,
                    functionBox.DispatchProvenance,
                    functionBox.DispatchProvenanceHistory,
                    functionBox.ResultVerificationState);
            });
        }

        /// <summary>Gets an untrusted staged function box by identifier.</summary>
        public FunctionBox? GetUntrustedFunctionBox(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? functionBox) ? functionBox : null;
            });
        }

        /// <summary>Stages a function box in the untrusted registry and ensures its canonical untrusted node exists.</summary>
        public bool SetUntrustedFunction(FunctionBox functionBox)
        {
            ArgumentNullException.ThrowIfNull(functionBox);
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                Guid functionId = functionBox.GetFunctionID();
                if (TrustedFunctionDictionary.ContainsKey(functionId))
                {
                    return false;
                }
                if (UntrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? existing) &&
                    !ReferenceEquals(existing, functionBox))
                {
                    return false;
                }
                functionBox.MarkWritable();
                UntrustedFunctionDictionary[functionId] = functionBox;
                if (!EnsureUntrustedNode(NodeTargetKind.Function, functionId))
                    RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>Removes a staged untrusted function box and cleans up any canonical or labeled untrusted nodes targeting it.</summary>
        public bool RemoveUntrustedFunction(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!UntrustedFunctionDictionary.Remove(functionId))
                {
                    return false;
                }
                RemoveUntrustedNodeForTarget(NodeTargetKind.Function, functionId);
                return true;
            });
        }

        /// <summary>Promotes a staged untrusted function box to trusted state after validating deterministic dispatch provenance.</summary>
        public bool ConvertUntrustedFunctionToTrusted(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!UntrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? functionBox) ||
                    !AddTrustedFunction(functionBox))
                {
                    return false;
                }
                UntrustedFunctionDictionary.Remove(functionId);
                return true;
            });
        }

        /// <summary>Gets a registered text conversation by identifier.</summary>
        public ConversationText? GetTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return TextConversationDictionary.TryGetValue(conversationId, out ConversationText? conversation) ? conversation : null;
            });
        }

        /// <summary>Gets a registered symbolic conversation by identifier.</summary>
        public ConversationSymbolic? GetSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return SymbolicConversationDictionary.TryGetValue(conversationId, out ConversationSymbolic? conversation) ? conversation : null;
            });
        }

        private Node? GetTrustedNodeInternal(Guid nodeId)
        {
            return TrustedNodeDictionary.TryGetValue(nodeId, out Node? node)
                ? node
                : GetPagedObject(DiskPartition.Nodes, nodeId, payload => FoundationBinarySerializer.DeserializeNode(payload, true), box => { });
        }

        private IEnumerable<Node> EnumerateTrustedNodesInternal()
        {
            if (!_diskPagedReadOnly)
            {
                return TrustedNodeDictionary.Values;
            }

            return TrustedNodeByIdentityIndex.Values
                .Distinct()
                .Select(GetTrustedNodeInternal)
                .Where(node => node != null)
                .Cast<Node>();
        }

        /// <summary>Gets a trusted node by identifier across in-memory and disk-paged modes.</summary>
        public Node? GetTrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return GetTrustedNodeInternal(nodeId);
            });
        }

        /// <summary>Gets an untrusted staged node by identifier.</summary>
        public Node? GetUntrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
            });
        }

        /// <summary>Gets the canonical null-label trusted node for a foundation target across in-memory and disk-paged modes.</summary>
        public Node? GetTrustedNodeByTarget(NodeTargetKind kind, Guid targetId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                _ = NodeIdentityKey.Create(kind, targetId, null);
                if (!TrustedNodeByTargetIndex.TryGetValue((kind, targetId), out Guid nodeId))
                {
                    return null;
                }
                return GetTrustedNode(nodeId);
            });
        }

        /// <summary>Gets a trusted node by target kind, target identifier, and optional role label across in-memory and disk-paged modes.</summary>
        public Node? GetTrustedNodeByTarget(NodeTargetKind kind, Guid targetId, string? label)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                NodeIdentityKey key = NodeIdentityKey.Create(kind, targetId, label);
                return TrustedNodeByIdentityIndex.TryGetValue(key, out Guid nodeId) ? GetTrustedNode(nodeId) : null;
            });
        }

        /// <summary>Returns an immutable snapshot of all canonical and labeled trusted nodes wrapping a foundation target.</summary>
        public IReadOnlyList<Node> GetTrustedNodesByTarget(NodeTargetKind kind, Guid targetId, int? limit = null, int offset = 0)
        {
            return GraphSynchronization.Execute<IReadOnlyList<Node>>(() =>
            {
                EnsureCoreInitialized();
                _ = NodeIdentityKey.Create(kind, targetId, null);
                ValidatePaging(limit, offset);
                IEnumerable<Node> query = TrustedNodeByIdentityIndex
                    .Where(entry => entry.Key.TargetKind == kind && entry.Key.TargetId == targetId)
                    .OrderBy(entry => entry.Key.Label, StringComparer.Ordinal)
                    .Select(entry => GetTrustedNode(entry.Value))
                    .Where(node => node != null)
                    .Cast<Node>();
                if (offset > 0) query = query.Skip(offset);
                if (limit.HasValue) query = query.Take(limit.Value);
                return query.ToList().AsReadOnly();
            });
        }

        /// <summary>Gets the canonical null-label untrusted node for a staged foundation target.</summary>
        public Node? GetUntrustedNodeByTarget(NodeTargetKind kind, Guid targetId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                _ = NodeIdentityKey.Create(kind, targetId, null);
                if (!UntrustedNodeByTargetIndex.TryGetValue((kind, targetId), out Guid nodeId))
                {
                    return null;
                }
                return UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
            });
        }

        /// <summary>Gets an untrusted staged node by target kind, target identifier, and optional role label.</summary>
        public Node? GetUntrustedNodeByTarget(NodeTargetKind kind, Guid targetId, string? label)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                NodeIdentityKey key = NodeIdentityKey.Create(kind, targetId, label);
                return UntrustedNodeByIdentityIndex.TryGetValue(key, out Guid nodeId) &&
                       UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
            });
        }

        /// <summary>Returns a deterministically ordered snapshot of trusted node identifiers with optional kind filtering and paging.</summary>
        public List<Guid> GetTrustedNodeIds(NodeTargetKind? kindFilter = null, int? limit = null, int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                ValidatePaging(limit, offset);
                IEnumerable<Guid> query = TrustedNodeByIdentityIndex
                    .Where(entry => !kindFilter.HasValue || entry.Key.TargetKind == kindFilter.Value)
                    .OrderBy(entry => entry.Key.TargetKind)
                    .ThenBy(entry => entry.Key.TargetId)
                    .ThenBy(entry => entry.Key.Label, StringComparer.Ordinal)
                    .Select(entry => entry.Value);
                if (offset > 0) query = query.Skip(offset);
                if (limit.HasValue) query = query.Take(limit.Value);
                return query.ToList();
            });
        }

        /// <summary>Returns a deterministically ordered snapshot of trusted nodes across in-memory and disk-paged modes.</summary>
        public List<Node> GetTrustedNodes(NodeTargetKind? kindFilter = null, int? limit = null, int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                ValidatePaging(limit, offset);
                return GetTrustedNodeIds(kindFilter, limit, offset)
                    .Select(GetTrustedNode)
                    .Where(node => node != null)
                    .Cast<Node>()
                    .ToList();
            });
        }

        /// <summary>Returns a deterministically ordered snapshot of untrusted staged node identifiers.</summary>
        public List<Guid> GetUntrustedNodeIds(NodeTargetKind? kindFilter = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                return UntrustedNodeDictionary.Values
                .Where(node => !kindFilter.HasValue || node.GetTargetKind() == kindFilter.Value)
                .OrderBy(node => node.GetTargetKind())
                .ThenBy(node => node.GetTargetID())
                .ThenBy(node => node.GetLabel(), StringComparer.Ordinal)
                .Select(node => node.GetNodeID())
                .ToList();
            });
        }

        /// <summary>Returns a deterministically ordered snapshot of active trusted links with optional relation, origin, author, confidence, and paging filters.</summary>
        public List<NodeLink> GetAllTrustedLinks(
            NodeRelationType? relationFilter = null,
            NodeLinkOrigin? originFilter = null,
            string? authorFilter = null,
            double? minimumConfidence = null,
            DateTimeOffset? activeAt = null,
            int? limit = null,
            int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                ValidateLinkQuery(relationFilter, originFilter, minimumConfidence);
                ValidatePaging(limit, offset);
                string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
                IEnumerable<NodeLink> query = TrustedOutgoingLinks.Values
                    .SelectMany(links => links)
                    .Where(link => MatchesLinkQuery(link, relationFilter, originFilter, normalizedAuthor, minimumConfidence, activeAt ?? DateTimeOffset.UtcNow))
                    .OrderBy(link => link.Provenance.AssertionId)
                    .ThenBy(link => link.SourceNodeId)
                    .ThenBy(link => link.TargetNodeId);
                if (offset > 0) query = query.Skip(offset);
                if (limit.HasValue) query = query.Take(limit.Value);
                return query.ToList();
            });
        }

        /// <summary>
        /// Aggregates active assertions between two endpoints using noisy-OR after collapsing dependent source groups.
        /// Assertions sharing the same <see cref="NodeLinkProvenance.SourceGroupId"/>, or (when absent) the same
        /// <c>(Origin, Author, SourceTargetId)</c> provenance chain, are treated as dependent evidence from the same
        /// inference source and collapsed by maximum confidence before independent groups are combined via noisy-OR.
        /// </summary>
        /// <param name="sourceNodeId">Trusted source node identifier.</param>
        /// <param name="targetNodeId">Trusted target node identifier.</param>
        /// <param name="relationType">Relation type shared by the assertions.</param>
        /// <param name="activeAt">Instant for inclusive validity filtering; defaults to current UTC time.</param>
        /// <returns>The aggregated confidence in [0, 1], or 0 when no active assertions exist.</returns>
        public double GetAggregatedLinkConfidence(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            DateTimeOffset? activeAt = null)
        {
            if (sourceNodeId == Guid.Empty)
                throw new ArgumentException("Source node identifiers cannot be Guid.Empty.", nameof(sourceNodeId));
            if (targetNodeId == Guid.Empty)
                throw new ArgumentException("Target node identifiers cannot be Guid.Empty.", nameof(targetNodeId));
            if (!Enum.IsDefined(relationType))
                throw new ArgumentOutOfRangeException(nameof(relationType));
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                DateTimeOffset instant = activeAt ?? DateTimeOffset.UtcNow;
                IEnumerable<NodeLink> links = TrustedOutgoingLinks.TryGetValue(sourceNodeId, out List<NodeLink>? outgoing)
                    ? outgoing.Where(link => link.TargetNodeId == targetNodeId && link.RelationType == relationType &&
                        link.Provenance.IsActiveAt(instant))
                    : Enumerable.Empty<NodeLink>();
                Dictionary<string, double> independentConfidences = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (NodeLink link in links)
                {
                    double confidence = link.Provenance.Confidence ?? link.Weight;
                    if (!double.IsFinite(confidence)) confidence = 0.0;
                    confidence = Math.Clamp(confidence, 0.0, 1.0);
                    string groupKey = GetNoisyOrSourceGroupKey(link.Provenance);
                    if (independentConfidences.TryGetValue(groupKey, out double existing))
                        independentConfidences[groupKey] = Math.Max(existing, confidence);
                    else
                        independentConfidences[groupKey] = confidence;
                }
                double independenceProduct = 1.0;
                foreach (double confidence in independentConfidences.Values)
                    independenceProduct *= 1.0 - confidence;
                return 1.0 - independenceProduct;
            });
        }

        private static string GetNoisyOrSourceGroupKey(NodeLinkProvenance provenance)
        {
            if (provenance.SourceGroupId.HasValue)
            {
                return $"group:{provenance.SourceGroupId.Value:N}";
            }
            if (!string.IsNullOrWhiteSpace(provenance.Author) && provenance.SourceTargetId.HasValue)
            {
                return $"chain:{(int)provenance.Origin}:{provenance.Author.Trim().ToLowerInvariant()}:{provenance.SourceTargetId.Value:N}";
            }
            return $"assertion:{provenance.AssertionId:N}";
        }

        /// <summary>Returns the count of registered trusted nodes across in-memory and disk-paged modes.</summary>
        public int GetTrustedNodeCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return TrustedNodeByIdentityIndex.Count; });

        /// <summary>Returns the count of staged untrusted nodes.</summary>
        public int GetUntrustedNodeCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return UntrustedNodeDictionary.Count; });

        /// <summary>Returns the count of registered trusted links.</summary>
        public int GetTrustedLinkCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return TrustedLinkKeys.Count; });

        /// <summary>Gets a node representation by its presentation identifier.</summary>
        public NodeRepresentation? GetNodeRepresentation(UInt128 presentationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return NodePresentation.TryGetValue(presentationId, out NodeRepresentation? representation)
                    ? representation
                    : null;
            });
        }

        /// <summary>Gets a node representation by its associated node identifier.</summary>
        public NodeRepresentation? GetNodeRepresentationByNodeId(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 presentationId)
                    && NodePresentation.TryGetValue(presentationId, out NodeRepresentation? representation)
                    ? representation
                    : null;
            });
        }

        /// <summary>Gets the sequential presentation identifier assigned to a registered node.</summary>
        public UInt128? GetNodePresentationId(Guid nodeId)
        {
            return GraphSynchronization.Execute<UInt128?>(() =>
            {
                EnsureCoreInitialized();
                return NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 presentationId)
                    ? presentationId
                    : null;
            });
        }

        /// <summary>Returns an immutable snapshot dictionary of all registered node presentations.</summary>
        public IReadOnlyDictionary<UInt128, NodeRepresentation> GetNodePresentations()
        {
            return GraphSynchronization.Execute<IReadOnlyDictionary<UInt128, NodeRepresentation>>(() =>
            {
                EnsureCoreInitialized();
                return new ReadOnlyDictionary<UInt128, NodeRepresentation>(
                    new Dictionary<UInt128, NodeRepresentation>(NodePresentation));
            });
        }

        /// <summary>Returns the total count of registered node presentations.</summary>
        public int GetNodePresentationCount() => GraphSynchronization.Execute(() =>
        {
            EnsureCoreInitialized();
            return NodePresentation.Count;
        });

        /// <summary>Updates position, size, color, or display label overrides for a registered node presentation.</summary>
        public bool UpdateNodeRepresentation(
            Guid nodeId,
            Vector3? position = null,
            double? size = null,
            uint? color = null,
            string? textLabel = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 presentationId) ||
                    !NodePresentation.TryGetValue(presentationId, out NodeRepresentation? representation))
                {
                    return false;
                }
                if (position.HasValue) representation.SetPosition(position.Value);
                if (size.HasValue) representation.SetSize(size.Value);
                if (color.HasValue) representation.SetColor(color.Value);
                if (textLabel != null) representation.SetTextLabel(textLabel);
                return true;
            });
        }

        /// <summary>Returns a copy of the active 3D node layout bias options across in-memory and disk-paged modes.</summary>
        public NodeLayoutOptions GetNodeLayoutOptions()
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return new NodeLayoutOptions(_nodeLayoutOptions.NodeWeightBias, _nodeLayoutOptions.NeighborPositionBias);
            });
        }

        /// <summary>Updates the active 3D node layout bias options and recalculates all node presentations.</summary>
        public void UpdateNodeLayoutOptions(double nodeWeightBias, double neighborPositionBias)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                _nodeLayoutOptions = new NodeLayoutOptions(nodeWeightBias, neighborPositionBias);
                RecalculateAllNodePresentationsInternal();
            });
        }

        /// <summary>Recalculates 3D layout positions, colors, sizes, and labels for all registered nodes.</summary>
        public void RecalculateAllNodePresentations()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                RecalculateAllNodePresentationsInternal();
            });
        }

        /// <summary>Recalculates presentation derivation and layout position for a single node.</summary>
        public bool RecalculateNodePresentation(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                Dictionary<Guid, NodeDerivationSnapshot> snapshots = BuildNodeDerivationSnapshots();
                if (!snapshots.TryGetValue(nodeId, out NodeDerivationSnapshot? snapshot)) return false;
                NodeLayoutResult layout = NodeLayoutCalculator.Calculate(snapshots, DeriveNodeSizes(snapshots),
                    SplitNodePresentationKinds, _nodeLayoutOptions);
                return layout.Positions.TryGetValue(nodeId, out Vector3 position) &&
                       ApplyNodePresentationDerivation(snapshot, position);
            });
        }

        internal NodeLayoutResult GetNodeLayoutResult()
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                Dictionary<Guid, NodeDerivationSnapshot> snapshots = BuildNodeDerivationSnapshots();
                return NodeLayoutCalculator.Calculate(snapshots, DeriveNodeSizes(snapshots),
                    SplitNodePresentationKinds, _nodeLayoutOptions);
            });
        }

        internal NodeDerivationSnapshot? GetNodeDerivationSnapshot(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return BuildNodeDerivationSnapshots().TryGetValue(nodeId, out NodeDerivationSnapshot? snapshot)
                    ? snapshot
                    : null;
            });
        }

        private void RecalculateAllNodePresentationsInternal()
        {
            if (_nodePresentationRecalculationSuspendCount > 0) return;
            Dictionary<Guid, NodeDerivationSnapshot> snapshots = BuildNodeDerivationSnapshots();
            Dictionary<Guid, double> sizes = DeriveNodeSizes(snapshots);
            NodeLayoutResult layout = NodeLayoutCalculator.Calculate(snapshots, sizes,
                SplitNodePresentationKinds, _nodeLayoutOptions);
            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                if (layout.Positions.TryGetValue(entry.Key, out Vector3 position))
                {
                    ApplyNodePresentationDerivation(entry.Value, position);
                }
            }
        }

        private Dictionary<Guid, double> DeriveNodeSizes(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots)
        {
            Dictionary<Guid, double> sizes = new Dictionary<Guid, double>();
            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots)
            {
                sizes[entry.Key] = NodePresentationCalculator.CalculateSize(entry.Value);
            }
            return sizes;
        }

        private bool ApplyNodePresentationDerivation(NodeDerivationSnapshot snapshot, Vector3 position)
        {
            if (!NodePresentationKeyByNodeId.TryGetValue(snapshot.NodeId, out UInt128 presentationId) ||
                !NodePresentation.TryGetValue(presentationId, out NodeRepresentation? representation))
            {
                return false;
            }
            representation.SetColor(NodePresentationCalculator.CalculateColor(snapshot));
            representation.SetSize(NodePresentationCalculator.CalculateSize(snapshot));
            representation.SetTextLabel(NodePresentationCalculator.CalculateLabel(snapshot));
            representation.SetPosition(position);
            return true;
        }

        private Dictionary<Guid, NodeDerivationSnapshot> BuildNodeDerivationSnapshots()
        {
            Dictionary<Guid, NodeDerivationSnapshot> snapshots = new Dictionary<Guid, NodeDerivationSnapshot>();
            foreach (Node node in EnumerateTrustedNodesInternal().Concat(UntrustedNodeDictionary.Values))
            {
                Guid nodeId = node.GetNodeID();
                bool isTrusted = node.IsTrusted;
                List<NodeDerivationLink> links = BuildNodeDerivationLinks(nodeId);
                NodeRelationType? derivedClass = NodePresentationCalculator.CalculateClass(links);
                double weight = NodePresentationCalculator.CalculateWeight(links);
                double incidentConfidenceSum = links.Sum(link => link.Weight);
                double trustRating = NodePresentationCalculator.CalculateTrustRating(
                    node.GetVerifiedReferences(), node.GetInvalidReferences(), incidentConfidenceSum, isTrusted);
                string? summary = isTrusted
                    ? GetTrustedNodeSummary(node) ?? node.GetLabel()
                    : GetUntrustedNodeSummary(node) ?? node.GetLabel();
                List<Guid> neighbors = links.Select(link => link.OtherNodeId)
                    .Where(id => id != nodeId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();
                snapshots[nodeId] = new NodeDerivationSnapshot(
                    nodeId,
                    node.GetTargetID(),
                    node.GetTargetKind(),
                    isTrusted,
                    derivedClass,
                    weight,
                    trustRating,
                    summary,
                    links.OrderBy(link => link.AssertionId).ThenBy(link => link.RelationType)
                        .ThenBy(link => link.OtherNodeId).ToList(),
                    neighbors);
            }
            return snapshots;
        }

        private List<NodeDerivationLink> BuildNodeDerivationLinks(Guid nodeId)
        {
            Dictionary<Guid, NodeDerivationLink> links = new Dictionary<Guid, NodeDerivationLink>();
            DateTimeOffset activeAt = DateTimeOffset.UtcNow;
            if (TrustedOutgoingLinks.TryGetValue(nodeId, out List<NodeLink>? outgoing))
            {
                foreach (NodeLink link in outgoing)
                {
                    AddDerivationLink(links, nodeId, link, activeAt);
                }
            }
            if (TrustedIncomingLinks.TryGetValue(nodeId, out List<NodeLink>? incoming))
            {
                foreach (NodeLink link in incoming)
                    AddDerivationLink(links, nodeId, link, activeAt);
            }
            return links.Values.ToList();
        }

        /// <summary>Defers presentation layout recalculation until the returned scope is disposed.</summary>
        /// <remarks>Scopes may be nested. Mutations still synchronize individually; the final dispose recalculates once.</remarks>
        public IDisposable SuspendPresentationRecalculation()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                _nodePresentationRecalculationSuspendCount = checked(_nodePresentationRecalculationSuspendCount + 1);
            });
            return new PresentationUpdateScope(this);
        }

        private void ResumePresentationRecalculation()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_nodePresentationRecalculationSuspendCount <= 0)
                    throw new InvalidOperationException("Presentation recalculation scope count is inconsistent.");
                _nodePresentationRecalculationSuspendCount--;
                if (_nodePresentationRecalculationSuspendCount == 0)
                    RecalculateAllNodePresentationsInternal();
            });
        }

        private static void AddDerivationLink(Dictionary<Guid, NodeDerivationLink> links, Guid nodeId,
            NodeLink link, DateTimeOffset activeAt)
        {
            if (!link.Provenance.IsActiveAt(activeAt)) return;
            Guid otherNodeId = link.SourceNodeId == nodeId ? link.TargetNodeId : link.SourceNodeId;
            if (otherNodeId == nodeId) return;
            NodeDerivationLink derived = new NodeDerivationLink(
                link.Provenance.AssertionId, link.RelationType, link.Weight, otherNodeId);
            if (!links.TryGetValue(derived.AssertionId, out NodeDerivationLink existing) ||
                PreferDerivationLink(derived, existing))
            {
                links[derived.AssertionId] = derived;
            }
        }

        private static bool PreferDerivationLink(NodeDerivationLink candidate, NodeDerivationLink existing)
        {
            int relationComparison = candidate.RelationType.CompareTo(existing.RelationType);
            if (relationComparison != 0) return relationComparison < 0;
            int nodeComparison = candidate.OtherNodeId.CompareTo(existing.OtherNodeId);
            if (nodeComparison != 0) return nodeComparison < 0;
            return candidate.Weight > existing.Weight;
        }

        public string? GetTrustedNodeSummary(Node node)
        {
            ArgumentNullException.ThrowIfNull(node);
            return GetTrustedNodeSummary(node.GetTargetKind(), node.GetTargetID(), node.GetLabel());
        }

        private string? GetUntrustedNodeSummary(Node node)
        {
            Guid targetId = node.GetTargetID();
            string? summary = node.GetTargetKind() switch
            {
                NodeTargetKind.Symbol => GetUntrustedSymbolBox(targetId) is SymbolBox untrustedSymbol && untrustedSymbol.HasSymbolChar
                    ? untrustedSymbol.GetSymbolChar().ToString()
                    : GetTrustedSymbolBox(targetId) is SymbolBox trustedSymbol && trustedSymbol.HasSymbolChar
                        ? trustedSymbol.GetSymbolChar().ToString()
                        : null,
                NodeTargetKind.Word => GetUntrustedWordBox(targetId) is WordBox untrustedWord
                    ? GetTextFromWordBox(untrustedWord, true)
                    : GetTrustedWordBox(targetId) is WordBox trustedWord
                        ? GetTextFromWordBox(trustedWord, true)
                        : null,
                NodeTargetKind.Sentence => GetUntrustedSentenceBox(targetId) is SentenceBox untrustedSentence
                    ? GetTextFromSentenceBox(untrustedSentence, true)
                    : GetTrustedSentenceBox(targetId) is SentenceBox trustedSentence
                        ? GetTextFromSentenceBox(trustedSentence, true)
                        : null,
                NodeTargetKind.Sense => $"Sense {targetId:N}",
                NodeTargetKind.Usage => $"Usage {targetId:N}",
                NodeTargetKind.Group => GetTrustedGroupBox(targetId)?.GetGroupingName(),
                NodeTargetKind.Source => GetTrustedSourceBox(targetId)?.GetSourceName(),
                NodeTargetKind.Function => GetUntrustedFunctionBox(targetId)?.FunctionKey ??
                    GetTrustedFunctionBox(targetId)?.FunctionKey,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(summary)) return null;
            string? label = node.GetLabel();
            return string.IsNullOrWhiteSpace(label) ? summary : $"{label.Trim()}: {summary}";
        }

        public string? GetTrustedNodeSummary(NodeTargetKind kind, Guid targetId, string? label = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (GetTrustedNodeByTarget(kind, targetId, label) == null)
                {
                    return null;
                }
                string? summary = kind switch
                {
                    NodeTargetKind.Symbol => GetTrustedSymbolBox(targetId) is SymbolBox symbol && symbol.HasSymbolChar
                        ? symbol.GetSymbolChar().ToString()
                        : "Symbol",
                    NodeTargetKind.Word => GetTrustedWordBox(targetId) is WordBox word ? GetTextFromWordBox(word) : "Word",
                    NodeTargetKind.Sentence => GetTextFromSentenceID(targetId),
                    NodeTargetKind.Sense => $"Sense {targetId:N}",
                    NodeTargetKind.Usage => $"Usage {targetId:N}",
                    NodeTargetKind.Group => GetTrustedGroupBox(targetId)?.GetGroupingName() ?? $"Group {targetId:N}",
                    NodeTargetKind.Source => GetTrustedSourceBox(targetId)?.GetSourceName() ?? $"Source {targetId:N}",
                    NodeTargetKind.Function => GetTrustedFunctionBox(targetId)?.FunctionKey ?? $"Function {targetId:N}",
                    _ => null
                };
                return string.IsNullOrWhiteSpace(label) ? summary : $"{label.Trim()}: {summary}";
            });
        }

        /// <summary>Gets the character assigned to a trusted symbol by identifier across in-memory and disk-paged modes.</summary>
        /// <param name="symbolId">Symbol identifier to resolve.</param>
        /// <returns>The symbol character, or <see langword="null"/> if not found.</returns>
        public char? GetTrustedSymbolChar(Guid symbolId)
        {
            return GraphSynchronization.Execute<char?>(() =>
                GetTrustedSymbolBox(symbolId)?.GetSymbolChar());
        }
        
        /// <summary>Gets the unique trusted symbol identifier for a character when exactly one sense exists.</summary>
        /// <param name="symbolChar">Character to resolve.</param>
        /// <returns>The unique symbol identifier, or <see langword="null"/> if zero or multiple senses exist.</returns>
        public Guid? GetTrustedSymbolID(char symbolChar)
        {
            return GraphSynchronization.Execute<Guid?>(() =>
            {
                EnsureCoreInitialized();
                return TrustedSymbolCharIndex.TryGetValue(symbolChar, out OrderedGuidIndexList? symbolIds) && symbolIds.Count == 1
                    ? symbolIds[0]
                    : null;
            });
        }

        /// <summary>Returns an isolated snapshot of all trusted symbol identifiers matching a character.</summary>
        public List<Guid> GetTrustedSymbolIDs(char symbolChar)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedSymbolCharIndex.TryGetValue(symbolChar, out OrderedGuidIndexList? symbolIds)
                    ? symbolIds.Snapshot()
                    : new List<Guid>();
            });
        }

        /// <summary>Returns an isolated snapshot of symbol identifiers contained in a trusted word.</summary>
        /// <param name="wordId">Trusted word identifier to inspect.</param>
        /// <returns>A list of constituent symbol identifiers.</returns>
        public List<Guid> GetSymbolsByTrustedWordID(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                List<Guid> symbols = new List<Guid>();
                WordBox? wordBox = GetTrustedWordBox(wordId);
                if (wordBox != null)
                {
                    var wordSymbols = wordBox.GetSymbols();
                    if (wordSymbols != null)
                    {
                        symbols.AddRange(wordSymbols);
                    }
                }
                return symbols;
            });
        }

        /// <summary>Returns an isolated snapshot of trusted symbol identifiers associated with a sense.</summary>
        /// <param name="senseId">Sense identifier to query.</param>
        /// <returns>A list of matching trusted symbol identifiers.</returns>
        public List<Guid> GetTrustedSymbolsBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SymbolsBySenseIndex, senseId); });
        }

        /// <summary>Returns an isolated snapshot of trusted word identifiers associated with a sense.</summary>
        /// <param name="senseId">Sense identifier to query.</param>
        /// <returns>A list of matching trusted word identifiers.</returns>
        public List<Guid> GetTrustedWordsBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsBySenseIndex, senseId); });
        }

        /// <summary>Returns an isolated snapshot of trusted sentence identifiers associated with a sense.</summary>
        public List<Guid> GetTrustedSentencesBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SentencesBySenseIndex, senseId); });
        }

        /// <summary>Returns an isolated snapshot of trusted word identifiers associated with a usage.</summary>
        public List<Guid> GetTrustedWordsByUsageID(Guid usageId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsByUsageIndex, usageId); });
        }

        /// <summary>Returns an isolated snapshot of trusted word identifiers containing a symbol.</summary>
        public List<Guid> GetTrustedWordsContainingSymbol(Guid symbolId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsBySymbolIndex, symbolId); });
        }

        /// <summary>Returns an isolated snapshot of trusted sentence identifiers containing a word.</summary>
        public List<Guid> GetTrustedSentencesContainingWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SentencesByWordIndex, wordId); });
        }

        /// <summary>Returns an isolated snapshot of trusted group identifiers containing a sentence.</summary>
        public List<Guid> GetTrustedGroupsContainingSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(GroupsBySentenceIndex, sentenceId); });
        }

        /// <summary>Returns an isolated snapshot of trusted source identifiers containing a group.</summary>
        public List<Guid> GetTrustedSourcesContainingGroup(Guid groupId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SourcesByGroupIndex, groupId); });
        }


        /// <summary>Returns an isolated snapshot of word identifiers for a trusted sentence across in-memory and paged modes.</summary>
        /// <param name="sentenceId">Trusted sentence identifier to inspect.</param>
        /// <returns>The list of constituent word identifiers, or <see langword="null"/> if the sentence is not trusted.</returns>
        public List<Guid>? GetWordsFromTrustedSentenceID(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
                GetTrustedSentenceBox(sentenceId)?.GetWords() ??
                (TrustedSentenceDictionary.ContainsKey(sentenceId) || IsPagedRecordAvailable(DiskPartition.Sentences, sentenceId)
                    ? new List<Guid>()
                    : null));
            
        }

        /// <summary>Reconstructs the text of a trusted word across in-memory and disk-paged modes.</summary>
        /// <param name="wordId">Trusted word identifier to render.</param>
        /// <returns>The reconstructed word text, or <see cref="string.Empty"/> if not found.</returns>
        public string GetTextFromWordID(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
                GetTrustedWordBox(wordId) is WordBox wordBox ? GetTextFromWordBox(wordBox) : string.Empty);
        }

        /// <summary>Reconstructs the space-joined text of a trusted sentence across in-memory and disk-paged modes.</summary>
        /// <param name="sentenceId">Trusted sentence identifier to render.</param>
        /// <returns>The reconstructed sentence text, or <see cref="string.Empty"/> if not found.</returns>
        public string GetTextFromSentenceID(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                SentenceBox? sentenceBox = GetTrustedSentenceBox(sentenceId);
                return sentenceBox == null ? string.Empty : GetTextFromSentenceBox(sentenceBox, false);
            });
        }

        private string GetTextFromSentenceBox(SentenceBox sentenceBox, bool includeUntrusted)
        {
            List<string> wordTexts = new List<string>();
            foreach (Guid wordId in sentenceBox.GetWords() ?? new List<Guid>())
            {
                WordBox? wordBox = includeUntrusted && UntrustedWordDictionary.TryGetValue(wordId, out WordBox? untrustedWord)
                    ? untrustedWord
                    : GetTrustedWordBox(wordId);
                if (wordBox != null)
                {
                    wordTexts.Add(GetTextFromWordBox(wordBox, includeUntrusted));
                }
            }
            return string.Join(" ", wordTexts);
        }

        private string GetTextFromWordBox(WordBox wordBox, bool includeUntrustedSymbols = false)
        {
            var symbols = wordBox.GetSymbols();
            if (symbols == null)
            {
                return string.Empty;
            }

            var text = new System.Text.StringBuilder(symbols.Count);
            foreach (Guid symbolId in symbols)
            {
                SymbolBox? symbolBox = includeUntrustedSymbols && UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? untrustedSymbol)
                    ? untrustedSymbol
                    : GetTrustedSymbolBox(symbolId);
                if (symbolBox != null)
                {
                    text.Append(symbolBox.GetSymbolChar());
                }
            }
            return text.ToString();
        }

        /// <summary>Disposes any attached disk store and clears all trusted and untrusted registries, indexes, and conversations.</summary>
        public void ClearAllDictionaries()
        {
            GraphSynchronization.Execute(() =>
            {
                _diskStore?.Dispose();
                _diskStore = null;
                _diskStorePath = null;
                _diskPagedReadOnly = false;
                _legacyDiskStoreReadOnly = false;
                _maxPagedObjects = 1024;
                _nodeLayoutOptions = new NodeLayoutOptions();
                ClearPagedObjectCache();
                SourceDictionary.Clear();
                TextConversationDictionary.Clear();
                SymbolicConversationDictionary.Clear();
                TrustedSymbolDictionary.Clear();
                TrustedUsageDictionary.Clear();
                TrustedSenseDictionary.Clear();
                TrustedWordDictionary.Clear();
                TrustedSentenceDictionary.Clear();
                TrustedGroupDictionary.Clear();
                TrustedFunctionDictionary.Clear();
                UntrustedFunctionDictionary.Clear();
                TrustedWordIdentityIndex.Clear();
                TrustedSentenceIdentityIndex.Clear();
                TrustedSymbolIdentityIndex.Clear();
                TrustedSymbolCharIndex.Clear();
                SymbolsBySenseIndex.Clear();
                WordsBySenseIndex.Clear();
                SentencesBySenseIndex.Clear();
                WordsByUsageIndex.Clear();
                WordsBySymbolIndex.Clear();
                SentencesByWordIndex.Clear();
                GroupsBySentenceIndex.Clear();
                SourcesByGroupIndex.Clear();
                foreach (Node node in EnumerateTrustedNodesInternal())
                {
                    node.SetIdentityChangingHandler(null);
                    node.SetIdentityChangedHandler(null);
                    node.SetReferenceCountsChangedHandler(null);
                }
                TrustedNodeDictionary.Clear();
                NodePresentation.Clear();
                NodePresentationKeyByNodeId.Clear();
                _nextNodePresentationId = 0;
                _nodePresentationIdSpaceExhausted = false;
                foreach (Node node in UntrustedNodeDictionary.Values)
                {
                    node.SetIdentityChangingHandler(null);
                    node.SetIdentityChangedHandler(null);
                    node.SetReferenceCountsChangedHandler(null);
                }
                UntrustedNodeDictionary.Clear();
                TrustedNodeByIdentityIndex.Clear();
                UntrustedNodeByIdentityIndex.Clear();
                TrustedNodeByTargetIndex.Clear();
                UntrustedNodeByTargetIndex.Clear();
                TrustedOutgoingLinks.Clear();
                TrustedIncomingLinks.Clear();
                TrustedLinkKeys.Clear();
                LinksByAssertionId.Clear();
                LinksBySourceTargetId.Clear();
                LinksByEvidenceTargetId.Clear();
                TrustedSkillsGraph = new Graph<Guid>(true);
                UntrustedSymbolDictionary.Clear();
                UntrustedWordDictionary.Clear();
                UntrustedSentenceDictionary.Clear();
                UntrustedSentenceIdentityIndex.Clear();
                UntrustedSentenceIndexedKeyById.Clear();
                UntrustedFunctionDictionary.Clear();
                _graphEpoch = 0;
                _initialized = false;
            });
        }

        /// <summary>Clears all staged untrusted symbol, word, sentence, and function registries and removes their untrusted nodes.</summary>
        public void ClearUntrustedDictionaries()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ClearUntrustedSymbols();
                ClearUntrustedWords();
                ClearUntrustedSentences();
                foreach (Guid functionId in new List<Guid>(UntrustedFunctionDictionary.Keys))
                {
                    RemoveUntrustedNodeForTarget(NodeTargetKind.Function, functionId);
                }
                UntrustedFunctionDictionary.Clear();
            });
        }

        /// <summary>Clears all staged untrusted symbols and removes their associated untrusted nodes.</summary>
        public void ClearUntrustedSymbols()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                foreach (Guid symbolId in new List<Guid>(UntrustedSymbolDictionary.Keys))
                {
                    RemoveUntrustedNodeForTarget(NodeTargetKind.Symbol, symbolId);
                }
                UntrustedSymbolDictionary.Clear();
            });
        }

        /// <summary>Clears all staged untrusted words and removes their associated untrusted nodes.</summary>
        public void ClearUntrustedWords()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                foreach (Guid wordId in new List<Guid>(UntrustedWordDictionary.Keys))
                {
                    RemoveUntrustedNodeForTarget(NodeTargetKind.Word, wordId);
                }
                UntrustedWordDictionary.Clear();
            });
        }

        /// <summary>Clears all staged untrusted sentences and removes their associated untrusted nodes.</summary>
        public void ClearUntrustedSentences()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                foreach (Guid sentenceId in new List<Guid>(UntrustedSentenceDictionary.Keys))
                {
                    RemoveUntrustedNodeForTarget(NodeTargetKind.Sentence, sentenceId);
                }
                UntrustedSentenceDictionary.Clear();
                UntrustedSentenceIdentityIndex.Clear();
                UntrustedSentenceIndexedKeyById.Clear();
            });
        }


        /// <summary>Assigns a sense identifier to a staged untrusted symbol box.</summary>
        /// <param name="symbolId">Staged symbol identifier.</param>
        /// <param name="senseId">Sense identifier to assign.</param>
        /// <returns><see langword="true"/> if the staged symbol was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedSymbolSenseID(Guid symbolId, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                {
                    symbolBox.SetSenseID(senseId);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns a character to a staged untrusted symbol box.</summary>
        public bool SetUntrustedSymbolChar(Guid symbolId, char symbolChar)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                {
                    symbolBox.SetSymbolChar(symbolChar);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns a sense identifier to a staged untrusted word box.</summary>
        /// <param name="wordId">Staged word identifier.</param>
        /// <param name="senseId">Sense identifier to assign.</param>
        /// <returns><see langword="true"/> if the staged word was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedWordSenseID(Guid wordId, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetSenseID(senseId);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns a sense identifier to a staged untrusted sentence box.</summary>
        /// <param name="sentenceId">Staged sentence identifier.</param>
        /// <param name="senseId">Sense identifier to assign.</param>
        /// <returns><see langword="true"/> if the staged sentence was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedSentenceSenseID(Guid sentenceId, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    if (WouldConflictUntrustedSentenceIdentity(sentenceId, sentenceBox, null, senseId))
                    {
                        return false;
                    }
                    sentenceBox.SetSenseID(senseId);
                    ReindexUntrustedSentence(sentenceId, sentenceBox);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns the constituent symbol sequence to a staged untrusted word box.</summary>
        /// <param name="wordId">Staged word identifier.</param>
        /// <param name="symbols">Ordered list of constituent symbol identifiers.</param>
        /// <returns><see langword="true"/> if the staged word was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedWordSymbols(Guid wordId, List<Guid> symbols)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetSymbols(symbols);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns the constituent word sequence to a staged untrusted sentence box.</summary>
        /// <param name="sentenceId">Staged sentence identifier.</param>
        /// <param name="words">Ordered list of constituent word identifiers.</param>
        /// <returns><see langword="true"/> if the staged sentence was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedSentenceWords(Guid sentenceId, List<Guid> words)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    if (WouldConflictUntrustedSentenceIdentity(sentenceId, sentenceBox, words, null))
                    {
                        return false;
                    }
                    sentenceBox.SetWords(words);
                    ReindexUntrustedSentence(sentenceId, sentenceBox);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns a usage identifier to a staged untrusted word box.</summary>
        /// <param name="wordId">Staged word identifier.</param>
        /// <param name="usageId">Usage identifier to assign.</param>
        /// <returns><see langword="true"/> if the staged word was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedWordUsageID(Guid wordId, Guid usageId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetUsageID(usageId);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Assigns a part-of-speech classification to a staged untrusted word box.</summary>
        /// <param name="wordId">Staged word identifier.</param>
        /// <param name="pos">Part-of-speech value to assign.</param>
        /// <returns><see langword="true"/> if the staged word was found and updated; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedWordPartOfSpeech(Guid wordId, PartOfSpeech pos)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetPartOfSpeech(pos);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>Registers a symbol box directly in the trusted registry if its identity is complete and unique.</summary>
        /// <param name="symbolBox">Symbol box to trust.</param>
        /// <returns><see langword="true"/> if trusted and wired into the graph; otherwise <see langword="false"/>.</returns>
        public bool AddTrustedSymbol(SymbolBox symbolBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(symbolBox);
                Guid symbolId = symbolBox.GetSymbolID();
                if (!symbolBox.HasCompleteIdentity ||
                    TrustedSymbolDictionary.ContainsKey(symbolId) ||
                    UntrustedSymbolDictionary.ContainsKey(symbolId) ||
                    !HasTrustedSense(symbolBox.GetSenseID()))
                {
                    return false;
                }

                SymbolIdentityKey identityKey = symbolBox.GetIdentityKey();
                if (TrustedSymbolIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                symbolBox.FreezeIdentity();
                TrustedSymbolDictionary.Add(symbolId, symbolBox);
                TrustedSymbolIdentityIndex.Add(identityKey, symbolId);
                AddIndex(TrustedSymbolCharIndex, identityKey.SymbolChar, symbolId);
                AddIndex(SymbolsBySenseIndex, identityKey.SenseId, symbolId);
                EnsureTrustedNode(NodeTargetKind.Symbol, symbolId);
                return true;
            });
        }

        /// <summary>Registers a word box directly in the trusted registry if its identity and dependencies are trusted.</summary>
        /// <param name="wordBox">Word box to trust.</param>
        /// <returns><see langword="true"/> if trusted and wired into the graph; otherwise <see langword="false"/>.</returns>
        public bool AddTrustedWord(WordBox wordBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(wordBox);
                Guid wordId = wordBox.GetWordID();
                if (!CanTrustWord(wordBox) ||
                    TrustedWordDictionary.ContainsKey(wordId) ||
                    UntrustedWordDictionary.ContainsKey(wordId))
                {
                    return false;
                }

                WordIdentityKey identityKey = wordBox.GetIdentityKey();
                if (TrustedWordIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                wordBox.FreezeIdentity();
                TrustedWordDictionary.Add(wordId, wordBox);
                TrustedWordIdentityIndex.Add(identityKey, wordId);
                AddIndex(WordsBySenseIndex, identityKey.SenseId, wordId);
                AddIndex(WordsByUsageIndex, identityKey.UsageId, wordId);
                foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                {
                    AddIndex(WordsBySymbolIndex, symbolId, wordId);
                }
                EnsureTrustedNode(NodeTargetKind.Word, wordId);
                return true;
            });
        }

        /// <summary>Registers a sentence box directly in the trusted registry if its identity and dependencies are trusted.</summary>
        /// <param name="sentenceBox">Sentence box to trust.</param>
        /// <returns><see langword="true"/> if trusted and wired into the graph; otherwise <see langword="false"/>.</returns>
        public bool AddTrustedSentence(SentenceBox sentenceBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(sentenceBox);
                Guid sentenceId = sentenceBox.GetSentenceID();
                if (!CanTrustSentence(sentenceBox) ||
                    TrustedSentenceDictionary.ContainsKey(sentenceId) ||
                    UntrustedSentenceDictionary.ContainsKey(sentenceId))
                {
                    return false;
                }

                SentenceIdentityKey identityKey = sentenceBox.GetIdentityKey();
                if (TrustedSentenceIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                sentenceBox.FreezeIdentity();
                TrustedSentenceDictionary.Add(sentenceId, sentenceBox);
                TrustedSentenceIdentityIndex.Add(identityKey, sentenceId);
                AddIndex(SentencesBySenseIndex, identityKey.SenseId, sentenceId);
                foreach (Guid wordId in sentenceBox.GetWords() ?? new List<Guid>())
                {
                    AddIndex(SentencesByWordIndex, wordId, sentenceId);
                }
                EnsureTrustedNode(NodeTargetKind.Sentence, sentenceId);
                return true;
            });
        }

        /// <summary>Registers a usage box in the trusted registry and creates its canonical trusted node.</summary>
        /// <param name="usageBox">Usage box to trust.</param>
        /// <returns><see langword="true"/> if trusted; otherwise <see langword="false"/>.</returns>
        public bool AddTrustedUsage(UsageBox usageBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(usageBox);
                Guid usageId = usageBox.GetUsageID();
                if (TrustedUsageDictionary.ContainsKey(usageId))
                {
                    return false;
                }

                usageBox.MarkTrusted();
                TrustedUsageDictionary.Add(usageId, usageBox);
                EnsureTrustedNode(NodeTargetKind.Usage, usageId);
                return true;
            });
        }

        /// <summary>Registers a sense box in the trusted registry and creates its canonical trusted node.</summary>
        /// <param name="senseBox">Sense box to trust.</param>
        /// <returns><see langword="true"/> if trusted; otherwise <see langword="false"/>.</returns>
        public bool AddTrustedSense(SenseBox senseBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(senseBox);
                Guid senseId = senseBox.GetSenseID();
                if (TrustedSenseDictionary.ContainsKey(senseId))
                {
                    return false;
                }

                senseBox.MarkTrusted();
                TrustedSenseDictionary.Add(senseId, senseBox);
                EnsureTrustedNode(NodeTargetKind.Sense, senseId);
                return true;
            });
        }

        /// <summary>Registers a group box in the trusted registry if all constituent sentences are trusted.</summary>
        public bool AddTrustedGroup(GroupBox groupBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(groupBox);
                Guid groupId = groupBox.GetGroupingID();
                List<Guid>? sentenceIds = groupBox.GetSentences();
                if (TrustedGroupDictionary.ContainsKey(groupId) || sentenceIds == null ||
                    !sentenceIds.All(TrustedSentenceDictionary.ContainsKey))
                {
                    return false;
                }

                groupBox.FreezeIdentity();
                TrustedGroupDictionary.Add(groupId, groupBox);
                foreach (Guid sentenceId in sentenceIds)
                {
                    AddIndex(GroupsBySentenceIndex, sentenceId, groupId);
                }
                EnsureTrustedNode(NodeTargetKind.Group, groupId);
                return true;
            });
        }

        /// <summary>Registers a dispatch-verified deterministic function box in the trusted registry.</summary>
        public bool AddTrustedFunction(FunctionBox functionBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(functionBox);
                Guid functionId = functionBox.GetFunctionID();
                if (TrustedFunctionDictionary.ContainsKey(functionId))
                {
                    return false;
                }

                MathFunctionDispatcher.ValidateDefinition(functionBox);
                TrustedFunctionDictionary.Add(functionId, functionBox);
                try
                {
                    EnsureTrustedNode(NodeTargetKind.Function, functionId);
                    functionBox.MarkTrusted();
                    UntrustedFunctionDictionary.Remove(functionId);
                }
                catch
                {
                    TrustedFunctionDictionary.Remove(functionId);
                    throw;
                }
                return true;
            });
        }

        /// <summary>Registers a source box in the trusted registry if all constituent groups are trusted.</summary>
        public bool AddTrustedSource(SourceBox sourceBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(sourceBox);
                Guid sourceId = sourceBox.GetSourceID();
                List<Guid>? groupIds = sourceBox.GetGroupingIDs();
                if (SourceDictionary.ContainsKey(sourceId) || groupIds == null ||
                    !groupIds.All(TrustedGroupDictionary.ContainsKey))
                {
                    return false;
                }

                sourceBox.FreezeIdentity();
                SourceDictionary.Add(sourceId, sourceBox);
                foreach (Guid groupId in groupIds)
                {
                    AddIndex(SourcesByGroupIndex, groupId, sourceId);
                }
                EnsureTrustedNode(NodeTargetKind.Source, sourceId);
                return true;
            });
        }

        /// <summary>Appends a trusted sentence membership to a trusted group box and wires its structural link.</summary>
        public bool AddSentenceToTrustedGroup(Guid groupId, Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                return AddSentenceToTrustedGroupInternal(groupId, sentenceId, true);
            });
        }

        private bool AddSentenceToTrustedGroupInternal(Guid groupId, Guid sentenceId, bool recalculate)
        {
            if (!TrustedGroupDictionary.TryGetValue(groupId, out GroupBox? groupBox) ||
                !TrustedSentenceDictionary.ContainsKey(sentenceId))
            {
                return false;
            }

            if (!groupBox.AddSentenceFromCore(sentenceId)) return false;
            AddIndex(GroupsBySentenceIndex, sentenceId, groupId);
            LinkNodeToTarget(GetNodeIdForTarget(NodeTargetKind.Group, groupId),
                NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
            if (recalculate) RecalculateAllNodePresentationsInternal();
            return true;
        }

        public bool AddGroupToTrustedSource(Guid sourceId, Guid groupId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                return AddGroupToTrustedSourceInternal(sourceId, groupId, true);
            });
        }

        private bool AddGroupToTrustedSourceInternal(Guid sourceId, Guid groupId, bool recalculate)
        {
            if (!SourceDictionary.TryGetValue(sourceId, out SourceBox? sourceBox) ||
                !TrustedGroupDictionary.ContainsKey(groupId))
            {
                return false;
            }

            if (!sourceBox.AddGroupingFromCore(groupId)) return false;
            AddIndex(SourcesByGroupIndex, groupId, sourceId);
            LinkNodeToTarget(GetNodeIdForTarget(NodeTargetKind.Source, sourceId),
                NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
            if (recalculate) RecalculateAllNodePresentationsInternal();
            return true;
        }

        public bool AddTextConversation(ConversationText conversation)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(conversation);
                Guid conversationId = conversation.GetConversationID();
                if (conversationId == Guid.Empty || TextConversationDictionary.ContainsKey(conversationId))
                {
                    return false;
                }

                TextConversationDictionary.Add(conversationId, conversation);
                return true;
            });
        }

        public bool RemoveTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() => { EnsureMutableStorageMode(); return TextConversationDictionary.Remove(conversationId); });
        }

        public bool AddSymbolicConversation(ConversationSymbolic conversation)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(conversation);
                Guid conversationId = conversation.GetConversationID();
                if (conversationId == Guid.Empty || SymbolicConversationDictionary.ContainsKey(conversationId))
                {
                    return false;
                }

                SymbolicConversationDictionary.Add(conversationId, conversation);
                return true;
            });
        }

        public bool RemoveSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() => { EnsureMutableStorageMode(); return SymbolicConversationDictionary.Remove(conversationId); });
        }


        /// <summary>Stages a symbol box in the untrusted registry and ensures its canonical untrusted node exists.</summary>
        /// <param name="symbolBox">Symbol box to stage.</param>
        /// <returns><see langword="true"/> if staged; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedSymbol(SymbolBox symbolBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(symbolBox);
                Guid symbolId = symbolBox.GetSymbolID();
                if (symbolBox.IsIdentityFrozen || TrustedSymbolDictionary.ContainsKey(symbolId))
                {
                    return false;
                }

                UntrustedSymbolDictionary[symbolId] = symbolBox;
                if (!EnsureUntrustedNode(NodeTargetKind.Symbol, symbolId))
                    RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>Stages a word box in the untrusted registry and ensures its canonical untrusted node exists.</summary>
        /// <param name="wordBox">Word box to stage.</param>
        /// <returns><see langword="true"/> if staged; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedWord(WordBox wordBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(wordBox);
                Guid wordId = wordBox.GetWordID();
                if (wordBox.IsIdentityFrozen || TrustedWordDictionary.ContainsKey(wordId))
                {
                    return false;
                }

                UntrustedWordDictionary[wordId] = wordBox;
                if (!EnsureUntrustedNode(NodeTargetKind.Word, wordId))
                    RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>Stages a sentence box in the untrusted registry and ensures its canonical untrusted node exists.</summary>
        /// <param name="sentenceBox">Sentence box to stage.</param>
        /// <returns><see langword="true"/> if staged; otherwise <see langword="false"/>.</returns>
        public bool SetUntrustedSentence(SentenceBox sentenceBox)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(sentenceBox);
                Guid sentenceId = sentenceBox.GetSentenceID();
                if (sentenceBox.IsIdentityFrozen || TrustedSentenceDictionary.ContainsKey(sentenceId))
                {
                    return false;
                }

                SentenceIdentityKey? newKey = sentenceBox.HasCompleteIdentity ? sentenceBox.GetIdentityKey() : null;
                if (newKey != null &&
                    UntrustedSentenceIdentityIndex.TryGetValue(newKey, out Guid conflictingId) &&
                    conflictingId != sentenceId)
                {
                    // Identity is already staged under another Guid; callers should reuse that Guid.
                    return false;
                }

                RemoveUntrustedSentenceEntry(sentenceId);
                UntrustedSentenceDictionary[sentenceId] = sentenceBox;
                if (newKey != null)
                {
                    IndexUntrustedSentenceKey(sentenceId, newKey);
                }
                if (!EnsureUntrustedNode(NodeTargetKind.Sentence, sentenceId))
                    RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>Returns the staged untrusted sentence whose identity (word sequence and sense) matches, if any.</summary>
        /// <param name="words">Ordered word identifiers.</param>
        /// <param name="senseId">Sense identifier.</param>
        /// <returns>The staged <see cref="SentenceBox"/> with that identity, or <see langword="null"/>.</returns>
        internal SentenceBox? GetUntrustedSentenceBoxByIdentity(IEnumerable<Guid> words, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                SentenceIdentityKey identityKey = new SentenceIdentityKey(words, senseId);
                if (!UntrustedSentenceIdentityIndex.TryGetValue(identityKey, out Guid sentenceId) ||
                    !UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    return null;
                }
                return sentenceBox.HasCompleteIdentity && sentenceBox.GetIdentityKey().Equals(identityKey) ? sentenceBox : null;
            });
        }

        /// <summary>Finds a staged untrusted symbol with the given character and sense. Linear scan over staged symbols.</summary>
        internal SymbolBox? FindUntrustedSymbolByIdentity(char symbolChar, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                foreach (SymbolBox symbol in UntrustedSymbolDictionary.Values)
                {
                    if (symbol.GetSymbolChar() == symbolChar && symbol.GetSenseID() == senseId)
                    {
                        return symbol;
                    }
                }
                return null;
            });
        }

        /// <summary>Finds a staged untrusted word with the given identity. Linear scan over staged words.</summary>
        internal WordBox? FindUntrustedWordByIdentity(List<Guid> symbols, Guid senseId, Guid usageId, PartOfSpeech partOfSpeech)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                WordIdentityKey identityKey = new WordIdentityKey(symbols, senseId, usageId, partOfSpeech);
                foreach (WordBox word in UntrustedWordDictionary.Values)
                {
                    if (word.HasCompleteIdentity && word.GetIdentityKey().Equals(identityKey))
                    {
                        return word;
                    }
                }
                return null;
            });
        }

        /// <summary>Adds an identity key for a staged untrusted sentence. Caller must have checked for conflicts.</summary>
        private void IndexUntrustedSentenceKey(Guid sentenceId, SentenceIdentityKey identityKey)
        {
            UntrustedSentenceIdentityIndex[identityKey] = sentenceId;
            UntrustedSentenceIndexedKeyById[sentenceId] = identityKey;
        }

        /// <summary>Removes a staged untrusted sentence's identity key from the index, leaving the dictionary entry in place.</summary>
        private void UnindexUntrustedSentence(Guid sentenceId)
        {
            if (UntrustedSentenceIndexedKeyById.TryGetValue(sentenceId, out SentenceIdentityKey? indexedKey))
            {
                if (UntrustedSentenceIdentityIndex.TryGetValue(indexedKey, out Guid indexedId) && indexedId == sentenceId)
                {
                    UntrustedSentenceIdentityIndex.Remove(indexedKey);
                }
                UntrustedSentenceIndexedKeyById.Remove(sentenceId);
            }
        }

        /// <summary>Re-derives the identity key for a staged sentence after its identity changed.</summary>
        private void ReindexUntrustedSentence(Guid sentenceId, SentenceBox sentenceBox)
        {
            UnindexUntrustedSentence(sentenceId);
            if (sentenceBox.HasCompleteIdentity)
            {
                IndexUntrustedSentenceKey(sentenceId, sentenceBox.GetIdentityKey());
            }
        }

        /// <summary>Returns true if applying the given words or sense would make the sentence's identity collide with another staged sentence.</summary>
        private bool WouldConflictUntrustedSentenceIdentity(Guid sentenceId, SentenceBox sentenceBox, List<Guid>? words, Guid? senseId)
        {
            List<Guid>? projectedWords = words ?? sentenceBox.GetWords();
            Guid? projectedSense = senseId ?? sentenceBox.GetSenseID();
            if (projectedWords == null || !projectedSense.HasValue)
            {
                return false;
            }
            SentenceIdentityKey projectedKey = new SentenceIdentityKey(projectedWords, projectedSense.Value);
            return UntrustedSentenceIdentityIndex.TryGetValue(projectedKey, out Guid otherId) && otherId != sentenceId;
        }

        /// <summary>Removes a staged untrusted sentence from its dictionary and identity index. Returns whether it was present.</summary>
        private bool RemoveUntrustedSentenceEntry(Guid sentenceId)
        {
            UnindexUntrustedSentence(sentenceId);
            return UntrustedSentenceDictionary.Remove(sentenceId);
        }

        /// <summary>Removes a staged untrusted symbol box and cleans up any canonical or labeled untrusted nodes targeting it.</summary>
        /// <param name="symbolId">Staged symbol identifier to remove.</param>
        /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveUntrustedSymbol(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                bool removed = UntrustedSymbolDictionary.Remove(symbolId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Symbol, symbolId);
                return removed;
            });
        }

        /// <summary>Removes a staged untrusted word box and cleans up any canonical or labeled untrusted nodes targeting it.</summary>
        /// <param name="wordId">Staged word identifier to remove.</param>
        /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveUntrustedWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                bool removed = UntrustedWordDictionary.Remove(wordId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Word, wordId);
                return removed;
            });
        }

        /// <summary>Removes a staged untrusted sentence box and cleans up any canonical or labeled untrusted nodes targeting it.</summary>
        /// <param name="sentenceId">Staged sentence identifier to remove.</param>
        /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveUntrustedSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                bool removed = RemoveUntrustedSentenceEntry(sentenceId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Sentence, sentenceId);
                return removed;
            });
        }


        /// <summary>Promotes a staged untrusted symbol box to trusted state if its identity is complete and unique.</summary>
        /// <param name="symbolId">Staged symbol identifier to promote.</param>
        /// <returns><see langword="true"/> if promoted; otherwise <see langword="false"/>.</returns>
        public bool ConvertUntrustedSymbolToTrusted(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedSymbolDictionary.ContainsKey(symbolId) ||
                    !UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                    !symbolBox.HasCompleteIdentity ||
                    !HasTrustedSense(symbolBox.GetSenseID()))
                {
                    return false;
                }

                SymbolIdentityKey identityKey = symbolBox.GetIdentityKey();
                if (TrustedSymbolIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                symbolBox.FreezeIdentity();
                TrustedSymbolDictionary.Add(symbolId, symbolBox);
                TrustedSymbolIdentityIndex.Add(identityKey, symbolId);
                AddIndex(TrustedSymbolCharIndex, identityKey.SymbolChar, symbolId);
                AddIndex(SymbolsBySenseIndex, identityKey.SenseId, symbolId);
                UntrustedSymbolDictionary.Remove(symbolId);
                EnsureTrustedNode(NodeTargetKind.Symbol, symbolId);
                return true;
            });
        }

        /// <summary>Promotes a staged untrusted word box to trusted state if its identity and dependencies are trusted.</summary>
        /// <param name="wordId">Staged word identifier to promote.</param>
        /// <returns><see langword="true"/> if promoted; otherwise <see langword="false"/>.</returns>
        public bool ConvertUntrustedWordToTrusted(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedWordDictionary.ContainsKey(wordId) ||
                    !UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ||
                    !CanTrustWord(wordBox))
                {
                    return false;
                }

                WordIdentityKey identityKey = wordBox.GetIdentityKey();
                if (TrustedWordIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                wordBox.FreezeIdentity();
                TrustedWordDictionary.Add(wordId, wordBox);
                TrustedWordIdentityIndex.Add(identityKey, wordId);
                AddIndex(WordsBySenseIndex, identityKey.SenseId, wordId);
                AddIndex(WordsByUsageIndex, identityKey.UsageId, wordId);
                foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                {
                    AddIndex(WordsBySymbolIndex, symbolId, wordId);
                }
                UntrustedWordDictionary.Remove(wordId);
                EnsureTrustedNode(NodeTargetKind.Word, wordId);
                return true;
            });
        }

        /// <summary>Promotes a staged untrusted sentence box to trusted state if its identity and dependencies are trusted.</summary>
        /// <param name="sentenceId">Staged sentence identifier to promote.</param>
        /// <returns><see langword="true"/> if promoted; otherwise <see langword="false"/>.</returns>
        public bool ConvertUntrustedSentenceToTrusted(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedSentenceDictionary.ContainsKey(sentenceId) ||
                    !UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ||
                    !CanTrustSentence(sentenceBox))
                {
                    return false;
                }

                SentenceIdentityKey identityKey = sentenceBox.GetIdentityKey();
                if (TrustedSentenceIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                sentenceBox.FreezeIdentity();
                TrustedSentenceDictionary.Add(sentenceId, sentenceBox);
                TrustedSentenceIdentityIndex.Add(identityKey, sentenceId);
                AddIndex(SentencesBySenseIndex, identityKey.SenseId, sentenceId);
                foreach (Guid wordId in sentenceBox.GetWords() ?? new List<Guid>())
                {
                    AddIndex(SentencesByWordIndex, wordId, sentenceId);
                }
                RemoveUntrustedSentenceEntry(sentenceId);
                EnsureTrustedNode(NodeTargetKind.Sentence, sentenceId);
                return true;
            });
        }

        public bool ConvertUntrustedSentenceSubgraphToTrusted(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedSentenceDictionary.ContainsKey(sentenceId) ||
                    !UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ||
                    !sentenceBox.HasCompleteIdentity ||
                    !HasTrustedSense(sentenceBox.GetSenseID()))
                {
                    return false;
                }

                List<Guid> wordIds = sentenceBox.GetWords() ?? new List<Guid>();
                SentenceIdentityKey sentenceIdentity = sentenceBox.GetIdentityKey();
                if (TrustedSentenceIdentityIndex.ContainsKey(sentenceIdentity))
                {
                    return false;
                }

                List<Guid> symbolIdsToPromote = new List<Guid>();
                HashSet<Guid> seenSymbolIds = new HashSet<Guid>();
                HashSet<SymbolIdentityKey> batchSymbolIdentities = new HashSet<SymbolIdentityKey>();
                List<Guid> wordIdsToPromote = new List<Guid>();
                HashSet<Guid> seenWordIds = new HashSet<Guid>();
                HashSet<WordIdentityKey> batchWordIdentities = new HashSet<WordIdentityKey>();

                foreach (Guid wordId in wordIds)
                {
                    if (TrustedWordDictionary.ContainsKey(wordId))
                    {
                        continue;
                    }
                    if (!seenWordIds.Add(wordId))
                    {
                        continue;
                    }
                    if (!UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ||
                        !wordBox.HasCompleteIdentity ||
                        !HasTrustedSense(wordBox.GetSenseID()) ||
                        !wordBox.GetUsageID().HasValue ||
                        !TrustedUsageDictionary.ContainsKey(wordBox.GetUsageID()!.Value))
                    {
                        return false;
                    }

                    WordIdentityKey wordIdentity = wordBox.GetIdentityKey();
                    if (TrustedWordIdentityIndex.ContainsKey(wordIdentity))
                    {
                        return false;
                    }
                    if (!batchWordIdentities.Add(wordIdentity))
                    {
                        return false;
                    }
                    wordIdsToPromote.Add(wordId);

                    foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                    {
                        if (TrustedSymbolDictionary.ContainsKey(symbolId))
                        {
                            continue;
                        }
                        if (seenSymbolIds.Contains(symbolId))
                        {
                            continue;
                        }
                        if (!UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                            !symbolBox.HasCompleteIdentity ||
                            !HasTrustedSense(symbolBox.GetSenseID()))
                        {
                            return false;
                        }

                        SymbolIdentityKey symbolIdentity = symbolBox.GetIdentityKey();
                        if (TrustedSymbolIdentityIndex.ContainsKey(symbolIdentity))
                        {
                            return false;
                        }
                        if (!batchSymbolIdentities.Add(symbolIdentity))
                        {
                            return false;
                        }
                        seenSymbolIds.Add(symbolId);
                        symbolIdsToPromote.Add(symbolId);
                    }
                }

                foreach (Guid symbolId in symbolIdsToPromote)
                {
                    if (!ConvertUntrustedSymbolToTrusted(symbolId))
                    {
                        return false;
                    }
                }
                foreach (Guid wordId in wordIdsToPromote)
                {
                    if (!ConvertUntrustedWordToTrusted(wordId))
                    {
                        return false;
                    }
                }
                return ConvertUntrustedSentenceToTrusted(sentenceId);
            });
        }

        public Guid? ResolveOrPromoteUntrustedSymbol(Guid symbolId)
        {
            return GraphSynchronization.Execute<Guid?>(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedSymbolDictionary.ContainsKey(symbolId)) return symbolId;
                if (!UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                    !symbolBox.HasCompleteIdentity || !HasTrustedSense(symbolBox.GetSenseID())) return null;

                SymbolIdentityKey identityKey = symbolBox.GetIdentityKey();
                if (!TrustedSymbolIdentityIndex.TryGetValue(identityKey, out Guid canonicalSymbolId))
                {
                    return ConvertUntrustedSymbolToTrusted(symbolId) ? symbolId : null;
                }

                List<WordBox> referencingWords = UntrustedWordDictionary.Values
                    .Where(word => (word.GetSymbols() ?? new List<Guid>()).Contains(symbolId))
                    .ToList();
                if (referencingWords.Any(word => word.IsIdentityFrozen)) return null;
                foreach (WordBox word in referencingWords)
                {
                    word.ReplaceSymbolReference(symbolId, canonicalSymbolId);
                }
                UntrustedSymbolDictionary.Remove(symbolId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Symbol, symbolId);
                return canonicalSymbolId;
            });
        }

        public Guid? ResolveOrPromoteUntrustedWord(Guid wordId)
        {
            return GraphSynchronization.Execute<Guid?>(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedWordDictionary.ContainsKey(wordId)) return wordId;
                if (!UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ||
                    !wordBox.HasCompleteIdentity || !HasTrustedSense(wordBox.GetSenseID()) ||
                    !wordBox.GetUsageID().HasValue || !TrustedUsageDictionary.ContainsKey(wordBox.GetUsageID()!.Value))
                {
                    return null;
                }

                List<Guid> symbolIds = (wordBox.GetSymbols() ?? new List<Guid>()).Distinct().ToList();
                foreach (Guid symbolId in symbolIds)
                {
                    if (TrustedSymbolDictionary.ContainsKey(symbolId)) continue;
                    if (!UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                        !symbolBox.HasCompleteIdentity || !HasTrustedSense(symbolBox.GetSenseID())) return null;
                }
                foreach (Guid symbolId in symbolIds)
                {
                    if (ResolveOrPromoteUntrustedSymbol(symbolId) == null) return null;
                }

                if (!UntrustedWordDictionary.TryGetValue(wordId, out wordBox) || !CanTrustWord(wordBox)) return null;
                WordIdentityKey identityKey = wordBox.GetIdentityKey();
                if (!TrustedWordIdentityIndex.TryGetValue(identityKey, out Guid canonicalWordId))
                {
                    return ConvertUntrustedWordToTrusted(wordId) ? wordId : null;
                }

                List<KeyValuePair<Guid, SentenceBox>> referencingSentences = UntrustedSentenceDictionary
                    .Where(entry => (entry.Value.GetWords() ?? new List<Guid>()).Contains(wordId))
                    .ToList();
                if (referencingSentences.Any(entry => entry.Value.IsIdentityFrozen)) return null;

                // Check for identity collisions before rewriting, since word replacement cannot be undone.
                HashSet<SentenceIdentityKey> projectedKeys = new HashSet<SentenceIdentityKey>();
                foreach (KeyValuePair<Guid, SentenceBox> entry in referencingSentences)
                {
                    if (!entry.Value.HasCompleteIdentity) continue;
                    List<Guid> projectedWords = (entry.Value.GetWords() ?? new List<Guid>())
                        .Select(id => id == wordId ? canonicalWordId : id)
                        .ToList();
                    SentenceIdentityKey projectedKey = new SentenceIdentityKey(projectedWords, entry.Value.GetSenseID()!.Value);
                    if (!projectedKeys.Add(projectedKey)) return null;
                    if (UntrustedSentenceIdentityIndex.TryGetValue(projectedKey, out Guid otherId) && otherId != entry.Key) return null;
                }

                foreach (KeyValuePair<Guid, SentenceBox> entry in referencingSentences)
                {
                    entry.Value.ReplaceWordReference(wordId, canonicalWordId);
                    ReindexUntrustedSentence(entry.Key, entry.Value);
                }
                UntrustedWordDictionary.Remove(wordId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Word, wordId);
                return canonicalWordId;
            });
        }

        public Guid? ResolveOrPromoteUntrustedSentenceSubgraph(Guid sentenceId)
        {
            return GraphSynchronization.Execute<Guid?>(() =>
            {
                EnsureMutableStorageMode();
                if (TrustedSentenceDictionary.ContainsKey(sentenceId)) return sentenceId;
                if (!UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ||
                    !sentenceBox.HasCompleteIdentity || !HasTrustedSense(sentenceBox.GetSenseID())) return null;

                List<Guid> wordIds = (sentenceBox.GetWords() ?? new List<Guid>()).Distinct().ToList();
                foreach (Guid wordId in wordIds)
                {
                    if (TrustedWordDictionary.ContainsKey(wordId)) continue;
                    if (!UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ||
                        !wordBox.HasCompleteIdentity || !HasTrustedSense(wordBox.GetSenseID()) ||
                        !wordBox.GetUsageID().HasValue || !TrustedUsageDictionary.ContainsKey(wordBox.GetUsageID()!.Value))
                    {
                        return null;
                    }
                    foreach (Guid symbolId in (wordBox.GetSymbols() ?? new List<Guid>()).Distinct())
                    {
                        if (TrustedSymbolDictionary.ContainsKey(symbolId)) continue;
                        if (!UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                            !symbolBox.HasCompleteIdentity || !HasTrustedSense(symbolBox.GetSenseID())) return null;
                    }
                }

                foreach (Guid wordId in wordIds)
                {
                    if (ResolveOrPromoteUntrustedWord(wordId) == null) return null;
                }
                if (!UntrustedSentenceDictionary.TryGetValue(sentenceId, out sentenceBox)) return null;
                SentenceIdentityKey identityKey = sentenceBox.GetIdentityKey();
                if (TrustedSentenceIdentityIndex.TryGetValue(identityKey, out Guid canonicalSentenceId))
                {
                    RemoveUntrustedSentenceEntry(sentenceId);
                    RemoveUntrustedNodeForTarget(NodeTargetKind.Sentence, sentenceId);
                    return canonicalSentenceId;
                }
                return ConvertUntrustedSentenceToTrusted(sentenceId) ? sentenceId : null;
            });
        }


        public bool SetUntrustedNode(Node node)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(node);
                Guid nodeId = node.GetNodeID();
                (NodeTargetKind Kind, Guid TargetId) targetKey = (node.GetTargetKind(), node.GetTargetID());
                NodeIdentityKey identityKey = node.GetIdentityKey();
                if (node.IsTrusted || TrustedNodeDictionary.ContainsKey(nodeId) ||
                    TrustedNodeByIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                if (UntrustedNodeByIdentityIndex.TryGetValue(identityKey, out Guid existingNodeId))
                {
                    if (existingNodeId == nodeId && UntrustedNodeDictionary.ContainsKey(nodeId))
                    {
                        UntrustedNodeDictionary[nodeId].SetIdentityChangingHandler(null);
                        UntrustedNodeDictionary[nodeId].SetIdentityChangedHandler(null);
                        UntrustedNodeDictionary[nodeId].SetReferenceCountsChangedHandler(null);
                        UntrustedNodeDictionary[nodeId] = node;
                        node.SetIdentityChangingHandler((oldKey, newKey) => HandleUntrustedNodeIdentityChange(nodeId, oldKey, newKey));
                        node.SetIdentityChangedHandler((oldKey, newKey) => HandleNodeIdentityChange(nodeId, oldKey, newKey));
                        node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
                        EnsureNodeRepresentation(node, isTrusted: false);
                        RecalculateAllNodePresentationsInternal();
                        return true;
                    }
                    return false;
                }

                if (UntrustedNodeDictionary.ContainsKey(nodeId))
                {
                    return false;
                }

                UntrustedNodeDictionary.Add(nodeId, node);
                UntrustedNodeByIdentityIndex.Add(identityKey, nodeId);
                if (identityKey.Label == null)
                {
                    UntrustedNodeByTargetIndex.Add(targetKey, nodeId);
                }
                node.SetIdentityChangingHandler((oldKey, newKey) => HandleUntrustedNodeIdentityChange(nodeId, oldKey, newKey));
                node.SetIdentityChangedHandler((oldKey, newKey) => HandleNodeIdentityChange(nodeId, oldKey, newKey));
                node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
                EnsureNodeRepresentation(node, isTrusted: false);
                RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        public bool AddUntrustedNode(Node node)
        {
            return SetUntrustedNode(node);
        }

        public bool RemoveUntrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node))
                {
                    return false;
                }

                UntrustedNodeDictionary.Remove(nodeId);
                NodeIdentityKey identityKey = node.GetIdentityKey();
                UntrustedNodeByIdentityIndex.Remove(identityKey);
                if (identityKey.Label == null)
                {
                    UntrustedNodeByTargetIndex.Remove((identityKey.TargetKind, identityKey.TargetId));
                }
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(null);
                RemoveNodeRepresentation(nodeId);
                RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        public bool ConvertUntrustedNodeToTrusted(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ||
                    node.IsTrusted || TrustedNodeDictionary.ContainsKey(nodeId) ||
                    !HasTrustedTarget(node.GetTargetKind(), node.GetTargetID()))
                {
                    return false;
                }

                NodeIdentityKey identityKey = node.GetIdentityKey();
                (NodeTargetKind Kind, Guid TargetId) targetKey = (identityKey.TargetKind, identityKey.TargetId);
                if (TrustedNodeByIdentityIndex.ContainsKey(identityKey))
                {
                    return false;
                }

                UntrustedNodeDictionary.Remove(nodeId);
                UntrustedNodeByIdentityIndex.Remove(identityKey);
                if (identityKey.Label == null)
                {
                    UntrustedNodeByTargetIndex.Remove(targetKey);
                    TrustedNodeByTargetIndex.Add(targetKey, nodeId);
                }
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
                node.MarkTrusted();
                TrustedNodeDictionary.Add(nodeId, node);
                TrustedNodeByIdentityIndex.Add(identityKey, nodeId);
                TrustedSkillsGraph.AddNode(nodeId);
                _graphEpoch = checked(_graphEpoch + 1);
                EnsureNodeRepresentation(node, isTrusted: true);
                if (identityKey.Label == null)
                {
                    WireNodeRelations(node);
                }
                RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        public bool AddTrustedLink(NodeLink link)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                bool added = !IsStructuralRelation(link.RelationType) && IsValidLinkKindCombination(link) &&
                             AreProvenanceTargetsTrusted(link.Provenance) && AddTrustedLinkInternal(link);
                if (added) RecalculateAllNodePresentationsInternal();
                return added;
            });
        }

        public bool AddTrustedLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            double weight = 1.0,
            string? label = null,
            NodeLinkProvenance? provenance = null)
        {
            return AddTrustedLink(new NodeLink(sourceNodeId, targetNodeId, relationType, weight, label, provenance));
        }

        /// <summary>Adds a provenance-backed reasoning assertion with a deterministic assertion ID.</summary>
        public bool AddReasoningLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            double confidence = 1.0,
            string? label = null,
            string? author = null,
            Guid? sourceTargetId = null,
            Guid? evidenceTargetId = null,
            DateTimeOffset? validFrom = null,
            DateTimeOffset? validUntil = null,
            Guid? sourceGroupId = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                NodeLinkProvenance provenance = NodeLinkProvenance.CreateReasoning(
                    sourceNodeId, targetNodeId, relationType, label, confidence, author,
                    sourceTargetId, evidenceTargetId, validFrom, validUntil, sourceGroupId);
                NodeLink link = new NodeLink(sourceNodeId, targetNodeId, relationType,
                    confidence, label, provenance);
                bool valid = !IsStructuralRelation(relationType) && IsValidLinkKindCombination(link) &&
                             AreProvenanceTargetsTrusted(provenance);
                bool added = valid && AddTrustedLinkInternal(link);
                bool recalculatedByUpsert = false;
                if (valid && !added && LinksByAssertionId.TryGetValue(provenance.AssertionId, out List<NodeLink>? existing))
                {
                    bool sameAssertion = existing.All(existingLink => SameReasoningAssertion(existingLink, link));
                    if (sameAssertion)
                    {
                        recalculatedByUpsert = existing[0].Weight != confidence;
                        added = !recalculatedByUpsert || UpdateTrustedLinkWeight(provenance.AssertionId, confidence);
                    }
                }
                if (added && !recalculatedByUpsert) RecalculateAllNodePresentationsInternal();
                return added;
            });
        }

        /// <summary>Begins a two-phase reasoning transaction that stages boxes, nodes, and assertions atomically.</summary>
        public ReasoningTransaction BeginReasoningTransaction()
        {
            GraphSynchronization.Execute(EnsureCoreInitialized);
            return new ReasoningTransaction(this);
        }

        /// <summary>Begins a reasoning write batch that commits staged items and presentation once.</summary>
        public ReasoningWriteBatch BeginReasoningWriteBatch()
        {
            GraphSynchronization.Execute(EnsureCoreInitialized);
            return new ReasoningWriteBatch(this);
        }

        private void CommitReasoningTransaction(
            IReadOnlyList<SenseBox> stagedSenses,
            IReadOnlyList<UsageBox> stagedUsages,
            IReadOnlyList<SymbolBox> stagedSymbols,
            IReadOnlyList<WordBox> stagedWords,
            IReadOnlyList<SentenceBox> stagedSentences,
            IReadOnlyList<GroupBox> stagedGroups,
            IReadOnlyList<SourceBox> stagedSources,
            IReadOnlyList<FunctionBox> stagedFunctions,
            IReadOnlyList<Node> stagedNodes,
            IReadOnlyList<NodeLink> pendingLinks)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();

                // Phase 1: Pre-validate staged boxes, nodes, and links against canonical + staged state before applying mutations.
                HashSet<Guid> newSenses = new HashSet<Guid>();
                foreach (SenseBox box in stagedSenses)
                {
                    Guid id = box.GetSenseID();
                    if (id == Guid.Empty || TrustedSenseDictionary.ContainsKey(id) || !newSenses.Add(id))
                        throw new InvalidOperationException($"Staged SenseBox '{id}' conflicts with an existing or duplicate identity.");
                }

                HashSet<Guid> newUsages = new HashSet<Guid>();
                foreach (UsageBox box in stagedUsages)
                {
                    Guid id = box.GetUsageID();
                    if (id == Guid.Empty || TrustedUsageDictionary.ContainsKey(id) || !newUsages.Add(id))
                        throw new InvalidOperationException($"Staged UsageBox '{id}' conflicts with an existing or duplicate identity.");
                }

                bool IsSenseAvailable(Guid? id) => id.HasValue && (HasTrustedSense(id) || newSenses.Contains(id.Value));
                bool IsUsageAvailable(Guid? id) => id.HasValue && (TrustedUsageDictionary.ContainsKey(id.Value) || newUsages.Contains(id.Value));

                HashSet<Guid> newSymbols = new HashSet<Guid>();
                HashSet<SymbolIdentityKey> newSymbolKeys = new HashSet<SymbolIdentityKey>();
                foreach (SymbolBox box in stagedSymbols)
                {
                    Guid id = box.GetSymbolID();
                    if (!box.HasCompleteIdentity || TrustedSymbolDictionary.ContainsKey(id) || !newSymbols.Add(id) || !IsSenseAvailable(box.GetSenseID()))
                        throw new InvalidOperationException($"Staged SymbolBox '{id}' has an incomplete, unresolved, or duplicate identity.");
                    SymbolIdentityKey key = box.GetIdentityKey();
                    if (TrustedSymbolIdentityIndex.ContainsKey(key) || !newSymbolKeys.Add(key))
                        throw new InvalidOperationException($"Staged SymbolBox '{id}' conflicts with an existing or duplicate symbol identity.");
                }

                bool IsSymbolAvailable(Guid id) => TrustedSymbolDictionary.ContainsKey(id) || newSymbols.Contains(id);

                HashSet<Guid> newWords = new HashSet<Guid>();
                HashSet<WordIdentityKey> newWordKeys = new HashSet<WordIdentityKey>();
                foreach (WordBox box in stagedWords)
                {
                    Guid id = box.GetWordID();
                    List<Guid>? symbols = box.GetSymbols();
                    if (!box.HasCompleteIdentity || TrustedWordDictionary.ContainsKey(id) || !newWords.Add(id) ||
                        !IsSenseAvailable(box.GetSenseID()) || !IsUsageAvailable(box.GetUsageID()) ||
                        symbols == null || !symbols.All(IsSymbolAvailable))
                    {
                        throw new InvalidOperationException($"Staged WordBox '{id}' has an incomplete, unresolved, or duplicate identity.");
                    }
                    if (!HasFinalWordClassification(box.GetSenseID(), box.GetUsageID(), box.GetPartOfSpeech()))
                    {
                        throw new InvalidOperationException($"Staged WordBox '{id}' has a placeholder sense, usage, or part of speech and cannot be trusted.");
                    }
                    WordIdentityKey key = box.GetIdentityKey();
                    if (TrustedWordIdentityIndex.ContainsKey(key) || !newWordKeys.Add(key))
                        throw new InvalidOperationException($"Staged WordBox '{id}' conflicts with an existing or duplicate word identity.");
                }

                bool IsWordAvailable(Guid id) => TrustedWordDictionary.ContainsKey(id) || newWords.Contains(id);

                HashSet<Guid> newSentences = new HashSet<Guid>();
                HashSet<SentenceIdentityKey> newSentenceKeys = new HashSet<SentenceIdentityKey>();
                foreach (SentenceBox box in stagedSentences)
                {
                    Guid id = box.GetSentenceID();
                    List<Guid>? words = box.GetWords();
                    if (!box.HasCompleteIdentity || TrustedSentenceDictionary.ContainsKey(id) || !newSentences.Add(id) ||
                        !IsSenseAvailable(box.GetSenseID()) || words == null || !words.All(IsWordAvailable))
                    {
                        throw new InvalidOperationException($"Staged SentenceBox '{id}' has an incomplete, unresolved, or duplicate identity.");
                    }
                    SentenceIdentityKey key = box.GetIdentityKey();
                    if (TrustedSentenceIdentityIndex.ContainsKey(key) || !newSentenceKeys.Add(key))
                        throw new InvalidOperationException($"Staged SentenceBox '{id}' conflicts with an existing or duplicate sentence identity.");
                }

                bool IsSentenceAvailable(Guid id) => TrustedSentenceDictionary.ContainsKey(id) || newSentences.Contains(id);

                HashSet<Guid> newGroups = new HashSet<Guid>();
                foreach (GroupBox box in stagedGroups)
                {
                    Guid id = box.GetGroupingID();
                    List<Guid>? sentences = box.GetSentences();
                    if (id == Guid.Empty || TrustedGroupDictionary.ContainsKey(id) || !newGroups.Add(id) ||
                        sentences == null || !sentences.All(IsSentenceAvailable))
                    {
                        throw new InvalidOperationException($"Staged GroupBox '{id}' has an unresolved sentence or duplicate identity.");
                    }
                }

                bool IsGroupAvailable(Guid id) => TrustedGroupDictionary.ContainsKey(id) || newGroups.Contains(id);

                HashSet<Guid> newSources = new HashSet<Guid>();
                foreach (SourceBox box in stagedSources)
                {
                    Guid id = box.GetSourceID();
                    List<Guid>? groups = box.GetGroupingIDs();
                    if (id == Guid.Empty || SourceDictionary.ContainsKey(id) || !newSources.Add(id) ||
                        groups == null || !groups.All(IsGroupAvailable))
                    {
                        throw new InvalidOperationException($"Staged SourceBox '{id}' has an unresolved group or duplicate identity.");
                    }
                }

                HashSet<Guid> newFunctions = new HashSet<Guid>();
                foreach (FunctionBox box in stagedFunctions)
                {
                    Guid id = box.GetFunctionID();
                    if (id == Guid.Empty || TrustedFunctionDictionary.ContainsKey(id) || !newFunctions.Add(id))
                        throw new InvalidOperationException($"Staged FunctionBox '{id}' conflicts with an existing or duplicate identity.");
                    MathFunctionDispatcher.ValidateDefinition(box);
                }

                bool IsTargetAvailable(NodeTargetKind kind, Guid targetId) => kind switch
                {
                    NodeTargetKind.Sense => IsSenseAvailable(targetId),
                    NodeTargetKind.Usage => IsUsageAvailable(targetId),
                    NodeTargetKind.Symbol => IsSymbolAvailable(targetId),
                    NodeTargetKind.Word => IsWordAvailable(targetId),
                    NodeTargetKind.Sentence => IsSentenceAvailable(targetId),
                    NodeTargetKind.Group => IsGroupAvailable(targetId),
                    NodeTargetKind.Source => SourceDictionary.ContainsKey(targetId) || newSources.Contains(targetId),
                    NodeTargetKind.Function => TrustedFunctionDictionary.ContainsKey(targetId) || newFunctions.Contains(targetId),
                    _ => false
                };

                HashSet<Guid> newNodeIds = new HashSet<Guid>();
                HashSet<NodeIdentityKey> newNodeKeys = new HashSet<NodeIdentityKey>();
                foreach (Node node in stagedNodes)
                {
                    Guid id = node.GetNodeID();
                    NodeIdentityKey key = node.GetIdentityKey();
                    if (node.IsTrusted || TrustedNodeDictionary.ContainsKey(id) || !newNodeIds.Add(id) ||
                        TrustedNodeByIdentityIndex.ContainsKey(key) || !newNodeKeys.Add(key) ||
                        !IsTargetAvailable(node.GetTargetKind(), node.GetTargetID()))
                    {
                        throw new InvalidOperationException($"Staged Node '{id}' has an invalid target or conflicting identity.");
                    }
                }

                // Phase 2: Apply mutations under suspended presentation recalculation with incremental rollback stack on failure.
                long initialEpoch = _graphEpoch;
                UInt128 initialNextPresentationId = _nextNodePresentationId;
                bool initialPresentationExhausted = _nodePresentationIdSpaceExhausted;
                Stack<Action> rollbackActions = new Stack<Action>();
                _nodePresentationRecalculationSuspendCount = checked(_nodePresentationRecalculationSuspendCount + 1);
                try
                {
                    void RegisterCanonicalOrStagedNode(NodeTargetKind kind, Guid targetId, Node? explicitNode = null)
                    {
                        (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
                        if (explicitNode == null && TrustedNodeByTargetIndex.ContainsKey(targetKey))
                        {
                            return;
                        }

                        Node? promotedFromUntrusted = null;
                        if (explicitNode != null)
                        {
                            if (UntrustedNodeDictionary.TryGetValue(explicitNode.GetNodeID(), out Node? existingUntrusted) &&
                                ReferenceEquals(existingUntrusted, explicitNode))
                            {
                                promotedFromUntrusted = existingUntrusted;
                            }
                        }
                        else if (UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid untrustedId) &&
                                 UntrustedNodeDictionary.TryGetValue(untrustedId, out Node? canonicalUntrusted))
                        {
                            promotedFromUntrusted = canonicalUntrusted;
                        }

                        Node node = explicitNode ?? promotedFromUntrusted ?? new Node(kind, targetId);
                        Guid nodeId = node.GetNodeID();
                        NodeIdentityKey identityKey = node.GetIdentityKey();
                        bool hadExistingPresentation = NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 prevPresId) &&
                                                       NodePresentation.TryGetValue(prevPresId, out NodeRepresentation? prevRep);
                        uint prevColor = hadExistingPresentation ? NodePresentation[prevPresId].GetColor() : 0;
                        double prevSize = hadExistingPresentation ? NodePresentation[prevPresId].GetSize() : 1.0;
                        string? prevLabel = hadExistingPresentation ? NodePresentation[prevPresId].GetTextLabel() : null;
                        Vector3 prevPos = hadExistingPresentation ? NodePresentation[prevPresId].GetPosition() : Vector3.Zero;

                        if (promotedFromUntrusted != null)
                        {
                            UntrustedNodeDictionary.Remove(nodeId);
                            UntrustedNodeByIdentityIndex.Remove(identityKey);
                            if (identityKey.Label == null)
                                UntrustedNodeByTargetIndex.Remove(targetKey);
                        }

                        node.SetIdentityChangingHandler(null);
                        node.SetIdentityChangedHandler(null);
                        node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
                        node.MarkTrusted();
                        TrustedNodeDictionary.Add(nodeId, node);
                        TrustedNodeByIdentityIndex.Add(identityKey, nodeId);
                        if (identityKey.Label == null)
                            TrustedNodeByTargetIndex.Add(targetKey, nodeId);
                        TrustedSkillsGraph.AddNode(nodeId);
                        EnsureNodeRepresentation(node, isTrusted: true);

                        List<NodeLink> wiredLinks = new List<NodeLink>();
                        List<Action> membershipRollbacks = new List<Action>();
                        if (identityKey.Label == null)
                        {
                            WireNodeRelationsTransactional(node, wiredLinks, membershipRollbacks);
                        }

                        rollbackActions.Push(() =>
                        {
                            for (int i = membershipRollbacks.Count - 1; i >= 0; i--)
                                membershipRollbacks[i]();
                            for (int i = wiredLinks.Count - 1; i >= 0; i--)
                                RemoveTrustedLinkInternal(wiredLinks[i]);
                            TrustedSkillsGraph.RemoveNode(nodeId);
                            if (identityKey.Label == null)
                                TrustedNodeByTargetIndex.Remove(targetKey);
                            TrustedNodeByIdentityIndex.Remove(identityKey);
                            TrustedNodeDictionary.Remove(nodeId);
                            node.UnmarkTrusted();
                            if (promotedFromUntrusted != null)
                            {
                                UntrustedNodeDictionary.Add(nodeId, node);
                                UntrustedNodeByIdentityIndex.Add(identityKey, nodeId);
                                if (identityKey.Label == null)
                                    UntrustedNodeByTargetIndex.Add(targetKey, nodeId);
                                node.SetIdentityChangingHandler((oldK, newK) => HandleUntrustedNodeIdentityChange(nodeId, oldK, newK));
                                node.SetIdentityChangedHandler((oldK, newK) => HandleNodeIdentityChange(nodeId, oldK, newK));
                                node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
                                if (hadExistingPresentation && NodePresentation.TryGetValue(prevPresId, out NodeRepresentation? rep))
                                {
                                    rep.SetColor(prevColor);
                                    rep.SetSize(prevSize);
                                    rep.SetTextLabel(prevLabel);
                                    rep.SetPosition(prevPos);
                                }
                            }
                            else if (!hadExistingPresentation)
                            {
                                RemoveNodeRepresentation(nodeId);
                            }
                        });
                    }

                    foreach (SenseBox box in stagedSenses)
                    {
                        Guid id = box.GetSenseID();
                        box.MarkTrusted();
                        TrustedSenseDictionary.Add(id, box);
                        rollbackActions.Push(() => TrustedSenseDictionary.Remove(id));
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Sense, id);
                    }

                    foreach (UsageBox box in stagedUsages)
                    {
                        Guid id = box.GetUsageID();
                        box.MarkTrusted();
                        TrustedUsageDictionary.Add(id, box);
                        rollbackActions.Push(() => TrustedUsageDictionary.Remove(id));
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Usage, id);
                    }

                    foreach (Node node in stagedNodes)
                    {
                        RegisterCanonicalOrStagedNode(node.GetTargetKind(), node.GetTargetID(), node);
                    }

                    foreach (SymbolBox box in stagedSymbols)
                    {
                        Guid id = box.GetSymbolID();
                        bool wasUntrusted = UntrustedSymbolDictionary.Remove(id);
                        SymbolIdentityKey key = box.GetIdentityKey();
                        box.FreezeIdentity();
                        TrustedSymbolDictionary.Add(id, box);
                        TrustedSymbolIdentityIndex.Add(key, id);
                        AddIndex(TrustedSymbolCharIndex, key.SymbolChar, id);
                        AddIndex(SymbolsBySenseIndex, key.SenseId, id);
                        rollbackActions.Push(() =>
                        {
                            RemoveFromIndex(SymbolsBySenseIndex, key.SenseId, id);
                            RemoveFromIndex(TrustedSymbolCharIndex, key.SymbolChar, id);
                            TrustedSymbolIdentityIndex.Remove(key);
                            TrustedSymbolDictionary.Remove(id);
                            if (wasUntrusted) UntrustedSymbolDictionary.Add(id, box);
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Symbol, id);
                    }

                    foreach (WordBox box in stagedWords)
                    {
                        Guid id = box.GetWordID();
                        bool wasUntrusted = UntrustedWordDictionary.Remove(id);
                        WordIdentityKey key = box.GetIdentityKey();
                        box.FreezeIdentity();
                        TrustedWordDictionary.Add(id, box);
                        TrustedWordIdentityIndex.Add(key, id);
                        AddIndex(WordsBySenseIndex, key.SenseId, id);
                        AddIndex(WordsByUsageIndex, key.UsageId, id);
                        List<Guid> symbols = box.GetSymbols() ?? new List<Guid>();
                        foreach (Guid symId in symbols) AddIndex(WordsBySymbolIndex, symId, id);
                        rollbackActions.Push(() =>
                        {
                            foreach (Guid symId in symbols) RemoveFromIndex(WordsBySymbolIndex, symId, id);
                            RemoveFromIndex(WordsByUsageIndex, key.UsageId, id);
                            RemoveFromIndex(WordsBySenseIndex, key.SenseId, id);
                            TrustedWordIdentityIndex.Remove(key);
                            TrustedWordDictionary.Remove(id);
                            if (wasUntrusted) UntrustedWordDictionary.Add(id, box);
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Word, id);
                    }

                    foreach (SentenceBox box in stagedSentences)
                    {
                        Guid id = box.GetSentenceID();
                        bool wasUntrusted = RemoveUntrustedSentenceEntry(id);
                        SentenceIdentityKey key = box.GetIdentityKey();
                        box.FreezeIdentity();
                        TrustedSentenceDictionary.Add(id, box);
                        TrustedSentenceIdentityIndex.Add(key, id);
                        AddIndex(SentencesBySenseIndex, key.SenseId, id);
                        List<Guid> words = box.GetWords() ?? new List<Guid>();
                        foreach (Guid wordId in words) AddIndex(SentencesByWordIndex, wordId, id);
                        rollbackActions.Push(() =>
                        {
                            foreach (Guid wordId in words) RemoveFromIndex(SentencesByWordIndex, wordId, id);
                            RemoveFromIndex(SentencesBySenseIndex, key.SenseId, id);
                            TrustedSentenceIdentityIndex.Remove(key);
                            TrustedSentenceDictionary.Remove(id);
                            if (wasUntrusted)
                            {
                                UntrustedSentenceDictionary.Add(id, box);
                                if (box.HasCompleteIdentity) IndexUntrustedSentenceKey(id, box.GetIdentityKey());
                            }
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Sentence, id);
                    }

                    foreach (GroupBox box in stagedGroups)
                    {
                        Guid id = box.GetGroupingID();
                        List<Guid> sentences = box.GetSentences() ?? new List<Guid>();
                        box.FreezeIdentity();
                        TrustedGroupDictionary.Add(id, box);
                        foreach (Guid sentId in sentences) AddIndex(GroupsBySentenceIndex, sentId, id);
                        rollbackActions.Push(() =>
                        {
                            foreach (Guid sentId in sentences) RemoveFromIndex(GroupsBySentenceIndex, sentId, id);
                            TrustedGroupDictionary.Remove(id);
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Group, id);
                    }

                    foreach (SourceBox box in stagedSources)
                    {
                        Guid id = box.GetSourceID();
                        List<Guid> groups = box.GetGroupingIDs() ?? new List<Guid>();
                        box.FreezeIdentity();
                        SourceDictionary.Add(id, box);
                        foreach (Guid grpId in groups) AddIndex(SourcesByGroupIndex, grpId, id);
                        rollbackActions.Push(() =>
                        {
                            foreach (Guid grpId in groups) RemoveFromIndex(SourcesByGroupIndex, grpId, id);
                            SourceDictionary.Remove(id);
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Source, id);
                    }

                    foreach (FunctionBox box in stagedFunctions)
                    {
                        Guid id = box.GetFunctionID();
                        bool wasUntrusted = UntrustedFunctionDictionary.Remove(id);
                        box.MarkTrusted();
                        TrustedFunctionDictionary.Add(id, box);
                        rollbackActions.Push(() =>
                        {
                            TrustedFunctionDictionary.Remove(id);
                            if (wasUntrusted) UntrustedFunctionDictionary.Add(id, box);
                        });
                        RegisterCanonicalOrStagedNode(NodeTargetKind.Function, id);
                    }

                    foreach (NodeLink link in pendingLinks)
                    {
                        if (IsStructuralRelation(link.RelationType) || !IsValidLinkKindCombination(link) ||
                            !AreProvenanceTargetsTrusted(link.Provenance))
                            throw new InvalidOperationException("A staged reasoning assertion has invalid endpoints, relation, or provenance references.");
                        if (!AddTrustedLinkInternal(link))
                            throw new InvalidOperationException($"Reasoning assertion '{link.Provenance.AssertionId}' could not be committed.");
                        Guid assertionId = link.Provenance.AssertionId;
                        rollbackActions.Push(() =>
                        {
                            if (!LinksByAssertionId.TryGetValue(assertionId, out List<NodeLink>? links)) return;
                            foreach (NodeLink added in new List<NodeLink>(links)) RemoveTrustedLinkInternal(added);
                        });
                    }

                    _graphEpoch = checked(initialEpoch + 1);
                }
                catch
                {
                    while (rollbackActions.Count > 0)
                    {
                        rollbackActions.Pop()();
                    }
                    _nextNodePresentationId = initialNextPresentationId;
                    _nodePresentationIdSpaceExhausted = initialPresentationExhausted;
                    _graphEpoch = initialEpoch;
                    throw;
                }
                finally
                {
                    _nodePresentationRecalculationSuspendCount--;
                    if (_nodePresentationRecalculationSuspendCount == 0)
                        RecalculateAllNodePresentationsInternal();
                }
            });
        }

        private void RemoveFromIndex<TKey>(Dictionary<TKey, OrderedGuidIndexList> index, TKey key, Guid value)
            where TKey : notnull
        {
            if (!index.TryGetValue(key, out OrderedGuidIndexList? values)) return;
            values.Remove(value);
            if (values.Count == 0) index.Remove(key);
        }

        private void WireNodeRelationsTransactional(Node node, List<NodeLink> wiredLinks, List<Action> membershipRollbacks)
        {
            if (node.GetLabel() != null) return;
            Guid nodeId = node.GetNodeID();
            Guid targetId = node.GetTargetID();

            void LinkIds(Guid srcId, Guid dstId, NodeRelationType rel)
            {
                if (srcId == Guid.Empty || dstId == Guid.Empty) return;
                NodeLinkProvenance prov = NodeLinkProvenance.CreateForLink(srcId, dstId, rel, null, NodeLinkOrigin.Structural, 1.0);
                NodeLink link = new NodeLink(srcId, dstId, rel, 1.0, null, prov);
                if (AddTrustedLinkInternal(link)) wiredLinks.Add(link);
            }

            void LinkToTarget(Guid srcId, NodeTargetKind kind, Guid tgtId, NodeRelationType rel)
            {
                Node? targetNode = GetTrustedNodeForTarget(kind, tgtId);
                if (targetNode != null) LinkIds(srcId, targetNode.GetNodeID(), rel);
            }

            switch (node.GetTargetKind())
            {
                case NodeTargetKind.Symbol:
                    if (TrustedSymbolDictionary.TryGetValue(targetId, out SymbolBox? symbolBox))
                    {
                        Guid? senseId = symbolBox.GetSenseID();
                        if (senseId.HasValue) LinkToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        foreach (Guid wordId in CopyIndexValues(WordsBySymbolIndex, targetId))
                            LinkIds(GetNodeIdForTarget(NodeTargetKind.Word, wordId), nodeId, NodeRelationType.ContainsSymbol);
                    }
                    break;
                case NodeTargetKind.Word:
                    if (TrustedWordDictionary.TryGetValue(targetId, out WordBox? wordBox))
                    {
                        Guid? senseId = wordBox.GetSenseID();
                        if (senseId.HasValue) LinkToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        Guid? usageId = wordBox.GetUsageID();
                        if (usageId.HasValue) LinkToTarget(nodeId, NodeTargetKind.Usage, usageId.Value, NodeRelationType.HasUsage);
                        foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                            LinkToTarget(nodeId, NodeTargetKind.Symbol, symbolId, NodeRelationType.ContainsSymbol);
                    }
                    foreach (Guid sentenceId in CopyIndexValues(SentencesByWordIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Sentence, sentenceId), nodeId, NodeRelationType.ContainsWord);
                    break;
                case NodeTargetKind.Sentence:
                    if (TrustedSentenceDictionary.TryGetValue(targetId, out SentenceBox? sentenceBox))
                    {
                        Guid? senseId = sentenceBox.GetSenseID();
                        if (senseId.HasValue) LinkToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        List<Guid> words = sentenceBox.GetWords() ?? new List<Guid>();
                        foreach (Guid wordId in words) LinkToTarget(nodeId, NodeTargetKind.Word, wordId, NodeRelationType.ContainsWord);
                        if (words.Count > 0)
                        {
                            LinkToTarget(nodeId, NodeTargetKind.Word, words[0], NodeRelationType.LeadingWord);
                            LinkToTarget(nodeId, NodeTargetKind.Word, words[words.Count - 1], NodeRelationType.TrailingWord);
                        }
                        if (senseId.HasValue && TrustedSenseDictionary.TryGetValue(senseId.Value, out SenseBox? senseBox))
                        {
                            if (senseBox.AddSentenceFromCore(targetId))
                                membershipRollbacks.Add(() => senseBox.RemoveSentenceFromCore(targetId));
                            if (words.Count > 0)
                            {
                                Guid leading = words[0];
                                Guid trailing = words[words.Count - 1];
                                if (senseBox.AddLeadingWordFromCore(leading))
                                    membershipRollbacks.Add(() => senseBox.RemoveLeadingWordFromCore(leading));
                                if (senseBox.AddTrailingWordFromCore(trailing))
                                    membershipRollbacks.Add(() => senseBox.RemoveTrailingWordFromCore(trailing));
                            }
                        }
                        foreach (Guid wordId in words)
                        {
                            if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? containedWord) &&
                                containedWord.GetUsageID().HasValue &&
                                TrustedUsageDictionary.TryGetValue(containedWord.GetUsageID()!.Value, out UsageBox? usageBox))
                            {
                                if (usageBox.AddSentenceFromCore(targetId))
                                    membershipRollbacks.Add(() => usageBox.RemoveSentenceFromCore(targetId));
                            }
                        }
                    }
                    foreach (Guid groupId in CopyIndexValues(GroupsBySentenceIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Group, groupId), nodeId, NodeRelationType.ContainsSentence);
                    break;
                case NodeTargetKind.Sense:
                    foreach (Guid symbolId in CopyIndexValues(SymbolsBySenseIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Symbol, symbolId), nodeId, NodeRelationType.HasSense);
                    foreach (Guid wordId in CopyIndexValues(WordsBySenseIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Word, wordId), nodeId, NodeRelationType.HasSense);
                    foreach (Guid sentenceId in CopyIndexValues(SentencesBySenseIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Sentence, sentenceId), nodeId, NodeRelationType.HasSense);
                    break;
                case NodeTargetKind.Usage:
                    foreach (Guid wordId in CopyIndexValues(WordsByUsageIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Word, wordId), nodeId, NodeRelationType.HasUsage);
                    break;
                case NodeTargetKind.Group:
                    if (TrustedGroupDictionary.TryGetValue(targetId, out GroupBox? groupBox))
                    {
                        foreach (Guid sentenceId in groupBox.GetSentences() ?? new List<Guid>())
                            LinkToTarget(nodeId, NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
                    }
                    foreach (Guid sourceId in CopyIndexValues(SourcesByGroupIndex, targetId))
                        LinkIds(GetNodeIdForTarget(NodeTargetKind.Source, sourceId), nodeId, NodeRelationType.ContainsGroup);
                    break;
                case NodeTargetKind.Source:
                    if (SourceDictionary.TryGetValue(targetId, out SourceBox? sourceBox))
                    {
                        foreach (Guid groupId in sourceBox.GetGroupingIDs() ?? new List<Guid>())
                            LinkToTarget(nodeId, NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
                    }
                    break;
            }
        }

        /// <summary>Updates confidence only when the assertion was created by the reasoning layer.</summary>
        public bool UpdateReasoningLinkWeight(Guid assertionId, double newConfidence)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (assertionId == Guid.Empty)
                    throw new ArgumentException("Assertion identifiers cannot be Guid.Empty.", nameof(assertionId));
                if (!double.IsFinite(newConfidence) || newConfidence < 0.0 || newConfidence > 1.0)
                    throw new ArgumentOutOfRangeException(nameof(newConfidence), "Confidence must be finite and in [0, 1].");
                List<NodeLink> matchingLinks = LinksByAssertionId.TryGetValue(assertionId, out List<NodeLink>? assertionLinks)
                    ? new List<NodeLink>(assertionLinks) : new List<NodeLink>();
                if (matchingLinks.Count == 0 || matchingLinks.Any(link => link.Provenance.Origin != NodeLinkOrigin.Reasoning))
                    return false;
                return UpdateTrustedLinkWeight(assertionId, newConfidence);
            });
        }

        /// <summary>Returns a deterministically ordered page of active outgoing links for a node.</summary>
        public List<NodeLink> GetOutgoingLinks(
            Guid nodeId,
            NodeRelationType? relationFilter = null,
            NodeLinkOrigin? originFilter = null,
            string? authorFilter = null,
            double? minimumConfidence = null,
            DateTimeOffset? activeAt = null,
            int? limit = null,
            int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                ValidateLinkQuery(relationFilter, originFilter, minimumConfidence);
                ValidatePaging(limit, offset);
                if (!TrustedOutgoingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                return PageLinks(links, relationFilter, originFilter, authorFilter, minimumConfidence, activeAt, limit, offset);
            });
        }

        /// <summary>Returns a deterministically ordered page of active incoming links for a node.</summary>
        public List<NodeLink> GetIncomingLinks(
            Guid nodeId,
            NodeRelationType? relationFilter = null,
            NodeLinkOrigin? originFilter = null,
            string? authorFilter = null,
            double? minimumConfidence = null,
            DateTimeOffset? activeAt = null,
            int? limit = null,
            int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                ValidateLinkQuery(relationFilter, originFilter, minimumConfidence);
                ValidatePaging(limit, offset);
                if (!TrustedIncomingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                return PageLinks(links, relationFilter, originFilter, authorFilter, minimumConfidence, activeAt, limit, offset);
            });
        }

        private List<NodeLink> PageLinks(
            IEnumerable<NodeLink> links,
            NodeRelationType? relationFilter,
            NodeLinkOrigin? originFilter,
            string? authorFilter,
            double? minimumConfidence,
            DateTimeOffset? activeAt,
            int? limit,
            int offset)
        {
            string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
            IEnumerable<NodeLink> query = links
                .Where(link => MatchesLinkQuery(link, relationFilter, originFilter,
                    normalizedAuthor, minimumConfidence, activeAt ?? DateTimeOffset.UtcNow))
                .OrderBy(link => link.Provenance.AssertionId)
                .ThenBy(link => link.SourceNodeId)
                .ThenBy(link => link.TargetNodeId);
            if (offset > 0) query = query.Skip(offset);
            if (limit.HasValue) query = query.Take(limit.Value);
            return query.ToList();
        }

        /// <summary>Builds an immutable Skills <see cref="Graph{TNode}"/> snapshot of trusted nodes and filtered confidence edges.</summary>
        public Graph<Guid> BuildSkillsGraphSnapshot(
            NodeRelationType? relationFilter = null,
            NodeLinkOrigin? originFilter = null,
            string? authorFilter = null,
            double? minimumConfidence = null,
            DateTimeOffset? activeAt = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                ValidateLinkQuery(relationFilter, originFilter, minimumConfidence);
                return BuildSkillsGraphSnapshotInternal(relationFilter, originFilter, authorFilter,
                    minimumConfidence, activeAt);
            });
        }

        /// <summary>Builds an immutable typed epoch snapshot of trusted nodes and active assertions.</summary>
        public TrustedKnowledgeGraphSnapshot BuildTrustedKnowledgeGraphSnapshot(DateTimeOffset? asOf = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                DateTimeOffset instant = asOf ?? DateTimeOffset.UtcNow;
                Dictionary<Guid, Node> nodeMap = new Dictionary<Guid, Node>();
                List<KnowledgeNodeSnapshot> nodes = new List<KnowledgeNodeSnapshot>();
                foreach (Guid nodeId in TrustedNodeByIdentityIndex.Values.Distinct())
                {
                    Node? node = GetTrustedNode(nodeId);
                    if (node == null) continue;
                    nodeMap[nodeId] = node;
                    nodes.Add(new KnowledgeNodeSnapshot(node.GetNodeID(), node.GetTargetKind(), node.GetTargetID(),
                        node.GetLabel(), node.GetVerifiedReferences(), node.GetInvalidReferences()));
                }
                nodes = nodes
                    .OrderBy(node => node.TargetKind).ThenBy(node => node.TargetId).ThenBy(node => node.Label, StringComparer.Ordinal)
                    .ToList();
                List<NodeLink> links = TrustedLinkKeys.Where(link => link.Provenance.IsActiveAt(instant))
                    .OrderBy(link => link.Provenance.AssertionId).ThenBy(link => link.SourceNodeId).ThenBy(link => link.TargetNodeId)
                    .ToList();
                return new TrustedKnowledgeGraphSnapshot(_graphEpoch, instant, nodes, links, nodeMap);
            });
        }

        /// <summary>Projects confidence edges to non-negative additive costs using -log(max(confidence, 1e-12)).</summary>
        public Graph<Guid> BuildSkillsCostGraphSnapshot(
            NodeRelationType? relationFilter = null,
            NodeLinkOrigin? originFilter = null,
            string? authorFilter = null,
            double? minimumConfidence = null,
            DateTimeOffset? activeAt = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                ValidateLinkQuery(relationFilter, originFilter, minimumConfidence);
                Graph<Guid> graph = new Graph<Guid>(true);
                foreach (Guid nodeId in TrustedNodeByIdentityIndex.Values.Distinct()) graph.AddNode(nodeId);
                string? author = NormalizeOptionalLinkText(authorFilter);
                DateTimeOffset instant = activeAt ?? DateTimeOffset.UtcNow;
                foreach (NodeLink link in TrustedLinkKeys)
                {
                    if (MatchesLinkQuery(link, relationFilter, originFilter, author, minimumConfidence, instant))
                    {
                        double cost = -Math.Log(Math.Max(link.Weight, 1e-12));
                        graph.AddEdge(link.SourceNodeId, link.TargetNodeId, cost);
                    }
                }
                return graph;
            });
        }

        /// <summary>Returns distinct active assertions whose provenance references the supplied trusted target.</summary>
        /// <param name="targetId">Trusted box or node identifier recorded as provenance.</param>
        /// <param name="includeSourceTarget">Whether to match the provenance source target.</param>
        /// <param name="includeEvidenceTarget">Whether to match the provenance evidence target.</param>
        /// <param name="originFilter">Optional assertion-origin filter.</param>
        /// <param name="activeAt">Instant for inclusive validity filtering; defaults to current UTC time.</param>
        /// <param name="limit">Optional maximum number of matching links to return.</param>
        /// <param name="offset">Number of matching links to skip after deterministic ordering.</param>
        /// <returns>A snapshot containing at most one directed link per matching assertion ID.</returns>
        public List<NodeLink> GetTrustedLinksByProvenanceTarget(
            Guid targetId,
            bool includeSourceTarget = true,
            bool includeEvidenceTarget = true,
            NodeLinkOrigin? originFilter = null,
            DateTimeOffset? activeAt = null,
            int? limit = null,
            int offset = 0)
        {
            if (targetId == Guid.Empty)
                throw new ArgumentException("Target identifiers cannot be Guid.Empty.", nameof(targetId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (originFilter.HasValue && !Enum.IsDefined(originFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(originFilter));
                ValidatePaging(limit, offset);
                IEnumerable<NodeLink> candidates = Enumerable.Empty<NodeLink>();
                if (includeSourceTarget && LinksBySourceTargetId.TryGetValue(targetId, out List<NodeLink>? sourceLinks))
                    candidates = candidates.Concat(sourceLinks);
                if (includeEvidenceTarget && LinksByEvidenceTargetId.TryGetValue(targetId, out List<NodeLink>? evidenceLinks))
                    candidates = candidates.Concat(evidenceLinks);
                DateTimeOffset instant = activeAt ?? DateTimeOffset.UtcNow;
                IEnumerable<NodeLink> query = candidates
                    .Where(link => (!originFilter.HasValue || link.Provenance.Origin == originFilter.Value) &&
                        link.Provenance.IsActiveAt(instant))
                    .GroupBy(link => link.Provenance.AssertionId)
                    .Select(group => group.First())
                    .OrderBy(link => link.Provenance.AssertionId)
                    .ThenBy(link => link.SourceNodeId)
                    .ThenBy(link => link.TargetNodeId);
                if (offset > 0) query = query.Skip(offset);
                if (limit.HasValue) query = query.Take(limit.Value);
                return query.ToList();
            });
        }

        /// <summary>Finds trusted nodes using target kind, label, trust weight, and incident-link filters.</summary>
        public List<Node> FindTrustedNodes(
            NodeTargetKind? kindFilter = null,
            string? labelContains = null,
            int? minimumNetTrustWeight = null,
            NodeRelationType? relationFilter = null,
            double? minimumIncidentLinkConfidence = null,
            bool canonicalOnly = false,
            DateTimeOffset? activeAt = null,
            int? limit = null,
            int offset = 0)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                if (relationFilter.HasValue && !Enum.IsDefined(relationFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(relationFilter));
                if (minimumIncidentLinkConfidence.HasValue &&
                    (!double.IsFinite(minimumIncidentLinkConfidence.Value) ||
                     minimumIncidentLinkConfidence.Value < 0.0 || minimumIncidentLinkConfidence.Value > 1.0))
                    throw new ArgumentOutOfRangeException(nameof(minimumIncidentLinkConfidence));
                ValidatePaging(limit, offset);

                string? normalizedLabel = NormalizeOptionalLinkText(labelContains);
                IEnumerable<Node> nodes = EnumerateTrustedNodesInternal();
                IEnumerable<Node> query = nodes
                    .Where(node => !kindFilter.HasValue || node.GetTargetKind() == kindFilter.Value)
                    .Where(node => !canonicalOnly || node.GetLabel() == null)
                    .Where(node => normalizedLabel == null ||
                        (node.GetLabel()?.Contains(normalizedLabel, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Where(node => !minimumNetTrustWeight.HasValue ||
                        node.GetNetTrustWeight() >= minimumNetTrustWeight.Value)
                    .Where(node => !relationFilter.HasValue && !minimumIncidentLinkConfidence.HasValue ||
                        HasMatchingIncidentLink(node.GetNodeID(), relationFilter, minimumIncidentLinkConfidence, activeAt ?? DateTimeOffset.UtcNow))
                    .OrderBy(node => node.GetTargetKind())
                    .ThenBy(node => node.GetTargetID())
                    .ThenBy(node => node.GetLabel(), StringComparer.Ordinal);
                if (offset > 0) query = query.Skip(offset);
                if (limit.HasValue) query = query.Take(limit.Value);
                return query.ToList();
            });
        }

        /// <summary>Writes all trusted foundation partitions, nodes, presentations, layout settings, and links to a versioned binary disk checkpoint.</summary>
        public void StreamTrustedDictionariesToDisk(string directoryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (_legacyDiskStoreReadOnly)
                {
                    throw new InvalidOperationException("A legacy checkpoint must be explicitly upgraded before writing.");
                }
                if (_diskPagedReadOnly) MaterializeDiskStore();
                Dictionary<DiskPartition, IEnumerable<DiskRecord>> partitions = BuildDiskRecords();
                using PartitionedBinaryStore store = PartitionedBinaryStore.Create(directoryPath, partitions);
                store.Validate();
            });
        }

        /// <summary>
        /// Loads a checkpoint or merges it into the current core. On merge, base layout
        /// options win by default; callers may opt to restore the incoming checkpoint's
        /// options (or its defaults if the record is absent) before derived recalculation.
        /// </summary>
        public void StreamTrustedDictionariesFromDisk(string directoryPath, bool replaceExisting = true,
            bool preloadAllIntoMemory = false, int maxCachedRecordsPerPartition = 128,
            LayoutOptionsMergePreference layoutOptionsMergePreference = LayoutOptionsMergePreference.BaseWins)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            if (!Enum.IsDefined(layoutOptionsMergePreference))
                throw new ArgumentOutOfRangeException(nameof(layoutOptionsMergePreference));
            GraphSynchronization.Execute(() =>
            {
                PartitionedBinaryStore? incoming = PartitionedBinaryStore.Open(directoryPath, maxCachedRecordsPerPartition);
                LRD_Core? staged = null;
                try
                {
                    DiskValidationProvenanceSummary validationSummary = ValidateDiskStoreContents(incoming);
                    if (!replaceExisting)
                    {
                        if (_legacyDiskStoreReadOnly)
                        {
                            throw new InvalidOperationException("A legacy checkpoint must be upgraded before merging another store.");
                        }
                        if (incoming.IsLegacyFormat)
                        {
                            throw new InvalidOperationException("Legacy checkpoints must be upgraded before they can be merged.");
                        }
                        string stagingPath = Path.Combine(Path.GetTempPath(), "open-lrd-merge-stage-" + Guid.NewGuid().ToString("N"));
                        PartitionedBinaryStore? baseStore = null;
                        LRD_Core? mergeStage = null;
                        try
                        {
                            baseStore = PartitionedBinaryStore.Create(stagingPath, BuildDiskRecords());
                            mergeStage = new LRD_Core();
                            mergeStage._diskStore = baseStore;
                            baseStore = null;
                            mergeStage._diskStorePath = stagingPath;
                            mergeStage._diskPagedReadOnly = true;
                            mergeStage._maxPagedObjects = Math.Max(0, maxCachedRecordsPerPartition) * Enum.GetValues<DiskPartition>().Length;
                            mergeStage.BuildPagedIndexesAndGraph(mergeStage._diskStore!);
                            mergeStage.MaterializeDiskStore();
                            mergeStage._nodePresentationRecalculationSuspendCount++;
                            try
                            {
                                foreach (Node existingUntrustedNode in UntrustedNodeDictionary.Values)
                                {
                                    Node clone = new Node(existingUntrustedNode.GetNodeID(), existingUntrustedNode.GetTargetKind(),
                                        existingUntrustedNode.GetTargetID(), existingUntrustedNode.GetLabel(), allowRegisteredTrustedId: true);
                                    clone.ReconcileReferenceCounts(existingUntrustedNode.GetVerifiedReferences(),
                                        existingUntrustedNode.GetInvalidReferences());
                                    mergeStage.SetUntrustedNode(clone);
                                    if (NodePresentationKeyByNodeId.TryGetValue(existingUntrustedNode.GetNodeID(), out UInt128 originalPresentationId) &&
                                        NodePresentation.TryGetValue(originalPresentationId, out NodeRepresentation? originalRepresentation))
                                    {
                                        mergeStage.RemoveNodeRepresentation(existingUntrustedNode.GetNodeID());
                                        UInt128 presentationId = mergeStage.NodePresentation.ContainsKey(originalPresentationId)
                                            ? mergeStage.AllocateNodePresentationId()
                                            : originalPresentationId;
                                        NodeRepresentation clonedRepresentation = new NodeRepresentation(
                                            clone.GetNodeID(),
                                            originalRepresentation.GetColor(),
                                            originalRepresentation.GetSize(),
                                            originalRepresentation.GetTextLabel(),
                                            originalRepresentation.GetPosition(),
                                            originalRepresentation.IsTextLabelCustomized);
                                        mergeStage.AddNodeRepresentation(presentationId, clonedRepresentation);
                                    }
                                }
                                mergeStage.MergeDiskStore(incoming, layoutOptionsMergePreference);
                            }
                            finally
                            {
                                mergeStage._nodePresentationRecalculationSuspendCount--;
                            }
                            mergeStage.RecalculateAllNodePresentationsInternal();
                            InstallMergedTrustedStateFrom(mergeStage);
                        }
                        finally
                        {
                            baseStore?.Dispose();
                            mergeStage?._diskStore?.Dispose();
                            if (Directory.Exists(stagingPath)) Directory.Delete(stagingPath, true);
                        }
                        return;
                    }

                    staged = new LRD_Core();
                    staged._diskStore = incoming;
                    incoming = null;
                    staged._diskStorePath = Path.GetFullPath(directoryPath);
                    staged._legacyDiskStoreReadOnly = staged._diskStore.IsLegacyFormat;
                    staged._maxPagedObjects = Math.Max(0, maxCachedRecordsPerPartition) * Enum.GetValues<DiskPartition>().Length;
                    staged._diskPagedReadOnly = true;
                    staged.BuildPagedIndexesAndGraph(staged._diskStore);
                    if (preloadAllIntoMemory) staged.MaterializeDiskStore();
                    InstallTrustedStateFrom(staged);
                    _lastDiskStoreValidationProvenanceSummary = validationSummary;
                    _lastDiskStoreValidationError = null;
                }
                finally
                {
                    incoming?.Dispose();
                    staged?._diskStore?.Dispose();
                }
            });
        }

        public void OpenDiskStore(string directoryPath, int maxCachedRecordsPerPartition = 128)
        {
            StreamTrustedDictionariesFromDisk(directoryPath, true, false, maxCachedRecordsPerPartition);
        }

        private void InstallTrustedStateFrom(LRD_Core staged)
        {
            long nextEpoch = checked(_graphEpoch + 1);
            ClearAllDictionaries();
            CopyTrustedStateFrom(staged);
            _diskStore = staged._diskStore;
            staged._diskStore = null;
            _diskStorePath = staged._diskStorePath;
            staged._diskStorePath = null;
            _diskPagedReadOnly = staged._diskPagedReadOnly;
            _legacyDiskStoreReadOnly = staged._legacyDiskStoreReadOnly;
            _maxPagedObjects = staged._maxPagedObjects;
            _graphEpoch = nextEpoch;
            _initialized = true;
        }

        private void InstallMergedTrustedStateFrom(LRD_Core staged)
        {
            long nextEpoch = checked(_graphEpoch + 1);
            string? existingDiskStorePath = _diskStorePath;
            int existingPagedObjectLimit = _maxPagedObjects;
            _diskStore?.Dispose();
            _diskStore = null;
            ClearPagedObjectCache();

            SourceDictionary.Clear();
            TrustedSymbolDictionary.Clear();
            TrustedUsageDictionary.Clear();
            TrustedSenseDictionary.Clear();
            TrustedWordDictionary.Clear();
            TrustedSentenceDictionary.Clear();
            TrustedGroupDictionary.Clear();
            TrustedFunctionDictionary.Clear();
            TrustedWordIdentityIndex.Clear();
            TrustedSentenceIdentityIndex.Clear();
            TrustedSymbolIdentityIndex.Clear();
            TrustedSymbolCharIndex.Clear();
            SymbolsBySenseIndex.Clear();
            WordsBySenseIndex.Clear();
            SentencesBySenseIndex.Clear();
            WordsByUsageIndex.Clear();
            WordsBySymbolIndex.Clear();
            SentencesByWordIndex.Clear();
            GroupsBySentenceIndex.Clear();
            SourcesByGroupIndex.Clear();
            foreach (Node node in EnumerateTrustedNodesInternal())
            {
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(null);
            }
            TrustedNodeDictionary.Clear();
            TrustedNodeByIdentityIndex.Clear();
            TrustedNodeByTargetIndex.Clear();
            NodePresentation.Clear();
            NodePresentationKeyByNodeId.Clear();
            _nextNodePresentationId = 0;
            _nodePresentationIdSpaceExhausted = false;
            TrustedOutgoingLinks.Clear();
            TrustedIncomingLinks.Clear();
            TrustedLinkKeys.Clear();
            LinksByAssertionId.Clear();
            LinksBySourceTargetId.Clear();
            LinksByEvidenceTargetId.Clear();
            TrustedSkillsGraph = new Graph<Guid>(true);

            foreach (Node node in UntrustedNodeDictionary.Values)
            {
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(null);
            }
            UntrustedNodeDictionary.Clear();
            UntrustedNodeByIdentityIndex.Clear();
            UntrustedNodeByTargetIndex.Clear();

            CopyTrustedStateFrom(staged);
            foreach (KeyValuePair<Guid, Node> entry in TrustedNodeDictionary)
            {
                Guid nodeId = entry.Key;
                entry.Value.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
            }
            CopyDictionary(staged.UntrustedNodeDictionary, UntrustedNodeDictionary);
            CopyDictionary(staged.UntrustedNodeByIdentityIndex, UntrustedNodeByIdentityIndex);
            CopyDictionary(staged.UntrustedNodeByTargetIndex, UntrustedNodeByTargetIndex);
            foreach (KeyValuePair<Guid, Node> entry in UntrustedNodeDictionary)
            {
                Guid nodeId = entry.Key;
                entry.Value.SetIdentityChangingHandler((oldKey, newKey) => HandleUntrustedNodeIdentityChange(nodeId, oldKey, newKey));
                entry.Value.SetIdentityChangedHandler((oldKey, newKey) => HandleNodeIdentityChange(nodeId, oldKey, newKey));
                entry.Value.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
            }

            foreach (Guid id in new List<Guid>(UntrustedSymbolDictionary.Keys))
                if (TrustedSymbolDictionary.ContainsKey(id)) UntrustedSymbolDictionary.Remove(id);
            foreach (Guid id in new List<Guid>(UntrustedWordDictionary.Keys))
                if (TrustedWordDictionary.ContainsKey(id)) UntrustedWordDictionary.Remove(id);
            foreach (Guid id in new List<Guid>(UntrustedSentenceDictionary.Keys))
                if (TrustedSentenceDictionary.ContainsKey(id)) RemoveUntrustedSentenceEntry(id);
            foreach (Guid id in new List<Guid>(UntrustedFunctionDictionary.Keys))
                if (TrustedFunctionDictionary.ContainsKey(id)) UntrustedFunctionDictionary.Remove(id);

            _diskStorePath = existingDiskStorePath;
            _diskPagedReadOnly = false;
            _legacyDiskStoreReadOnly = false;
            _maxPagedObjects = existingPagedObjectLimit;
            _graphEpoch = nextEpoch;
            _initialized = true;
        }

        private void CopyTrustedStateFrom(LRD_Core staged)
        {
            CopyDictionary(staged.SourceDictionary, SourceDictionary);
            CopyDictionary(staged.TrustedSymbolDictionary, TrustedSymbolDictionary);
            CopyDictionary(staged.TrustedUsageDictionary, TrustedUsageDictionary);
            CopyDictionary(staged.TrustedSenseDictionary, TrustedSenseDictionary);
            CopyDictionary(staged.TrustedWordDictionary, TrustedWordDictionary);
            CopyDictionary(staged.TrustedSentenceDictionary, TrustedSentenceDictionary);
            CopyDictionary(staged.TrustedGroupDictionary, TrustedGroupDictionary);
            CopyDictionary(staged.TrustedFunctionDictionary, TrustedFunctionDictionary);
            CopyDictionary(staged.TrustedSymbolIdentityIndex, TrustedSymbolIdentityIndex);
            CopyDictionary(staged.TrustedWordIdentityIndex, TrustedWordIdentityIndex);
            CopyDictionary(staged.TrustedSentenceIdentityIndex, TrustedSentenceIdentityIndex);
            CopyDictionary(staged.TrustedSymbolCharIndex, TrustedSymbolCharIndex);
            CopyDictionary(staged.SymbolsBySenseIndex, SymbolsBySenseIndex);
            CopyDictionary(staged.WordsBySenseIndex, WordsBySenseIndex);
            CopyDictionary(staged.SentencesBySenseIndex, SentencesBySenseIndex);
            CopyDictionary(staged.WordsByUsageIndex, WordsByUsageIndex);
            CopyDictionary(staged.WordsBySymbolIndex, WordsBySymbolIndex);
            CopyDictionary(staged.SentencesByWordIndex, SentencesByWordIndex);
            CopyDictionary(staged.GroupsBySentenceIndex, GroupsBySentenceIndex);
            CopyDictionary(staged.SourcesByGroupIndex, SourcesByGroupIndex);
            CopyDictionary(staged.TrustedNodeDictionary, TrustedNodeDictionary);
            CopyDictionary(staged.TrustedNodeByIdentityIndex, TrustedNodeByIdentityIndex);
            CopyDictionary(staged.TrustedNodeByTargetIndex, TrustedNodeByTargetIndex);
            CopyDictionary(staged.NodePresentation, NodePresentation);
            CopyDictionary(staged.NodePresentationKeyByNodeId, NodePresentationKeyByNodeId);
            _nextNodePresentationId = staged._nextNodePresentationId;
            _nodePresentationIdSpaceExhausted = staged._nodePresentationIdSpaceExhausted;
            _nodeLayoutOptions = new NodeLayoutOptions(staged._nodeLayoutOptions.NodeWeightBias,
                staged._nodeLayoutOptions.NeighborPositionBias);
            CopyDictionary(staged.TrustedOutgoingLinks, TrustedOutgoingLinks);
            CopyDictionary(staged.TrustedIncomingLinks, TrustedIncomingLinks);
            TrustedLinkKeys.UnionWith(staged.TrustedLinkKeys);
            CopyDictionary(staged.LinksByAssertionId, LinksByAssertionId);
            CopyDictionary(staged.LinksBySourceTargetId, LinksBySourceTargetId);
            CopyDictionary(staged.LinksByEvidenceTargetId, LinksByEvidenceTargetId);
            TrustedSkillsGraph = staged.TrustedSkillsGraph;
        }

        private static void CopyDictionary<TKey, TValue>(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> destination)
            where TKey : notnull
        {
            destination.Clear();
            foreach (KeyValuePair<TKey, TValue> entry in source)
            {
                destination.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>Materializes a read-only disk-paged checkpoint into writable in-memory trusted registries.</summary>
        public void MaterializeDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!_diskPagedReadOnly || _diskStore == null) return;
                PartitionedBinaryStore sourceStore = _diskStore;
                LRD_Core staged = new LRD_Core
                {
                    _diskStore = sourceStore,
                    _diskStorePath = _diskStorePath,
                    _diskPagedReadOnly = true,
                    _legacyDiskStoreReadOnly = _legacyDiskStoreReadOnly,
                    _maxPagedObjects = _maxPagedObjects
                };
                staged.BuildPagedIndexesAndGraph(sourceStore);
                staged.LoadMaterializedObjects(sourceStore);
                staged._diskStore = null;
                staged._diskPagedReadOnly = false;
                InstallTrustedStateFrom(staged);
            });
        }

        private void LoadMaterializedObjects(PartitionedBinaryStore store)
        {
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
                TrustedSymbolDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeSymbol(record.Payload, store.IsLegacyFormat));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Usages))
            {
                UsageBox box = FoundationBinarySerializer.DeserializeUsage(record.Payload);
                box.MarkTrusted();
                TrustedUsageDictionary.Add(record.Key, box);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Senses))
            {
                SenseBox box = FoundationBinarySerializer.DeserializeSense(record.Payload);
                box.MarkTrusted();
                TrustedSenseDictionary.Add(record.Key, box);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words))
                TrustedWordDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeWord(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences))
                TrustedSentenceDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeSentence(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups))
            {
                GroupBox box = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                box.FreezeIdentity();
                TrustedGroupDictionary.Add(record.Key, box);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources))
            {
                SourceBox box = FoundationBinarySerializer.DeserializeSource(record.Payload);
                box.FreezeIdentity();
                SourceDictionary.Add(record.Key, box);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Functions))
            {
                FunctionBox box = FoundationBinarySerializer.DeserializeFunction(record.Payload, store.IsLegacyFormat);
                box.MarkTrusted();
                TrustedFunctionDictionary.Add(record.Key, box);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
                TrustedNodeDictionary[record.Key] = FoundationBinarySerializer.DeserializeNode(record.Payload, false);
            foreach (NodeRepresentation representation in NodePresentation.Values)
            {
                representation.MarkWritable();
            }
            foreach (KeyValuePair<Guid, Node> entry in TrustedNodeDictionary)
            {
                Guid nodeId = entry.Key;
                entry.Value.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
            }
            RecalculateAllNodePresentationsInternal();
        }

        /// <summary>Closes and detaches the active disk store, optionally materializing paged records into memory first.</summary>
        public void CloseDiskStore(bool materialize = true)
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStore == null)
                {
                    if (materialize && !_legacyDiskStoreReadOnly) _diskStorePath = null;
                    return;
                }
                if (materialize)
                {
                    MaterializeDiskStore();
                }
                else
                {
                    if (_diskPagedReadOnly)
                        throw new InvalidOperationException("A paged disk store must be materialized before it can be detached.");
                    _diskStore.Dispose();
                    _diskStore = null;
                    _diskStorePath = null;
                    _diskPagedReadOnly = false;
                    ClearPagedObjectCache();
                }
            });
        }

        /// <summary>Flushes current trusted state to the attached disk-store checkpoint path.</summary>
        public void FlushTrustedDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStorePath == null) throw new InvalidOperationException("No disk store is attached.");
                StreamTrustedDictionariesToDisk(_diskStorePath);
            });
        }

        /// <summary>Compacts and rewrites the attached trusted disk-store checkpoint.</summary>
        public void CompactTrustedDiskStore()
        {
            FlushTrustedDiskStore();
        }

        /// <summary>Compacts and rewrites the attached disk-store checkpoint.</summary>
        public void CompactDiskStore() => CompactTrustedDiskStore();

        /// <summary>Validates partition checksums, cross-partition referential integrity, structural links, and function dispatch provenance for the attached disk store.</summary>
        public void ValidateDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStore == null) throw new InvalidOperationException("No disk store is attached.");
                try
                {
                    _diskStore.Validate();
                    _lastDiskStoreValidationProvenanceSummary = ValidateDiskStoreContents(_diskStore);
                    _lastDiskStoreValidationError = null;
                }
                catch (Exception exception)
                {
                    _lastDiskStoreValidationError = $"{exception.GetType().Name}: {exception.Message}";
                    _lastDiskStoreValidationProvenanceSummary = null;
                    throw;
                }
            });
        }

        /// <summary>Returns checkpoint partition sizes/counts, paged-cache counters, and the latest validation/provenance summary.</summary>
        public DiskStoreDiagnostics? GetDiskStoreDiagnostics()
        {
            return GraphSynchronization.Execute(() => _diskStore == null ? null : new DiskStoreDiagnostics(
                _diskStorePath, _diskStore.FormatVersion, _diskPagedReadOnly, _pagedCacheHits, _pagedCacheMisses,
                _lastDiskStoreValidationError, _lastDiskStoreValidationProvenanceSummary, _diskStore.GetPartitionDiagnostics()));
        }

        /// <summary>Gets whether the core is currently operating in read-only disk-paged mode.</summary>
        public bool IsDiskPagedReadOnly => GraphSynchronization.Execute(() => _diskPagedReadOnly);

        /// <summary>Gets whether the attached disk store is in legacy read-only format requiring upgrade before mutation.</summary>
        public bool IsLegacyDiskStoreReadOnly => GraphSynchronization.Execute(() => _legacyDiskStoreReadOnly);

        /// <summary>Materializes and upgrades an attached legacy read-only disk store to the current checkpoint format version.</summary>
        public void UpgradeLegacyDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!_legacyDiskStoreReadOnly || string.IsNullOrWhiteSpace(_diskStorePath))
                {
                    throw new InvalidOperationException("No legacy disk store is attached for upgrade.");
                }
                if (_diskPagedReadOnly)
                {
                    MaterializeDiskStore();
                }
                Dictionary<DiskPartition, IEnumerable<DiskRecord>> partitions = BuildDiskRecords();
                using PartitionedBinaryStore upgraded = PartitionedBinaryStore.Create(_diskStorePath, partitions);
                upgraded.Validate();
                _legacyDiskStoreReadOnly = false;
                _diskPagedReadOnly = false;
            });
        }

        private Dictionary<DiskPartition, IEnumerable<DiskRecord>> BuildDiskRecords()
        {
            if (_diskPagedReadOnly && _diskStore != null)
            {
                Dictionary<DiskPartition, IEnumerable<DiskRecord>> copied =
                    Enum.GetValues<DiskPartition>().ToDictionary(partition => partition,
                        partition => _diskStore.ReadAll(partition));
                List<DiskRecord> layoutRecords = copied[DiskPartition.LayoutSettings].ToList();
                if (layoutRecords.Count == 0)
                {
                    layoutRecords.Add(new DiskRecord(NodeLayoutOptions.RecordKey,
                        FoundationBinarySerializer.SerializeNodeLayoutOptions(_nodeLayoutOptions)));
                }
                copied[DiskPartition.LayoutSettings] = layoutRecords;
                return copied;
            }

            return new Dictionary<DiskPartition, IEnumerable<DiskRecord>>
            {
                [DiskPartition.Symbols] = ToDiskRecords(TrustedSymbolDictionary.Values, box => box.GetSymbolID(), FoundationBinarySerializer.SerializeSymbol),
                [DiskPartition.Words] = ToDiskRecords(TrustedWordDictionary.Values, box => box.GetWordID(), FoundationBinarySerializer.SerializeWord),
                [DiskPartition.Sentences] = ToDiskRecords(TrustedSentenceDictionary.Values, box => box.GetSentenceID(), FoundationBinarySerializer.SerializeSentence),
                [DiskPartition.Senses] = ToDiskRecords(TrustedSenseDictionary.Values, box => box.GetSenseID(), FoundationBinarySerializer.SerializeSense),
                [DiskPartition.Usages] = ToDiskRecords(TrustedUsageDictionary.Values, box => box.GetUsageID(), FoundationBinarySerializer.SerializeUsage),
                [DiskPartition.Groups] = ToDiskRecords(TrustedGroupDictionary.Values, box => box.GetGroupingID(), FoundationBinarySerializer.SerializeGroup),
                [DiskPartition.Sources] = ToDiskRecords(SourceDictionary.Values, box => box.GetSourceID(), FoundationBinarySerializer.SerializeSource),
                [DiskPartition.Functions] = ToDiskRecords(TrustedFunctionDictionary.Values, box => box.GetFunctionID(), FoundationBinarySerializer.SerializeFunction),
                [DiskPartition.Nodes] = ToDiskRecords(EnumerateTrustedNodesInternal(), box => box.GetNodeID(), FoundationBinarySerializer.SerializeNode),
                [DiskPartition.NodePresentations] = TrustedNodePresentationRecords(),
                [DiskPartition.LayoutSettings] = new[]
                {
                    new DiskRecord(NodeLayoutOptions.RecordKey,
                        FoundationBinarySerializer.SerializeNodeLayoutOptions(_nodeLayoutOptions))
                },
                [DiskPartition.Links] = TrustedOutgoingLinks.Values.SelectMany(links => links)
                    .Select(link => new DiskRecord(CreateLinkRecordId(link), FoundationBinarySerializer.SerializeLink(link)))
            };
        }

        private IEnumerable<DiskRecord> TrustedNodePresentationRecords()
        {
            HashSet<Guid> trustedNodeIds = TrustedNodeByIdentityIndex.Values.ToHashSet();
            foreach (KeyValuePair<UInt128, NodeRepresentation> entry in NodePresentation.OrderBy(pair => pair.Key))
            {
                NodeRepresentation representation = entry.Value;
                Guid nodeId = representation.GetNodeID();
                if (trustedNodeIds.Contains(nodeId) || TrustedNodeDictionary.ContainsKey(nodeId))
                {
                    yield return new DiskRecord(
                        nodeId,
                        FoundationBinarySerializer.SerializeNodeRepresentation(entry.Key, representation));
                }
            }
        }

        private static Guid CreateLinkRecordId(NodeLink link)
        {
            byte[] identity = new byte[52];
            link.Provenance.AssertionId.TryWriteBytes(identity.AsSpan(0, 16));
            link.SourceNodeId.TryWriteBytes(identity.AsSpan(16, 16));
            link.TargetNodeId.TryWriteBytes(identity.AsSpan(32, 16));
            BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(48, 4), (int)link.RelationType);
            byte[] hash = SHA256.HashData(identity);
            Guid key = new Guid(hash.AsSpan(0, 16));
            return key == Guid.Empty ? new Guid(hash.AsSpan(16, 16)) : key;
        }

        private static IEnumerable<DiskRecord> ToDiskRecords<T>(IEnumerable<T> boxes, Func<T, Guid> getId,
            Func<T, byte[]> serialize)
        {
            foreach (T box in boxes) yield return new DiskRecord(getId(box), serialize(box));
        }

        private DiskValidationProvenanceSummary ValidateDiskStoreContents(PartitionedBinaryStore store)
        {
            Dictionary<Guid, SymbolBox> symbolBoxes = ReadDiskBoxes(store, DiskPartition.Symbols,
                payload => FoundationBinarySerializer.DeserializeSymbol(payload, store.IsLegacyFormat),
                box => box.GetSymbolID(), "Symbol");
            Dictionary<Guid, SenseBox> senseBoxes = ReadDiskBoxes(store, DiskPartition.Senses,
                FoundationBinarySerializer.DeserializeSense, box => box.GetSenseID(), "Sense");
            Dictionary<Guid, UsageBox> usageBoxes = ReadDiskBoxes(store, DiskPartition.Usages,
                FoundationBinarySerializer.DeserializeUsage, box => box.GetUsageID(), "Usage");
            Dictionary<Guid, WordBox> wordBoxes = ReadDiskBoxes(store, DiskPartition.Words,
                FoundationBinarySerializer.DeserializeWord, box => box.GetWordID(), "Word");
            Dictionary<Guid, SentenceBox> sentenceBoxes = ReadDiskBoxes(store, DiskPartition.Sentences,
                FoundationBinarySerializer.DeserializeSentence, box => box.GetSentenceID(), "Sentence");
            Dictionary<Guid, GroupBox> groupBoxes = ReadDiskBoxes(store, DiskPartition.Groups,
                FoundationBinarySerializer.DeserializeGroup, box => box.GetGroupingID(), "Group");
            Dictionary<Guid, SourceBox> sourceBoxes = ReadDiskBoxes(store, DiskPartition.Sources,
                FoundationBinarySerializer.DeserializeSource, box => box.GetSourceID(), "Source");
            Dictionary<Guid, FunctionBox> functionBoxes = ReadDiskBoxes(store, DiskPartition.Functions,
                payload => FoundationBinarySerializer.DeserializeFunction(payload, store.IsLegacyFormat),
                box => box.GetFunctionID(), "Function");

            HashSet<Guid> symbols = symbolBoxes.Keys.ToHashSet();
            HashSet<Guid> words = wordBoxes.Keys.ToHashSet();
            HashSet<Guid> sentences = sentenceBoxes.Keys.ToHashSet();
            HashSet<Guid> senses = senseBoxes.Keys.ToHashSet();
            HashSet<Guid> usages = usageBoxes.Keys.ToHashSet();
            HashSet<Guid> groups = groupBoxes.Keys.ToHashSet();
            HashSet<Guid> sources = sourceBoxes.Keys.ToHashSet();
            HashSet<Guid> functions = functionBoxes.Keys.ToHashSet();
            HashSet<Guid> nodes = new HashSet<Guid>();
            Dictionary<Guid, NodeTargetKind> nodeKinds = new Dictionary<Guid, NodeTargetKind>();
            Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> canonicalNodeIds =
                new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
            HashSet<NodeIdentityKey> nodeIdentities = new HashSet<NodeIdentityKey>();
            HashSet<(NodeTargetKind Kind, Guid TargetId)> canonicalNodeTargets = new HashSet<(NodeTargetKind Kind, Guid TargetId)>();
            HashSet<NodeLink> linkIdentities = new HashSet<NodeLink>();
            Dictionary<Guid, List<NodeLink>> assertionLinks = new Dictionary<Guid, List<NodeLink>>();

            if (store.HasPartition(DiskPartition.LayoutSettings))
            {
                List<DiskRecord> layoutRecords = store.ReadAll(DiskPartition.LayoutSettings).ToList();
                if (layoutRecords.Count > 0)
                {
                    if (layoutRecords.Count != 1 || layoutRecords[0].Key != NodeLayoutOptions.RecordKey)
                        throw new InvalidDataException("LayoutSettings partition contains an invalid record set.");
                    try
                    {
                        _ = FoundationBinarySerializer.DeserializeNodeLayoutOptions(layoutRecords[0].Payload);
                    }
                    catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
                    {
                        throw new InvalidDataException("LayoutSettings partition contains invalid bias values.", exception);
                    }
                }
            }

            HashSet<SymbolIdentityKey> symbolIdentities = new HashSet<SymbolIdentityKey>();
            foreach (SymbolBox box in symbolBoxes.Values)
            {
                if (!box.HasCompleteIdentity || !symbolIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Symbol partition contains a duplicate or invalid identity.");
            }
            HashSet<WordIdentityKey> wordIdentities = new HashSet<WordIdentityKey>();
            foreach (WordBox box in wordBoxes.Values)
            {
                if (!box.HasCompleteIdentity || !wordIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Word partition contains a duplicate or invalid identity.");
                if (!box.GetSenseID().HasValue || !senses.Contains(box.GetSenseID()!.Value) ||
                    !box.GetUsageID().HasValue || !usages.Contains(box.GetUsageID()!.Value) ||
                    !(box.GetSymbols() ?? new List<Guid>()).All(symbols.Contains))
                    throw new InvalidDataException("Word partition contains unresolved references.");
            }
            HashSet<SentenceIdentityKey> sentenceIdentities = new HashSet<SentenceIdentityKey>();
            foreach (SentenceBox box in sentenceBoxes.Values)
            {
                if (!box.HasCompleteIdentity || !sentenceIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Sentence partition contains a duplicate or invalid identity.");
                if (!box.GetSenseID().HasValue || !senses.Contains(box.GetSenseID()!.Value) ||
                    !(box.GetWords() ?? new List<Guid>()).All(words.Contains))
                    throw new InvalidDataException("Sentence partition contains unresolved references.");
            }
            foreach (SenseBox box in senseBoxes.Values)
            {
                if (!(box.GetSentences() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Sense partition contains an unresolved sentence membership.");
                if (!(box.GetLeadingWords() ?? new List<Guid>()).All(words.Contains) ||
                    !(box.GetTrailingWords() ?? new List<Guid>()).All(words.Contains))
                    throw new InvalidDataException("Sense partition contains an unresolved leading/trailing word.");
                if (!float.IsFinite(box.GetAverageWordPosition()) || !float.IsFinite(box.GetAverageSentencePosition()) ||
                    !float.IsFinite(box.GetAverageSpread()))
                    throw new InvalidDataException("Sense partition contains a non-finite metric.");
            }
            foreach (UsageBox box in usageBoxes.Values)
            {
                if (!(box.GetSentences() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Usage partition contains an unresolved sentence membership.");
            }
            foreach (GroupBox box in groupBoxes.Values)
            {
                if (!(box.GetSentenceIDs() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Group partition contains an unresolved sentence.");
            }
            foreach (SourceBox box in sourceBoxes.Values)
            {
                if (!(box.GetGroupingIDs() ?? new List<Guid>()).All(groups.Contains))
                    throw new InvalidDataException("Source partition contains an unresolved group.");
            }
            int verifiedProvenanceCount = 0;
            int unverifiedProvenanceCount = 0;
            int tamperedProvenanceCount = 0;
            int stagedNonDeterministicCount = 0;
            List<Guid> verifiedIds = new List<Guid>();
            List<Guid> unverifiedIds = new List<Guid>();
            List<Guid> tamperedIds = new List<Guid>();
            List<Guid> stagedNonDeterministicIds = new List<Guid>();
            foreach (FunctionBox box in functionBoxes.Values)
            {
                try
                {
                    MathFunctionDispatcher.ValidateDefinition(box);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
                {
                    throw new InvalidDataException("Function partition contains an incomplete or invalid trusted definition.", exception);
                }
                ClassifyFunctionProvenance(
                    box,
                    ref verifiedProvenanceCount,
                    ref unverifiedProvenanceCount,
                    ref tamperedProvenanceCount,
                    ref stagedNonDeterministicCount,
                    verifiedIds,
                    unverifiedIds,
                    tamperedIds,
                    stagedNonDeterministicIds);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
            {
                Node node = FoundationBinarySerializer.DeserializeNode(record.Payload, false);
                NodeIdentityKey identityKey = node.GetIdentityKey();
                if (record.Key != node.GetNodeID() || !nodes.Add(record.Key) || !node.IsTrusted || !nodeIdentities.Add(identityKey))
                    throw new InvalidDataException("Node partition contains a duplicate, untrusted, or invalid node.");
                nodeKinds.Add(record.Key, node.GetTargetKind());
                if (identityKey.Label == null && !canonicalNodeTargets.Add((identityKey.TargetKind, identityKey.TargetId)))
                    throw new InvalidDataException("Node partition contains duplicate canonical nodes for a target.");
                if (identityKey.Label == null)
                {
                    canonicalNodeIds.Add((identityKey.TargetKind, identityKey.TargetId), record.Key);
                }
                if (!DiskTargetExists(node.GetTargetKind(), node.GetTargetID(), symbols, words, sentences, senses, usages, groups, sources, functions))
                    throw new InvalidDataException("Node partition contains a node with an unresolved target.");
            }
            if (store.HasPartition(DiskPartition.NodePresentations))
            {
                HashSet<Guid> presentedNodes = new HashSet<Guid>();
                HashSet<UInt128> presentationIds = new HashSet<UInt128>();
                foreach (DiskRecord record in store.ReadAll(DiskPartition.NodePresentations))
                {
                    (UInt128 presentationId, NodeRepresentation representation) =
                        FoundationBinarySerializer.DeserializeNodeRepresentation(record.Payload);
                    Guid nodeId = representation.GetNodeID();
                    if (record.Key != nodeId || !nodes.Contains(nodeId) ||
                        !presentedNodes.Add(nodeId) || !presentationIds.Add(presentationId))
                    {
                        throw new InvalidDataException("NodePresentation partition contains a duplicate, unresolved, or mismatched identity.");
                    }
                }
                if (!presentedNodes.SetEquals(nodes))
                {
                    throw new InvalidDataException("NodePresentation partition must contain exactly one record per trusted Node.");
                }
            }
            RequireCanonicalNodes(NodeTargetKind.Symbol, symbols, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Word, words, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Sentence, sentences, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Sense, senses, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Usage, usages, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Group, groups, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Source, sources, canonicalNodeTargets);
            RequireCanonicalNodes(NodeTargetKind.Function, functions, canonicalNodeTargets);
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload, store.IsLegacyFormat);
                if (!nodes.Contains(link.SourceNodeId) || !nodes.Contains(link.TargetNodeId))
                    throw new InvalidDataException("Link partition contains an unresolved endpoint.");
                if (!IsValidLinkKindCombination(nodeKinds[link.SourceNodeId], nodeKinds[link.TargetNodeId], link))
                    throw new InvalidDataException("Link partition contains an invalid relation/target-kind combination.");
                if ((link.Provenance.SourceTargetId.HasValue && !DiskTargetExistsAny(link.Provenance.SourceTargetId.Value,
                        symbols, words, sentences, senses, usages, groups, sources, functions)) ||
                    (link.Provenance.EvidenceTargetId.HasValue && !DiskTargetExistsAny(link.Provenance.EvidenceTargetId.Value,
                        symbols, words, sentences, senses, usages, groups, sources, functions)))
                {
                    throw new InvalidDataException("Link provenance references an unresolved Foundation target.");
                }
                if (!linkIdentities.Add(link))
                    throw new InvalidDataException("Link partition contains a duplicate edge identity.");
                if (IsStructuralRelation(link.RelationType) && !store.IsLegacyFormat &&
                    (link.Provenance.Origin is not (NodeLinkOrigin.Structural or NodeLinkOrigin.LegacyImport) ||
                     link.Label != null || link.Weight != 1.0))
                {
                    throw new InvalidDataException("Structural links must have structural provenance, no label, and unit confidence.");
                }
                if (!store.IsLegacyFormat && record.Key != CreateLinkRecordId(link))
                    throw new InvalidDataException("Link record key does not match its assertion identity and direction.");
                if (!assertionLinks.TryGetValue(link.Provenance.AssertionId, out List<NodeLink>? linksForAssertion))
                {
                    linksForAssertion = new List<NodeLink>();
                    assertionLinks.Add(link.Provenance.AssertionId, linksForAssertion);
                }
                linksForAssertion.Add(link);
            }

            HashSet<(Guid Source, Guid Target, NodeRelationType Relation)> expectedStructuralLinks =
                new HashSet<(Guid Source, Guid Target, NodeRelationType Relation)>();
            foreach (KeyValuePair<Guid, SymbolBox> entry in symbolBoxes)
            {
                if (entry.Value.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Symbol,
                        entry.Key, NodeTargetKind.Sense, entry.Value.GetSenseID()!.Value, NodeRelationType.HasSense);
            }
            foreach (KeyValuePair<Guid, WordBox> entry in wordBoxes)
            {
                WordBox box = entry.Value;
                if (box.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        entry.Key, NodeTargetKind.Sense, box.GetSenseID()!.Value, NodeRelationType.HasSense);
                if (box.GetUsageID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        entry.Key, NodeTargetKind.Usage, box.GetUsageID()!.Value, NodeRelationType.HasUsage);
                foreach (Guid symbolId in box.GetSymbols() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        entry.Key, NodeTargetKind.Symbol, symbolId, NodeRelationType.ContainsSymbol);
            }
            foreach (KeyValuePair<Guid, SentenceBox> entry in sentenceBoxes)
            {
                SentenceBox box = entry.Value;
                if (box.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        entry.Key, NodeTargetKind.Sense, box.GetSenseID()!.Value, NodeRelationType.HasSense);
                List<Guid> wordIds = box.GetWords() ?? new List<Guid>();
                foreach (Guid wordId in wordIds)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        entry.Key, NodeTargetKind.Word, wordId, NodeRelationType.ContainsWord);
                if (wordIds.Count > 0)
                {
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        entry.Key, NodeTargetKind.Word, wordIds[0], NodeRelationType.LeadingWord);
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        entry.Key, NodeTargetKind.Word, wordIds[^1], NodeRelationType.TrailingWord);
                }
            }
            foreach (KeyValuePair<Guid, GroupBox> entry in groupBoxes)
            {
                foreach (Guid sentenceId in entry.Value.GetSentenceIDs() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Group,
                        entry.Key, NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
            }
            foreach (KeyValuePair<Guid, SourceBox> entry in sourceBoxes)
            {
                foreach (Guid groupId in entry.Value.GetGroupingIDs() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Source,
                        entry.Key, NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
            }

            List<(Guid Source, Guid Target, NodeRelationType Relation)> actualStructuralLinkList = assertionLinks.Values
                .SelectMany(linkList => linkList)
                .Where(link => IsStructuralRelation(link.RelationType))
                .Select(link => (link.SourceNodeId, link.TargetNodeId, link.RelationType))
                .ToList();
            HashSet<(Guid Source, Guid Target, NodeRelationType Relation)> actualStructuralLinks =
                actualStructuralLinkList.ToHashSet();
            if (actualStructuralLinks.Count != actualStructuralLinkList.Count)
                throw new InvalidDataException("Structural links contain duplicate membership edges.");
            if (!actualStructuralLinks.SetEquals(expectedStructuralLinks))
                throw new InvalidDataException("Structural links do not match trusted Foundation memberships.");

            foreach (List<NodeLink> linksForAssertion in assertionLinks.Values)
            {
                if (linksForAssertion.Count <= 1) continue;
                if (linksForAssertion.Count != 2)
                    throw new InvalidDataException("An assertion ID is used by more than one link pair.");
                NodeLink first = linksForAssertion[0];
                NodeLink second = linksForAssertion[1];
                if (!ShouldStoreReverse(first.RelationType) || first.RelationType != second.RelationType ||
                    first.SourceNodeId != second.TargetNodeId || first.TargetNodeId != second.SourceNodeId ||
                    first.Weight != second.Weight || !string.Equals(first.Label, second.Label, StringComparison.Ordinal) ||
                    first.Provenance != second.Provenance)
                {
                    throw new InvalidDataException("Duplicate assertion IDs are allowed only on matching symmetric reverse links.");
                }
            }
            return new DiskValidationProvenanceSummary(
                verifiedProvenanceCount,
                unverifiedProvenanceCount,
                tamperedProvenanceCount,
                stagedNonDeterministicCount,
                verifiedIds.AsReadOnly(),
                unverifiedIds.AsReadOnly(),
                tamperedIds.AsReadOnly(),
                stagedNonDeterministicIds.AsReadOnly());
        }

        private static void ClassifyFunctionProvenance(
            FunctionBox box,
            ref int verified,
            ref int unverified,
            ref int tampered,
            ref int stagedNonDeterministic,
            List<Guid> verifiedIds,
            List<Guid> unverifiedIds,
            List<Guid> tamperedIds,
            List<Guid> stagedNonDeterministicIds)
        {
            Guid funcId = box.GetFunctionID();
            FunctionDispatchProvenance? provenance = box.DispatchProvenance;
            if (provenance == null)
            {
                unverified++;
                unverifiedIds.Add(funcId);
                return;
            }
            if (!provenance.IsDeterministic)
            {
                stagedNonDeterministic++;
                stagedNonDeterministicIds.Add(funcId);
                return;
            }
            string expectedArgHash = MathFunctionDispatcher.ComputeArgumentSnapshotHash(box);
            string expectedResultHash = MathFunctionDispatcher.ComputeResultHash(box.Result!);
            string expectedResultType = box.Result!.DataType.AssemblyQualifiedName ?? box.Result.DataType.FullName ?? box.Result.DataType.Name;
            bool hashMismatch = !string.Equals(provenance.ArgumentSnapshotHash, expectedArgHash, StringComparison.Ordinal) ||
                !string.Equals(provenance.ResultHash, expectedResultHash, StringComparison.Ordinal) ||
                provenance.DefinitionRevision != box.ResultDefinitionRevision ||
                !string.Equals(provenance.ResultType, expectedResultType, StringComparison.Ordinal) ||
                !string.Equals(provenance.ResultUnitTitle ?? string.Empty, box.Result.UnitTitle ?? string.Empty, StringComparison.Ordinal);
            if (hashMismatch)
            {
                tampered++;
                tamperedIds.Add(funcId);
                return;
            }
            if (box.IsResultDispatchVerified && provenance.IsDeterministic)
            {
                verified++;
                verifiedIds.Add(funcId);
            }
            else
            {
                unverified++;
                unverifiedIds.Add(funcId);
            }
        }

        private static Dictionary<Guid, T> ReadDiskBoxes<T>(PartitionedBinaryStore store, DiskPartition partition,
            Func<byte[], T> deserialize, Func<T, Guid> getId, string label)
        {
            Dictionary<Guid, T> boxes = new Dictionary<Guid, T>();
            foreach (DiskRecord record in store.ReadAll(partition))
            {
                T box = deserialize(record.Payload);
                Guid id = getId(box);
                if (record.Key != id || !boxes.TryAdd(id, box))
                    throw new InvalidDataException($"{label} partition contains a duplicate or invalid ID.");
            }
            return boxes;
        }

        private static bool DiskTargetExists(NodeTargetKind kind, Guid id, HashSet<Guid> symbols, HashSet<Guid> words,
            HashSet<Guid> sentences, HashSet<Guid> senses, HashSet<Guid> usages, HashSet<Guid> groups,
            HashSet<Guid> sources, HashSet<Guid> functions) => kind switch
        {
            NodeTargetKind.Symbol => symbols.Contains(id), NodeTargetKind.Word => words.Contains(id),
            NodeTargetKind.Sentence => sentences.Contains(id), NodeTargetKind.Sense => senses.Contains(id),
            NodeTargetKind.Usage => usages.Contains(id), NodeTargetKind.Group => groups.Contains(id),
            NodeTargetKind.Source => sources.Contains(id), NodeTargetKind.Function => functions.Contains(id), _ => false
        };

        private static bool DiskTargetExistsAny(Guid id, HashSet<Guid> symbols, HashSet<Guid> words,
            HashSet<Guid> sentences, HashSet<Guid> senses, HashSet<Guid> usages, HashSet<Guid> groups,
            HashSet<Guid> sources, HashSet<Guid> functions) => symbols.Contains(id) || words.Contains(id) ||
            sentences.Contains(id) || senses.Contains(id) || usages.Contains(id) || groups.Contains(id) ||
            sources.Contains(id) || functions.Contains(id);

        private static void RequireCanonicalNodes(
            NodeTargetKind kind,
            IEnumerable<Guid> targetIds,
            HashSet<(NodeTargetKind Kind, Guid TargetId)> canonicalNodeTargets)
        {
            foreach (Guid targetId in targetIds)
            {
                if (!canonicalNodeTargets.Contains((kind, targetId)))
                    throw new InvalidDataException($"Trusted {kind} target '{targetId}' has no canonical null-label node.");
            }
        }

        private static void AddExpectedStructuralLink(
            HashSet<(Guid Source, Guid Target, NodeRelationType Relation)> expected,
            Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> canonicalNodeIds,
            NodeTargetKind sourceKind,
            Guid sourceTargetId,
            NodeTargetKind targetKind,
            Guid targetTargetId,
            NodeRelationType relationType)
        {
            if (!canonicalNodeIds.TryGetValue((sourceKind, sourceTargetId), out Guid sourceNodeId) ||
                !canonicalNodeIds.TryGetValue((targetKind, targetTargetId), out Guid targetNodeId))
            {
                throw new InvalidDataException("A Foundation membership has no canonical node endpoint.");
            }
            expected.Add((sourceNodeId, targetNodeId, relationType));
        }

        /// <summary>Updates one trusted link and rebuilds the derived Skills graph snapshot.</summary>
        public bool UpdateTrustedLinkWeight(Guid sourceNodeId, Guid targetNodeId,
            NodeRelationType relationType, double newWeight, string? label = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!Enum.IsDefined(relationType)) throw new ArgumentOutOfRangeException(nameof(relationType));
                if (IsStructuralRelation(relationType) ||
                    !TrustedOutgoingLinks.TryGetValue(sourceNodeId, out List<NodeLink>? links))
                {
                    return false;
                }
                List<NodeLink> matches = links.Where(link => link.TargetNodeId == targetNodeId &&
                    link.RelationType == relationType && string.Equals(link.Label, NormalizeOptionalLinkText(label), StringComparison.Ordinal)).ToList();
                return matches.Count == 1 && UpdateTrustedLinkWeight(matches[0].Provenance.AssertionId, newWeight);
            });
        }

        public bool UpdateTrustedLinkWeight(Guid assertionId, double newWeight)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (assertionId == Guid.Empty)
                {
                    throw new ArgumentException("Assertion identifiers cannot be Guid.Empty.", nameof(assertionId));
                }
                if (!double.IsFinite(newWeight) || newWeight < 0.0 || newWeight > 1.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(newWeight), "Confidence must be finite and in [0, 1].");
                }

                List<NodeLink> originals = LinksByAssertionId.TryGetValue(assertionId, out List<NodeLink>? indexedLinks)
                    ? new List<NodeLink>(indexedLinks) : new List<NodeLink>();
                if (originals.Count == 0 || originals.Any(link => IsStructuralRelation(link.RelationType))) return false;
                if (ShouldStoreReverse(originals[0].RelationType) && originals[0].SourceNodeId != originals[0].TargetNodeId && originals.Count != 2)
                    return false;
                if (originals.Count > 1 && (originals.Count != 2 || !ShouldStoreReverse(originals[0].RelationType) ||
                    originals[0].SourceNodeId != originals[1].TargetNodeId || originals[0].TargetNodeId != originals[1].SourceNodeId))
                {
                    return false;
                }

                List<NodeLink> replacements = originals.Select(link => link.WithWeight(newWeight)).ToList();
                int replaced = 0;
                for (; replaced < originals.Count; replaced++)
                {
                    if (!ReplaceTrustedLink(replacements[replaced])) break;
                }
                if (replaced != originals.Count)
                {
                    for (int index = 0; index < replaced; index++) ReplaceTrustedLink(originals[index]);
                    return false;
                }
                RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>Removes the exact trusted link identified by endpoints, relation, and label.</summary>
        public bool RemoveTrustedLink(Guid sourceNodeId, Guid targetNodeId,
            NodeRelationType relationType, string? label = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!Enum.IsDefined(relationType)) throw new ArgumentOutOfRangeException(nameof(relationType));
                if (IsStructuralRelation(relationType) ||
                    !TrustedOutgoingLinks.TryGetValue(sourceNodeId, out List<NodeLink>? links)) return false;
                List<NodeLink> matches = links.Where(link => link.TargetNodeId == targetNodeId &&
                    link.RelationType == relationType && string.Equals(link.Label, NormalizeOptionalLinkText(label), StringComparison.Ordinal)).ToList();
                return matches.Count == 1 && RemoveTrustedLink(matches[0].Provenance.AssertionId);
            });
        }

        public bool RemoveTrustedLink(Guid assertionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (assertionId == Guid.Empty) return false;
                List<NodeLink> links = LinksByAssertionId.TryGetValue(assertionId, out List<NodeLink>? indexedLinks)
                    ? new List<NodeLink>(indexedLinks) : new List<NodeLink>();
                if (links.Count == 0 || links.Any(link => IsStructuralRelation(link.RelationType))) return false;
                if (ShouldStoreReverse(links[0].RelationType) && links[0].SourceNodeId != links[0].TargetNodeId && links.Count != 2)
                    return false;
                if (links.Count > 1 && (links.Count != 2 || !ShouldStoreReverse(links[0].RelationType) ||
                    links[0].SourceNodeId != links[1].TargetNodeId || links[0].TargetNodeId != links[1].SourceNodeId))
                {
                    return false;
                }

                // Validate every link before mutating anything, so the removal loop cannot fail part-way.
                if (links.Any(link => !CanRemoveTrustedLinkInternal(link))) return false;

                int removed = 0;
                for (; removed < links.Count; removed++)
                {
                    if (!RemoveTrustedLinkInternal(links[removed])) break;
                }
                if (removed != links.Count)
                {
                    // Targeted undo: restore exactly the links removed so far. Each restore re-adds its
                    // graph edge, so no full rebuild is needed; a failed restore means the state is corrupt.
                    for (int index = 0; index < removed; index++)
                    {
                        if (!AddTrustedLinkOneDirection(links[index]))
                        {
                            throw new InvalidOperationException(
                                "Link removal rollback failed; the trusted link state could not be restored.");
                        }
                    }
                    return false;
                }
                RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        private void LoadNodeLayoutSettings(PartitionedBinaryStore store)
        {
            _nodeLayoutOptions = new NodeLayoutOptions();
            if (!store.HasPartition(DiskPartition.LayoutSettings)) return;
            List<DiskRecord> records = store.ReadAll(DiskPartition.LayoutSettings).ToList();
            if (records.Count == 0) return;
            if (records.Count != 1 || records[0].Key != NodeLayoutOptions.RecordKey)
                throw new InvalidDataException("LayoutSettings partition contains an invalid record set.");
            try
            {
                _nodeLayoutOptions = FoundationBinarySerializer.DeserializeNodeLayoutOptions(records[0].Payload);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
            {
                throw new InvalidDataException("LayoutSettings partition contains invalid bias values.", exception);
            }
        }

        private void BuildPagedIndexesAndGraph(PartitionedBinaryStore store)
        {
            _initialized = true;
            LoadNodeLayoutSettings(store);
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox box = FoundationBinarySerializer.DeserializeSymbol(record.Payload, store.IsLegacyFormat);
                SymbolIdentityKey key = box.GetIdentityKey();
                TrustedSymbolIdentityIndex.Add(key, record.Key);
                AddIndex(TrustedSymbolCharIndex, key.SymbolChar, record.Key);
                AddIndex(SymbolsBySenseIndex, key.SenseId, record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words))
            {
                WordBox box = FoundationBinarySerializer.DeserializeWord(record.Payload);
                WordIdentityKey key = box.GetIdentityKey();
                TrustedWordIdentityIndex.Add(key, record.Key);
                AddIndex(WordsBySenseIndex, key.SenseId, record.Key);
                AddIndex(WordsByUsageIndex, key.UsageId, record.Key);
                foreach (Guid symbolId in box.GetSymbols() ?? new List<Guid>()) AddIndex(WordsBySymbolIndex, symbolId, record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences))
            {
                SentenceBox box = FoundationBinarySerializer.DeserializeSentence(record.Payload);
                SentenceIdentityKey key = box.GetIdentityKey();
                TrustedSentenceIdentityIndex.Add(key, record.Key);
                AddIndex(SentencesBySenseIndex, key.SenseId, record.Key);
                foreach (Guid wordId in box.GetWords() ?? new List<Guid>()) AddIndex(SentencesByWordIndex, wordId, record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups))
            {
                GroupBox box = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                foreach (Guid sentenceId in box.GetSentenceIDs() ?? new List<Guid>()) AddIndex(GroupsBySentenceIndex, sentenceId, record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources))
            {
                SourceBox box = FoundationBinarySerializer.DeserializeSource(record.Payload);
                foreach (Guid groupId in box.GetGroupingIDs() ?? new List<Guid>()) AddIndex(SourcesByGroupIndex, groupId, record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
            {
                Node node = FoundationBinarySerializer.DeserializeNode(record.Payload, true);
                NodeIdentityKey identityKey = node.GetIdentityKey();
                if (!TrustedNodeByIdentityIndex.TryAdd(identityKey, record.Key))
                {
                    throw new InvalidDataException("Node partition contains a duplicate target/label identity.");
                }
                if (identityKey.Label == null && !TrustedNodeByTargetIndex.TryAdd((identityKey.TargetKind, identityKey.TargetId), record.Key))
                {
                    throw new InvalidDataException("Node partition contains duplicate canonical nodes for a target.");
                }
                TrustedSkillsGraph.AddNode(record.Key);
            }
            LoadNodePresentationRecords(store);
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload, store.IsLegacyFormat);
                AddTrustedLinkInternal(link, true, false);
            }
            RecalculateAllNodePresentationsInternal();
            foreach (NodeRepresentation representation in NodePresentation.Values)
            {
                representation.MarkReadOnly();
            }
        }

        private void LoadNodePresentationRecords(PartitionedBinaryStore store)
        {
            HashSet<Guid> trustedNodeIds = TrustedNodeByIdentityIndex.Values.ToHashSet();
            if (!store.HasPartition(DiskPartition.NodePresentations))
            {
                foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
                {
                    Node node = FoundationBinarySerializer.DeserializeNode(record.Payload, true);
                    NodeRepresentation representation = CreateDefaultNodeRepresentation(node, isTrusted: true);
                    AddNodeRepresentation(AllocateNodePresentationId(), representation);
                }
                return;
            }
            if (store.GetRecordCount(DiskPartition.NodePresentations) != trustedNodeIds.Count)
            {
                throw new InvalidDataException("NodePresentation partition must contain exactly one record per trusted Node.");
            }

            foreach (DiskRecord record in store.ReadAll(DiskPartition.NodePresentations))
            {
                (UInt128 presentationId, NodeRepresentation representation) =
                    FoundationBinarySerializer.DeserializeNodeRepresentation(record.Payload);
                if (record.Key != representation.GetNodeID() || !trustedNodeIds.Contains(record.Key))
                {
                    throw new InvalidDataException("NodePresentation partition contains an unresolved or mismatched Node ID.");
                }
                try
                {
                    AddNodeRepresentation(presentationId, representation);
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException("NodePresentation partition contains a duplicate presentation key or Node ID.", exception);
                }
            }
            if (NodePresentationKeyByNodeId.Count != trustedNodeIds.Count)
            {
                throw new InvalidDataException("NodePresentation partition does not contain exactly one presentation per trusted Node.");
            }
        }

        private NodeRepresentation CreateDefaultNodeRepresentation(Node node, bool isTrusted)
        {
            return new NodeRepresentation(
                node.GetNodeID(),
                GetDefaultNodeColor(node.GetTargetKind(), isTrusted),
                1.0,
                CreateDefaultNodeLabel(node.GetTargetKind(), node.GetTargetID(), node.GetLabel()));
        }

        private void MergeDiskStore(PartitionedBinaryStore store, LayoutOptionsMergePreference layoutOptionsMergePreference)
        {
            if (layoutOptionsMergePreference == LayoutOptionsMergePreference.IncomingWins)
                LoadNodeLayoutSettings(store);
            HashSet<Guid> existingPresentationNodeIds = NodePresentationKeyByNodeId.Keys.ToHashSet();
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Senses))
            {
                SenseBox incoming = FoundationBinarySerializer.DeserializeSense(record.Payload);
                if (TrustedSenseDictionary.TryGetValue(incoming.GetSenseID(), out SenseBox? existing))
                {
                    if (existing.GetAverageWordPosition() != incoming.GetAverageWordPosition() ||
                        existing.GetAverageSentencePosition() != incoming.GetAverageSentencePosition() ||
                        existing.GetAverageSpread() != incoming.GetAverageSpread())
                    {
                        throw new InvalidDataException("Merge contains conflicting metrics for an existing SenseBox.");
                    }
                    foreach (Guid sentenceId in incoming.GetSentences() ?? new List<Guid>()) existing.AddSentenceFromCore(sentenceId);
                    foreach (Guid wordId in incoming.GetLeadingWords() ?? new List<Guid>()) existing.AddLeadingWordFromCore(wordId);
                    foreach (Guid wordId in incoming.GetTrailingWords() ?? new List<Guid>()) existing.AddTrailingWordFromCore(wordId);
                }
                else
                {
                    AddTrustedSense(incoming);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Usages))
            {
                UsageBox incoming = FoundationBinarySerializer.DeserializeUsage(record.Payload);
                if (TrustedUsageDictionary.TryGetValue(incoming.GetUsageID(), out UsageBox? existing))
                {
                    foreach (Guid sentenceId in incoming.GetSentences() ?? new List<Guid>()) existing.AddSentenceFromCore(sentenceId);
                }
                else
                {
                    AddTrustedUsage(incoming);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox incoming = FoundationBinarySerializer.DeserializeSymbol(record.Payload, store.IsLegacyFormat);
                if (TrustedSymbolDictionary.TryGetValue(incoming.GetSymbolID(), out SymbolBox? existing))
                {
                    if (!existing.GetIdentityKey().Equals(incoming.GetIdentityKey()))
                        throw new InvalidDataException("Merge contains a conflicting SymbolBox identity.");
                }
                else if (!AddTrustedSymbol(incoming))
                {
                    throw new InvalidDataException("Merge could not add an incoming SymbolBox.");
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words))
            {
                WordBox incoming = FoundationBinarySerializer.DeserializeWord(record.Payload);
                if (TrustedWordDictionary.TryGetValue(incoming.GetWordID(), out WordBox? existing))
                {
                    if (!existing.GetIdentityKey().Equals(incoming.GetIdentityKey()))
                        throw new InvalidDataException("Merge contains a conflicting WordBox identity.");
                }
                else if (!AddTrustedWord(incoming))
                {
                    throw new InvalidDataException("Merge could not add an incoming WordBox.");
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences))
            {
                SentenceBox incoming = FoundationBinarySerializer.DeserializeSentence(record.Payload);
                if (TrustedSentenceDictionary.TryGetValue(incoming.GetSentenceID(), out SentenceBox? existing))
                {
                    if (!existing.GetIdentityKey().Equals(incoming.GetIdentityKey()))
                        throw new InvalidDataException("Merge contains a conflicting SentenceBox identity.");
                }
                else if (!AddTrustedSentence(incoming))
                {
                    throw new InvalidDataException("Merge could not add an incoming SentenceBox.");
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups))
            {
                GroupBox incoming = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                if (TrustedGroupDictionary.ContainsKey(incoming.GetGroupingID()))
                {
                    GroupBox existing = TrustedGroupDictionary[incoming.GetGroupingID()];
                    if (!string.Equals(existing.GetGroupingName(), incoming.GetGroupingName(), StringComparison.Ordinal))
                        throw new InvalidDataException("Merge contains a conflicting GroupBox name.");
                    foreach (Guid sentenceId in incoming.GetSentenceIDs() ?? new List<Guid>())
                        AddSentenceToTrustedGroupInternal(incoming.GetGroupingID(), sentenceId, false);
                }
                else
                {
                    AddTrustedGroup(incoming);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources))
            {
                SourceBox incoming = FoundationBinarySerializer.DeserializeSource(record.Payload);
                if (SourceDictionary.ContainsKey(incoming.GetSourceID()))
                {
                    SourceBox existing = SourceDictionary[incoming.GetSourceID()];
                    if (!string.Equals(existing.GetSourceName(), incoming.GetSourceName(), StringComparison.Ordinal))
                        throw new InvalidDataException("Merge contains a conflicting SourceBox name.");
                    foreach (Guid groupId in incoming.GetGroupingIDs() ?? new List<Guid>())
                        AddGroupToTrustedSourceInternal(incoming.GetSourceID(), groupId, false);
                }
                else
                {
                    AddTrustedSource(incoming);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Functions))
            {
                FunctionBox incoming = FoundationBinarySerializer.DeserializeFunction(record.Payload, store.IsLegacyFormat);
                if (TrustedFunctionDictionary.TryGetValue(incoming.GetFunctionID(), out FunctionBox? existing))
                {
                    bool definitionsMatch = string.Equals(existing.FunctionKey, incoming.FunctionKey, StringComparison.Ordinal) &&
                        existing.ExpectedResultType == incoming.ExpectedResultType &&
                        string.Equals(existing.ExpectedResultUnitTitle, incoming.ExpectedResultUnitTitle, StringComparison.Ordinal) &&
                        existing.Variables.Count == incoming.Variables.Count &&
                        existing.Variables.Zip(incoming.Variables).All(pair =>
                            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal) &&
                            pair.First.DataType == pair.Second.DataType &&
                            string.Equals(pair.First.UnitTitle, pair.Second.UnitTitle, StringComparison.Ordinal) &&
                            FunctionValuesEqual(pair.First.Value, pair.Second.Value));
                    FunctionResult? er = existing.Result; FunctionResult? ir = incoming.Result;
                    bool resultsMatch = (er == null && ir == null) || (er != null && ir != null &&
                        er.DataType == ir.DataType && string.Equals(er.UnitTitle, ir.UnitTitle, StringComparison.Ordinal) && FunctionValuesEqual(er.Value, ir.Value));
                    if (!definitionsMatch || !resultsMatch)
                        throw new InvalidDataException($"Merge contains a conflicting trusted FunctionBox definition for {incoming.GetFunctionID()} ({incoming.FunctionKey}): def={definitionsMatch} res={resultsMatch}.");
                    if (resultsMatch)
                    {
                        foreach (FunctionDispatchProvenance provenance in incoming.DispatchProvenanceHistory)
                        {
                            existing.RecordDispatchProvenance(provenance);
                        }
                    }
                }
                else if (!AddTrustedFunction(incoming))
                {
                    throw new InvalidDataException("Merge could not add an incoming FunctionBox.");
                }
            }

            Dictionary<Guid, Guid> nodeIdMap = new Dictionary<Guid, Guid>();
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
            {
                Node incoming = FoundationBinarySerializer.DeserializeNode(record.Payload, false);
                NodeIdentityKey identityKey = incoming.GetIdentityKey();
                Node? existing = GetTrustedNodeByTarget(identityKey.TargetKind, identityKey.TargetId, identityKey.Label);
                if (existing != null)
                {
                    existing.ReconcileReferenceCounts(
                        checked(existing.GetVerifiedReferences() + incoming.GetVerifiedReferences()),
                        checked(existing.GetInvalidReferences() + incoming.GetInvalidReferences()));
                    nodeIdMap[record.Key] = existing.GetNodeID();
                    continue;
                }
                if (!HasTrustedTarget(incoming.GetTargetKind(), incoming.GetTargetID())) continue;
                Guid mergedNodeId = record.Key;
                if (TrustedNodeDictionary.ContainsKey(mergedNodeId))
                {
                    mergedNodeId = GenerateUntrustedNodeId();
                    Node remapped = new Node(mergedNodeId, identityKey.TargetKind, identityKey.TargetId, identityKey.Label, allowRegisteredTrustedId: true);
                    remapped.ReconcileReferenceCounts(incoming.GetVerifiedReferences(), incoming.GetInvalidReferences());
                    incoming = remapped;
                }
                Guid importedNodeId = mergedNodeId;
                incoming.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(importedNodeId));
                incoming.MarkTrusted();
                TrustedNodeDictionary.Add(mergedNodeId, incoming);
                TrustedNodeByIdentityIndex.Add(identityKey, mergedNodeId);
                if (identityKey.Label == null)
                {
                    TrustedNodeByTargetIndex.Add((identityKey.TargetKind, identityKey.TargetId), mergedNodeId);
                }
                TrustedSkillsGraph.AddNode(mergedNodeId);
                nodeIdMap[record.Key] = mergedNodeId;
            }
            MergeNodePresentations(store, nodeIdMap, existingPresentationNodeIds);
            foreach (Node trustedNode in EnumerateTrustedNodesInternal())
            {
                EnsureNodeRepresentation(trustedNode, isTrusted: true);
                if (trustedNode.GetLabel() == null)
                {
                    WireNodeRelations(trustedNode);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload, store.IsLegacyFormat);
                if (IsStructuralRelation(link.RelationType))
                {
                    continue;
                }
                if (nodeIdMap.TryGetValue(link.SourceNodeId, out Guid sourceId) && nodeIdMap.TryGetValue(link.TargetNodeId, out Guid targetId))
                {
                    NodeLink mapped = new NodeLink(sourceId, targetId, link.RelationType, link.Weight, link.Label, link.Provenance);
                    List<NodeLink> existingAssertions = LinksByAssertionId.TryGetValue(mapped.Provenance.AssertionId, out List<NodeLink>? indexedAssertions)
                        ? new List<NodeLink>(indexedAssertions) : new List<NodeLink>();
                    if (existingAssertions.Count == 0)
                    {
                        AddTrustedLinkInternal(mapped, true, false);
                    }
                    else
                    {
                        bool sameIdentity = existingAssertions.Any(existing => existing.Equals(mapped));
                        bool sameMetadata = existingAssertions.All(existing =>
                            existing.Provenance.AssertionId == mapped.Provenance.AssertionId &&
                            existing.RelationType == mapped.RelationType &&
                            string.Equals(existing.Label, mapped.Label, StringComparison.Ordinal));
                        if (!sameIdentity || !sameMetadata)
                        {
                            throw new InvalidDataException("Merge contains a conflicting link assertion identity.");
                        }
                        if (existingAssertions.Any(existing => existing.Weight != mapped.Weight))
                            UpdateTrustedLinkWeight(mapped.Provenance.AssertionId, mapped.Weight);
                    }
                }
            }
        }

        private void MergeNodePresentations(
            PartitionedBinaryStore store,
            IReadOnlyDictionary<Guid, Guid> nodeIdMap,
            HashSet<Guid> existingPresentationNodeIds)
        {
            foreach (DiskRecord record in store.ReadAll(DiskPartition.NodePresentations))
            {
                (UInt128 incomingPresentationId, NodeRepresentation incomingRepresentation) =
                    FoundationBinarySerializer.DeserializeNodeRepresentation(record.Payload);
                if (record.Key != incomingRepresentation.GetNodeID() ||
                    !nodeIdMap.TryGetValue(record.Key, out Guid mappedNodeId))
                {
                    throw new InvalidDataException("Incoming NodePresentation does not map to an imported Node.");
                }
                if (existingPresentationNodeIds.Contains(mappedNodeId))
                {
                    continue;
                }

                RemoveNodeRepresentation(mappedNodeId);

                UInt128 presentationId = NodePresentation.ContainsKey(incomingPresentationId)
                    ? AllocateNodePresentationId()
                    : incomingPresentationId;
                NodeRepresentation mappedRepresentation = new NodeRepresentation(
                    mappedNodeId,
                    incomingRepresentation.GetColor(),
                    incomingRepresentation.GetSize(),
                    incomingRepresentation.GetTextLabel(),
                    incomingRepresentation.GetPosition(),
                    incomingRepresentation.IsTextLabelCustomized);
                AddNodeRepresentation(presentationId, mappedRepresentation);
            }
        }

        private T? GetPagedObject<T>(DiskPartition partition, Guid id, Func<byte[], T> deserialize, Action<T> makeReadOnly)
            where T : class
        {
            if (!_diskPagedReadOnly || _diskStore == null || !_diskStore.TryRead(partition, id, out byte[] payload)) return null;
            (DiskPartition Partition, Guid Id) key = (partition, id);
            if (_pagedObjectCache.TryGetValue(key, out object? cached))
            {
                if (cached is T typedCached)
                {
                    _pagedCacheHits++;
                    TouchPagedObject(key);
                    return typedCached;
                }

                _pagedObjectCache.Remove(key);
                if (_pagedObjectLruNodes.TryGetValue(key, out LinkedListNode<(DiskPartition Partition, Guid Id)>? staleNode))
                {
                    _pagedObjectLru.Remove(staleNode);
                    _pagedObjectLruNodes.Remove(key);
                }
            }
            _pagedCacheMisses++;
            T value = deserialize(payload);
            makeReadOnly(value);
            if (_maxPagedObjects > 0)
            {
                _pagedObjectCache.Add(key, value);
                _pagedObjectLruNodes.Add(key, _pagedObjectLru.AddLast(key));
                while (_pagedObjectCache.Count > _maxPagedObjects)
                {
                    (DiskPartition Partition, Guid Id) oldest = _pagedObjectLru.First!.Value;
                    _pagedObjectLru.RemoveFirst();
                    _pagedObjectLruNodes.Remove(oldest);
                    _pagedObjectCache.Remove(oldest);
                }
            }
            return value;
        }

        private bool IsPagedRecordAvailable(DiskPartition partition, Guid id) =>
            _diskPagedReadOnly && _diskStore != null && _diskStore.Contains(partition, id);

        private void TouchPagedObject((DiskPartition Partition, Guid Id) key)
        {
            if (_pagedObjectLruNodes.TryGetValue(key, out LinkedListNode<(DiskPartition Partition, Guid Id)>? node))
            {
                _pagedObjectLru.Remove(node);
                _pagedObjectLru.AddLast(node);
            }
        }

        private void ClearPagedObjectCache()
        {
            _pagedObjectCache.Clear();
            _pagedObjectLruNodes.Clear();
            _pagedObjectLru.Clear();
            _pagedCacheHits = 0;
            _pagedCacheMisses = 0;
            _lastDiskStoreValidationError = null;
        }

        private void EnsureMutableStorageMode()
        {
            EnsureCoreInitialized();
            if (_legacyDiskStoreReadOnly)
                throw new InvalidOperationException("The core contains a legacy read-only checkpoint. Call UpgradeLegacyDiskStore() before mutation.");
            if (_diskPagedReadOnly)
                throw new InvalidOperationException("The core is in read-only disk-paged mode. Call MaterializeDiskStore() before mutation.");
        }

        private void EnsureCoreInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("The core is uninitialized. Call InitializeCore() before registry operations.");
            }
        }

        /// <summary>
        /// Returns true if the sense can be used as a trust prerequisite. The default sense is registered so that
        /// placeholder boxes can exist, but it is never a valid basis for promoting a symbol, word, or sentence.
        /// Every caller is a promotion gate.
        /// </summary>
        private bool HasTrustedSense(Guid? senseId)
        {
            return senseId.HasValue && senseId.Value != DefaultSenseId && TrustedSenseDictionary.ContainsKey(senseId.Value);
        }

        private bool HasTrustedTarget(NodeTargetKind kind, Guid targetId)
        {
            return kind switch
            {
                NodeTargetKind.Symbol => TrustedSymbolDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Symbols, targetId),
                NodeTargetKind.Word => TrustedWordDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Words, targetId),
                NodeTargetKind.Sentence => TrustedSentenceDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Sentences, targetId),
                NodeTargetKind.Sense => TrustedSenseDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Senses, targetId),
                NodeTargetKind.Usage => TrustedUsageDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Usages, targetId),
                NodeTargetKind.Source => SourceDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Sources, targetId),
                NodeTargetKind.Group => TrustedGroupDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Groups, targetId),
                NodeTargetKind.Function => TrustedFunctionDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Functions, targetId),
                _ => false
            };
        }


        /// <summary>
        /// Returns true if a word's classification is final enough to trust: no placeholder part of speech,
        /// and neither the default sense nor the default usage (both are placeholders until assigned).
        /// </summary>
        private static bool HasFinalWordClassification(Guid? senseId, Guid? usageId, PartOfSpeech? partOfSpeech)
        {
            return partOfSpeech.HasValue && partOfSpeech.Value != PartOfSpeech.Unknown &&
                   senseId != DefaultSenseId && usageId != DefaultUsageId;
        }

        private bool CanTrustWord(WordBox wordBox)
        {
            if (!HasFinalWordClassification(wordBox.GetSenseID(), wordBox.GetUsageID(), wordBox.GetPartOfSpeech()))
            {
                return false;
            }

            if (!wordBox.HasCompleteIdentity ||
                !HasTrustedSense(wordBox.GetSenseID()) ||
                !wordBox.GetUsageID().HasValue ||
                !TrustedUsageDictionary.ContainsKey(wordBox.GetUsageID()!.Value))
            {
                return false;
            }

            List<Guid>? symbolIds = wordBox.GetSymbols();
            return symbolIds != null && symbolIds.All(TrustedSymbolDictionary.ContainsKey);
        }

        private bool CanTrustSentence(SentenceBox sentenceBox)
        {
            if (!sentenceBox.HasCompleteIdentity || !HasTrustedSense(sentenceBox.GetSenseID()))
            {
                return false;
            }

            List<Guid>? wordIds = sentenceBox.GetWords();
            return wordIds != null && wordIds.All(TrustedWordDictionary.ContainsKey);
        }

        private void AddIndex<TKey>(Dictionary<TKey, OrderedGuidIndexList> index, TKey key, Guid value)
            where TKey : notnull
        {
            if (!index.TryGetValue(key, out OrderedGuidIndexList? values))
            {
                values = new OrderedGuidIndexList();
                index.Add(key, values);
            }
            values.Add(value);
        }

        private static List<Guid> CopyIndexValues<TKey>(Dictionary<TKey, OrderedGuidIndexList> index, TKey key)
            where TKey : notnull
        {
            return index.TryGetValue(key, out OrderedGuidIndexList? values) ? values.Snapshot() : new List<Guid>();
        }

        private void RemoveUntrustedNodeForTarget(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            List<Guid> nodeIds = UntrustedNodeDictionary
                .Where(entry => entry.Value.GetTargetKind() == kind && entry.Value.GetTargetID() == targetId)
                .Select(entry => entry.Key)
                .ToList();
            if (nodeIds.Count == 0 && UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid canonicalNodeId))
            {
                nodeIds.Add(canonicalNodeId);
            }
            if (nodeIds.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            foreach (Guid nodeId in nodeIds.Distinct())
            {
                if (!UntrustedNodeDictionary.Remove(nodeId, out Node? node))
                {
                    continue;
                }
                UntrustedNodeByIdentityIndex.Remove(node.GetIdentityKey());
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(null);
                RemoveNodeRepresentation(nodeId);
                removedAny = true;
            }
            UntrustedNodeByTargetIndex.Remove(targetKey);
            if (removedAny)
            {
                RecalculateAllNodePresentationsInternal();
            }
        }

        private bool HandleUntrustedNodeIdentityChange(Guid nodeId, NodeIdentityKey oldKey, NodeIdentityKey newKey)
        {
            if (!UntrustedNodeDictionary.ContainsKey(nodeId) ||
                UntrustedNodeByIdentityIndex.ContainsKey(newKey) ||
                TrustedNodeByIdentityIndex.ContainsKey(newKey))
            {
                return false;
            }

            UntrustedNodeByIdentityIndex.Remove(oldKey);
            UntrustedNodeByIdentityIndex.Add(newKey, nodeId);
            (NodeTargetKind Kind, Guid TargetId) targetKey = (oldKey.TargetKind, oldKey.TargetId);
            if (oldKey.Label == null && UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid currentId) && currentId == nodeId)
            {
                UntrustedNodeByTargetIndex.Remove(targetKey);
            }
            if (newKey.Label == null)
            {
                UntrustedNodeByTargetIndex[targetKey] = nodeId;
            }
            if (NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 presentationId) &&
                NodePresentation.TryGetValue(presentationId, out NodeRepresentation? representation))
            {
                representation.UpdateDefaultTextLabel(CreateDefaultNodeLabel(newKey.TargetKind, newKey.TargetId, newKey.Label));
            }
            return true;
        }

        private void HandleNodeIdentityChange(Guid nodeId, NodeIdentityKey oldKey, NodeIdentityKey newKey)
        {
            if (_diskPagedReadOnly) return;
            RecalculateAllNodePresentationsInternal();
        }

        private void HandleNodeReferenceChange(Guid nodeId)
        {
            if (_diskPagedReadOnly) return;
            if (TrustedNodeDictionary.ContainsKey(nodeId))
                _graphEpoch = checked(_graphEpoch + 1);
            RecalculateAllNodePresentationsInternal();
        }

        private bool EnsureUntrustedNode(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            if (TrustedNodeByTargetIndex.ContainsKey(targetKey) ||
                UntrustedNodeByTargetIndex.ContainsKey(targetKey))
            {
                return false;
            }

            Node node = new Node(kind, targetId);
            UntrustedNodeDictionary.Add(node.GetNodeID(), node);
            UntrustedNodeByTargetIndex.Add(targetKey, node.GetNodeID());
            UntrustedNodeByIdentityIndex.Add(node.GetIdentityKey(), node.GetNodeID());
            Guid nodeId = node.GetNodeID();
            node.SetIdentityChangingHandler((oldKey, newKey) => HandleUntrustedNodeIdentityChange(nodeId, oldKey, newKey));
            node.SetIdentityChangedHandler((oldKey, newKey) => HandleNodeIdentityChange(nodeId, oldKey, newKey));
            node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
            EnsureNodeRepresentation(node, isTrusted: false);
            RecalculateAllNodePresentationsInternal();
            return true;
        }

        private Node EnsureTrustedNode(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            if (TrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid trustedNodeId) &&
                TrustedNodeDictionary.TryGetValue(trustedNodeId, out Node? existingNode))
            {
                existingNode.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(trustedNodeId));
                EnsureNodeRepresentation(existingNode, isTrusted: true);
                return existingNode;
            }

            if (UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid untrustedNodeId) &&
                UntrustedNodeDictionary.TryGetValue(untrustedNodeId, out Node? untrustedNode))
            {
                ConvertUntrustedNodeToTrusted(untrustedNodeId);
                if (TrustedNodeByTargetIndex.TryGetValue(targetKey, out trustedNodeId) &&
                    TrustedNodeDictionary.TryGetValue(trustedNodeId, out existingNode))
                {
                    return existingNode;
                }
            }

            Node node = new Node(kind, targetId);
            node.MarkTrusted();
            TrustedNodeDictionary.Add(node.GetNodeID(), node);
            TrustedNodeByTargetIndex.Add(targetKey, node.GetNodeID());
            TrustedNodeByIdentityIndex.Add(node.GetIdentityKey(), node.GetNodeID());
            TrustedSkillsGraph.AddNode(node.GetNodeID());
            _graphEpoch = checked(_graphEpoch + 1);
            Guid nodeId = node.GetNodeID();
            node.SetReferenceCountsChangedHandler(() => HandleNodeReferenceChange(nodeId));
            EnsureNodeRepresentation(node, isTrusted: true);
            WireNodeRelations(node);
            return node;
        }

        private void EnsureNodeRepresentation(Node node, bool isTrusted)
        {
            Guid nodeId = node.GetNodeID();
            if (NodePresentationKeyByNodeId.TryGetValue(nodeId, out UInt128 existingKey))
            {
                if (!NodePresentation.TryGetValue(existingKey, out NodeRepresentation? existingRepresentation) ||
                    existingRepresentation.GetNodeID() != nodeId)
                {
                    throw new InvalidOperationException("Node presentation indexes are inconsistent.");
                }
                existingRepresentation.UpdateDefaultTextLabel(CreateDefaultNodeLabel(
                    node.GetTargetKind(), node.GetTargetID(), node.GetLabel(), isTrusted));
                return;
            }

            UInt128 presentationId = AllocateNodePresentationId();
            NodeRepresentation representation = new NodeRepresentation(
                nodeId,
                GetDefaultNodeColor(node.GetTargetKind(), isTrusted),
                1.0,
                CreateDefaultNodeLabel(node.GetTargetKind(), node.GetTargetID(), node.GetLabel(), isTrusted));
            AddNodeRepresentation(presentationId, representation);
        }

        private static uint GetDefaultNodeColor(NodeTargetKind kind, bool isTrusted)
        {
            uint color = kind switch
            {
                NodeTargetKind.Symbol => 0xFF4E79A7,
                NodeTargetKind.Word => 0xFFF28E2B,
                NodeTargetKind.Sentence => 0xFFE15759,
                NodeTargetKind.Sense => 0xFF76B7B2,
                NodeTargetKind.Usage => 0xFF59A14F,
                NodeTargetKind.Source => 0xFFEDC949,
                NodeTargetKind.Group => 0xFFAF7AA1,
                NodeTargetKind.Function => 0xFFFF9DA7,
                _ => 0xFFBAB0AC
            };
            return isTrusted ? color : (color & 0x00FFFFFF) | 0xA0000000;
        }

        private string? CreateDefaultNodeLabel(NodeTargetKind kind, Guid targetId, string? nodeLabel, bool isTrusted = false)
        {
            if (!string.IsNullOrWhiteSpace(nodeLabel))
            {
                return nodeLabel.Trim();
            }
            if (isTrusted && _initialized)
            {
                string? summary = GetTrustedNodeSummary(kind, targetId);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    return summary;
                }
            }
            return $"{kind} {targetId.ToString("N")[..8]}";
        }

        private UInt128 AllocateNodePresentationId()
        {
            if (_nodePresentationIdSpaceExhausted)
            {
                throw new OverflowException("No further UInt128 presentation IDs are available for NodePresentation.");
            }
            UInt128 assigned = _nextNodePresentationId;
            if (assigned == UInt128.MaxValue)
            {
                _nodePresentationIdSpaceExhausted = true;
            }
            else
            {
                _nextNodePresentationId = assigned + 1;
            }
            return assigned;
        }

        private void AddNodeRepresentation(UInt128 presentationId, NodeRepresentation representation)
        {
            Guid nodeId = representation.GetNodeID();
            if (NodePresentation.ContainsKey(presentationId) || NodePresentationKeyByNodeId.ContainsKey(nodeId))
            {
                throw new InvalidOperationException("A Node or presentation ID is already registered in NodePresentation.");
            }
            NodePresentation.Add(presentationId, representation);
            NodePresentationKeyByNodeId.Add(nodeId, presentationId);
            AdvanceNodePresentationId(presentationId);
        }

        private void RemoveNodeRepresentation(Guid nodeId)
        {
            if (NodePresentationKeyByNodeId.Remove(nodeId, out UInt128 presentationId))
            {
                NodePresentation.Remove(presentationId);
            }
        }

        private void AdvanceNodePresentationId(UInt128 usedId)
        {
            if (_nodePresentationIdSpaceExhausted || usedId < _nextNodePresentationId)
            {
                return;
            }
            if (usedId == UInt128.MaxValue)
            {
                _nextNodePresentationId = UInt128.MaxValue;
                _nodePresentationIdSpaceExhausted = true;
            }
            else
            {
                _nextNodePresentationId = usedId + 1;
            }
        }

        private Node? GetTrustedNodeForTarget(NodeTargetKind kind, Guid targetId)
        {
            if (!TrustedNodeByTargetIndex.TryGetValue((kind, targetId), out Guid nodeId))
            {
                return null;
            }
            return GetTrustedNodeInternal(nodeId);
        }

        private void LinkNodeToTarget(Guid sourceNodeId, NodeTargetKind targetKind, Guid targetId, NodeRelationType relationType)
        {
            Node? targetNode = GetTrustedNodeForTarget(targetKind, targetId);
            if (targetNode == null)
            {
                return;
            }
            LinkNodeIds(sourceNodeId, targetNode.GetNodeID(), relationType);
        }

        private void LinkNodeIds(Guid sourceNodeId, Guid targetNodeId, NodeRelationType relationType)
        {
            if (sourceNodeId == Guid.Empty || targetNodeId == Guid.Empty)
            {
                return;
            }
            string? label = null;
            NodeLinkProvenance provenance = NodeLinkProvenance.CreateForLink(
                sourceNodeId, targetNodeId, relationType, label, NodeLinkOrigin.Structural, 1.0);
            AddTrustedLinkInternal(new NodeLink(sourceNodeId, targetNodeId, relationType, 1.0, label, provenance));
        }

        private static bool IsStructuralRelation(NodeRelationType relationType) => relationType is
            NodeRelationType.HasSense or NodeRelationType.HasUsage or NodeRelationType.ContainsSymbol or
            NodeRelationType.ContainsWord or NodeRelationType.ContainsSentence or NodeRelationType.ContainsGroup or
            NodeRelationType.LeadingWord or NodeRelationType.TrailingWord;

        private static string? NormalizeOptionalLinkText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private bool IsValidLinkKindCombination(NodeLink link)
        {
            Node? source = GetTrustedNode(link.SourceNodeId);
            Node? target = GetTrustedNode(link.TargetNodeId);
            return source != null && target != null &&
                   IsValidLinkKindCombination(source.GetTargetKind(), target.GetTargetKind(), link);
        }

        private bool AreProvenanceTargetsTrusted(NodeLinkProvenance provenance)
        {
            return (!provenance.SourceTargetId.HasValue || HasAnyTrustedTarget(provenance.SourceTargetId.Value)) &&
                   (!provenance.EvidenceTargetId.HasValue ||
                    HasAnyTrustedTarget(provenance.EvidenceTargetId.Value) ||
                    (provenance.Origin == NodeLinkOrigin.Reasoning && HasValidStagedNonDeterministicFunctionEvidence(provenance.EvidenceTargetId.Value)));
        }

        private bool HasValidStagedNonDeterministicFunctionEvidence(Guid targetId)
        {
            if (UntrustedFunctionDictionary.TryGetValue(targetId, out FunctionBox? stagedFunction) &&
                stagedFunction.HasValidStagedNonDeterministicProvenance)
            {
                return true;
            }
            if (UntrustedNodeDictionary.TryGetValue(targetId, out Node? stagedNode) &&
                stagedNode.GetTargetKind() == NodeTargetKind.Function &&
                UntrustedFunctionDictionary.TryGetValue(stagedNode.GetTargetID(), out FunctionBox? targetFunction) &&
                targetFunction.HasValidStagedNonDeterministicProvenance)
            {
                return true;
            }
            return false;
        }

        private static bool SameReasoningAssertion(NodeLink existing, NodeLink candidate)
        {
            bool sameDirection = existing.SourceNodeId == candidate.SourceNodeId && existing.TargetNodeId == candidate.TargetNodeId;
            bool matchingReverse = ShouldStoreReverse(candidate.RelationType) &&
                existing.SourceNodeId == candidate.TargetNodeId && existing.TargetNodeId == candidate.SourceNodeId;
            NodeLinkProvenance first = existing.Provenance;
            NodeLinkProvenance second = candidate.Provenance;
            return (sameDirection || matchingReverse) &&
                   existing.RelationType == candidate.RelationType &&
                   string.Equals(existing.Label, candidate.Label, StringComparison.Ordinal) &&
                   first.AssertionId == second.AssertionId && first.Origin == NodeLinkOrigin.Reasoning &&
                   first.Origin == second.Origin && string.Equals(first.Author, second.Author, StringComparison.Ordinal) &&
                   first.SourceTargetId == second.SourceTargetId && first.EvidenceTargetId == second.EvidenceTargetId &&
                   first.ValidFrom == second.ValidFrom && first.ValidUntil == second.ValidUntil;
        }

        private bool HasAnyTrustedTarget(Guid targetId)
        {
            if (TrustedNodeDictionary.ContainsKey(targetId) || IsPagedRecordAvailable(DiskPartition.Nodes, targetId))
                return true;
            if (TrustedSymbolDictionary.ContainsKey(targetId) || TrustedWordDictionary.ContainsKey(targetId) ||
                TrustedSentenceDictionary.ContainsKey(targetId) || TrustedSenseDictionary.ContainsKey(targetId) ||
                TrustedUsageDictionary.ContainsKey(targetId) || TrustedGroupDictionary.ContainsKey(targetId) ||
                SourceDictionary.ContainsKey(targetId) || TrustedFunctionDictionary.ContainsKey(targetId))
            {
                return true;
            }
            return IsPagedRecordAvailable(DiskPartition.Symbols, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Words, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Sentences, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Senses, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Usages, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Groups, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Sources, targetId) ||
                   IsPagedRecordAvailable(DiskPartition.Functions, targetId);
        }

        private static bool IsValidLinkKindCombination(NodeTargetKind sourceKind, NodeTargetKind targetKind, NodeLink link)
        {
            if (IsStructuralRelation(link.RelationType) && link.SourceNodeId == link.TargetNodeId)
            {
                return false;
            }

            return link.RelationType switch
            {
                NodeRelationType.HasSense =>
                    sourceKind is NodeTargetKind.Symbol or NodeTargetKind.Word or NodeTargetKind.Sentence &&
                    targetKind == NodeTargetKind.Sense,
                NodeRelationType.HasUsage => sourceKind == NodeTargetKind.Word && targetKind == NodeTargetKind.Usage,
                NodeRelationType.ContainsSymbol => sourceKind == NodeTargetKind.Word && targetKind == NodeTargetKind.Symbol,
                NodeRelationType.ContainsWord => sourceKind == NodeTargetKind.Sentence && targetKind == NodeTargetKind.Word,
                NodeRelationType.ContainsSentence => sourceKind == NodeTargetKind.Group && targetKind == NodeTargetKind.Sentence,
                NodeRelationType.ContainsGroup => sourceKind == NodeTargetKind.Source && targetKind == NodeTargetKind.Group,
                NodeRelationType.LeadingWord or NodeRelationType.TrailingWord =>
                    sourceKind == NodeTargetKind.Sentence && targetKind == NodeTargetKind.Word,
                NodeRelationType.Synonym or NodeRelationType.Antonym or NodeRelationType.IsA =>
                    sourceKind == NodeTargetKind.Word && targetKind == NodeTargetKind.Word && link.SourceNodeId != link.TargetNodeId,
                NodeRelationType.PartOf => sourceKind == targetKind &&
                    sourceKind is NodeTargetKind.Word or NodeTargetKind.Sentence or NodeTargetKind.Group &&
                    link.SourceNodeId != link.TargetNodeId,
                NodeRelationType.Causes or NodeRelationType.Implies =>
                    sourceKind is NodeTargetKind.Word or NodeTargetKind.Sentence or NodeTargetKind.Function &&
                    targetKind is NodeTargetKind.Word or NodeTargetKind.Sentence or NodeTargetKind.Function &&
                    link.SourceNodeId != link.TargetNodeId,
                NodeRelationType.EvaluatesWithFunction =>
                    sourceKind != NodeTargetKind.Function && targetKind == NodeTargetKind.Function,
                NodeRelationType.Custom => true,
                _ => false
            };
        }

        private void WireNodeRelations(Node node)
        {
            if (node.GetLabel() != null)
            {
                return;
            }
            Guid nodeId = node.GetNodeID();
            Guid targetId = node.GetTargetID();
            switch (node.GetTargetKind())
            {
                case NodeTargetKind.Symbol:
                    if (TrustedSymbolDictionary.TryGetValue(targetId, out SymbolBox? symbolBox))
                    {
                        Guid? senseId = symbolBox.GetSenseID();
                        if (senseId.HasValue)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        }
                        foreach (Guid wordId in CopyIndexValues(WordsBySymbolIndex, targetId))
                        {
                            Guid wordNodeId = GetNodeIdForTarget(NodeTargetKind.Word, wordId);
                            LinkNodeIds(wordNodeId, nodeId, NodeRelationType.ContainsSymbol);
                        }
                    }
                    break;
                case NodeTargetKind.Word:
                    if (TrustedWordDictionary.TryGetValue(targetId, out WordBox? wordBox))
                    {
                        Guid? senseId = wordBox.GetSenseID();
                        if (senseId.HasValue)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        }
                        Guid? usageId = wordBox.GetUsageID();
                        if (usageId.HasValue)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Usage, usageId.Value, NodeRelationType.HasUsage);
                        }
                        foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Symbol, symbolId, NodeRelationType.ContainsSymbol);
                        }
                    }
                    foreach (Guid sentenceId in CopyIndexValues(SentencesByWordIndex, targetId))
                    {
                        Guid sentenceNodeId = GetNodeIdForTarget(NodeTargetKind.Sentence, sentenceId);
                        LinkNodeIds(sentenceNodeId, nodeId, NodeRelationType.ContainsWord);
                    }
                    break;
                case NodeTargetKind.Sentence:
                    if (TrustedSentenceDictionary.TryGetValue(targetId, out SentenceBox? sentenceBox))
                    {
                        Guid? senseId = sentenceBox.GetSenseID();
                        if (senseId.HasValue)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Sense, senseId.Value, NodeRelationType.HasSense);
                        }

                        List<Guid> words = sentenceBox.GetWords() ?? new List<Guid>();
                        foreach (Guid wordId in words)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Word, wordId, NodeRelationType.ContainsWord);
                        }
                        if (words.Count > 0)
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Word, words[0], NodeRelationType.LeadingWord);
                            LinkNodeToTarget(nodeId, NodeTargetKind.Word, words[words.Count - 1], NodeRelationType.TrailingWord);
                        }

                        if (senseId.HasValue && TrustedSenseDictionary.TryGetValue(senseId.Value, out SenseBox? senseBox))
                        {
                            senseBox.AddSentenceFromCore(targetId);
                            if (words.Count > 0)
                            {
                                senseBox.AddLeadingWordFromCore(words[0]);
                                senseBox.AddTrailingWordFromCore(words[words.Count - 1]);
                            }
                        }
                        foreach (Guid wordId in words)
                        {
                            if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? containedWord) &&
                                containedWord.GetUsageID().HasValue &&
                                TrustedUsageDictionary.TryGetValue(containedWord.GetUsageID()!.Value, out UsageBox? usageBox))
                            {
                                usageBox.AddSentenceFromCore(targetId);
                            }
                        }
                    }
                    foreach (Guid groupId in CopyIndexValues(GroupsBySentenceIndex, targetId))
                    {
                        Guid groupNodeId = GetNodeIdForTarget(NodeTargetKind.Group, groupId);
                        LinkNodeIds(groupNodeId, nodeId, NodeRelationType.ContainsSentence);
                    }
                    break;
                case NodeTargetKind.Sense:
                    foreach (Guid symbolId in CopyIndexValues(SymbolsBySenseIndex, targetId))
                    {
                        Guid symbolNodeId = GetNodeIdForTarget(NodeTargetKind.Symbol, symbolId);
                        LinkNodeIds(symbolNodeId, nodeId, NodeRelationType.HasSense);
                    }
                    foreach (Guid wordId in CopyIndexValues(WordsBySenseIndex, targetId))
                    {
                        Guid wordNodeId = GetNodeIdForTarget(NodeTargetKind.Word, wordId);
                        LinkNodeIds(wordNodeId, nodeId, NodeRelationType.HasSense);
                    }
                    foreach (Guid sentenceId in CopyIndexValues(SentencesBySenseIndex, targetId))
                    {
                        Guid sentenceNodeId = GetNodeIdForTarget(NodeTargetKind.Sentence, sentenceId);
                        LinkNodeIds(sentenceNodeId, nodeId, NodeRelationType.HasSense);
                    }
                    break;
                case NodeTargetKind.Usage:
                    foreach (Guid wordId in CopyIndexValues(WordsByUsageIndex, targetId))
                    {
                        Guid wordNodeId = GetNodeIdForTarget(NodeTargetKind.Word, wordId);
                        LinkNodeIds(wordNodeId, nodeId, NodeRelationType.HasUsage);
                    }
                    break;
                case NodeTargetKind.Group:
                    if (TrustedGroupDictionary.TryGetValue(targetId, out GroupBox? groupBox))
                    {
                        foreach (Guid sentenceId in groupBox.GetSentences() ?? new List<Guid>())
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
                        }
                    }
                    foreach (Guid sourceId in CopyIndexValues(SourcesByGroupIndex, targetId))
                    {
                        Guid sourceNodeId = GetNodeIdForTarget(NodeTargetKind.Source, sourceId);
                        LinkNodeIds(sourceNodeId, nodeId, NodeRelationType.ContainsGroup);
                    }
                    break;
                case NodeTargetKind.Source:
                    if (SourceDictionary.TryGetValue(targetId, out SourceBox? sourceBox))
                    {
                        foreach (Guid groupId in sourceBox.GetGroupingIDs() ?? new List<Guid>())
                        {
                            LinkNodeToTarget(nodeId, NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
                        }
                    }
                    break;
                case NodeTargetKind.Function:
                    break;
            }
            RecalculateAllNodePresentationsInternal();
        }

        private Guid GetNodeIdForTarget(NodeTargetKind kind, Guid targetId)
        {
            Node? node = GetTrustedNodeForTarget(kind, targetId);
            return node == null ? Guid.Empty : node.GetNodeID();
        }

        private bool AddTrustedLinkInternal(NodeLink link, bool addSymmetricReverse = true, bool validateRelationKinds = true)
        {
            if (!TrustedSkillsGraph.ContainsNode(link.SourceNodeId) ||
                !TrustedSkillsGraph.ContainsNode(link.TargetNodeId))
            {
                return false;
            }
            if (validateRelationKinds && (!IsValidLinkKindCombination(link) || !AreProvenanceTargetsTrusted(link.Provenance)))
            {
                return false;
            }

            if (!AddTrustedLinkOneDirection(link))
            {
                return false;
            }
            if (addSymmetricReverse && link.SourceNodeId != link.TargetNodeId && ShouldStoreReverse(link.RelationType))
            {
                NodeLink reverse = new NodeLink(link.TargetNodeId, link.SourceNodeId,
                    link.RelationType, link.Weight, link.Label, link.Provenance);
                if (!AddTrustedLinkOneDirection(reverse))
                {
                    RemoveTrustedLinkInternal(link);
                    return false;
                }
            }
            return true;
        }

        private bool AddTrustedLinkOneDirection(NodeLink link)
        {
            if (!TrustedSkillsGraph.ContainsNode(link.SourceNodeId) ||
                !TrustedSkillsGraph.ContainsNode(link.TargetNodeId) ||
                TrustedLinkKeys.Contains(link))
            {
                return false;
            }
            IEnumerable<NodeLink> assertionLinks = LinksByAssertionId.TryGetValue(link.Provenance.AssertionId, out List<NodeLink>? existingAssertionLinks)
                ? existingAssertionLinks : Enumerable.Empty<NodeLink>();
            if (assertionLinks.Any(existing => !existing.Equals(link) &&
                !(ShouldStoreReverse(link.RelationType) &&
                  existing.SourceNodeId == link.TargetNodeId &&
                  existing.TargetNodeId == link.SourceNodeId &&
                  existing.RelationType == link.RelationType &&
                  string.Equals(existing.Label, link.Label, StringComparison.Ordinal) &&
                  existing.Weight == link.Weight && existing.Provenance == link.Provenance)))
            {
                throw new ArgumentException($"AssertionId '{link.Provenance.AssertionId}' is already used by a different logical link.", nameof(link));
            }

            bool addedKey = false;
            bool addedOutgoing = false;
            bool addedIncoming = false;
            bool addedGraphEdge = false;
            bool addedIndexes = false;
            try
            {
                if (!TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? outgoing))
                {
                    outgoing = new List<NodeLink>();
                    TrustedOutgoingLinks.Add(link.SourceNodeId, outgoing);
                }
                if (!TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? incoming))
                {
                    incoming = new List<NodeLink>();
                    TrustedIncomingLinks.Add(link.TargetNodeId, incoming);
                }
                addedKey = TrustedLinkKeys.Add(link);
                if (!addedKey)
                {
                    if (outgoing.Count == 0) TrustedOutgoingLinks.Remove(link.SourceNodeId);
                    if (incoming.Count == 0) TrustedIncomingLinks.Remove(link.TargetNodeId);
                    return false;
                }
                outgoing.Add(link);
                addedOutgoing = true;
                incoming.Add(link);
                addedIncoming = true;
                TrustedSkillsGraph.AddEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                addedGraphEdge = true;
                addedIndexes = true;
                AddTrustedLinkIndexes(link);
                _graphEpoch = checked(_graphEpoch + 1);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                                KeyNotFoundException or InvalidDataException)
            {
                if (addedIndexes) RemoveTrustedLinkIndexes(link);
                if (addedOutgoing && TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? outgoing))
                    outgoing.Remove(link);
                if (addedIncoming && TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? incoming))
                    incoming.Remove(link);
                if (addedGraphEdge) TrustedSkillsGraph.RemoveEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                if (addedKey) TrustedLinkKeys.Remove(link);
                if (TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? remainingOutgoing) && remainingOutgoing.Count == 0)
                    TrustedOutgoingLinks.Remove(link.SourceNodeId);
                if (TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? remainingIncoming) && remainingIncoming.Count == 0)
                    TrustedIncomingLinks.Remove(link.TargetNodeId);
                return false;
            }
            catch
            {
                if (addedIndexes) RemoveTrustedLinkIndexes(link);
                if (addedOutgoing && TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? outgoing))
                    outgoing.Remove(link);
                if (addedIncoming && TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? incoming))
                    incoming.Remove(link);
                if (addedGraphEdge) TrustedSkillsGraph.RemoveEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                if (addedKey) TrustedLinkKeys.Remove(link);
                if (TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? remainingOutgoing) && remainingOutgoing.Count == 0)
                    TrustedOutgoingLinks.Remove(link.SourceNodeId);
                if (TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? remainingIncoming) && remainingIncoming.Count == 0)
                    TrustedIncomingLinks.Remove(link.TargetNodeId);
                throw;
            }
        }

        private void AddTrustedLinkIndexes(NodeLink link)
        {
            AddIndexedLink(LinksByAssertionId, link.Provenance.AssertionId, link);
            if (link.Provenance.SourceTargetId.HasValue)
                AddIndexedLink(LinksBySourceTargetId, link.Provenance.SourceTargetId.Value, link);
            if (link.Provenance.EvidenceTargetId.HasValue)
                AddIndexedLink(LinksByEvidenceTargetId, link.Provenance.EvidenceTargetId.Value, link);
        }

        private static void AddIndexedLink(Dictionary<Guid, List<NodeLink>> index, Guid key, NodeLink link)
        {
            if (!index.TryGetValue(key, out List<NodeLink>? links))
            {
                links = new List<NodeLink>();
                index.Add(key, links);
            }
            links.Add(link);
        }

        private void RemoveTrustedLinkIndexes(NodeLink link)
        {
            RemoveIndexedLink(LinksByAssertionId, link.Provenance.AssertionId, link);
            if (link.Provenance.SourceTargetId.HasValue)
                RemoveIndexedLink(LinksBySourceTargetId, link.Provenance.SourceTargetId.Value, link);
            if (link.Provenance.EvidenceTargetId.HasValue)
                RemoveIndexedLink(LinksByEvidenceTargetId, link.Provenance.EvidenceTargetId.Value, link);
        }

        private static void RemoveIndexedLink(Dictionary<Guid, List<NodeLink>> index, Guid key, NodeLink link)
        {
            if (!index.TryGetValue(key, out List<NodeLink>? links)) return;
            links.Remove(link);
            if (links.Count == 0) index.Remove(key);
        }

        private bool ReplaceTrustedLink(NodeLink replacement)
        {
            if (!TrustedLinkKeys.Contains(replacement) ||
                !TrustedOutgoingLinks.TryGetValue(replacement.SourceNodeId, out List<NodeLink>? outgoing) ||
                !TrustedIncomingLinks.TryGetValue(replacement.TargetNodeId, out List<NodeLink>? incoming))
            {
                return false;
            }
            int outgoingIndex = outgoing.FindIndex(link => link.Equals(replacement));
            if (outgoingIndex < 0) return false;
            NodeLink previous = outgoing[outgoingIndex];
            int incomingIndex = incoming.IndexOf(previous);
            if (incomingIndex < 0) return false;
            if (!TrustedSkillsGraph.RemoveEdge(previous.SourceNodeId, previous.TargetNodeId, previous.Weight)) return false;
            try
            {
                TrustedSkillsGraph.AddEdge(replacement.SourceNodeId, replacement.TargetNodeId, replacement.Weight);
            }
            catch
            {
                TrustedSkillsGraph.AddEdge(previous.SourceNodeId, previous.TargetNodeId, previous.Weight);
                throw;
            }
            outgoing[outgoingIndex] = replacement;
            incoming[incomingIndex] = replacement;
            RemoveTrustedLinkIndexes(previous);
            AddTrustedLinkIndexes(replacement);
            TrustedLinkKeys.Remove(previous);
            TrustedLinkKeys.Add(replacement);
            _graphEpoch = checked(_graphEpoch + 1);
            return true;
        }

        private bool RemoveTrustedLinkInternal(NodeLink identity)
        {
            if (!TrustedLinkKeys.Contains(identity) ||
                !TrustedOutgoingLinks.TryGetValue(identity.SourceNodeId, out List<NodeLink>? outgoing) ||
                !TrustedIncomingLinks.TryGetValue(identity.TargetNodeId, out List<NodeLink>? incoming))
            {
                return false;
            }
            int linkIndex = outgoing.FindIndex(link => link.Equals(identity));
            if (linkIndex < 0) return false;
            NodeLink removed = outgoing[linkIndex];
            if (!incoming.Contains(removed)) return false;
            if (!TrustedSkillsGraph.RemoveEdge(removed.SourceNodeId, removed.TargetNodeId, removed.Weight)) return false;
            TrustedLinkKeys.Remove(identity);
            RemoveTrustedLinkIndexes(removed);
            outgoing.RemoveAt(linkIndex);
            incoming.Remove(removed);
            if (outgoing.Count == 0) TrustedOutgoingLinks.Remove(identity.SourceNodeId);
            if (incoming.Count == 0) TrustedIncomingLinks.Remove(identity.TargetNodeId);
            _graphEpoch = checked(_graphEpoch + 1);
            return true;
        }

        private static bool ShouldStoreReverse(NodeRelationType relationType) =>
            relationType is NodeRelationType.Synonym or NodeRelationType.Antonym;

        /// <summary>Read-only mirror of the checks in <see cref="RemoveTrustedLinkInternal"/>; mutates nothing.</summary>
        private bool CanRemoveTrustedLinkInternal(NodeLink identity)
        {
            if (!TrustedLinkKeys.Contains(identity) ||
                !TrustedOutgoingLinks.TryGetValue(identity.SourceNodeId, out List<NodeLink>? outgoing) ||
                !TrustedIncomingLinks.TryGetValue(identity.TargetNodeId, out List<NodeLink>? incoming))
            {
                return false;
            }
            int linkIndex = outgoing.FindIndex(link => link.Equals(identity));
            if (linkIndex < 0) return false;
            NodeLink removed = outgoing[linkIndex];
            return incoming.Contains(removed) &&
                   TrustedSkillsGraph.ContainsEdge(removed.SourceNodeId, removed.TargetNodeId, removed.Weight);
        }

        private static void ValidateLinkQuery(
            NodeRelationType? relationFilter,
            NodeLinkOrigin? originFilter,
            double? minimumConfidence)
        {
            if (relationFilter.HasValue && !Enum.IsDefined(relationFilter.Value))
                throw new ArgumentOutOfRangeException(nameof(relationFilter));
            if (originFilter.HasValue && !Enum.IsDefined(originFilter.Value))
                throw new ArgumentOutOfRangeException(nameof(originFilter));
            if (minimumConfidence.HasValue &&
                (!double.IsFinite(minimumConfidence.Value) || minimumConfidence.Value < 0.0 || minimumConfidence.Value > 1.0))
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
        }

        private static void ValidatePaging(int? limit, int offset)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (limit.HasValue && limit.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be non-negative.");
        }

        private static bool FunctionValuesEqual(object? left, object? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            if (left is Array leftArray && right is Array rightArray)
            {
                if (leftArray.GetType() != rightArray.GetType() || leftArray.Length != rightArray.Length) return false;
                for (int i = 0; i < leftArray.Length; i++)
                    if (!FunctionValuesEqual(leftArray.GetValue(i), rightArray.GetValue(i))) return false;
                return true;
            }
            return Equals(left, right);
        }

        private static bool MatchesLinkQuery(
            NodeLink link,
            NodeRelationType? relationFilter,
            NodeLinkOrigin? originFilter,
            string? authorFilter,
            double? minimumConfidence,
            DateTimeOffset? activeAt)
        {
            return (!relationFilter.HasValue || link.RelationType == relationFilter.Value) &&
                   (!originFilter.HasValue || link.Provenance.Origin == originFilter.Value) &&
                   (authorFilter == null || string.Equals(link.Provenance.Author, authorFilter, StringComparison.OrdinalIgnoreCase)) &&
                   (!minimumConfidence.HasValue || link.Weight >= minimumConfidence.Value) &&
                   (!activeAt.HasValue || link.Provenance.IsActiveAt(activeAt.Value));
        }

        private bool HasMatchingIncidentLink(
            Guid nodeId,
            NodeRelationType? relationFilter,
            double? minimumConfidence,
            DateTimeOffset activeAt)
        {
            bool Matches(NodeLink link) =>
                (!relationFilter.HasValue || link.RelationType == relationFilter.Value) &&
                (!minimumConfidence.HasValue || link.Weight >= minimumConfidence.Value) &&
                link.Provenance.IsActiveAt(activeAt);
            return (TrustedOutgoingLinks.TryGetValue(nodeId, out List<NodeLink>? outgoing) && outgoing.Any(Matches)) ||
                   (TrustedIncomingLinks.TryGetValue(nodeId, out List<NodeLink>? incoming) && incoming.Any(Matches));
        }

        private Graph<Guid> BuildSkillsGraphSnapshotInternal(
            NodeRelationType? relationFilter,
            NodeLinkOrigin? originFilter,
            string? authorFilter,
            double? minimumConfidence,
            DateTimeOffset? activeAt)
        {
            Graph<Guid> graph = new Graph<Guid>(true);
            string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
            DateTimeOffset instant = activeAt ?? DateTimeOffset.UtcNow;

            foreach (Guid nodeId in TrustedSkillsGraph.Nodes)
            {
                graph.AddNode(nodeId);
            }
            foreach (List<NodeLink> links in TrustedOutgoingLinks.Values)
            {
                foreach (NodeLink link in links)
                {
                    if (MatchesLinkQuery(link, relationFilter, originFilter, normalizedAuthor, minimumConfidence, instant))
                    {
                        graph.AddEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                    }
                }
            }
            return graph;
        }

        /// <summary>Returns an existing trusted symbol box with matching identity, or creates a new untrusted symbol box.</summary>
        /// <param name="symbolChar">Symbol character.</param>
        /// <param name="senseId">Sense identifier.</param>
        /// <returns>The canonical trusted <see cref="SymbolBox"/> if registered, otherwise a new untrusted instance.</returns>
        public SymbolBox GetNewSymbolBox(char symbolChar, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                SymbolIdentityKey identityKey = new SymbolIdentityKey(symbolChar, senseId);
                if (TrustedSymbolIdentityIndex.TryGetValue(identityKey, out Guid existingSymbolId) &&
                    GetTrustedSymbolBox(existingSymbolId) is SymbolBox existingSymbolBox)
                {
                    return existingSymbolBox;
                }

                SymbolBox newSymbolBox = new SymbolBox(GenerateUntrustedId(NodeTargetKind.Symbol), symbolChar);
                newSymbolBox.SetSenseID(senseId);
                return newSymbolBox;
            });
        }

        /// <summary>Returns an existing trusted word box with matching identity, or creates a new untrusted word box.</summary>
        /// <param name="symbols">Constituent symbol identifiers.</param>
        /// <param name="senseId">Sense identifier.</param>
        /// <param name="usageId">Usage identifier.</param>
        /// <param name="partOfSpeech">Part of speech.</param>
        /// <returns>The canonical trusted <see cref="WordBox"/> if registered, otherwise a new untrusted instance.</returns>
        public WordBox GetNewWordBox(List<Guid> symbols, Guid senseId, Guid usageId, PartOfSpeech partOfSpeech)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                WordIdentityKey identityKey = new WordIdentityKey(symbols, senseId, usageId, partOfSpeech);
                if (TrustedWordIdentityIndex.TryGetValue(identityKey, out Guid existingWordId) &&
                    GetTrustedWordBox(existingWordId) is WordBox existingWord)
                {
                    return existingWord;
                }

                return new WordBox(GenerateUntrustedId(NodeTargetKind.Word), senseId, usageId, symbols, partOfSpeech);
            });
        }

        /// <summary>Returns an existing trusted sentence box with matching identity, or creates a new untrusted sentence box.</summary>
        /// <param name="words">Constituent word identifiers.</param>
        /// <param name="senseId">Sense identifier.</param>
        /// <param name="checkUntrusted">When true, a staged untrusted sentence with the same identity is returned after the trusted check.</param>
        /// <returns>The canonical trusted or matching staged <see cref="SentenceBox"/> if found, otherwise a new unregistered untrusted instance.</returns>
        public SentenceBox GetNewSentenceBox(List<Guid> words, Guid senseId, bool checkUntrusted = false)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                SentenceIdentityKey identityKey = new SentenceIdentityKey(words, senseId);
                if (TrustedSentenceIdentityIndex.TryGetValue(identityKey, out Guid existingSentenceId) &&
                    GetTrustedSentenceBox(existingSentenceId) is SentenceBox existingSentence)
                {
                    return existingSentence;
                }

                if (checkUntrusted)
                {
                    SentenceBox? stagedSentence = GetUntrustedSentenceBoxByIdentity(words, senseId);
                    if (stagedSentence != null)
                    {
                        return stagedSentence;
                    }
                }

                return new SentenceBox(GenerateUntrustedId(NodeTargetKind.Sentence), senseId, words);
            });
        }

        internal bool IsTrustedIdRegistered(NodeTargetKind kind, Guid id)
        {
            if (id == Guid.Empty)
            {
                return false;
            }
            return GraphSynchronization.Execute(() =>
            {
                if (!_initialized)
                {
                    return false;
                }
                if (TrustedNodeByTargetIndex.ContainsKey((kind, id)))
                {
                    return true;
                }
                return kind switch
                {
                    NodeTargetKind.Symbol => TrustedSymbolDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Symbols, id),
                    NodeTargetKind.Word => TrustedWordDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Words, id),
                    NodeTargetKind.Sentence => TrustedSentenceDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Sentences, id),
                    NodeTargetKind.Sense => TrustedSenseDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Senses, id),
                    NodeTargetKind.Usage => TrustedUsageDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Usages, id),
                    NodeTargetKind.Source => SourceDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Sources, id),
                    NodeTargetKind.Group => TrustedGroupDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Groups, id),
                    NodeTargetKind.Function => TrustedFunctionDictionary.ContainsKey(id) || IsPagedRecordAvailable(DiskPartition.Functions, id),
                    _ => false
                };
            });
        }

        internal bool IsUntrustedIdRegistered(NodeTargetKind kind, Guid id)
        {
            if (id == Guid.Empty)
            {
                return false;
            }
            return GraphSynchronization.Execute(() =>
            {
                if (!_initialized)
                {
                    return false;
                }
                if (UntrustedNodeByTargetIndex.ContainsKey((kind, id)))
                {
                    return true;
                }
                return kind switch
                {
                    NodeTargetKind.Symbol => UntrustedSymbolDictionary.ContainsKey(id),
                    NodeTargetKind.Word => UntrustedWordDictionary.ContainsKey(id),
                    NodeTargetKind.Sentence => UntrustedSentenceDictionary.ContainsKey(id),
                    NodeTargetKind.Function => UntrustedFunctionDictionary.ContainsKey(id),
                    _ => false
                };
            });
        }

        internal Guid GenerateUntrustedId(NodeTargetKind kind, Func<Guid>? guidGenerator = null)
        {
            Func<Guid> generator = guidGenerator ?? Guid.NewGuid;
            return GraphSynchronization.Execute(() =>
            {
                const int maxAttempts = 1024;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Guid candidate = generator();
                    if (candidate != Guid.Empty &&
                        !IsTrustedIdRegistered(kind, candidate) &&
                        !IsUntrustedIdRegistered(kind, candidate))
                    {
                        return candidate;
                    }
                }
                throw new InvalidOperationException($"Unable to generate a non-colliding untrusted {kind} identifier.");
            });
        }

        internal void ValidateNewUntrustedId(NodeTargetKind kind, Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
            if (IsTrustedIdRegistered(kind, id))
            {
                throw new ArgumentException($"The identifier '{id}' conflicts with a registered trusted {kind} object.", parameterName);
            }
        }

        internal bool IsTrustedNodeIdRegistered(Guid nodeId)
        {
            if (nodeId == Guid.Empty)
            {
                return false;
            }
            return GraphSynchronization.Execute(() =>
                _initialized && (GetTrustedNodeInternal(nodeId) != null));
        }

        internal Guid GenerateUntrustedNodeId(Func<Guid>? guidGenerator = null)
        {
            Func<Guid> generator = guidGenerator ?? Guid.NewGuid;
            return GraphSynchronization.Execute(() =>
            {
                const int maxAttempts = 1024;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Guid candidate = generator();
                    if (candidate != Guid.Empty &&
                        !IsTrustedNodeIdRegistered(candidate) &&
                        (!_initialized || !UntrustedNodeDictionary.ContainsKey(candidate)))
                    {
                        return candidate;
                    }
                }
                throw new InvalidOperationException("Unable to generate a non-colliding untrusted Node identifier.");
            });
        }

        internal void ValidateNewUntrustedNodeId(Guid nodeId, string parameterName)
        {
            if (nodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", parameterName);
            }
            if (IsTrustedNodeIdRegistered(nodeId))
            {
                throw new ArgumentException($"The node identifier '{nodeId}' conflicts with a registered trusted Node.", parameterName);
            }
        }

        internal bool IsConversationIdRegistered(Guid conversationId)
        {
            if (conversationId == Guid.Empty)
            {
                return false;
            }
            return GraphSynchronization.Execute(() =>
                _initialized &&
                (TextConversationDictionary.ContainsKey(conversationId) ||
                 SymbolicConversationDictionary.ContainsKey(conversationId)));
        }

        internal Guid GenerateConversationId(Func<Guid>? guidGenerator = null)
        {
            Func<Guid> generator = guidGenerator ?? Guid.NewGuid;
            return GraphSynchronization.Execute(() =>
            {
                const int maxAttempts = 1024;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Guid candidate = generator();
                    if (candidate != Guid.Empty && !IsConversationIdRegistered(candidate))
                    {
                        return candidate;
                    }
                }
                throw new InvalidOperationException("Unable to generate a non-colliding Conversation identifier.");
            });
        }

        internal void ValidateNewConversationId(Guid conversationId, string parameterName)
        {
            if (conversationId == Guid.Empty)
            {
                throw new ArgumentException("Conversation identifiers cannot be Guid.Empty.", parameterName);
            }
            if (IsConversationIdRegistered(conversationId))
            {
                throw new ArgumentException($"The conversation identifier '{conversationId}' conflicts with a registered conversation.", parameterName);
            }
        }

    }
}
