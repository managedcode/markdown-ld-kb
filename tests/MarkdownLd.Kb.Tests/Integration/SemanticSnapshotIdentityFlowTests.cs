using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SemanticSnapshotIdentityFlowTests
{
    [Test]
    [Arguments("token-section")]
    [Arguments("token-segment")]
    [Arguments("token-topic")]
    public async Task Authored_literal_containing_a_retrieval_path_remains_in_semantic_snapshot(string segment)
    {
        var title = $"guide/{segment}/evidence";
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://literal-path.example/"),
            extractionMode: MarkdownKnowledgeExtractionMode.None);
        using var graph = (await pipeline.BuildAsync([
            new MarkdownSourceDocument("guide.md", $"---\ntitle: {title}\n---\n# Evidence\n\nRead the authored guide."),
        ])).Graph;

        var complete = graph.ToCompleteSnapshot();
        var semantic = graph.ToSemanticSnapshot();
        var literalId = $"literal:{title}";

        complete.Nodes.ShouldContain(node => node.Id == literalId && node.Kind == KnowledgeGraphNodeKind.Literal);
        semantic.Nodes.ShouldContain(node => node.Id == literalId && node.Label == title);
        semantic.Edges.ShouldContain(edge => edge.PredicateId == "https://schema.org/name" && edge.ObjectId == literalId);
        semantic.SerializeTurtle().ShouldContain(title);
        (await graph.ExecuteSelectAsync("SELECT ?title WHERE { ?document <https://schema.org/name> ?title }")).Rows
            .ShouldContain(row => row.Values.Values.Contains(title));
    }
}
