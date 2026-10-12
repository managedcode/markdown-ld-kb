#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj

using System.Diagnostics;
using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Pipeline;

if (args.Length is not (2 or 3)) throw new ArgumentException("Usage: <real catalog JSON-LD> <query experiment directory>");
using var graph = KnowledgeGraph.LoadJsonLd(await File.ReadAllTextAsync(args[0]));
string[]? baseline = null;
foreach (var path in Directory.GetFiles(args[1], "*.sparql").Where(path => args.Length == 2 || Path.GetFileName(path).StartsWith("00-", StringComparison.Ordinal) || Path.GetFileName(path).StartsWith(args[2], StringComparison.Ordinal)).Order(StringComparer.Ordinal))
{
    var query = await File.ReadAllTextAsync(path);
    var warm = await graph.ExecuteSelectAsync(query);
    var signatures = Signatures(warm);
    baseline ??= signatures;
    var equal = baseline.SequenceEqual(signatures);
    var before = GC.GetTotalAllocatedBytes(precise: true);
    var clock = Stopwatch.StartNew();
    for (var iteration = 0; iteration < 3; iteration++)
    {
        var current = await graph.ExecuteSelectAsync(query);
        if (!signatures.SequenceEqual(Signatures(current))) throw new InvalidOperationException("Rows changed between iterations.");
    }
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
    Console.WriteLine(JsonSerializer.Serialize(new { query = Path.GetFileName(path), rows = warm.Rows.Count, equalRows = equal, bytesPerOperation = allocated / 3.0, millisecondsPerOperation = clock.Elapsed.TotalMilliseconds / 3.0 }));
}
static string[] Signatures(SparqlQueryResult rows) => rows.Rows.Select(row => JsonSerializer.Serialize(row.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))).Order(StringComparer.Ordinal).ToArray();
