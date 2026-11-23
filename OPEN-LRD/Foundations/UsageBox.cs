using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    /// <summary>Stores usage sentence memberships; trusted memberships are append-only.</summary>
    internal class UsageBox
    {
        private Guid UsageID;
        private List<Guid>? _sentences;
        private HashSet<Guid> _sentenceSet;
        private bool _isTrusted;
        private bool _isReadOnly;

        /// <summary>Initializes a new untrusted usage box with a generated identifier.</summary>
        public UsageBox()
        {
            UsageID = LRD_Core.Instance.GenerateUntrustedId(NodeTargetKind.Usage);
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        /// <summary>Initializes a new untrusted usage box with an explicit identifier.</summary>
        /// <param name="usageId">Unique usage identifier.</param>
        public UsageBox(Guid usageId)
            : this(usageId, allowRegisteredTrustedId: false)
        {
        }

        internal UsageBox(Guid usageId, bool allowRegisteredTrustedId)
        {
            ValidateUsageId(usageId, nameof(usageId), allowRegisteredTrustedId);
            UsageID = usageId;
            _sentences = new List<Guid>();
            _sentenceSet = new HashSet<Guid>();
        }

        /// <summary>Initializes a new untrusted usage box with an explicit identifier and initial sentence memberships.</summary>
        /// <param name="usageId">Unique usage identifier.</param>
        /// <param name="sentences">Initial sentence identifiers.</param>
        public UsageBox(Guid usageId, List<Guid> sentences)
            : this(usageId, sentences, allowRegisteredTrustedId: false)
        {
        }

        internal UsageBox(Guid usageId, List<Guid> sentences, bool allowRegisteredTrustedId)
        {
            ValidateUsageId(usageId, nameof(usageId), allowRegisteredTrustedId);
            ArgumentNullException.ThrowIfNull(sentences);
            ValidateIdentityIds(sentences, nameof(sentences));
            UsageID = usageId;
            _sentences = new List<Guid>(sentences);
            _sentenceSet = new HashSet<Guid>(sentences);
        }

        /// <summary>Returns the unique identifier of this usage box.</summary>
        /// <returns>The usage box <see cref="Guid"/>.</returns>
        public Guid GetUsageID()
        {
            return GraphSynchronization.Execute(() => UsageID);
        }

        /// <summary>Returns an isolated snapshot of sentence identifiers associated with this usage.</summary>
        /// <returns>A copy of the sentence identifier list, or <see langword="null"/> if uninitialized.</returns>
        public List<Guid>? GetSentences()
        {
            return GraphSynchronization.Execute(() =>
                _sentences == null ? null : new List<Guid>(_sentences));
        }

        /// <summary>Returns the count of sentence memberships in this usage box.</summary>
        public int GetSentenceCount()
        {
            return GraphSynchronization.Execute(() => _sentences?.Count ?? 0);
        }

        /// <summary>Returns whether the specified sentence identifier is a member of this usage box.</summary>
        public bool ContainsSentence(Guid sentenceId)
        {
            return GraphSynchronization.Execute(() => _sentenceSet.Contains(sentenceId));
        }

        /// <summary>Adds a sentence membership while this usage box is untrusted.</summary>
        /// <param name="sentenceId">Sentence identifier to add.</param>
        /// <returns><see langword="true"/> if added; <see langword="false"/> if already present.</returns>
        public bool AddSentence(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() =>
            {
                EnsureUntrustedMutable();
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

        internal bool AddSentenceFromCore(Guid sentenceId)
        {
            ValidateIdentityId(sentenceId, nameof(sentenceId));
            return GraphSynchronization.Execute(() =>
            {
                if (_isReadOnly)
                {
                    throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
                }
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

        internal void MarkTrusted() => GraphSynchronization.Execute(() => _isTrusted = true);

        internal void RemoveSentenceFromCore(Guid sentenceId)
        {
            GraphSynchronization.Execute(() =>
            {
                if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
                _sentences?.Remove(sentenceId);
                _sentenceSet.Remove(sentenceId);
            });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => { _isTrusted = true; _isReadOnly = true; });

        private static void ValidateUsageId(Guid id, string parameterName, bool allowRegisteredTrustedId)
        {
            if (allowRegisteredTrustedId)
            {
                ValidateIdentityId(id, parameterName);
            }
            else
            {
                LRD_Core.Instance.ValidateNewUntrustedId(NodeTargetKind.Usage, id, parameterName);
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
            foreach (Guid id in ids)
            {
                ValidateIdentityId(id, parameterName);
            }
        }

        private void EnsureUntrustedMutable()
        {
            if (_isReadOnly) throw new InvalidOperationException("Disk-paged boxes are read-only; materialize the disk store before mutation.");
            if (_isTrusted) throw new InvalidOperationException("Trusted usage memberships can only be appended through LRD_Core.");
        }
    }
}
