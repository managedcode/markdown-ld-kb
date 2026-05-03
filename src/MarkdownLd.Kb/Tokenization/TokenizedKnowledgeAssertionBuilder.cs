using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class TokenizedKnowledgeAssertionBuilder
{
    public static List<KnowledgeAssertionFact> Build(
        IReadOnlyList<TokenizedKnowledgeSection> sections,
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<TokenizedKnowledgeTopic> topics,
        IReadOnlyList<TokenizedKnowledgeEntityHint> entityHints,
        IReadOnlyList<TokenizedKnowledgeRelation> relations)
    {
        var assertions = new List<KnowledgeAssertionFact>(
            entityHints.Count + sections.Count + (segments.Count * 2) + (topics.Count * 2) + relations.Count);
        AddEntityHintAssertions(assertions, entityHints);
        AddDocumentSectionAssertions(assertions, sections);
        AddSegmentParentAssertions(assertions, segments);
        AddDocumentSegmentAssertions(assertions, segments);
        AddTopicAssertions(assertions, topics);
        foreach (var relation in relations)
        {
            assertions.Add(CreateRelationAssertion(relation));
        }

        return assertions;
    }

    private static void AddEntityHintAssertions(
        ICollection<KnowledgeAssertionFact> assertions,
        IReadOnlyList<TokenizedKnowledgeEntityHint> hints)
    {
        foreach (var hint in hints)
        {
            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = hint.DocumentId,
                Predicate = SchemaMentionsText,
                ObjectId = hint.Id,
                Confidence = FullConfidence,
                Source = hint.DocumentId,
            });
        }
    }

    private static void AddDocumentSectionAssertions(
        ICollection<KnowledgeAssertionFact> assertions,
        IReadOnlyList<TokenizedKnowledgeSection> sections)
    {
        foreach (var section in sections)
        {
            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = section.DocumentId,
                Predicate = SchemaHasPartText,
                ObjectId = section.Id,
                Source = section.DocumentId,
            });
        }
    }

    private static void AddSegmentParentAssertions(
        ICollection<KnowledgeAssertionFact> assertions,
        IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        foreach (var segment in segments)
        {
            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = segment.ParentId,
                Predicate = SchemaHasPartText,
                ObjectId = segment.Id,
                Source = segment.DocumentId,
            });
        }
    }

    private static void AddDocumentSegmentAssertions(
        ICollection<KnowledgeAssertionFact> assertions,
        IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        foreach (var segment in segments)
        {
            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = segment.DocumentId,
                Predicate = SchemaMentionsText,
                ObjectId = segment.Id,
                Source = segment.DocumentId,
            });
        }
    }

    private static void AddTopicAssertions(
        ICollection<KnowledgeAssertionFact> assertions,
        IReadOnlyList<TokenizedKnowledgeTopic> topics)
    {
        foreach (var topic in topics)
        {
            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = topic.SegmentId,
                Predicate = SchemaAboutText,
                ObjectId = topic.Id,
                Confidence = topic.Score,
                Source = topic.DocumentId,
            });

            assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = topic.DocumentId,
                Predicate = SchemaAboutText,
                ObjectId = topic.Id,
                Confidence = topic.Score,
                Source = topic.DocumentId,
            });
        }
    }

    private static KnowledgeAssertionFact CreateRelationAssertion(TokenizedKnowledgeRelation relation)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = relation.SubjectId,
            Predicate = KbRelatedTo,
            ObjectId = relation.ObjectId,
            Confidence = Math.Max(ZeroConfidence, FullConfidence - (relation.Distance / MaximumNormalizedTokenDistance)),
            Source = relation.SubjectId,
        };
    }
}
