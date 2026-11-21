using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
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

    internal readonly record struct NodeLink
    {
        public NodeLink(
            Guid sourceNodeId,
            Guid targetNodeId,
            NodeRelationType relationType,
            double weight = 1.0,
            string? label = null)
        {
            if (sourceNodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(sourceNodeId));
            }
            if (targetNodeId == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", nameof(targetNodeId));
            }
            if (!double.IsFinite(weight) || weight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "The link weight must be finite and greater than zero.");
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
        }

        public Guid SourceNodeId { get; }
        public Guid TargetNodeId { get; }
        public NodeRelationType RelationType { get; }
        public double Weight { get; }
        public string? Label { get; }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    internal sealed class Node
    {
        private Guid nodeID;
        private NodeTargetKind targetKind;
        private Guid targetID;
        private string? label;
        private int verifiedReferences;
        private int invalidReferences;
        private bool isTrusted;
        private bool isReadOnly;

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
            GraphSynchronization.Execute(() => { EnsureWritable(); label = NormalizeOptionalText(value); });
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
            return GraphSynchronization.Execute(() => verifiedReferences - invalidReferences);
        }

        public void RegisterVerifiedReference(int count = 1)
        {
            ValidateCount(count);
            GraphSynchronization.Execute(() => { EnsureWritable(); verifiedReferences += count; });
        }

        public void RegisterInvalidReference(int count = 1)
        {
            ValidateCount(count);
            GraphSynchronization.Execute(() => { EnsureWritable(); invalidReferences += count; });
        }

        internal void MarkTrusted()
        {
            GraphSynchronization.Execute(() => { EnsureWritable(); isTrusted = true; });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => isReadOnly = true);

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
    }
}
