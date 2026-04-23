using VDS.RDF.Query;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Patterns;

using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeGraphFederatedQueryProcessor(
    ISparqlDataset dataset,
    KnowledgeGraphFederatedLocalServiceRegistry? localServices,
    Action<LeviathanQueryOptions>? configureOptions)
    : LeviathanQueryProcessor(dataset, configureOptions)
{
    private readonly KnowledgeGraphFederatedLocalServiceRegistry? _localServices = localServices;

    public override BaseMultiset ProcessService(Service service, SparqlEvaluationContext context)
    {
        if (_localServices is null ||
            context is null ||
            !_localServices.TryResolve(service.EndpointSpecifier, context, out var localClient))
        {
            return base.ProcessService(service, context);
        }

        try
        {
            context.OutputMultiset = new Multiset();
            foreach (var remoteQuery in CreateRemoteQueries(context, service.Pattern, GetBindings(context, service.Pattern)))
            {
                using var cancellation = new CancellationTokenSource();
                var remainingTimeout = context.RemainingTimeout;
                if (remainingTimeout > 0)
                {
                    cancellation.CancelAfter(TimeSpan.FromMilliseconds(remainingTimeout));
                }

                var task = localClient.ExecuteResultSetAsync(remoteQuery.ToString(), cancellation.Token);
                task.Wait(cancellation.Token);
                context.CheckTimeout();

                foreach (var item in task.Result)
                {
                    var set = new Set();
                    foreach (var variable in item.Variables)
                    {
                        set.Add(variable, item[variable]);
                    }

                    context.OutputMultiset.Add(set);
                }
            }

            return context.OutputMultiset;
        }
        catch (Exception cause)
        {
            if (service.Silent)
            {
                if (context.OutputMultiset.IsEmpty)
                {
                    var set = new Set();
                    foreach (var variable in service.Pattern.Variables.Distinct())
                    {
                        set.Add(variable, null);
                    }

                    context.OutputMultiset.Add(set);
                }

                return context.OutputMultiset;
            }

            throw new RdfQueryException(FederatedServiceExecutionFailedMessage, cause);
        }
    }

    private static ISet[] GetBindings(SparqlEvaluationContext context, GraphPattern pattern)
    {
        var bindings = new HashSet<ISet>();
        var boundVariables = pattern.Variables.Where(context.InputMultiset.ContainsVariable).ToList();
        if (!boundVariables.Any() && context.Query.Bindings is null)
        {
            return [];
        }

        if (context.Query.Bindings is not null && pattern.Variables.Intersect(context.Query.Bindings.Variables).Any())
        {
            foreach (var tuple in context.Query.Bindings.Tuples)
            {
                var set = new Set();
                foreach (var pair in tuple.Values)
                {
                    set.Add(pair.Key, tuple[pair.Key]);
                }

                bindings.Add(set);
            }

            return bindings.ToArray();
        }

        foreach (var inputSet in context.InputMultiset.Sets)
        {
            var set = new Set();
            foreach (var variable in boundVariables)
            {
                set.Add(variable, inputSet[variable]);
            }

            bindings.Add(set);
        }

        return bindings.ToArray();
    }

    private static IEnumerable<SparqlQuery> CreateRemoteQueries(
        SparqlEvaluationContext context,
        GraphPattern pattern,
        IReadOnlyList<ISet> bindings)
    {
        if (bindings.Count == 0)
        {
            yield return CreateRemoteQuery(context, pattern);
            yield break;
        }

        foreach (var chunk in bindings.Chunk(FederatedBindingsChunkSize))
        {
            var variables = chunk.SelectMany(static set => set.Variables).Distinct();
            var bindingsPattern = new BindingsPattern(variables);
            foreach (var set in chunk)
            {
                var tuple = new BindingTuple(
                    [.. set.Variables],
                    [.. set.Values.Select(static node => new NodeMatchPattern(node))]);
                bindingsPattern.AddTuple(tuple);
            }

            var remoteQuery = CreateRemoteQuery(context, pattern);
            remoteQuery.RootGraphPattern.AddInlineData(bindingsPattern);
            yield return remoteQuery;
        }
    }

    private static SparqlQuery CreateRemoteQuery(SparqlEvaluationContext context, GraphPattern pattern)
    {
        var limit = context.Query.Limit;
        if (context.Query.Offset > 0)
        {
            limit += context.Query.Offset;
        }

        return SparqlQuery.FromServiceQuery(pattern, limit);
    }
}
