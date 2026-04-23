namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public enum KnowledgeGraphFileFormat
{
    Turtle,
    JsonLd,
    RdfXml,
    NTriples,
    Notation3,
    TriG,
    NQuads,
}

public sealed record KnowledgeGraphFilePersistenceOptions
{
    public static KnowledgeGraphFilePersistenceOptions Default { get; } = new();

    public KnowledgeGraphFileFormat? Format { get; init; }
}

public sealed record KnowledgeGraphLoadOptions
{
    public static KnowledgeGraphLoadOptions Default { get; } = new();

    public KnowledgeGraphFileFormat? Format { get; init; }

    public string SearchPattern { get; init; } = "*.*";

    public SearchOption SearchOption { get; init; } = SearchOption.AllDirectories;

    public bool SkipUnsupportedFiles { get; init; } = true;
}
