using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPEN_LRD
{
    internal class GroupBox
    {
        private Guid GroupingID;
        private string? GroupingName;
        private List<Guid> _sentences;
        private HashSet<Guid> _sentenceSet;
        private bool _identityFrozen;

        public GroupBox()
        {
            GroupingID = Guid.NewGuid();
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }
        public GroupBox(string groupingName)
        {
            GroupingID = Guid.NewGuid();
            GroupingName = groupingName;
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        public GroupBox(Guid groupingId)
        {
            ValidateIdentityId(groupingId, nameof(groupingId));
            GroupingID = groupingId;
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        public GroupBox(Dictionary<Guid, SentenceBox> sentences)
            : this(Guid.NewGuid(), null, ExtractSentenceIds(sentences))
        {
        }

        public GroupBox(Guid groupingId, Dictionary<Guid, SentenceBox> sentences)
            : this(groupingId, null, ExtractSentenceIds(sentences))
        {
        }

        public GroupBox(Guid groupingId, string groupingName, Dictionary<Guid, SentenceBox> sentences)
            : this(groupingId, groupingName, ExtractSentenceIds(sentences))
        {
        }

        public GroupBox(IEnumerable<Guid> sentenceIds)
            : this(Guid.NewGuid(), null, sentenceIds)
        {
        }

        public GroupBox(Guid groupingId, IEnumerable<Guid> sentenceIds)
            : this(groupingId, null, sentenceIds)
        {
        }

        public GroupBox(Guid groupingId, string? groupingName, IEnumerable<Guid> sentenceIds)
        {
            ValidateIdentityId(groupingId, nameof(groupingId));
            ArgumentNullException.ThrowIfNull(sentenceIds);
            ValidateIdentityIds(sentenceIds, nameof(sentenceIds));
            GroupingID = groupingId;
            GroupingName = groupingName;
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
                    sentenceIds.Add(sentenceId);
                }
                return sentenceIds;
            });
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
    }
}
