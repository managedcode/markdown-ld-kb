#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Pipeline;

if (args.Length != 1 && (args.Length != 3 || args[1] is not ("profile" or "memory")))
{
    throw new ArgumentException("Usage: <repository-owned result directory> [profile|memory <case name>]");
}

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var probeMode = args.Length == 3 ? args[1] : "measure";
if (args.Length == 3 && args[2] is not ("metadata-250" or "metadata-2500" or "tiktoken-120"))
{
    throw new ArgumentException("Unknown snapshot case.");
}

foreach (var (name, documents, mode) in new[]
{
    ("metadata-250", 250, MarkdownKnowledgeExtractionMode.None),
    ("metadata-2500", 2500, MarkdownKnowledgeExtractionMode.None),
    ("tiktoken-120", 120, MarkdownKnowledgeExtractionMode.Tiktoken),
})
{
    if (args.Length == 1 || name == args[2])
    {
        await RunCaseAsync(output, name, documents, mode, probeMode);
    }
}

static async Task RunCaseAsync(string output, string name, int documents, MarkdownKnowledgeExtractionMode mode, string probeMode)
{
    var sources = Enumerable.Range(0, documents).Select(index => new MarkdownSourceDocument($"docs/item-{index:D4}.md", $"---\ntitle: Catalog item {index:D4}\ntags: [graph, catalog]\n---\n# Catalog item {index:D4}\n\nFind weather forecast and archive evidence for city number {index.ToString(CultureInfo.InvariantCulture)}.\n\n## Details\nInspect graph neighbors and source provenance.")).ToArray();
    var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.probe.example/"), extractionMode: mode);
    var build = await pipeline.BuildAsync(sources);
    using var graph = build.Graph;
    var expected = graph.ToCompleteSnapshot();
    var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expected);
    var expectedHash = Convert.ToHexString(SHA256.HashData(expectedBytes));
    await File.WriteAllBytesAsync(Path.Combine(output, name + ".snapshot.json"), expectedBytes);
    for (var warmup = 0; warmup < 10; warmup++)
    {
        _ = graph.ToCompleteSnapshot();
    }

    if (probeMode == "profile")
    {
        await RunProfileAsync(name, documents, graph, expected, expectedBytes, expectedHash);
        return;
    }

    if (probeMode == "memory")
    {
        await MeasureRetainedMemoryAsync(output, name, graph, expected, expectedBytes, expectedHash);
        return;
    }

    var batches = MeasureBatches(graph, expected, expectedBytes);
    await WriteMetricsAsync(output, name, documents, graph.TripleCount, expectedHash, batches);
}

static List<object> MeasureBatches(KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedBytes)
{
    using var process = Process.GetCurrentProcess();
    var batches = new List<object>();
    for (var batch = 0; batch < 5; batch++)
    {
        process.Refresh();
        var cpuBefore = process.TotalProcessorTime;
        var before = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 20; iteration++)
        {
            ValidateCardinality(graph.ToCompleteSnapshot(), expected);
        }
        var elapsed = clock.Elapsed.TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        process.Refresh();
        var cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        VerifyFullResult(graph, expectedBytes);
        batches.Add(new { millisecondsPerOperation = elapsed / 20, cpuMillisecondsPerOperation = cpuMilliseconds / 20, bytesPerOperation = allocated / 20.0 });
    }

    return batches;
}

static async Task RunProfileAsync(string name, int documents, KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedBytes, string expectedHash)
{
    Console.WriteLine(JsonSerializer.Serialize(new { ready = true, pid = Environment.ProcessId, name, documents, triples = graph.TripleCount, expectedHash, moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId }));
    while (await Console.In.ReadLineAsync() is { } command && command != "exit")
    {
        var seconds = int.Parse(command, CultureInfo.InvariantCulture);
        if (seconds is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        var before = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        var operations = 0;
        while (watch.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            ValidateCardinality(graph.ToCompleteSnapshot(), expected);
            operations++;
        }

        var elapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
        var bytes = GC.GetTotalAllocatedBytes(true) - before;
        VerifyFullResult(graph, expectedBytes);
        Console.WriteLine(JsonSerializer.Serialize(new { phase = name, operations, elapsedMilliseconds, allocatedBytesPerOperation = (double)bytes / operations }));
    }
}

static void ValidateCardinality(KnowledgeGraphSnapshot snapshot, KnowledgeGraphSnapshot expected)
{
    if (snapshot.Nodes.Count != expected.Nodes.Count || snapshot.Edges.Count != expected.Edges.Count)
    {
        throw new InvalidOperationException("Snapshot cardinality changed.");
    }
}

static void VerifyFullResult(KnowledgeGraph graph, byte[] expectedBytes)
{
    if (!JsonSerializer.SerializeToUtf8Bytes(graph.ToCompleteSnapshot()).AsSpan().SequenceEqual(expectedBytes))
    {
        throw new InvalidOperationException("Complete snapshot result changed.");
    }
}

static async Task MeasureRetainedMemoryAsync(string output, string name, KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedBytes, string expectedHash)
{
    const int snapshotCount = 10;
    using var process = Process.GetCurrentProcess();
    var retained = new List<KnowledgeGraphSnapshot>(snapshotCount);
    var before = GC.GetTotalMemory(forceFullCollection: true);
    var baselineWorkingSet = process.WorkingSet64;
    for (var index = 0; index < snapshotCount; index++)
    {
        var snapshot = graph.ToCompleteSnapshot();
        ValidateCardinality(snapshot, expected);
        retained.Add(snapshot);
    }

    var after = GC.GetTotalMemory(forceFullCollection: true);
    process.Refresh();
    var result = new
    {
        name, snapshotCount, expectedHash,
        moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId,
        retainedManagedBytes = after - before,
        managedBytesPerSnapshot = (after - before) / (double)snapshotCount,
        baselineWorkingSet, retainedWorkingSet = process.WorkingSet64, peakWorkingSet = process.PeakWorkingSet64,
        methodology = "Isolated single-case process: 10 warmups, force GC before/after holding10 complete snapshots. Managed delta excludes the already-live graph/expected snapshot. RSS includes runtime/JIT/graph initialization. Forced GC is diagnostic-only."
    };
    VerifyFullResult(graph, expectedBytes);
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".memory.json"), json);
    Console.WriteLine(json);
    GC.KeepAlive(retained);
    GC.KeepAlive(graph);
}

static async Task WriteMetricsAsync(string output, string name, int documents, int triples, string expectedHash, List<object> batches)
{
    var assembly = typeof(KnowledgeGraph).Assembly;
    var result = new { name, documents, triples, expectedHash, assemblyVersion = assembly.GetName().Version?.ToString(), moduleVersionId = assembly.ManifestModule.ModuleVersionId, assemblyPath = assembly.Location, assemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))), probePath = System.Reflection.Assembly.GetExecutingAssembly().Location, methodology = "Clean repeated-counter lane: 10 warmups, 5 batches of20 snapshots; exact full JSON identity checked outside each timed batch; no attached profiler; allocated bytes are process-wide traffic, not retained heap.", batches };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".metrics.json"), json);
    Console.WriteLine(json);
}
