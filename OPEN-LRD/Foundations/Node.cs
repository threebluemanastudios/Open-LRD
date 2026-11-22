using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace OPEN_LRD.Foundations
{
    /// <summary>
    /// Identifies the Foundation box wrapped by a graph node.
    /// </summary>
    public enum NodeTargetKind
    {
        /// <summary>Wraps a SymbolBox.</summary>
        Symbol,
        /// <summary>Wraps a WordBox.</summary>
        Word,
        /// <summary>Wraps a SentenceBox.</summary>
        Sentence,
        /// <summary>Wraps a SenseBox.</summary>
        Sense,
        /// <summary>Wraps a UsageBox.</summary>
        Usage,
        /// <summary>Wraps a SourceBox.</summary>
        Source,
        /// <summary>Wraps a GroupBox.</summary>
        Group,
        /// <summary>Wraps a FunctionBox.</summary>
        Function
    }

    /// <summary>
    /// Identifies the relationship represented by a graph link.
    /// </summary>
    public enum NodeRelationType
    {
        /// <summary>A symbol or word has a sense.</summary>
        HasSense,
        /// <summary>A word has a usage.</summary>
        HasUsage,
        /// <summary>A word contains a symbol.</summary>
        ContainsSymbol,
        /// <summary>A sentence contains a word.</summary>
        ContainsWord,
        /// <summary>A group contains a sentence.</summary>
        ContainsSentence,
        /// <summary>A source contains a group.</summary>
        ContainsGroup,
        /// <summary>A sentence starts with a word.</summary>
        LeadingWord,
        /// <summary>A sentence ends with a word.</summary>
        TrailingWord,
        /// <summary>A synonym relationship.</summary>
        Synonym,
        /// <summary>An antonym relationship.</summary>
        Antonym,
        /// <summary>An is-a relationship.</summary>
        IsA,
        /// <summary>A part-of relationship.</summary>
        PartOf,
        /// <summary>A causes relationship.</summary>
        Causes,
        /// <summary>An implies relationship.</summary>
        Implies,
        /// <summary>A function evaluation relationship.</summary>
        EvaluatesWithFunction,
        /// <summary>A caller-defined relationship.</summary>
        Custom
    }

    internal enum NodeLinkOrigin
    {
        Structural,
        User,
        Import,
        Reasoning,
        LegacyImport
    }

    internal readonly record struct NodeLinkProvenance
    {
        public Guid AssertionId { get; }
        public NodeLinkOrigin Origin { get; }
        public DateTimeOffset AssertedAt { get; }
        public string? Author { get; }
        public Guid? SourceTargetId { get; }
        public Guid? EvidenceTargetId { get; }
        public double? Confidence { get; }
        public DateTimeOffset? ValidFrom { get; }
        public DateTimeOffset? ValidUntil { get; }

        public NodeLinkProvenance(
            Guid assertionId,
            NodeLinkOrigin origin,
            DateTimeOffset assertedAt,
            string? author = null,
            Guid? sourceTargetId = null,
            Guid? evidenceTargetId = null,
            double? confidence = null,
            DateTimeOffset? validFrom = null,
            DateTimeOffset? validUntil = null)
        {
            if (assertionId == Guid.Empty)
            {
                throw new ArgumentException("Assertion identifiers cannot be Guid.Empty.", nameof(assertionId));
            }
            if (!Enum.IsDefined(origin))
            {
                throw new ArgumentOutOfRangeException(nameof(origin));
            }
            if (sourceTargetId == Guid.Empty)
            {
                throw new ArgumentException("Source target identifiers cannot be Guid.Empty.", nameof(sourceTargetId));
            }
            if (evidenceTargetId == Guid.Empty)
            {
                throw new ArgumentException("Evidence target identifiers cannot be Guid.Empty.", nameof(evidenceTargetId));
            }
            if (confidence.HasValue && (!double.IsFinite(confidence.Value) || confidence.Value < 0.0 || confidence.Value > 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be finite and in [0, 1].");
            }
            if (validFrom.HasValue && validUntil.HasValue && validFrom.Value > validUntil.Value)
            {
                throw new ArgumentException("The validity window start cannot be later than its end.");
            }

            AssertionId = assertionId;
            Origin = origin;
            AssertedAt = assertedAt;
            Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim();
            SourceTargetId = sourceTargetId;
            EvidenceTargetId = evidenceTargetId;
            Confidence = confidence;
            ValidFrom = validFrom;
            ValidUntil = validUntil;
        }

        internal static NodeLinkProvenance CreateForLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            string? label,
            NodeLinkOrigin origin,
            double weight)
        {
            if ((relationType is NodeRelationType.Synonym or NodeRelationType.Antonym) && sourceNodeId.CompareTo(targetNodeId) > 0)
            {
                (sourceNodeId, targetNodeId) = (targetNodeId, sourceNodeId);
            }
            byte[] labelBytes = Encoding.UTF8.GetBytes(label ?? string.Empty);
            byte[] identity = new byte[44 + labelBytes.Length];
            sourceNodeId.TryWriteBytes(identity.AsSpan(0, 16));
            targetNodeId.TryWriteBytes(identity.AsSpan(16, 16));
            BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(32, 4), (int)relationType);
            BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(36, 4), (int)origin);
            BinaryPrimitives.WriteInt32LittleEndian(identity.AsSpan(40, 4), labelBytes.Length);
            labelBytes.CopyTo(identity.AsSpan(44));
            byte[] hash = SHA256.HashData(identity);
            Guid assertionId = new Guid(hash.AsSpan(0, 16));
            if (assertionId == Guid.Empty)
            {
                assertionId = new Guid(hash.AsSpan(16, 16));
            }
            return new NodeLinkProvenance(assertionId, origin, DateTimeOffset.UtcNow, confidence: weight);
        }

        internal static NodeLinkProvenance CreateLegacy(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            string? label,
            double weight)
        {
            NodeLinkProvenance deterministic = CreateForLink(
                sourceNodeId, targetNodeId, relationType, label, NodeLinkOrigin.LegacyImport, weight);
            return new NodeLinkProvenance(deterministic.AssertionId, NodeLinkOrigin.LegacyImport,
                DateTimeOffset.UnixEpoch, confidence: weight);
        }

        internal NodeLinkProvenance WithConfidence(double weight)
        {
            return new NodeLinkProvenance(AssertionId, Origin, AssertedAt, Author, SourceTargetId,
                EvidenceTargetId, Confidence.HasValue ? weight : null, ValidFrom, ValidUntil);
        }
    }

    internal readonly struct NodeLink : IEquatable<NodeLink>
    {
        public NodeLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            double weight = 1.0,
            string? label = null,
            NodeLinkProvenance? provenance = null)
        {
            if (sourceNodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(sourceNodeId));
            }
            if (targetNodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(targetNodeId));
            }
            if (!double.IsFinite(weight) || weight < 0.0 || weight > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "The link confidence must be finite and in [0, 1].");
            }
            if (!Enum.IsDefined(typeof(NodeRelationType), relationType))
            {
                throw new ArgumentOutOfRangeException(nameof(relationType));
            }

            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            RelationType = relationType;
            Weight = weight;
            Label = NormalizeOptionalText(label);
            Provenance = provenance ?? NodeLinkProvenance.CreateForLink(
                sourceNodeId, targetNodeId, relationType, Label, NodeLinkOrigin.User, weight);
            if (Provenance.Confidence.HasValue && Provenance.Confidence.Value != weight)
            {
                throw new ArgumentException("Provenance confidence must match the NodeLink weight.", nameof(provenance));
            }
        }

        public Guid SourceNodeId { get; }
        public Guid TargetNodeId { get; }
        public NodeRelationType RelationType { get; }
        public double Weight { get; }
        public string? Label { get; }
        public NodeLinkProvenance Provenance { get; }

        internal NodeLink WithWeight(double weight)
        {
            return new NodeLink(SourceNodeId, TargetNodeId, RelationType, weight, Label, Provenance.WithConfidence(weight));
        }

        public bool Equals(NodeLink other)
        {
            return SourceNodeId == other.SourceNodeId && TargetNodeId == other.TargetNodeId &&
                   RelationType == other.RelationType && string.Equals(Label, other.Label, StringComparison.Ordinal) &&
                   Provenance.AssertionId == other.Provenance.AssertionId;
        }

        public override bool Equals(object? obj) => obj is NodeLink other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceNodeId, TargetNodeId, RelationType, Label, Provenance.AssertionId);
        public static bool operator ==(NodeLink left, NodeLink right) => left.Equals(right);
        public static bool operator !=(NodeLink left, NodeLink right) => !left.Equals(right);

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal readonly record struct NodeIdentityKey(NodeTargetKind TargetKind, Guid TargetId, string? Label)
    {
        internal static NodeIdentityKey Create(NodeTargetKind targetKind, Guid targetId, string? label)
        {
            if (!Enum.IsDefined(targetKind))
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            }
            if (targetId == Guid.Empty)
            {
                throw new ArgumentException("Target identifiers cannot be Guid.Empty.", nameof(targetId));
            }
            return new NodeIdentityKey(targetKind, targetId, NormalizeOptionalText(label));
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal sealed class Node : IEquatable<Node>
    {
        private Guid nodeID;
        private NodeTargetKind targetKind;
        private Guid targetID;
        private string? label;
        private int verifiedReferences;
        private int invalidReferences;
        private bool isTrusted;
        private bool isReadOnly;
        private Func<NodeIdentityKey, NodeIdentityKey, bool>? identityChanging;

        public Node(NodeTargetKind targetKind, Guid targetId, string? label = null)
            : this(Guid.NewGuid(), targetKind, targetId, label)
        {
        }

        public Node(Guid nodeId, NodeTargetKind targetKind, Guid targetId, string? label = null)
        {
            if (nodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(nodeId));
            }
            if (targetId == Guid.Empty)
            {
                throw new ArgumentException("Target identifiers cannot be Guid.Empty.", nameof(targetId));
            }
            if (!Enum.IsDefined(typeof(NodeTargetKind), targetKind))
            {
                throw new ArgumentOutOfRangeException(nameof(targetKind));
            }
            nodeID = nodeId;
            this.targetKind = targetKind;
            this.targetID = targetId;
            this.label = NormalizeOptionalText(label);
        }

        public Guid NodeID => GraphSynchronization.Execute(() => nodeID);
        public NodeTargetKind TargetKind => GraphSynchronization.Execute(() => targetKind);
        public Guid TargetID => GraphSynchronization.Execute(() => targetID);
        public string? Label => GraphSynchronization.Execute(() => label);
        public bool IsTrusted => GraphSynchronization.Execute(() => isTrusted);
        internal bool IsReadOnly => GraphSynchronization.Execute(() => isReadOnly);

        internal NodeIdentityKey GetIdentityKey()
        {
            return GraphSynchronization.Execute(() => NodeIdentityKey.Create(targetKind, targetID, label));
        }

        public Guid GetNodeID()
        {
            return NodeID;
        }

        public NodeTargetKind GetTargetKind()
        {
            return TargetKind;
        }

        public Guid GetTargetID()
        {
            return TargetID;
        }

        public string? GetLabel()
        {
            return Label;
        }

        public void SetLabel(string? value)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                if (isTrusted)
                {
                    throw new InvalidOperationException("A trusted node label is immutable; create a new labeled node instead.");
                }
                string? normalized = NormalizeOptionalText(value);
                NodeIdentityKey oldKey = NodeIdentityKey.Create(targetKind, targetID, label);
                NodeIdentityKey newKey = NodeIdentityKey.Create(targetKind, targetID, normalized);
                if (!oldKey.Equals(newKey) && identityChanging != null && !identityChanging(oldKey, newKey))
                {
                    throw new InvalidOperationException("Another node already has the requested target and label identity.");
                }
                label = normalized;
            });
        }

        internal void SetIdentityChangingHandler(Func<NodeIdentityKey, NodeIdentityKey, bool>? handler)
        {
            GraphSynchronization.Execute(() => identityChanging = handler);
        }

        public int GetVerifiedReferences()
        {
            return GraphSynchronization.Execute(() => verifiedReferences);
        }

        public int GetInvalidReferences()
        {
            return GraphSynchronization.Execute(() => invalidReferences);
        }

        public int GetNetTrustWeight()
        {
            return GraphSynchronization.Execute(() => checked(verifiedReferences - invalidReferences));
        }

        public void RegisterVerifiedReference(int count = 1)
        {
            ValidateCount(count);
            GraphSynchronization.Execute(() => { EnsureWritable(); verifiedReferences = checked(verifiedReferences + count); });
        }

        public void RegisterInvalidReference(int count = 1)
        {
            ValidateCount(count);
            GraphSynchronization.Execute(() => { EnsureWritable(); invalidReferences = checked(invalidReferences + count); });
        }

        internal void ReconcileReferenceCounts(int verified, int invalid)
        {
            if (verified < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(verified));
            }
            if (invalid < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(invalid));
            }
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                verifiedReferences = verified;
                invalidReferences = invalid;
                _ = checked(verifiedReferences - invalidReferences);
            });
        }

        internal void MarkTrusted()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); isTrusted = true; });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => { isTrusted = true; isReadOnly = true; });

        internal void MarkWritable() => GraphSynchronization.Execute(() => isReadOnly = false);

        private void EnsureWritable()
        {
            if (isReadOnly) throw new InvalidOperationException("Disk-paged nodes are read-only; materialize the disk store before mutation.");
        }

        private static void ValidateCount(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "The reference count must be positive.");
            }
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public bool Equals(Node? other)
        {
            return other != null && GetIdentityKey().Equals(other.GetIdentityKey());
        }

        public override bool Equals(object? obj) => obj is Node other && Equals(other);
        public override int GetHashCode() => GetIdentityKey().GetHashCode();
    }
}
