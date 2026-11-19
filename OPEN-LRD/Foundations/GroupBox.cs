using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
{
    internal class GroupBox
    {
        private Guid GroupingID;
        private string? GroupingName;
        private Dictionary<Guid, SentenceBox>? _sentences;

        public GroupBox()
        {
            GroupingID = Guid.NewGuid();
            _sentences = new Dictionary<Guid, SentenceBox>();
        }
        public GroupBox(string groupingName)
        {
            GroupingID = Guid.NewGuid();
            GroupingName = groupingName;
            _sentences = new Dictionary<Guid, SentenceBox>();
        }

        public GroupBox(Guid groupingId)
        {
            GroupingID = groupingId;
            _sentences = new Dictionary<Guid, SentenceBox>();
        }

        public GroupBox(Dictionary<Guid, SentenceBox> sentences)
        {
            GroupingID = Guid.NewGuid();
            _sentences = new Dictionary<Guid, SentenceBox>(sentences);
        }
        public GroupBox(Guid groupingId, Dictionary<Guid, SentenceBox> sentences)
        {
            GroupingID = groupingId;
            _sentences = new Dictionary<Guid, SentenceBox>(sentences);
        }

        public GroupBox(Guid groupingId, string groupingName, Dictionary<Guid, SentenceBox> sentences)
        {
            GroupingID = groupingId;
            GroupingName = groupingName;
            _sentences = new Dictionary<Guid, SentenceBox>(sentences);
        }

        public Dictionary<Guid, SentenceBox>? GetSentences()
        {
            return _sentences;
        }

        public Guid GetGroupingID()
        {
            return GroupingID;
        }

        public void AddSentence(SentenceBox sentence)
        {
            if (_sentences == null)
            {
                _sentences = new Dictionary<Guid, SentenceBox>();
            }
            _sentences[sentence.GetSentenceID()] = sentence;
        }

        public string? GetGroupingName()
        {
            return GroupingName;
        }

    }
}
