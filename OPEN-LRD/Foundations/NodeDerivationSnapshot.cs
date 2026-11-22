using System;
using System.Collections.Generic;

namespace OPEN_LRD.Foundations
{
    internal readonly record struct NodeDerivationLink(
        Guid AssertionId,
        NodeRelationType RelationType,
        double Weight,
        Guid OtherNodeId);

    internal sealed class NodeDerivationSnapshot
    {
        internal NodeDerivationSnapshot(
            Guid nodeId,
            Guid targetId,
            NodeTargetKind targetKind,
            bool isTrusted,
            NodeRelationType? derivedClass,
            double weight,
            double trustRating,
            string? summary,
            IReadOnlyList<NodeDerivationLink> links,
            IReadOnlyList<Guid> neighbors)
        {
            if (nodeId == Guid.Empty) throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(nodeId));
            if (targetId == Guid.Empty) throw new ArgumentException("Target identifiers cannot be Guid.Empty.", nameof(targetId));
            if (!Enum.IsDefined(targetKind)) throw new ArgumentOutOfRangeException(nameof(targetKind));
            if (derivedClass.HasValue && !Enum.IsDefined(derivedClass.Value)) throw new ArgumentOutOfRangeException(nameof(derivedClass));
            if (!double.IsFinite(weight) || weight < 0.0 || weight > 1.0)
                throw new ArgumentOutOfRangeException(nameof(weight), "Node weight must be finite and in [0, 1].");
            if (!double.IsFinite(trustRating) || trustRating < 0.0 || trustRating > 1.0)
                throw new ArgumentOutOfRangeException(nameof(trustRating), "Trust rating must be finite and in [0, 1].");

            NodeId = nodeId;
            TargetId = targetId;
            TargetKind = targetKind;
            IsTrusted = isTrusted;
            DerivedClass = derivedClass;
            Weight = weight;
            TrustRating = trustRating;
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
            Links = links ?? throw new ArgumentNullException(nameof(links));
            Neighbors = neighbors ?? throw new ArgumentNullException(nameof(neighbors));
        }

        internal Guid NodeId { get; }
        internal Guid TargetId { get; }
        internal NodeTargetKind TargetKind { get; }
        internal bool IsTrusted { get; }
        internal NodeRelationType? DerivedClass { get; }
        internal double Weight { get; }
        internal double TrustRating { get; }
        internal string? Summary { get; }
        internal IReadOnlyList<NodeDerivationLink> Links { get; }
        internal IReadOnlyList<Guid> Neighbors { get; }
    }
}
