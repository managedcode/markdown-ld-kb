using System.Globalization;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class ConcurrentKnowledgeGraphFlowTests
{
    private const int ConcurrentWorkerCount = 100;
    private const string ConcurrentBaseUriText = "https://concurrent.example/";
    private const string WorkerIndexToken = "{INDEX}";
    private const string WorkerIndexFormat = "000";
    private const string ConcurrentSeedPath = "content/concurrent/seed.md";
    private const string ConcurrentPathTemplate = "content/write/{INDEX}.md";
    private const string ConcurrentSearchTerm = "Thread Entity";
    private const string ConcurrentEntityKey = "entity";
    private const string ConcurrentMarkdownTemplate = """
---
title: Concurrent Article {INDEX}
tags:
  - concurrency
---
# Concurrent Article {INDEX}

This note links [Thread Entity {INDEX}](https://example.com/concurrent/{INDEX}).
""";
    private const string ConcurrentEntityAskQueryTemplate = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://concurrent.example/write/{INDEX}/> schema:mentions ?segment .
  ?segment schema:name ?name .
  FILTER(CONTAINS(STR(?name), "{INDEX}"))
}
""";
    private const string ConcurrentEntitySelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?entity WHERE {
  ?entity schema:name ?name .
  FILTER(CONTAINS(STR(?name), "Thread Entity"))
}
ORDER BY ?entity
""";

    private static readonly Uri ConcurrentBaseUri = new(ConcurrentBaseUriText);

    [Test]
    [NotInParallel]
    public async Task SharedGraphSupportsConcurrentMarkdownWritesAndSearchesAcrossOneHundredWorkers()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            ConcurrentBaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var shared = await pipeline.BuildFromMarkdownAsync(string.Empty, ConcurrentSeedPath);
        var start = new ManualResetEventSlim();

        var workers = Enumerable.Range(0, ConcurrentWorkerCount)
            .Select(index => Task.Factory.StartNew(
                () => RunConcurrentWorkerAsync(pipeline, shared.Graph, index, start),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())
            .ToArray();

        start.Set();

        await Task.WhenAll(workers);

        var finalRows = await shared.Graph.ExecuteSelectAsync(ConcurrentEntitySelectQuery);
        finalRows.Rows.Count.ShouldBe(ConcurrentWorkerCount);
        finalRows.Rows.Select(row => row.Values[ConcurrentEntityKey]).Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(ConcurrentWorkerCount);
    }

    private static async Task RunConcurrentWorkerAsync(
        MarkdownKnowledgePipeline pipeline,
        KnowledgeGraph sharedGraph,
        int index,
        ManualResetEventSlim start)
    {
        start.Wait();

        var workerIndex = FormatWorkerIndex(index);
        var markdown = CreateWorkerMarkdown(workerIndex);
        var path = CreateWorkerPath(workerIndex);
        var askQuery = CreateWorkerAskQuery(workerIndex);

        var built = await pipeline.BuildFromMarkdownAsync(markdown, path);
        var beforeMergeSearch = await sharedGraph.SearchAsync(ConcurrentSearchTerm);

        beforeMergeSearch.Rows.Count.ShouldBeLessThanOrEqualTo(ConcurrentWorkerCount);

        await sharedGraph.MergeAsync(built.Graph);

        var merged = await sharedGraph.ExecuteAskAsync(askQuery);
        merged.ShouldBeTrue();

        var afterMergeSearch = await sharedGraph.SearchAsync(workerIndex);
        afterMergeSearch.Rows.Count.ShouldBeGreaterThan(0);
    }

    private static string FormatWorkerIndex(int index)
    {
        return index.ToString(WorkerIndexFormat, CultureInfo.InvariantCulture);
    }

    private static string CreateWorkerMarkdown(string workerIndex)
    {
        return ConcurrentMarkdownTemplate.Replace(WorkerIndexToken, workerIndex, StringComparison.Ordinal);
    }

    private static string CreateWorkerPath(string workerIndex)
    {
        return ConcurrentPathTemplate.Replace(WorkerIndexToken, workerIndex, StringComparison.Ordinal);
    }

    private static string CreateWorkerAskQuery(string workerIndex)
    {
        return ConcurrentEntityAskQueryTemplate.Replace(WorkerIndexToken, workerIndex, StringComparison.Ordinal);
    }
}
