using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Query;

public interface INaturalLanguageSparqlTranslator
{
    Task<NaturalLanguageSparqlTranslation> TranslateAsync(
        KnowledgeGraph graph,
        string question,
        CancellationToken cancellationToken = default);
}
