using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownKnowledgeScanner
{
    private static readonly Regex HeadingRegex = new(@"^\s*#\s+(?<title>.+?)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WikilinkRegex = new(@"\[\[(?<label>[^\[\]\|]+)(?:\|(?<alias>[^\[\]]+))?\]\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<label>[^\]]+)\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ArrowRegex = new(@"(?<subject>[^-\r\n]+?)\s*--(?<predicate>[^>\r\n]+?)-->\s*(?<object>.+?)(?=$|\s{2,}|[.!?;,])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static MarkdownKnowledgeScanResult Scan(string markdown)
    {
        var entities = new List<MarkdownKnowledgeEntityCandidate>();
        var assertions = new List<MarkdownKnowledgeAssertionCandidate>();
        string? title = null;

        foreach (var line in EnumerateContentLines(markdown))
        {
            title ??= TryReadHeading(line);
            AddArrowAssertions(line, assertions);
            AddWikilinkEntities(line, entities);
            AddMarkdownLinkEntities(line, entities);
        }

        return new MarkdownKnowledgeScanResult(title, entities, assertions);
    }

    private static IEnumerable<string> EnumerateContentLines(string markdown)
    {
        using var reader = new StringReader(markdown.Replace("\r\n", "\n"));
        var inFence = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.TrimStart();
            if (IsFenceMarker(trimmed))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence)
            {
                yield return line;
            }
        }
    }

    private static bool IsFenceMarker(string line)
    {
        return line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static string? TryReadHeading(string line)
    {
        var match = HeadingRegex.Match(line);
        return match.Success ? NormalizeSurfaceText(match.Groups["title"].Value) : null;
    }

    private static void AddWikilinkEntities(string line, ICollection<MarkdownKnowledgeEntityCandidate> entities)
    {
        foreach (Match match in WikilinkRegex.Matches(line))
        {
            var label = NormalizeSurfaceText(match.Groups["label"].Value);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            entities.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = label,
                Type = "schema:Thing",
                SameAs = [],
                SourceKind = "wikilink",
            });
        }
    }

    private static void AddMarkdownLinkEntities(string line, ICollection<MarkdownKnowledgeEntityCandidate> entities)
    {
        foreach (Match match in MarkdownLinkRegex.Matches(line))
        {
            var label = NormalizeSurfaceText(match.Groups["label"].Value);
            var target = NormalizeSurfaceText(match.Groups["target"].Value);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            entities.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = label,
                Type = "schema:Thing",
                SameAs = string.IsNullOrWhiteSpace(target) ? [] : [target],
                SourceKind = "markdown-link",
            });
        }
    }

    private static void AddArrowAssertions(string line, ICollection<MarkdownKnowledgeAssertionCandidate> assertions)
    {
        foreach (Match match in ArrowRegex.Matches(line))
        {
            var subject = NormalizeSurfaceText(match.Groups["subject"].Value);
            var predicate = NormalizePredicate(match.Groups["predicate"].Value);
            var obj = NormalizeSurfaceText(match.Groups["object"].Value);

            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(predicate) || string.IsNullOrWhiteSpace(obj))
            {
                continue;
            }

            assertions.Add(new MarkdownKnowledgeAssertionCandidate
            {
                Subject = subject,
                Predicate = predicate,
                Object = obj,
                Confidence = 0.95,
                Source = "markdown-arrow",
            });
        }
    }

    private static string NormalizeSurfaceText(string value)
    {
        var text = value.Trim();
        text = text.Replace("[[", string.Empty, StringComparison.Ordinal).Replace("]]", string.Empty, StringComparison.Ordinal);
        text = text.Replace("`", string.Empty, StringComparison.Ordinal);
        text = text.Replace("*", string.Empty, StringComparison.Ordinal);
        text = text.Replace("_", " ", StringComparison.Ordinal);
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant);
        return text.Trim(' ', '\t', '.', ',', ';', ':', '!', '?', ')', '(', '[', ']', '"', '\'');
    }

    private static string NormalizePredicate(string value)
    {
        var predicate = NormalizeSurfaceText(value);
        if (predicate.Contains("://", StringComparison.Ordinal) || predicate.Contains(':', StringComparison.Ordinal))
        {
            return predicate;
        }

        return $"schema:{MarkdownKnowledgeIds.Slugify(predicate)}";
    }
}

internal sealed record MarkdownKnowledgeScanResult(
    string? Title,
    IReadOnlyList<MarkdownKnowledgeEntityCandidate> Entities,
    IReadOnlyList<MarkdownKnowledgeAssertionCandidate> Assertions);

internal sealed record MarkdownKnowledgeEntityCandidate
{
    public required string Label { get; init; }

    public required string Type { get; init; }

    public IReadOnlyList<string> SameAs { get; init; } = [];

    public string SourceKind { get; init; } = "front-matter";
}

internal sealed record MarkdownKnowledgeAssertionCandidate
{
    public required string Subject { get; init; }

    public required string Predicate { get; init; }

    public required string Object { get; init; }

    public required double Confidence { get; init; }

    public required string Source { get; init; }
}
