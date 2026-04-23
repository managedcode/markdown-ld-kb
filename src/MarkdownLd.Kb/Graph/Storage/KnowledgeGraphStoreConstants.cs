namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphStoreConstants
{
    public const string GraphLocationRequiredMessage = "Graph file or directory path is required.";
    public const string GraphLocationNotFoundMessagePrefix = "Graph file or directory was not found: ";
    public const string UnsupportedGraphFileExtensionMessagePrefix = "Unsupported RDF graph file extension: ";
    public const string GraphDirectoryContainsNoSupportedFilesMessagePrefix = "Graph directory does not contain supported RDF files: ";
    public const string StorageSaveFailedMessagePrefix = "Knowledge graph save failed for location: ";
    public const string StorageLoadFailedMessagePrefix = "Knowledge graph load failed for location: ";
    public const string TextTurtleMimeType = "text/turtle";
    public const string ApplicationJsonLdMimeType = "application/ld+json";
    public const string ApplicationRdfXmlMimeType = "application/rdf+xml";
    public const string ApplicationNTriplesMimeType = "application/n-triples";
    public const string TextNotation3MimeType = "text/n3";
    public const string ApplicationTrigMimeType = "application/trig";
    public const string ApplicationNQuadsMimeType = "application/n-quads";
    public const string TurtleExtension = ".ttl";
    public const string JsonLdExtension = ".jsonld";
    public const string JsonExtension = ".json";
    public const string RdfExtension = ".rdf";
    public const string XmlExtension = ".xml";
    public const string NTriplesExtension = ".nt";
    public const string Notation3Extension = ".n3";
    public const string TriGExtension = ".trig";
    public const string NQuadsExtension = ".nq";
    public const string PathSeparator = "/";
}
