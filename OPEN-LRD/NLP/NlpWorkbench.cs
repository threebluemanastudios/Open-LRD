using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    /// <summary>Provides UI-independent lifecycle and operation access to NLP pipeline sessions.</summary>
    internal sealed class NlpWorkbench : IDisposable
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;
        private NlpPipeline? pipeline;
        private NlpWorkbenchState? state;
        private bool disposed;

        /// <summary>Creates an NLP workbench bound to the supplied core graph and options.</summary>
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

        /// <summary>Creates and attaches a new NLP pipeline conversation when the core graph is initialized.</summary>
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

        /// <summary>Attempts to attach the workbench to an existing conversation managed by the core graph.</summary>
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

        /// <summary>Clears turn, focus, mention, and provenance state on the active conversation.</summary>
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

        /// <summary>Detaches and removes the active conversation from the core graph.</summary>
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

        /// <summary>Removes all conversations from the core graph and detaches any active pipeline.</summary>
        public void ClearAllConversations()
        {
            ThrowIfDisposed();

            core.ClearConversations();
            pipeline?.Dispose();
            pipeline = null;
            RefreshState();
        }

        /// <summary>Ingests natural-language text through the active conversation pipeline and refreshes workbench state.</summary>
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

        /// <summary>Answers a natural-language question through the active conversation pipeline and refreshes workbench state.</summary>
        public NlpResult Ask(string? text)
        {
            ThrowIfDisposed();

            NlpPipeline current = RequireLivePipeline();
            NlpResult result = current.Ask(text);
            RefreshState();
            return result;
        }

        /// <summary>Rebuilds and returns the current read-only snapshot of graph and conversation state.</summary>
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

        /// <summary>Disposes the active NLP pipeline and refreshes the workbench state.</summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            pipeline?.Dispose();
            pipeline = null;
            RefreshState();
        }

        /// <summary>Returns the active live pipeline or throws when no conversation session is attached.</summary>
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

        /// <summary>Returns the current pipeline when its conversation remains registered in the core graph.</summary>
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

        /// <summary>Checks whether a pipeline and its conversation are still active in the core graph.</summary>
        private bool IsPipelineLive(NlpPipeline candidate)
        {
            if (!core.initialized)
                return false;

            if (!core.TryGetPipelineConversation(candidate.PipelineID, out Guid conversationID))
                return false;

            return conversationID == candidate.ConversationID
                && core.TryGetConversation(conversationID, out _);
        }

        /// <summary>Throws an <see cref="ObjectDisposedException"/> if this workbench has been disposed.</summary>
        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(NlpWorkbench));
        }
    }
}
