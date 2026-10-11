#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj

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
