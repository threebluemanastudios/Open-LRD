using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    internal enum PronounPerson
    {
        Unknown,
        First,
        Second,
        Third
    }

    internal enum PronounNumber
    {
        Unknown,
        Singular,
        Plural
    }

    internal enum PronounCase
    {
        Unknown,
        Subject,
        Object,
        Possessive
    }

    internal sealed record PronounInfo(
        string Surface,
        string Normalized,
        PronounPerson Person,
        PronounNumber Number,
        PronounCase Case,
        bool IsReflexive,
        bool IsIndefinite,
        bool IsResolvable);

    internal sealed record ConversationMention(
        Guid NodeID,
        string Label,
        string SurfaceText,
        NodeTypes NodeType,
        FrameRole Role,
        int TurnIndex,
        int Sequence,
        bool FromAnswer);

    internal sealed record PronounResolution(
        Guid? NodeID,
        string? Label,
        NodeTypes NodeType,
        IReadOnlyList<Guid> CandidateNodeIDs,
        bool IsAmbiguous,
        bool IsSupported,
        IReadOnlyList<string> Diagnostics);

    internal sealed record SessionProvenanceEntry(
        int Sequence,
        int TurnIndex,
        int? SentenceIndex,
        string SourceText,
        ReasoningStatus Status,
        string? RelationName,
        LinkClass LinkClass,
        int? ForwardLinkTypeId,
        int? InverseLinkTypeId,
        Guid? SubjectNodeID,
        Guid? ObjectNodeID,
        Guid? QualifierNodeID,
        Guid? TrustSourceNodeID,
        Guid? TopicNodeID,
        IReadOnlyList<Guid> NodeIDs,
        IReadOnlyList<Guid> TemporaryNodeIDs,
        bool GraphMutated,
        bool IsUsableForAnswer,
        IReadOnlyList<string> Diagnostics);

    internal sealed class ConversationContext
    {
        private readonly List<ConversationMention> mentions = new List<ConversationMention>();
        private readonly List<SessionProvenanceEntry> provenance = new List<SessionProvenanceEntry>();
        private readonly NlpOptions options;

        public ConversationContext(Guid conversationID, NlpOptions? options = null)
        {
            if (conversationID == Guid.Empty)
                throw new ArgumentException("Conversation ID cannot be empty.", nameof(conversationID));

            ConversationID = conversationID;
            this.options = options ?? new NlpOptions();
        }

        public Guid ConversationID { get; }

        internal NlpOptions Options => options;

        public int TurnIndex { get; private set; }

        public int Sequence { get; private set; }

        public Guid? FocusNodeID { get; private set; }

        public Guid? LastSubjectNodeID { get; private set; }

        public Guid? LastObjectNodeID { get; private set; }

        public Guid? LastQualifierNodeID { get; private set; }

        public Guid? LastAnswerNodeID { get; private set; }

        public Guid? LastObjectOrQualifierNodeID => LastObjectNodeID ?? LastQualifierNodeID;

        public IReadOnlyList<ConversationMention> Mentions => mentions.AsReadOnly();

        public IReadOnlyList<SessionProvenanceEntry> Provenance => provenance.AsReadOnly();

        public void BeginTurn()
        {
            TurnIndex++;
        }

        public void RecordMention(ConversationMention mention)
        {
            if (!options.EnableConversationContext || mention.NodeID == Guid.Empty)
                return;

            mentions.RemoveAll(existing => existing.NodeID == mention.NodeID);

            ConversationMention recorded = mention with { Sequence = ++Sequence };
            mentions.Add(recorded);

            int limit = Math.Max(1, options.MaxConversationMentions);

            while (mentions.Count > limit)
                mentions.RemoveAt(0);

            switch (recorded.Role)
            {
                case FrameRole.Subject:
                    LastSubjectNodeID = recorded.NodeID;
                    break;

                case FrameRole.Object:
                    LastObjectNodeID = recorded.NodeID;
                    break;

                case FrameRole.Location:
                case FrameRole.Time:
                case FrameRole.Qualifier:
                    LastQualifierNodeID = recorded.NodeID;
                    break;
            }
        }

        public void RecordMentions(IEnumerable<ConversationMention> newMentions)
        {
            ArgumentNullException.ThrowIfNull(newMentions);

            foreach (ConversationMention mention in newMentions)
                RecordMention(mention);
        }

        public void SetFocus(Guid? nodeID, FrameRole role, bool fromAnswer)
        {
            if (!options.EnableConversationContext)
                return;

            if (nodeID.HasValue && nodeID.Value == Guid.Empty)
                return;

            FocusNodeID = nodeID;

            if (!nodeID.HasValue)
                return;

            switch (role)
            {
                case FrameRole.Subject:
                    LastSubjectNodeID = nodeID.Value;
                    break;

                case FrameRole.Object:
                    LastObjectNodeID = nodeID.Value;
                    break;

                case FrameRole.Location:
                case FrameRole.Time:
                case FrameRole.Qualifier:
                    LastQualifierNodeID = nodeID.Value;
                    break;
            }

            if (fromAnswer)
                LastAnswerNodeID = nodeID.Value;
        }

        public PronounResolution ResolvePronoun(
            string surfaceText,
            NodeTypes expectedType,
            FrameRole role,
            LRD_Core core,
            NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            if (!Lexicon.TryGetPronounInfo(surfaceText, out PronounInfo info))
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    false,
                    new[] { $"Value '{surfaceText}' is not a recognized pronoun." });
            }

            if (info.Person == PronounPerson.First)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    false,
                    new[] { "First-person pronouns are resolved to the configured speaker node." });
            }

            if (info.Person == PronounPerson.Second)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    false,
                    new[] { "Second-person pronouns are not supported in this session." });
            }

            if (info.IsReflexive)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    false,
                    new[] { $"Reflexive pronoun '{info.Surface}' is not supported in this session." });
            }

            if (info.IsIndefinite)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    false,
                    new[] { $"Indefinite pronoun '{info.Surface}' is not supported in this session." });
            }

            int recencyWindow = Math.Max(1, options.PronounRecencyWindow);
            List<ConversationMention> recent = new List<ConversationMention>();

            foreach (ConversationMention mention in mentions)
            {
                int turnDistance = Math.Max(0, TurnIndex - mention.TurnIndex);

                if (turnDistance > recencyWindow)
                    continue;

                if (!core.TryGetNode(mention.NodeID, out Web_Node? node) || node is null)
                    continue;

                recent.Add(mention);
            }

            List<ConversationMention> typeMatches = recent
                .Where(mention => mention.NodeType == expectedType)
                .ToList();
            List<ConversationMention> pool = typeMatches.Count > 0 ? typeMatches : recent;

            List<(ConversationMention Mention, double Score)> candidates =
                new List<(ConversationMention Mention, double Score)>(pool.Count);

            foreach (ConversationMention mention in pool)
            {
                int turnDistance = Math.Max(0, TurnIndex - mention.TurnIndex);
                double score = 0.20 * (1.0 / (1 + turnDistance));

                if (mention.Role == role)
                    score += 0.50;

                if (mention.NodeID == FocusNodeID)
                    score += 0.20;

                if (mention.FromAnswer)
                    score += 0.10;

                if (mention.NodeType == expectedType)
                    score += 0.10;

                candidates.Add((mention, score));
            }

            candidates = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Mention.TurnIndex)
                .ThenByDescending(candidate => candidate.Mention.Sequence)
                .ThenBy(candidate => candidate.Mention.NodeID.ToString(), StringComparer.Ordinal)
                .Take(Math.Max(1, options.MaxPronounCandidates))
                .ToList();

            if (candidates.Count == 0)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    true,
                    new[] { $"Pronoun '{info.Surface}' has no clear antecedent in this session." });
            }

            (ConversationMention Mention, double Score) top = candidates[0];

            if (candidates.Count >= 2)
            {
                (ConversationMention Mention, double Score) second = candidates[1];

                if (top.Mention.NodeID != second.Mention.NodeID
                    && top.Score - second.Score <= options.PronounAmbiguityMargin)
                {
                    List<Guid> tied = candidates
                        .Where(candidate => top.Score - candidate.Score <= options.PronounAmbiguityMargin)
                        .Select(candidate => candidate.Mention.NodeID)
                        .Distinct()
                        .ToList();

                    return new PronounResolution(
                        null,
                        null,
                        expectedType,
                        tied,
                        true,
                        true,
                        new[] { $"Pronoun '{info.Surface}' matches multiple recent antecedents." });
                }
            }

            if (!core.TryGetNode(top.Mention.NodeID, out Web_Node? resolvedNode) || resolvedNode is null)
            {
                return new PronounResolution(
                    null,
                    null,
                    expectedType,
                    Array.Empty<Guid>(),
                    false,
                    true,
                    new[] { $"Pronoun '{info.Surface}' has no clear antecedent in this session." });
            }

            string label = ResolveLabel(resolvedNode, top.Mention.Label);

            return new PronounResolution(
                top.Mention.NodeID,
                label,
                resolvedNode.Get_NodeType(),
                new[] { top.Mention.NodeID },
                false,
                true,
                new[] { $"Pronoun '{info.Surface}' resolved to '{label}'." });
        }

        public SessionProvenanceEntry RecordProvenance(SessionProvenanceEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (!options.EnableSessionProvenance)
                return entry;

            int sequence = ++Sequence;
            SessionProvenanceEntry recorded = entry with { Sequence = sequence };
            provenance.Add(recorded);

            int limit = Math.Max(1, options.MaxSessionProvenanceEntries);

            while (provenance.Count > limit)
                provenance.RemoveAt(0);

            return recorded;
        }

        public IReadOnlyList<SessionProvenanceEntry> FindProvenance(GraphPath? path)
        {
            if (path is null || path.NodeIDs.Count < 2 || path.Links.Count == 0)
                return Array.Empty<SessionProvenanceEntry>();

            return provenance
                .Where(entry => entry.IsUsableForAnswer && PathMatches(entry, path))
                .ToList();
        }

        internal void RestoreState(
            int turnIndex,
            int sequence,
            Guid? focusNodeID,
            Guid? lastSubjectNodeID,
            Guid? lastObjectNodeID,
            Guid? lastQualifierNodeID,
            Guid? lastAnswerNodeID,
            IEnumerable<ConversationMention> restoredMentions,
            IEnumerable<SessionProvenanceEntry> restoredProvenance)
        {
            ArgumentNullException.ThrowIfNull(restoredMentions);
            ArgumentNullException.ThrowIfNull(restoredProvenance);

            if (turnIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(turnIndex), "Turn index cannot be negative.");

            if (sequence < 0)
                throw new ArgumentOutOfRangeException(nameof(sequence), "Sequence cannot be negative.");

            List<ConversationMention> mentionList = restoredMentions.ToList();
            List<SessionProvenanceEntry> provenanceList = restoredProvenance.ToList();

            int mentionLimit = Math.Max(1, options.MaxConversationMentions);
            int provenanceLimit = Math.Max(1, options.MaxSessionProvenanceEntries);

            if (mentionList.Count > mentionLimit)
                throw new InvalidOperationException(
                    $"Conversation mentions exceed the configured limit of {mentionLimit}.");

            if (provenanceList.Count > provenanceLimit)
                throw new InvalidOperationException(
                    $"Conversation provenance exceeds the configured limit of {provenanceLimit}.");

            if (mentionList.Any(mention => mention.Sequence <= 0))
                throw new InvalidOperationException("Conversation mention sequences must be positive.");

            if (provenanceList.Any(entry => entry.Sequence <= 0))
                throw new InvalidOperationException("Conversation provenance sequences must be positive.");

            int highestMentionSequence = mentionList.Count == 0
                ? 0
                : mentionList.Max(mention => mention.Sequence);
            int highestProvenanceSequence = provenanceList.Count == 0
                ? 0
                : provenanceList.Max(entry => entry.Sequence);

            if (sequence < highestMentionSequence || sequence < highestProvenanceSequence)
                throw new InvalidOperationException(
                    "Conversation sequence must be at least as large as every restored entry sequence.");

            TurnIndex = turnIndex;
            Sequence = sequence;
            FocusNodeID = focusNodeID;
            LastSubjectNodeID = lastSubjectNodeID;
            LastObjectNodeID = lastObjectNodeID;
            LastQualifierNodeID = lastQualifierNodeID;
            LastAnswerNodeID = lastAnswerNodeID;
            mentions.Clear();
            mentions.AddRange(mentionList);
            provenance.Clear();
            provenance.AddRange(provenanceList);
        }

        public void Clear()
        {
            TurnIndex = 0;
            Sequence = 0;
            FocusNodeID = null;
            LastSubjectNodeID = null;
            LastObjectNodeID = null;
            LastQualifierNodeID = null;
            LastAnswerNodeID = null;
            mentions.Clear();
            provenance.Clear();
        }

        public void Reset() => Clear();

        private static bool PathMatches(SessionProvenanceEntry entry, GraphPath path)
        {
            if (entry.ForwardLinkTypeId is null || entry.InverseLinkTypeId is null)
                return false;

            if (entry.SubjectNodeID is null || entry.ObjectNodeID is null)
                return false;

            for (int index = 0; index < path.Links.Count && index + 1 < path.NodeIDs.Count; index++)
            {
                Link link = path.Links[index];
                bool idsMatch =
                    entry.ForwardLinkTypeId.Value == link.ForwardLink.Id
                    && entry.InverseLinkTypeId.Value == link.InverseLink.Id;

                if (!idsMatch)
                    continue;

                Guid first = path.NodeIDs[index];
                Guid second = path.NodeIDs[index + 1];

                if ((entry.SubjectNodeID.Value == first && entry.ObjectNodeID.Value == second)
                    || (entry.SubjectNodeID.Value == second && entry.ObjectNodeID.Value == first))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveLabel(Web_Node node, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            string? alias = node.Aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return alias ?? fallback;
        }
    }
}
