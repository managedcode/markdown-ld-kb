using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphSemanticIndex
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IReadOnlyList<KnowledgeGraphSemanticEntry> _entries;

    internal KnowledgeGraphSemanticIndex(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IReadOnlyList<KnowledgeGraphSemanticEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(embeddingGenerator);
        ArgumentNullException.ThrowIfNull(entries);

        _embeddingGenerator = embeddingGenerator;
        _entries = entries;
    }

    public int Count => _entries.Count;

    internal static async Task<KnowledgeGraphSemanticIndex> CreateAsync(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(embeddingGenerator);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateTexts = candidates.Select(static candidate => candidate.SearchText).ToArray();
        var embeddings = await embeddingGenerator.GenerateAsync(
                candidateTexts,
                new EmbeddingGenerationOptions(),
                cancellationToken)
            .ConfigureAwait(false);

        if (embeddings.Count != candidates.Count)
        {
            throw new InvalidOperationException(SemanticSearchEmbeddingCountMismatchMessage);
        }

        var entries = new KnowledgeGraphSemanticEntry[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            entries[index] = new KnowledgeGraphSemanticEntry(
                candidates[index].NodeId,
                candidates[index].Label,
                candidates[index].Description,
                embeddings[index].Vector.ToArray());
        }

        return new KnowledgeGraphSemanticIndex(embeddingGenerator, entries);
    }

    internal async Task<IReadOnlyList<KnowledgeGraphRankedSearchMatch>> SearchAsync(
        string query,
        int limit,
        double minimumSemanticScore,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = await _embeddingGenerator.GenerateAsync(
                [query],
                new EmbeddingGenerationOptions(),
                cancellationToken)
            .ConfigureAwait(false);

        if (embeddings.Count != 1)
        {
            throw new InvalidOperationException(SemanticSearchEmbeddingCountMismatchMessage);
        }

        var queryVector = embeddings[0].Vector.ToArray();
        return _entries
            .Select(entry => CreateSemanticMatch(entry, queryVector))
            .Where(match => match.Score >= minimumSemanticScore)
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static KnowledgeGraphRankedSearchMatch CreateSemanticMatch(
        KnowledgeGraphSemanticEntry entry,
        float[] queryVector)
    {
        var score = ComputeCosineSimilarity(queryVector, entry.Vector);
        return new KnowledgeGraphRankedSearchMatch(
            entry.NodeId,
            entry.Label,
            entry.Description,
            KnowledgeGraphRankedSearchSource.Semantic,
            score,
            SemanticScore: score);
    }

    private static double ComputeCosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return ZeroConfidence;
        }

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= ZeroConfidence || rightMagnitude <= ZeroConfidence)
        {
            return ZeroConfidence;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}

internal sealed record KnowledgeGraphSemanticEntry(
    string NodeId,
    string Label,
    string? Description,
    float[] Vector);
