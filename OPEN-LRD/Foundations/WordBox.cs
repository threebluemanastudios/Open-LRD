using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
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

    }
    internal class WordBox
    {
        private Guid WordID;
        private Guid SenseID;
        private Guid UsageID;
        private List<Guid>? _symbols;
        private PartOfSpeech? _partOfSpeech;

        public WordBox( Guid senseId, Guid usageId)
        {
            WordID = Guid.NewGuid();
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>();
        }
        public WordBox(Guid wordId, Guid senseId, Guid usageId)
        {
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>();
        }

        public WordBox(Guid wordId, Guid senseId, Guid usageId, List<Guid> symbols)
        {
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
        }
        public WordBox( Guid senseId, Guid usageId, List<Guid> symbols)
        {
            WordID = Guid.NewGuid();
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
        }
        public WordBox(Guid wordId, Guid senseId, Guid usageId, List<Guid> symbols, PartOfSpeech pos)
        {
            WordID = wordId;
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
            _partOfSpeech = pos;
        }
        public WordBox(Guid senseId, Guid usageId, List<Guid> symbols, PartOfSpeech pos)
        {
            WordID = Guid.NewGuid();
            SenseID = senseId;
            UsageID = usageId;
            _symbols = new List<Guid>(symbols);
            _partOfSpeech = pos;
        }

        public void SetSymbols(List<Guid> symbols)
        {
            if (_symbols == null)
            {
                _symbols = new List<Guid>(symbols);
            }
            else
            {
                _symbols.Clear();
                _symbols.AddRange(symbols);
            }
        }

        public List<Guid>? GetSymbols()
        {
            return _symbols;
        }

        public Guid GetWordID()
        {
            return WordID;
        }

        public Guid GetSenseID()
        {
            return SenseID;
        }

        public Guid GetUsageID()
        {
            return UsageID;
        }

        public void SetPartOfSpeech(PartOfSpeech pos)
        {
            _partOfSpeech = pos;
        }

        public PartOfSpeech? GetPartOfSpeech()
        {
            return _partOfSpeech;
        }

    }
}
