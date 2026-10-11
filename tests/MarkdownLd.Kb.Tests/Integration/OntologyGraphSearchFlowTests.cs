using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class OntologyGraphSearchFlowTests
{
    private const string ForecastId = "https://ontology.example/Forecast";
    private const string ProcessId = "https://ontology.example/Process";
    private const string ObservesId = "https://ontology.example/observes";
    private const string OntologyJson = """
        { "@context": { "r": "http://www.w3.org/2000/01/rdf-schema#", "o": "http://www.w3.org/2002/07/owl#", "s": "https://schema.org/" }, "@graph": [
          { "@id": "https://ontology.example/Forecast", "@type": "o:Class", "r:label": "Rainfall Forecast", "r:comment": "Predict regional precipitation", "r:subClassOf": { "@id": "https://ontology.example/Process" } },
          { "@id": "https://ontology.example/Process", "@type": "r:Class", "r:label": "Process" },
          { "@id": "https://ontology.example/observes", "@type": "o:ObjectProperty", "r:label": "Observes rainfall", "r:domain": { "@id": "https://ontology.example/Forecast" }, "r:range": { "@id": "https://ontology.example/Process" } },
          { "@id": "https://ontology.example/instance", "@type": "s:SoftwareApplication", "s:name": "Rainfall instance" }
        ] }
        """;

    [Test]
    public async Task Markdown_ontology_snapshot_exposes_classes_and_keeps_edits_outside_live_graph()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://ontology.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var build = await pipeline.BuildAsync([new MarkdownSourceDocument("weather.md", "# Rainfall forecasting\n\nForecast workflow.")]);
        using var graph = build.Graph;
        var originalCount = graph.TripleCount;
        using var ontology = graph.ToOntologySnapshot();
        ontology.AllClasses.Count().ShouldBe(4);
        ontology.AllProperties.Count().ShouldBe(6);
        var validation = graph.ValidateShacl();
        validation.Conforms.ShouldBeTrue(graph.SerializeTurtle());
        var added = ontology.CreateOntologyClass(new Uri("https://ontology.example/Added"));
        added.AddSuperClass(new Uri(ForecastId));
        ontology.Triples.Count.ShouldBeGreaterThan(originalCount);
        graph.TripleCount.ShouldBe(originalCount);
        (await graph.ExecuteAskAsync("ASK { <https://ontology.example/Added> ?p ?o }")).ShouldBeFalse();
    }

    [Test]
    public async Task Ontology_profile_searches_class_comments_and_hierarchy_evidence()
    {
        using var graph = KnowledgeGraph.LoadJsonLd(OntologyJson);
        var description = await graph.SearchBySchemaAsync("precipitation", KnowledgeGraphSchemaSearchProfile.Ontology);
        description.Matches.Single(match => match.NodeId == ForecastId).Evidence.ShouldContain(evidence => evidence.MatchedText == "Predict regional precipitation" && evidence.Kind == KnowledgeGraphSchemaSearchEvidenceKind.Direct);
        var parents = await graph.SearchBySchemaAsync("Process", KnowledgeGraphSchemaSearchProfile.Ontology);
        parents.Matches.Select(match => match.NodeId).ShouldContain(ForecastId);
        parents.Matches.Single(match => match.NodeId == ForecastId).Evidence.ShouldContain(evidence => evidence.RelatedNodeId == ProcessId);
        parents.FocusedGraph.Nodes.Select(node => node.Id).ShouldContain(ProcessId);
        (await graph.SearchBySchemaAsync("satellite coffee", KnowledgeGraphSchemaSearchProfile.Ontology)).Matches.ShouldBeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Full_text_subject_candidates_refine_real_local_and_opt_in_federated_ontology_search(bool federated)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(OntologyJson);
        using var fullText = await graph.BuildFullTextIndexAsync();
        var lexical = await fullText.SearchAsync("rainfall");
        lexical.Select(match => match.NodeId).ShouldContain(ForecastId);
        lexical.Select(match => match.NodeId).ShouldContain(ObservesId);
        using var root = KnowledgeGraph.LoadJsonLd("""{"@graph": []}""");
        var endpoint = new Uri("https://ontology.example/sparql");
        var profile = KnowledgeGraphSchemaSearchProfile.Ontology with
        {
            CandidateNodeIds = lexical.Select(match => match.NodeId).Distinct().ToArray(),
            FederatedServiceEndpoints = [endpoint]
        };
        var result = federated
            ? await root.SearchBySchemaFederatedAsync("rainfall", profile, new FederatedSparqlExecutionOptions
            {
                AllowedServiceEndpoints = [endpoint],
                LocalServiceBindings = [new(endpoint, graph)]
            })
            : await graph.SearchBySchemaAsync("rainfall", profile);
        result.Matches.Select(match => match.NodeId).Order().ShouldBe(new[] { ForecastId, ObservesId }.Order());
        result.Matches.SelectMany(match => match.Evidence).ShouldAllBe(evidence => evidence.SourceContexts.Count == 0);
        if (federated)
        {
            result.ServiceEndpointSpecifiers.ShouldContain(endpoint.AbsoluteUri);
        }
        (await fullText.SearchAsync("satellite coffee")).ShouldBeEmpty();
    }
}
