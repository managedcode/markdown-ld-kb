using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record KnowledgeEntitySearchResult(
    Uri Id,
    string Label,
    string Type,
    IReadOnlyList<Uri> SameAs);

public sealed record KnowledgeArticleSearchResult(
    Uri Id,
    string Title,
    string? Summary,
    string? Keywords);

public sealed class KnowledgeSearchService
{
    private const string SchemaPlaceholder = "{SCHEMA}";
    private const string RdfPlaceholder = "{RDF}";
    private const string TermPlaceholder = "{TERM}";
    private const string LimitPlaceholder = "{LIMIT}";

    private const string EntityVariable = "entity";
    private const string LabelVariable = "label";
    private const string TypeVariable = "type";
    private const string SameAsVariable = "sameAs";
    private const string ArticleVariable = "article";
    private const string TitleVariable = "title";
    private const string SummaryVariable = "summary";
    private const string KeywordsVariable = "keywords";
    private const string SearchEntitiesQueryTemplate = """
PREFIX schema: <{SCHEMA}>
PREFIX rdf: <{RDF}>
SELECT DISTINCT ?entity ?label ?type ?sameAs WHERE {
  ?entity a ?type ;
          schema:name ?label .
  OPTIONAL { ?entity schema:sameAs ?sameAs }
  FILTER(?type != schema:Article)
  FILTER(CONTAINS(LCASE(STR(?label)), LCASE("{TERM}")))
}
LIMIT {LIMIT}
""";

    private const string SearchArticlesQueryTemplate = """
PREFIX schema: <{SCHEMA}>
SELECT DISTINCT ?article ?title ?summary ?keywords WHERE {
  ?article a schema:Article ;
           schema:name ?title .
  OPTIONAL { ?article schema:description ?summary }
  OPTIONAL { ?article schema:keywords ?keywords }
  OPTIONAL {
    ?article schema:mentions ?entity .
    ?entity schema:name ?entityLabel .
  }
  FILTER(
    CONTAINS(LCASE(STR(?title)), LCASE("{TERM}")) ||
    CONTAINS(LCASE(STR(COALESCE(?summary, ""))), LCASE("{TERM}")) ||
    CONTAINS(LCASE(STR(COALESCE(?keywords, ""))), LCASE("{TERM}")) ||
    CONTAINS(LCASE(STR(COALESCE(?entityLabel, ""))), LCASE("{TERM}"))
  )
}
LIMIT {LIMIT}
""";

    private const string SearchArticlesByEntityLabelQueryTemplate = """
PREFIX schema: <{SCHEMA}>
SELECT DISTINCT ?article ?title ?summary ?keywords WHERE {
  ?article a schema:Article ;
           schema:name ?title ;
           schema:mentions ?entity .
  ?entity schema:name ?entityLabel .
  OPTIONAL { ?article schema:description ?summary }
  OPTIONAL { ?article schema:keywords ?keywords }
  FILTER(CONTAINS(LCASE(STR(?entityLabel)), LCASE("{TERM}")))
}
LIMIT {LIMIT}
""";

    private const string Backslash = "\\";
    private const string EscapedBackslash = "\\\\";
    private const string Quote = "\"";
    private const string EscapedQuote = "\\\"";
    private const string CarriageReturn = "\r";
    private const string EscapedCarriageReturn = "\\r";
    private const string LineFeed = "\n";
    private const string EscapedLineFeed = "\\n";
    private const string EmptyString = "";

    private readonly SparqlQueryExecutor _queryExecutor;

    public KnowledgeSearchService(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _queryExecutor = new SparqlQueryExecutor(graph);
    }

