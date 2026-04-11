namespace ManagedCode.MarkdownLd.Kb.Rdf;

public sealed record KnowledgeArticle(
    Uri Id,
    string Title,
    DateOnly? DatePublished = null,
    DateOnly? DateModified = null,
    IReadOnlyList<string>? Tags = null,
    string? Summary = null);

public sealed record KnowledgeEntity(
    Uri Id,
    string Label,
    Uri Type,
    IReadOnlyList<Uri>? SameAs = null);

public sealed record KnowledgeAssertion(
    Uri Subject,
    Uri Predicate,
    Uri Object,
    decimal? Confidence = null,
    Uri? Source = null,
    Uri? Chunk = null,
    string? DocPath = null,
    int? CharStart = null,
    int? CharEnd = null);

public sealed record KnowledgeGraphDocument(
    KnowledgeArticle Article,
    IReadOnlyList<KnowledgeEntity> Entities,
    IReadOnlyList<KnowledgeAssertion> Assertions);
