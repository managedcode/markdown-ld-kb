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

    public static readonly Uri SchemaThing = new($"{Schema}Thing");
    public static readonly Uri SchemaArticle = new($"{Schema}Article");
    public static readonly Uri SchemaPerson = new($"{Schema}Person");
    public static readonly Uri SchemaOrganization = new($"{Schema}Organization");
    public static readonly Uri SchemaSoftwareApplication = new($"{Schema}SoftwareApplication");
    public static readonly Uri SchemaCreativeWork = new($"{Schema}CreativeWork");

    public static readonly Uri SchemaName = new($"{Schema}name");
    public static readonly Uri SchemaKeywords = new($"{Schema}keywords");
    public static readonly Uri SchemaDescription = new($"{Schema}description");
    public static readonly Uri SchemaDatePublished = new($"{Schema}datePublished");
    public static readonly Uri SchemaDateModified = new($"{Schema}dateModified");
    public static readonly Uri SchemaMentions = new($"{Schema}mentions");
    public static readonly Uri SchemaAbout = new($"{Schema}about");
    public static readonly Uri SchemaAuthor = new($"{Schema}author");
    public static readonly Uri SchemaCreator = new($"{Schema}creator");
    public static readonly Uri SchemaSameAs = new($"{Schema}sameAs");
    public static readonly Uri SchemaSubjectOf = new($"{Schema}subjectOf");

    public static readonly Uri RdfType = new($"{Rdf}type");
    public static readonly Uri XsdDate = new($"{Xsd}date");
    public static readonly Uri XsdDecimal = new($"{Xsd}decimal");
    public static readonly Uri XsdInteger = new($"{Xsd}integer");

    public static readonly Uri KbAssertion = new($"{Kb}Assertion");
    public static readonly Uri KbConfidence = new($"{Kb}confidence");
    public static readonly Uri KbRelatedTo = new($"{Kb}relatedTo");
    public static readonly Uri KbChunk = new($"{Kb}chunk");
    public static readonly Uri KbDocPath = new($"{Kb}docPath");
    public static readonly Uri KbCharStart = new($"{Kb}charStart");
    public static readonly Uri KbCharEnd = new($"{Kb}charEnd");

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
