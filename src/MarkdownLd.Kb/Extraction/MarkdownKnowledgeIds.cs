using System.Text.RegularExpressions;

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

        return BuildId("article", sourceLabel);
    }

    public static string BuildEntityId(string label) => BuildId("entity", label);

    public static string BuildId(string kind, string label)
    {
        var slug = Slugify(label);
        return $"{Namespace}{kind}/{slug}";
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", string.Empty, RegexOptions.CultureInvariant);
        slug = Regex.Replace(slug, @"[\s_]+", "-", RegexOptions.CultureInvariant);
        slug = Regex.Replace(slug, @"-+", "-", RegexOptions.CultureInvariant);
        return slug.Trim('-') is { Length: > 0 } normalized ? normalized : "item";
    }

    public static string HumanizeLabel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        text = Regex.Replace(text, @"([a-z0-9])([A-Z])", "$1 $2", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"[-_/]+", " ", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant);
        return text.Trim();
    }

    private static string NormalizeSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return "untitled";
        }

        var normalized = sourcePath.Replace('\\', '/');
        normalized = Regex.Replace(normalized, @"\.[^.\/]+$", string.Empty, RegexOptions.CultureInvariant);
        normalized = normalized.Replace('/', '-');
        return normalized;
    }
}

