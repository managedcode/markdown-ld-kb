using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

internal static class KnowledgeAnswerServiceTestFixtures
{
    public const string BaseUriText = "https://answer.example/";
    public const string NotificationsPath = "content/tools/notification-settings.md";
    public const string TreePath = "content/tools/person-tree-parent-link.md";
    public const string ProvenancePath = "content/tools/cache-verifier.md";
    public const string SummaryOnlyPath = "content/tools/audit-settings.md";
    public const string LabelOnlyPath = "content/tools/label-only.md";
    public const string LabelWithBodyPath = "content/tools/label-with-body.md";
    public const string DuplicatePrimaryPath = "content/tools/duplicate-primary.md";
    public const string DuplicateSecondaryPath = "content/tools/duplicate-secondary.md";
    public const string MultiSourcePrimaryPath = "content/tools/multi-source-primary.md";
    public const string MultiSourceSecondaryPath = "content/tools/multi-source-secondary.md";
    public const string NotificationsDocumentUri = "https://answer.example/tools/notification-settings/";
    public const string SummaryOnlyDocumentUri = "https://answer.example/tools/audit-settings/";
    public const string DuplicateCanonicalUriText = "https://answer.example/tools/duplicate-canonical/";
    public const string MultiSourceEntityUriText = "https://answer.example/id/shared-citation-tool";
    public const string AnswerText = "Use notification settings for inbox delivery preferences [1].";
    public const string ProvenanceAnswerText = "Cache Verifier is covered by the cache verifier source [1].";
    public const string EdgeAnswerText = "Edge case answer.";
    public const string RewrittenQuery = "Notification settings";
    public const string FollowUpQuestion = "What about those?";
    public const string OriginalQuestion = "notification settings";
    public const string ProvenanceQuestion = "Cache Verifier";
    public const string SettingsQuestion = "settings";
    public const string LabelOnlyQuestion = "Label Only";
    public const string LabelWithBodyQuestion = "Graph Label Evidence";
    public const string DuplicateCanonicalQuestion = "duplicatesecondaryanchor";
    public const string MultiSourceEntityQuestion = "Shared Citation Tool";
    public const string OrphanQuestion = "Orphan Capability";
    public const string NoMatchQuestion = "Where is the coffee machine?";
    public const string EmptyRewriteQuestion = "And it?";
    public const string AssistantHistoryAnswer = "Notification settings manage delivery preferences.";
    public const string NoMatchAnswer = "No relevant data found in the knowledge graph.";

    public const string NotificationsMarkdown = """
---
title: Notification settings
summary: Manage notification delivery preferences and inbox alerts for your account.
---
# Notification settings

Use this tool to review notification delivery preferences and inbox alerts.
""";

    public const string TreeMarkdown = """
---
title: Person tree parent link
summary: Add father or mother relationships for a person in a family tree.
---
# Person tree parent link

Use this tool to connect parent relationships in a genealogy tree.
""";

    public const string ProvenanceMarkdown = """
---
title: Cache Verifier Source
graph_entities:
  - label: Cache Verifier
    type: schema:SoftwareApplication
---
# Cache Verifier Source

Cache Verifier validates archive checksums, cache manifests, and release evidence before a restore run.
""";

    public const string SummaryOnlyMarkdown = """
---
title: Audit settings
summary: Audit settings summarize retention controls without body chunks.
---
""";

    public const string LabelOnlyMarkdown = """
---
title: Label Only
---
""";

    public const string LabelWithBodyMarkdown = """
---
title: Graph Label Evidence
---
# Body

This unrelated body text describes operational maintenance without the graph title phrase.
""";

    public const string DuplicatePrimaryMarkdown = """
---
title: Duplicate canonical answer source
summary: Primary duplicate source.
---
# Duplicate canonical answer source

The primary duplicate source contains only ordinary routing notes.
""";

    public const string DuplicateSecondaryMarkdown = """
---
title: Duplicate canonical answer source
summary: Secondary duplicate source.
---
# Duplicate canonical answer source

The secondary duplicate source contains duplicatesecondaryanchor citation evidence.
""";

    public const string MultiSourcePrimaryMarkdown = """
---
title: Primary owner note
graph_entities:
  - label: Shared Citation Tool
    type: schema:SoftwareApplication
---
# Primary owner note

The primary owner note lists routine maintenance only.
""";

    public const string MultiSourceSecondaryMarkdown = """
---
title: Secondary owner note
graph_entities:
  - label: Shared Citation Tool
    type: schema:SoftwareApplication
---
# Secondary owner note

Shared Citation Tool handles cited evidence in the secondary owner note.
""";

    public const string OrphanJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/"
  },
  "@id": "https://external.example/orphan",
  "@type": "schema:SoftwareApplication",
  "schema:name": "Orphan Capability"
}
""";

    public static Task<MarkdownKnowledgeBuildResult> BuildAsync(params MarkdownSourceDocument[] documents)
    {
        var sources = documents.Length == 0
            ?
            [
                new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
                new MarkdownSourceDocument(TreePath, TreeMarkdown),
            ]
            : documents;
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(BaseUriText),
            extractionMode: MarkdownKnowledgeExtractionMode.None);

        return pipeline.BuildAsync(sources);
    }
}
