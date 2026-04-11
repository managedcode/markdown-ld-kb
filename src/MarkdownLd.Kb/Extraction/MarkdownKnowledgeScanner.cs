using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Extraction.MarkdownKnowledgeConstants;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownKnowledgeScanner
{
    private static readonly Regex HeadingRegex = new(HeadingPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WikilinkRegex = new(WikiLinkPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkRegex = new(MarkdownLinkPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ArrowRegex = new(ArrowPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        using var reader = new StringReader(markdown.Replace(CarriageReturnLineFeed, LineFeed));
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
        return line.StartsWith(CodeFenceBacktick, StringComparison.Ordinal) || line.StartsWith(CodeFenceTilde, StringComparison.Ordinal);
    }

    private static string? TryReadHeading(string line)
    {
        var match = HeadingRegex.Match(line);
        return match.Success ? NormalizeSurfaceText(match.Groups[HeadingGroup].Value) : null;
    }

    private static void AddWikilinkEntities(string line, ICollection<MarkdownKnowledgeEntityCandidate> entities)
    {
        foreach (Match match in WikilinkRegex.Matches(line))
        {
            var target = NormalizeSurfaceText(match.Groups[TargetGroup].Value);
            var alias = NormalizeSurfaceText(match.Groups[AliasGroup].Value);

            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            entities.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = target,
                Type = SchemaThing,
                SameAs = string.IsNullOrWhiteSpace(alias) || string.Equals(alias, target, StringComparison.OrdinalIgnoreCase)
                    ? []
                    : [alias],
                SourceKind = WikiLinkSource,
            });
        }
    }

    private static void AddMarkdownLinkEntities(string line, ICollection<MarkdownKnowledgeEntityCandidate> entities)
    {
        foreach (Match match in MarkdownLinkRegex.Matches(line))
        {
            var label = NormalizeSurfaceText(match.Groups[LabelGroup].Value);
            var target = NormalizeSurfaceText(match.Groups[TargetGroup].Value);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            entities.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = label,
                Type = SchemaThing,
                SameAs = string.IsNullOrWhiteSpace(target) ? [] : [target],
                SourceKind = MarkdownLinkSource,
            });
        }
    }

    private static void AddArrowAssertions(string line, ICollection<MarkdownKnowledgeAssertionCandidate> assertions)
    {
        foreach (Match match in ArrowRegex.Matches(line))
        {
            var subject = NormalizeSurfaceText(match.Groups[SubjectGroup].Value);
            var predicate = NormalizePredicate(match.Groups[PredicateGroup].Value);
            var obj = NormalizeSurfaceText(match.Groups[ObjectGroup].Value);

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
                Source = MarkdownArrowSource,
            });
        }
    }

    private static string NormalizeSurfaceText(string value)
    {
        var text = value.Trim();
        text = text.Replace(WikiLinkStart, string.Empty, StringComparison.Ordinal).Replace(WikiLinkEnd, string.Empty, StringComparison.Ordinal);
        text = text.Replace(InlineCodeMarker, string.Empty, StringComparison.Ordinal);
        text = text.Replace(EmphasisMarker, string.Empty, StringComparison.Ordinal);
        text = text.Replace(Underscore, Space, StringComparison.Ordinal);
        text = Regex.Replace(text, WhitespacePattern, Space, RegexOptions.CultureInvariant);
        return text.Trim(' ', '\t', '.', ',', ';', ':', '!', '?', ')', '(', '[', ']', '"', '\'');
    }

    private static string NormalizePredicate(string value)
    {
        var predicate = NormalizeSurfaceText(value);
        if (predicate.Contains(UriSchemeSeparator, StringComparison.Ordinal) || predicate.Contains(':', StringComparison.Ordinal))
        {
            return predicate;
        }

        return string.Concat(SchemaPrefix, MarkdownKnowledgeIds.Slugify(predicate));
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

    public string SourceKind { get; init; } = FrontMatterSource;
}

internal sealed record MarkdownKnowledgeAssertionCandidate
{
    public required string Subject { get; init; }

    public required string Predicate { get; init; }

    public required string Object { get; init; }

    public required double Confidence { get; init; }

    public required string Source { get; init; }
}
