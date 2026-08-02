using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Pipeline;

public sealed class TokenizedKnowledgeFactFactoryTests
{
    private const string TopicId = "https://facts.example/token-topic/repeated";
    private const string FirstDocumentId = "https://facts.example/first/";
    private const string SecondDocumentId = "https://facts.example/second/";
    private const string FirstSegmentId = "https://facts.example/token-segment/first";
    private const string SecondSegmentId = "https://facts.example/token-segment/second";
    private const string ThirdSegmentId = "https://facts.example/token-segment/third";
    private const string SchemaAbout = "https://schema.org/about";
    private const double HighestScore = 0.91;

    [Test]
    public void Repeated_topics_are_aggregated_before_graph_normalization()
    {
        var facts = TokenizedKnowledgeFactFactory.Build(
            [],
            [],
            [
                Topic(FirstDocumentId, FirstSegmentId, "Repeated topic", 0.61),
                Topic(FirstDocumentId, SecondSegmentId, "Repeated topic label", HighestScore),
                Topic(SecondDocumentId, ThirdSegmentId, "Repeated topic", 0.72),
            ],
            [],
            []);

        var topic = facts.Entities.Single();
        topic.Id.ShouldBe(TopicId);
        topic.Label.ShouldBe("Repeated topic label");
        topic.Confidence.ShouldBe(HighestScore);
        topic.Source.ShouldBe(FirstDocumentId);
        topic.Sources.ShouldBe([FirstDocumentId, SecondDocumentId]);

        var segmentAssertions = facts.Assertions
            .Where(static assertion => assertion.SubjectId.Contains("token-segment", StringComparison.Ordinal))
            .ToArray();
        segmentAssertions.Length.ShouldBe(3);

        var documentAssertions = facts.Assertions
            .Where(static assertion => !assertion.SubjectId.Contains("token-segment", StringComparison.Ordinal))
            .OrderBy(static assertion => assertion.SubjectId, StringComparer.Ordinal)
            .ToArray();
        documentAssertions.Length.ShouldBe(2);
        documentAssertions[0].SubjectId.ShouldBe(FirstDocumentId);
        documentAssertions[0].Predicate.ShouldBe(SchemaAbout);
        documentAssertions[0].ObjectId.ShouldBe(TopicId);
        documentAssertions[0].Confidence.ShouldBe(HighestScore);
        documentAssertions[0].Source.ShouldBe(FirstDocumentId);
        documentAssertions[0].Sources.ShouldBe([FirstDocumentId]);
        documentAssertions[1].SubjectId.ShouldBe(SecondDocumentId);
        documentAssertions[1].Confidence.ShouldBe(0.72);
        documentAssertions[1].Source.ShouldBe(SecondDocumentId);
        documentAssertions[1].Sources.ShouldBeEmpty();

        var normalized = new KnowledgeGraphNormalizer().Normalize(facts);
        normalized.Report.Warnings.ShouldBeEmpty();
    }

    [Test]
    public void Topic_ranking_scores_are_bounded_before_fact_materialization()
    {
        var facts = TokenizedKnowledgeFactFactory.Build(
            [],
            [],
            [Topic(FirstDocumentId, FirstSegmentId, "High ranking topic", 10.25)],
            [],
            []);

        facts.Entities.Single().Confidence.ShouldBe(1d);
        facts.Assertions.Select(static assertion => assertion.Confidence).ShouldAllBe(static confidence => confidence == 1d);

        var normalized = new KnowledgeGraphNormalizer().Normalize(facts);
        normalized.Report.Warnings.ShouldBeEmpty();
    }

    private static TokenizedKnowledgeTopic Topic(
        string documentId,
        string segmentId,
        string label,
        double score)
    {
        return new TokenizedKnowledgeTopic(TopicId, documentId, segmentId, label, score);
    }
}
