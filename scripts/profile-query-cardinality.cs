#!/usr/bin/env -S dotnet run --file
#:property PublishAot=false
#:property GenerateDocumentationFile=false
#:project ../src/MarkdownLd.Kb/MarkdownLd.Kb.csproj

using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Query.Datasets;

using var graph = KnowledgeGraph.LoadJsonLd(await File.ReadAllTextAsync(args[0]));
using var rdf = graph.ToOntologySnapshot();
var query = new SparqlQueryParser().ParseFromString(await File.ReadAllTextAsync(args[1]));
var processor = new ObservationProcessor(new InMemoryDataset(rdf));
var result = (SparqlResultSet)processor.ProcessQuery(query);
Console.WriteLine(JsonSerializer.Serialize(new { triples = rdf.Triples.Count, rows = result.Count, groups = processor.Groups.OrderByDescending(p => Math.Max(p.Input, p.Output)).Take(40) }));

sealed class ObservationProcessor(ISparqlDataset dataset) : LeviathanQueryProcessor(dataset, options => options.UsePLinqEvaluation = false)
{
    public List<Group> Groups { get; } = [];
    public override BaseMultiset ProcessBgp(IBgp bgp, SparqlEvaluationContext context)
    {
        var input = context.InputMultiset.Count;
        var result = base.ProcessBgp(bgp, context);
        Groups.Add(new(bgp.ToString() ?? throw new InvalidOperationException("BGP representation is absent."), input, result.Count));
        return result;
    }
}
sealed record Group(string Pattern, int Input, int Output);
