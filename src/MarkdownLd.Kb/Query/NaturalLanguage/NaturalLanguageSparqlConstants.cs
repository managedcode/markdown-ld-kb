namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class NaturalLanguageSparqlConstants
{
    internal const string CodeFence = "```";
    internal const string SparqlFence = "```sparql";
    internal const string BulletPrefix = "- ";
    internal const string LineFeed = "\n";
    internal const string DoubleLineFeed = "\n\n";
    internal const string QueryPrefixSelect = "SELECT";
    internal const string QueryPrefixAsk = "ASK";
    internal const string SchemaSummaryLabel = "SCHEMA SUMMARY";
    internal const string TypesLabel = "TYPES:";
    internal const string PredicatesLabel = "PREDICATES:";
    internal const string QuestionLabel = "QUESTION:";
    internal const string InstructionLabel = "INSTRUCTIONS:";
    internal const string EmptySchemaPlaceholder = "- none";
    internal const string ReadOnlyFailureMessage = "NL-to-SPARQL translation must return a read-only SELECT or ASK query.";
    internal const string EmptyTranslationMessage = "NL-to-SPARQL translation returned an empty query.";
    internal const string DefaultSystemPrompt = """
You are a deterministic SPARQL translator for an in-memory RDF knowledge graph.

Rules:
- Return only a SPARQL query.
- Use only SELECT or ASK.
- Never emit INSERT, DELETE, LOAD, CLEAR, CREATE, DROP, MOVE, COPY, or ADD.
- Prefer schema, kb, prov, rdf, and xsd prefixes already present in the graph.
- Use only predicates and types that fit the provided schema summary.
- If the question cannot be answered safely, return a narrow SELECT that is still read-only.
""";
}
