using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPEN_LRD.Foundations
{
    /// <summary>Stores an append-only trusted set of sentence memberships.</summary>
    internal class GroupBox
    {
        private Guid GroupingID;
        private string? GroupingName;
        private List<Guid> _sentences;
        private HashSet<Guid> _sentenceSet;
        private bool _identityFrozen;
        private readonly Dictionary<Guid, ConversationSymbolic> _symbolicConversations = new Dictionary<Guid, ConversationSymbolic>();
        private readonly Dictionary<Guid, ConversationText> _textConversations = new Dictionary<Guid, ConversationText>();

        public GroupBox()
        {
            GroupingID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Group);
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }
        public GroupBox(string groupingName)
        {
            GroupingID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Group);
            GroupingName = NormalizeOptionalText(groupingName);
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        public GroupBox(Guid groupingId)
        {
            ValidateGroupingId(groupingId, nameof(groupingId), allowRegisteredTrustedId: false);
            GroupingID = groupingId;
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        public GroupBox(Dictionary<Guid, SentenceBox> sentences)
            : this(LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Group), null, ExtractSentenceIds(sentences), allowRegisteredTrustedId: true)
        {
        }

        public GroupBox(Guid groupingId, Dictionary<Guid, SentenceBox> sentences)
            : this(groupingId, null, ExtractSentenceIds(sentences), allowRegisteredTrustedId: false)
        {
        }

        public GroupBox(Guid groupingId, string groupingName, Dictionary<Guid, SentenceBox> sentences)
            : this(groupingId, groupingName, ExtractSentenceIds(sentences), allowRegisteredTrustedId: false)
        {
        }

        public GroupBox(IEnumerable<Guid> sentenceIds)
            : this(LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Group), null, sentenceIds, allowRegisteredTrustedId: true)
        {
        }

        public GroupBox(Guid groupingId, IEnumerable<Guid> sentenceIds)
            : this(groupingId, null, sentenceIds, allowRegisteredTrustedId: false)
        {
        }

        public GroupBox(Guid groupingId, string? groupingName, IEnumerable<Guid> sentenceIds)
            : this(groupingId, groupingName, sentenceIds, allowRegisteredTrustedId: false)
        {
        }

        internal GroupBox(Guid groupingId, string? groupingName, IEnumerable<Guid> sentenceIds, bool allowRegisteredTrustedId)
        {
            ValidateGroupingId(groupingId, nameof(groupingId), allowRegisteredTrustedId);
            ArgumentNullException.ThrowIfNull(sentenceIds);
            ValidateIdentityIds(sentenceIds, nameof(sentenceIds));
            GroupingID = groupingId;
            GroupingName = NormalizeOptionalText(groupingName);
            _sentences = GraphSynchronization.Execute(() => new List<Guid>(sentenceIds.Distinct()));
            _sentenceSet = GraphSynchronization.Execute(() => new HashSet<Guid>(_sentences));
        }

        public List<Guid>? GetSentences()
        {
            return GetSentenceIDs();
        }

        public List<Guid>? GetSentenceIDs()
        {
            return GraphSynchronization.Execute(() => new List<Guid>(_sentences));
        }

        public Guid GetGroupingID()
        {
            return GraphSynchronization.Execute(() => GroupingID);
        }

        public int GetSentenceCount()
        {
            return GraphSynchronization.Execute(() => _sentences.Count);
        }

        public bool ContainsSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => _sentenceSet.Contains(sentenceId));
        }

        public bool AddSentence(SentenceBox sentence)
        {
            ArgumentNullException.ThrowIfNull(sentence);
            return AddSentence(sentence.GetSentenceID());
        }

        public bool AddSentence(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_sentenceSet.Add(sentenceId))
                {
                    _sentences.Add(sentenceId);
                    return true;
                }
                return false;
            });
        }

        internal bool AddSentenceFromCore(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() => AddSentenceInternal(sentenceId));
        }

        internal bool IsIdentityFrozen => GraphSynchronization.Execute(() => _identityFrozen);

        internal void FreezeIdentity()
        {
            GraphSynchronization.Execute(() => _identityFrozen = true);
        }

        internal void MarkReadOnly() => FreezeIdentity();

        public string? GetGroupingName()
        {
            return GraphSynchronization.Execute(() => GroupingName);
        }

        /// <summary>Adds a symbolic conversation to this group. Fails if the identifier is already present.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <param name="conversation">Symbolic conversation to store.</param>
        /// <returns><see langword="true"/> if added; otherwise <see langword="false"/>.</returns>
        public bool AddSymbolicConversation(Guid conversationId, ConversationSymbolic conversation)
        {
            ValidateIdentityId(conversationId, nameof(conversationId));
            ArgumentNullException.ThrowIfNull(conversation);
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_symbolicConversations.ContainsKey(conversationId))
                {
                    return false;
                }
                _symbolicConversations.Add(conversationId, conversation);
                return true;
            });
        }

        /// <summary>Adds a text conversation to this group. Fails if the identifier is already present.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <param name="conversation">Text conversation to store.</param>
        /// <returns><see langword="true"/> if added; otherwise <see langword="false"/>.</returns>
        public bool AddTextConversation(Guid conversationId, ConversationText conversation)
        {
            ValidateIdentityId(conversationId, nameof(conversationId));
            ArgumentNullException.ThrowIfNull(conversation);
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_textConversations.ContainsKey(conversationId))
                {
                    return false;
                }
                _textConversations.Add(conversationId, conversation);
                return true;
            });
        }

        /// <summary>Gets a symbolic conversation by identifier.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <returns>The stored conversation, or <see langword="null"/> if absent.</returns>
        public ConversationSymbolic? GetSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
                _symbolicConversations.TryGetValue(conversationId, out ConversationSymbolic? conversation) ? conversation : null);
        }

        /// <summary>Gets a text conversation by identifier.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <returns>The stored conversation, or <see langword="null"/> if absent.</returns>
        public ConversationText? GetTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
                _textConversations.TryGetValue(conversationId, out ConversationText? conversation) ? conversation : null);
        }

        /// <summary>Returns a snapshot of all symbolic conversations keyed by identifier.</summary>
        public Dictionary<Guid, ConversationSymbolic> GetSymbolicConversations()
        {
            return GraphSynchronization.Execute(() => new Dictionary<Guid, ConversationSymbolic>(_symbolicConversations));
        }

        /// <summary>Returns a snapshot of all text conversations keyed by identifier.</summary>
        public Dictionary<Guid, ConversationText> GetTextConversations()
        {
            return GraphSynchronization.Execute(() => new Dictionary<Guid, ConversationText>(_textConversations));
        }

        public bool ContainsSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() => _symbolicConversations.ContainsKey(conversationId));
        }

        public bool ContainsTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() => _textConversations.ContainsKey(conversationId));
        }

        public int GetSymbolicConversationCount()
        {
            return GraphSynchronization.Execute(() => _symbolicConversations.Count);
        }

        public int GetTextConversationCount()
        {
            return GraphSynchronization.Execute(() => _textConversations.Count);
        }

        /// <summary>Removes a symbolic conversation from this group.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveSymbolicConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                return _symbolicConversations.Remove(conversationId);
            });
        }

        /// <summary>Removes a text conversation from this group.</summary>
        /// <param name="conversationId">Conversation identifier.</param>
        /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
        public bool RemoveTextConversation(Guid conversationId)
        {
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                return _textConversations.Remove(conversationId);
            });
        }

        private static List<Guid> ExtractSentenceIds(Dictionary<Guid, SentenceBox> sentences)
        {
            ArgumentNullException.ThrowIfNull(sentences);
            return GraphSynchronization.Execute(() =>
            {
                List<Guid> sentenceIds = new List<Guid>();
                foreach (KeyValuePair<Guid, SentenceBox> entry in sentences)
                {
                    ValidateIdentityId(entry.Key, nameof(sentences));
                    if (entry.Value == null)
                    {
                        throw new ArgumentException("Sentence entries cannot be null.", nameof(sentences));
                    }
                    Guid sentenceId = entry.Value.GetSentenceID();
                    ValidateIdentityId(sentenceId, nameof(sentences));
                    if (entry.Key != sentenceId)
                    {
                        throw new ArgumentException("Sentence keys must match the contained SentenceBox IDs.", nameof(sentences));
                    }
                    sentenceIds.Add(sentenceId);
                }
                return sentenceIds;
            });
        }

        private static void ValidateGroupingId(Guid id, string parameterName, bool allowRegisteredTrustedId)
        {
            if (allowRegisteredTrustedId)
            {
                ValidateIdentityId(id, parameterName);
            }
            else
            {
                LRD_Core.Instance.ValidateNewUntrustedId(NodeTargetKind.Group, id, parameterName);
            }
        }

        private static void ValidateIdentityId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
        }

        private static void ValidateIdentityIds(IEnumerable<Guid> ids, string parameterName)
        {
            foreach (Guid id in ids)
            {
                ValidateIdentityId(id, parameterName);
            }
        }

        private bool AddSentenceInternal(Guid sentenceId)
        {
            if (_sentenceSet.Add(sentenceId))
            {
                _sentences.Add(sentenceId);
                return true;
            }
            return false;
        }

        private void EnsureIdentityMutable()
        {
            if (_identityFrozen)
            {
                throw new InvalidOperationException("Group identity is frozen after trust.");
            }
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
