Disabling `LeviathanQueryOptions.UsePLinqEvaluation` does not propagate to intermediate join multisets in dotNetRDF 3.5.2. A real SPARQL join returns correct rows, but its result multiset still has `UsePLinqEvaluation == true`; subsequent `BaseMultiset.Join`/`Product` operations consult that multiset flag rather than the evaluation-context option.

Environment: dotNetRdf.Core 3.5.2, .NET SDK 10.0.401 / runtime 10.0.12, macOS arm64. Package repository commit: `67142d12e67fb68b37d009c3579ddebaeb4ff2c1`.

Reproduction (save as `repro.cs`, then run `dotnet run --file repro.cs` with .NET 10):

```csharp
#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:package dotNetRdf.Core@3.5.2

using System.Text.Json;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Datasets;

using var graph = new VDS.RDF.Graph();
StringParser.Parse(graph, """
    @prefix ex: <https://engine.example/> .
    ex:a ex:name "first" ; ex:category "tool" .
    ex:b ex:name "second" ; ex:category "tool" .
    """, new TurtleParser());
var processor = new JoinProbe(new InMemoryDataset(graph));
var query = new SparqlQueryParser().ParseFromString("""
    SELECT ?s ?name WHERE {
      { ?s <https://engine.example/name> ?name }
      { ?s <https://engine.example/category> "tool" }
    }
    """);
var result = (SparqlResultSet)processor.ProcessQuery(query);
if (result.Count != 2 || processor.ObservedJoins == 0)
{
    throw new InvalidOperationException("Expected two results and an observed real engine join.");
}
Console.WriteLine(JsonSerializer.Serialize(new { correctResults = result.Count, observedJoins = processor.ObservedJoins, parallelEnabledDespiteFalseOptions = processor.ParallelEnabledDespiteFalseOptions }));

sealed class JoinProbe(ISparqlDataset dataset) : LeviathanQueryProcessor(dataset, options => { options.UsePLinqEvaluation = false; options.AlgebraOptimisation = false; })
{
    public int ObservedJoins { get; private set; }
    public bool ParallelEnabledDespiteFalseOptions { get; private set; }

    public override BaseMultiset ProcessJoin(IJoin join, SparqlEvaluationContext context)
    {
        var result = base.ProcessJoin(join, context);
        ObservedJoins++;
        ParallelEnabledDespiteFalseOptions |= !context.Options.UsePLinqEvaluation && result.UsePLinqEvaluation;
        Console.WriteLine(JsonSerializer.Serialize(new { optionsUsePLinq = context.Options.UsePLinqEvaluation, resultUsePLinq = result.UsePLinqEvaluation }));
        return result;
    }
}
```

Observed output:

```json
{"optionsUsePLinq":false,"resultUsePLinq":true}
{"correctResults":2,"observedJoins":1,"parallelEnabledDespiteFalseOptions":true}
```

Expected: intermediate set operations should honor the disabled parallel-evaluation option, including join/product chains. The result flag should not silently re-enable parallel set operations. Algebra optimisation is disabled only to make the actual join observable in this small reproduction; query results are asserted correct.

Source pointers: `BaseMultiset` defaults its constructor's `usePLinqEvaluation` argument to true, `Multiset` constructors do not forward an explicit disabled value, and Join/LeftJoin create `new Multiset()` results. `BaseMultiset.Join` and `Product` use the instance flag; LeftJoin additionally consults context options. This appears to make option behavior inconsistent across algebra operations.

In an application allocation capture we also observed PLINQ frames despite configuring the processor option false. That observation motivated the smaller reproduction above; it is not a claim that this issue explains all allocation cost or that changing PLINQ is guaranteed to improve performance. No engine patch/fork or workaround is proposed in this report.
