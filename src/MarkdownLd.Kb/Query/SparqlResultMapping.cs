using VDS.RDF;
using VDS.RDF.Query;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record SparqlBindingValue(
    string NodeKind,
    string Value,
    string? Datatype = null,
    string? Language = null);

public sealed record SparqlResultRow(IReadOnlyDictionary<string, SparqlBindingValue> Bindings);

public sealed record SparqlQueryResult(
    SparqlResultsType ResultType,
    bool? BooleanValue,
    IReadOnlyList<string> Variables,
    IReadOnlyList<SparqlResultRow> Rows);

public static class SparqlResultMapper
{
    private const string UriNodeKind = "uri";
    private const string LiteralNodeKind = "literal";
    private const string BlankNodeKind = "blank";
    private const string UnknownNodeKind = "unknown";

    public static SparqlQueryResult Map(SparqlResultSet resultSet)
    {
        ArgumentNullException.ThrowIfNull(resultSet);

        if (resultSet.ResultsType == SparqlResultsType.Boolean)
        {
            return new SparqlQueryResult(
                resultSet.ResultsType,
                resultSet.Result,
                Array.Empty<string>(),
                Array.Empty<SparqlResultRow>());
        }

        var variables = resultSet.Variables.Select(variable => variable.ToString()).ToArray();
        var rows = new List<SparqlResultRow>(resultSet.Count);

        foreach (var result in resultSet.Results)
        {
            var bindings = new Dictionary<string, SparqlBindingValue>(StringComparer.Ordinal);
            foreach (var variable in variables)
            {
                if (!result.TryGetBoundValue(variable, out var node) || node is null)
                {
                    continue;
                }

                bindings[variable] = MapNode(node);
            }

            rows.Add(new SparqlResultRow(bindings));
        }

        return new SparqlQueryResult(resultSet.ResultsType, null, variables, rows);
    }

    public static SparqlBindingValue MapNode(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            IUriNode uriNode => new SparqlBindingValue(UriNodeKind, uriNode.Uri.AbsoluteUri),
            ILiteralNode literalNode => new SparqlBindingValue(
                LiteralNodeKind,
                literalNode.Value,
                literalNode.DataType?.AbsoluteUri,
                string.IsNullOrWhiteSpace(literalNode.Language) ? null : literalNode.Language),
            IBlankNode blankNode => new SparqlBindingValue(BlankNodeKind, blankNode.InternalID),
            _ => new SparqlBindingValue(UnknownNodeKind, node.ToString())
        };
    }
}
