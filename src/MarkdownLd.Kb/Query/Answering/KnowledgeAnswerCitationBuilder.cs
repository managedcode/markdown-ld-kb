using ManagedCode.MarkdownLd.Kb.Pipeline;
using PipelineMarkdownDocument = ManagedCode.MarkdownLd.Kb.Pipeline.MarkdownDocument;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerCitationBuilder
{
    public static IReadOnlyList<KnowledgeAnswerCitation> Build(
        IReadOnlyList<PipelineMarkdownDocument> documents,
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches,
        string searchQuery,
        int maxCitations,
        int maxSnippetLength)
    {
        var documentsByUri = KnowledgeAnswerDocumentSelector.CreateLookup(documents);
        var sourceBySubject = CreateSourceLookup(snapshot, documentsByUri);
        var citations = new List<KnowledgeAnswerCitation>(maxCitations);
        var seenDocumentUris = new HashSet<string>(StringComparer.Ordinal);

        foreach (var match in matches)
        {
            if (citations.Count >= maxCitations)
            {
                break;
            }

            var citation = CreateCitationOrDefault(
                match,
                searchQuery,
                maxSnippetLength,
                documentsByUri,
                sourceBySubject,
                citations.Count + 1);
            if (citation is not null && seenDocumentUris.Add(citation.DocumentUri))
            {
                citations.Add(citation);
            }
        }

        return citations;
    }

    private static IReadOnlyDictionary<string, string[]> CreateSourceLookup(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyDictionary<string, PipelineMarkdownDocument[]> documentsByUri)
    {
        return snapshot.Edges
            .Where(edge => edge.PredicateId == KnowledgeAnsweringConstants.ProvenanceWasDerivedFromPredicate && documentsByUri.ContainsKey(edge.ObjectId))
            .GroupBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static edge => edge.ObjectId)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private static KnowledgeAnswerCitation? CreateCitationOrDefault(
        KnowledgeGraphRankedSearchMatch match,
        string searchQuery,
        int maxSnippetLength,
        IReadOnlyDictionary<string, PipelineMarkdownDocument[]> documentsByUri,
        IReadOnlyDictionary<string, string[]> sourceBySubject,
        int citationIndex)
    {
        var selection = KnowledgeAnswerDocumentSelector.ResolveSelection(
            match,
            searchQuery,
            documentsByUri,
            sourceBySubject);
        if (selection is null)
        {
            return null;
        }

        return new KnowledgeAnswerCitation(
            citationIndex,
            selection.Document.DocumentUri.AbsoluteUri,
            selection.Document.SourcePath,
            selection.Chunk?.HeadingPath ?? Array.Empty<string>(),
            KnowledgeAnswerSnippetBuilder.Create(
                ResolveSnippetSource(selection, searchQuery, match),
                searchQuery,
                match,
                maxSnippetLength),
            match.NodeId,
            match.Label,
            match.Score,
            match.Source);
    }

    private static string ResolveSnippetSource(
        CitationDocumentSelection selection,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match)
    {
        var descriptionScore = ScoreEvidence(match.Description, searchQuery, match);
        var labelScore = ScoreEvidence(match.Label, searchQuery, match);
        if (ShouldUseChunk(selection, descriptionScore, labelScore, match))
        {
            return selection.Chunk!.Markdown;
        }

        var bodyScore = ScoreBodyEvidence(selection.Document, searchQuery, match);
        if (bodyScore > descriptionScore && bodyScore > labelScore)
        {
            return selection.Document.Body;
        }

        if (KnowledgeAnswerEvidenceScorer.HasContextOverlap(selection.Document.Body, match.Description))
        {
            return selection.Document.Body;
        }

        if (descriptionScore >= labelScore &&
            descriptionScore > KnowledgeAnsweringConstants.NoSnippetAnchorScore)
        {
            return match.Description!;
        }

        return match.Label;
    }

    private static bool ShouldUseChunk(
        CitationDocumentSelection selection,
        int descriptionScore,
        int labelScore,
        KnowledgeGraphRankedSearchMatch match)
    {
        return !string.IsNullOrWhiteSpace(selection.Chunk?.Markdown) &&
               (selection.Score >= descriptionScore && selection.Score >= labelScore ||
                KnowledgeAnswerEvidenceScorer.HasContextOverlap(selection.Chunk.Markdown, match.Description));
    }

    private static int ScoreBodyEvidence(
        PipelineMarkdownDocument document,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match) =>
        document.Chunks.Count == 0
            ? ScoreEvidence(document.Body, searchQuery, match)
            : KnowledgeAnsweringConstants.NoSnippetAnchorScore;

    private static int ScoreEvidence(
        string? text,
        string searchQuery,
        KnowledgeGraphRankedSearchMatch match) =>
        KnowledgeAnswerEvidenceScorer.Score(text, searchQuery, match);
}
