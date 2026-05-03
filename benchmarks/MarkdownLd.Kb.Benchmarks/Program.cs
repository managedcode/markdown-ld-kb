using BenchmarkDotNet.Running;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

internal static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args, new MarkdownLdBenchmarkConfig());
    }
}
