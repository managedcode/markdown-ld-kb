using ManagedCode.MarkdownLd.Kb.Parsing;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Parsing;

public sealed class MarkdownLinkResolutionFlowTests
{
    private const string BaseUri = "https://links.example/";
    private const string SourcePath = "content/guides/setup/intro.md";
    private const string RunbookDisplay = "Runbook";
    private const string LocalStepsDisplay = "local steps";
    private const string DiagramDisplay = "diagram";
    private const string ExpectedRunbookTarget = "https://links.example/guides/runbooks/cache-restore/#steps";
    private const string ExpectedLocalStepsTarget = "https://links.example/guides/setup/intro/#local-steps";
    private const string ExpectedDiagramTarget = "https://links.example/guides/assets/topology.png";

    private const string Markdown = """
# Intro

See the [Runbook](../runbooks/cache-restore.md#steps), [local steps](#local-steps), and ![diagram](../assets/topology.png).
""";

    [Test]
    public void Parser_resolves_relative_markdown_and_image_links_from_current_source_path()
    {
        var document = new MarkdownDocumentParser().Parse(
            new MarkdownDocumentSource(Markdown, SourcePath, BaseUri));

        var runbook = document.Links.Single(link => link.DisplayText == RunbookDisplay);
        runbook.IsDocumentLink.ShouldBeTrue();
        runbook.ResolvedTarget.ShouldBe(ExpectedRunbookTarget);

        var localSteps = document.Links.Single(link => link.DisplayText == LocalStepsDisplay);
        localSteps.IsDocumentLink.ShouldBeTrue();
        localSteps.ResolvedTarget.ShouldBe(ExpectedLocalStepsTarget);

        var image = document.Links.Single(link => link.DisplayText == DiagramDisplay);
        image.IsImage.ShouldBeTrue();
        image.ResolvedTarget.ShouldBe(ExpectedDiagramTarget);
    }
}
