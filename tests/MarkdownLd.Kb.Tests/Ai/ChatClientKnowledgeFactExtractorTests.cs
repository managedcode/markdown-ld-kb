using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Ai;

public sealed class ChatClientKnowledgeFactExtractorTests
{
    private const string BaseUriText = "https://example.com/";
    private const string ChatFlowPath = "articles/markdown-ld.md";
    private const string ChatFlowDocumentId = "https://example.com/articles/markdown-ld/";
    private const string ChatFlowTitle = "Markdown-LD Knowledge Bank";
    private const string ChatFlowTag = "knowledge-graph";
    private const string ChatFlowSearchTerm = "sparql";
    private const string SearchSubjectKey = "subject";
    private const string ArticleEntityId = "https://example.com/id/markdown-ld-knowledge-bank";
    private const string ArticleEntityType = "schema:Article";
    private const string ArticleEntitySameAs = "https://kb.example.com/article";
    private const string SparqlEntityId = "https://example.com/id/sparql";
    private const string SparqlEntityLabel = "SPARQL";
    private const string MentionsPredicate = "schema:mentions";
    private const string InvalidPayload = "not json";
    private const string InvalidChatPath = "articles/invalid-chat.md";
    private const string InvalidChatDocumentId = "https://example.com/articles/invalid-chat/";
    private const string BlankChatPath = "articles/blank.md";
    private const string BlankMarkdown = "   ";
    private const int ExpectedChatCallCount = 1;
    private const int ExpectedBlankChatCallCount = 0;
    private const int ExpectedDocumentCount = 1;
    private const int ExpectedEmptyTripleCount = 0;
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string ChatFlowMarkdown = """
---
title: Markdown-LD Knowledge Bank
tags:
  - knowledge-graph
---
# Introduction

Markdown-LD Knowledge Bank connects Markdown and RDF.
It mentions SPARQL and JSON-LD.
""";

    private static readonly string ChatFlowPayload = $$"""
{
  "entities": [
    {
      "id": "",
      "type": "schema:Thing",
      "label": "{{ChatFlowTitle}}",
      "sameAs": []
    },
    {
      "id": "{{ArticleEntityId}}",
      "type": "{{ArticleEntityType}}",
      "label": "{{ChatFlowTitle}}",
      "sameAs": ["{{ArticleEntitySameAs}}"]
    },
    {
      "id": "{{SparqlEntityId}}",
      "type": "schema:Thing",
      "label": "{{SparqlEntityLabel}}"
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "{{MentionsPredicate}}",
      "o": "{{ArticleEntityId}}",
      "confidence": 0.72,
      "source": ""
    },
    {
      "s": "<ARTICLE_ID>",
      "p": "{{MentionsPredicate}}",
      "o": "{{ArticleEntityId}}",
      "confidence": 0.91,
      "source": "urn:kb:chunk:https://example.com/articles/markdown-ld/:articlesmarkdownldmd"
    },
    {
      "s": "<ARTICLE_ID>",
      "p": "{{MentionsPredicate}}",
      "o": "{{SparqlEntityId}}",
      "confidence": 1.2,
      "source": "urn:kb:chunk:https://example.com/articles/markdown-ld/:articlesmarkdownldmd"
    }
  ]
}
""";

    private const string ChatFlowAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://example.com/articles/markdown-ld/> schema:name "Markdown-LD Knowledge Bank" ;
                                              schema:keywords "knowledge-graph" ;
                                              schema:mentions <https://example.com/id/markdown-ld-knowledge-bank> ;
                                              schema:mentions <https://example.com/id/sparql> .
  <https://example.com/id/markdown-ld-knowledge-bank> rdf:type <https://schema.org/Article> ;
                                                      schema:sameAs <https://kb.example.com/article> .
  <https://example.com/id/sparql> schema:name "SPARQL" .
}
""";

    private const string InvalidChatMarkdown = """
---
title: Invalid Chat
---
# Invalid Chat

Plain body without graph assertions.
""";

    private const string InvalidChatDocumentAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://example.com/articles/invalid-chat/> schema:name "Invalid Chat" .
}
""";

    private const string InvalidChatMentionAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://example.com/articles/invalid-chat/> schema:mentions <https://example.com/id/sparql> .
}
""";

    [Test]
    public async Task Pipeline_flow_uses_chat_entities_to_build_queryable_graph()
    {
        var chatClient = new TestChatClient((_, _) => ChatFlowPayload);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri, chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ChatFlowPath, ChatFlowMarkdown),
        ]);

        var graphHasChatFacts = await result.Graph.ExecuteAskAsync(ChatFlowAskQuery);
        graphHasChatFacts.ShouldBeTrue();

        var search = await result.Graph.SearchAsync(ChatFlowSearchTerm);
        search.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == SparqlEntityId).ShouldBeTrue();

        chatClient.CallCount.ShouldBe(ExpectedChatCallCount);
    }

    [Test]
    public async Task Pipeline_flow_keeps_markdown_graph_queryable_when_chat_payload_is_invalid()
    {
        var chatClient = new TestChatClient((_, _) => InvalidPayload);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri, chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(InvalidChatPath, InvalidChatMarkdown),
        ]);

        result.Documents.Count.ShouldBe(ExpectedDocumentCount);
        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(InvalidChatDocumentId);

        var documentExists = await result.Graph.ExecuteAskAsync(InvalidChatDocumentAskQuery);
        documentExists.ShouldBeTrue();

        var chatMentionWasIgnored = await result.Graph.ExecuteAskAsync(InvalidChatMentionAskQuery);
        chatMentionWasIgnored.ShouldBeFalse();

        chatClient.CallCount.ShouldBe(ExpectedChatCallCount);
    }

    [Test]
    public async Task Pipeline_flow_skips_chat_extraction_for_blank_markdown()
    {
        var chatClient = new TestChatClient((_, _) => ChatFlowPayload);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri, chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(BlankChatPath, BlankMarkdown),
        ]);

        result.Documents.Count.ShouldBe(ExpectedDocumentCount);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Graph.TripleCount.ShouldBe(ExpectedEmptyTripleCount);
        chatClient.CallCount.ShouldBe(ExpectedBlankChatCallCount);
    }
}
