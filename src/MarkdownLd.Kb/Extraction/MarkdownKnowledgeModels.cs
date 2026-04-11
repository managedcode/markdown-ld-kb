namespace ManagedCode.MarkdownLd.Kb.Extraction;

public sealed record MarkdownKnowledgeExtractionResult
{
    public required MarkdownArticleMetadata Article { get; init; }

    public IReadOnlyList<MarkdownKnowledgeEntity> Entities { get; init; } = [];

    public IReadOnlyList<MarkdownKnowledgeAssertion> Assertions { get; init; } = [];
}

public sealed record MarkdownArticleMetadata
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    public string? CanonicalUrl { get; init; }

    public string? DatePublished { get; init; }

    public string? DateModified { get; init; }

    public IReadOnlyList<MarkdownAuthor> Authors { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<MarkdownTopic> About { get; init; } = [];
}

public sealed record MarkdownAuthor
{
    public required string Name { get; init; }

    public string? SameAs { get; init; }

    public string? Type { get; init; }
}

public sealed record MarkdownTopic
{
    public required string Label { get; init; }

    public string? SameAs { get; init; }
}

public sealed record MarkdownEntityHint
{
    public required string Label { get; init; }

    public string? SameAs { get; init; }

    public string? Type { get; init; }
}

public sealed record MarkdownKnowledgeEntity
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string Type { get; init; }

    public IReadOnlyList<string> SameAs { get; init; } = [];
}

public sealed record MarkdownKnowledgeAssertion
{
    public required string SubjectId { get; init; }

    public required string Predicate { get; init; }

    public required string ObjectId { get; init; }

    public required double Confidence { get; init; }

    public required string Source { get; init; }
}

public sealed record MarkdownFrontMatter
{
    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? CanonicalUrl { get; init; }

    public string? DatePublished { get; init; }

    public string? DateModified { get; init; }

    public IReadOnlyList<MarkdownAuthor> Authors { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<MarkdownTopic> About { get; init; } = [];

    public IReadOnlyList<MarkdownEntityHint> EntityHints { get; init; } = [];
}

