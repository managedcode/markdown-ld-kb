using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TiktokenKnowledgeGraphFlowTests
{
    private const string BaseUriText = "https://tokens.example/";
    private const string EnglishPath = "content/english-token-lines.md";
    private const string UkrainianPath = "content/ukrainian-token-lines.md";
    private const string NoExtractorPath = "content/no-extractor.md";
    private const string ChatOnlyPath = "content/chat-only.md";
    private const string DiagnosticText = "No fact extractor was selected. Connect an IChatClient or set ExtractionMode to Tiktoken.";
    private const string NoExtractorTitle = "No Extractor";
    private const string NoExtractorDocumentUri = "https://tokens.example/no-extractor/";
    private const string ChatOnlyDocumentUri = "https://tokens.example/chat-only/";
    private const string ChatOnlyChatEntityId = "https://tokens.example/id/chat-only-entity";
    private const string ChatOnlyMarkdownEntityId = "https://tokens.example/id/rdf";
    private const string ChatOnlyEntityLabel = "Chat Only Entity";
    private const string CreativeWorkType = "schema:CreativeWork";
    private const string DefinedTermType = "schema:DefinedTerm";
    private const string SubjectKey = "subject";
    private const int QueryLimit = 1;
    private const int ExpectedSameLanguageMinimumHits = 8;
    private const int ExpectedCrossLanguageMaximumHits = 3;
    private const int ExpectedHeadinglessSectionCount = 1;
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string NoExtractorMarkdown = """
---
title: No Extractor
tags:
  - local
---
# No Extractor

This Markdown mentions [RDF](https://www.w3.org/RDF/) but no extractor is active.
""";

    private const string ChatOnlyMarkdown = """
---
title: Chat Only
---
# Chat Only

This Markdown mentions [RDF](https://www.w3.org/RDF/), but chat extraction is the only active fact source.
""";

    private const string ChatOnlyPayload = """
{
  "entities": [
    {
      "id": "https://tokens.example/id/chat-only-entity",
      "type": "schema:Thing",
      "label": "Chat Only Entity"
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "schema:mentions",
      "o": "https://tokens.example/id/chat-only-entity",
      "confidence": 0.91,
      "source": "https://tokens.example/chat-only/"
    }
  ]
}
""";

    private const string ChatOnlyAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://tokens.example/chat-only/> schema:mentions <https://tokens.example/id/chat-only-entity> .
  <https://tokens.example/id/chat-only-entity> schema:name "Chat Only Entity" .
}
""";

    private const string ChatOnlyHeuristicAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://tokens.example/chat-only/> schema:mentions <https://tokens.example/id/rdf> .
}
""";

    private const string NoExtractorDocumentAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://tokens.example/no-extractor/> schema:name "No Extractor" ;
                                           schema:keywords "local" .
}
""";

    private const string EnglishMarkdown = """
The observatory stores telescope images in a cold archive near the mountain lab.
River sensors use cached forecasts to protect orchards from frost.
The clinic schedule groups urgent vaccine visits before routine checkups.
Solar pumps move clean water into the village storage tank during daylight.
A robotics team labels damaged bridge photos before training the inspection model.
The library catalog connects oral history recordings with family names and dates.
Farm drones map dry soil zones so irrigation crews can prioritize fields.
The museum preserves ceramic fragments with humidity sensors and numbered boxes.
Harbor dispatchers compare storm alerts with cargo departure plans.
The school kitchen tracks allergy meals separately from the regular lunch menu.
""";

    private const string UkrainianMarkdown = """
