using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class LargeKnowledgeBankChatPromptFlowTests
{
    private const string SearchSubjectKey = "subject";

    private const string LargeChatAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://large-fixture.example/playbooks/extraction-operations-workbook/> schema:mentions <https://large-fixture.example/id/corpus-intake-checklist> ;
    schema:mentions <https://large-fixture.example/id/rdf-normalizer> ;
    schema:mentions <https://large-fixture.example/id/prompt-version-gate> .
  <https://large-fixture.example/playbooks/multilingual-query-governance/> schema:mentions <https://large-fixture.example/id/read-only-query-guard> ;
    schema:mentions <https://large-fixture.example/id/hybrid-semantic-fallback> ;
    schema:mentions <https://large-fixture.example/id/release-evidence-checklist> .
  <https://large-fixture.example/id/rdf-normalizer> rdf:type <https://schema.org/SoftwareApplication> .
  <https://large-fixture.example/id/read-only-query-guard> schema:sameAs <https://example.com/components/read-only-query-guard> .
}
""";

    [Test]
    public async Task Large_chat_corpus_builds_queryable_graph_across_multiple_big_documents_and_chunks()
    {
        var (result, chatClient) = await BuildLargeChatCorpusAsync();

        result.Documents.Count.ShouldBe(LargeKnowledgeBankFixtureCatalog.ChatDocuments.Count);
        result.Documents.Sum(static document => document.Chunks.Count).ShouldBe(8);
        result.Documents.Select(static document => document.DocumentUri.AbsoluteUri).ShouldBe([
            LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook.DocumentUri,
            LargeKnowledgeBankFixtureCatalog.MultilingualGovernance.DocumentUri,
        ]);
        chatClient.CallCount.ShouldBe(8);

        var graphHasExpectedFacts = await result.Graph.ExecuteAskAsync(LargeChatAskQuery);
        graphHasExpectedFacts.ShouldBeTrue();
    }

    [Test]
    [Arguments("Extraction Operations Workbook", "Corpus Intake", "corpus intake checklist")]
    [Arguments("Extraction Operations Workbook", "Corpus Intake / Chunk Shaping", "chunk boundary ledger")]
    [Arguments("Extraction Operations Workbook", "Corpus Intake / RDF Normalization", "schema mapping contract")]
    [Arguments("Extraction Operations Workbook", "Corpus Intake / Cache Rewrite Review", "atomic cache file move")]
    [Arguments("Multilingual Query Governance", "Cross-Language Recall", "cross-language recall map")]
    [Arguments("Multilingual Query Governance", "Cross-Language Recall / Read-Only SPARQL Safety", "read only query guard")]
    [Arguments("Multilingual Query Governance", "Cross-Language Recall / Semantic Fallback", "embedding calibration notes")]
    [Arguments("Multilingual Query Governance", "Cross-Language Recall / Release Evidence", "release evidence checklist")]
    public async Task Large_chat_prompts_preserve_real_section_context_and_markdown_body(
        string title,
        string sectionPath,
        string expectedSnippet)
    {
        var prompts = await CollectPromptsAsync();
        var prompt = prompts.Single(candidate =>
            LargeKnowledgeBankFixtureCatalog.ExtractTitle(candidate) == title &&
            LargeKnowledgeBankFixtureCatalog.ExtractSectionPath(candidate) == sectionPath);

        prompt.ShouldContain("MARKDOWN:");
        prompt.Contains(expectedSnippet, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Test]
    [Arguments("Extraction Operations Workbook", "- summary:")]
    [Arguments("Extraction Operations Workbook", "- tags:")]
    [Arguments("Multilingual Query Governance", "- authors:")]
    [Arguments("Multilingual Query Governance", "- entity_hints:")]
    public async Task Large_chat_prompts_include_front_matter_keys_from_real_documents(
        string title,
        string expectedPromptLine)
    {
        var prompts = await CollectPromptsAsync();
        var prompt = prompts.First(candidate => candidate.Contains("TITLE: " + title, StringComparison.Ordinal));

        prompt.ShouldContain("FRONT_MATTER:");
        prompt.ShouldContain(expectedPromptLine);
    }

    [Test]
    [Arguments("Corpus Intake Checklist", "https://large-fixture.example/id/corpus-intake-checklist")]
    [Arguments("RDF Normalizer", "https://large-fixture.example/id/rdf-normalizer")]
    [Arguments("Read Only Query Guard", "https://large-fixture.example/id/read-only-query-guard")]
    [Arguments("Release Evidence Checklist", "https://large-fixture.example/id/release-evidence-checklist")]
    public async Task Large_chat_graph_supports_search_queries_over_extracted_entities(
        string term,
        string expectedSubject)
    {
        var (result, _) = await BuildLargeChatCorpusAsync();

        var search = await result.Graph.SearchAsync(term);

        search.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == expectedSubject).ShouldBeTrue();
    }

    private static async Task<IReadOnlyList<string>> CollectPromptsAsync()
    {
        var (_, chatClient) = await BuildLargeChatCorpusAsync();

        return chatClient.Requests
            .Select(LargeKnowledgeBankFixtureCatalog.ExtractUserPrompt)
            .ToArray();
    }

    private static async Task<(MarkdownKnowledgeBuildResult Result, TestChatClient ChatClient)> BuildLargeChatCorpusAsync()
    {
        var chatClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = LargeKnowledgeBankFixtureCatalog.BaseUri,
            ChatClient = chatClient,
            ChatModelId = "large-chat-model",
            MarkdownChunker = new WholeSectionMarkdownChunker(),
        });
        var result = await pipeline.BuildAsync(LargeKnowledgeBankFixtureCatalog.CreateChatSources());

        return (result, chatClient);
    }
}
