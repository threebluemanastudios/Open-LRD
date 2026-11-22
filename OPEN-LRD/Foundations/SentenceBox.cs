using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class SentenceBox
    {
        private Guid SentenceID;
        private Guid? SenseID;
        private List<Guid>? _words;
        private bool _identityFrozen;

        public SentenceBox()
        {
            SentenceID = Guid.NewGuid();
            SenseID = null;
            _words = null;
        }

        public SentenceBox(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            SentenceID = sentenceId;
            SenseID = null;
            _words = null;
        }
        public SentenceBox(List<Guid> words)
        {
            ValidateIdentityIds(words, nameof(words));
            SentenceID = Guid.NewGuid();
            SenseID = null;
            _words = new List<Guid>(words);
        }
        public SentenceBox(Guid sentenceId, List<Guid> words)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            ValidateIdentityIds(words, nameof(words));
            SentenceID = sentenceId;
            SenseID = null;
            _words = new List<Guid>(words);
        }
        public SentenceBox(Guid sentenceId, Guid? senseId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            ValidateOptionalIdentityId(senseId, nameof(senseId));
            SentenceID = sentenceId;
            SenseID = senseId;
            _words = null;
        }

        public SentenceBox(Guid sentenceId, Guid senseId, List<Guid> words)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityIds(words, nameof(words));
            SentenceID = sentenceId;
            SenseID = senseId;
            _words = new List<Guid>(words);
        }

        public SentenceBox(Guid sentenceId, Guid? senseId, Dictionary<int, WordBox> words)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            ValidateOptionalIdentityId(senseId, nameof(senseId));
            ArgumentNullException.ThrowIfNull(words);
            List<Guid> wordIds = new List<Guid>();
            foreach (KeyValuePair<int, WordBox> entry in words.OrderBy(entry => entry.Key))
            {
                if (entry.Key < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(words), "Sentence word positions cannot be negative.");
                }
                WordBox word = entry.Value;
                if (word == null)
                {
                    throw new ArgumentException("Word entries cannot be null.", nameof(words));
                }
                Guid wordId = word.GetWordID();
                ValidateIdentityId(wordId, nameof(words));
                wordIds.Add(wordId);
            }
            SentenceID = sentenceId;
            SenseID = senseId;
            _words = wordIds;
        }

        public List<Guid>? GetWords()
        {
            return GraphSynchronization.Execute(() =>
            {
                return _words == null ? null : new List<Guid>(_words);
            });
        }


        public Guid? GetSenseID()
        {
            return GraphSynchronization.Execute(() => SenseID);
        }

        public Guid GetSentenceID()
        {
            return GraphSynchronization.Execute(() => SentenceID);
        }

        public void SetSenseID(Guid senseId)
        {
            ValidateIdentityId(senseId, nameof(senseId));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (SenseID == null)
                {
                    SenseID = senseId;
                }
                else
                {
                    throw new InvalidOperationException("SenseID is already set and cannot be changed.");
                }
            });
        }

        public void SetWords(List<Guid> words)
        {
            ValidateIdentityIds(words, nameof(words));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_words == null)
                {
                    _words = new List<Guid>(words);
                }
                else
                {
                    throw new InvalidOperationException("Words are already set and cannot be changed.");
                }
            });
        }

        internal bool ReplaceWordReference(Guid oldWordId, Guid canonicalWordId)
        {
            ValidateIdentityId(oldWordId, nameof(oldWordId));
            ValidateIdentityId(canonicalWordId, nameof(canonicalWordId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_words == null)
                {
                    return false;
                }
                bool replaced = false;
                for (int index = 0; index < _words.Count; index++)
                {
                    if (_words[index] == oldWordId)
                    {
                        _words[index] = canonicalWordId;
                        replaced = true;
                    }
                }
                return replaced;
            });
        }

        internal bool HasCompleteIdentity => GraphSynchronization.Execute(() =>
            SentenceID != Guid.Empty &&
            SenseID.HasValue &&
            SenseID.Value != Guid.Empty &&
            _words != null);

        internal SentenceIdentityKey GetIdentityKey()
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Sentence identity is incomplete.");
                }

                return new SentenceIdentityKey(_words!, SenseID!.Value);
            });
        }

        internal bool IsIdentityFrozen => GraphSynchronization.Execute(() => _identityFrozen);

        internal void FreezeIdentity()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Cannot trust a sentence with incomplete identity.");
                }

                _identityFrozen = true;
            });
        }

        private void EnsureIdentityMutable()
        {
            if (_identityFrozen)
            {
                throw new InvalidOperationException("Sentence identity is frozen after trust.");
            }
        }

        private static void ValidateIdentityId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
        }

        private static void ValidateOptionalIdentityId(Guid? id, string parameterName)
        {
            if (id.HasValue)
            {
                ValidateIdentityId(id.Value, parameterName);
            }
        }

        private static void ValidateIdentityIds(IEnumerable<Guid> ids, string parameterName)
        {
            ArgumentNullException.ThrowIfNull(ids, parameterName);
            foreach (Guid id in ids)
            {
                ValidateIdentityId(id, parameterName);
            }
        }
    }

    internal sealed class SentenceIdentityKey : IEquatable<SentenceIdentityKey>
    {
        private readonly Guid[] _words;

        internal SentenceIdentityKey(IEnumerable<Guid> words, Guid senseId)
        {
            ArgumentNullException.ThrowIfNull(words);
            if (senseId == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(senseId));
            }
            _words = words.ToArray();
            if (_words.Any(wordId => wordId == Guid.Empty))
            {
                throw new ArgumentException("Identity sequences cannot contain Guid.Empty.", nameof(words));
            }
            SenseId = senseId;
        }

        internal Guid SenseId { get; }

        public bool Equals(SentenceIdentityKey? other)
        {
            return other != null && SenseId == other.SenseId && _words.SequenceEqual(other._words);
        }

        public override bool Equals(object? obj)
        {
            return obj is SentenceIdentityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SenseId);
            foreach (Guid wordId in _words)
            {
                hash.Add(wordId);
            }
            return hash.ToHashCode();
        }
    }
}
