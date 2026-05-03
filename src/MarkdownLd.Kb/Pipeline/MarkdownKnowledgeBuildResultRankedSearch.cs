using System.Text;
using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class MarkdownKnowledgeBuildResultRankedSearch
{
    public static IReadOnlyList<KnowledgeGraphSearchCandidate> CreateDocumentAwareCandidates(
        MarkdownKnowledgeBuildResult buildResult)
    {
        ArgumentNullException.ThrowIfNull(buildResult);

        var documentsByUri = CreateDocumentLookup(buildResult.Documents);

        return KnowledgeGraph.CreateSearchCandidates(buildResult.Graph.ToSnapshot())
            .Select(candidate => documentsByUri.TryGetValue(candidate.NodeId, out var documents)
                ? candidate with { SearchText = ComposeDocumentSearchText(candidate.SearchText, documents) }
                : candidate)
            .ToArray();
    }

    public static Task<IReadOnlyList<KnowledgeGraphRankedSearchMatch>> SearchRankedAsync(
        MarkdownKnowledgeBuildResult buildResult,
        string query,
        KnowledgeGraphRankedSearchOptions? options,
        KnowledgeGraphSemanticIndex? semanticIndex,
        CancellationToken cancellationToken)
    {
        var candidates = CreateDocumentAwareCandidates(buildResult);
        return KnowledgeGraph.SearchRankedCandidatesAsync(
            query,
            candidates,
            options,
            semanticIndex,
            cancellationToken);
    }

    public static Task<KnowledgeGraphSemanticIndex> BuildSemanticIndexAsync(
        MarkdownKnowledgeBuildResult buildResult,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken)
    {
        var candidates = CreateDocumentAwareCandidates(buildResult);
        return KnowledgeGraphSemanticIndex.CreateAsync(candidates, embeddingGenerator, cancellationToken);
    }

    private static string ComposeDocumentSearchText(
        string graphSearchText,
        IReadOnlyList<MarkdownDocument> documents)
    {
        var builder = new StringBuilder(graphSearchText);
        foreach (var document in documents)
        {
            AppendDocumentSearchText(builder, document);
        }

        return builder.ToString();
    }

    private static void AppendDocumentSearchText(StringBuilder builder, MarkdownDocument document)
    {
        if (document.Chunks.Count == 0)
        {
            AppendSearchText(builder, document.Body);
            return;
        }

        foreach (var chunk in document.Chunks)
        {
            foreach (var heading in chunk.HeadingPath)
            {
                AppendSearchText(builder, heading);
            }

            AppendSearchText(builder, chunk.Markdown);
        }
    }

    private static void AppendSearchText(StringBuilder builder, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(text.Trim());
    }

    private static IReadOnlyDictionary<string, MarkdownDocument[]> CreateDocumentLookup(
        IReadOnlyList<MarkdownDocument> documents)
    {
        return documents
            .GroupBy(static document => document.DocumentUri.AbsoluteUri, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
    }
}
