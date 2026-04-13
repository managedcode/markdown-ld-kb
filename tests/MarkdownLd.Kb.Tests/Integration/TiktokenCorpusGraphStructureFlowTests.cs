using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TiktokenCorpusGraphStructureFlowTests
{
    private const string BaseUriText = "https://corpus-graph.example/";
    private const string SourcePath = "content/observatory.md";
    private const string LooseSourcePath = "content/loose-note.md";
    private const string EntityHintsSourcePath = "content/entity-hints.md";
    private const string CamelEntityHintsSourcePath = "content/camel-entity-hints.md";
    private const string MixedEntityHintsSourcePath = "content/mixed-entity-hints.md";
    private const string DocumentUri = "https://corpus-graph.example/observatory/";
    private const string MixedEntityHintsDocumentUri = "https://corpus-graph.example/mixed-entity-hints/";
    private const string ObservatoryHeading = "Observatory Operations";
    private const string LooseTitle = "Loose Corpus Note";
    private const string PortableAnalyzerLabel = "Portable Analyzer";
    private const string PortableAnalyzerSameAs = "https://example.com/tools/portable-analyzer";
    private const string CamelHintLabel = "Camel Hint";
    private const string CamelHintSameAs = "https://example.com/entities/camel-hint";
    private const string ScalarHintLabel = "Scalar Hint";
    private const string NumericHintLabel = "42";
    private const string ValueHintLabel = "Value Hint";
    private const string ValueHintType = "schema:Dataset";
    private const string ValueHintSameAs = "https://example.com/entities/value-hint";
    private const string UkrainianObservatoryLabel = "Українська обсерваторія";
    private const string TokenEntityHintIdPrefixText = "https://corpus-graph.example/token-entity-hint/";
    private const string TelescopeQuery = "telescope archive mountain observatory";
    private const string LooseQuery = "orchard frost sensors sunrise";
    private const string MixedEntityHintsQuery = "scalar hint value hint numeric front matter";
    private const string SchemaThingType = "schema:Thing";
    private const string DefinedTermType = "schema:DefinedTerm";
    private const string TelescopeStem = "telescope";
    private const string FrostSensorStem = "frost sensors";
    private const string UkrainianTelescopeStem = "телескоп";
    private const string FrenchTelescopeStem = "télescope";
    private const int QueryLimit = 1;
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string StructuredMarkdown = """
---
title: Local Corpus Graph
---
# Observatory Operations

The observatory stores telescope images in a cold archive near the mountain lab.
The archive team calibrates telescope lenses before winter maintenance.

# River Monitoring

River sensors use cached forecasts to protect orchards from frost.
""";

    private const string LooseMarkdown = """
---
title: Loose Corpus Note
---
Loose orchard analysts compare frost sensors before sunrise.
""";

    private const string EntityHintsMarkdown = """
---
title: Entity Hint Note
entity_hints:
  - label: Portable Analyzer
    type: schema:SoftwareApplication
    sameAs:
      - https://example.com/tools/portable-analyzer
  - label: Українська обсерваторія
    type: schema:Thing
---
Portable Analyzer reviews local corpus notes beside the Ukrainian observatory.
""";

    private const string CamelEntityHintsMarkdown = """
---
title: Camel Entity Hint Note
entityHints:
  - name: Camel Hint
    sameAs: https://example.com/entities/camel-hint
---
Camel Hint appears in a local corpus note.
""";

    private const string MixedEntityHintsMarkdown = """
---
title: Mixed Entity Hint Note
entity_hints:
  - Scalar Hint
  - 42
  - ""
  -
  - value: Value Hint
    type: schema:Dataset
    same_as: https://example.com/entities/value-hint
  - label:
  - {}
---
Scalar Hint, Value Hint, and numeric front matter hints remain explicit graph entities.
""";

    private const string MultilingualMarkdown = """
# Українська обсерваторія

Обсерваторія зберігає знімки телескопа в холодному архіві біля гірської лабораторії.

# Observatoire français

L'observatoire conserve les images du télescope dans une archive froide près du laboratoire de montagne.
""";

    private const string StructureAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://corpus-graph.example/observatory/> schema:hasPart ?section .
  ?section a schema:CreativeWork ;
           schema:name "Observatory Operations" ;
           schema:hasPart ?segment .
  ?segment schema:about ?topic .
  ?topic a schema:DefinedTerm ;
         schema:name ?topicName .
  FILTER(CONTAINS(LCASE(STR(?topicName)), "telescope"))
}
""";

    private const string LooseStructureAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://corpus-graph.example/loose-note/> schema:hasPart ?section .
  ?section a schema:CreativeWork ;
           schema:name "Loose Corpus Note" ;
           schema:hasPart ?segment .
  ?segment a schema:CreativeWork ;
           schema:name "Loose orchard analysts compare frost sensors before sunrise." .
}
""";

    private const string EntityHintAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://corpus-graph.example/entity-hints/> schema:mentions ?analyzer ;
                                                 schema:mentions ?ukrainian .
  ?analyzer a schema:SoftwareApplication ;
            schema:name "Portable Analyzer" ;
            schema:sameAs <https://example.com/tools/portable-analyzer> .
  ?ukrainian schema:name "Українська обсерваторія" .
  FILTER(STRSTARTS(STR(?ukrainian), "https://corpus-graph.example/token-entity-hint/"))
}
""";

    private const string CamelEntityHintAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://corpus-graph.example/camel-entity-hints/> schema:mentions ?hint .
  ?hint schema:name "Camel Hint" ;
        schema:sameAs <https://example.com/entities/camel-hint> .
}
""";

    private const string MixedEntityHintsAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://corpus-graph.example/mixed-entity-hints/> schema:mentions ?scalar ;
                                                        schema:mentions ?number ;
                                                        schema:mentions ?value .
  ?scalar a schema:Thing ;
          schema:name "Scalar Hint" .
  ?number a schema:Thing ;
          schema:name "42" .
  ?value a schema:Dataset ;
         schema:name "Value Hint" ;
         schema:sameAs <https://example.com/entities/value-hint> .
}
""";

    [Test]
    public async Task Tiktoken_mode_builds_structural_section_chunk_and_topic_edges()
    {
        var result = await BuildAsync(StructuredMarkdown);

        var hasStructure = await result.Graph.ExecuteAskAsync(StructureAskQuery);
        hasStructure.ShouldBeTrue();

        var search = await result.Graph.SearchByTokenDistanceAsync(TelescopeQuery, QueryLimit);
        search.Single().Text.ShouldContain(TelescopeStem);
        result.Facts.Assertions.ShouldContain(assertion =>
            assertion.SubjectId == DocumentUri &&
            assertion.Predicate == "schema:hasPart");
    }

    [Test]
    public async Task Tiktoken_topic_nodes_keep_non_ascii_labels_distinct()
    {
        var result = await BuildAsync(MultilingualMarkdown);
        var topics = result.Facts.Entities
            .Where(entity => entity.Type == DefinedTermType)
            .ToArray();

        topics.ShouldContain(topic => topic.Label.Contains(UkrainianTelescopeStem, StringComparison.OrdinalIgnoreCase));
        topics.ShouldContain(topic => topic.Label.Contains(FrenchTelescopeStem, StringComparison.OrdinalIgnoreCase));
        topics.Select(static topic => topic.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(topics.Length);
    }

    [Test]
    public async Task Tiktoken_mode_builds_loose_section_node_for_headingless_markdown()
    {
        var result = await BuildAsync(LooseSourcePath, LooseMarkdown);

        result.Facts.Entities.ShouldContain(entity => entity.Label == LooseTitle);
        var hasLooseStructure = await result.Graph.ExecuteAskAsync(LooseStructureAskQuery);
        hasLooseStructure.ShouldBeTrue();

        var search = await result.Graph.SearchByTokenDistanceAsync(LooseQuery, QueryLimit);
        search.Single().Text.ShouldContain(FrostSensorStem);
    }

    [Test]
    public async Task Tiktoken_mode_keeps_explicit_entity_hints_as_graph_entities()
    {
        var result = await BuildAsync(EntityHintsSourcePath, EntityHintsMarkdown);

        result.Facts.Entities.ShouldContain(entity =>
            entity.Label == PortableAnalyzerLabel &&
            entity.SameAs.Contains(PortableAnalyzerSameAs));
        result.Facts.Entities.ShouldContain(entity =>
            entity.Label == UkrainianObservatoryLabel &&
            entity.Id!.StartsWith(TokenEntityHintIdPrefixText, StringComparison.Ordinal));

        var hasEntityHints = await result.Graph.ExecuteAskAsync(EntityHintAskQuery);
        hasEntityHints.ShouldBeTrue();
    }

    [Test]
    public async Task Tiktoken_mode_reads_camel_case_entity_hints()
    {
        var result = await BuildAsync(CamelEntityHintsSourcePath, CamelEntityHintsMarkdown);

        result.Facts.Entities.ShouldContain(entity =>
            entity.Label == CamelHintLabel &&
            entity.SameAs.Contains(CamelHintSameAs));

        var hasEntityHint = await result.Graph.ExecuteAskAsync(CamelEntityHintAskQuery);
        hasEntityHint.ShouldBeTrue();
    }

    [Test]
    public async Task Tiktoken_mode_reads_scalar_numeric_value_and_snake_case_entity_hints()
    {
        var result = await BuildAsync(MixedEntityHintsSourcePath, MixedEntityHintsMarkdown);

        var hintEntities = result.Facts.Entities
            .Where(static entity => entity.Type is SchemaThingType or ValueHintType)
            .ToArray();

        hintEntities.ShouldContain(entity => entity.Label == ScalarHintLabel && entity.Type == SchemaThingType);
        hintEntities.ShouldContain(entity => entity.Label == NumericHintLabel && entity.Type == SchemaThingType);
        hintEntities.ShouldContain(entity =>
            entity.Label == ValueHintLabel &&
            entity.Type == ValueHintType &&
            entity.SameAs.Contains(ValueHintSameAs));
        hintEntities.ShouldNotContain(entity => string.IsNullOrWhiteSpace(entity.Label));
        hintEntities.Length.ShouldBe(3);

        var hasEntityHints = await result.Graph.ExecuteAskAsync(MixedEntityHintsAskQuery);
        hasEntityHints.ShouldBeTrue();

        var search = await result.Graph.SearchByTokenDistanceAsync(MixedEntityHintsQuery, QueryLimit);
        search.Single().DocumentId.ShouldBe(MixedEntityHintsDocumentUri);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(string markdown)
    {
        return BuildAsync(SourcePath, markdown);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(string path, string markdown)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions
            {
                MaxRelatedSegments = 2,
            });

        return pipeline.BuildAsync([
            new MarkdownSourceDocument(path, markdown),
        ]);
    }
}
