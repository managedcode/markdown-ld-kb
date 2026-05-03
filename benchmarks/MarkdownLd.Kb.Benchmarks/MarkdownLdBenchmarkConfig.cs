using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

public sealed class MarkdownLdBenchmarkConfig : ManualConfig
{
    private const string ArtifactsDirectory = "artifacts/benchmarks";
    private const string ProfileEnvironmentVariable = "MARKDOWN_LD_KB_BENCHMARK_PROFILE";
    private const string CpuProfileValue = "cpu";
    private const string GcProfileValue = "gc";
    private const string JitProfileValue = "jit";

    public MarkdownLdBenchmarkConfig()
    {
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddJob(Job.ShortRun.WithId("ShortRun"));
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(CsvExporter.Default);
        AddExporter(JsonExporter.Full);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        AddOptionalProfiler();
        ArtifactsPath = Path.Combine(Directory.GetCurrentDirectory(), ArtifactsDirectory);
    }

    private void AddOptionalProfiler()
    {
        var profiler = Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);
        var eventPipeProfile = profiler?.Trim().ToLowerInvariant() switch
        {
            CpuProfileValue => EventPipeProfile.CpuSampling,
            GcProfileValue => EventPipeProfile.GcVerbose,
            JitProfileValue => EventPipeProfile.Jit,
            _ => (EventPipeProfile?)null,
        };

        if (eventPipeProfile.HasValue)
        {
            AddDiagnoser(new EventPipeProfiler(eventPipeProfile.Value));
        }
    }
}
