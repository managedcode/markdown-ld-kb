using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphProjectionOptions(IReadOnlySet<string> AllowedSubjectIds)
{
    public IReadOnlySet<string>? AllowedResourceObjectIds { get; init; }
    public IReadOnlySet<string>? AllowedPredicateIds { get; init; }
}

public sealed partial class KnowledgeGraph
{
    /// <summary>Independent RDF projection. Literal values require an allowed subject; resource objects require explicit membership.</summary>
    public KnowledgeGraph Project(KnowledgeGraphProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.AllowedSubjectIds);
        var objects = options.AllowedResourceObjectIds ?? options.AllowedSubjectIds;
        var projected = new Graph();
        _graphLock.EnterReadLock();
        try
        {
            foreach (var triple in _graph.Triples)
            {
                if (!options.AllowedSubjectIds.Contains(RenderGraphNodeId(triple.Subject))
                    || (options.AllowedPredicateIds is { } predicates && !predicates.Contains(RenderGraphNodeId(triple.Predicate)))
                    || (triple.Object is not ILiteralNode && !objects.Contains(RenderGraphNodeId(triple.Object))))
                {
                    continue;
                }
                projected.Assert(triple);
            }
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
        return new KnowledgeGraph(projected);
    }
}
