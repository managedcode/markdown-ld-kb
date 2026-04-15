using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ManagedCode.MarkdownLd.Kb.Query;
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
    public string Predicate { get; init; } = string.Empty;
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
    KnowledgeGraph Graph)
{
    public MarkdownKnowledgeExtractionMode ExtractionMode { get; init; } = MarkdownKnowledgeExtractionMode.None;

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public KnowledgeGraphShaclValidationReport ValidateShacl(string? shapesTurtle = null)
    {
        return Graph.ValidateShacl(shapesTurtle);
    }
}

public sealed record KnowledgeGraphSnapshot(
    IReadOnlyList<KnowledgeGraphNode> Nodes,
    IReadOnlyList<KnowledgeGraphEdge> Edges);

public sealed record KnowledgeGraphNode(
    string Id,
    string Label,
    KnowledgeGraphNodeKind Kind);

public sealed record KnowledgeGraphEdge(
    string SubjectId,
    string PredicateId,
    string PredicateLabel,
    string ObjectId);

public enum KnowledgeGraphNodeKind
{
    Uri,
    Literal,
    Blank,
}

public sealed class ReadOnlySparqlQueryException(string message) : InvalidOperationException(message);

public static class KnowledgeNaming
{
    private static readonly IReadOnlyDictionary<string, string> PredicateUriAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [SchemaAboutText] = ExpectedSchemaAbout,
        [ExpectedSchemaAbout] = ExpectedSchemaAbout,
        [SchemaAuthorText] = ExpectedSchemaAuthor,
        [ExpectedSchemaAuthor] = ExpectedSchemaAuthor,
        [SchemaCreatorText] = ExpectedSchemaCreator,
        [ExpectedSchemaCreator] = ExpectedSchemaCreator,
        [SchemaHasPartText] = ExpectedSchemaHasPart,
        [ExpectedSchemaHasPart] = ExpectedSchemaHasPart,
        [SchemaDescriptionText] = ExpectedSchemaDescription,
        [ExpectedSchemaDescription] = ExpectedSchemaDescription,
        [SchemaKeywordsText] = ExpectedSchemaKeywords,
        [ExpectedSchemaKeywords] = ExpectedSchemaKeywords,
        [SchemaMentionsText] = ExpectedSchemaMentions,
        [ExpectedSchemaMentions] = ExpectedSchemaMentions,
        [SchemaNameText] = ExpectedSchemaName,
        [ExpectedSchemaName] = ExpectedSchemaName,
        [SchemaSameAsText] = ExpectedSchemaSameAs,
        [ExpectedSchemaSameAs] = ExpectedSchemaSameAs,
    };

    private static readonly IReadOnlyDictionary<string, string> PredicateKeywordAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [MentionPredicateKey] = ExpectedSchemaMentions,
        [AboutPredicateKey] = ExpectedSchemaAbout,
        [AuthorPredicateKey] = ExpectedSchemaAuthor,
        [CreatorPredicateKey] = ExpectedSchemaCreator,
        [HasPartPredicateKey] = ExpectedSchemaHasPart,
        [SameAsPredicateKey] = ExpectedSchemaSameAs,
        [DescriptionPredicateKey] = ExpectedSchemaDescription,
        [KeywordsPredicateKey] = ExpectedSchemaKeywords,
        [RelatedToPredicateKey] = KbRelatedTo,
        [MemberOfPredicateKey] = KbMemberOf,
        [NextStepPredicateKey] = KbNextStep,
    };

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
        var withoutExtension = Path.ChangeExtension(normalized, null);
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
        if (string.IsNullOrWhiteSpace(queryText))
        {
            failureReason = EmptySparqlQueryMessage;
            return false;
        }

        var safety = SparqlSafety.EnforceReadOnly(queryText);
        if (!safety.IsAllowed)
        {
            failureReason = SparqlSafety.TryGetMutatingKeywordOutsideString(queryText, out var keyword)
                ? MutatingKeywordMessagePrefix + keyword + MutatingKeywordMessageSuffix
                : safety.ErrorMessage;
            return false;
        }

        failureReason = null;
        return true;
    }

    public static string NormalizePredicate(string predicate)
    {
        var trimmed = predicate.Trim();

        if (PredicateUriAliases.TryGetValue(trimmed, out var canonicalPredicate))
        {
            return canonicalPredicate;
        }

        if (trimmed.Contains(':', StringComparison.Ordinal))
        {
            return trimmed;
        }

        return PredicateKeywordAliases.TryGetValue(trimmed, out canonicalPredicate)
            ? canonicalPredicate
            : string.Empty;
    }
}
