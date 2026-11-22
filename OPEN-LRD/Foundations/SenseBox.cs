using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    /// <summary>Stores sense metrics and sentence/word memberships; trusted memberships are append-only.</summary>
    internal class SenseBox
    {
        private Guid SenseID;
        private List<Guid> _sentences;
        private List<Guid> _leadingWords;
        private List<Guid> _trailingWords;
        private HashSet<Guid> _sentenceSet;
        private HashSet<Guid> _leadingWordSet;
        private HashSet<Guid> _trailingWordSet;
        private bool _isTrusted;
        private bool _isReadOnly;
        private float AverageWordPosition;
        private float AverageSentencePosition;
        private float AverageSpread;

        public SenseBox()
            : this(LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Sense), allowRegisteredTrustedId: true)
        {
        }

        public SenseBox(Guid senseId)
            : this(senseId, allowRegisteredTrustedId: false)
        {
        }

        internal SenseBox(Guid senseId, bool allowRegisteredTrustedId)
        {
            ValidateSenseId(senseId, nameof(senseId), allowRegisteredTrustedId);
            SenseID = senseId;
            AverageWordPosition = 0.0f;
            AverageSentencePosition = 0.0f;
            AverageSpread = 0.0f;
            _sentences = new List<Guid>();
            _leadingWords = new List<Guid>();
            _trailingWords = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
            _leadingWordSet = new HashSet<Guid>();
            _trailingWordSet = new HashSet<Guid>();
        }

        public void AddSentence(SentenceBox sentence)
        {
            ArgumentNullException.ThrowIfNull(sentence);
            AddSentence(sentence.GetSentenceID());
        }

        public bool AddSentence(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureUntrustedMutable();
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
            return GraphSynchronization.Execute(() => AddUnique(_sentences, _sentenceSet, sentenceId));
        }

        public void AddLeadingWord(WordBox word)
        {
            ArgumentNullException.ThrowIfNull(word);
            AddLeadingWord(word.GetWordID());
        }

        public bool AddLeadingWord(Guid wordId)
        {
            ValidateIdentityId(wordId, nameof(wordId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureUntrustedMutable();
                if (_leadingWordSet.Add(wordId))
                {
                    _leadingWords.Add(wordId);
                    return true;
                }
                return false;
            });
        }

        internal bool AddLeadingWordFromCore(Guid wordId)
        {
            ValidateIdentityId(wordId, nameof(wordId));
            return GraphSynchronization.Execute(() => AddUnique(_leadingWords, _leadingWordSet, wordId));
        }

        public void AddTrailingWord(WordBox word)
        {
            ArgumentNullException.ThrowIfNull(word);
            AddTrailingWord(word.GetWordID());
        }

        public bool AddTrailingWord(Guid wordId)
        {
            ValidateIdentityId(wordId, nameof(wordId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureUntrustedMutable();
                if (_trailingWordSet.Add(wordId))
                {
                    _trailingWords.Add(wordId);
                    return true;
                }
                return false;
            });
        }

        internal bool AddTrailingWordFromCore(Guid wordId)
        {
            ValidateIdentityId(wordId, nameof(wordId));
            return GraphSynchronization.Execute(() => AddUnique(_trailingWords, _trailingWordSet, wordId));
        }

        public Guid GetSenseID()
        {
            return GraphSynchronization.Execute(() => SenseID);
        }
        public List<Guid>? GetSentences()
        {
            return GraphSynchronization.Execute(() => new List<Guid>(_sentences));
        }
        public List<Guid>? GetLeadingWords()
        {
            return GraphSynchronization.Execute(() => new List<Guid>(_leadingWords));
        }
        public List<Guid>? GetTrailingWords()
        {
            return GraphSynchronization.Execute(() => new List<Guid>(_trailingWords));
        }
        public int GetSentenceCount()
        {
            return GraphSynchronization.Execute(() => _sentences.Count);
        }
        public int GetLeadingWordCount()
        {
            return GraphSynchronization.Execute(() => _leadingWords.Count);
        }
        public int GetTrailingWordCount()
        {
            return GraphSynchronization.Execute(() => _trailingWords.Count);
        }
        public bool ContainsSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => _sentenceSet.Contains(sentenceId));
        }
        public bool ContainsLeadingWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() => _leadingWordSet.Contains(wordId));
        }
        public bool ContainsTrailingWord(Guid wordId)
        {
            return GraphSynchronization.Execute(() => _trailingWordSet.Contains(wordId));
        }
        public float GetAverageWordPosition()
        {
            return GraphSynchronization.Execute(() => AverageWordPosition);
        }

        public float GetAverageSentencePosition()
        {
            return GraphSynchronization.Execute(() => AverageSentencePosition);
        }

        public float GetAverageSpread()
        {
            return GraphSynchronization.Execute(() => AverageSpread);
        }
        public void SetAverageWordPosition(float avgWordPos)
        {
            ValidateMetric(avgWordPos, nameof(avgWordPos));
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageWordPosition = avgWordPos; });
        }

        public void SetAverageSentencePosition(float avgSentPos)
        {
            ValidateMetric(avgSentPos, nameof(avgSentPos));
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageSentencePosition = avgSentPos; });
        }

        public void SetAverageSpread(float avgSpread)
        {
            ValidateMetric(avgSpread, nameof(avgSpread));
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageSpread = avgSpread; });
        }
        public void UpdateAverageMetrics(float avgWordPos, float avgSentPos, float avgSpread)
        {
            ValidateMetric(avgWordPos, nameof(avgWordPos));
            ValidateMetric(avgSentPos, nameof(avgSentPos));
            ValidateMetric(avgSpread, nameof(avgSpread));
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                AverageWordPosition = avgWordPos;
                AverageSentencePosition = avgSentPos;
                AverageSpread = avgSpread;
            });
        }
        public void ResetAverageMetrics()
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                AverageWordPosition = 0.0f;
                AverageSentencePosition = 0.0f;
                AverageSpread = 0.0f;
            });
        }
        internal void ClearSentences()
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _sentences.Clear(); _sentenceSet.Clear(); });
        }
        internal void RemoveSentence(Guid sentenceId)
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _sentences.Remove(sentenceId); _sentenceSet.Remove(sentenceId); });
        }

        internal void ClearLeadingWords()
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _leadingWords.Clear(); _leadingWordSet.Clear(); });
        }

        internal void RemoveLeadingWord(Guid wordId)
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _leadingWords.Remove(wordId); _leadingWordSet.Remove(wordId); });
        }
        internal void ClearTrailingWords()
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _trailingWords.Clear(); _trailingWordSet.Clear(); });
        }
        internal void RemoveTrailingWord(Guid wordId)
        {
            GraphSynchronization.Execute(() => { EnsureUntrustedMutable(); _trailingWords.Remove(wordId); _trailingWordSet.Remove(wordId); });
        }

        internal void MarkTrusted() => GraphSynchronization.Execute(() => _isTrusted = true);

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => { _isTrusted = true; _isReadOnly = true; });

        private static void ValidateSenseId(Guid id, string parameterName, bool allowRegisteredTrustedId)
        {
            if (allowRegisteredTrustedId)
            {
                ValidateIdentityId(id, parameterName);
            }
            else
            {
                LRD_Core.Instance.ValidateNewUntrustedId(NodeTargetKind.Sense, id, parameterName);
            }
        }

        private static void ValidateIdentityId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
        }

        private void EnsureUntrustedMutable()
        {
            if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
            if (_isTrusted) throw new InvalidOperationException("Trusted sense memberships can only be appended through LRD_Core.");
        }

        private void EnsureWritable()
        {
            if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
        }

        private static void ValidateMetric(float value, string parameterName)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Sense metrics must be finite.");
            }
        }

        private bool AddUnique(List<Guid> values, HashSet<Guid> index, Guid id)
        {
            if (_isReadOnly)
            {
                throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
            }
            if (index.Add(id))
            {
                values.Add(id);
                return true;
            }
            return false;
        }
    }
}
