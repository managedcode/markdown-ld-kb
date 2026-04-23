using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Store;
using Lucene.Net.Util;
using VDS.RDF.Query.FullText.Indexing;
using VDS.RDF.Query.FullText.Indexing.Lucene;
using VDS.RDF.Query.FullText.Schema;
using VDS.RDF.Query.FullText.Search.Lucene;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static readonly object FullTextIndexSync = new();

    public Task<KnowledgeGraphFullTextIndex> BuildFullTextIndexAsync(
        KnowledgeGraphFullTextIndexOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => BuildFullTextIndex(options ?? KnowledgeGraphFullTextIndexOptions.Default), cancellationToken);
    }

    private KnowledgeGraphFullTextIndex BuildFullTextIndex(KnowledgeGraphFullTextIndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var snapshot = CreateSnapshot();
        var graphSnapshot = CreateGraphSnapshot(snapshot.Triples);
        var labels = graphSnapshot.Nodes.ToDictionary(static node => node.Id, static node => node.Label, StringComparer.Ordinal);
        var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
        var schema = new DefaultIndexSchema();
        var directory = CreateLuceneDirectory(options);
        var indexer = CreateIndexer(options.Target, directory, analyzer, schema);
        lock (FullTextIndexSync)
        {
            indexer.Index(snapshot);
            indexer.Flush();
        }

        var provider = new LuceneSearchProvider(LuceneVersion.LUCENE_48, directory, analyzer, schema, options.AutoSync);
        return new KnowledgeGraphFullTextIndex(directory, analyzer, indexer, provider, labels);
    }

    private static LuceneDirectory CreateLuceneDirectory(KnowledgeGraphFullTextIndexOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryPath))
        {
            return new RAMDirectory();
        }

        System.IO.Directory.CreateDirectory(options.DirectoryPath);
        return FSDirectory.Open(new DirectoryInfo(options.DirectoryPath));
    }

    private static IFullTextIndexer CreateIndexer(
        KnowledgeGraphFullTextIndexTarget target,
        LuceneDirectory directory,
        Analyzer analyzer,
        IFullTextIndexSchema schema)
    {
        return target switch
        {
            KnowledgeGraphFullTextIndexTarget.Subjects => new LuceneSubjectsIndexer(directory, analyzer, schema),
            KnowledgeGraphFullTextIndexTarget.Objects => new LuceneObjectsIndexer(directory, analyzer, schema),
            KnowledgeGraphFullTextIndexTarget.Predicates => new LucenePredicatesIndexer(directory, analyzer, schema),
            _ => throw new InvalidOperationException(target.ToString()),
        };
    }
}
