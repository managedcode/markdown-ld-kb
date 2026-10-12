using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class AuthorizedGraphProjectionTests
{
    [Test]
    public async Task Projection_removes_private_subjects_relationship_targets_and_disallowed_literals()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s":"https://schema.org/" }, "@graph": [
              { "@id":"https://projection.example/public", "s:name":"Weather", "s:description":"SEEDED_PRIVATE_FIELD",
                "s:about":{"@id":"https://projection.example/private"} },
              { "@id":"https://projection.example/private", "s:name":"SEEDED_PRIVATE_ENTITY" } ] }
            """);
        using var projected = graph.Project(new(new HashSet<string>(StringComparer.Ordinal) { "https://projection.example/public" })
        { AllowedPredicateIds = new HashSet<string>(StringComparer.Ordinal) { "https://schema.org/name", "https://schema.org/about" } });
        projected.SerializeJsonLd().ShouldNotContain("SEEDED_PRIVATE");
        projected.ToCompleteSnapshot().Edges.Count.ShouldBe(1);
        (await projected.ExecuteSelectAsync("SELECT ?name WHERE { ?s <https://schema.org/name> ?name }")).Rows.Count.ShouldBe(1);
        graph.ToCompleteSnapshot().Edges.Count.ShouldBe(4);
    }

    [Test]
    public async Task Explicitly_approved_resource_targets_preserve_object_only_edges_without_importing_private_subjects()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s":"https://schema.org/" }, "@graph": [
              { "@id":"https://projection.example/public", "s:name":"Weather", "s:about":{"@id":"https://projection.example/shared"} },
              { "@id":"https://projection.example/shared", "s:name":"Private shared label" } ] }
            """);
        using var projected = graph.Project(new(new HashSet<string>(StringComparer.Ordinal) { "https://projection.example/public" })
        { AllowedResourceObjectIds = new HashSet<string>(StringComparer.Ordinal) { "https://projection.example/shared" } });
        projected.TripleCount.ShouldBe(2);
        projected.SerializeJsonLd().ShouldNotContain("Private shared label");
        using var merged = KnowledgeGraph.LoadJsonLd("""{ "@id":"https://projection.example/new", "https://schema.org/name":"New" }""");
        await projected.MergeAsync(merged);
        graph.TripleCount.ShouldBe(3);
    }
}
