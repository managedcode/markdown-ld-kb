namespace ManagedCode.MarkdownLd.Kb;

internal static class KnowledgeFactConstants
{
    internal const string DefaultGraphBaseUrl = MarkdownKnowledgeDefaults.BaseUriText;
    internal const string EntityPathSegment = "/id/";
    internal const string SchemaPerson = "schema:Person";
    internal const string SchemaOrganization = "schema:Organization";
    internal const string SchemaSoftwareApplication = "schema:SoftwareApplication";
    internal const string SchemaCreativeWork = "schema:CreativeWork";
    internal const string SchemaArticle = "schema:Article";
    internal const string SchemaThing = "schema:Thing";

    internal const string ArticleIdLabel = "ARTICLE_ID: ";
    internal const string ChunkIdLabel = "CHUNK_ID: ";
    internal const string ChunkSourceLabel = "CHUNK_SOURCE: ";
    internal const string TitleLabel = "TITLE: ";
    internal const string SectionPathLabel = "SECTION_PATH: ";
    internal const string FrontMatterLabel = "FRONT_MATTER:";
    internal const string FrontMatterItemPrefix = "- ";
    internal const string KeyValueSeparator = ": ";
    internal const string ExtractInstruction = "Extract entities and assertions from the markdown below.";
    internal const string ExplicitFactsInstruction = "Return only facts that are explicit or strongly implied.";
    internal const string ArticleIdInstruction = "Use <ARTICLE_ID> for assertions whose subject is the article itself.";
    internal const string MarkdownLabel = "MARKDOWN:";
    internal const string ChunkSourcePrefix = "urn:kb:chunk:";
    internal const string ChunkSourceSeparator = ":";
    internal const string ArticleIdPlaceholder = "<ARTICLE_ID>";
    internal const string ArticlePlaceholder = "<ARTICLE>";
    internal const string ArticleIdToken = "ARTICLE_ID";
    internal const string ArticleToken = "ARTICLE";
    internal const string BracedArticleIdToken = "{ARTICLE_ID}";

    internal const string EntitiesJsonName = "entities";
    internal const string AssertionsJsonName = "assertions";
    internal const string EntityIdJsonName = "id";
    internal const string EntityTypeJsonName = "type";
    internal const string EntityLabelJsonName = "label";
    internal const string EntitySameAsJsonName = "sameAs";
    internal const string AssertionSubjectJsonName = "s";
    internal const string AssertionPredicateJsonName = "p";
    internal const string AssertionObjectJsonName = "o";
    internal const string AssertionConfidenceJsonName = "confidence";
    internal const string AssertionSourceJsonName = "source";

    internal const string DefaultSystemPrompt = """
SYSTEM:
You are a deterministic RDF extraction engine. Output must be valid JSON and match the structured output schema.

ONTOLOGY:
- Types: schema:Article, schema:Person, schema:Organization, schema:SoftwareApplication, schema:CreativeWork, schema:Thing
- Preferred properties: schema:about, schema:mentions, schema:sameAs, schema:author, schema:creator, schema:datePublished, schema:dateModified
- Provenance: prov:wasDerivedFrom (use chunk URI)
- Confidence: kb:confidence (0..1)

RULES:
- Only add relations explicitly stated or strongly implied by the text.
- Prefer schema.org properties.
- If the predicate cannot be named explicitly, omit the assertion.
- Every entity must have id, type, and label.
- Use stable ids for entities.
- If a wikilink or entity hint provides sameAs, include it.
- Emit no duplicate entities or assertions.
- Use <ARTICLE_ID> when the article itself is the assertion subject.

OUTPUT JSON SCHEMA:
{
  "entities": [
    { "id": "...", "type": "schema:Thing", "label": "...", "sameAs": ["..."] }
  ],
  "assertions": [
    { "s": "...", "p": "schema:mentions", "o": "...", "confidence": 0.85, "source": "urn:kb:chunk:..." }
  ]
}
""";
}
