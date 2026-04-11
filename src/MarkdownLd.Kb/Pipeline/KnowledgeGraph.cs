using System.Text;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class KnowledgeGraph
{
    private readonly Graph _graph;

    internal KnowledgeGraph(Graph graph)
    {
        _graph = graph;
    }

    public int TripleCount => _graph.Triples.Count;

    public async Task<SparqlQueryResult> ExecuteSelectAsync(string sparql, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteQueryAsync(sparql, cancellationToken);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException("Expected a SPARQL result set.");
        }

        return ToResult(resultSet);
    }

    public async Task<bool> ExecuteAskAsync(string sparql, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteQueryAsync(sparql, cancellationToken);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException("Expected a SPARQL result set.");
        }

        return resultSet.Result;
    }

    public Task<SparqlQueryResult> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var searchQuery = $"""
            PREFIX schema: <https://schema.org/>
            PREFIX kb: <https://example.com/vocab/kb#>
            SELECT DISTINCT ?subject ?name ?type WHERE {{
              ?subject a ?type .
              OPTIONAL {{ ?subject schema:name ?name . }}
              OPTIONAL {{ ?subject schema:description ?description . }}
              OPTIONAL {{ ?subject schema:keywords ?keyword . }}
              FILTER(
                (BOUND(?name) && CONTAINS(LCASE(STR(?name)), LCASE("{EscapeSparqlLiteral(term)}"))) ||
                (BOUND(?description) && CONTAINS(LCASE(STR(?description)), LCASE("{EscapeSparqlLiteral(term)}"))) ||
                (BOUND(?keyword) && CONTAINS(LCASE(STR(?keyword)), LCASE("{EscapeSparqlLiteral(term)}")))
              )
            }}
            LIMIT 100
            """;

        return ExecuteSelectAsync(searchQuery, cancellationToken);
    }

    public string SerializeTurtle()
    {
        using var writer = new StringWriter();
        var turtleWriter = new CompressingTurtleWriter();
        turtleWriter.Save(_graph, writer);
        return writer.ToString();
    }

    public string SerializeJsonLd()
    {
        using var writer = new StringWriter();
        var store = new TripleStore();
        store.Add(_graph);
        var jsonLdWriter = new JsonLdWriter();
        jsonLdWriter.Save(store, writer, false);
        return writer.ToString();
    }

    internal Graph InnerGraph => _graph;

    private async Task<object> ExecuteQueryAsync(string sparql, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KnowledgeQueryValidator.ValidateReadOnly(sparql);

        var parser = new SparqlQueryParser();
        var query = parser.ParseFromString(sparql);
        if (query.QueryType is not (SparqlQueryType.Ask
            or SparqlQueryType.Select
            or SparqlQueryType.SelectAll
            or SparqlQueryType.SelectAllDistinct
            or SparqlQueryType.SelectAllReduced
            or SparqlQueryType.SelectDistinct
            or SparqlQueryType.SelectReduced))
        {
            throw new ReadOnlySparqlQueryException($"Only ASK and SELECT queries are allowed, not {query.QueryType}.");
        }

        var dataset = new InMemoryDataset(_graph);
        var processor = new LeviathanQueryProcessor(dataset);
        return await Task.Run(() => processor.ProcessQuery(query), cancellationToken).ConfigureAwait(false);
    }

    private static SparqlQueryResult ToResult(SparqlResultSet resultSet)
    {
        var variables = resultSet.Variables.Select(variable => variable.ToString()).ToArray();
        var rows = new List<SparqlRow>();

        foreach (var result in resultSet)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in resultSet.Variables)
            {
                var node = result.Value(variable);
                if (node is not null)
                {
                    values[variable] = RenderNode(node);
                }
            }

            rows.Add(new SparqlRow(values));
        }

        return new SparqlQueryResult(variables, rows);
    }

    private static string RenderNode(INode node)
    {
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            ILiteralNode literalNode => literalNode.Value,
            IBlankNode blankNode => $"_:{blankNode.InternalID}",
            _ => node.ToString(),
        };
    }

    private static string EscapeSparqlLiteral(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}

internal static class KnowledgeQueryValidator
{
    public static void ValidateReadOnly(string sparql)
    {
        if (!KnowledgeNaming.IsReadOnlySparql(sparql, out var failureReason))
        {
            throw new ReadOnlySparqlQueryException(failureReason ?? "SPARQL query is not read-only.");
        }
    }
}
