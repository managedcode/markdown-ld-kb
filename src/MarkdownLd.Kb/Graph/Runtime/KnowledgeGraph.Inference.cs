using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query.Inference;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const string RdfsReasonerName = "RDFS";
    private const string SkosReasonerName = "SKOS";
    private const string N3RulesReasonerName = "N3Rules";

    public Task<KnowledgeGraphInferenceResult> MaterializeInferenceAsync(
        KnowledgeGraphInferenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => MaterializeInference(options ?? KnowledgeGraphInferenceOptions.Default), cancellationToken);
    }

    private KnowledgeGraphInferenceResult MaterializeInference(KnowledgeGraphInferenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var baseGraph = CreateSnapshot();
        var outputGraph = new Graph();
        outputGraph.Merge(baseGraph);
        var schemaGraph = CreateSchemaGraph(baseGraph, options);
        var appliedReasoners = new List<string>();

        if (options.UseRdfsReasoner)
        {
            var rdfsReasoner = new StaticRdfsReasoner();
            rdfsReasoner.Initialise(schemaGraph);
            rdfsReasoner.Apply(outputGraph);
            appliedReasoners.Add(RdfsReasonerName);
        }

        if (options.UseSkosReasoner)
        {
            var skosReasoner = new StaticSkosReasoner();
            skosReasoner.Initialise(schemaGraph);
            skosReasoner.Apply(outputGraph);
            appliedReasoners.Add(SkosReasonerName);
        }

        if (options.AdditionalN3RuleFilePaths.Count > 0 || options.AdditionalN3RuleTexts.Count > 0)
        {
            var n3Reasoner = new SimpleN3RulesReasoner();
            foreach (var rulesGraph in LoadRuleGraphs(options))
            {
                n3Reasoner.Initialise(rulesGraph);
            }

            n3Reasoner.Apply(outputGraph);
            appliedReasoners.Add(N3RulesReasonerName);
        }

        return new KnowledgeGraphInferenceResult(
            new KnowledgeGraph(outputGraph),
            baseGraph.Triples.Count,
            outputGraph.Triples.Count,
            appliedReasoners);
    }

    private static Graph CreateSchemaGraph(Graph baseGraph, KnowledgeGraphInferenceOptions options)
    {
        var schemaGraph = new Graph();
        schemaGraph.Merge(baseGraph);

        foreach (var filePath in options.AdditionalSchemaFilePaths)
        {
            var additionalGraph = new Graph();
            FileLoader.Load(additionalGraph, filePath);
            schemaGraph.Merge(additionalGraph);
        }

        foreach (var schemaText in options.AdditionalSchemaTexts)
        {
            var additionalGraph = new Graph();
            additionalGraph.LoadFromString(schemaText);
            schemaGraph.Merge(additionalGraph);
        }

        return schemaGraph;
    }

    private static IEnumerable<Graph> LoadRuleGraphs(KnowledgeGraphInferenceOptions options)
    {
        foreach (var filePath in options.AdditionalN3RuleFilePaths)
        {
            var graph = new Graph();
            graph.LoadFromFile(filePath, new Notation3Parser());
            yield return graph;
        }

        foreach (var rulesText in options.AdditionalN3RuleTexts)
        {
            var graph = new Graph();
            graph.LoadFromString(rulesText, new Notation3Parser());
            yield return graph;
        }
    }
}
