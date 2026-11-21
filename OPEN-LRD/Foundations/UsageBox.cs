using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class UsageBox
    {
        private Guid UsageID;
        private List<Guid>? _sentences;
        private HashSet<Guid> _sentenceSet;
        private bool _isReadOnly;

        /// <summary>
        /// 
        /// </summary>
        public UsageBox()
        {
            UsageID = Guid.NewGuid();
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usageId"></param>
        public UsageBox(Guid usageId)
        {
            ValidateIdentityId(usageId, nameof(usageId));
            UsageID = usageId;
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="usageId"></param>
        /// <param name="sentences"></param>
        public UsageBox(Guid usageId, List<Guid> sentences)
        {
            ValidateIdentityId(usageId, nameof(usageId));
            ArgumentNullException.ThrowIfNull(sentences);
            ValidateIdentityIds(sentences, nameof(sentences));
            UsageID = usageId;
            _sentences = new List<Guid>(sentences);
            _sentenceSet = new HashSet<Guid>(sentences);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Guid GetUsageID()
        {
            return GraphSynchronization.Execute(() => UsageID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<Guid>? GetSentences()
        {
            return GraphSynchronization.Execute(() =>
                _sentences == null ? null : new List<Guid>(_sentences));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sentenceId"></param>
        /// <returns></returns>
        public bool AddSentence(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                if (_sentences == null)
                {
                    _sentences = new List<Guid>();
                }
                if (_sentenceSet.Add(sentenceId))
                {
                    _sentences.Add(sentenceId);
                    return true;
                }
                return false;
            });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => _isReadOnly = true);

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

        private void EnsureWritable()
        {
            if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
        }
    }
}
