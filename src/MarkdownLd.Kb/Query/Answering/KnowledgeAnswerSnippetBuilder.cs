using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerSnippetBuilder
{
    public static int ScoreEvidenceText(
        string? text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return KnowledgeAnsweringConstants.NoSnippetAnchorScore;
        }

        var anchorMatch = FindBestAnchorMatch(text, searchQuery, match);
        return anchorMatch is null
            ? KnowledgeAnsweringConstants.NoSnippetAnchorScore
            : (anchorMatch.Anchor.Priority * KnowledgeAnsweringConstants.SnippetAnchorPriorityMultiplier) +
              anchorMatch.Anchor.Text.Length;
    }

    public static string Create(
        string text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match,
        int maxSnippetLength)
    {
        var trimmed = text.Trim();
        if (trimmed.Length <= maxSnippetLength)
        {
            return trimmed;
        }

        var start = FindSnippetStart(trimmed, searchQuery, match, maxSnippetLength);
        return CreateBoundedWindow(trimmed, start, maxSnippetLength);
    }

    private static int FindSnippetStart(
        string text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match,
        int maxSnippetLength)
    {
        var anchorIndex = FindSnippetAnchor(text, searchQuery, match);
        if (anchorIndex < 0)
        {
            return 0;
        }

        var context = maxSnippetLength / KnowledgeAnsweringConstants.SnippetContextDivisor;
        var latestStart = Math.Max(0, text.Length - maxSnippetLength);
        return Math.Min(Math.Max(0, anchorIndex - context), latestStart);
    }

    private static int FindSnippetAnchor(
        string text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        return FindBestAnchorMatch(text, searchQuery, match)?.Index ?? -1;
    }

    private static SnippetAnchorMatch? FindBestAnchorMatch(
        string text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        SnippetAnchorMatch? bestMatch = null;
        foreach (var anchor in EnumerateSnippetAnchors(searchQuery, match))
        {
            var index = text.IndexOf(anchor.Text, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var candidate = new SnippetAnchorMatch(anchor, index);
            bestMatch = IsBetterAnchorMatch(candidate, bestMatch) ? candidate : bestMatch;
        }

        return bestMatch;
    }

    private static bool IsBetterAnchorMatch(SnippetAnchorMatch candidate, SnippetAnchorMatch? current)
    {
        return current is null ||
               candidate.Anchor.Priority > current.Anchor.Priority ||
               (candidate.Anchor.Priority == current.Anchor.Priority &&
                candidate.Anchor.Text.Length > current.Anchor.Text.Length) ||
               (candidate.Anchor.Priority == current.Anchor.Priority &&
                candidate.Anchor.Text.Length == current.Anchor.Text.Length &&
                candidate.Index < current.Index);
    }

    private static IEnumerable<SnippetAnchor> EnumerateSnippetAnchors(
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        foreach (var anchor in EnumerateCandidateAnchors(searchQuery, match))
        {
            var trimmed = anchor.Text.Trim();
            if (trimmed.Length >= KnowledgeAnsweringConstants.MinimumSnippetAnchorLength)
            {
                yield return anchor with { Text = trimmed };
            }
        }
    }

    private static IEnumerable<SnippetAnchor> EnumerateCandidateAnchors(
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        yield return new SnippetAnchor(searchQuery, KnowledgeAnsweringConstants.ExactQuerySnippetAnchorPriority);
        foreach (var token in searchQuery.Split(
                     KnowledgeAnsweringConstants.SnippetAnchorSeparators,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return new SnippetAnchor(token, KnowledgeAnsweringConstants.QueryTokenSnippetAnchorPriority);
        }

        if (!string.IsNullOrWhiteSpace(match.Label))
        {
            yield return new SnippetAnchor(match.Label, KnowledgeAnsweringConstants.LabelSnippetAnchorPriority);
        }

        if (!string.IsNullOrWhiteSpace(match.Description))
        {
            yield return new SnippetAnchor(match.Description, KnowledgeAnsweringConstants.DescriptionSnippetAnchorPriority);
        }
    }

    private static string CreateBoundedWindow(string text, int start, int maxSnippetLength)
    {
        var marker = KnowledgeAnsweringConstants.SnippetTruncationSuffix;
        var includePrefix = start > 0 && CanReserveMarker(maxSnippetLength, marker.Length);
        var prefixLength = includePrefix ? marker.Length : 0;
        var contentLimit = maxSnippetLength - prefixLength;
        var end = Math.Min(text.Length, start + contentLimit);
        var includeSuffix = end < text.Length && CanReserveMarker(contentLimit, marker.Length);
        if (includeSuffix)
        {
            contentLimit -= marker.Length;
            end = Math.Min(text.Length, start + contentLimit);
        }

        var snippet = text.Substring(start, end - start).Trim();
        return string.Concat(
            includePrefix ? marker : string.Empty,
            snippet,
            includeSuffix ? marker : string.Empty);
    }

    private static bool CanReserveMarker(int availableLength, int markerLength)
    {
        return availableLength > markerLength + KnowledgeAnsweringConstants.MinimumSnippetContentLength;
    }
}

internal sealed record SnippetAnchor(string Text, int Priority);

internal sealed record SnippetAnchorMatch(SnippetAnchor Anchor, int Index);
