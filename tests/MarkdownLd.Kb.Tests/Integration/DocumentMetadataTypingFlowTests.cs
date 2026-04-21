using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class DocumentMetadataTypingFlowTests
{
    private const string BaseUriText = "https://metadata.example/";
    private const string DocumentPath = "content/ai-memex-pipeline.md";
    private const string DocumentUri = "https://metadata.example/ai-memex-pipeline/";
    private const string SearchTitle = "AI Memex Pipeline";
    private const string EntryTypeValue = "TechArticle";
    private const string SourceProjectValue = "AI Memex";

    private const string Markdown = """
---
title: AI Memex Pipeline
entryType: TechArticle
sourceProject: AI Memex
---
# AI Memex Pipeline

Library-first graph build.
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://metadata.example/ai-memex-pipeline/> a schema:Article ;
                                                a schema:TechArticle ;
                                                schema:name "AI Memex Pipeline" ;
                                                kb:entryType "TechArticle" ;
                                                kb:sourceProject "AI Memex" .
}
""";

    [Test]
    public async Task Pipeline_materializes_entry_type_source_project_and_article_subtype_flow()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(DocumentPath, Markdown),
        ]);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(DocumentUri);
        result.Documents.Single().Title.ShouldBe(SearchTitle);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();

        var graphHasTypedArticle = await result.Graph.ExecuteAskAsync(AskQuery);
        graphHasTypedArticle.ShouldBeTrue();

        var search = await result.Graph.SearchAsync(SearchTitle);
        search.Rows.Any(row =>
            row.Values.TryGetValue("subject", out var subject) &&
            subject == DocumentUri).ShouldBeTrue();
    }
}
