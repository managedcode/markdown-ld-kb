using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF;
using VDS.RDF.Query;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private Task<KnowledgeGraphSchemaSearchMatch[]> ReadLocalSchemaMatchesAsync(
        KnowledgeGraphSchemaSearchPlan plan,
        CancellationToken cancellationToken)
    {
        var query = ParseReadOnlyQuery(plan.EvidenceSparql, SparqlSafety.IsSelectQuery, ExecuteSelectRequiresSelectQueryMessage);
        return Task.Run(() => ReadLocalSchemaMatches(query, plan, cancellationToken), cancellationToken);
    }

    private KnowledgeGraphSchemaSearchMatch[] ReadLocalSchemaMatches(
        SparqlQuery query,
        KnowledgeGraphSchemaSearchPlan plan,
        CancellationToken cancellationToken)
    {
        _graphLock.EnterReadLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = (SparqlResultSet)ProcessQueryUnderReadLock(query);
            var matches = BuildPrimaryMatches(ToResult(rows), plan).Take(plan.Profile.MaxResults).ToArray();
            if (matches.Length == 0)
            {
                return matches;
            }

            var sources = new SchemaSearchSourceProjection(_graph, rows, cancellationToken);
            for (var index = 0; index < matches.Length; index++)
            {
                matches[index] = sources.Attach(matches[index]);
            }

            return matches;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private sealed class SchemaSearchSourceProjection
    {
        private readonly Graph _graph;
        private readonly CancellationToken _cancellationToken;
        private readonly INode _sourcePredicate;
        private readonly INode[] _labelPredicates;
        private readonly Dictionary<string, INode> _evidenceNodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, KnowledgeGraphSchemaSearchSourceContext[]> _sourcesByNode = new(StringComparer.Ordinal);
        private readonly Dictionary<INode, string?> _labelsBySource = [];
        private readonly Dictionary<(string Subject, string Related), KnowledgeGraphSchemaSearchSourceContext[]> _relatedSources = [];

        public SchemaSearchSourceProjection(Graph graph, SparqlResultSet rows, CancellationToken cancellationToken)
        {
            _graph = graph;
            _cancellationToken = cancellationToken;
            _sourcePredicate = graph.CreateUriNode(ProvWasDerivedFromUri);
            _labelPredicates = [graph.CreateUriNode(SchemaNameUri), graph.CreateUriNode(RdfsLabelUri), graph.CreateUriNode(SkosPrefLabelUri)];
            foreach (var row in rows)
            {
                AddEvidenceNode(row, SchemaSearchSubjectKey);
                AddEvidenceNode(row, SchemaSearchRelatedNodeKey);
            }
        }

        public KnowledgeGraphSchemaSearchMatch Attach(KnowledgeGraphSchemaSearchMatch match) => match with
        {
            Evidence = match.Evidence.Select(evidence => evidence with
            {
                SourceContexts = ReadContexts(match.NodeId, evidence.RelatedNodeId),
            }).ToArray(),
        };

        private void AddEvidenceNode(ISparqlResult row, string variable)
        {
            if (row.TryGetBoundValue(variable, out var node) && node is not null)
            {
                _evidenceNodes.TryAdd(RenderNode(node), node);
            }
        }

        private KnowledgeGraphSchemaSearchSourceContext[] ReadContexts(string subject, string? related)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var primary = ReadNodeSources(subject);
            if (related is null || related == subject)
            {
                return primary;
            }

            var key = (subject, related);
            if (!_relatedSources.TryGetValue(key, out var combined))
            {
                combined = primary.Concat(ReadNodeSources(related))
                    .DistinctBy(static source => source.SourceId, StringComparer.Ordinal)
                    .OrderBy(static source => source.SourceId, StringComparer.Ordinal)
                    .ToArray();
                _relatedSources.Add(key, combined);
            }

            return combined;
        }

        private KnowledgeGraphSchemaSearchSourceContext[] ReadNodeSources(string id)
        {
            if (!_sourcesByNode.TryGetValue(id, out var sources))
            {
                sources = _graph.GetTriplesWithSubjectPredicate(_evidenceNodes[id], _sourcePredicate)
                    .Select(static triple => triple.Object)
                    .Select(CreateSourceContext)
                    .Where(static source => !string.IsNullOrWhiteSpace(source.SourceId))
                    .DistinctBy(static source => source.SourceId, StringComparer.Ordinal)
                    .OrderBy(static source => source.SourceId, StringComparer.Ordinal)
                    .ToArray();
                _sourcesByNode.Add(id, sources);
            }

            return sources;
        }

        private KnowledgeGraphSchemaSearchSourceContext CreateSourceContext(INode source)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!_labelsBySource.TryGetValue(source, out var label))
            {
                label = ReadPreferredLabel(source);
                _labelsBySource.Add(source, label);
            }

            return new KnowledgeGraphSchemaSearchSourceContext(RenderNode(source), label);
        }

        private string? ReadPreferredLabel(INode source)
        {
            foreach (var predicate in _labelPredicates)
            {
                var labels = _graph.GetTriplesWithSubjectPredicate(source, predicate);
                if (labels.Any())
                {
                    return labels.Select(static triple => RenderNode(triple.Object))
                        .Where(static label => !string.IsNullOrWhiteSpace(label))
                        .Min(StringComparer.Ordinal);
                }
            }

            return null;
        }
    }
}
