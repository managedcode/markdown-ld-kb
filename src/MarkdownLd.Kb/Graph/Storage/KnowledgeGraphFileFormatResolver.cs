using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphFileFormatResolver
{
    public static KnowledgeGraphFileFormat ResolveForSave(string location, KnowledgeGraphFileFormat? format)
    {
        FileSystemKnowledgeGraphStore.EnsureLocation(location);
        return format ?? TryInfer(location) ?? throw new InvalidDataException(UnsupportedGraphFileExtensionMessagePrefix + location);
    }

    public static KnowledgeGraphFileFormat ResolveForLoad(string location, KnowledgeGraphFileFormat? format)
    {
        FileSystemKnowledgeGraphStore.EnsureLocation(location);
        return format ?? TryInfer(location) ?? throw new InvalidDataException(UnsupportedGraphFileExtensionMessagePrefix + location);
    }

    public static KnowledgeGraphFileFormat? TryInfer(string location)
    {
        var extension = Path.GetExtension(location);
        return extension.ToLowerInvariant() switch
        {
            TurtleExtension => KnowledgeGraphFileFormat.Turtle,
            JsonLdExtension => KnowledgeGraphFileFormat.JsonLd,
            JsonExtension => KnowledgeGraphFileFormat.JsonLd,
            RdfExtension => KnowledgeGraphFileFormat.RdfXml,
            XmlExtension => KnowledgeGraphFileFormat.RdfXml,
            NTriplesExtension => KnowledgeGraphFileFormat.NTriples,
            Notation3Extension => KnowledgeGraphFileFormat.Notation3,
            TriGExtension => KnowledgeGraphFileFormat.TriG,
            NQuadsExtension => KnowledgeGraphFileFormat.NQuads,
            _ => null,
        };
    }

    public static string GetMimeType(KnowledgeGraphFileFormat format)
    {
        return format switch
        {
            KnowledgeGraphFileFormat.Turtle => TextTurtleMimeType,
            KnowledgeGraphFileFormat.JsonLd => ApplicationJsonLdMimeType,
            KnowledgeGraphFileFormat.RdfXml => ApplicationRdfXmlMimeType,
            KnowledgeGraphFileFormat.NTriples => ApplicationNTriplesMimeType,
            KnowledgeGraphFileFormat.Notation3 => TextNotation3MimeType,
            KnowledgeGraphFileFormat.TriG => ApplicationTrigMimeType,
            KnowledgeGraphFileFormat.NQuads => ApplicationNQuadsMimeType,
            _ => throw new InvalidOperationException(UnsupportedGraphFileExtensionMessagePrefix + format),
        };
    }
}
