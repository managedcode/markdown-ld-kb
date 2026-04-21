using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class ChatClientKnowledgePipelineFlowTests
{
    private const string TempDirectoryPrefix = "markdown-ld-kb-ai-flow-";
    private const string GuidFormat = "N";
    private const string BaseUriText = "https://kb.example/";
    private const string FirstFileName = "01-zero-cost-graph.md";
    private const string SecondFileName = "02-federated-query.md";
    private const string FirstDocumentUri = "https://kb.example/01-zero-cost-graph/";
    private const string SecondDocumentUri = "https://kb.example/02-federated-query/";
    private const string FirstTitle = "Zero Cost Knowledge Graph";
    private const string SecondTitle = "SPARQL Federated Query Note";
    private const string MarkdownKeyword = "markdown";
    private const string EntityExtractionPipelineLabel = "Entity Extraction Pipeline";
    private const string EntityExtractionPipelineId = "https://kb.example/id/entity-extraction-pipeline";
    private const string EntityExtractionPipelineSameAs = "https://example.com/pipelines/entity-extraction-rdf";
    private const string MarkdownLdKnowledgeBankId = "https://kb.example/id/markdown-ld-knowledge-bank";
    private const string MarkdownLdKnowledgeBankSameAs = "https://lqdev.me/resources/ai-memex/blog-post-zero-cost-knowledge-graph-from-markdown/";
    private const string FederatedQueryLabel = "SPARQL Federated Query";
    private const string FederatedQueryId = "https://kb.example/id/sparql-federated-query";
    private const string FederatedQuerySameAs = "https://www.w3.org/TR/sparql11-federated-query/";
    private const string RemoteEndpointId = "https://kb.example/id/remote-endpoint";
    private const string SchemaMentionsPredicate = "schema:mentions";
    private const string SchemaAboutPredicate = "schema:about";
    private const string KbRelatedToPredicate = "kb:relatedTo";
    private const string SearchTermFederated = "federated";
    private const string SearchTermDeleteLiteral = "DELETE";
    private const string SearchSubjectKey = "subject";
    private const string ArticleTitleKey = "articleTitle";
    private const string SourcePlaceholder = "__SOURCE__";
    private const string ChunkSourceLabel = "CHUNK_SOURCE: ";
    private const string UserPromptLabel = "MARKDOWN:";
    private const string ChunkSourcePrefix = "urn:kb:chunk:";
    private const string ChunkSourceSeparator = ":";
    private const int ExpectedDocumentCount = 2;
    private const int ExpectedChatCallCount = 2;
    private const int ExpectedPendingResponseCount = 0;

    private const string FirstMarkdown = """
---
title: Zero Cost Knowledge Graph
tags:
  - markdown
  - rdf
---
# Zero Cost Knowledge Graph

The note explains Markdown-LD Knowledge Bank and the Entity Extraction Pipeline.
""";

    private const string SecondMarkdown = """
---
title: SPARQL Federated Query Note
tags:
  - sparql
---
# SPARQL Federated Query Note

The note explains federated graph lookup through remote endpoints.
""";

    private const string FirstPayload = """
{
  "entities": [
    {
      "id": "https://kb.example/id/entity-extraction-pipeline",
      "type": "schema:SoftwareApplication",
      "label": "Entity Extraction Pipeline",
      "sameAs": ["https://example.com/pipelines/entity-extraction-rdf"]
    },
    {
      "id": "https://kb.example/id/markdown-ld-knowledge-bank",
      "type": "schema:CreativeWork",
      "label": "Markdown-LD Knowledge Bank",
      "sameAs": ["https://lqdev.me/resources/ai-memex/blog-post-zero-cost-knowledge-graph-from-markdown/"]
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "schema:mentions",
      "o": "https://kb.example/id/entity-extraction-pipeline",
      "confidence": 0.98,
      "source": "__SOURCE__"
    },
    {
      "s": "https://kb.example/id/markdown-ld-knowledge-bank",
      "p": "kb:relatedTo",
      "o": "https://kb.example/id/entity-extraction-pipeline",
      "confidence": 0.87,
      "source": "__SOURCE__"
    }
  ]
}
""";

    private const string SecondPayload = """
{
  "entities": [
    {
      "id": "https://kb.example/id/sparql-federated-query",
      "type": "schema:CreativeWork",
      "label": "SPARQL Federated Query",
      "sameAs": ["https://www.w3.org/TR/sparql11-federated-query/"]
    },
    {
      "id": "https://kb.example/id/remote-endpoint",
      "type": "schema:Thing",
      "label": "Remote Endpoint",
      "sameAs": []
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "schema:mentions",
      "o": "https://kb.example/id/sparql-federated-query",
      "confidence": 0.96,
      "source": "__SOURCE__"
    },
    {
      "s": "https://kb.example/id/sparql-federated-query",
      "p": "schema:about",
      "o": "https://kb.example/id/remote-endpoint",
      "confidence": 0.9,
      "source": "__SOURCE__"
    }
  ]
}
""";

    private const string GraphAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/01-zero-cost-graph/> schema:name "Zero Cost Knowledge Graph" ;
                                           schema:keywords "markdown" ;
                                           schema:mentions <https://kb.example/id/entity-extraction-pipeline> .
  <https://kb.example/id/entity-extraction-pipeline> rdf:type <https://schema.org/SoftwareApplication> ;
                                                     schema:sameAs <https://example.com/pipelines/entity-extraction-rdf> .
  <https://kb.example/id/markdown-ld-knowledge-bank> kb:relatedTo <https://kb.example/id/entity-extraction-pipeline> ;
                                                     schema:sameAs <https://lqdev.me/resources/ai-memex/blog-post-zero-cost-knowledge-graph-from-markdown/> .
  <https://kb.example/02-federated-query/> schema:name "SPARQL Federated Query Note" ;
                                           schema:mentions <https://kb.example/id/sparql-federated-query> .
  <https://kb.example/id/sparql-federated-query> rdf:type <https://schema.org/CreativeWork> ;
                                                 schema:about <https://kb.example/id/remote-endpoint> ;
                                                 schema:sameAs <https://www.w3.org/TR/sparql11-federated-query/> .
}
""";

    private const string MentionSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?articleTitle WHERE {
  <https://kb.example/02-federated-query/> schema:name ?articleTitle ;
                                           schema:mentions <https://kb.example/id/sparql-federated-query> .
}
""";

    private const string StringLiteralKeywordSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?subject WHERE {
  ?subject schema:name "DELETE" .
}
""";

    private static readonly string[] Payloads = [FirstPayload, SecondPayload];

    [Test]
    public async Task Directory_pipeline_uses_IChatClient_extraction_to_build_and_search_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempDirectoryPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, FirstFileName), FirstMarkdown);
            await File.WriteAllTextAsync(Path.Combine(root, SecondFileName), SecondMarkdown);

            var responses = new Queue<string>(Payloads);
            var chatClient = new TestChatClient((messages, _) =>
                responses.Dequeue().Replace(SourcePlaceholder, ExtractChunkSource(messages), StringComparison.Ordinal));
            var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText), chatClient);

            var result = await pipeline.BuildFromDirectoryAsync(root);
            var firstChunkSource = CreateChunkSource(result.Documents[0], 0);
            var secondChunkSource = CreateChunkSource(result.Documents[1], 0);

            result.Documents.Count.ShouldBe(ExpectedDocumentCount);
            result.Documents.Select(document => document.DocumentUri.AbsoluteUri).ShouldBe([FirstDocumentUri, SecondDocumentUri]);
            result.Documents.Select(document => document.Title).ShouldBe([FirstTitle, SecondTitle]);
            chatClient.CallCount.ShouldBe(ExpectedChatCallCount);
            responses.Count.ShouldBe(ExpectedPendingResponseCount);

            result.Facts.Entities.Any(entity =>
                entity.Label == EntityExtractionPipelineLabel &&
                entity.Id == EntityExtractionPipelineId &&
                entity.SameAs.Contains(EntityExtractionPipelineSameAs)).ShouldBeTrue();
            result.Facts.Entities.Any(entity =>
                entity.Id == MarkdownLdKnowledgeBankId &&
                entity.SameAs.Contains(MarkdownLdKnowledgeBankSameAs)).ShouldBeTrue();
            result.Facts.Entities.Any(entity =>
                entity.Label == FederatedQueryLabel &&
                entity.Id == FederatedQueryId &&
                entity.SameAs.Contains(FederatedQuerySameAs)).ShouldBeTrue();

            result.Facts.Assertions.Any(assertion =>
                assertion.SubjectId == FirstDocumentUri &&
                assertion.Predicate == SchemaMentionsPredicate &&
                assertion.ObjectId == EntityExtractionPipelineId &&
                assertion.Source == firstChunkSource).ShouldBeTrue();
            result.Facts.Assertions.Any(assertion =>
                assertion.SubjectId == FederatedQueryId &&
                assertion.Predicate == SchemaAboutPredicate &&
                assertion.ObjectId == RemoteEndpointId &&
                assertion.Source == secondChunkSource).ShouldBeTrue();
            result.Facts.Assertions.Any(assertion =>
                assertion.SubjectId == MarkdownLdKnowledgeBankId &&
                assertion.Predicate == KbRelatedToPredicate &&
                assertion.ObjectId == EntityExtractionPipelineId).ShouldBeTrue();

            var graphHasAiFacts = await result.Graph.ExecuteAskAsync(GraphAskQuery);
            graphHasAiFacts.ShouldBeTrue();

            var articleMention = await result.Graph.ExecuteSelectAsync(MentionSelectQuery);
            articleMention.Rows.Single().Values[ArticleTitleKey].ShouldBe(SecondTitle);

            var search = await result.Graph.SearchAsync(SearchTermFederated);
            search.Rows.Any(row =>
                row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
                subject == FederatedQueryId).ShouldBeTrue();

            var keywordSearch = await result.Graph.SearchAsync(MarkdownKeyword);
            keywordSearch.Rows.Any(row =>
                row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
                subject == FirstDocumentUri).ShouldBeTrue();

            var literalKeywordSelect = await result.Graph.ExecuteSelectAsync(StringLiteralKeywordSelectQuery);
            literalKeywordSelect.Rows.ShouldBeEmpty();

            var literalKeywordSearch = await result.Graph.SearchAsync(SearchTermDeleteLiteral);
            literalKeywordSearch.Rows.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateChunkSource(ManagedCode.MarkdownLd.Kb.Pipeline.MarkdownDocument document, int chunkIndex)
    {
        return string.Concat(
            ChunkSourcePrefix,
            document.DocumentUri.AbsoluteUri,
            ChunkSourceSeparator,
            document.Chunks[chunkIndex].ChunkId);
    }

    private static string ExtractChunkSource(IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        var userPrompt = messages.Single(message => message.Role == Microsoft.Extensions.AI.ChatRole.User).Text;
        var sourceLine = userPrompt
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith(ChunkSourceLabel, StringComparison.Ordinal));

        return sourceLine[ChunkSourceLabel.Length..].Trim();
    }
}
