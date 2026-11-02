#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Pipeline;

if (args.Length != 1 && (args.Length != 3 || args[1] is not ("profile" or "memory" or "cold" or "semantic" or "dispose" or "soak")))
{
    throw new ArgumentException("Usage: <repository-owned result directory> [profile|memory|cold|semantic|dispose|soak <case name>]");
}

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var probeMode = args.Length == 3 ? args[1] : "measure";
if (args.Length == 3 && args[2] is not ("metadata-250" or "metadata-2500" or "tiktoken-120" or "repeated-2500" or "unicode-250"))
{
    throw new ArgumentException("Unknown snapshot case.");
}

if (probeMode == "cold" && args[2] == "tiktoken-120")
{
    throw new ArgumentException("Cold-cache identity validation uses metadata-only cases.");
}

foreach (var (name, documents, mode) in new[]
{
    ("metadata-250", 250, MarkdownKnowledgeExtractionMode.None),
    ("metadata-2500", 2500, MarkdownKnowledgeExtractionMode.None),
    ("tiktoken-120", 120, MarkdownKnowledgeExtractionMode.Tiktoken),
    ("repeated-2500", 2500, MarkdownKnowledgeExtractionMode.None),
    ("unicode-250", 250, MarkdownKnowledgeExtractionMode.None),
})
{
    if (args.Length == 1 || name == args[2])
    {
        await RunCaseAsync(output, name, documents, mode, probeMode);
    }
}

static async Task RunCaseAsync(string output, string name, int documents, MarkdownKnowledgeExtractionMode mode, string probeMode)
{
    var sources = CreateSources(name, documents);
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

    await RunMeasurementAsync(output, name, documents, probeMode, graph, expected, expectedBytes, expectedHash);
}

static Task RunMeasurementAsync(string output, string name, int documents, string probeMode,
    KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedBytes, string expectedHash) => probeMode switch
{
    "profile" => RunProfileAsync(name, documents, graph, expected, expectedBytes, expectedHash),
    "memory" => MeasureRetainedMemoryAsync(output, name, graph, expected, expectedBytes, expectedHash),
    "cold" => MeasureColdSnapshotAsync(output, name, graph, expectedBytes),
    "semantic" => MeasureSemanticSnapshotAsync(output, name, graph, expectedBytes),
    "dispose" => MeasureDisposedMemoryAsync(output, name, graph, expected, expectedBytes),
    "soak" => MeasureSoakAsync(output, name, graph, expected, expectedBytes),
    "measure" => WriteMetricsAsync(output, name, documents, graph.TripleCount, expectedHash, MeasureBatches(graph, expected, expectedBytes)),
    _ => throw new ArgumentException("Unknown measurement mode.")
};

static MarkdownSourceDocument[] CreateSources(string name, int documents)
{
    return Enumerable.Range(0, documents).Select(index =>
    {
        var contentIndex = name == "repeated-2500" ? 0 : index;
        var title = name == "unicode-250" ? $"Обсерваторія {index:D4} 天気 🌦️" : $"Catalog item {contentIndex:D4}";
        var evidence = name == "unicode-250" ? "Прогноз погоди, українська обсерваторія, 天気予報 및 관측 기록 🌦️." :
            $"Find weather forecast and archive evidence for city number {contentIndex.ToString(CultureInfo.InvariantCulture)}.";
        return new MarkdownSourceDocument($"docs/item-{index:D4}.md", $"---\ntitle: {title}\ntags: [graph, catalog]\n---\n# {title}\n\n{evidence}\n\n## Details\nInspect graph neighbors and source provenance.");
    }).ToArray();
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
    GC.KeepAlive(expected);
    GC.KeepAlive(graph);
}

static async Task MeasureColdSnapshotAsync(string output, string name, KnowledgeGraph graph, byte[] expectedBytes)
{
    const int readerCount = 16;
    using var ready = new CountdownEvent(readerCount);
    using var start = new ManualResetEventSlim();
    using var process = Process.GetCurrentProcess();
    var snapshots = new KnowledgeGraphSnapshot[readerCount];
    var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
    var threads = Enumerable.Range(0, readerCount).Select(index => new Thread(() =>
    {
        ready.Signal();
        start.Wait();
        try { snapshots[index] = graph.ToSemanticSnapshot(); }
        catch (Exception error) { errors.Enqueue(error); }
    })).ToArray();
    foreach (var thread in threads) { thread.Start(); }
    ready.Wait();
    process.Refresh();
    var cpuBefore = process.TotalProcessorTime;
    var before = GC.GetTotalAllocatedBytes(true);
    var clock = Stopwatch.StartNew();
    start.Set();
    foreach (var thread in threads) { thread.Join(); }
    var elapsedMilliseconds = clock.Elapsed.TotalMilliseconds;
    var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
    process.Refresh();
    var cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
    if (!errors.IsEmpty) { throw new AggregateException(errors); }
    var resultBytes = JsonSerializer.SerializeToUtf8Bytes(snapshots[0]);
    if (snapshots.Any(snapshot => !ReferenceEquals(snapshot, snapshots[0])) || !resultBytes.AsSpan().SequenceEqual(expectedBytes))
    {
        throw new InvalidOperationException("Cold readers did not share the complete metadata projection.");
    }
    var result = new { name, readerCount, elapsedMilliseconds, cpuMilliseconds, allocatedBytes,
        moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId,
        snapshotHash = Convert.ToHexString(SHA256.HashData(resultBytes)),
        methodology = "16 dedicated threads rendezvous before one cold semantic snapshot; join all readers; verify shared result and complete metadata identity. No attached profiler." };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".cold.json"), json);
    Console.WriteLine(json);
}

