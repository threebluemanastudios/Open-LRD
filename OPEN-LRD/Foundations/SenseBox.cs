using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class SenseBox
    {
        private Guid SenseID;
        private List<Guid> _sentences;
        private List<Guid> _leadingWords;
        private List<Guid> _trailingWords;
        private HashSet<Guid> _sentenceSet;
        private HashSet<Guid> _leadingWordSet;
        private HashSet<Guid> _trailingWordSet;
        private bool _isReadOnly;
        private float AverageWordPosition;
        private float AverageSentencePosition;
        private float AverageSpread;

        public SenseBox(Guid senseId)
        {
            ValidateIdentityId(senseId, nameof(senseId));
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
                EnsureWritable();
                if (_sentenceSet.Add(sentenceId))
                {
                    _sentences.Add(sentenceId);
                    return true;
                }
                return false;
            });
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
                EnsureWritable();
                if (_leadingWordSet.Add(wordId))
                {
                    _leadingWords.Add(wordId);
                    return true;
                }
                return false;
            });
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
                EnsureWritable();
                if (_trailingWordSet.Add(wordId))
                {
                    _trailingWords.Add(wordId);
                    return true;
                }
                return false;
            });
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
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageWordPosition = avgWordPos; });
        }

        public void SetAverageSentencePosition(float avgSentPos)
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageSentencePosition = avgSentPos; });
        }

        public void SetAverageSpread(float avgSpread)
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); AverageSpread = avgSpread; });
        }
        public void UpdateAverageMetrics(float avgWordPos, float avgSentPos, float avgSpread)
        {
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
        public void ClearSentences()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _sentences.Clear(); _sentenceSet.Clear(); });
        }
        public void RemoveSentence(Guid sentenceId)
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _sentences.Remove(sentenceId); _sentenceSet.Remove(sentenceId); });
        }

        public void ClearLeadingWords()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _leadingWords.Clear(); _leadingWordSet.Clear(); });
        }

        public void RemoveLeadingWord(Guid wordId)
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _leadingWords.Remove(wordId); _leadingWordSet.Remove(wordId); });
        }
        public void ClearTrailingWords()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _trailingWords.Clear(); _trailingWordSet.Clear(); });
        }
        public void RemoveTrailingWord(Guid wordId)
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); _trailingWords.Remove(wordId); _trailingWordSet.Remove(wordId); });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => _isReadOnly = true);

        private static void ValidateIdentityId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
        }

        private void EnsureWritable()
        {
            if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
        }
    }
}
