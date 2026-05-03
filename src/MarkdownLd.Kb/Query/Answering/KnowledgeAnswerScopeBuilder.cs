using ManagedCode.MarkdownLd.Kb.Pipeline;
using PipelineMarkdownDocument = ManagedCode.MarkdownLd.Kb.Pipeline.MarkdownDocument;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerScopeBuilder
{
    public static KnowledgeGraphRankedSearchOptions CreateSearchOptions(
        KnowledgeAnswerRequest request,
        IReadOnlyList<PipelineMarkdownDocument> documents,
        KnowledgeGraphSnapshot snapshot)
    {
        return HasSourceScope(request)
            ? request.SearchOptions with
            {
                CandidateNodeIds = IntersectCandidateNodeIds(
                    request.SearchOptions.CandidateNodeIds,
                    CreateAllowedCandidateNodeIds(request, documents, snapshot)),
            }
            : request.SearchOptions;
    }

    private static bool HasSourceScope(KnowledgeAnswerRequest request)
    {
        return request.AllowedSourcePaths.Count > 0 ||
               request.AllowedDocumentUris.Count > 0;
    }

    private static IReadOnlyCollection<string> CreateAllowedCandidateNodeIds(
        KnowledgeAnswerRequest request,
        IReadOnlyList<PipelineMarkdownDocument> documents,
        KnowledgeGraphSnapshot snapshot)
    {
        var allowedDocumentUris = ResolveAllowedDocumentUris(request, documents);
        var allowedNodeIds = new HashSet<string>(allowedDocumentUris, StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges.Where(edge =>
                     edge.PredicateId == KnowledgeAnsweringConstants.ProvenanceWasDerivedFromPredicate &&
                     allowedDocumentUris.Contains(edge.ObjectId)))
        {
            allowedNodeIds.Add(edge.SubjectId);
        }

        return allowedNodeIds;
    }

    private static IReadOnlyCollection<string> IntersectCandidateNodeIds(
        IReadOnlyCollection<string>? existingCandidateNodeIds,
        IReadOnlyCollection<string> scopedCandidateNodeIds)
    {
        if (existingCandidateNodeIds is null)
        {
            return scopedCandidateNodeIds;
        }

        var scoped = scopedCandidateNodeIds.ToHashSet(StringComparer.Ordinal);
        return existingCandidateNodeIds
            .Where(scoped.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> ResolveAllowedDocumentUris(
        KnowledgeAnswerRequest request,
        IReadOnlyList<PipelineMarkdownDocument> documents)
    {
        var sourcePaths = request.AllowedSourcePaths
            .Select(KnowledgeNaming.NormalizeSourcePath)
            .ToHashSet(StringComparer.Ordinal);
        var documentUris = request.AllowedDocumentUris
            .Select(static uri => uri.Trim())
            .ToHashSet(StringComparer.Ordinal);

        return documents
            .Where(document => IsAllowedDocument(document, sourcePaths, documentUris))
            .Select(static document => document.DocumentUri.AbsoluteUri)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsAllowedDocument(
        PipelineMarkdownDocument document,
        IReadOnlySet<string> sourcePaths,
        IReadOnlySet<string> documentUris)
    {
        return sourcePaths.Contains(KnowledgeNaming.NormalizeSourcePath(document.SourcePath)) ||
               documentUris.Contains(document.DocumentUri.AbsoluteUri);
    }
}
