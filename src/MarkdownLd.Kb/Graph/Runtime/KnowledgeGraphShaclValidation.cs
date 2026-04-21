namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphShaclValidationReport(
    bool Conforms,
    IReadOnlyList<KnowledgeGraphShaclValidationIssue> Results,
    string ReportTurtle);

public sealed record KnowledgeGraphShaclValidationIssue(
    string Severity,
    string FocusNode,
    string Value,
    string SourceShape,
    string ResultPath,
    string SourceConstraintComponent,
    string Message);
