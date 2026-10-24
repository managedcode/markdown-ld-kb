using System.Globalization;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FullTextIndexConcurrencyFlowTests
{
    private const int ConcurrentBuildCount = 16;
    private const int OverlappingBuildCount = 2;
    private const int StepCount = 24;
    private const int SearchLimit = 1_000;
    private const int UnknownTarget = 999;
    private const string IndexFormat = "00";
    private const string IndexToken = "{INDEX}";
    private const string StepsToken = "{STEPS}";
    private const string BaseUriTemplate = "https://fulltext-concurrency.example/{INDEX}/";
    private const string MarkerTemplate = "beacon{INDEX}";
    private const string SharedTerm = "relay";
    private const string DocumentPathTemplate = "runbooks/beacon-{INDEX}.md";
    private const string DocumentUriTemplate = "https://fulltext-concurrency.example/{INDEX}/runbooks/beacon-{INDEX}/";
    private const string StepTemplate = "- Step {STEP}: operators verify the beacon{INDEX} relay checksum {STEP}.";
    private const string StepToken = "{STEP}";
    private const string MarkdownTemplate = """
---
title: Beacon {INDEX} Relay Runbook
tags:
  - beacon{INDEX}
---
# Beacon {INDEX} Relay Runbook

Operators restart the beacon{INDEX} relay after checksum drift.

{STEPS}
""";

    private static readonly TimeSpan OverlapTimeout = TimeSpan.FromSeconds(30);

    [Test]
    public async Task Sixteen_concurrent_full_text_builds_return_only_their_own_graph_matches()
    {
        var graphs = await BuildGraphsAsync(ConcurrentBuildCount);
        using var start = new ManualResetEventSlim();
        var builds = graphs
            .Select(graph => Task.Run(() =>
            {
                start.Wait();
                return graph.BuildFullTextIndexAsync();
            }))
            .ToArray();
        start.Set();

        var indexes = await Task.WhenAll(builds);
        try
        {
            for (var index = 0; index < ConcurrentBuildCount; index++)
            {
                await AssertIndependentMatchesAsync(indexes[index], index);
            }
        }
        finally
        {
            DisposeAll(indexes, graphs);
        }
    }

    [Test]
    [NotInParallel]
    public async Task Two_full_text_builds_are_inside_their_index_phases_at_the_same_time()
    {
        var graphs = await BuildGraphsAsync(OverlappingBuildCount);
        var builds = StartBuildsBehindHeldEngineHashGate(graphs);

        var indexes = await Task.WhenAll(builds);
        try
        {
            for (var index = 0; index < OverlappingBuildCount; index++)
            {
                await AssertIndependentMatchesAsync(indexes[index], index);
            }
        }
        finally
        {
            DisposeAll(indexes, graphs);
        }
    }

    [Test]
    public async Task Unknown_full_text_target_fails_before_indexing()
    {
        var graphs = await BuildGraphsAsync(1);
        using var graph = graphs[0];
        var options = new KnowledgeGraphFullTextIndexOptions
        {
            Target = (KnowledgeGraphFullTextIndexTarget)UnknownTarget,
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await graph.BuildFullTextIndexAsync(options));
        exception.Message.ShouldBe(UnknownTarget.ToString(CultureInfo.InvariantCulture));
    }

    // While the test owns the only cross-build exclusion, both builds must reach it from inside
    // their own index phases. A process-wide build lock would admit only one of them.
    private static Task<KnowledgeGraphFullTextIndex>[] StartBuildsBehindHeldEngineHashGate(KnowledgeGraph[] graphs)
    {
        var gate = KnowledgeGraphLuceneIndexer.EngineHashGate;
        gate.EnterWriteLock();
        try
        {
            var builds = graphs.Select(graph => graph.BuildFullTextIndexAsync()).ToArray();
            SpinWait.SpinUntil(() => gate.WaitingWriteCount == builds.Length, OverlapTimeout).ShouldBeTrue();
            builds.ShouldAllBe(build => !build.IsCompleted);
            return builds;
        }
        finally
        {
            gate.ExitWriteLock();
        }
    }

    private static async Task AssertIndependentMatchesAsync(KnowledgeGraphFullTextIndex fullText, int index)
    {
        var ownBaseUri = Format(BaseUriTemplate, index);
        var ownMatches = await fullText.SearchAsync(Format(MarkerTemplate, index), SearchLimit);
        ownMatches.Select(match => match.NodeId).ShouldContain(Format(DocumentUriTemplate, index));
        ownMatches.ShouldAllBe(match => match.NodeId.StartsWith(ownBaseUri, StringComparison.Ordinal));

        var sharedMatches = await fullText.SearchAsync(SharedTerm, SearchLimit);
        sharedMatches.Select(match => match.NodeId).ShouldContain(Format(DocumentUriTemplate, index));
        sharedMatches.ShouldAllBe(match => match.NodeId.StartsWith(ownBaseUri, StringComparison.Ordinal));

        var otherIndex = (index + 1) % ConcurrentBuildCount;
        (await fullText.SearchAsync(Format(MarkerTemplate, otherIndex), SearchLimit)).ShouldBeEmpty();
    }

    private static async Task<KnowledgeGraph[]> BuildGraphsAsync(int count)
    {
        var builds = await Task.WhenAll(Enumerable.Range(0, count).Select(BuildGraphAsync));
        return builds.Select(build => build.Graph).ToArray();
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildGraphAsync(int index)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(Format(BaseUriTemplate, index)),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var steps = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, StepCount).Select(step => Format(StepTemplate, index)
                .Replace(StepToken, step.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)));
        var markdown = Format(MarkdownTemplate, index).Replace(StepsToken, steps, StringComparison.Ordinal);
        return pipeline.BuildFromMarkdownAsync(markdown, Format(DocumentPathTemplate, index));
    }

    private static string Format(string template, int index)
    {
        return template.Replace(IndexToken, index.ToString(IndexFormat, CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static void DisposeAll(KnowledgeGraphFullTextIndex[] indexes, KnowledgeGraph[] graphs)
    {
        foreach (var fullText in indexes)
        {
            fullText.Dispose();
        }

        foreach (var graph in graphs)
        {
            graph.Dispose();
        }
    }
}
