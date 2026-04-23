using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.Storage.Core;
using ManagedCode.Storage.FileSystem;
using ManagedCode.Storage.FileSystem.Extensions;
using ManagedCode.Storage.FileSystem.Options;
using ManagedCode.Storage.VirtualFileSystem.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeGraphStorageAbstractionFlowTests
{
    private const string BaseUriText = "https://graph-storage.example/";
    private const string QueryRunbookPath = "operations/query-federation-runbook.md";
    private const string ReleaseGatePath = "release/release-gate-checklist.md";
    private const string CatalogPath = "catalog/query-workflows-catalog.md";
    private const string QueryRunbookUri = "https://graph-storage.example/operations/query-federation-runbook/";
    private const string StorageAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX ex: <https://graph-storage.example/vocab/>
ASK WHERE {
  <https://graph-storage.example/operations/query-federation-runbook/> a kb:MarkdownDocument ;
    a ex:Runbook ;
    schema:name "Query Federation Runbook" ;
    schema:about <https://graph-storage.example/id/query-workflows> ;
    kb:memberOf <https://graph-storage.example/id/semantic-operations> ;
    kb:nextStep <https://graph-storage.example/release/release-gate-checklist/> .

  <https://graph-storage.example/release/release-gate-checklist/> a ex:ReleaseGate .
  <https://graph-storage.example/catalog/query-workflows-catalog/> a ex:CapabilityCatalog .
}
""";

    private const string QueryRunbookMarkdown = """
---
title: Query Federation Runbook
about:
  - Query Workflows
graph_groups:
  - Semantic Operations
graph_next_steps:
  - https://graph-storage.example/release/release-gate-checklist/
rdf_prefixes:
  ex: https://graph-storage.example/vocab/
rdf_types:
  - ex:Runbook
---
# Query Federation Runbook

This runbook coordinates federated queries across Markdown corpora and RDF graphs.
""";

    private const string ReleaseGateMarkdown = """
---
title: Release Gate Checklist
about:
  - Release Workflows
graph_groups:
  - Semantic Operations
rdf_prefixes:
  ex: https://graph-storage.example/vocab/
rdf_types:
  - ex:ReleaseGate
---
# Release Gate Checklist

The release gate validates graph integrity and workflow readiness.
""";

    private const string CatalogMarkdown = """
---
title: Query Workflows Catalog
about:
  - Query Workflows
graph_groups:
  - Semantic Operations
graph_related:
  - https://graph-storage.example/operations/query-federation-runbook/
rdf_prefixes:
  ex: https://graph-storage.example/vocab/
rdf_types:
  - ex:CapabilityCatalog
---
# Query Workflows Catalog

The catalog indexes query workflows and semantic operations.
""";

    [Test]
    public async Task Default_storage_backed_di_store_round_trips_a_graph_through_managed_storage()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var storageRoot = Path.Combine(temp.RootPath, "storage-default");

        var services = new ServiceCollection();
        services.AddFileSystemKnowledgeGraphStoreAsDefault(options =>
        {
            options.BaseFolder = storageRoot;
            options.CreateContainerIfNotExists = true;
        });

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IKnowledgeGraphStore>();
        await graph.SaveToStoreAsync(store, "graphs/runtime/storage-graph.ttl");

        var reloaded = await KnowledgeGraph.LoadFromStoreAsync(store, "graphs/runtime/storage-graph.ttl");
        (await reloaded.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        File.Exists(Path.Combine(storageRoot, "graphs", "runtime", "storage-graph.ttl")).ShouldBeTrue();
    }

    [Test]
    public async Task In_memory_store_round_trips_graphs_and_directory_prefixes_through_the_store_abstraction()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));

        var services = new ServiceCollection();
        services.AddInMemoryKnowledgeGraphStore();

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IKnowledgeGraphStore>();
        await graph.SaveToStoreAsync(store, "graphs/runtime/a.ttl");
        await graph.SaveToStoreAsync(
            store,
            "graphs/runtime/nested/b.jsonld",
            new KnowledgeGraphFilePersistenceOptions
            {
                Format = KnowledgeGraphFileFormat.JsonLd,
            });

        var single = await KnowledgeGraph.LoadFromStoreAsync(store, "graphs/runtime/a.ttl");
        var merged = await KnowledgeGraph.LoadFromStoreAsync(
            store,
            "graphs/runtime",
            new KnowledgeGraphLoadOptions
            {
                SearchPattern = "*.*",
                SearchOption = SearchOption.AllDirectories,
            });

        var topDirectoryOnly = await KnowledgeGraph.LoadFromStoreAsync(
            store,
            "graphs/runtime",
            new KnowledgeGraphLoadOptions
            {
                SearchPattern = "*.ttl",
                SearchOption = SearchOption.TopDirectoryOnly,
            });

        (await single.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        (await merged.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        (await topDirectoryOnly.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        merged.TripleCount.ShouldBeGreaterThanOrEqualTo(graph.TripleCount);
    }

    [Test]
    public async Task In_memory_store_reports_missing_prefixes_unsupported_entries_and_empty_supported_sets_explicitly()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var store = new InMemoryKnowledgeGraphStore();

        await graph.SaveToStoreAsync(
            store,
            "graphs/runtime/unsupported.bin",
            new KnowledgeGraphFilePersistenceOptions
            {
                Format = KnowledgeGraphFileFormat.Turtle,
            });
        await graph.SaveToStoreAsync(store, "other/outside.ttl");

        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await KnowledgeGraph.LoadFromStoreAsync(store, "graphs/missing"));

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KnowledgeGraph.LoadFromStoreAsync(
                store,
                "graphs/runtime",
                new KnowledgeGraphLoadOptions
                {
                    SearchPattern = "*.*",
                    SearchOption = SearchOption.AllDirectories,
                    SkipUnsupportedFiles = false,
                }));

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await KnowledgeGraph.LoadFromStoreAsync(
                store,
                "graphs/runtime",
                new KnowledgeGraphLoadOptions
                {
                    SearchPattern = "*.jsonld",
                    SearchOption = SearchOption.TopDirectoryOnly,
                }));
    }

    [Test]
    public async Task Keyed_storage_backed_di_stores_resolve_distinct_roots()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var alphaRoot = Path.Combine(temp.RootPath, "storage-alpha");
        var betaRoot = Path.Combine(temp.RootPath, "storage-beta");

        var services = new ServiceCollection();
        services.AddFileSystemStorage("alpha", options =>
        {
            options.BaseFolder = alphaRoot;
            options.CreateContainerIfNotExists = true;
        });
        services.AddFileSystemStorage("beta", options =>
        {
            options.BaseFolder = betaRoot;
            options.CreateContainerIfNotExists = true;
        });
        services.AddKeyedStorageBackedKnowledgeGraphStore<IFileSystemStorage>("alpha");
        services.AddKeyedStorageBackedKnowledgeGraphStore<IFileSystemStorage>("beta");

        await using var provider = services.BuildServiceProvider();
        var alphaStore = provider.GetRequiredKeyedService<IKnowledgeGraphStore>("alpha");
        var betaStore = provider.GetRequiredKeyedService<IKnowledgeGraphStore>("beta");

        await graph.SaveToStoreAsync(alphaStore, "graphs/runtime/alpha.ttl");
        await graph.SaveToStoreAsync(betaStore, "graphs/runtime/beta.ttl");

        var alphaGraph = await KnowledgeGraph.LoadFromStoreAsync(alphaStore, "graphs/runtime/alpha.ttl");
        var betaGraph = await KnowledgeGraph.LoadFromStoreAsync(betaStore, "graphs/runtime/beta.ttl");

        (await alphaGraph.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        (await betaGraph.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        File.Exists(Path.Combine(alphaRoot, "graphs", "runtime", "alpha.ttl")).ShouldBeTrue();
        File.Exists(Path.Combine(betaRoot, "graphs", "runtime", "beta.ttl")).ShouldBeTrue();
    }

    [Test]
    public async Task Virtual_file_system_registration_can_back_the_graph_store()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var storageRoot = Path.Combine(temp.RootPath, "storage-vfs");

        var services = new ServiceCollection();
        services.AddFileSystemStorageAsDefault(options =>
        {
            options.BaseFolder = storageRoot;
            options.CreateContainerIfNotExists = true;
        });
        services.AddVirtualFileSystem(_ => { });
        services.AddVirtualFileSystemKnowledgeGraphStore();

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IKnowledgeGraphStore>();
        await graph.SaveToStoreAsync(store, "graphs/runtime/vfs.ttl");

        var reloaded = await KnowledgeGraph.LoadFromStoreAsync(store, "graphs/runtime/vfs.ttl");
        (await reloaded.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        File.Exists(Path.Combine(storageRoot, "graphs", "runtime", "vfs.ttl")).ShouldBeTrue();
    }

    [Test]
    [NotInParallel]
    public async Task Relative_file_paths_round_trip_through_the_file_system_convenience_store()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var relativeFilePath = Path.Combine("relative-storage", "runtime-graph.ttl");

        using var scope = new WorkingDirectoryScope(temp.RootPath);
        await graph.SaveToFileAsync(relativeFilePath);

        var reloaded = await KnowledgeGraph.LoadFromFileAsync(relativeFilePath);
        (await reloaded.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        File.Exists(Path.Combine(temp.RootPath, relativeFilePath)).ShouldBeTrue();
    }

    [Test]
    public async Task Storage_backed_store_reports_transport_failures_explicitly()
    {
        using var temp = CreateTempDirectory();
        var graph = await BuildGraphAsync(Path.Combine(temp.RootPath, "docs"));
        var blockedPath = Path.Combine(temp.RootPath, "blocked-root");
        await File.WriteAllTextAsync(blockedPath, "blocked");

        using IStorage storage = new FileSystemStorage(new FileSystemStorageOptions
        {
            BaseFolder = blockedPath,
            CreateContainerIfNotExists = true,
        });

        var store = new StorageKnowledgeGraphStore(storage);
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await graph.SaveToStoreAsync(store, "graphs/runtime/failure.ttl"));

        exception.Message.ShouldContain("Knowledge graph save failed");
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(string rootPath)
    {
        await WriteMarkdownDatasetAsync(rootPath);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var result = await pipeline.BuildFromDirectoryAsync(rootPath);

        (await result.Graph.ExecuteAskAsync(StorageAskQuery)).ShouldBeTrue();
        return result.Graph;
    }

    private static async Task WriteMarkdownDatasetAsync(string rootPath)
    {
        await WriteTextFileAsync(rootPath, QueryRunbookPath, QueryRunbookMarkdown);
        await WriteTextFileAsync(rootPath, ReleaseGatePath, ReleaseGateMarkdown);
        await WriteTextFileAsync(rootPath, CatalogPath, CatalogMarkdown);
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
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "markdown-ld-kb-storage-" + Guid.NewGuid().ToString("N"));
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
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class WorkingDirectoryScope : IDisposable
    {
        private readonly string _originalCurrentDirectory = Directory.GetCurrentDirectory();

        public WorkingDirectoryScope(string rootPath)
        {
            Directory.SetCurrentDirectory(rootPath);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalCurrentDirectory);
        }
    }
}
