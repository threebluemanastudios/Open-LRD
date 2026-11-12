using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal sealed class NlpWorkbench : IDisposable
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;
        private NlpPipeline? pipeline;
        private NlpWorkbenchState? state;
        private bool disposed;

        public NlpWorkbench(LRD_Core core, NlpOptions? options = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            this.options = options ?? new NlpOptions();
            RefreshState();
        }

        public bool IsCoreInitialized => core.initialized;

        public bool HasSession => pipeline is not null;

        public Guid? PipelineID => pipeline?.PipelineID;

        public Guid? ConversationID => pipeline?.ConversationID;

        public NlpWorkbenchState? State => state;

        public bool StartNewConversation()
        {
            ThrowIfDisposed();

            if (!core.initialized)
                return false;

            NlpPipeline newPipeline = new NlpPipeline(core, options);
            NlpPipeline? previous = pipeline;
            pipeline = newPipeline;
            previous?.Dispose();
            RefreshState();
            return true;
        }

        public bool TryAttachConversation(Guid conversationID, out string? error)
        {
            ThrowIfDisposed();
            error = null;

            if (!core.initialized)
            {
                error = "Launch Core before attaching a conversation.";
                return false;
            }

            if (conversationID == Guid.Empty)
            {
                error = "Conversation ID cannot be empty.";
                return false;
            }

            if (!core.TryGetConversation(conversationID, out _))
            {
                error = $"Conversation '{conversationID}' was not found.";
                return false;
            }

            NlpPipeline newPipeline = new NlpPipeline(core, options, conversationID: conversationID);
            NlpPipeline? previous = pipeline;
            pipeline = newPipeline;
            previous?.Dispose();
            RefreshState();
            return true;
        }

        public bool ResetCurrentConversation()
        {
            ThrowIfDisposed();

            NlpPipeline? current = GetLivePipeline();

            if (current is null)
            {
                RefreshState();
                return false;
            }

            current.ResetConversation();
            RefreshState();
            return true;
        }

        public bool RemoveCurrentConversation()
        {
            ThrowIfDisposed();

            if (pipeline is null)
            {
                RefreshState();
                return false;
            }

            Guid conversationID = pipeline.ConversationID;
            bool removed = core.RemoveConversation(conversationID);
            pipeline.Dispose();
            pipeline = null;
            RefreshState();
            return removed;
        }

        public void ClearAllConversations()
        {
            ThrowIfDisposed();

            core.ClearConversations();
            pipeline?.Dispose();
            pipeline = null;
            RefreshState();
        }

        public NlpResult Ingest(
            string? text,
            Guid? sourceNodeID = null,
            Guid? topicNodeID = null)
        {
            ThrowIfDisposed();

            NlpPipeline current = RequireLivePipeline();
            NlpResult result = current.Ingest(text, sourceNodeID, topicNodeID);
            RefreshState();
            return result;
        }

        public NlpResult Ask(string? text)
        {
            ThrowIfDisposed();

            NlpPipeline current = RequireLivePipeline();
            NlpResult result = current.Ask(text);
            RefreshState();
            return result;
        }

        public NlpWorkbenchState RefreshState()
        {
            if (pipeline is not null && !IsPipelineLive(pipeline))
            {
                pipeline.Dispose();
                pipeline = null;
            }

            IReadOnlyDictionary<Guid, Web_Node>? graph = core.GetDictionary();
            int nodeCount = graph?.Count ?? 0;
            int linkCount = graph is null
                ? 0
                : graph.Values.Sum(node => core.GetLinks(node.ID).Count);

            if (!core.initialized || pipeline is null)
            {
                state = new NlpWorkbenchState(
                    core.initialized,
                    false,
                    null,
                    null,
                    0,
                    null,
                    null,
                    null,
                    null,
                    null,
                    nodeCount,
                    linkCount,
                    0,
                    0,
                    Array.Empty<ConversationMention>(),
                    Array.Empty<SessionProvenanceEntry>());
                return state;
            }

            ConversationContext conversation = pipeline.Conversation;

            state = new NlpWorkbenchState(
                true,
                true,
                pipeline.PipelineID,
                pipeline.ConversationID,
                conversation.TurnIndex,
                conversation.FocusNodeID,
                conversation.LastSubjectNodeID,
                conversation.LastObjectNodeID,
                conversation.LastQualifierNodeID,
                conversation.LastAnswerNodeID,
                nodeCount,
                linkCount,
                conversation.Mentions.Count,
                conversation.Provenance.Count,
                conversation.Mentions.ToArray(),
                conversation.Provenance.ToArray());
            return state;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            pipeline?.Dispose();
            pipeline = null;
            RefreshState();
        }

        private NlpPipeline RequireLivePipeline()
        {
            NlpPipeline? current = GetLivePipeline();

            if (current is null)
            {
                RefreshState();
                throw new InvalidOperationException("A current NLP conversation is required.");
            }

            return current;
        }

        private NlpPipeline? GetLivePipeline()
        {
            if (pipeline is null)
                return null;

            if (!IsPipelineLive(pipeline))
            {
                pipeline.Dispose();
                pipeline = null;
                return null;
            }

            return pipeline;
        }

        private bool IsPipelineLive(NlpPipeline candidate)
        {
            if (!core.initialized)
                return false;

            if (!core.TryGetPipelineConversation(candidate.PipelineID, out Guid conversationID))
                return false;

            return conversationID == candidate.ConversationID
                && core.TryGetConversation(conversationID, out _);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NlpWorkbench));
        }
    }
}
