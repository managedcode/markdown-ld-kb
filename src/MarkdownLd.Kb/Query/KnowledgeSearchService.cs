using System.Globalization;
using VDS.RDF;
using VDS.RDF.Query;

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
    private readonly SparqlQueryExecutor _queryExecutor;

    public KnowledgeSearchService(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _queryExecutor = new SparqlQueryExecutor(graph);
    }

    public IReadOnlyList<KnowledgeEntitySearchResult> SearchEntities(string term, int limit = 25)
    {
        var query = $"""
PREFIX schema: <{KbNamespaces.Schema}>
PREFIX rdf: <{KbNamespaces.Rdf}>
SELECT DISTINCT ?entity ?label ?type ?sameAs WHERE {{
  ?entity a ?type ;
          schema:name ?label .
  OPTIONAL {{ ?entity schema:sameAs ?sameAs }}
  FILTER(?type != schema:Article)
  FILTER(CONTAINS(LCASE(STR(?label)), LCASE("{EscapeSparqlString(term)}")))
}}
LIMIT {limit}
""";

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapEntityResults(result);
    }

    public IReadOnlyList<KnowledgeArticleSearchResult> SearchArticles(string term, int limit = 25)
    {
        var escaped = EscapeSparqlString(term);
        var query = $"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT DISTINCT ?article ?title ?summary ?keywords WHERE {{
  ?article a schema:Article ;
           schema:name ?title .
  OPTIONAL {{ ?article schema:description ?summary }}
  OPTIONAL {{ ?article schema:keywords ?keywords }}
  OPTIONAL {{
    ?article schema:mentions ?entity .
    ?entity schema:name ?entityLabel .
  }}
  FILTER(
    CONTAINS(LCASE(STR(?title)), LCASE("{escaped}")) ||
    CONTAINS(LCASE(STR(COALESCE(?summary, ""))), LCASE("{escaped}")) ||
    CONTAINS(LCASE(STR(COALESCE(?keywords, ""))), LCASE("{escaped}")) ||
    CONTAINS(LCASE(STR(COALESCE(?entityLabel, ""))), LCASE("{escaped}"))
  )
}}
LIMIT {limit}
""";

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapArticleResults(result);
    }

    public IReadOnlyList<KnowledgeArticleSearchResult> SearchArticlesByEntityLabel(string entityLabel, int limit = 25)
    {
        var query = $"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT DISTINCT ?article ?title ?summary ?keywords WHERE {{
  ?article a schema:Article ;
           schema:name ?title ;
           schema:mentions ?entity .
  ?entity schema:name ?entityLabel .
  OPTIONAL {{ ?article schema:description ?summary }}
  OPTIONAL {{ ?article schema:keywords ?keywords }}
  FILTER(CONTAINS(LCASE(STR(?entityLabel)), LCASE("{EscapeSparqlString(entityLabel)}")))
}}
LIMIT {limit}
""";

        var result = _queryExecutor.ExecuteReadOnly(query);
        return MapArticleResults(result);
    }

    private static IReadOnlyList<KnowledgeEntitySearchResult> MapEntityResults(SparqlQueryResult result)
    {
        var items = new Dictionary<Uri, KnowledgeEntitySearchResult>();

        foreach (var row in result.Rows)
        {
            if (!TryGetUri(row.Bindings, "entity", out var entityId))
            {
                continue;
            }

            var label = TryGetString(row.Bindings, "label") ?? string.Empty;
            var type = TryGetString(row.Bindings, "type") ?? KbNamespaces.SchemaThing.AbsoluteUri;

            if (TryGetUri(row.Bindings, "sameAs", out var sameAsUri))
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
                if (TryGetUri(row.Bindings, "sameAs", out var currentSameAs))
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
                TryGetUri(row.Bindings, "sameAs", out var oneSameAs) ? [oneSameAs] : []);
        }

        return items.Values.OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<KnowledgeArticleSearchResult> MapArticleResults(SparqlQueryResult result)
    {
        var items = new Dictionary<Uri, KnowledgeArticleSearchResult>();

        foreach (var row in result.Rows)
        {
            if (!TryGetUri(row.Bindings, "article", out var articleId))
            {
                continue;
            }

            var title = TryGetString(row.Bindings, "title") ?? string.Empty;
            var summary = TryGetString(row.Bindings, "summary");
            var keywords = TryGetString(row.Bindings, "keywords");

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
        if (bindings.TryGetValue(variable, out var binding) && Uri.TryCreate(binding.Value, UriKind.Absolute, out value))
        {
            return true;
        }

        value = null!;
        return false;
    }

    private static string? TryGetString(IReadOnlyDictionary<string, SparqlBindingValue> bindings, string variable)
    {
        return bindings.TryGetValue(variable, out var binding) ? binding.Value : null;
    }

    private static string EscapeSparqlString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
