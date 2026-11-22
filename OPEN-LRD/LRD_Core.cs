using System.Collections;
using System.Collections.ObjectModel;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
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
        private bool _suspendNodePresentationRecalculation;
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
        private Graph<Guid> TrustedSkillsGraph = new Graph<Guid>(true);
        private PartitionedBinaryStore? _diskStore;
        private string? _diskStorePath;
        private bool _diskPagedReadOnly;
        private bool _legacyDiskStoreReadOnly;
        private int _maxPagedObjects = 1024;
        private readonly Dictionary<(DiskPartition Partition, Guid Id), object> _pagedObjectCache = new Dictionary<(DiskPartition Partition, Guid Id), object>();
        private readonly Dictionary<(DiskPartition Partition, Guid Id), LinkedListNode<(DiskPartition Partition, Guid Id)>> _pagedObjectLruNodes = new Dictionary<(DiskPartition Partition, Guid Id), LinkedListNode<(DiskPartition Partition, Guid Id)>>();
        private readonly LinkedList<(DiskPartition Partition, Guid Id)> _pagedObjectLru = new LinkedList<(DiskPartition Partition, Guid Id)>();


        /// <summary>Initializes the singleton core type.</summary>
        static LRD_Core() { }

        /// <summary>Prevents external instantiation of the singleton core.</summary>
        private LRD_Core() { }

        public static LRD_Core Instance => _instance;

        /// <summary>Initializes or resets the singleton graph and its runtime state.</summary>
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
            });
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

        /// <summary>
        ///    
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usageId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
        public SymbolBox? GetUntrustedSymbolBox(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ? symbolBox : null;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
        public WordBox? GetUntrustedWordBox(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ? wordBox : null;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public SentenceBox? GetUntrustedSentenceBox(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ? sentenceBox : null;
            });
        }

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

        public FunctionBox? GetUntrustedFunctionBox(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? functionBox) ? functionBox : null;
            });
        }

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

        public ConversationText? GetTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return TextConversationDictionary.TryGetValue(conversationId, out ConversationText? conversation) ? conversation : null;
            });
        }

        public ConversationSymbolic? GetSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return SymbolicConversationDictionary.TryGetValue(conversationId, out ConversationSymbolic? conversation) ? conversation : null;
            });
        }

        public Node? GetTrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return
                TrustedNodeDictionary.TryGetValue(nodeId, out Node? node)
                    ? node
                    : GetPagedObject(DiskPartition.Nodes, nodeId, payload => FoundationBinarySerializer.DeserializeNode(payload, true), box => { });
            });
        }

        public Node? GetUntrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
            });
        }

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

        public Node? GetTrustedNodeByTarget(NodeTargetKind kind, Guid targetId, string? label)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                NodeIdentityKey key = NodeIdentityKey.Create(kind, targetId, label);
                return TrustedNodeByIdentityIndex.TryGetValue(key, out Guid nodeId) ? GetTrustedNode(nodeId) : null;
            });
        }

        public IReadOnlyList<Node> GetTrustedNodesByTarget(NodeTargetKind kind, Guid targetId)
        {
            return GraphSynchronization.Execute<IReadOnlyList<Node>>(() =>
            {
                EnsureCoreInitialized();
                _ = NodeIdentityKey.Create(kind, targetId, null);
                List<Node> nodes = TrustedNodeByIdentityIndex
                    .Where(entry => entry.Key.TargetKind == kind && entry.Key.TargetId == targetId)
                    .OrderBy(entry => entry.Key.Label, StringComparer.Ordinal)
                    .Select(entry => GetTrustedNode(entry.Value))
                    .Where(node => node != null)
                    .Cast<Node>()
                    .ToList();
                return nodes.AsReadOnly();
            });
        }

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

        public List<Guid> GetTrustedNodeIds(NodeTargetKind? kindFilter = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                return TrustedNodeByIdentityIndex
                .Where(entry => !kindFilter.HasValue || entry.Key.TargetKind == kindFilter.Value)
                .OrderBy(entry => entry.Key.TargetKind)
                .ThenBy(entry => entry.Key.TargetId)
                .ThenBy(entry => entry.Key.Label, StringComparer.Ordinal)
                .Select(entry => entry.Value)
                .ToList();
            });
        }

        public List<Node> GetTrustedNodes(NodeTargetKind? kindFilter = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (kindFilter.HasValue && !Enum.IsDefined(kindFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(kindFilter));
                return GetTrustedNodeIds(kindFilter)
                    .Select(GetTrustedNode)
                    .Where(node => node != null)
                    .Cast<Node>()
                    .ToList();
            });
        }

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

        public List<NodeLink> GetAllTrustedLinks(
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
                string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
                return TrustedOutgoingLinks.Values
                .SelectMany(links => links)
                .Where(link => MatchesLinkQuery(link, relationFilter, originFilter, normalizedAuthor, minimumConfidence, activeAt))
                .ToList();
            });
        }

        public int GetTrustedNodeCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return TrustedNodeByIdentityIndex.Count; });
        public int GetUntrustedNodeCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return UntrustedNodeDictionary.Count; });
        public int GetTrustedLinkCount() => GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return TrustedLinkKeys.Count; });

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

        public IReadOnlyDictionary<UInt128, NodeRepresentation> GetNodePresentations()
        {
            return GraphSynchronization.Execute<IReadOnlyDictionary<UInt128, NodeRepresentation>>(() =>
            {
                EnsureCoreInitialized();
                return new ReadOnlyDictionary<UInt128, NodeRepresentation>(
                    new Dictionary<UInt128, NodeRepresentation>(NodePresentation));
            });
        }

        public int GetNodePresentationCount() => GraphSynchronization.Execute(() =>
        {
            EnsureCoreInitialized();
            return NodePresentation.Count;
        });

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

        public NodeLayoutOptions GetNodeLayoutOptions()
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                return new NodeLayoutOptions(_nodeLayoutOptions.NodeWeightBias, _nodeLayoutOptions.NeighborPositionBias);
            });
        }

        public void UpdateNodeLayoutOptions(double nodeWeightBias, double neighborPositionBias)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                _nodeLayoutOptions = new NodeLayoutOptions(nodeWeightBias, neighborPositionBias);
                RecalculateAllNodePresentationsInternal();
            });
        }

        public void RecalculateAllNodePresentations()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                RecalculateAllNodePresentationsInternal();
            });
        }

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
            if (_suspendNodePresentationRecalculation) return;
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
            foreach (KeyValuePair<Guid, Node> entry in TrustedNodeDictionary.Concat(UntrustedNodeDictionary))
            {
                Node node = entry.Value;
                bool isTrusted = node.IsTrusted;
                List<NodeDerivationLink> links = BuildNodeDerivationLinks(entry.Key);
                NodeRelationType? derivedClass = NodePresentationCalculator.CalculateClass(links);
                double weight = NodePresentationCalculator.CalculateWeight(links);
                double incidentConfidenceSum = links.Sum(link => link.Weight);
                double trustRating = NodePresentationCalculator.CalculateTrustRating(
                    node.GetVerifiedReferences(), node.GetInvalidReferences(), incidentConfidenceSum, isTrusted);
                string? summary = isTrusted
                    ? GetTrustedNodeSummary(node) ?? node.GetLabel()
                    : GetUntrustedNodeSummary(node) ?? node.GetLabel();
                List<Guid> neighbors = links.Select(link => link.OtherNodeId)
                    .Where(id => id != entry.Key)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();
                snapshots[entry.Key] = new NodeDerivationSnapshot(
                    entry.Key,
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
            foreach (List<NodeLink> outgoing in TrustedOutgoingLinks.Values)
            {
                foreach (NodeLink link in outgoing)
                {
                    if (link.SourceNodeId != nodeId && link.TargetNodeId != nodeId) continue;
                    Guid otherNodeId = link.SourceNodeId == nodeId ? link.TargetNodeId : link.SourceNodeId;
                    if (otherNodeId == nodeId) continue;
                    NodeDerivationLink derived = new NodeDerivationLink(
                        link.Provenance.AssertionId, link.RelationType, link.Weight, otherNodeId);
                    if (!links.TryGetValue(derived.AssertionId, out NodeDerivationLink existing) ||
                        PreferDerivationLink(derived, existing))
                    {
                        links[derived.AssertionId] = derived;
                    }
                }
            }
            return links.Values.ToList();
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
        public char? GetTrustedSymbolChar(Guid symbolId)

        {
            return GraphSynchronization.Execute<char?>(() =>
                GetTrustedSymbolBox(symbolId)?.GetSymbolChar());
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolChar"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public List<Guid> GetTrustedSymbolsBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SymbolsBySenseIndex, senseId); });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public List<Guid> GetTrustedWordsBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsBySenseIndex, senseId); });
        }

        public List<Guid> GetTrustedSentencesBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SentencesBySenseIndex, senseId); });
        }

        public List<Guid> GetTrustedWordsByUsageID(Guid usageId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsByUsageIndex, usageId); });
        }

        public List<Guid> GetTrustedWordsContainingSymbol(Guid symbolId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(WordsBySymbolIndex, symbolId); });
        }

        public List<Guid> GetTrustedSentencesContainingWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SentencesByWordIndex, wordId); });
        }

        public List<Guid> GetTrustedGroupsContainingSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(GroupsBySentenceIndex, sentenceId); });
        }

        public List<Guid> GetTrustedSourcesContainingGroup(Guid groupId)
        {
            return GraphSynchronization.Execute(() => { EnsureCoreInitialized(); return CopyIndexValues(SourcesByGroupIndex, groupId); });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public List<Guid>? GetWordsFromTrustedSentenceID(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
                GetTrustedSentenceBox(sentenceId)?.GetWords() ??
                (TrustedSentenceDictionary.ContainsKey(sentenceId) || IsPagedRecordAvailable(DiskPartition.Sentences, sentenceId)
                    ? new List<Guid>()
                    : null));
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
        public string GetTextFromWordID(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
                GetTrustedWordBox(wordId) is WordBox wordBox ? GetTextFromWordBox(wordBox) : string.Empty);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
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
                foreach (Node node in TrustedNodeDictionary.Values)
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
                TrustedSkillsGraph = new Graph<Guid>(true);
                UntrustedSymbolDictionary.Clear();
                UntrustedWordDictionary.Clear();
                UntrustedSentenceDictionary.Clear();
                UntrustedFunctionDictionary.Clear();
                _initialized = false;
            });
        }

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
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
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <param name="senseId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <param name="senseId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public bool SetUntrustedSentenceSenseID(Guid sentenceId, Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    sentenceBox.SetSenseID(senseId);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <param name="symbols"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <param name="words"></param>
        public bool SetUntrustedSentenceWords(Guid sentenceId, List<Guid> words)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    sentenceBox.SetWords(words);
                    RecalculateAllNodePresentationsInternal();
                    return true;
                }
                return false;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <param name="usageId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usageBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseBox"></param>
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


        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordBox"></param>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceBox"></param>
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

                UntrustedSentenceDictionary[sentenceId] = sentenceBox;
                if (!EnsureUntrustedNode(NodeTargetKind.Sentence, sentenceId))
                    RecalculateAllNodePresentationsInternal();
                return true;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public bool RemoveUntrustedSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                bool removed = UntrustedSentenceDictionary.Remove(sentenceId);
                RemoveUntrustedNodeForTarget(NodeTargetKind.Sentence, sentenceId);
                return removed;
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
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
                UntrustedSentenceDictionary.Remove(sentenceId);
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

                foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                {
                    if (TrustedSymbolDictionary.ContainsKey(symbolId)) continue;
                    if (!UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ||
                        !symbolBox.HasCompleteIdentity || !HasTrustedSense(symbolBox.GetSenseID())) return null;
                }
                foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
                {
                    if (ResolveOrPromoteUntrustedSymbol(symbolId) == null) return null;
                }

                if (!UntrustedWordDictionary.TryGetValue(wordId, out wordBox) || !CanTrustWord(wordBox)) return null;
                WordIdentityKey identityKey = wordBox.GetIdentityKey();
                if (!TrustedWordIdentityIndex.TryGetValue(identityKey, out Guid canonicalWordId))
                {
                    return ConvertUntrustedWordToTrusted(wordId) ? wordId : null;
                }

                List<SentenceBox> referencingSentences = UntrustedSentenceDictionary.Values
                    .Where(sentence => (sentence.GetWords() ?? new List<Guid>()).Contains(wordId))
                    .ToList();
                if (referencingSentences.Any(sentence => sentence.IsIdentityFrozen)) return null;
                foreach (SentenceBox sentence in referencingSentences)
                {
                    sentence.ReplaceWordReference(wordId, canonicalWordId);
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

                List<Guid> wordIds = sentenceBox.GetWords() ?? new List<Guid>();
                foreach (Guid wordId in wordIds)
                {
                    if (TrustedWordDictionary.ContainsKey(wordId)) continue;
                    if (!UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ||
                        !wordBox.HasCompleteIdentity || !HasTrustedSense(wordBox.GetSenseID()) ||
                        !wordBox.GetUsageID().HasValue || !TrustedUsageDictionary.ContainsKey(wordBox.GetUsageID()!.Value))
                    {
                        return null;
                    }
                    foreach (Guid symbolId in wordBox.GetSymbols() ?? new List<Guid>())
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
                    UntrustedSentenceDictionary.Remove(sentenceId);
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
            DateTimeOffset? validUntil = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                NodeLinkProvenance provenance = NodeLinkProvenance.CreateReasoning(
                    sourceNodeId, targetNodeId, relationType, label, confidence, author,
                    sourceTargetId, evidenceTargetId, validFrom, validUntil);
                NodeLink link = new NodeLink(sourceNodeId, targetNodeId, relationType,
                    confidence, label, provenance);
                bool added = !IsStructuralRelation(relationType) && IsValidLinkKindCombination(link) &&
                             AreProvenanceTargetsTrusted(provenance) && AddTrustedLinkInternal(link);
                if (added) RecalculateAllNodePresentationsInternal();
                return added;
            });
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
                List<NodeLink> matchingLinks = TrustedLinkKeys
                    .Where(link => link.Provenance.AssertionId == assertionId)
                    .ToList();
                if (matchingLinks.Count == 0 || matchingLinks.Any(link => link.Provenance.Origin != NodeLinkOrigin.Reasoning))
                    return false;
                return UpdateTrustedLinkWeight(assertionId, newConfidence);
            });
        }

        public List<NodeLink> GetOutgoingLinks(
            Guid nodeId,
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
                if (!TrustedOutgoingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
                return links.Where(link => MatchesLinkQuery(link, relationFilter, originFilter,
                    normalizedAuthor, minimumConfidence, activeAt)).ToList();
            });
        }

        public List<NodeLink> GetIncomingLinks(
            Guid nodeId,
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
                if (!TrustedIncomingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                string? normalizedAuthor = NormalizeOptionalLinkText(authorFilter);
                return links.Where(link => MatchesLinkQuery(link, relationFilter, originFilter,
                    normalizedAuthor, minimumConfidence, activeAt)).ToList();
            });
        }

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

        /// <summary>Returns distinct assertions whose provenance references the supplied trusted target.</summary>
        public List<NodeLink> GetTrustedLinksByProvenanceTarget(
            Guid targetId,
            bool includeSourceTarget = true,
            bool includeEvidenceTarget = true,
            NodeLinkOrigin? originFilter = null)
        {
            if (targetId == Guid.Empty)
                throw new ArgumentException("Target identifiers cannot be Guid.Empty.", nameof(targetId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureCoreInitialized();
                if (originFilter.HasValue && !Enum.IsDefined(originFilter.Value))
                    throw new ArgumentOutOfRangeException(nameof(originFilter));
                return TrustedLinkKeys
                    .Where(link => (!originFilter.HasValue || link.Provenance.Origin == originFilter.Value) &&
                        ((includeSourceTarget && link.Provenance.SourceTargetId == targetId) ||
                         (includeEvidenceTarget && link.Provenance.EvidenceTargetId == targetId)))
                    .GroupBy(link => link.Provenance.AssertionId)
                    .Select(group => group.First())
                    .ToList();
            });
        }

        /// <summary>Finds trusted nodes using target kind, label, trust weight, and incident-link filters.</summary>
        public List<Node> FindTrustedNodes(
            NodeTargetKind? kindFilter = null,
            string? labelContains = null,
            int? minimumNetTrustWeight = null,
            NodeRelationType? relationFilter = null,
            double? minimumIncidentLinkConfidence = null,
            bool canonicalOnly = false)
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

                string? normalizedLabel = NormalizeOptionalLinkText(labelContains);
                return TrustedNodeDictionary.Values
                    .Where(node => !kindFilter.HasValue || node.GetTargetKind() == kindFilter.Value)
                    .Where(node => !canonicalOnly || node.GetLabel() == null)
                    .Where(node => normalizedLabel == null ||
                        (node.GetLabel()?.Contains(normalizedLabel, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Where(node => !minimumNetTrustWeight.HasValue ||
                        node.GetNetTrustWeight() >= minimumNetTrustWeight.Value)
                    .Where(node => !relationFilter.HasValue && !minimumIncidentLinkConfidence.HasValue ||
                        HasMatchingIncidentLink(node.GetNodeID(), relationFilter, minimumIncidentLinkConfidence))
                    .OrderBy(node => node.GetTargetKind())
                    .ThenBy(node => node.GetTargetID())
                    .ThenBy(node => node.GetLabel(), StringComparer.Ordinal)
                    .ToList();
            });
        }

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
                    ValidateDiskStoreContents(incoming);
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
                            mergeStage._suspendNodePresentationRecalculation = true;
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
                                mergeStage._suspendNodePresentationRecalculation = false;
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
            ClearAllDictionaries();
            CopyTrustedStateFrom(staged);
            _diskStore = staged._diskStore;
            staged._diskStore = null;
            _diskStorePath = staged._diskStorePath;
            staged._diskStorePath = null;
            _diskPagedReadOnly = staged._diskPagedReadOnly;
            _legacyDiskStoreReadOnly = staged._legacyDiskStoreReadOnly;
            _maxPagedObjects = staged._maxPagedObjects;
            _initialized = true;
        }

        private void InstallMergedTrustedStateFrom(LRD_Core staged)
        {
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
            foreach (Node node in TrustedNodeDictionary.Values)
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
                if (TrustedSentenceDictionary.ContainsKey(id)) UntrustedSentenceDictionary.Remove(id);
            foreach (Guid id in new List<Guid>(UntrustedFunctionDictionary.Keys))
                if (TrustedFunctionDictionary.ContainsKey(id)) UntrustedFunctionDictionary.Remove(id);

            _diskStorePath = existingDiskStorePath;
            _diskPagedReadOnly = false;
            _legacyDiskStoreReadOnly = false;
            _maxPagedObjects = existingPagedObjectLimit;
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

        public void FlushTrustedDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStorePath == null) throw new InvalidOperationException("No disk store is attached.");
                StreamTrustedDictionariesToDisk(_diskStorePath);
            });
        }

        public void CompactTrustedDiskStore()
        {
            FlushTrustedDiskStore();
        }

        public void CompactDiskStore() => CompactTrustedDiskStore();

        public void ValidateDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStore == null) throw new InvalidOperationException("No disk store is attached.");
                _diskStore.Validate();
                ValidateDiskStoreContents(_diskStore);
            });
        }

        public bool IsDiskPagedReadOnly => GraphSynchronization.Execute(() => _diskPagedReadOnly);

        public bool IsLegacyDiskStoreReadOnly => GraphSynchronization.Execute(() => _legacyDiskStoreReadOnly);

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
                [DiskPartition.Nodes] = ToDiskRecords(TrustedNodeDictionary.Values, box => box.GetNodeID(), FoundationBinarySerializer.SerializeNode),
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
            foreach (KeyValuePair<UInt128, NodeRepresentation> entry in NodePresentation.OrderBy(pair => pair.Key))
            {
                NodeRepresentation representation = entry.Value;
                Guid nodeId = representation.GetNodeID();
                if (TrustedNodeDictionary.ContainsKey(nodeId))
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

        private void ValidateDiskStoreContents(PartitionedBinaryStore store)
        {
            HashSet<Guid> symbols = new HashSet<Guid>();
            HashSet<Guid> words = new HashSet<Guid>();
            HashSet<Guid> sentences = new HashSet<Guid>();
            HashSet<Guid> senses = new HashSet<Guid>();
            HashSet<Guid> usages = new HashSet<Guid>();
            HashSet<Guid> groups = new HashSet<Guid>();
            HashSet<Guid> sources = new HashSet<Guid>();
            HashSet<Guid> functions = new HashSet<Guid>();
            HashSet<Guid> nodes = new HashSet<Guid>();
            Dictionary<Guid, NodeTargetKind> nodeKinds = new Dictionary<Guid, NodeTargetKind>();
            Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> canonicalNodeIds =
                new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
            HashSet<NodeIdentityKey> nodeIdentities = new HashSet<NodeIdentityKey>();
            HashSet<(NodeTargetKind Kind, Guid TargetId)> canonicalNodeTargets = new HashSet<(NodeTargetKind Kind, Guid TargetId)>();
            HashSet<SymbolIdentityKey> symbolIdentities = new HashSet<SymbolIdentityKey>();
            HashSet<WordIdentityKey> wordIdentities = new HashSet<WordIdentityKey>();
            HashSet<SentenceIdentityKey> sentenceIdentities = new HashSet<SentenceIdentityKey>();
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

            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox box = FoundationBinarySerializer.DeserializeSymbol(record.Payload, store.IsLegacyFormat);
                if (record.Key != box.GetSymbolID() || !symbols.Add(record.Key) || !box.HasCompleteIdentity || !symbolIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Symbol partition contains a duplicate or invalid identity.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Senses))
            {
                SenseBox box = FoundationBinarySerializer.DeserializeSense(record.Payload);
                if (record.Key != box.GetSenseID() || !senses.Add(record.Key)) throw new InvalidDataException("Sense partition contains a duplicate or invalid ID.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Usages))
            {
                UsageBox box = FoundationBinarySerializer.DeserializeUsage(record.Payload);
                if (record.Key != box.GetUsageID() || !usages.Add(record.Key)) throw new InvalidDataException("Usage partition contains a duplicate or invalid ID.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words))
            {
                WordBox box = FoundationBinarySerializer.DeserializeWord(record.Payload);
                if (record.Key != box.GetWordID() || !words.Add(record.Key) || !box.HasCompleteIdentity || !wordIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Word partition contains a duplicate or invalid identity.");
                if (!box.GetSenseID().HasValue || !senses.Contains(box.GetSenseID()!.Value) ||
                    !box.GetUsageID().HasValue || !usages.Contains(box.GetUsageID()!.Value) ||
                    !(box.GetSymbols() ?? new List<Guid>()).All(symbols.Contains))
                    throw new InvalidDataException("Word partition contains unresolved references.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences))
            {
                SentenceBox box = FoundationBinarySerializer.DeserializeSentence(record.Payload);
                if (record.Key != box.GetSentenceID() || !sentences.Add(record.Key) || !box.HasCompleteIdentity || !sentenceIdentities.Add(box.GetIdentityKey()))
                    throw new InvalidDataException("Sentence partition contains a duplicate or invalid identity.");
                if (!box.GetSenseID().HasValue || !senses.Contains(box.GetSenseID()!.Value) ||
                    !(box.GetWords() ?? new List<Guid>()).All(words.Contains))
                    throw new InvalidDataException("Sentence partition contains unresolved references.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Senses))
            {
                SenseBox box = FoundationBinarySerializer.DeserializeSense(record.Payload);
                if (!(box.GetSentences() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Sense partition contains an unresolved sentence membership.");
                if (!(box.GetLeadingWords() ?? new List<Guid>()).All(words.Contains) ||
                    !(box.GetTrailingWords() ?? new List<Guid>()).All(words.Contains))
                    throw new InvalidDataException("Sense partition contains an unresolved leading/trailing word.");
                if (!float.IsFinite(box.GetAverageWordPosition()) || !float.IsFinite(box.GetAverageSentencePosition()) ||
                    !float.IsFinite(box.GetAverageSpread()))
                    throw new InvalidDataException("Sense partition contains a non-finite metric.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Usages))
            {
                UsageBox box = FoundationBinarySerializer.DeserializeUsage(record.Payload);
                if (!(box.GetSentences() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Usage partition contains an unresolved sentence membership.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups))
            {
                GroupBox box = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                if (record.Key != box.GetGroupingID() || !groups.Add(record.Key) || !(box.GetSentenceIDs() ?? new List<Guid>()).All(sentences.Contains))
                    throw new InvalidDataException("Group partition contains a duplicate ID or unresolved sentence.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources))
            {
                SourceBox box = FoundationBinarySerializer.DeserializeSource(record.Payload);
                if (record.Key != box.GetSourceID() || !sources.Add(record.Key) || !(box.GetGroupingIDs() ?? new List<Guid>()).All(groups.Contains))
                    throw new InvalidDataException("Source partition contains a duplicate ID or unresolved group.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Functions))
            {
                FunctionBox box = FoundationBinarySerializer.DeserializeFunction(record.Payload, store.IsLegacyFormat);
                if (record.Key != box.GetFunctionID() || !functions.Add(record.Key))
                    throw new InvalidDataException("Function partition contains a duplicate ID.");
                try
                {
                    MathFunctionDispatcher.ValidateDefinition(box);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
                {
                    throw new InvalidDataException("Function partition contains an incomplete or invalid trusted definition.", exception);
                }
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
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox box = FoundationBinarySerializer.DeserializeSymbol(record.Payload, store.IsLegacyFormat);
                if (box.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Symbol,
                        record.Key, NodeTargetKind.Sense, box.GetSenseID()!.Value, NodeRelationType.HasSense);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words))
            {
                WordBox box = FoundationBinarySerializer.DeserializeWord(record.Payload);
                if (box.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        record.Key, NodeTargetKind.Sense, box.GetSenseID()!.Value, NodeRelationType.HasSense);
                if (box.GetUsageID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        record.Key, NodeTargetKind.Usage, box.GetUsageID()!.Value, NodeRelationType.HasUsage);
                foreach (Guid symbolId in box.GetSymbols() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Word,
                        record.Key, NodeTargetKind.Symbol, symbolId, NodeRelationType.ContainsSymbol);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences))
            {
                SentenceBox box = FoundationBinarySerializer.DeserializeSentence(record.Payload);
                if (box.GetSenseID().HasValue)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        record.Key, NodeTargetKind.Sense, box.GetSenseID()!.Value, NodeRelationType.HasSense);
                List<Guid> wordIds = box.GetWords() ?? new List<Guid>();
                foreach (Guid wordId in wordIds)
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        record.Key, NodeTargetKind.Word, wordId, NodeRelationType.ContainsWord);
                if (wordIds.Count > 0)
                {
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        record.Key, NodeTargetKind.Word, wordIds[0], NodeRelationType.LeadingWord);
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Sentence,
                        record.Key, NodeTargetKind.Word, wordIds[^1], NodeRelationType.TrailingWord);
                }
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups))
            {
                GroupBox box = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                foreach (Guid sentenceId in box.GetSentenceIDs() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Group,
                        record.Key, NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources))
            {
                SourceBox box = FoundationBinarySerializer.DeserializeSource(record.Payload);
                foreach (Guid groupId in box.GetGroupingIDs() ?? new List<Guid>())
                    AddExpectedStructuralLink(expectedStructuralLinks, canonicalNodeIds, NodeTargetKind.Source,
                        record.Key, NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
            }

            List<(Guid Source, Guid Target, NodeRelationType Relation)> actualStructuralLinkList = assertionLinks.Values
                .SelectMany(linkList => linkList)
                .Where(link => IsStructuralRelation(link.RelationType))
                .Select(link => (link.SourceNodeId, link.TargetNodeId, link.RelationType))
                .ToList();
            HashSet<(Guid Source, Guid Target, NodeRelationType Relation)> actualStructuralLinks =
                actualStructuralLinkList.ToHashSet();
            if (actualStructuralLinks.Count != actualStructuralLinkList.Count)
            {
                throw new InvalidDataException("Structural links contain duplicate membership edges.");
            }
            if (!actualStructuralLinks.SetEquals(expectedStructuralLinks))
            {
                throw new InvalidDataException("Structural links do not match trusted Foundation memberships.");
            }

            foreach (List<NodeLink> linksForAssertion in assertionLinks.Values)
            {
                if (linksForAssertion.Count <= 1)
                {
                    continue;
                }
                if (linksForAssertion.Count != 2)
                {
                    throw new InvalidDataException("An assertion ID is used by more than one link pair.");
                }
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
                {
                    throw new InvalidDataException($"Trusted {kind} target '{targetId}' has no canonical null-label node.");
                }
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

                List<NodeLink> originals = TrustedOutgoingLinks.Values.SelectMany(links => links)
                    .Where(link => link.Provenance.AssertionId == assertionId).ToList();
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
                RebuildTrustedSkillsGraph();
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
                List<NodeLink> links = TrustedOutgoingLinks.Values.SelectMany(values => values)
                    .Where(link => link.Provenance.AssertionId == assertionId).ToList();
                if (links.Count == 0 || links.Any(link => IsStructuralRelation(link.RelationType))) return false;
                if (ShouldStoreReverse(links[0].RelationType) && links[0].SourceNodeId != links[0].TargetNodeId && links.Count != 2)
                    return false;
                if (links.Count > 1 && (links.Count != 2 || !ShouldStoreReverse(links[0].RelationType) ||
                    links[0].SourceNodeId != links[1].TargetNodeId || links[0].TargetNodeId != links[1].SourceNodeId))
                {
                    return false;
                }

                int removed = 0;
                for (; removed < links.Count; removed++)
                {
                    if (!RemoveTrustedLinkInternal(links[removed])) break;
                }
                if (removed != links.Count)
                {
                    for (int index = 0; index < removed; index++) AddTrustedLinkOneDirection(links[index]);
                    RebuildTrustedSkillsGraph();
                    return false;
                }
                RebuildTrustedSkillsGraph();
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
                    if (!FoundationBinarySerializer.SerializeFunction(existing).AsSpan().SequenceEqual(
                            FoundationBinarySerializer.SerializeFunction(incoming)))
                        throw new InvalidDataException("Merge contains a conflicting trusted FunctionBox definition.");
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
            foreach (Node trustedNode in TrustedNodeDictionary.Values)
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
                    List<NodeLink> existingAssertions = TrustedOutgoingLinks.Values.SelectMany(links => links)
                        .Where(existing => existing.Provenance.AssertionId == mapped.Provenance.AssertionId).ToList();
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
                TouchPagedObject(key);
                return (T)cached;
            }
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

        private bool HasTrustedSense(Guid? senseId)
        {
            return senseId.HasValue && TrustedSenseDictionary.ContainsKey(senseId.Value);
        }

        private bool HasTrustedTarget(NodeTargetKind kind, Guid targetId)
        {
            return kind switch
            {
                NodeTargetKind.Symbol => TrustedSymbolDictionary.ContainsKey(targetId),
                NodeTargetKind.Word => TrustedWordDictionary.ContainsKey(targetId),
                NodeTargetKind.Sentence => TrustedSentenceDictionary.ContainsKey(targetId),
                NodeTargetKind.Sense => TrustedSenseDictionary.ContainsKey(targetId),
                NodeTargetKind.Usage => TrustedUsageDictionary.ContainsKey(targetId),
                NodeTargetKind.Source => SourceDictionary.ContainsKey(targetId),
                NodeTargetKind.Group => TrustedGroupDictionary.ContainsKey(targetId),
                NodeTargetKind.Function => TrustedFunctionDictionary.ContainsKey(targetId),
                _ => false
            };
        }


        private bool CanTrustWord(WordBox wordBox)
        {
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
            if (!UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid nodeId))
            {
                return;
            }

            if (UntrustedNodeDictionary.Remove(nodeId, out Node? node))
            {
                UntrustedNodeByIdentityIndex.Remove(node.GetIdentityKey());
                node.SetIdentityChangingHandler(null);
                node.SetIdentityChangedHandler(null);
                node.SetReferenceCountsChangedHandler(null);
                RemoveNodeRepresentation(nodeId);
                RecalculateAllNodePresentationsInternal();
            }
            UntrustedNodeByTargetIndex.Remove(targetKey);
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
            return TrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
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
                   (!provenance.EvidenceTargetId.HasValue || HasAnyTrustedTarget(provenance.EvidenceTargetId.Value));
        }

        private bool HasAnyTrustedTarget(Guid targetId)
        {
            return TrustedSymbolDictionary.ContainsKey(targetId) || TrustedWordDictionary.ContainsKey(targetId) ||
                   TrustedSentenceDictionary.ContainsKey(targetId) || TrustedSenseDictionary.ContainsKey(targetId) ||
                   TrustedUsageDictionary.ContainsKey(targetId) || TrustedGroupDictionary.ContainsKey(targetId) ||
                   SourceDictionary.ContainsKey(targetId) || TrustedFunctionDictionary.ContainsKey(targetId);
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
                    RebuildTrustedSkillsGraph();
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

            bool addedKey = false;
            bool addedOutgoing = false;
            bool addedIncoming = false;
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
                return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                if (addedOutgoing && TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? outgoing))
                    outgoing.Remove(link);
                if (addedIncoming && TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? incoming))
                    incoming.Remove(link);
                if (addedKey) TrustedLinkKeys.Remove(link);
                if (TrustedOutgoingLinks.TryGetValue(link.SourceNodeId, out List<NodeLink>? remainingOutgoing) && remainingOutgoing.Count == 0)
                    TrustedOutgoingLinks.Remove(link.SourceNodeId);
                if (TrustedIncomingLinks.TryGetValue(link.TargetNodeId, out List<NodeLink>? remainingIncoming) && remainingIncoming.Count == 0)
                    TrustedIncomingLinks.Remove(link.TargetNodeId);
                RebuildTrustedSkillsGraph();
                return false;
            }
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
            outgoing[outgoingIndex] = replacement;
            incoming[incomingIndex] = replacement;
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
            TrustedLinkKeys.Remove(identity);
            outgoing.RemoveAt(linkIndex);
            incoming.Remove(removed);
            if (outgoing.Count == 0) TrustedOutgoingLinks.Remove(identity.SourceNodeId);
            if (incoming.Count == 0) TrustedIncomingLinks.Remove(identity.TargetNodeId);
            return true;
        }

        private static bool ShouldStoreReverse(NodeRelationType relationType) =>
            relationType is NodeRelationType.Synonym or NodeRelationType.Antonym;

        private void RebuildTrustedSkillsGraph()
        {
            Graph<Guid> graph = new Graph<Guid>(true);
            foreach (Guid nodeId in TrustedSkillsGraph.Nodes)
            {
                graph.AddNode(nodeId);
            }
            foreach (List<NodeLink> links in TrustedOutgoingLinks.Values)
            {
                foreach (NodeLink link in links)
                {
                    graph.AddEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                }
            }
            TrustedSkillsGraph = graph;
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
            double? minimumConfidence)
        {
            bool Matches(NodeLink link) =>
                (!relationFilter.HasValue || link.RelationType == relationFilter.Value) &&
                (!minimumConfidence.HasValue || link.Weight >= minimumConfidence.Value);
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
            if (!relationFilter.HasValue && !originFilter.HasValue &&
                normalizedAuthor == null && !minimumConfidence.HasValue && !activeAt.HasValue)
            {
                foreach (Guid nodeId in TrustedSkillsGraph.Nodes)
                {
                    graph.AddNode(nodeId);
                }
                foreach (GraphEdge<Guid> edge in TrustedSkillsGraph.Edges)
                {
                    graph.AddEdge(edge.From, edge.To, edge.Weight);
                }
                return graph;
            }

            foreach (Guid nodeId in TrustedSkillsGraph.Nodes)
            {
                graph.AddNode(nodeId);
            }
            foreach (List<NodeLink> links in TrustedOutgoingLinks.Values)
            {
                foreach (NodeLink link in links)
                {
                    if (MatchesLinkQuery(link, relationFilter, originFilter, normalizedAuthor, minimumConfidence, activeAt))
                    {
                        graph.AddEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
                    }
                }
            }
            return graph;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolChar"></param>

        /// <param name="senseId"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbols"></param>
        /// <param name="senseId"></param>
        /// <param name="usageId"></param>
        /// <param name="partOfSpeech"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="words"></param>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public SentenceBox GetNewSentenceBox(List<Guid> words, Guid senseId)
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
                _initialized && (TrustedNodeDictionary.ContainsKey(nodeId) || IsPagedRecordAvailable(DiskPartition.Nodes, nodeId)));
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
