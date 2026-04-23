namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public enum KnowledgeGraphFullTextIndexTarget
{
    Subjects,
    Objects,
    Predicates,
}

public sealed record KnowledgeGraphFullTextIndexOptions
{
    public static KnowledgeGraphFullTextIndexOptions Default { get; } = new();

    public KnowledgeGraphFullTextIndexTarget Target { get; init; } = KnowledgeGraphFullTextIndexTarget.Subjects;

    public string? DirectoryPath { get; init; }

    public bool AutoSync { get; init; }
}

public sealed record KnowledgeGraphFullTextMatch(
    string NodeId,
    string Label,
    double Score);
