using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class TokenizedKnowledgeFactFactory
{
    public static KnowledgeExtractionResult Build(
        IReadOnlyList<TokenizedKnowledgeSection> sections,
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<TokenizedKnowledgeTopic> topics,
        IReadOnlyList<TokenizedKnowledgeEntityHint> entityHints,
        IReadOnlyList<TokenizedKnowledgeRelation> relations)
    {
        return new KnowledgeExtractionResult
        {
            Entities = entityHints.Select(CreateEntityHintEntity)
                .Concat(sections.Select(CreateSectionEntity))
                .Concat(segments.Select(CreateSegmentEntity))
                .Concat(topics.Select(CreateTopicEntity))
                .ToList(),
            Assertions = CreateEntityHintAssertions(entityHints)
                .Concat(CreateDocumentSectionAssertions(sections))
                .Concat(CreateSegmentParentAssertions(segments))
                .Concat(CreateDocumentSegmentAssertions(segments))
                .Concat(CreateTopicAssertions(topics))
                .Concat(relations.Select(CreateRelationAssertion))
                .ToList(),
        };
    }

    private static KnowledgeEntityFact CreateEntityHintEntity(TokenizedKnowledgeEntityHint hint)
    {
        return new KnowledgeEntityFact
        {
            Id = hint.Id,
            Label = hint.Label,
            Type = hint.Type,
            SameAs = hint.SameAs.ToList(),
            Source = hint.DocumentId,
        };
    }

    private static KnowledgeEntityFact CreateSectionEntity(TokenizedKnowledgeSection section)
    {
        return new KnowledgeEntityFact
        {
            Id = section.Id,
            Label = section.Label,
            Type = TokenSegmentTypeText,
            Source = section.DocumentId,
        };
    }

    private static KnowledgeEntityFact CreateSegmentEntity(TokenizedKnowledgeSegment segment)
    {
        return new KnowledgeEntityFact
        {
            Id = segment.Id,
            Label = segment.Text,
            Type = TokenSegmentTypeText,
            Source = segment.DocumentId,
        };
    }

    private static KnowledgeEntityFact CreateTopicEntity(TokenizedKnowledgeTopic topic)
    {
        return new KnowledgeEntityFact
        {
            Id = topic.Id,
            Label = topic.Label,
            Type = TokenTopicTypeText,
            Confidence = topic.Score,
            Source = topic.DocumentId,
        };
    }

    private static IEnumerable<KnowledgeAssertionFact> CreateEntityHintAssertions(
        IEnumerable<TokenizedKnowledgeEntityHint> hints)
    {
        foreach (var hint in hints)
        {
            yield return new KnowledgeAssertionFact
            {
                SubjectId = hint.DocumentId,
                Predicate = SchemaMentionsText,
                ObjectId = hint.Id,
                Confidence = FullConfidence,
                Source = hint.DocumentId,
            };
        }
    }

    private static IEnumerable<KnowledgeAssertionFact> CreateDocumentSectionAssertions(
        IEnumerable<TokenizedKnowledgeSection> sections)
    {
        foreach (var section in sections)
        {
            yield return new KnowledgeAssertionFact
            {
                SubjectId = section.DocumentId,
                Predicate = SchemaHasPartText,
                ObjectId = section.Id,
                Source = section.DocumentId,
            };
        }
    }

    private static IEnumerable<KnowledgeAssertionFact> CreateSegmentParentAssertions(
        IEnumerable<TokenizedKnowledgeSegment> segments)
    {
        foreach (var segment in segments)
        {
            yield return new KnowledgeAssertionFact
            {
                SubjectId = segment.ParentId,
                Predicate = SchemaHasPartText,
                ObjectId = segment.Id,
                Source = segment.DocumentId,
            };
        }
    }

    private static IEnumerable<KnowledgeAssertionFact> CreateDocumentSegmentAssertions(
        IEnumerable<TokenizedKnowledgeSegment> segments)
    {
        foreach (var segment in segments)
        {
            yield return new KnowledgeAssertionFact
            {
                SubjectId = segment.DocumentId,
                Predicate = SchemaMentionsText,
                ObjectId = segment.Id,
                Source = segment.DocumentId,
            };
        }
    }

    private static IEnumerable<KnowledgeAssertionFact> CreateTopicAssertions(IEnumerable<TokenizedKnowledgeTopic> topics)
    {
        foreach (var topic in topics)
        {
            yield return new KnowledgeAssertionFact
            {
                SubjectId = topic.SegmentId,
                Predicate = SchemaAboutText,
                ObjectId = topic.Id,
                Confidence = topic.Score,
                Source = topic.DocumentId,
            };

            yield return new KnowledgeAssertionFact
            {
                SubjectId = topic.DocumentId,
                Predicate = SchemaAboutText,
                ObjectId = topic.Id,
                Confidence = topic.Score,
                Source = topic.DocumentId,
            };
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
