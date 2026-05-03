using ManagedCode.MarkdownLd.Kb.Pipeline;
using PipelineMarkdownDocument = ManagedCode.MarkdownLd.Kb.Pipeline.MarkdownDocument;
using RootMarkdownChunk = ManagedCode.MarkdownLd.Kb.MarkdownChunk;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerDocumentSelector
{
    public static IReadOnlyDictionary<string, PipelineMarkdownDocument[]> CreateLookup(
        IReadOnlyList<PipelineMarkdownDocument> documents)
    {
        return documents
            .GroupBy(static document => document.DocumentUri.AbsoluteUri, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
    }

    public static CitationDocumentSelection? ResolveSelection(
        KnowledgeGraphRankedSearchMatch match,
        string searchQuery,
        IReadOnlyDictionary<string, PipelineMarkdownDocument[]> documentsByUri,
        IReadOnlyDictionary<string, string[]> sourceBySubject)
    {
        if (documentsByUri.TryGetValue(match.NodeId, out var directDocuments))
        {
            return SelectDocument(directDocuments, searchQuery, match);
        }

        if (sourceBySubject.TryGetValue(match.NodeId, out var sourceUris))
        {
            var sourceDocuments = ResolveSourceDocuments(sourceUris, documentsByUri);
            return sourceDocuments.Length == 0
                ? null
                : SelectDocument(sourceDocuments, searchQuery, match);
        }

        return null;
    }

    private static PipelineMarkdownDocument[] ResolveSourceDocuments(
        IReadOnlyList<string> sourceUris,
        IReadOnlyDictionary<string, PipelineMarkdownDocument[]> documentsByUri)
    {
        var documents = new List<PipelineMarkdownDocument>();
        foreach (var sourceUri in sourceUris)
        {
            if (documentsByUri.TryGetValue(sourceUri, out var sourceDocuments))
            {
                documents.AddRange(sourceDocuments);
            }
        }

        return documents.ToArray();
    }

    private static CitationDocumentSelection? SelectDocument(
        IReadOnlyList<PipelineMarkdownDocument> documents,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        return documents
            .Select((document, index) => CreateDocumentSelection(document, index, searchQuery, match))
            .OrderByDescending(static selection => selection.Score)
            .ThenBy(static selection => selection.Index)
            .FirstOrDefault();
    }

    private static CitationDocumentSelection CreateDocumentSelection(
        PipelineMarkdownDocument document,
        int index,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        var chunk = SelectChunk(document, searchQuery, match);
        var score = chunk?.Score ?? ScoreDocumentBody(document, searchQuery, match);
        return new CitationDocumentSelection(document, chunk?.Chunk, index, score);
    }

    private static int ScoreDocumentBody(
        PipelineMarkdownDocument document,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        return document.Chunks.Count == 0
            ? KnowledgeAnswerEvidenceScorer.Score(document.Body, searchQuery, match)
            : KnowledgeAnsweringConstants.NoSnippetAnchorScore;
    }

    private static ChunkCandidate? SelectChunk(
        PipelineMarkdownDocument document,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        return document.Chunks
            .Select((chunk, index) => new ChunkCandidate(
                chunk,
                index,
                KnowledgeAnswerEvidenceScorer.Score(chunk.Markdown, searchQuery, match)))
            .Where(static candidate => candidate.Score > KnowledgeAnsweringConstants.NoSnippetAnchorScore)
            .OrderByDescending(static candidate => candidate.Score)
            .ThenBy(static candidate => candidate.Index)
            .FirstOrDefault();
    }

    private sealed record ChunkCandidate(RootMarkdownChunk Chunk, int Index, int Score);
}

internal sealed record CitationDocumentSelection(
    PipelineMarkdownDocument Document,
    RootMarkdownChunk? Chunk,
    int Index,
    int Score);
