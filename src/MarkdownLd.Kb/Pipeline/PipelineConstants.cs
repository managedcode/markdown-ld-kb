using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class PipelineConstants
{
    internal const string DefaultBaseUriText = "https://example.com/";
    internal const string DefaultSchemaThing = "schema:Thing";
    internal const string DefaultKbRelatedTo = "kb:relatedTo";
    internal const string DefaultItem = "item";
    internal const string DefaultDocument = "document";
    internal const string EntityIdPrefix = "id/";
    internal const string ContentPrefix = "content/";
    internal const string PathSeparator = " / ";
    internal const string Slash = "/";
    internal const string Colon = ":";
    internal const string Hyphen = "-";
    internal const string Underscore = "_";
    internal const string EmptySparqlQueryMessage = "SPARQL query is empty";
    internal const string ReadOnlySparqlQueryMessage = "SPARQL query is not read-only.";
    internal const string ExpectedResultSetMessage = "Expected a SPARQL result set.";
    internal const string MutatingKeywordMessagePrefix = "Mutating keyword '";
    internal const string MutatingKeywordMessageSuffix = "' is not allowed";
    internal const string SelectAskOnlyMessagePrefix = "Only ASK and SELECT queries are allowed, not ";
    internal const string SchemaPrefix = "schema";
    internal const string KbPrefix = "kb";
    internal const string ProvPrefix = "prov";
    internal const string RdfPrefix = "rdf";
    internal const string XsdPrefix = "xsd";
    internal const string SchemaNamespaceText = "https://schema.org/";
    internal const string KbNamespaceText = "https://example.com/vocab/kb#";
    internal const string ProvNamespaceText = "http://www.w3.org/ns/prov#";
    internal const string RdfNamespaceText = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    internal const string XsdNamespaceText = "http://www.w3.org/2001/XMLSchema#";
    internal const string SchemaArticleText = "https://schema.org/Article";
    internal const string SchemaNameText = "https://schema.org/name";
    internal const string SchemaDescriptionText = "https://schema.org/description";
    internal const string SchemaDatePublishedText = "https://schema.org/datePublished";
    internal const string SchemaDateModifiedText = "https://schema.org/dateModified";
    internal const string SchemaKeywordsText = "https://schema.org/keywords";
    internal const string SchemaAboutText = "https://schema.org/about";
    internal const string SchemaAuthorText = "https://schema.org/author";
    internal const string SchemaSameAsText = "https://schema.org/sameAs";
    internal const string SchemaMentionsText = "https://schema.org/mentions";
    internal const string SchemaCreatorText = "https://schema.org/creator";
    internal const string SchemaArticleTypeText = "schema:Article";
    internal const string SchemaPersonTypeText = "schema:Person";
    internal const string SchemaOrganizationTypeText = "schema:Organization";
    internal const string SchemaSoftwareApplicationTypeText = "schema:SoftwareApplication";
    internal const string SchemaCreativeWorkTypeText = "schema:CreativeWork";
    internal const string SchemaThingTypeText = "schema:Thing";
    internal const string KbConfidenceText = "https://example.com/vocab/kb#confidence";
    internal const string RdfTypeText = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
    internal const string XsdDateText = "http://www.w3.org/2001/XMLSchema#date";
    internal const string XsdIntegerText = "http://www.w3.org/2001/XMLSchema#integer";
    internal const string TitleKey = "title";
    internal const string SummaryKey = "summary";
    internal const string DescriptionKey = "description";
    internal const string DatePublishedKey = "date_published";
    internal const string DatePublishedCamelKey = "datePublished";
    internal const string DateModifiedKey = "date_modified";
    internal const string DateModifiedCamelKey = "dateModified";
    internal const string TagsKey = "tags";
    internal const string KeywordsKey = "keywords";
    internal const string AboutKey = "about";
    internal const string AuthorKey = "author";
    internal const string EntityHintsKey = "entity_hints";
    internal const string LabelKey = "label";
    internal const string NameKey = "name";
    internal const string TypeKey = "type";
    internal const string SameAsKey = "sameAs";
    internal const string ArticleMarker = "article";
    internal const string ThisArticleMarker = "this article";
    internal const string MentionPredicateKey = "mentions";
    internal const string AboutPredicateKey = "about";
    internal const string AuthorPredicateKey = "author";
    internal const string CreatorPredicateKey = "creator";
    internal const string SameAsPredicateKey = "sameas";
    internal const string DescriptionPredicateKey = "description";
    internal const string KeywordsPredicateKey = "keywords";
    internal const string RelatedToPredicateKey = "relatedto";
    internal const string UriSchemePrefix = "urn:";
    internal const string BlankNodePrefix = "_:";
    internal const string AssertionKeySeparator = "||";
    internal const string SearchTermToken = "{TERM}";
    internal const string BackslashText = "\\";
    internal const string EscapedBackslashText = "\\\\";
    internal const string QuoteText = "\"";
    internal const string EscapedQuoteText = "\\\"";
    internal const string SearchQueryTemplate = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <https://example.com/vocab/kb#>
SELECT DISTINCT ?subject ?name ?type WHERE {
  ?subject a ?type .
  OPTIONAL { ?subject schema:name ?name . }
  OPTIONAL { ?subject schema:description ?description . }
  OPTIONAL { ?subject schema:keywords ?keyword . }
  FILTER(
    (BOUND(?name) && CONTAINS(LCASE(STR(?name)), LCASE("{TERM}"))) ||
    (BOUND(?description) && CONTAINS(LCASE(STR(?description)), LCASE("{TERM}"))) ||
    (BOUND(?keyword) && CONTAINS(LCASE(STR(?keyword)), LCASE("{TERM}")))
  )
}
LIMIT 100
""";
    internal const string SearchLimit = "100";
    internal const string SearchSubjectVariable = "?subject";
    internal const string SearchNameVariable = "?name";
    internal const string SearchTypeVariable = "?type";
    internal const string SearchDescriptionVariable = "?description";
    internal const string SearchKeywordVariable = "?keyword";
    internal const string SearchFilterPrefix = "FILTER(";
    internal const string SearchSelectPrefix = "SELECT DISTINCT ?subject ?name ?type WHERE {";
    internal const string SearchPrefixSchema = "PREFIX schema: <https://schema.org/>";
    internal const string SearchPrefixKb = "PREFIX kb: <https://example.com/vocab/kb#>";
    internal const string SearchOptionalName = "  OPTIONAL { ?subject schema:name ?name . }";
    internal const string SearchOptionalDescription = "  OPTIONAL { ?subject schema:description ?description . }";
    internal const string SearchOptionalKeyword = "  OPTIONAL { ?subject schema:keywords ?keyword . }";
    internal const string SearchFilterName = "(BOUND(?name) && CONTAINS(LCASE(STR(?name)), LCASE(\"{TERM}\")))";
    internal const string SearchFilterDescription = "(BOUND(?description) && CONTAINS(LCASE(STR(?description)), LCASE(\"{TERM}\")))";
    internal const string SearchFilterKeyword = "(BOUND(?keyword) && CONTAINS(LCASE(STR(?keyword)), LCASE(\"{TERM}\")))";
    internal const string SearchEnd = "LIMIT 100";
    internal const string QueryReadOnlySelectMessage = "Only ASK and SELECT queries are allowed, not ";
    internal const string ExpectedSchemaName = "schema:name";
    internal const string ExpectedSchemaDescription = "schema:description";
    internal const string ExpectedSchemaKeywords = "schema:keywords";
    internal const string ExpectedSchemaAbout = "schema:about";
    internal const string ExpectedSchemaAuthor = "schema:author";
    internal const string ExpectedSchemaMentions = "schema:mentions";
    internal const string ExpectedSchemaSameAs = "schema:sameAs";
    internal const string ExpectedSchemaCreator = "schema:creator";
    internal const string ExpectedRdfType = "rdf:type";
    internal const string ArrowSeparator = "--";
    internal const string ArrowTail = "-->";
    internal const string SectionHeaderPrefix = "SECTION: ";
    internal const string DocumentUriLabel = "DOCUMENT_URI: ";
    internal const string TitleLabel = "TITLE: ";
    internal const string BodyLabel = "BODY:";
    internal const string SectionsLabel = "SECTIONS:";
    internal const string ExtractPromptStart = "Extract knowledge facts from the Markdown document. ";
    internal const string ExtractPromptJson = "Return only JSON matching the requested structured output envelope. ";
    internal const string SpaceSlashSpace = " / ";
    internal const string BlankString = "";
    internal const string NonAlphaNumericPattern = @"[^a-z0-9\s-]";
    internal const string WhitespacePattern = @"[\s_]+";
    internal const string DashesPattern = @"-+";
    internal const string HeadingPattern = @"^(#{1,6})\s+(.+?)\s*$";
    internal const string WikiLinkPattern = @"\[\[([^\]]+)\]\]";
    internal const string MarkdownLinkPattern = @"\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)";
    internal const string ArrowPattern = @"^(?<subject>.+?)\s*--(?<predicate>[^-]+?)-->\s*(?<object>.+?)\s*$";
    internal const string MutatingKeywordPattern = @"\b(?:INSERT|DELETE|LOAD|CLEAR|DROP|CREATE|MOVE|COPY|ADD|WITH|MODIFY)\b";
    internal const string FrontMatterMarker = "---";
    internal const string NewLineDelimiter = "\n";
    internal const string CarriageReturn = "\r";
    internal const string LeftRightDashDelimiter = "-";
    internal const string ListItemPrefix = "- ";
    internal const string DotNetDateFormat = "yyyy-MM-dd";
    internal const string SpaceSeparator = " ";
    internal const string UnderscoreSeparator = "_";
    internal const string MatchLabelGroup = "label";
    internal const string MatchUrlGroup = "url";
    internal const string MatchSubjectGroup = "subject";
    internal const string MatchPredicateGroup = "predicate";
    internal const string MatchObjectGroup = "object";
    internal const string ProvWasDerivedFromSuffix = "wasDerivedFrom";
    internal const string MarkdownMediaType = "text/markdown";
    internal const string MdxMediaType = "text/mdx";
    internal const string PlainTextMediaType = "text/plain";
    internal const string JsonMediaType = "application/json";
    internal const string YamlMediaType = "application/yaml";
    internal const string CsvMediaType = "text/csv";
    internal const string MarkdownExtension = ".md";
    internal const string MarkdownLongExtension = ".markdown";
    internal const string MdxExtension = ".mdx";
    internal const string TextExtension = ".txt";
    internal const string TextLongExtension = ".text";
    internal const string LogExtension = ".log";
    internal const string CsvExtension = ".csv";
    internal const string JsonExtension = ".json";
    internal const string JsonLinesExtension = ".jsonl";
    internal const string YamlExtension = ".yaml";
    internal const string YmlExtension = ".yml";
    internal const string AllFilesSearchPattern = "*";
    internal const string ConverterDefaultPath = "document.md";
    internal const string UnsupportedFileMessagePrefix = "Unsupported text document extension: ";
    internal const string UnsupportedFileMessageSuffix = ". Supported extensions: ";
    internal const string SupportedExtensionsSeparator = ", ";
    internal const string FilePathRequiredMessage = "A source file path is required.";
    internal const string DirectoryPathRequiredMessage = "A source directory path is required.";
    internal const string DirectoryNotFoundMessagePrefix = "Source directory was not found: ";
    internal const char DoubleQuoteCharacter = '"';
    internal const char SingleQuoteCharacter = '\'';
    internal const char CommaCharacter = ',';
    internal const char SemicolonCharacter = ';';

    internal static readonly Uri SchemaNamespaceUri = new(SchemaNamespaceText);
    internal static readonly Uri KbNamespaceUri = new(KbNamespaceText);
    internal static readonly Uri ProvNamespaceUri = new(ProvNamespaceText);
    internal static readonly Uri RdfNamespaceUri = new(RdfNamespaceText);
    internal static readonly Uri XsdNamespaceUri = new(XsdNamespaceText);
    internal static readonly Uri SchemaArticleUri = new(SchemaArticleText);
    internal static readonly Uri SchemaNameUri = new(SchemaNameText);
    internal static readonly Uri SchemaDescriptionUri = new(SchemaDescriptionText);
    internal static readonly Uri SchemaDatePublishedUri = new(SchemaDatePublishedText);
    internal static readonly Uri SchemaDateModifiedUri = new(SchemaDateModifiedText);
    internal static readonly Uri SchemaKeywordsUri = new(SchemaKeywordsText);
    internal static readonly Uri SchemaAboutUri = new(SchemaAboutText);
    internal static readonly Uri SchemaAuthorUri = new(SchemaAuthorText);
    internal static readonly Uri SchemaSameAsUri = new(SchemaSameAsText);
    internal static readonly Uri SchemaMentionsUri = new(SchemaMentionsText);
    internal static readonly Uri SchemaCreatorUri = new(SchemaCreatorText);
    internal static readonly Uri KbConfidenceUri = new(KbConfidenceText);
    internal static readonly Uri RdfTypeUri = new(RdfTypeText);
    internal static readonly Uri XsdDateUri = new(XsdDateText);
    internal static readonly Uri XsdIntegerUri = new(XsdIntegerText);
    internal static readonly Uri ProvWasDerivedFromUri = new(ProvNamespaceText + ProvWasDerivedFromSuffix);
    internal static readonly char[] ArrowOperandTrimChars = [DoubleQuoteCharacter, SingleQuoteCharacter, CommaCharacter, SemicolonCharacter];

    internal static readonly Regex MutatingKeywordRegex = new(
        MutatingKeywordPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
