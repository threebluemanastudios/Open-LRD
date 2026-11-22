using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OPEN_LRD;

namespace OPEN_LRD.Foundations
{
    /// <summary>Stores an append-only trusted set of group memberships.</summary>
    internal class SourceBox
    {
        private Guid SourceID;
        private string? SourceName;
        private List<Guid> _groupings;
        private HashSet<Guid> _groupingSet;
        private bool _identityFrozen;

        public SourceBox()
        {
            SourceID = Guid.NewGuid();
            _groupings = new List<Guid>();
            _groupingSet = new HashSet<Guid>();
        }

        public SourceBox(Guid sourceId)
        {
            ValidateIdentityId(sourceId, nameof(sourceId));
            SourceID = sourceId;
            _groupings = new List<Guid>();
            _groupingSet = new HashSet<Guid>();
        }

        public SourceBox(Guid sourceId, string? sourceName)
        {
            ValidateIdentityId(sourceId, nameof(sourceId));
            SourceID = sourceId;
            SourceName = NormalizeOptionalText(sourceName);
            _groupings = new List<Guid>();
            _groupingSet = new HashSet<Guid>();
        }
        public SourceBox(Guid sourceId, string? sourceName, Dictionary<Guid, GroupBox> groupings)
        {
            ValidateIdentityId(sourceId, nameof(sourceId));
            ArgumentNullException.ThrowIfNull(groupings);
            foreach (KeyValuePair<Guid, GroupBox> entry in groupings)
            {
                ValidateIdentityId(entry.Key, nameof(groupings));
                if (entry.Value == null)
                {
                    throw new ArgumentException("Grouping entries cannot be null.", nameof(groupings));
                }
                Guid groupingId = entry.Value.GetGroupingID();
                ValidateIdentityId(groupingId, nameof(groupings));
                if (entry.Key != groupingId)
                {
                    throw new ArgumentException("Grouping keys must match the contained GroupBox IDs.", nameof(groupings));
                }
            }
            SourceID = sourceId;
            SourceName = NormalizeOptionalText(sourceName);
            _groupings = GraphSynchronization.Execute(() => groupings.Values.Select(group => group.GetGroupingID()).Distinct().ToList());
            _groupingSet = GraphSynchronization.Execute(() => new HashSet<Guid>(_groupings));
        }

        public SourceBox(IEnumerable<Guid> groupingIds)
            : this(Guid.NewGuid(), null, groupingIds)
        {
        }

        public SourceBox(Guid sourceId, IEnumerable<Guid> groupingIds)
            : this(sourceId, null, groupingIds)
        {
        }

        public SourceBox(Guid sourceId, string? sourceName, IEnumerable<Guid> groupingIds)
        {
            ValidateIdentityId(sourceId, nameof(sourceId));
            ArgumentNullException.ThrowIfNull(groupingIds);
            ValidateIdentityIds(groupingIds, nameof(groupingIds));
            SourceID = sourceId;
            SourceName = NormalizeOptionalText(sourceName);
            _groupings = GraphSynchronization.Execute(() => groupingIds.Distinct().ToList());
            _groupingSet = GraphSynchronization.Execute(() => new HashSet<Guid>(_groupings));
        }

        public Dictionary<Guid, GroupBox>? GetGroupings()
        {
            List<Guid> ids = GetGroupingIDs() ?? new List<Guid>();
            Dictionary<Guid, GroupBox> groups = new Dictionary<Guid, GroupBox>();
            foreach (Guid id in ids)
            {
                GroupBox? group = LRD_Core.Instance.GetTrustedGroupBox(id);
                if (group == null)
                {
                    throw new InvalidDataException($"Source '{SourceID}' references missing trusted group '{id}'.");
                }
                groups[id] = group;
            }
            return groups;
        }

        public List<Guid>? GetGroupingIDs()
        {
            return GraphSynchronization.Execute(() => new List<Guid>(_groupings));
        }

        public Guid GetSourceID()
        {
            return GraphSynchronization.Execute(() => SourceID);
        }

        public int GetGroupingCount()
        {
            return GraphSynchronization.Execute(() => _groupings.Count);
        }

        public bool ContainsGrouping(Guid groupingId)
        {
            return GraphSynchronization.Execute(() => _groupingSet.Contains(groupingId));
        }

        public string? GetSourceName()
        {
            return GraphSynchronization.Execute(() => SourceName);
        }

        public void SetSourceName(string? sourceName)
        {
            GraphSynchronization.Execute(() => { EnsureIdentityMutable(); SourceName = NormalizeOptionalText(sourceName); });
        }

        public bool AddGrouping(GroupBox grouping)
        {
            ArgumentNullException.ThrowIfNull(grouping);
            Guid groupingId = grouping.GetGroupingID();
            ValidateIdentityId(groupingId, nameof(grouping));
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                return AddGroupingInternal(groupingId);
            });
        }

        public bool AddGrouping(Guid groupingId)
        {
            ValidateIdentityId(groupingId, nameof(groupingId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                return AddGroupingInternal(groupingId);
            });
        }

        internal bool AddGroupingFromCore(Guid groupingId)
        {
            ValidateIdentityId(groupingId, nameof(groupingId));
            return GraphSynchronization.Execute(() => AddGroupingInternal(groupingId));
        }

        internal bool IsIdentityFrozen => GraphSynchronization.Execute(() => _identityFrozen);

        internal void FreezeIdentity()
        {
            GraphSynchronization.Execute(() => _identityFrozen = true);
        }

        internal void MarkReadOnly() => FreezeIdentity();

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

        private bool AddGroupingInternal(Guid groupingId)
        {
            if (_groupingSet.Add(groupingId))
            {
                _groupings.Add(groupingId);
                return true;
            }
            return false;
        }

        private void EnsureIdentityMutable()
        {
            if (_identityFrozen)
            {
                throw new InvalidOperationException("Source identity is frozen after trust.");
            }
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
