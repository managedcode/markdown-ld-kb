using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;
using StringWriter = System.IO.StringWriter;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphTextCodec
{
    public static string Serialize(KnowledgeGraph graph, KnowledgeGraphFileFormat format)
    {
        var snapshot = graph.CreateSnapshot();
        return format switch
        {
            KnowledgeGraphFileFormat.Turtle => SaveGraph(snapshot, new CompressingTurtleWriter()),
            KnowledgeGraphFileFormat.JsonLd => SaveStore(CreateStore(snapshot), new JsonLdWriter()),
            KnowledgeGraphFileFormat.RdfXml => SaveGraph(snapshot, new RdfXmlWriter()),
            KnowledgeGraphFileFormat.NTriples => SaveGraph(snapshot, new NTriplesWriter()),
            KnowledgeGraphFileFormat.Notation3 => SaveGraph(snapshot, new Notation3Writer()),
            KnowledgeGraphFileFormat.TriG => SaveStore(CreateStore(snapshot), new TriGWriter()),
            KnowledgeGraphFileFormat.NQuads => SaveStore(CreateStore(snapshot), new NQuadsWriter()),
            _ => throw new InvalidOperationException(KnowledgeGraphStoreConstants.UnsupportedGraphFileExtensionMessagePrefix + format),
        };
    }

    public static void MergeInto(Graph graph, string content, KnowledgeGraphFileFormat format)
    {
        using var reader = new StringReader(content);
        switch (format)
        {
            case KnowledgeGraphFileFormat.Turtle:
                new TurtleParser().Load(graph, reader);
                break;

            case KnowledgeGraphFileFormat.JsonLd:
                MergeStoreIntoGraph(graph, reader, new JsonLdParser());
                break;

            case KnowledgeGraphFileFormat.RdfXml:
                new RdfXmlParser().Load(graph, reader);
                break;

            case KnowledgeGraphFileFormat.NTriples:
                new NTriplesParser().Load(graph, reader);
                break;

            case KnowledgeGraphFileFormat.Notation3:
                new Notation3Parser().Load(graph, reader);
                break;

            case KnowledgeGraphFileFormat.TriG:
                MergeStoreIntoGraph(graph, reader, new TriGParser());
                break;

            case KnowledgeGraphFileFormat.NQuads:
                MergeStoreIntoGraph(graph, reader, new NQuadsParser());
                break;

            default:
                throw new InvalidDataException(KnowledgeGraphStoreConstants.UnsupportedGraphFileExtensionMessagePrefix + format);
        }
    }

    private static TripleStore CreateStore(IGraph graph)
    {
        var store = new TripleStore();
        store.Add(graph, true);
        return store;
    }

    private static string SaveGraph(IGraph graph, IRdfWriter writer)
    {
        using var textWriter = new StringWriter();
        writer.Save(graph, textWriter);
        return textWriter.ToString();
    }

    private static string SaveStore(ITripleStore store, IStoreWriter writer)
    {
        using var textWriter = new StringWriter();
        writer.Save(store, textWriter, false);
        return textWriter.ToString();
    }

    private static void MergeStoreIntoGraph(Graph graph, TextReader reader, IStoreReader parser)
    {
        var store = new TripleStore();
        parser.Load(store, reader);
        foreach (var sourceGraph in store.Graphs)
        {
            graph.Merge(sourceGraph);
        }
    }
}
