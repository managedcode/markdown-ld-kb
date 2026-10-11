#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj
#:include ../benchmarks/MarkdownLd.Kb.Benchmarks/BenchmarkMarkdownCorpus.cs
#:include ../benchmarks/MarkdownLd.Kb.Benchmarks/BenchmarkCorpusProfile.cs

using System.Diagnostics;
using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Benchmarks;
using ManagedCode.MarkdownLd.Kb.Pipeline;

const string matchQuery = "cache restore manifest evidence";
const string missingQuery = "satellite coffee roasting";
const int maximumResults = 10;
const int maximumPhaseSeconds = 300;
var corpus = args.Length == 1 ? Enum.Parse<BenchmarkCorpusProfile>(args[0]) : BenchmarkCorpusProfile.ShortDocuments;
var sources = BenchmarkMarkdownCorpus.CreateSources(corpus);
var pipeline = new MarkdownKnowledgePipeline(new Uri("https://bench.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
var build = await pipeline.BuildAsync(sources);
var unrestricted = KnowledgeGraphSchemaSearchProfile.Default with
{
    TermMode = KnowledgeGraphSchemaSearchTermMode.AnyTerm,
    MaxResults = maximumResults,
};
var warm = await build.Graph.SearchBySchemaAsync(matchQuery, unrestricted);
var expectedIds = warm.Matches.Select(match => match.NodeId).ToArray();
if (expectedIds.Length != maximumResults)
{
    throw new InvalidOperationException($"Expected {maximumResults} warmup matches, got {expectedIds.Length}.");
}
var candidates = unrestricted with { CandidateNodeIds = expectedIds };
var narrowed = await build.Graph.SearchBySchemaAsync(matchQuery, candidates);
if (!expectedIds.SequenceEqual(narrowed.Matches.Select(match => match.NodeId)))
{
    throw new InvalidOperationException("Candidate and unrestricted primary match order differ.");
}
Console.WriteLine(JsonSerializer.Serialize(new { ready = true, pid = Environment.ProcessId, runtime = Environment.Version.ToString(), corpus = corpus.ToString(), documents = sources.Length, query = matchQuery, candidateCount = expectedIds.Length, primaryResultsEqual = true }));

while (await Console.In.ReadLineAsync() is { } command)
{
    if (command == "exit")
    {
        break;
    }
    var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2 || !int.TryParse(parts[1], out var seconds) || seconds < 1 || seconds > maximumPhaseSeconds)
    {
        throw new ArgumentException("Use unrestricted|candidates|no-match|no-match-candidates <seconds 1..300>, or exit.");
    }
    var phase = parts[0];
    var profile = phase switch
    {
        "unrestricted" or "no-match" => unrestricted,
        "candidates" or "no-match-candidates" => candidates,
        _ => throw new ArgumentException("Unknown phase."),
    };
    var isMissing = phase.StartsWith("no-match", StringComparison.Ordinal);
    var query = isMissing ? missingQuery : matchQuery;
    var expectedCount = isMissing ? 0 : maximumResults;
    for (var iteration = 0; iteration < 3; iteration++)
    {
        _ = await build.Graph.SearchBySchemaAsync(query, profile);
    }
    Console.WriteLine(JsonSerializer.Serialize(new { started = phase, pid = Environment.ProcessId, seconds }));
    var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
    var clock = Stopwatch.StartNew();
    long operations = 0;
    while (clock.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        var result = await build.Graph.SearchBySchemaAsync(query, profile);
        if (result.Matches.Count != expectedCount)
        {
            throw new InvalidOperationException($"Unexpected result count in {phase}: {result.Matches.Count}.");
        }
        operations++;
    }
    var elapsed = clock.Elapsed.TotalMilliseconds;
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
    Console.WriteLine(JsonSerializer.Serialize(new { phase, pid = Environment.ProcessId, operations, elapsedMilliseconds = elapsed, allocatedBytes = allocated, millisecondsPerOperation = elapsed / operations, allocatedBytesPerOperation = (double)allocated / operations }));
}
