using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerEvidenceScorer
{
    public static int Score(
        string? text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        return KnowledgeAnswerSnippetBuilder.ScoreEvidenceText(text, searchQuery, match);
    }

    public static bool HasContextOverlap(string? text, string? context)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        var contextTokens = Tokenize(context).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (contextTokens.Count == 0)
        {
            return false;
        }

        return Tokenize(text)
            .Where(contextTokens.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(KnowledgeAnsweringConstants.MinimumContextOverlapTokenCount)
            .Count() >= KnowledgeAnsweringConstants.MinimumContextOverlapTokenCount;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return text
            .Split(
                KnowledgeAnsweringConstants.SnippetAnchorSeparators,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static token => token.Length >= KnowledgeAnsweringConstants.MinimumContextOverlapTokenLength);
    }
}
