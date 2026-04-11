using System.Text.RegularExpressions;
using VDS.RDF.Query;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record SparqlSafetyResult(bool IsAllowed, string Query, string? ErrorMessage);

public static class SparqlSafety
{
    private static readonly Regex MutatingKeywordPattern = new(
        @"\b(INSERT|DELETE|LOAD|CLEAR|DROP|CREATE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly SparqlQueryParser Parser = new();

    public static SparqlSafetyResult EnforceReadOnly(string query, int defaultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new(false, string.Empty, "SPARQL query is required");
        }

        var trimmed = query.Trim();

        if (MutatingKeywordPattern.IsMatch(trimmed))
        {
            return new(false, query, "Only SELECT and ASK queries are allowed");
        }

        SparqlQuery parsed;
        try
        {
            parsed = Parser.ParseFromString(trimmed);
        }
        catch (Exception ex)
        {
            return new(false, query, ex.Message);
        }

        if (!IsReadOnlyQuery(parsed.QueryType))
        {
            return new(false, query, "Only SELECT and ASK queries are allowed");
        }

        if (IsSelectQuery(parsed.QueryType) && !HasTopLevelLimit(trimmed))
        {
            trimmed = trimmed.TrimEnd(';') + Environment.NewLine + $"LIMIT {defaultLimit}";
        }

        return new(true, trimmed, null);
    }

    public static bool IsReadOnlyQuery(SparqlQueryType queryType) => IsSelectQuery(queryType) || queryType == SparqlQueryType.Ask;

    public static bool IsSelectQuery(SparqlQueryType queryType)
    {
        return queryType is SparqlQueryType.Select
            or SparqlQueryType.SelectAll
            or SparqlQueryType.SelectDistinct
            or SparqlQueryType.SelectReduced
            or SparqlQueryType.SelectAllDistinct
            or SparqlQueryType.SelectAllReduced;
    }

    public static bool HasTopLevelLimit(string query)
    {
        return Regex.IsMatch(query, @"\bLIMIT\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
