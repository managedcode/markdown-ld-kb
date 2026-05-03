using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(ManagedCode.MarkdownLd.Kb.AssemblyInfoConstants.TestsAssemblyName)]
[assembly: InternalsVisibleTo(ManagedCode.MarkdownLd.Kb.AssemblyInfoConstants.BenchmarksAssemblyName)]

namespace ManagedCode.MarkdownLd.Kb;

internal static class AssemblyInfoConstants
{
    internal const string TestsAssemblyName = "ManagedCode.MarkdownLd.Kb.Tests";
    internal const string BenchmarksAssemblyName = "MarkdownLd.Kb.Benchmarks";
}
