using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class PlainMarkdownGraphFlowTests
{
    private const string BaseUriText = "https://plain-markdown.example/";
    private const string TempRootPrefix = "markdown-ld-kb-plain-md-";
    private const string GuidFormat = "N";
    private const string PlainFileName = "federation-note.md";
    private const string RootFileName = "root.md";
    private const string PolicyPath = "policy/federation-governance-policy.md";
    private const string RunbookPath = "operations/federated-query-approval-runbook.md";
    private const string StoragePath = "storage/federated-storage-blueprint.md";
    private const string SchemePath = "ontology/federated-query-governance-scheme.md";
    private const string ReferencePath = "reference/wikidata-federated-query-guide.md";
    private const string PlainDocumentUri = "https://plain-markdown.example/federation-note/";
    private const string PlainTitle = "Plain Markdown Federation Note";
    private const string GovernanceTopicLabel = "Federated Query Governance";
    private const string PolicyEndpointText = "https://plain-markdown.example/services/policy";
    private const string RunbookEndpointText = "https://plain-markdown.example/services/runbook";
    private const string StorageEndpointText = "https://plain-markdown.example/services/storage";
    private const string SchemeEndpointText = "https://plain-markdown.example/services/scheme";
    private const string ReferenceEndpointText = "https://plain-markdown.example/services/reference";
    private const string CheckpointText = "https://plain-markdown.example/workflows/query-release-gate/";
    private static readonly Uri BaseUri = new(BaseUriText);
    private static readonly Uri GuideUri = new("https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries");

    private const string PlainMarkdown = """
# Plain Markdown Federation Note

Federated Query Governance governs approvals.
Federated Query Governance governs storage.
""";

    private const string PolicyMarkdown = """
# Federation Governance Policy

Federated Query Governance governs policy.
Federated Query Governance governs review.
""";

    private const string RunbookMarkdown = """
# Federated Query Approval Runbook

Federated Query Governance governs approval.
Federated Query Governance governs rollback.
""";

    private const string StorageMarkdown = """
# Federated Storage Blueprint

Federated Query Governance governs storage.
Federated Query Governance governs fragments.
""";

    private const string SchemeMarkdown = """
# Federated Query Governance Scheme

Federated Query Governance defines the scheme.
Federated Query Governance defines the vocabulary.
""";

    private const string ReferenceMarkdown = """
# Wikidata Federated Query Guide

Federated Query Governance references Wikidata.
Federated Query Governance references service guidance.
""";

    private const string BaseAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://plain-markdown.example/federation-note/> a schema:Article ;
    schema:name "Plain Markdown Federation Note" ;
    schema:hasPart ?section ;
    schema:about ?topic .
  ?section a schema:CreativeWork ;
    schema:name "Plain Markdown Federation Note" ;
    schema:hasPart ?segment .
  ?segment a schema:CreativeWork ;
    schema:name ?segmentText .
  ?topic a schema:DefinedTerm ;
    schema:name "Federated Query Governance" .
  FILTER(CONTAINS(LCASE(STR(?segmentText)), "federated query governance"))
}
""";

    private const string InferenceSchemaText = """
@prefix ex: <https://plain-markdown.example/vocab/> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix schema: <https://schema.org/> .

ex:PlainGovernanceDocument a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .

ex:ApprovalRunbookDocument a rdfs:Class ;
  rdfs:subClassOf ex:PlainGovernanceDocument .

ex:PolicyArtifact a rdfs:Class ;
  rdfs:subClassOf ex:PlainGovernanceDocument .

ex:StorageArtifact a rdfs:Class ;
  rdfs:subClassOf ex:PlainGovernanceDocument .

ex:SchemeArtifact a rdfs:Class ;
  rdfs:subClassOf ex:PlainGovernanceDocument .

ex:ReferenceArtifact a rdfs:Class ;
  rdfs:subClassOf ex:PlainGovernanceDocument .

ex:hasCheckpoint a rdf:Property .
ex:persistenceShape a rdf:Property .
ex:referencesGuide a rdf:Property .
""";

    private const string InferenceRulesText = """
@prefix ex: <https://plain-markdown.example/vocab/> .
@prefix schema: <https://schema.org/> .

{ ?doc schema:about ?topic .
  ?topic schema:name "Federated Query Governance" . }
=> { ?doc a ex:PlainGovernanceDocument } .

{ ?doc schema:name "Federated Query Approval Runbook" ;
       schema:about ?topic .
  ?topic schema:name "Federated Query Governance" . }
