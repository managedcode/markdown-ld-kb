using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record MarkdownSourceDocument(string Path, string Content, Uri? CanonicalUri = null);

public sealed record KnowledgeSourceDocument(
    string Path,
    string Content,
    Uri? CanonicalUri,
    string MediaType)
{
    public MarkdownSourceDocument ToMarkdownSourceDocument()
    {
        return new MarkdownSourceDocument(Path, Content, CanonicalUri);
    }
}

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
    public string Type { get; init; } = DefaultSchemaThing;
    public List<string> SameAs { get; init; } = [];
    public double Confidence { get; init; } = 0.8;
    public string Source { get; init; } = string.Empty;
}

public sealed record KnowledgeAssertionFact
{
    public string SubjectId { get; init; } = string.Empty;
    public string Predicate { get; init; } = DefaultKbRelatedTo;
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

public sealed class ReadOnlySparqlQueryException(string message) : InvalidOperationException(message)
{
}

public static class KnowledgeNaming
{
    private static readonly Regex NonAlphaNumeric = new(NonAlphaNumericPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new(WhitespacePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Dashes = new(DashesPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultItem;
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
        s = Whitespace.Replace(s, Hyphen);
        s = Dashes.Replace(s, Hyphen);
        return s.Trim('-');
    }

    public static Uri NormalizeBaseUri(Uri baseUri)
    {
        var text = baseUri.AbsoluteUri;
        if (!text.EndsWith(Slash, StringComparison.Ordinal))
        {
            text += Slash;
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
            withoutExtension = DefaultDocument;
        }

        if (!withoutExtension.EndsWith(Slash, StringComparison.Ordinal))
        {
            withoutExtension += Slash;
        }

        return new Uri(NormalizeBaseUri(baseUri), withoutExtension);
    }

    public static string CreateEntityId(Uri baseUri, string label)
    {
        return new Uri(NormalizeBaseUri(baseUri), EntityIdPrefix + Slugify(label)).AbsoluteUri;
    }

    public static string NormalizeSourcePath(string sourcePath)
    {
        var normalized = sourcePath.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[ContentPrefix.Length..];
        }

        return normalized;
    }

    public static bool IsReadOnlySparql(string queryText, out string? failureReason)
    {
        var normalized = queryText.Trim();
        if (normalized.Length == 0)
        {
            failureReason = EmptySparqlQueryMessage;
            return false;
        }

        if (MutatingKeywordRegex.IsMatch(normalized.ToUpperInvariant()))
        {
            var match = MutatingKeywordRegex.Match(normalized.ToUpperInvariant());
            failureReason = MutatingKeywordMessagePrefix + match.Value + MutatingKeywordMessageSuffix;
            return false;
        }

        failureReason = null;
        return true;
    }

    public static string NormalizePredicate(string predicate)
    {
        var trimmed = predicate.Trim();

        if (trimmed.Equals(SchemaAboutText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaAbout, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaAbout;
        }

        if (trimmed.Equals(SchemaAuthorText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaAuthor, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaAuthor;
        }

        if (trimmed.Equals(SchemaCreatorText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaCreator, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaCreator;
        }

        if (trimmed.Equals(SchemaDescriptionText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaDescription, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaDescription;
        }

        if (trimmed.Equals(SchemaKeywordsText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaKeywords, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaKeywords;
        }

        if (trimmed.Equals(SchemaMentionsText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaMentions, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaMentions;
        }

        if (trimmed.Equals(SchemaNameText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaName, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaName;
        }

        if (trimmed.Equals(SchemaSameAsText, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals(ExpectedSchemaSameAs, StringComparison.OrdinalIgnoreCase))
        {
            return ExpectedSchemaSameAs;
        }

        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed.ToLowerInvariant() switch
        {
            MentionPredicateKey => ExpectedSchemaMentions,
            AboutPredicateKey => ExpectedSchemaAbout,
            AuthorPredicateKey => ExpectedSchemaAuthor,
            CreatorPredicateKey => ExpectedSchemaCreator,
            SameAsPredicateKey => ExpectedSchemaSameAs,
            DescriptionPredicateKey => ExpectedSchemaDescription,
            KeywordsPredicateKey => ExpectedSchemaKeywords,
            RelatedToPredicateKey => DefaultKbRelatedTo,
            _ => DefaultKbRelatedTo,
        };
    }

    private static readonly Regex MutatingKeywordRegex = new(
        MutatingKeywordPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
