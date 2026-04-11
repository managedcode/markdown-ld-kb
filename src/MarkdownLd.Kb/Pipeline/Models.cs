using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownSourceDocument(string Path, string Content, Uri? CanonicalUri = null);

public sealed record MarkdownSection(
    int Level,
    string Heading,
    IReadOnlyList<string> HeadingPath,
    string Text,
    int StartOffset,
    int EndOffset);

public sealed record MarkdownDocument(
    Uri DocumentUri,
    string SourcePath,
    string Title,
    IReadOnlyDictionary<string, object?> FrontMatter,
    string Body,
    IReadOnlyList<MarkdownSection> Sections);

public sealed record KnowledgeEntityFact
{
    public string? Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = "schema:Thing";
    public List<string> SameAs { get; init; } = [];
    public double Confidence { get; init; } = 0.8;
    public string Source { get; init; } = string.Empty;
}

public sealed record KnowledgeAssertionFact
{
    public string SubjectId { get; init; } = string.Empty;
    public string Predicate { get; init; } = "kb:relatedTo";
    public string ObjectId { get; init; } = string.Empty;
    public double Confidence { get; init; } = 0.8;
    public string Source { get; init; } = string.Empty;
}

public sealed record KnowledgeExtractionResult
{
    public List<KnowledgeEntityFact> Entities { get; init; } = [];
    public List<KnowledgeAssertionFact> Assertions { get; init; } = [];
}

public sealed record SparqlRow(IReadOnlyDictionary<string, string> Values);

public sealed record SparqlQueryResult(IReadOnlyList<string> Variables, IReadOnlyList<SparqlRow> Rows);

public sealed record MarkdownKnowledgeBuildResult(
    IReadOnlyList<MarkdownDocument> Documents,
    KnowledgeExtractionResult Facts,
    KnowledgeGraph Graph);

public sealed class ReadOnlySparqlQueryException : InvalidOperationException
{
    public ReadOnlySparqlQueryException(string message) : base(message)
    {
    }
}

public static class KnowledgeNaming
{
    private static readonly Regex NonAlphaNumeric = new("[^a-z0-9\\s-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new("[\\s_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Dashes = new("-+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        var s = NonAlphaNumeric.Replace(builder.ToString(), string.Empty);
        s = Whitespace.Replace(s, "-");
        s = Dashes.Replace(s, "-");
        return s.Trim('-');
    }

    public static Uri NormalizeBaseUri(Uri baseUri)
    {
        var text = baseUri.AbsoluteUri;
        if (!text.EndsWith('/', StringComparison.Ordinal))
        {
            text += "/";
        }

        return new Uri(text, UriKind.Absolute);
    }

    public static Uri CreateDocumentUri(Uri baseUri, string sourcePath)
    {
        var normalized = NormalizeSourcePath(sourcePath);
        var withoutExtension = Path.ChangeExtension(normalized, null) ?? normalized;
        withoutExtension = withoutExtension.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(withoutExtension))
        {
            withoutExtension = "document";
        }

        if (!withoutExtension.EndsWith('/', StringComparison.Ordinal))
        {
            withoutExtension += "/";
        }

        return new Uri(NormalizeBaseUri(baseUri), withoutExtension);
    }

    public static string CreateEntityId(Uri baseUri, string label)
    {
        return new Uri(NormalizeBaseUri(baseUri), $"id/{Slugify(label)}").AbsoluteUri;
    }

    public static string NormalizeSourcePath(string sourcePath)
    {
        var normalized = sourcePath.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["content/".Length..];
        }

        return normalized;
    }

    public static bool IsReadOnlySparql(string queryText, out string? failureReason)
    {
        var normalized = queryText.Trim();
        if (normalized.Length == 0)
        {
            failureReason = "SPARQL query is empty";
            return false;
        }

        var upper = normalized.ToUpperInvariant();
        foreach (var keyword in new[] { "INSERT", "DELETE", "LOAD", "CLEAR", "DROP", "CREATE", "MOVE", "COPY", "ADD", "WITH", "MODIFY" })
        {
            if (Regex.IsMatch(upper, $@"\b{keyword}\b", RegexOptions.CultureInvariant))
            {
                failureReason = $"Mutating keyword '{keyword}' is not allowed";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    public static string NormalizePredicate(string predicate)
    {
        var trimmed = predicate.Trim();
        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed.ToLowerInvariant() switch
        {
            "mentions" => "schema:mentions",
            "about" => "schema:about",
            "author" => "schema:author",
            "creator" => "schema:creator",
            "sameas" => "schema:sameAs",
            "description" => "schema:description",
            "keywords" => "schema:keywords",
            "relatedto" => "kb:relatedTo",
            _ => "kb:relatedTo",
        };
    }
}