=> { ?doc a ex:ApprovalRunbookDocument ;
           ex:hasCheckpoint <https://plain-markdown.example/workflows/query-release-gate/> . } .

{ ?doc schema:name "Federation Governance Policy" ;
       schema:about ?topic .
  ?topic schema:name "Federated Query Governance" . }
=> { ?doc a ex:PolicyArtifact } .

{ ?doc schema:name "Federated Storage Blueprint" ;
       schema:about ?topic . }
=> { ?doc a ex:StorageArtifact ;
           ex:persistenceShape "Linked Data Fragments" . } .

{ ?doc schema:name "Federated Query Governance Scheme" . }
=> { ?doc a ex:SchemeArtifact } .

{ ?doc schema:name "Wikidata Federated Query Guide" . }
=> { ?doc a ex:ReferenceArtifact ;
           ex:referencesGuide <https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries> . } .
""";

    private const string InferenceAskQuery = """
PREFIX ex: <https://plain-markdown.example/vocab/>
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://plain-markdown.example/federation-note/> a ex:PlainGovernanceDocument ;
    schema:about ?topic .
  ?topic schema:name "Federated Query Governance" .
}
""";

    private const string FederatedSelectQuery = """
PREFIX ex: <https://plain-markdown.example/vocab/>
PREFIX schema: <https://schema.org/>
SELECT ?policyTitle ?runbookTitle ?storageTitle ?schemeTitle ?guide ?checkpoint WHERE {
  SERVICE <https://plain-markdown.example/services/policy> {
    ?policy a ex:PolicyArtifact ;
      schema:name ?policyTitle ;
      schema:about ?topic .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/runbook> {
    ?runbook a ex:ApprovalRunbookDocument ;
      schema:name ?runbookTitle ;
      schema:about ?topic ;
      ex:hasCheckpoint ?checkpoint .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/storage> {
    ?storage a ex:StorageArtifact ;
      schema:name ?storageTitle ;
      schema:about ?topic ;
      ex:persistenceShape "Linked Data Fragments" .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/scheme> {
    ?scheme a ex:SchemeArtifact ;
      schema:name ?schemeTitle ;
      schema:about ?topic .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/reference> {
    ?reference a ex:ReferenceArtifact ;
      schema:about ?topic ;
      ex:referencesGuide ?guide .
    ?topic schema:name "Federated Query Governance" .
  }
}
""";

    private const string FederatedAskQuery = """
PREFIX ex: <https://plain-markdown.example/vocab/>
PREFIX schema: <https://schema.org/>
ASK WHERE {
  SERVICE <https://plain-markdown.example/services/policy> {
    ?policy a ex:PolicyArtifact ;
      schema:name "Federation Governance Policy" ;
      schema:about ?topic .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/runbook> {
    ?runbook a ex:ApprovalRunbookDocument ;
      schema:name "Federated Query Approval Runbook" ;
      schema:about ?topic ;
      ex:hasCheckpoint <https://plain-markdown.example/workflows/query-release-gate/> .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/storage> {
    ?storage a ex:StorageArtifact ;
      schema:name "Federated Storage Blueprint" ;
      schema:about ?topic ;
      ex:persistenceShape "Linked Data Fragments" .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/scheme> {
    ?scheme a ex:SchemeArtifact ;
      schema:name "Federated Query Governance Scheme" ;
      schema:about ?topic .
    ?topic schema:name "Federated Query Governance" .
  }
  SERVICE <https://plain-markdown.example/services/reference> {
    ?reference a ex:ReferenceArtifact ;
      schema:about ?topic ;
      ex:referencesGuide <https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries> .
    ?topic schema:name "Federated Query Governance" .
  }
}
""";

    [Test]
    public async Task Plain_markdown_file_builds_a_queryable_graph_without_front_matter()
    {
        using var temp = CreateTempDirectory();
        var filePath = await WriteTextFileAsync(temp.RootPath, PlainFileName, PlainMarkdown);
        var pipeline = CreatePipeline();

        var result = await pipeline.BuildFromFileAsync(filePath);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(PlainDocumentUri);
        result.Documents.Single().Title.ShouldBe(PlainTitle);
        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        (await result.Graph.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();

        var search = await result.Graph.SearchByTokenDistanceAsync("federated query governance storage", 1);
        search.Single().Text.ShouldContain(GovernanceTopicLabel);
    }

    [Test]
    public async Task Plain_markdown_file_materializes_inference_without_front_matter()
    {
        using var temp = CreateTempDirectory();
        var filePath = await WriteTextFileAsync(temp.RootPath, PlainFileName, PlainMarkdown);
        var pipeline = CreatePipeline();
        var built = await pipeline.BuildFromFileAsync(filePath);

        var inference = await built.Graph.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaTexts = [InferenceSchemaText],
            AdditionalN3RuleTexts = [InferenceRulesText],
        });

        inference.InferredTripleCount.ShouldBeGreaterThan(0);
        inference.AppliedReasoners.ShouldContain("RDFS");
        inference.AppliedReasoners.ShouldContain("N3Rules");
        (await inference.Graph.ExecuteAskAsync(InferenceAskQuery)).ShouldBeTrue();
    }

    [Test]
    public async Task Federated_query_execution_can_join_five_plain_markdown_graphs_after_inference()
    {
        using var temp = CreateTempDirectory();
        var fixture = await BuildFederatedFixtureAsync(temp.RootPath, materializeInference: true);

        var result = await fixture.RootGraph.ExecuteFederatedSelectAsync(FederatedSelectQuery, fixture.Options);

        result.Result.Rows.Count.ShouldBe(1);
        result.Result.Rows[0].Values["policyTitle"].ShouldBe("Federation Governance Policy");
        result.Result.Rows[0].Values["runbookTitle"].ShouldBe("Federated Query Approval Runbook");
        result.Result.Rows[0].Values["storageTitle"].ShouldBe("Federated Storage Blueprint");
        result.Result.Rows[0].Values["schemeTitle"].ShouldBe("Federated Query Governance Scheme");
        result.Result.Rows[0].Values["guide"].ShouldBe(GuideUri.AbsoluteUri);
        result.Result.Rows[0].Values["checkpoint"].ShouldBe(CheckpointText);
    }

    [Test]
    public async Task Federated_query_execution_does_not_match_plain_markdown_inference_shape_before_inference()
    {
        using var temp = CreateTempDirectory();
        var fixture = await BuildFederatedFixtureAsync(temp.RootPath, materializeInference: false);

        var select = await fixture.RootGraph.ExecuteFederatedSelectAsync(FederatedSelectQuery, fixture.Options);
        var ask = await fixture.RootGraph.ExecuteFederatedAskAsync(FederatedAskQuery, fixture.Options);

        select.Result.Rows.ShouldBeEmpty();
        ask.Result.ShouldBeFalse();
    }

    private static MarkdownKnowledgePipeline CreatePipeline() =>
        new(BaseUri, extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);

    private static async Task<FederatedFixture> BuildFederatedFixtureAsync(string rootPath, bool materializeInference)
    {
        var pipeline = CreatePipeline();
        var rootFile = await WriteTextFileAsync(rootPath, RootFileName, "# Root");

        return new FederatedFixture(
            await BuildGraphAsync(pipeline, await WriteTextFileAsync(rootPath, PolicyPath, PolicyMarkdown), materializeInference),
            await BuildGraphAsync(pipeline, await WriteTextFileAsync(rootPath, RunbookPath, RunbookMarkdown), materializeInference),
            await BuildGraphAsync(pipeline, await WriteTextFileAsync(rootPath, StoragePath, StorageMarkdown), materializeInference),
            await BuildGraphAsync(pipeline, await WriteTextFileAsync(rootPath, SchemePath, SchemeMarkdown), materializeInference),
            await BuildGraphAsync(pipeline, await WriteTextFileAsync(rootPath, ReferencePath, ReferenceMarkdown), materializeInference),
            (await pipeline.BuildFromFileAsync(rootFile)).Graph);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(
        MarkdownKnowledgePipeline pipeline,
        string filePath,
        bool materializeInference)
    {
        var built = await pipeline.BuildFromFileAsync(filePath);
        if (!materializeInference)
        {
            return built.Graph;
        }

        var inference = await built.Graph.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaTexts = [InferenceSchemaText],
            AdditionalN3RuleTexts = [InferenceRulesText],
        });
        return inference.Graph;
    }

    private static async Task<string> WriteTextFileAsync(string rootPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
        return fullPath;
    }

    private static TempDirectory CreateTempDirectory()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), TempRootPrefix + Guid.NewGuid().ToString(GuidFormat));
        Directory.CreateDirectory(rootPath);
        return new(rootPath);
    }

    private sealed record FederatedFixture(
        KnowledgeGraph PolicyGraph,
        KnowledgeGraph RunbookGraph,
        KnowledgeGraph StorageGraph,
        KnowledgeGraph SchemeGraph,
        KnowledgeGraph ReferenceGraph,
        KnowledgeGraph RootGraph)
    {
        public FederatedSparqlExecutionOptions Options =>
            new()
            {
                AllowedServiceEndpoints =
                [
                    new Uri(PolicyEndpointText),
                    new Uri(RunbookEndpointText),
                    new Uri(StorageEndpointText),
                    new Uri(SchemeEndpointText),
                    new Uri(ReferenceEndpointText),
                ],
                LocalServiceBindings =
                [
                    new FederatedSparqlLocalServiceBinding(new Uri(PolicyEndpointText), PolicyGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(RunbookEndpointText), RunbookGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(StorageEndpointText), StorageGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(SchemeEndpointText), SchemeGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(ReferenceEndpointText), ReferenceGraph),
                ],
            };
    }

    private sealed class TempDirectory(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
