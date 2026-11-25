using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPEN_LRD.Foundations
{
    /// <summary>
    /// A public foundation type representing the part of speech of a word, used in the WordBox class.
    /// </summary>
    public enum PartOfSpeech
    {
        /// <summary>
        /// Represents a noun.
        /// </summary>
        Noun,
        /// <summary>
        /// Represents a verb.
        /// </summary>
        Verb,
        /// <summary>
        /// Represents an adjective.
        /// </summary>
        Adjective,
        /// <summary>
        /// Represents an adverb.
        /// </summary>
        Adverb,
        /// <summary>
        /// Represents a pronoun.
        /// </summary>
        Pronoun,
        /// <summary>
        /// Represents a preposition.
        /// </summary>
        Preposition,
        /// <summary>
        /// Represents a conjunction.
        /// </summary>
        Conjunction,
        /// <summary>
        /// Represents an interjection.
        /// </summary>
        Interjection,
        /// <summary>
        /// Represents a function.
        /// </summary>
        Function,
        /// <summary>
        /// Represents a value.
        /// </summary>
        Value,
        /// <summary>
        /// Placeholder for a word whose part of speech has not been determined yet. Words with this value cannot be trusted.
        /// Appended last so existing persisted values keep their meaning.
        /// </summary>
        Unknown,
    }
    internal class WordBox
    {
        private Guid WordID;
        private Guid? SenseID;
        private Guid? UsageID;
        private List<Guid>? _symbols;
        private PartOfSpeech? _partOfSpeech;
        private bool _identityFrozen;

        public WordBox(List<Guid> symbols)
        {
            ValidateIdentityIds(symbols, nameof(symbols));
            WordID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Word);
            SenseID = null;
            UsageID = null;
            _symbols = new List<Guid>(symbols);
        }
        public WordBox( Guid senseId, Guid usageId)
        {
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            WordID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Word);
            SenseID = senseId;
            UsageID = usageId;
            _symbols = null;
        }
        public WordBox(Guid wordId, Guid senseId, Guid usageId)
        {
            ValidateWordId(wordId, nameof(wordId), allowRegisteredTrustedId: false);
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = null;
        }

        public WordBox(Guid wordID, List<Guid> symbols)
        {
            ValidateWordId(wordID, nameof(wordID), allowRegisteredTrustedId: false);
            ValidateIdentityIds(symbols, nameof(symbols));
            WordID = wordID;
            SenseID = null;
            UsageID = null;
            _symbols = new List<Guid>(symbols);
        }

        public WordBox(Guid wordId, Guid senseId, Guid usageId, List<Guid> symbols)
        {
            ValidateWordId(wordId, nameof(wordId), allowRegisteredTrustedId: false);
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            ValidateIdentityIds(symbols, nameof(symbols));
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
        }
        public WordBox( Guid senseId, Guid usageId, List<Guid> symbols)
        {
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            ValidateIdentityIds(symbols, nameof(symbols));
            WordID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Word);
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
        }
        public WordBox(Guid wordId, Guid senseId, Guid usageId, List<Guid> symbols, PartOfSpeech pos)
            : this(wordId, senseId, usageId, symbols, pos, allowRegisteredTrustedId: false)
        {
        }
        internal WordBox(Guid wordId, Guid senseId, Guid usageId, List<Guid> symbols, PartOfSpeech pos, bool allowRegisteredTrustedId)
        {
            ValidateWordId(wordId, nameof(wordId), allowRegisteredTrustedId);
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            ValidateIdentityIds(symbols, nameof(symbols));
            ValidatePartOfSpeech(pos, nameof(pos));
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
            _partOfSpeech = pos;
        }
        public WordBox(Guid senseId, Guid usageId, List<Guid> symbols, PartOfSpeech pos)
        {
            ValidateIdentityId(senseId, nameof(senseId));
            ValidateIdentityId(usageId, nameof(usageId));
            ValidateIdentityIds(symbols, nameof(symbols));
            ValidatePartOfSpeech(pos, nameof(pos));
            WordID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Word);
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
            _partOfSpeech = pos;
        }

        public void SetSymbols(List<Guid> symbols)
        {
            ValidateIdentityIds(symbols, nameof(symbols));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_symbols == null)
                {
                    _symbols = new List<Guid>(symbols);
                }
                else
                {
                    throw new InvalidOperationException("Symbols are already set and cannot be changed.");
                }
            });
        }

        internal bool ReplaceSymbolReference(Guid oldSymbolId, Guid canonicalSymbolId)
        {
            ValidateIdentityId(oldSymbolId, nameof(oldSymbolId));
            ValidateIdentityId(canonicalSymbolId, nameof(canonicalSymbolId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_symbols == null)
                {
                    return false;
                }
                bool replaced = false;
                for (int index = 0; index < _symbols.Count; index++)
                {
                    if (_symbols[index] == oldSymbolId)
                    {
                        _symbols[index] = canonicalSymbolId;
                        replaced = true;
                    }
                }
                return replaced;
            });
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

        public void SetUsageID(Guid usageId)
        {
            ValidateIdentityId(usageId, nameof(usageId));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (UsageID == null)
                {
                    UsageID = usageId;
                }
                else
                {
                    throw new InvalidOperationException("UsageID is already set and cannot be changed.");
                }
            });
        }

        public List<Guid>? GetSymbols()
        {
            return GraphSynchronization.Execute(() =>
            {
                return _symbols == null ? null : new List<Guid>(_symbols);
            });
        }

        public Guid GetWordID()
        {
            return GraphSynchronization.Execute(() => WordID);
        }

        public Guid? GetSenseID()
        {
            return GraphSynchronization.Execute(() => SenseID);
        }

        public Guid? GetUsageID()
        {
            return GraphSynchronization.Execute(() => UsageID);
        }

        public void SetPartOfSpeech(PartOfSpeech pos)
        {
            ValidatePartOfSpeech(pos, nameof(pos));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (_partOfSpeech == null)
                {
                    _partOfSpeech = pos;
                }
                else
                {
                    throw new InvalidOperationException("PartOfSpeech is already set and cannot be changed.");
                }
            });
        }

        public PartOfSpeech? GetPartOfSpeech()
        {
            return GraphSynchronization.Execute(() => _partOfSpeech);
        }

        internal bool HasCompleteIdentity => GraphSynchronization.Execute(() =>
            WordID != Guid.Empty &&
            SenseID.HasValue &&
            SenseID.Value != Guid.Empty &&
            UsageID.HasValue &&
            UsageID.Value != Guid.Empty &&
            _symbols != null &&
            _partOfSpeech.HasValue);

        internal WordIdentityKey GetIdentityKey()
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Word identity is incomplete.");
                }

                return new WordIdentityKey(_symbols!, SenseID!.Value, UsageID!.Value, _partOfSpeech!.Value);
            });
        }

        internal bool IsIdentityFrozen => GraphSynchronization.Execute(() => _identityFrozen);

        internal void FreezeIdentity()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Cannot trust a word with incomplete identity.");
                }

                _identityFrozen = true;
            });
        }

        private void EnsureIdentityMutable()
        {
            if (_identityFrozen)
            {
                throw new InvalidOperationException("Word identity is frozen after trust.");
            }
        }

        private static void ValidateWordId(Guid id, string parameterName, bool allowRegisteredTrustedId)
        {
            if (allowRegisteredTrustedId)
            {
                ValidateIdentityId(id, parameterName);
            }
            else
            {
                LRD_Core.Instance.ValidateNewUntrustedId(NodeTargetKind.Word, id, parameterName);
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
            ArgumentNullException.ThrowIfNull(ids, parameterName);
            foreach (Guid id in ids)
            {
                ValidateIdentityId(id, parameterName);
            }
        }

        private static void ValidatePartOfSpeech(PartOfSpeech value, string parameterName)
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "The part-of-speech value is undefined.");
            }
        }

    }

    internal sealed class WordIdentityKey : IEquatable<WordIdentityKey>
    {
        private readonly Guid[] _symbols;

        internal WordIdentityKey(IEnumerable<Guid> symbols, Guid senseId, Guid usageId, PartOfSpeech partOfSpeech)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            if (senseId == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(senseId));
            }
            if (usageId == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(usageId));
            }
            if (!Enum.IsDefined(partOfSpeech))
            {
                throw new ArgumentOutOfRangeException(nameof(partOfSpeech), "The part-of-speech value is undefined.");
            }
            _symbols = symbols.ToArray();
            if (_symbols.Any(symbolId => symbolId == Guid.Empty))
            {
                throw new ArgumentException("Identity sequences cannot contain Guid.Empty.", nameof(symbols));
            }
            SenseId = senseId;
            UsageId = usageId;
            PartOfSpeech = partOfSpeech;
        }

        internal Guid SenseId { get; }
        internal Guid UsageId { get; }
        internal PartOfSpeech PartOfSpeech { get; }

        public bool Equals(WordIdentityKey? other)
        {
            return other != null &&
                   SenseId == other.SenseId &&
                   UsageId == other.UsageId &&
                   PartOfSpeech == other.PartOfSpeech &&
                   _symbols.SequenceEqual(other._symbols);
        }

        public override bool Equals(object? obj)
        {
            return obj is WordIdentityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SenseId);
            hash.Add(UsageId);
            hash.Add(PartOfSpeech);
            foreach (Guid symbolId in _symbols)
            {
                hash.Add(symbolId);
            }
            return hash.ToHashCode();
        }
    }
}
