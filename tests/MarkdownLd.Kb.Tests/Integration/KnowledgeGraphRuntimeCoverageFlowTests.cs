using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeGraphRuntimeCoverageFlowTests
{
    private const string BaseUriText = "https://graph-runtime.example/";
    private const string NotePath = "runtime/runtime-coverage-note.md";
    private const string NoteUri = "https://graph-runtime.example/runtime/runtime-coverage-note/";
    private const string TempRootPrefix = "markdown-ld-kb-runtime-coverage-";
    private const string GuidFormat = "N";
    private const string UnsupportedFileName = "graph.unsupported";
    private const string MixedDirectoryName = "mixed";

    private static readonly (KnowledgeGraphFileFormat Format, string FileName)[] FormatCases =
    [
        (KnowledgeGraphFileFormat.Turtle, "runtime-graph.ttl"),
        (KnowledgeGraphFileFormat.JsonLd, "runtime-graph.json"),
        (KnowledgeGraphFileFormat.RdfXml, "runtime-graph.rdf"),
        (KnowledgeGraphFileFormat.NTriples, "runtime-graph.nt"),
        (KnowledgeGraphFileFormat.Notation3, "runtime-graph.n3"),
        (KnowledgeGraphFileFormat.TriG, "runtime-graph.trig"),
        (KnowledgeGraphFileFormat.NQuads, "runtime-graph.nq"),
    ];

    private const string RuntimeCoverageMarkdown = """
---
title: Runtime Coverage Note
about:
  - Runtime Workflows
graph_groups:
  - Runtime Operations
graph_next_steps:
  - https://graph-runtime.example/runtime/runtime-release-gate/
rdf_prefixes:
  ex: https://graph-runtime.example/vocab/
rdf_types:
  - ex:RuntimeNote
---
# Runtime Coverage Note

This note exercises graph persistence, inference, and full-text adapters.
""";

    private const string BaseAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/runtime/runtime-coverage-note/> a kb:MarkdownDocument ;
    a ex:RuntimeNote ;
    schema:name "Runtime Coverage Note" ;
    schema:about <https://graph-runtime.example/id/runtime-workflows> ;
    kb:memberOf <https://graph-runtime.example/id/runtime-operations> ;
    kb:nextStep <https://graph-runtime.example/runtime/runtime-release-gate/> .
}
""";

    private const string InferenceSchemaText = """
@prefix ex: <https://graph-runtime.example/vocab/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix schema: <https://schema.org/> .
@prefix skos: <http://www.w3.org/2004/02/skos/core#> .

ex:WorkflowArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .

ex:RuntimeNote a rdfs:Class ;
  rdfs:subClassOf ex:WorkflowArtifact .

<https://graph-runtime.example/id/runtime-workflows> a skos:Concept ;
  skos:broader <https://graph-runtime.example/id/runtime-operations> .

<https://graph-runtime.example/id/runtime-operations> a skos:Concept .
""";

    private const string InferenceRulesText = """
@prefix ex: <https://graph-runtime.example/vocab/> .
@prefix kb: <urn:managedcode:markdown-ld-kb:vocab:> .

