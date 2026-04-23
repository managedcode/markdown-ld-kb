namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphInferenceOptions
{
    public static KnowledgeGraphInferenceOptions Default { get; } = new();

    public bool UseRdfsReasoner { get; init; } = true;

    public bool UseSkosReasoner { get; init; } = true;

    public IReadOnlyList<string> AdditionalSchemaFilePaths { get; init; } = [];

    public IReadOnlyList<string> AdditionalSchemaTexts { get; init; } = [];

    public IReadOnlyList<string> AdditionalN3RuleFilePaths { get; init; } = [];

    public IReadOnlyList<string> AdditionalN3RuleTexts { get; init; } = [];
}

public sealed record KnowledgeGraphInferenceResult(
    KnowledgeGraph Graph,
    int BaseTripleCount,
    int FinalTripleCount,
    IReadOnlyList<string> AppliedReasoners)
{
    public int InferredTripleCount => FinalTripleCount - BaseTripleCount;
}
