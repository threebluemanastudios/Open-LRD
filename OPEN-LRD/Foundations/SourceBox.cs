using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
{
    internal class SourceBox
    {
        private Guid SourceID;
        private string? SourceName;
        private Dictionary<Guid, GroupBox>? _groupings;

        public SourceBox()
        {
            SourceID = Guid.NewGuid();
            _groupings = new Dictionary<Guid, GroupBox>();
        }

        public SourceBox(Guid sourceId)
        {
            SourceID = sourceId;
            _groupings = new Dictionary<Guid, GroupBox>();
        }

        public SourceBox(Guid sourceId, string sourceName)
        {
            SourceID = sourceId;
            SourceName = sourceName;
            _groupings = new Dictionary<Guid, GroupBox>();
        }
        public SourceBox(Guid sourceId, string sourceName, Dictionary<Guid, GroupBox> groupings)
        {
            SourceID = sourceId;
            SourceName = sourceName;
            _groupings = new Dictionary<Guid, GroupBox>(groupings);
        }

        public Dictionary<Guid, GroupBox>? GetGroupings()
        {
            return _groupings;
        }

        public Guid GetSourceID()
        {
            return SourceID;
        }

        public string? GetSourceName()
        {
            return SourceName;
        }

        public void SetSourceName(string sourceName)
        {
            SourceName = sourceName;
        }

        public void AddGrouping(GroupBox grouping)
        {
            if (_groupings == null)
            {
                _groupings = new Dictionary<Guid, GroupBox>();
            }
            _groupings[grouping.GetGroupingID()] = grouping;
        }

    }
}
