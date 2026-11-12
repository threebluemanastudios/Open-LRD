using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.NLP
{
    internal static class ChainBuilder
    {
        public static ChainBuildResult Build(
            RelationFrame frame,
            EntityResolver resolver,
            NlpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(frame);
            ArgumentNullException.ThrowIfNull(resolver);

            options ??= new NlpOptions();

            List<LinkedList<Chain_Node>> chains = new List<LinkedList<Chain_Node>>();
            List<string> diagnostics = new List<string>();

            FrameArgument? subjectArgument = frame.Arguments.FirstOrDefault(
                argument => argument.Role == FrameRole.Subject);
            FrameArgument? objectArgument = frame.Arguments.FirstOrDefault(
                argument => argument.Role is FrameRole.Object);

            if (subjectArgument is null || objectArgument is null)
            {
                diagnostics.Add($"Relation '{frame.RelationName}' is missing a subject or object.");
                return new ChainBuildResult(chains, diagnostics);
            }

            EntityResolution subject = resolver.Resolve(subjectArgument);
            EntityResolution target = resolver.Resolve(objectArgument);
            diagnostics.AddRange(subject.Diagnostics);
            diagnostics.AddRange(target.Diagnostics);

            if (subject.IsPronoun || target.IsPronoun)
                return new ChainBuildResult(chains, diagnostics);

            if (subject.IsAmbiguous || target.IsAmbiguous)
                return new ChainBuildResult(chains, diagnostics);

            Guid subjectID = subject.ResolvedNodeID ?? resolver.CreateNodeId();
            Guid targetID = target.ResolvedNodeID ?? resolver.CreateNodeId();

            if (subjectID == targetID)
            {
                diagnostics.Add(
                    $"Relation '{frame.RelationName}' resolves both endpoints to the same node.");
                return new ChainBuildResult(chains, diagnostics);
            }

            RelationLinkType relation = RelationRegistry.GetOrRegister(frame.RelationName);
            Chain_Node subjectNode = CreateNode(subjectID, subject, options);
            Chain_Node targetNode = CreateNode(targetID, target, options);
            Link link = new Link(
                subjectID,
                targetID,
                relation.Forward.Id,
                relation.Inverse.Id,
                frame.LinkClass);

            subjectNode.Set_LinkNext(link);
            targetNode.Set_LinkPrevious(link);
            chains.Add(CreateChain(subjectNode, targetNode));

            if (chains.Count >= options.MaxChainsPerText)
            {
                diagnostics.Add(
                    $"The maximum of {options.MaxChainsPerText} chains per text was reached.");
                return new ChainBuildResult(chains, diagnostics);
            }

            HashSet<string> qualifierKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                QualifierKey(subjectID, frame.RelationName, targetID, frame.LinkClass)
            };

            foreach (FrameArgument qualifierArgument in frame.Arguments.Where(
                argument => argument.Role is FrameRole.Location
                    or FrameRole.Time
                    or FrameRole.Qualifier))
            {
                EntityResolution qualifier = resolver.Resolve(qualifierArgument);
                diagnostics.AddRange(qualifier.Diagnostics);

                if (qualifier.IsPronoun || qualifier.IsAmbiguous)
                    continue;

                Guid qualifierID = qualifier.ResolvedNodeID ?? resolver.CreateNodeId();

                if (qualifierID == subjectID)
                {
                    diagnostics.Add(
                        $"Qualifier '{qualifier.Label}' would create a self-loop and was skipped.");
                    continue;
                }

                string relationName = string.IsNullOrWhiteSpace(qualifierArgument.Preposition)
                    ? "related_to"
                    : RelationRegistry.Normalize(qualifierArgument.Preposition);
                LinkClass linkClass = qualifierArgument.Role switch
                {
                    FrameRole.Location => LinkClass.spatial,
                    FrameRole.Time => LinkClass.temporal,
                    _ => LinkClass.basic
                };

                if (!qualifierKeys.Add(QualifierKey(subjectID, relationName, qualifierID, linkClass)))
                    continue;

                RelationLinkType qualifierRelation = RelationRegistry.GetOrRegister(relationName);
                Chain_Node qualifierSubjectNode = CreateNode(subjectID, subject, options);
                Chain_Node qualifierNode = CreateNode(qualifierID, qualifier, options);
                Link qualifierLink = new Link(
                    subjectID,
                    qualifierID,
                    qualifierRelation.Forward.Id,
                    qualifierRelation.Inverse.Id,
                    linkClass);

                qualifierSubjectNode.Set_LinkNext(qualifierLink);
                qualifierNode.Set_LinkPrevious(qualifierLink);
                chains.Add(CreateChain(qualifierSubjectNode, qualifierNode));

                if (chains.Count >= options.MaxChainsPerText)
                {
                    diagnostics.Add(
                        $"The maximum of {options.MaxChainsPerText} chains per text was reached.");
                    break;
                }
            }

            return new ChainBuildResult(chains, diagnostics);
        }

        private static Chain_Node CreateNode(
            Guid nodeID,
            EntityResolution resolution,
            NlpOptions options)
        {
            Chain_Node node = new Chain_Node(
                nodeID,
                resolution.NodeType,
                options.TemporaryNodeTimeToLive);

            node.Set_Label(resolution.Label);

            foreach (string alias in resolution.Aliases)
                node.Add_Alias(alias);

            return node;
        }

        private static LinkedList<Chain_Node> CreateChain(
            Chain_Node subject,
            Chain_Node target)
        {
            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();
            chain.AddLast(subject);
            chain.AddLast(target);
            return chain;
        }

        private static string QualifierKey(
            Guid subjectID,
            string relationName,
            Guid targetID,
            LinkClass linkClass)
        {
            return string.Join("|", subjectID, relationName, targetID, linkClass.Id);
        }
    }
}
