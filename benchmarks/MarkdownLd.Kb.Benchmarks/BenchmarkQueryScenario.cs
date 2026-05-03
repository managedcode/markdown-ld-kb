namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

public enum BenchmarkQueryScenario
{
    Exact,
    Typo,
    NoMatch,
}

public enum FuzzyTokenScenario
{
    ShortDeletion,
    ShortSubstitution,
    LongInsertion,
    LongNoMatch,
}
