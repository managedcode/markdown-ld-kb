namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactExtractionResult(
    string DocumentId,
    string ChunkId,
    IReadOnlyList<KnowledgeFactEntity> Entities,
    IReadOnlyList<KnowledgeFactAssertion> Assertions,
    string RawResponse);
