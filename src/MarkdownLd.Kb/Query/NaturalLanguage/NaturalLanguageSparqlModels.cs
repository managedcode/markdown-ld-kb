using PipelineSparqlQueryResult = ManagedCode.MarkdownLd.Kb.Pipeline.SparqlQueryResult;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record NaturalLanguageSparqlTranslation(
    string Question,
    string QueryText,
    NaturalLanguageSparqlQueryKind QueryKind);

public sealed record NaturalLanguageSparqlExecutionResult(
    NaturalLanguageSparqlTranslation Translation,
    PipelineSparqlQueryResult? SelectResult,
    bool? AskResult);

public enum NaturalLanguageSparqlQueryKind
{
    Select,
    Ask,
}
