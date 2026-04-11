using System.Text.RegularExpressions;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record SparqlSafetyResult(bool IsAllowed, string Query, string? ErrorMessage);

public static class SparqlSafety
{
    private const string SparqlQueryRequiredMessage = "SPARQL query is required";
    private const string OnlySelectAndAskQueriesAllowedMessage = "Only SELECT and ASK queries are allowed";
    private const string LimitClausePrefix = "LIMIT ";
    private const string MutatingKeywordPattern = @"\b(INSERT|DELETE|LOAD|CLEAR|DROP|CREATE)\b";
    private const string EmptyString = "";
    private const char SemicolonCharacter = ';';
    private const char DoubleQuoteCharacter = '"';
    private const char SingleQuoteCharacter = '\'';
    private const char EscapeCharacter = '\\';
    private const char MaskCharacter = ' ';

    private static readonly SparqlQueryParser Parser = new();
    private static readonly Regex MutatingKeywordRegex = new(MutatingKeywordPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static SparqlSafetyResult EnforceReadOnly(string query, int defaultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new(false, EmptyString, SparqlQueryRequiredMessage);
        }

        var trimmed = query.Trim();
        if (ContainsMutatingKeywordOutsideString(trimmed))
        {
            return new(false, query, OnlySelectAndAskQueriesAllowedMessage);
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
            return new(false, query, OnlySelectAndAskQueriesAllowedMessage);
        }

        if (IsSelectQuery(parsed.QueryType) && parsed.Limit < 0)
        {
            trimmed = trimmed.TrimEnd(SemicolonCharacter) + Environment.NewLine + LimitClausePrefix + defaultLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static bool ContainsMutatingKeywordOutsideString(string query)
    {
        var masked = query.ToCharArray();
        var inString = false;
        var quote = MaskCharacter;

        for (var index = 0; index < masked.Length; index++)
        {
            var current = masked[index];
            if (inString)
            {
                masked[index] = MaskCharacter;
                if (current == EscapeCharacter && index + 1 < masked.Length)
                {
                    masked[++index] = MaskCharacter;
                    continue;
                }

                if (current == quote)
                {
                    inString = false;
                }

                continue;
            }

            if (current is DoubleQuoteCharacter or SingleQuoteCharacter)
            {
                inString = true;
                quote = current;
                masked[index] = MaskCharacter;
            }
        }

        return MutatingKeywordRegex.IsMatch(new string(masked));
    }
}