    public IReadOnlyList<KnowledgeEntitySearchResult> SearchEntities(string term, int limit = 25)
    {
        var query = BuildQuery(
            SearchEntitiesQueryTemplate,
            (SchemaPlaceholder, KbNamespaces.Schema),
            (RdfPlaceholder, KbNamespaces.Rdf),
            (TermPlaceholder, EscapeSparqlString(term)),
            (LimitPlaceholder, limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapEntityResults(result);
    }

    public IReadOnlyList<KnowledgeArticleSearchResult> SearchArticles(string term, int limit = 25)
    {
        var query = BuildQuery(
            SearchArticlesQueryTemplate,
            (SchemaPlaceholder, KbNamespaces.Schema),
            (TermPlaceholder, EscapeSparqlString(term)),
            (LimitPlaceholder, limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapArticleResults(result);
    }

    public IReadOnlyList<KnowledgeArticleSearchResult> SearchArticlesByEntityLabel(string entityLabel, int limit = 25)
    {
        var query = BuildQuery(
            SearchArticlesByEntityLabelQueryTemplate,
            (SchemaPlaceholder, KbNamespaces.Schema),
            (TermPlaceholder, EscapeSparqlString(entityLabel)),
            (LimitPlaceholder, limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapArticleResults(result);
    }

    private static IReadOnlyList<KnowledgeEntitySearchResult> MapEntityResults(SparqlQueryResult result)
    {
        var items = new Dictionary<Uri, KnowledgeEntitySearchResult>();

        foreach (var row in result.Rows)
        {
            if (!TryGetUri(row.Bindings, EntityVariable, out var entityId))
            {
                continue;
            }

            var label = TryGetString(row.Bindings, LabelVariable) ?? EmptyString;
            var type = TryGetString(row.Bindings, TypeVariable) ?? KbNamespaces.SchemaThing.AbsoluteUri;

            if (TryGetUri(row.Bindings, SameAsVariable, out var sameAsUri))
            {
                if (items.TryGetValue(entityId, out var existing))
                {
                    var mergedSameAs = existing.SameAs.Concat([sameAsUri]).Distinct().ToArray();
                    items[entityId] = existing with { SameAs = mergedSameAs };
                    continue;
                }
            }

            if (items.TryGetValue(entityId, out var current))
            {
                var mergedSameAs = current.SameAs;
                if (TryGetUri(row.Bindings, SameAsVariable, out var currentSameAs))
                {
                    mergedSameAs = mergedSameAs.Concat([currentSameAs]).Distinct().ToArray();
                }

                items[entityId] = current with { Label = label, Type = type, SameAs = mergedSameAs };
                continue;
            }

            items[entityId] = new KnowledgeEntitySearchResult(
                entityId,
                label,
                type,
                TryGetUri(row.Bindings, SameAsVariable, out var oneSameAs) ? [oneSameAs] : []);
        }

        return items.Values.OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<KnowledgeArticleSearchResult> MapArticleResults(SparqlQueryResult result)
    {
        var items = new Dictionary<Uri, KnowledgeArticleSearchResult>();

        foreach (var row in result.Rows)
        {
            if (!TryGetUri(row.Bindings, ArticleVariable, out var articleId))
            {
                continue;
            }

            var title = TryGetString(row.Bindings, TitleVariable) ?? EmptyString;
            var summary = TryGetString(row.Bindings, SummaryVariable);
            var keywords = TryGetString(row.Bindings, KeywordsVariable);

            if (items.TryGetValue(articleId, out var current))
            {
                items[articleId] = current with
                {
                    Title = title.Length > 0 ? title : current.Title,
                    Summary = current.Summary ?? summary,
                    Keywords = current.Keywords ?? keywords
                };
                continue;
            }

            items[articleId] = new KnowledgeArticleSearchResult(articleId, title, summary, keywords);
        }

        return items.Values.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool TryGetUri(IReadOnlyDictionary<string, SparqlBindingValue> bindings, string variable, out Uri value)
    {
        if (bindings.TryGetValue(variable, out var binding) && Uri.TryCreate(binding.Value, UriKind.Absolute, out var parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = null!;
        return false;
    }

    private static string? TryGetString(IReadOnlyDictionary<string, SparqlBindingValue> bindings, string variable)
    {
        return bindings.TryGetValue(variable, out var binding) ? binding.Value : null;
    }

    private static string BuildQuery(string template, params (string Token, string Value)[] replacements)
    {
        var query = template;
        foreach (var replacement in replacements)
        {
            query = query.Replace(replacement.Token, replacement.Value, StringComparison.Ordinal);
        }

        return query;
    }

    private static string EscapeSparqlString(string value)
    {
        return value
            .Replace(Backslash, EscapedBackslash, StringComparison.Ordinal)
            .Replace(Quote, EscapedQuote, StringComparison.Ordinal)
            .Replace(CarriageReturn, EscapedCarriageReturn, StringComparison.Ordinal)
            .Replace(LineFeed, EscapedLineFeed, StringComparison.Ordinal);
    }
}
