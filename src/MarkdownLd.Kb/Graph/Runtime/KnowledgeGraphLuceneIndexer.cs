using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using VDS.RDF;
using VDS.RDF.Query.FullText.Indexing;
using VDS.RDF.Query.FullText.Indexing.Lucene;
using VDS.RDF.Query.FullText.Schema;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

// dotNetRDF hashes every indexed document with one process-wide SHA256 instance
// (FullTextExtensions.GetSha256Hash), which rejects concurrent use. Only that document
// creation is exclusive; analysis, AddDocument and commits of independent builds overlap.
internal sealed class KnowledgeGraphLuceneIndexer(
    LuceneDirectory directory,
    Analyzer analyzer,
    IFullTextIndexSchema schema,
    IndexingMode mode) : BaseSimpleLuceneIndexer(directory, analyzer, schema, mode)
{
    // Entered in write mode only; its waiting count makes build overlap observable.
    internal static ReaderWriterLockSlim EngineHashGate { get; } = new();

    protected override Document CreateDocument(string graphUri, INode n, string text)
    {
        EngineHashGate.EnterWriteLock();
        try
        {
            return base.CreateDocument(graphUri, n, text);
        }
        finally
        {
            EngineHashGate.ExitWriteLock();
        }
    }
}
