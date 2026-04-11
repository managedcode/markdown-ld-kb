using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Rdf;

public static class KbNamespaces
{
    public const string SchemaPrefix = "schema";
    public const string ProvPrefix = "prov";
    public const string RdfPrefix = "rdf";
    public const string XsdPrefix = "xsd";
    public const string KbPrefix = "kb";

    public const string Schema = "https://schema.org/";
    public const string Prov = "http://www.w3.org/ns/prov#";
    public const string Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    public const string Xsd = "http://www.w3.org/2001/XMLSchema#";
    public const string Kb = "https://example.com/vocab/kb#";

    public static readonly Uri SchemaUri = new(Schema);
    public static readonly Uri ProvUri = new(Prov);
    public static readonly Uri RdfUri = new(Rdf);
    public static readonly Uri XsdUri = new(Xsd);
    public static readonly Uri KbUri = new(Kb);

    private const string ThingSuffix = "Thing";
    private const string ArticleSuffix = "Article";
    private const string PersonSuffix = "Person";
    private const string OrganizationSuffix = "Organization";
    private const string SoftwareApplicationSuffix = "SoftwareApplication";
    private const string CreativeWorkSuffix = "CreativeWork";

    private const string NameSuffix = "name";
    private const string KeywordsSuffix = "keywords";
    private const string DescriptionSuffix = "description";
    private const string DatePublishedSuffix = "datePublished";
    private const string DateModifiedSuffix = "dateModified";
    private const string MentionsSuffix = "mentions";
    private const string AboutSuffix = "about";
    private const string AuthorSuffix = "author";
    private const string CreatorSuffix = "creator";
    private const string SameAsSuffix = "sameAs";
    private const string SubjectOfSuffix = "subjectOf";

    private const string TypeSuffix = "type";
    private const string DateSuffix = "date";
    private const string DecimalSuffix = "decimal";
    private const string IntegerSuffix = "integer";

    private const string AssertionSuffix = "Assertion";
    private const string ConfidenceSuffix = "confidence";
    private const string RelatedToSuffix = "relatedTo";
    private const string ChunkSuffix = "chunk";
    private const string DocPathSuffix = "docPath";
    private const string CharStartSuffix = "charStart";
    private const string CharEndSuffix = "charEnd";
    private const string WasDerivedFromSuffix = "wasDerivedFrom";

    public static readonly Uri SchemaThing = new(Schema + ThingSuffix);
    public static readonly Uri SchemaArticle = new(Schema + ArticleSuffix);
    public static readonly Uri SchemaPerson = new(Schema + PersonSuffix);
    public static readonly Uri SchemaOrganization = new(Schema + OrganizationSuffix);
    public static readonly Uri SchemaSoftwareApplication = new(Schema + SoftwareApplicationSuffix);
    public static readonly Uri SchemaCreativeWork = new(Schema + CreativeWorkSuffix);

    public static readonly Uri SchemaName = new(Schema + NameSuffix);
    public static readonly Uri SchemaKeywords = new(Schema + KeywordsSuffix);
    public static readonly Uri SchemaDescription = new(Schema + DescriptionSuffix);
    public static readonly Uri SchemaDatePublished = new(Schema + DatePublishedSuffix);
    public static readonly Uri SchemaDateModified = new(Schema + DateModifiedSuffix);
    public static readonly Uri SchemaMentions = new(Schema + MentionsSuffix);
    public static readonly Uri SchemaAbout = new(Schema + AboutSuffix);
    public static readonly Uri SchemaAuthor = new(Schema + AuthorSuffix);
    public static readonly Uri SchemaCreator = new(Schema + CreatorSuffix);
    public static readonly Uri SchemaSameAs = new(Schema + SameAsSuffix);
    public static readonly Uri SchemaSubjectOf = new(Schema + SubjectOfSuffix);

    public static readonly Uri RdfType = new(Rdf + TypeSuffix);
    public static readonly Uri XsdDate = new(Xsd + DateSuffix);
    public static readonly Uri XsdDecimal = new(Xsd + DecimalSuffix);
    public static readonly Uri XsdInteger = new(Xsd + IntegerSuffix);

    public static readonly Uri KbAssertion = new(Kb + AssertionSuffix);
    public static readonly Uri KbConfidence = new(Kb + ConfidenceSuffix);
    public static readonly Uri KbRelatedTo = new(Kb + RelatedToSuffix);
    public static readonly Uri KbChunk = new(Kb + ChunkSuffix);
    public static readonly Uri KbDocPath = new(Kb + DocPathSuffix);
    public static readonly Uri KbCharStart = new(Kb + CharStartSuffix);
    public static readonly Uri KbCharEnd = new(Kb + CharEndSuffix);
    public static readonly Uri ProvWasDerivedFrom = new(Prov + WasDerivedFromSuffix);

    public static void Register(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        graph.NamespaceMap.AddNamespace(SchemaPrefix, SchemaUri);
        graph.NamespaceMap.AddNamespace(ProvPrefix, ProvUri);
        graph.NamespaceMap.AddNamespace(RdfPrefix, RdfUri);
        graph.NamespaceMap.AddNamespace(XsdPrefix, XsdUri);
        graph.NamespaceMap.AddNamespace(KbPrefix, KbUri);
    }
}
