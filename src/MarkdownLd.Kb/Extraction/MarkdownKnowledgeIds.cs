using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Extraction.MarkdownKnowledgeConstants;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

public static class MarkdownKnowledgeIds
{
    public const string Namespace = "urn:managedcode:markdown-ld-kb:";

    public static string BuildArticleId(string? title, string? sourcePath = null, string? canonicalUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(canonicalUrl))
        {
            return canonicalUrl!;
        }

        var sourceLabel = !string.IsNullOrWhiteSpace(title)
            ? title
            : NormalizeSourcePath(sourcePath);

        return BuildId(ArticleKind, sourceLabel);
    }

    public static string BuildEntityId(string label) => BuildId(EntityKind, label);

    public static string BuildId(string kind, string label)
    {
        var slug = Slugify(label);
        return string.Concat(Namespace, kind, IdSeparator, slug);
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ItemLabel;
        }

        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, SlugInvalidCharactersPattern, string.Empty, RegexOptions.CultureInvariant);
        slug = Regex.Replace(slug, SlugWhitespacePattern, Hyphen, RegexOptions.CultureInvariant);
        slug = Regex.Replace(slug, SlugHyphenPattern, Hyphen, RegexOptions.CultureInvariant);
        return slug.Trim('-') is { Length: > 0 } normalized ? normalized : ItemLabel;
    }

    public static string HumanizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        text = Regex.Replace(text, CamelBoundaryPattern, CamelBoundaryReplacement, RegexOptions.CultureInvariant);
        text = Regex.Replace(text, LabelSeparatorPattern, Space, RegexOptions.CultureInvariant);
        text = Regex.Replace(text, WhitespacePattern, Space, RegexOptions.CultureInvariant);
        return text.Trim();
    }

    private static string NormalizeSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return UntitledLabel;
        }

        var normalized = sourcePath.Replace('\\', '/');
        normalized = Regex.Replace(normalized, ExtensionPattern, string.Empty, RegexOptions.CultureInvariant);
        normalized = normalized.Replace('/', '-');
        return normalized;
    }
}
