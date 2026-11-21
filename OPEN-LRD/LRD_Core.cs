using System.Collections.ObjectModel;
using System.IO;
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
        private readonly Dictionary<Guid, GroupBox> TrustedGroupDictionary = new Dictionary<Guid, GroupBox>();
        private readonly Dictionary<Guid, FunctionBox> TrustedFunctionDictionary = new Dictionary<Guid, FunctionBox>();
        private readonly Dictionary<WordIdentityKey, Guid> TrustedWordIdentityIndex = new Dictionary<WordIdentityKey, Guid>();
        private readonly Dictionary<SentenceIdentityKey, Guid> TrustedSentenceIdentityIndex = new Dictionary<SentenceIdentityKey, Guid>();
        private readonly Dictionary<SymbolIdentityKey, Guid> TrustedSymbolIdentityIndex = new Dictionary<SymbolIdentityKey, Guid>();
        private readonly Dictionary<char, List<Guid>> TrustedSymbolCharIndex = new Dictionary<char, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> SymbolsBySenseIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> WordsBySenseIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> SentencesBySenseIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> WordsByUsageIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> WordsBySymbolIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> SentencesByWordIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> GroupsBySentenceIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<Guid>> SourcesByGroupIndex = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<List<Guid>, HashSet<Guid>> IndexMembershipSets = new Dictionary<List<Guid>, HashSet<Guid>>();
        private readonly Dictionary<Guid, Node> TrustedNodeDictionary = new Dictionary<Guid, Node>();
        private readonly Dictionary<Guid, Node> UntrustedNodeDictionary = new Dictionary<Guid, Node>();
        private readonly Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> TrustedNodeByTargetIndex = new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
        private readonly Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid> UntrustedNodeByTargetIndex = new Dictionary<(NodeTargetKind Kind, Guid TargetId), Guid>();
        private readonly Dictionary<Guid, List<NodeLink>> TrustedOutgoingLinks = new Dictionary<Guid, List<NodeLink>>();
        private readonly Dictionary<Guid, List<NodeLink>> TrustedIncomingLinks = new Dictionary<Guid, List<NodeLink>>();
        private readonly HashSet<(Guid SourceNodeId, Guid TargetNodeId, NodeRelationType RelationType, string? Label)> TrustedLinkKeys = new HashSet<(Guid SourceNodeId, Guid TargetNodeId, NodeRelationType RelationType, string? Label)>();
        private Graph<Guid> TrustedSkillsGraph = new Graph<Guid>(true);
        private PartitionedBinaryStore? _diskStore;
        private string? _diskStorePath;
        private bool _diskPagedReadOnly;
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
            GraphSynchronization.Execute(() => _initialized = true);
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
                TrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbol)
                    ? symbol
                    : GetPagedObject(DiskPartition.Symbols, symbolId, FoundationBinarySerializer.DeserializeSymbol, box => { }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usageId"></param>
        /// <returns></returns>
        public UsageBox? GetTrustedUsageBox(Guid usageId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedUsageDictionary.TryGetValue(usageId, out UsageBox? usage)
                    ? usage
                    : GetPagedObject(DiskPartition.Usages, usageId, FoundationBinarySerializer.DeserializeUsage, box => box.MarkReadOnly()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public SenseBox? GetTrustedSenseBox(Guid senseId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedSenseDictionary.TryGetValue(senseId, out SenseBox? sense)
                    ? sense
                    : GetPagedObject(DiskPartition.Senses, senseId, FoundationBinarySerializer.DeserializeSense, box => box.MarkReadOnly()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
        public WordBox? GetTrustedWordBox(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox)
                    ? wordBox
                    : GetPagedObject(DiskPartition.Words, wordId, FoundationBinarySerializer.DeserializeWord, box => { }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public SentenceBox? GetTrustedSentenceBox(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox)
                    ? sentenceBox
                    : GetPagedObject(DiskPartition.Sentences, sentenceId, FoundationBinarySerializer.DeserializeSentence, box => { }));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbolId"></param>
        /// <returns></returns>
        public SymbolBox? GetUntrustedSymbolBox(Guid symbolId)
        {
            return GraphSynchronization.Execute(() =>
                UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox) ? symbolBox : null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wordId"></param>
        /// <returns></returns>
        public WordBox? GetUntrustedWordBox(Guid wordId)
        {
            return GraphSynchronization.Execute(() =>
                UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox) ? wordBox : null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public SentenceBox? GetUntrustedSentenceBox(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() =>
                UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox) ? sentenceBox : null);
        }

        public SourceBox? GetTrustedSourceBox(Guid sourceId)
        {
            return GraphSynchronization.Execute(() =>
                SourceDictionary.TryGetValue(sourceId, out SourceBox? sourceBox)
                    ? sourceBox
                    : GetPagedObject(DiskPartition.Sources, sourceId, FoundationBinarySerializer.DeserializeSource, box => box.MarkReadOnly()));
        }

        public GroupBox? GetTrustedGroupBox(Guid groupId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedGroupDictionary.TryGetValue(groupId, out GroupBox? groupBox)
                    ? groupBox
                    : GetPagedObject(DiskPartition.Groups, groupId, FoundationBinarySerializer.DeserializeGroup, box => box.MarkReadOnly()));
        }

        public FunctionBox? GetTrustedFunctionBox(Guid functionId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedFunctionDictionary.TryGetValue(functionId, out FunctionBox? functionBox)
                    ? functionBox
                    : GetPagedObject(DiskPartition.Functions, functionId, FoundationBinarySerializer.DeserializeFunction, box => box.MarkReadOnly()));
        }

        public ConversationText? GetTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
                TextConversationDictionary.TryGetValue(conversationId, out ConversationText? conversation) ? conversation : null);
        }

        public ConversationSymbolic? GetSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
                SymbolicConversationDictionary.TryGetValue(conversationId, out ConversationSymbolic? conversation) ? conversation : null);
        }

        public Node? GetTrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
                TrustedNodeDictionary.TryGetValue(nodeId, out Node? node)
                    ? node
                    : GetPagedObject(DiskPartition.Nodes, nodeId, payload => FoundationBinarySerializer.DeserializeNode(payload, true), box => { }));
        }

        public Node? GetUntrustedNode(Guid nodeId)
        {
            return GraphSynchronization.Execute(() =>
                UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null);
        }

        public Node? GetTrustedNodeByTarget(NodeTargetKind kind, Guid targetId)
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!TrustedNodeByTargetIndex.TryGetValue((kind, targetId), out Guid nodeId))
                {
                    return null;
                }
                return GetTrustedNode(nodeId);
            });
        }

        public Node? GetUntrustedNodeByTarget(NodeTargetKind kind, Guid targetId)
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!UntrustedNodeByTargetIndex.TryGetValue((kind, targetId), out Guid nodeId))
                {
                    return null;
                }
                return UntrustedNodeDictionary.TryGetValue(nodeId, out Node? node) ? node : null;
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
                return TrustedSymbolCharIndex.TryGetValue(symbolChar, out List<Guid>? symbolIds) && symbolIds.Count == 1
                    ? symbolIds[0]
                    : null;
            });
        }

        public List<Guid> GetTrustedSymbolIDs(char symbolChar)
        {
            return GraphSynchronization.Execute(() =>
                TrustedSymbolCharIndex.TryGetValue(symbolChar, out List<Guid>? symbolIds)
                    ? new List<Guid>(symbolIds)
                    : new List<Guid>());
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
            return GraphSynchronization.Execute(() => CopyIndexValues(SymbolsBySenseIndex, senseId));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="senseId"></param>
        /// <returns></returns>
        public List<Guid> GetTrustedWordsBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(WordsBySenseIndex, senseId));
        }

        public List<Guid> GetTrustedSentencesBySenseID(Guid senseId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(SentencesBySenseIndex, senseId));
        }

        public List<Guid> GetTrustedWordsByUsageID(Guid usageId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(WordsByUsageIndex, usageId));
        }

        public List<Guid> GetTrustedWordsContainingSymbol(Guid symbolId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(WordsBySymbolIndex, symbolId));
        }

        public List<Guid> GetTrustedSentencesContainingWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(SentencesByWordIndex, wordId));
        }

        public List<Guid> GetTrustedGroupsContainingSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(GroupsBySentenceIndex, sentenceId));
        }

        public List<Guid> GetTrustedSourcesContainingGroup(Guid groupId)
        {
            return GraphSynchronization.Execute(() => CopyIndexValues(SourcesByGroupIndex, groupId));
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
                if (sentenceBox == null)
                {
                    return string.Empty;
                }

                List<string> wordTexts = new List<string>();
                foreach (Guid wordId in sentenceBox.GetWords() ?? new List<Guid>())
                {
                    WordBox? wordBox = GetTrustedWordBox(wordId);
                    if (wordBox != null)
                    {
                        wordTexts.Add(GetTextFromWordBox(wordBox));
                    }
                }
                return string.Join(" ", wordTexts);
            });
        }

        private string GetTextFromWordBox(WordBox wordBox)
        {
            var symbols = wordBox.GetSymbols();
            if (symbols == null)
            {
                return string.Empty;
            }

            var text = new System.Text.StringBuilder(symbols.Count);
            foreach (Guid symbolId in symbols)
            {
                SymbolBox? symbolBox = GetTrustedSymbolBox(symbolId);
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
                IndexMembershipSets.Clear();
                TrustedNodeDictionary.Clear();
                UntrustedNodeDictionary.Clear();
                TrustedNodeByTargetIndex.Clear();
                UntrustedNodeByTargetIndex.Clear();
                TrustedOutgoingLinks.Clear();
                TrustedIncomingLinks.Clear();
                TrustedLinkKeys.Clear();
                TrustedSkillsGraph = new Graph<Guid>(true);
                UntrustedSymbolDictionary.Clear();
                UntrustedWordDictionary.Clear();
                UntrustedSentenceDictionary.Clear();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearUntrustedDictionaries()
        {
            GraphSynchronization.Execute(() =>
            {
                ClearUntrustedSymbols();
                ClearUntrustedWords();
                ClearUntrustedSentences();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        public void ClearUntrustedSymbols()
        {
            GraphSynchronization.Execute(() =>
            {
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
                if (UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                {
                    symbolBox.SetSenseID(senseId);
                    return true;
                }
                return false;
            });
        }

        public bool SetUntrustedSymbolChar(Guid symbolId, char symbolChar)
        {
            return GraphSynchronization.Execute(() =>
            {
                if (UntrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                {
                    symbolBox.SetSymbolChar(symbolChar);
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
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetSenseID(senseId);
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
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    sentenceBox.SetSenseID(senseId);
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
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetSymbols(symbols);
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
                if (UntrustedSentenceDictionary.TryGetValue(sentenceId, out SentenceBox? sentenceBox))
                {
                    sentenceBox.SetWords(words);
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
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetUsageID(usageId);
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
                if (UntrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    wordBox.SetPartOfSpeech(pos);
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

                TrustedFunctionDictionary.Add(functionId, functionBox);
                EnsureTrustedNode(NodeTargetKind.Function, functionId);
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
                if (!TrustedGroupDictionary.TryGetValue(groupId, out GroupBox? groupBox) ||
                    !TrustedSentenceDictionary.ContainsKey(sentenceId))
                {
                    return false;
                }

                if (!groupBox.AddSentenceFromCore(sentenceId))
                {
                    return false;
                }
                AddIndex(GroupsBySentenceIndex, sentenceId, groupId);
                LinkNodeToTarget(GetNodeIdForTarget(NodeTargetKind.Group, groupId),
                    NodeTargetKind.Sentence, sentenceId, NodeRelationType.ContainsSentence);
                return true;
            });
        }

        public bool AddGroupToTrustedSource(Guid sourceId, Guid groupId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                if (!SourceDictionary.TryGetValue(sourceId, out SourceBox? sourceBox) ||
                    !TrustedGroupDictionary.ContainsKey(groupId))
                {
                    return false;
                }

                if (!sourceBox.AddGroupingFromCore(groupId))
                {
                    return false;
                }
                AddIndex(SourcesByGroupIndex, groupId, sourceId);
                LinkNodeToTarget(GetNodeIdForTarget(NodeTargetKind.Source, sourceId),
                    NodeTargetKind.Group, groupId, NodeRelationType.ContainsGroup);
                return true;
            });
        }

        public bool AddTextConversation(ConversationText conversation)
        {
            return GraphSynchronization.Execute(() =>
            {
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
            return GraphSynchronization.Execute(() => TextConversationDictionary.Remove(conversationId));
        }

        public bool AddSymbolicConversation(ConversationSymbolic conversation)
        {
            return GraphSynchronization.Execute(() =>
            {
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
            return GraphSynchronization.Execute(() => SymbolicConversationDictionary.Remove(conversationId));
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
                EnsureUntrustedNode(NodeTargetKind.Symbol, symbolId);
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
                EnsureUntrustedNode(NodeTargetKind.Word, wordId);
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
                EnsureUntrustedNode(NodeTargetKind.Sentence, sentenceId);
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


        public bool SetUntrustedNode(Node node)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                ArgumentNullException.ThrowIfNull(node);
                Guid nodeId = node.GetNodeID();
                (NodeTargetKind Kind, Guid TargetId) targetKey = (node.GetTargetKind(), node.GetTargetID());
                if (node.IsTrusted || TrustedNodeDictionary.ContainsKey(nodeId) ||
                    TrustedNodeByTargetIndex.ContainsKey(targetKey))
                {
                    return false;
                }

                if (UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid existingNodeId))
                {
                    if (existingNodeId == nodeId && UntrustedNodeDictionary.ContainsKey(nodeId))
                    {
                        UntrustedNodeDictionary[nodeId] = node;
                        return true;
                    }
                    return false;
                }

                if (UntrustedNodeDictionary.ContainsKey(nodeId))
                {
                    return false;
                }

                UntrustedNodeDictionary.Add(nodeId, node);
                UntrustedNodeByTargetIndex.Add(targetKey, nodeId);
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
                UntrustedNodeByTargetIndex.Remove((node.GetTargetKind(), node.GetTargetID()));
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

                (NodeTargetKind Kind, Guid TargetId) targetKey = (node.GetTargetKind(), node.GetTargetID());
                if (TrustedNodeByTargetIndex.ContainsKey(targetKey))
                {
                    return false;
                }

                UntrustedNodeDictionary.Remove(nodeId);
                UntrustedNodeByTargetIndex.Remove(targetKey);
                node.MarkTrusted();
                TrustedNodeDictionary.Add(nodeId, node);
                TrustedNodeByTargetIndex.Add(targetKey, nodeId);
                TrustedSkillsGraph.AddNode(nodeId);
                WireNodeRelations(node);
                return true;
            });
        }

        public bool AddTrustedLink(NodeLink link)
        {
            return GraphSynchronization.Execute(() => { EnsureMutableStorageMode(); return AddTrustedLinkInternal(link); });
        }

        public bool AddTrustedLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            double weight = 1.0,
            string? label = null)
        {
            return AddTrustedLink(new NodeLink(sourceNodeId, targetNodeId, relationType, weight, label));
        }

        public List<NodeLink> GetOutgoingLinks(Guid nodeId, NodeRelationType? relationFilter = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!TrustedOutgoingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                if (!relationFilter.HasValue)
                {
                    return new List<NodeLink>(links);
                }
                return links.Where(link => link.RelationType == relationFilter.Value).ToList();
            });
        }

        public List<NodeLink> GetIncomingLinks(Guid nodeId, NodeRelationType? relationFilter = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!TrustedIncomingLinks.TryGetValue(nodeId, out List<NodeLink>? links))
                {
                    return new List<NodeLink>();
                }
                if (!relationFilter.HasValue)
                {
                    return new List<NodeLink>(links);
                }
                return links.Where(link => link.RelationType == relationFilter.Value).ToList();
            });
        }

        public Graph<Guid> BuildSkillsGraphSnapshot(NodeRelationType? relationFilter = null)
        {
            return GraphSynchronization.Execute(() => BuildSkillsGraphSnapshotInternal(relationFilter));
        }

        public void StreamTrustedDictionariesToDisk(string directoryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            GraphSynchronization.Execute(() =>
            {
                if (_diskPagedReadOnly) MaterializeDiskStore();
                Dictionary<DiskPartition, IEnumerable<DiskRecord>> partitions = BuildDiskRecords();
                using PartitionedBinaryStore store = PartitionedBinaryStore.Create(directoryPath, partitions);
                store.Validate();
            });
        }

        public void StreamTrustedDictionariesFromDisk(string directoryPath, bool replaceExisting = true,
            bool preloadAllIntoMemory = false, int maxCachedRecordsPerPartition = 128)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            GraphSynchronization.Execute(() =>
            {
                using PartitionedBinaryStore incoming = PartitionedBinaryStore.Open(directoryPath, maxCachedRecordsPerPartition);
                ValidateDiskStoreContents(incoming);
                if (!replaceExisting)
                {
                    if (_diskPagedReadOnly) MaterializeDiskStore();
                    MergeDiskStore(incoming);
                    return;
                }

                ClearAllDictionaries();
                _diskStore = PartitionedBinaryStore.Open(directoryPath, maxCachedRecordsPerPartition);
                _diskStorePath = Path.GetFullPath(directoryPath);
                _maxPagedObjects = Math.Max(0, maxCachedRecordsPerPartition) * Enum.GetValues<DiskPartition>().Length;
                BuildPagedIndexesAndGraph(_diskStore);
                _diskPagedReadOnly = true;
                if (preloadAllIntoMemory) MaterializeDiskStore();
            });
        }

        public void OpenDiskStore(string directoryPath, int maxCachedRecordsPerPartition = 128)
        {
            StreamTrustedDictionariesFromDisk(directoryPath, true, false, maxCachedRecordsPerPartition);
        }

        public void MaterializeDiskStore()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!_diskPagedReadOnly || _diskStore == null) return;
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Symbols))
                    TrustedSymbolDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeSymbol(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Usages))
                    TrustedUsageDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeUsage(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Senses))
                    TrustedSenseDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeSense(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Words))
                    TrustedWordDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeWord(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Sentences))
                    TrustedSentenceDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeSentence(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Groups))
                {
                    GroupBox box = FoundationBinarySerializer.DeserializeGroup(record.Payload);
                    box.FreezeIdentity();
                    TrustedGroupDictionary.Add(record.Key, box);
                }
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Sources))
                {
                    SourceBox box = FoundationBinarySerializer.DeserializeSource(record.Payload);
                    box.FreezeIdentity();
                    SourceDictionary.Add(record.Key, box);
                }
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Functions))
                    TrustedFunctionDictionary.Add(record.Key, FoundationBinarySerializer.DeserializeFunction(record.Payload));
                foreach (DiskRecord record in _diskStore.ReadAll(DiskPartition.Nodes))
                    TrustedNodeDictionary[record.Key] = FoundationBinarySerializer.DeserializeNode(record.Payload, false);

                _diskPagedReadOnly = false;
                _diskStore.Dispose();
                _diskStore = null;
                ClearPagedObjectCache();
            });
        }

        public void CloseDiskStore(bool materialize = true)
        {
            GraphSynchronization.Execute(() =>
            {
                if (_diskStore == null)
                {
                    if (materialize) _diskStorePath = null;
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

        private Dictionary<DiskPartition, IEnumerable<DiskRecord>> BuildDiskRecords()
        {
            if (_diskPagedReadOnly && _diskStore != null)
            {
                return Enum.GetValues<DiskPartition>().ToDictionary(partition => partition,
                    partition => _diskStore.ReadAll(partition));
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
                [DiskPartition.Links] = TrustedOutgoingLinks.Values.SelectMany(links => links)
                    .Select(link => new DiskRecord(Guid.NewGuid(), FoundationBinarySerializer.SerializeLink(link)))
            };
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
            HashSet<(NodeTargetKind Kind, Guid TargetId)> nodeTargets = new HashSet<(NodeTargetKind Kind, Guid TargetId)>();
            HashSet<SymbolIdentityKey> symbolIdentities = new HashSet<SymbolIdentityKey>();
            HashSet<WordIdentityKey> wordIdentities = new HashSet<WordIdentityKey>();
            HashSet<SentenceIdentityKey> sentenceIdentities = new HashSet<SentenceIdentityKey>();
            HashSet<(Guid Source, Guid Target, NodeRelationType Relation, string? Label)> linkKeys =
                new HashSet<(Guid Source, Guid Target, NodeRelationType Relation, string? Label)>();

            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox box = FoundationBinarySerializer.DeserializeSymbol(record.Payload);
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
                FunctionBox box = FoundationBinarySerializer.DeserializeFunction(record.Payload);
                if (record.Key != box.GetFunctionID() || !functions.Add(record.Key)) throw new InvalidDataException("Function partition contains a duplicate ID.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
            {
                Node node = FoundationBinarySerializer.DeserializeNode(record.Payload, false);
                if (record.Key != node.GetNodeID() || !nodes.Add(record.Key) || !node.IsTrusted || !nodeTargets.Add((node.GetTargetKind(), node.GetTargetID())))
                    throw new InvalidDataException("Node partition contains a duplicate, untrusted, or invalid node.");
                if (!DiskTargetExists(node.GetTargetKind(), node.GetTargetID(), symbols, words, sentences, senses, usages, groups, sources, functions))
                    throw new InvalidDataException("Node partition contains a node with an unresolved target.");
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload);
                if (!nodes.Contains(link.SourceNodeId) || !nodes.Contains(link.TargetNodeId))
                    throw new InvalidDataException("Link partition contains an unresolved endpoint.");
                if (!linkKeys.Add((link.SourceNodeId, link.TargetNodeId, link.RelationType, link.Label)))
                    throw new InvalidDataException("Link partition contains a duplicate edge identity.");
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

        /// <summary>Updates one trusted link and rebuilds the derived Skills graph snapshot.</summary>
        public bool UpdateTrustedLinkWeight(Guid sourceNodeId, Guid targetNodeId,
            NodeRelationType relationType, double newWeight, string? label = null)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureMutableStorageMode();
                NodeLink replacement = new NodeLink(sourceNodeId, targetNodeId, relationType, newWeight, label);
                if (!ReplaceTrustedLink(replacement))
                {
                    return false;
                }
                if (ShouldStoreReverse(relationType) && sourceNodeId != targetNodeId)
                {
                    NodeLink reverse = new NodeLink(targetNodeId, sourceNodeId, relationType, newWeight, replacement.Label);
                    if (!ReplaceTrustedLink(reverse)) AddTrustedLinkInternal(reverse, false);
                }
                RebuildTrustedSkillsGraph();
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
                NodeLink identity = new NodeLink(sourceNodeId, targetNodeId, relationType, 1.0, label);
                if (!RemoveTrustedLinkInternal(identity)) return false;
                if (ShouldStoreReverse(relationType) && sourceNodeId != targetNodeId)
                    RemoveTrustedLinkInternal(new NodeLink(targetNodeId, sourceNodeId, relationType, 1.0, identity.Label));
                RebuildTrustedSkillsGraph();
                return true;
            });
        }

        private void BuildPagedIndexesAndGraph(PartitionedBinaryStore store)
        {
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols))
            {
                SymbolBox box = FoundationBinarySerializer.DeserializeSymbol(record.Payload);
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
                TrustedNodeByTargetIndex.Add((node.GetTargetKind(), node.GetTargetID()), record.Key);
                TrustedSkillsGraph.AddNode(record.Key);
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload);
                AddTrustedLinkInternal(link);
            }
        }

        private void MergeDiskStore(PartitionedBinaryStore store)
        {
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Senses)) AddTrustedSense(FoundationBinarySerializer.DeserializeSense(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Usages)) AddTrustedUsage(FoundationBinarySerializer.DeserializeUsage(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Symbols)) AddTrustedSymbol(FoundationBinarySerializer.DeserializeSymbol(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Words)) AddTrustedWord(FoundationBinarySerializer.DeserializeWord(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sentences)) AddTrustedSentence(FoundationBinarySerializer.DeserializeSentence(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Groups)) AddTrustedGroup(FoundationBinarySerializer.DeserializeGroup(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Sources)) AddTrustedSource(FoundationBinarySerializer.DeserializeSource(record.Payload));
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Functions)) AddTrustedFunction(FoundationBinarySerializer.DeserializeFunction(record.Payload));

            Dictionary<Guid, Guid> nodeIdMap = new Dictionary<Guid, Guid>();
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Nodes))
            {
                Node incoming = FoundationBinarySerializer.DeserializeNode(record.Payload, false);
                Node? existing = GetTrustedNodeByTarget(incoming.GetTargetKind(), incoming.GetTargetID());
                if (existing != null)
                {
                    nodeIdMap[record.Key] = existing.GetNodeID();
                    continue;
                }
                if (!HasTrustedTarget(incoming.GetTargetKind(), incoming.GetTargetID()) || TrustedNodeDictionary.ContainsKey(record.Key)) continue;
                incoming.MarkTrusted();
                TrustedNodeDictionary.Add(record.Key, incoming);
                TrustedNodeByTargetIndex.Add((incoming.GetTargetKind(), incoming.GetTargetID()), record.Key);
                TrustedSkillsGraph.AddNode(record.Key);
                WireNodeRelations(incoming);
                nodeIdMap[record.Key] = record.Key;
            }
            foreach (DiskRecord record in store.ReadAll(DiskPartition.Links))
            {
                NodeLink link = FoundationBinarySerializer.DeserializeLink(record.Payload);
                if (nodeIdMap.TryGetValue(link.SourceNodeId, out Guid sourceId) && nodeIdMap.TryGetValue(link.TargetNodeId, out Guid targetId))
                    AddTrustedLinkInternal(new NodeLink(sourceId, targetId, link.RelationType, link.Weight, link.Label));
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
            if (_diskPagedReadOnly)
                throw new InvalidOperationException("The core is in read-only disk-paged mode. Call MaterializeDiskStore() before mutation.");
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

        private void AddIndex<TKey>(Dictionary<TKey, List<Guid>> index, TKey key, Guid value)
            where TKey : notnull
        {
            if (!index.TryGetValue(key, out List<Guid>? values))
            {
                values = new List<Guid>();
                index.Add(key, values);
                IndexMembershipSets.Add(values, new HashSet<Guid>());
            }
            if (!IndexMembershipSets.TryGetValue(values, out HashSet<Guid>? membership))
            {
                membership = new HashSet<Guid>(values);
                IndexMembershipSets.Add(values, membership);
            }
            if (membership.Add(value))
            {
                values.Add(value);
            }
        }

        private static List<Guid> CopyIndexValues<TKey>(Dictionary<TKey, List<Guid>> index, TKey key)
            where TKey : notnull
        {
            return index.TryGetValue(key, out List<Guid>? values) ? new List<Guid>(values) : new List<Guid>();
        }

        private void RemoveUntrustedNodeForTarget(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            if (!UntrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid nodeId))
            {
                return;
            }

            UntrustedNodeDictionary.Remove(nodeId);
            UntrustedNodeByTargetIndex.Remove(targetKey);
        }

        private void EnsureUntrustedNode(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            if (TrustedNodeByTargetIndex.ContainsKey(targetKey) ||
                UntrustedNodeByTargetIndex.ContainsKey(targetKey))
            {
                return;
            }

            Node node = new Node(kind, targetId);
            UntrustedNodeDictionary.Add(node.GetNodeID(), node);
            UntrustedNodeByTargetIndex.Add(targetKey, node.GetNodeID());
        }

        private Node EnsureTrustedNode(NodeTargetKind kind, Guid targetId)
        {
            (NodeTargetKind Kind, Guid TargetId) targetKey = (kind, targetId);
            if (TrustedNodeByTargetIndex.TryGetValue(targetKey, out Guid trustedNodeId) &&
                TrustedNodeDictionary.TryGetValue(trustedNodeId, out Node? existingNode))
            {
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
            TrustedSkillsGraph.AddNode(node.GetNodeID());
            WireNodeRelations(node);
            return node;
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
            AddTrustedLinkInternal(new NodeLink(sourceNodeId, targetNodeId, relationType));
        }

        private void WireNodeRelations(Node node)
        {
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
                            senseBox.AddSentence(targetId);
                            if (words.Count > 0)
                            {
                                senseBox.AddLeadingWord(words[0]);
                                senseBox.AddTrailingWord(words[words.Count - 1]);
                            }
                        }
                        foreach (Guid wordId in words)
                        {
                            if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? containedWord) &&
                                containedWord.GetUsageID().HasValue &&
                                TrustedUsageDictionary.TryGetValue(containedWord.GetUsageID()!.Value, out UsageBox? usageBox))
                            {
                                usageBox.AddSentence(targetId);
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
        }

        private Guid GetNodeIdForTarget(NodeTargetKind kind, Guid targetId)
        {
            Node? node = GetTrustedNodeForTarget(kind, targetId);
            return node == null ? Guid.Empty : node.GetNodeID();
        }

        private bool AddTrustedLinkInternal(NodeLink link, bool addSymmetricReverse = true)
        {
            if (!TrustedSkillsGraph.ContainsNode(link.SourceNodeId) ||
                !TrustedSkillsGraph.ContainsNode(link.TargetNodeId))
            {
                return false;
            }

            (Guid SourceNodeId, Guid TargetNodeId, NodeRelationType RelationType, string? Label) key =
                (link.SourceNodeId, link.TargetNodeId, link.RelationType, link.Label);
            if (!TrustedLinkKeys.Add(key))
            {
                return false;
            }

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
            outgoing.Add(link);
            incoming.Add(link);
            TrustedSkillsGraph.AddEdge(link.SourceNodeId, link.TargetNodeId, link.Weight);
            if (addSymmetricReverse && link.SourceNodeId != link.TargetNodeId && ShouldStoreReverse(link.RelationType))
            {
                AddTrustedLinkInternal(new NodeLink(link.TargetNodeId, link.SourceNodeId,
                    link.RelationType, link.Weight, link.Label), false);
            }
            return true;
        }

        private bool ReplaceTrustedLink(NodeLink replacement)
        {
            (Guid SourceNodeId, Guid TargetNodeId, NodeRelationType RelationType, string? Label) key =
                (replacement.SourceNodeId, replacement.TargetNodeId, replacement.RelationType, replacement.Label);
            if (!TrustedLinkKeys.Contains(key) ||
                !TrustedOutgoingLinks.TryGetValue(replacement.SourceNodeId, out List<NodeLink>? outgoing) ||
                !TrustedIncomingLinks.TryGetValue(replacement.TargetNodeId, out List<NodeLink>? incoming))
            {
                return false;
            }
            int outgoingIndex = outgoing.FindIndex(link => link.TargetNodeId == replacement.TargetNodeId &&
                link.RelationType == replacement.RelationType && link.Label == replacement.Label);
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
            (Guid SourceNodeId, Guid TargetNodeId, NodeRelationType RelationType, string? Label) key =
                (identity.SourceNodeId, identity.TargetNodeId, identity.RelationType, identity.Label);
            if (!TrustedLinkKeys.Contains(key) ||
                !TrustedOutgoingLinks.TryGetValue(identity.SourceNodeId, out List<NodeLink>? outgoing) ||
                !TrustedIncomingLinks.TryGetValue(identity.TargetNodeId, out List<NodeLink>? incoming))
            {
                return false;
            }
            int linkIndex = outgoing.FindIndex(link => link.TargetNodeId == identity.TargetNodeId &&
                link.RelationType == identity.RelationType && link.Label == identity.Label);
            if (linkIndex < 0) return false;
            NodeLink removed = outgoing[linkIndex];
            if (!incoming.Contains(removed)) return false;
            TrustedLinkKeys.Remove(key);
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

        private Graph<Guid> BuildSkillsGraphSnapshotInternal(NodeRelationType? relationFilter)
        {
            Graph<Guid> graph = new Graph<Guid>(true);
            if (!relationFilter.HasValue)
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
                    if (link.RelationType == relationFilter.Value)
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
                SymbolIdentityKey identityKey = new SymbolIdentityKey(symbolChar, senseId);
                if (TrustedSymbolIdentityIndex.TryGetValue(identityKey, out Guid existingSymbolId) &&
                    GetTrustedSymbolBox(existingSymbolId) is SymbolBox existingSymbolBox)
                {
                    return existingSymbolBox;
                }

                SymbolBox newSymbolBox = new SymbolBox(Guid.NewGuid(), symbolChar);
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
                WordIdentityKey identityKey = new WordIdentityKey(symbols, senseId, usageId, partOfSpeech);
                if (TrustedWordIdentityIndex.TryGetValue(identityKey, out Guid existingWordId) &&
                    GetTrustedWordBox(existingWordId) is WordBox existingWord)
                {
                    return existingWord;
                }

                return new WordBox(senseId, usageId, symbols, partOfSpeech);
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
                SentenceIdentityKey identityKey = new SentenceIdentityKey(words, senseId);
                if (TrustedSentenceIdentityIndex.TryGetValue(identityKey, out Guid existingSentenceId) &&
                    GetTrustedSentenceBox(existingSentenceId) is SentenceBox existingSentence)
                {
                    return existingSentence;
                }

                return new SentenceBox(Guid.NewGuid(), senseId, words);
            });
        }

    }
}