{ ?workflow kb:nextStep ?step } => { ?workflow ex:hasOperationalSuccessor ?step } .
""";

    private const string InferenceAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/runtime/runtime-coverage-note/> a ex:WorkflowArtifact ;
    a schema:CreativeWork ;
    schema:about <https://graph-runtime.example/id/runtime-operations> ;
    ex:hasOperationalSuccessor <https://graph-runtime.example/runtime/runtime-release-gate/> .
}
""";

    [Test]
    public async Task Graph_persistence_covers_supported_formats_and_directory_policies()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(temp.RootPath);

        foreach (var formatCase in FormatCases)
        {
            var filePath = Path.Combine(temp.RootPath, formatCase.FileName);
            await graph.SaveToFileAsync(
                filePath,
                new KnowledgeGraphFilePersistenceOptions
                {
                    Format = formatCase.Format,
                });

            var reloaded = await KnowledgeGraph.LoadFromFileAsync(filePath);
            (await reloaded.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();
        }

        var mixedDirectory = Path.Combine(temp.RootPath, MixedDirectoryName);
        Directory.CreateDirectory(mixedDirectory);

        var inferredPath = Path.Combine(mixedDirectory, "runtime-graph.ttl");
        await graph.SaveToFileAsync(inferredPath);
        await File.WriteAllTextAsync(Path.Combine(mixedDirectory, "ignored.bin"), "ignored");

        var merged = await KnowledgeGraph.LoadFromDirectoryAsync(
            mixedDirectory,
            new KnowledgeGraphLoadOptions
            {
                SearchPattern = "*",
                SearchOption = SearchOption.TopDirectoryOnly,
            });

        (await merged.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KnowledgeGraph.LoadFromDirectoryAsync(
                mixedDirectory,
                new KnowledgeGraphLoadOptions
                {
                    SearchPattern = "*",
                    SearchOption = SearchOption.TopDirectoryOnly,
                    SkipUnsupportedFiles = false,
                }));

        var unsupportedPath = Path.Combine(temp.RootPath, UnsupportedFileName);
        await File.WriteAllTextAsync(unsupportedPath, "unsupported");

        await Should.ThrowAsync<InvalidDataException>(async () => await graph.SaveToFileAsync(unsupportedPath));
        await Should.ThrowAsync<InvalidDataException>(async () => await KnowledgeGraph.LoadFromFileAsync(unsupportedPath));
        await Should.ThrowAsync<ArgumentException>(async () => await graph.SaveToFileAsync(string.Empty));
        await Should.ThrowAsync<ArgumentException>(async () => await KnowledgeGraph.LoadFromFileAsync(string.Empty));
        await Should.ThrowAsync<FileNotFoundException>(async () => await KnowledgeGraph.LoadFromFileAsync(Path.Combine(temp.RootPath, "missing.ttl")));

        var unsupportedOnlyDirectory = Path.Combine(temp.RootPath, "unsupported-only");
        Directory.CreateDirectory(unsupportedOnlyDirectory);
        await File.WriteAllTextAsync(Path.Combine(unsupportedOnlyDirectory, "ignored.bin"), "ignored");
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KnowledgeGraph.LoadFromDirectoryAsync(
                unsupportedOnlyDirectory,
                new KnowledgeGraphLoadOptions
                {
                    SearchPattern = "*",
                    SearchOption = SearchOption.TopDirectoryOnly,
                }));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await graph.SaveToFileAsync(
                Path.Combine(temp.RootPath, "runtime-graph.ttl"),
                new KnowledgeGraphFilePersistenceOptions
                {
                    Format = (KnowledgeGraphFileFormat)999,
                }));

        var validPath = Path.Combine(temp.RootPath, FormatCases[0].FileName);
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KnowledgeGraph.LoadFromFileAsync(
                validPath,
                new KnowledgeGraphLoadOptions
                {
                    Format = (KnowledgeGraphFileFormat)999,
                }));
    }

    [Test]
    public async Task Inference_and_full_text_runtime_cover_text_rules_directory_indexes_and_empty_queries()
    {
        using var temp = CreateTempDirectory();
        var built = await BuildGraphAsync(temp.RootPath);

        var inference = await built.MaterializeInferenceAsync(
            new KnowledgeGraphInferenceOptions
            {
                AdditionalSchemaTexts = [InferenceSchemaText],
                AdditionalN3RuleTexts = [InferenceRulesText],
            });

        (await inference.Graph.ExecuteAskAsync(InferenceAskQuery)).ShouldBeTrue();

        var objectIndexDirectory = Path.Combine(temp.RootPath, "fulltext-objects");
        using var objectIndex = await inference.Graph.BuildFullTextIndexAsync(
            new KnowledgeGraphFullTextIndexOptions
            {
                DirectoryPath = objectIndexDirectory,
                Target = KnowledgeGraphFullTextIndexTarget.Objects,
            });

        Directory.Exists(objectIndexDirectory).ShouldBeTrue();
        (await objectIndex.SearchAsync("Runtime Coverage Note")).Count.ShouldBeGreaterThan(0);
        (await objectIndex.SearchAsync("")).Count.ShouldBe(0);

        using var predicateIndex = await inference.Graph.BuildFullTextIndexAsync(
            new KnowledgeGraphFullTextIndexOptions
            {
                Target = KnowledgeGraphFullTextIndexTarget.Predicates,
            });

        var predicateMatches = await predicateIndex.SearchAsync("name");
        predicateMatches.ShouldNotBeNull();
        (await predicateIndex.SearchAsync("")).Count.ShouldBe(0);
    }

    [Test]
    public async Task Full_text_index_builds_are_safe_under_parallel_runtime_calls()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(temp.RootPath);

        var tasks = Enumerable.Range(0, 4)
            .Select(async index =>
            {
                using var fullText = await graph.BuildFullTextIndexAsync(
                    new KnowledgeGraphFullTextIndexOptions
                    {
                        DirectoryPath = Path.Combine(temp.RootPath, "parallel-" + index),
                    });

                (await fullText.SearchAsync("Runtime Coverage Note")).Count.ShouldBeGreaterThan(0);
            });

        await Task.WhenAll(tasks);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(string rootPath)
    {
        var filePath = await WriteTextFileAsync(rootPath, NotePath, RuntimeCoverageMarkdown);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var result = await pipeline.BuildFromFileAsync(
            filePath,
            new KnowledgeDocumentConversionOptions
            {
                CanonicalUri = new Uri(NoteUri),
            });

        (await result.Graph.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();
        return result.Graph;
    }

    private static async Task<string> WriteTextFileAsync(string rootPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content);
        return fullPath;
    }

    private static TempDirectory CreateTempDirectory()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), TempRootPrefix + Guid.NewGuid().ToString(GuidFormat));
        Directory.CreateDirectory(rootPath);
        return new TempDirectory(rootPath);
    }

    private sealed class TempDirectory(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
    }
}