static async Task WriteMetricsAsync(string output, string name, int documents, int triples, string expectedHash, List<object> batches)
{
    var assembly = typeof(KnowledgeGraph).Assembly;
    var result = new { name, documents, triples, expectedHash, assemblyVersion = assembly.GetName().Version?.ToString(), moduleVersionId = assembly.ManifestModule.ModuleVersionId, assemblyPath = assembly.Location, assemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))), probePath = System.Reflection.Assembly.GetExecutingAssembly().Location, methodology = "Clean repeated-counter lane: 10 warmups, 5 batches of20 snapshots; exact full JSON identity checked outside each timed batch; no attached profiler; allocated bytes are process-wide traffic, not retained heap.", batches };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".metrics.json"), json);
    Console.WriteLine(json);
}

static async Task MeasureSemanticSnapshotAsync(string output, string name, KnowledgeGraph graph, byte[] expectedComplete)
{
    using var process = Process.GetCurrentProcess();
    process.Refresh();
    var cpuBefore = process.TotalProcessorTime;
    var before = GC.GetTotalAllocatedBytes(true);
    var clock = Stopwatch.StartNew();
    var snapshot = graph.ToSemanticSnapshot();
    var elapsedMilliseconds = clock.Elapsed.TotalMilliseconds;
    var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
    process.Refresh();
    var cpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
    if (!ReferenceEquals(snapshot, graph.ToSemanticSnapshot()))
    {
        throw new InvalidOperationException("Warm semantic snapshot was not reused.");
    }
    VerifyFullResult(graph, expectedComplete);
    var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
    await File.WriteAllBytesAsync(Path.Combine(output, name + ".semantic.snapshot.json"), bytes);
    var result = new { name, snapshot.Nodes.Count, edgeCount = snapshot.Edges.Count, allocatedBytes, elapsedMilliseconds, cpuMilliseconds,
        moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId,
        snapshotHash = Convert.ToHexString(SHA256.HashData(bytes)),
        methodology = "One cold semantic projection after10 complete-projection warmups; exact complete graph result preserved and warm semantic reference reused. No profiler." };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".semantic.metrics.json"), json);
    Console.WriteLine(json);
}

static async Task MeasureDisposedMemoryAsync(string output, string name, KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedComplete)
{
    _ = graph.ToSemanticSnapshot();
    VerifyFullResult(graph, expectedComplete);
    var before = GC.GetTotalMemory(forceFullCollection: true);
    var clock = Stopwatch.StartNew();
    graph.Dispose();
    var disposalMilliseconds = clock.Elapsed.TotalMilliseconds;
    var after = GC.GetTotalMemory(forceFullCollection: true);
    var result = new { name, managedBytesBefore = before, managedBytesAfter = after, releasedManagedBytes = before - after,
        disposalMilliseconds, tokenSearchAvailableAfterDispose = graph.CanSearchByTokenDistance,
        moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId,
        methodology = "Force GC before/after Dispose while graph and reference complete snapshot remain live. Measures managed retained delta, not returned OS pages." };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".dispose.memory.json"), json);
    Console.WriteLine(json);
    GC.KeepAlive(expected);
    GC.KeepAlive(graph);
}

static async Task MeasureSoakAsync(string output, string name, KnowledgeGraph graph, KnowledgeGraphSnapshot expected, byte[] expectedBytes)
{
    var semantic = graph.ToSemanticSnapshot();
    var initialManagedBytes = GC.GetTotalMemory(forceFullCollection: true);
    var batches = new List<object>();
    for (var batch = 0; batch < 5; batch++)
    {
        var before = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            ValidateCardinality(graph.ToCompleteSnapshot(), expected);
            if (!ReferenceEquals(semantic, graph.ToSemanticSnapshot()))
            {
                throw new InvalidOperationException("Warm semantic cache changed during read-only soak.");
            }
        }
        var elapsedMilliseconds = clock.Elapsed.TotalMilliseconds;
        var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
        VerifyFullResult(graph, expectedBytes);
        batches.Add(new { batch, elapsedMilliseconds, allocatedBytes, retainedManagedBytes = GC.GetTotalMemory(forceFullCollection: true) });
    }
    var result = new { name, initialManagedBytes, batches,
        moduleVersionId = typeof(KnowledgeGraph).Assembly.ManifestModule.ModuleVersionId,
        methodology = "5 batches of100 complete+semantic reads on one graph; full result identity and warm cache reference checked; force GC only outside timed batches; five tiny result rows remain live." };
    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.Combine(output, name + ".soak.json"), json);
    Console.WriteLine(json);
    GC.KeepAlive(expected);
    GC.KeepAlive(semantic);
    GC.KeepAlive(graph);
}
