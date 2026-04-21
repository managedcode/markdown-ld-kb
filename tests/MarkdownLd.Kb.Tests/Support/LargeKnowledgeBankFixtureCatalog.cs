using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

public static class LargeKnowledgeBankFixtureCatalog
{
    private const string BaseUriText = "https://large-fixture.example/";
    private const string SectionPathLabel = "SECTION_PATH: ";
    private const string TitleLabel = "TITLE: ";
    private const string ChunkSourceLabel = "CHUNK_SOURCE: ";
    private const string SourcePlaceholder = "__SOURCE__";

    public static readonly Uri BaseUri = new(BaseUriText);

    public static readonly LargeFixtureDocument GraphIngestion = new(
        "Large/Graph/01-graph-ingestion-playbook.md",
        "runbooks/graph-ingestion-playbook.md",
        "https://large-fixture.example/runbooks/graph-ingestion-playbook/",
        "Graph Ingestion Playbook");

    public static readonly LargeFixtureDocument QueryFederation = new(
        "Large/Graph/02-query-federation-runbook.md",
        "runbooks/query-federation-runbook.md",
        "https://large-fixture.example/runbooks/query-federation-runbook/",
        "Query Federation Runbook");

    public static readonly LargeFixtureDocument CacheRecovery = new(
        "Large/Graph/03-cache-recovery-workflow.md",
        "runbooks/cache-recovery-workflow.md",
        "https://large-fixture.example/runbooks/cache-recovery-workflow/",
        "Cache Recovery Workflow");

    public static readonly LargeFixtureDocument SemanticSearch = new(
        "Large/Graph/04-semantic-search-tuning.md",
        "runbooks/semantic-search-tuning.md",
        "https://large-fixture.example/runbooks/semantic-search-tuning/",
        "Semantic Search Tuning");

    public static readonly LargeFixtureDocument IncidentTriage = new(
        "Large/Graph/05-incident-triage-guide.md",
        "runbooks/incident-triage-guide.md",
        "https://large-fixture.example/runbooks/incident-triage-guide/",
        "Incident Triage Guide");

    public static readonly LargeFixtureDocument ReleaseGate = new(
        "Large/Graph/06-release-gate-checklist.md",
        "runbooks/release-gate-checklist.md",
        "https://large-fixture.example/runbooks/release-gate-checklist/",
        "Release Gate Checklist");

    public static readonly LargeFixtureDocument SearchEdgeCaseLab = new(
        "Large/Graph/07-search-edge-case-lab.md",
        "runbooks/search-edge-case-lab.md",
        "https://large-fixture.example/runbooks/search-edge-case-lab/",
        "Search Edge Case Lab");

    public static readonly LargeFixtureDocument QuarterlyArchiveDigest = new(
        "Large/Graph/08-quarterly-archive-digest.md",
        "runbooks/quarterly-archive-digest.md",
        "https://large-fixture.example/runbooks/quarterly-archive-digest/",
        "Quarterly Archive Digest");

    public static readonly LargeFixtureDocument ObservabilityRegression = new(
        "Large/Graph/09-observability-regression-workbook.md",
        "runbooks/observability-regression-workbook.md",
        "https://large-fixture.example/runbooks/observability-regression-workbook/",
        "Observability Regression Workbook");

    public static readonly LargeFixtureDocument ExtractionWorkbook = new(
        "Large/Chat/01-extraction-operations-workbook.md",
        "playbooks/extraction-operations-workbook.md",
        "https://large-fixture.example/playbooks/extraction-operations-workbook/",
        "Extraction Operations Workbook");

    public static readonly LargeFixtureDocument MultilingualGovernance = new(
        "Large/Chat/02-multilingual-query-governance.md",
        "playbooks/multilingual-query-governance.md",
        "https://large-fixture.example/playbooks/multilingual-query-governance/",
        "Multilingual Query Governance");

    public static IReadOnlyList<LargeFixtureDocument> GraphDocuments =>
    [
        GraphIngestion,
        QueryFederation,
        CacheRecovery,
        SemanticSearch,
        IncidentTriage,
        ReleaseGate,
        SearchEdgeCaseLab,
        QuarterlyArchiveDigest,
        ObservabilityRegression,
    ];

    public static IReadOnlyList<LargeFixtureDocument> ChatDocuments =>
    [
        ExtractionWorkbook,
        MultilingualGovernance,
    ];

    public static IReadOnlyList<MarkdownSourceDocument> CreateGraphSources()
    {
        return GraphDocuments
            .Select(CreateSource)
            .ToArray();
    }

    public static IReadOnlyList<MarkdownSourceDocument> CreateChatSources()
    {
        return ChatDocuments
            .Select(CreateSource)
            .ToArray();
    }

    public static string ReadFixture(string fixturePath)
    {
        return FixtureLoader.Read(fixturePath);
    }

    public static MarkdownSourceDocument CreateSource(LargeFixtureDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new MarkdownSourceDocument(document.SourcePath, ReadFixture(document.FixturePath));
    }

    public static TestChatClient CreateChatClient()
    {
        var payloadsBySectionPath = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Corpus Intake"] = "Large/ChatPayloads/corpus-intake.json",
            ["Corpus Intake / Chunk Shaping"] = "Large/ChatPayloads/chunk-shaping.json",
            ["Corpus Intake / RDF Normalization"] = "Large/ChatPayloads/rdf-normalization.json",
            ["Corpus Intake / Cache Rewrite Review"] = "Large/ChatPayloads/cache-rewrite-review.json",
            ["Cross-Language Recall"] = "Large/ChatPayloads/cross-language-recall.json",
            ["Cross-Language Recall / Read-Only SPARQL Safety"] = "Large/ChatPayloads/read-only-sparql-safety.json",
            ["Cross-Language Recall / Semantic Fallback"] = "Large/ChatPayloads/semantic-fallback.json",
            ["Cross-Language Recall / Release Evidence"] = "Large/ChatPayloads/release-evidence.json",
        };

        return new TestChatClient((messages, _) =>
        {
            var prompt = ExtractUserPrompt(messages);
            var sectionPath = ExtractPromptValue(prompt, SectionPathLabel);
            if (!payloadsBySectionPath.TryGetValue(sectionPath, out var fixturePath))
            {
                throw new InvalidOperationException("Missing payload fixture for section path: " + sectionPath);
            }

            return ReadFixture(fixturePath)
                .Replace(SourcePlaceholder, ExtractChunkSource(messages), StringComparison.Ordinal);
        });
    }

    public static string ExtractUserPrompt(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return messages.Single(message => message.Role == ChatRole.User).Text;
    }

    public static string ExtractChunkSource(IReadOnlyList<ChatMessage> messages)
    {
        return ExtractPromptValue(ExtractUserPrompt(messages), ChunkSourceLabel);
    }

    public static string ExtractTitle(IReadOnlyList<ChatMessage> messages)
    {
        return ExtractPromptValue(ExtractUserPrompt(messages), TitleLabel);
    }

    public static string ExtractTitle(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return ExtractPromptValue(prompt, TitleLabel);
    }

    public static string ExtractSectionPath(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return ExtractPromptValue(prompt, SectionPathLabel);
    }

    private static string ExtractPromptValue(string prompt, string label)
    {
        var valueLine = prompt
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith(label, StringComparison.Ordinal));

        return valueLine[label.Length..].Trim();
    }
}

public sealed record LargeFixtureDocument(
    string FixturePath,
    string SourcePath,
    string DocumentUri,
    string Title);
