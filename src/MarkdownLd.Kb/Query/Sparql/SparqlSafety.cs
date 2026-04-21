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
    private const char SemicolonCharacter = ';';
    private const char DoubleQuoteCharacter = '"';
    private const char SingleQuoteCharacter = '\'';
    private const char EscapeCharacter = '\\';
    private const char MaskCharacter = ' ';
    private const char CommentCharacter = '#';
    private const char IriStartCharacter = '<';
    private const char IriEndCharacter = '>';
    private const char LineFeedCharacter = '\n';
    private const char CarriageReturnCharacter = '\r';

    private static readonly SparqlQueryParser Parser = new();
    private static readonly Regex MutatingKeywordRegex = new(MutatingKeywordPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static SparqlSafetyResult EnforceReadOnly(string query, int defaultLimit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new(false, string.Empty, SparqlQueryRequiredMessage);
        }

        var trimmed = query.Trim();

        SparqlQuery parsed;
        try
        {
            parsed = Parser.ParseFromString(trimmed);
        }
        catch (Exception ex)
        {
            if (TryGetMutatingKeywordOutsideString(trimmed, out _))
            {
                return new(false, query, OnlySelectAndAskQueriesAllowedMessage);
            }

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

    internal static bool TryGetMutatingKeywordOutsideString(string query, out string? keyword)
    {
        var masked = query.ToCharArray();
        var state = SparqlMaskState.None;
        var quote = MaskCharacter;

        for (var index = 0; index < masked.Length; index++)
        {
            var current = masked[index];
            if (TryMaskCurrentCharacter(masked, ref index, current, ref state, ref quote))
            {
                continue;
            }

            StartMaskingIfNeeded(masked, index, current, ref state, ref quote);
        }

        var match = MutatingKeywordRegex.Match(new string(masked));
        keyword = match.Success ? match.Value : null;
        return match.Success;
    }

    private static bool TryMaskCurrentCharacter(
        char[] masked,
        ref int index,
        char current,
        ref SparqlMaskState state,
        ref char quote)
    {
        return state switch
        {
            SparqlMaskState.Comment => MaskComment(masked, index, current, ref state),
            SparqlMaskState.Iri => MaskIri(masked, index, current, ref state),
            SparqlMaskState.String => MaskString(masked, ref index, current, ref state, ref quote),
            _ => false,
        };
    }

    private static void StartMaskingIfNeeded(
        char[] masked,
        int index,
        char current,
        ref SparqlMaskState state,
        ref char quote)
    {
        switch (current)
        {
            case CommentCharacter:
                state = SparqlMaskState.Comment;
                masked[index] = MaskCharacter;
                break;

            case IriStartCharacter:
                state = SparqlMaskState.Iri;
                masked[index] = MaskCharacter;
                break;

            case DoubleQuoteCharacter:
            case SingleQuoteCharacter:
                state = SparqlMaskState.String;
                quote = current;
                masked[index] = MaskCharacter;
                break;
        }
    }

    private static bool MaskComment(char[] masked, int index, char current, ref SparqlMaskState state)
    {
        masked[index] = MaskCharacter;
        if (current is LineFeedCharacter or CarriageReturnCharacter)
        {
            state = SparqlMaskState.None;
        }

        return true;
    }

    private static bool MaskIri(char[] masked, int index, char current, ref SparqlMaskState state)
    {
        masked[index] = MaskCharacter;
        if (current == IriEndCharacter)
        {
            state = SparqlMaskState.None;
        }

        return true;
    }

    private static bool MaskString(
        char[] masked,
        ref int index,
        char current,
        ref SparqlMaskState state,
        ref char quote)
    {
        masked[index] = MaskCharacter;
        if (current == EscapeCharacter && index + 1 < masked.Length)
        {
            masked[++index] = MaskCharacter;
            return true;
        }

        if (current == quote)
        {
            state = SparqlMaskState.None;
            quote = MaskCharacter;
        }

        return true;
    }

    private enum SparqlMaskState
    {
        None,
        String,
        Comment,
        Iri,
    }
}
