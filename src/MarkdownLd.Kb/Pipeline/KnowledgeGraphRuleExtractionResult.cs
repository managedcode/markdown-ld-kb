namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed record KnowledgeGraphRuleExtractionResult(
    KnowledgeExtractionResult Facts,
    IReadOnlyList<string> Diagnostics);