Обсерваторія зберігає знімки телескопа в холодному архіві біля гірської лабораторії.
Річкові датчики використовують кешовані прогнози, щоб захистити сади від морозу.
Розклад клініки групує термінові візити вакцинації перед плановими оглядами.
Сонячні насоси переміщують чисту воду в сільський накопичувальний бак удень.
Команда робототехніки позначає фото пошкодженого мосту перед навчанням моделі інспекції.
Бібліотечний каталог поєднує записи усної історії з родинними іменами та датами.
Фермерські дрони картографують сухі зони грунту, щоб бригади поливу визначали пріоритетні поля.
Музей зберігає керамічні фрагменти з датчиками вологості та нумерованими коробками.
Диспетчери гавані порівнюють штормові попередження з планами відправлення вантажів.
Шкільна кухня відстежує алергенні страви окремо від звичайного обіднього меню.
""";

    private static readonly TokenDistanceExpectation[] EnglishQueries =
    [
        new("telescope images cold archive mountain lab", "The observatory stores telescope images in a cold archive near the mountain lab."),
        new("orchard frost forecasts river sensors", "River sensors use cached forecasts to protect orchards from frost."),
        new("urgent vaccine visits clinic schedule", "The clinic schedule groups urgent vaccine visits before routine checkups."),
        new("solar pump clean water village tank daylight", "Solar pumps move clean water into the village storage tank during daylight."),
        new("damaged bridge photos robotics inspection model", "A robotics team labels damaged bridge photos before training the inspection model."),
        new("oral history recordings family names library catalog", "The library catalog connects oral history recordings with family names and dates."),
        new("dry soil zones irrigation crews farm drones", "Farm drones map dry soil zones so irrigation crews can prioritize fields."),
        new("ceramic fragments humidity sensors numbered boxes", "The museum preserves ceramic fragments with humidity sensors and numbered boxes."),
        new("storm alerts cargo departure harbor dispatchers", "Harbor dispatchers compare storm alerts with cargo departure plans."),
        new("allergy meals school kitchen lunch menu", "The school kitchen tracks allergy meals separately from the regular lunch menu."),
    ];

    private static readonly TokenDistanceExpectation[] UkrainianQueries =
    [
        new("знімки телескопа холодний архів гірська лабораторія", "Обсерваторія зберігає знімки телескопа в холодному архіві біля гірської лабораторії."),
        new("річкові датчики прогнози захистити сади мороз", "Річкові датчики використовують кешовані прогнози, щоб захистити сади від морозу."),
        new("термінові візити вакцинації розклад клініки", "Розклад клініки групує термінові візити вакцинації перед плановими оглядами."),
        new("сонячні насоси чиста вода сільський бак", "Сонячні насоси переміщують чисту воду в сільський накопичувальний бак удень."),
        new("фото пошкодженого мосту робототехніка модель інспекції", "Команда робототехніки позначає фото пошкодженого мосту перед навчанням моделі інспекції."),
        new("усна історія родинні імена бібліотечний каталог", "Бібліотечний каталог поєднує записи усної історії з родинними іменами та датами."),
        new("сухі зони грунту полив фермерські дрони", "Фермерські дрони картографують сухі зони грунту, щоб бригади поливу визначали пріоритетні поля."),
        new("керамічні фрагменти датчики вологості коробки", "Музей зберігає керамічні фрагменти з датчиками вологості та нумерованими коробками."),
        new("штормові попередження відправлення вантажів гавань", "Диспетчери гавані порівнюють штормові попередження з планами відправлення вантажів."),
        new("алергенні страви шкільна кухня обіднє меню", "Шкільна кухня відстежує алергенні страви окремо від звичайного обіднього меню."),
    ];

    [Test]
    public async Task Auto_mode_without_chat_builds_document_metadata_without_extracted_facts_and_reports_choice_diagnostic()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(NoExtractorPath, NoExtractorMarkdown),
        ]);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        result.Diagnostics.ShouldBe([DiagnosticText]);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Graph.CanSearchByTokenDistance.ShouldBeFalse();

        var documentExists = await result.Graph.ExecuteAskAsync(NoExtractorDocumentAskQuery);
        documentExists.ShouldBeTrue();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await result.Graph.SearchByTokenDistanceAsync("rdf"));
    }

    [Test]
    public async Task Chat_mode_uses_only_chat_facts_without_markdown_heuristic_link_extraction()
    {
        var chatClient = new TestChatClient((_, _) => ChatOnlyPayload);
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            chatClient,
            MarkdownKnowledgeExtractionMode.ChatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ChatOnlyPath, ChatOnlyMarkdown),
        ]);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.ChatClient);
        result.Diagnostics.ShouldBeEmpty();
        chatClient.CallCount.ShouldBe(1);
        result.Facts.Entities.Single().Label.ShouldBe(ChatOnlyEntityLabel);
        result.Facts.Entities.Any(entity => entity.Id == ChatOnlyMarkdownEntityId).ShouldBeFalse();

        var chatFactExists = await result.Graph.ExecuteAskAsync(ChatOnlyAskQuery);
        chatFactExists.ShouldBeTrue();

        var markdownLinkHeuristicWasNotUsed = await result.Graph.ExecuteAskAsync(ChatOnlyHeuristicAskQuery);
        markdownLinkHeuristicWasNotUsed.ShouldBeFalse();

        var search = await result.Graph.SearchAsync(ChatOnlyEntityLabel);
        search.Rows.Single().Values[SubjectKey].ShouldBe(ChatOnlyChatEntityId);
    }

    [Test]
    public async Task Tiktoken_mode_builds_token_distance_graph_and_shows_language_dependent_hit_rates()
    {
        var englishResult = await BuildTokenGraphAsync(EnglishPath, EnglishMarkdown);
        var ukrainianResult = await BuildTokenGraphAsync(UkrainianPath, UkrainianMarkdown);

        englishResult.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        ukrainianResult.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        englishResult.Graph.CanSearchByTokenDistance.ShouldBeTrue();
        ukrainianResult.Graph.CanSearchByTokenDistance.ShouldBeTrue();
        englishResult.Facts.Entities.Count(entity => entity.Type == CreativeWorkType).ShouldBe(EnglishQueries.Length + ExpectedHeadinglessSectionCount);
        ukrainianResult.Facts.Entities.Count(entity => entity.Type == CreativeWorkType).ShouldBe(UkrainianQueries.Length + ExpectedHeadinglessSectionCount);
        englishResult.Facts.Entities.Count(entity => entity.Type == DefinedTermType).ShouldBeGreaterThan(EnglishQueries.Length);
        ukrainianResult.Facts.Entities.Count(entity => entity.Type == DefinedTermType).ShouldBeGreaterThan(UkrainianQueries.Length);
        englishResult.Facts.Assertions.Count.ShouldBeGreaterThanOrEqualTo(EnglishQueries.Length);
        ukrainianResult.Facts.Assertions.Count.ShouldBeGreaterThanOrEqualTo(UkrainianQueries.Length);

        var englishHits = await CountTopHitsAsync(englishResult.Graph, EnglishQueries);
        var ukrainianHits = await CountTopHitsAsync(ukrainianResult.Graph, UkrainianQueries);
        var englishToUkrainianHits = await CountTopHitsAsync(englishResult.Graph, UkrainianQueries);
        var ukrainianToEnglishHits = await CountTopHitsAsync(ukrainianResult.Graph, EnglishQueries);

        englishHits.ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        ukrainianHits.ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
        englishToUkrainianHits.ShouldBeLessThanOrEqualTo(ExpectedCrossLanguageMaximumHits);
        ukrainianToEnglishHits.ShouldBeLessThanOrEqualTo(ExpectedCrossLanguageMaximumHits);
    }

    [Test]
    public async Task Tiktoken_mode_can_disable_auto_related_segment_relations_and_keep_token_distance_search()
    {
        var result = await BuildTokenGraphAsync(
            EnglishPath,
            EnglishMarkdown,
            buildAutoRelatedSegmentRelations: false);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        result.Graph.CanSearchByTokenDistance.ShouldBeTrue();

        var hits = await CountTopHitsAsync(result.Graph, EnglishQueries);
        hits.ShouldBeGreaterThanOrEqualTo(ExpectedSameLanguageMinimumHits);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildTokenGraphAsync(
        string path,
        string markdown,
        bool buildAutoRelatedSegmentRelations = true)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions
            {
                BuildAutoRelatedSegmentRelations = buildAutoRelatedSegmentRelations,
                MaxRelatedSegments = 2,
            });

        return pipeline.BuildAsync([
            new MarkdownSourceDocument(path, markdown),
        ]);
    }

    private static async Task<int> CountTopHitsAsync(KnowledgeGraph graph, IReadOnlyList<TokenDistanceExpectation> expectations)
    {
        var hits = 0;
        foreach (var expectation in expectations)
        {
            var matches = await graph.SearchByTokenDistanceAsync(expectation.Query, QueryLimit);
            if (matches.Single().Text == expectation.ExpectedText)
            {
                hits++;
            }
        }

        return hits;
    }

    private sealed record TokenDistanceExpectation(string Query, string ExpectedText);
}
