using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
{
    internal class SentenceBox
    {
        private Guid SentenceID;
        private Guid? SenseID;
        private List<Guid> _words;

        public SentenceBox()
        {
            SentenceID = Guid.NewGuid();
            SenseID = null;
            _words = new List<Guid>();
        }

        public SentenceBox(Guid sentenceId)
        {
            SentenceID = sentenceId;
            SenseID = null;
            _words = new List<Guid>();
        }
        public SentenceBox(List<Guid> words)
        {
            SentenceID = Guid.NewGuid();
            SenseID = null;
            _words = new List<Guid>(words);
        }
        public SentenceBox(Guid sentenceId, List<Guid> words)
        {
            SentenceID = sentenceId;
            SenseID = null;
            _words = new List<Guid>(words);
        }
        public SentenceBox(Guid sentenceId, Guid? senseId)
        {
            SentenceID = sentenceId;
            SenseID = senseId;
            _words = new List<Guid>();
        }

        public SentenceBox(Guid sentenceId, Guid? senseId, Dictionary<int, WordBox> words)
        {
            SentenceID = sentenceId;
            SenseID = senseId;
            _words = new List<Guid>(words.Values.Select(wb => wb.GetWordID()));
        }

        public List<Guid>? GetWords()
        {
            return _words;
        }


        public Guid GetSenseID()
        {
            return SenseID ?? Guid.Empty;
        }

        public Guid GetSentenceID()
        {
            return SentenceID;
        }
    }
}
