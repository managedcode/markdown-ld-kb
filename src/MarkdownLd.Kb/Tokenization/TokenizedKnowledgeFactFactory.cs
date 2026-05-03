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
        var entities = new List<KnowledgeEntityFact>(
            entityHints.Count + sections.Count + segments.Count + topics.Count);
        foreach (var entityHint in entityHints)
        {
            entities.Add(CreateEntityHintEntity(entityHint));
        }

        foreach (var section in sections)
        {
            entities.Add(CreateSectionEntity(section));
        }

        foreach (var segment in segments)
        {
            entities.Add(CreateSegmentEntity(segment));
        }

        foreach (var topic in topics)
        {
            entities.Add(CreateTopicEntity(topic));
        }

        return new KnowledgeExtractionResult
        {
            Entities = entities,
            Assertions = TokenizedKnowledgeAssertionBuilder.Build(
                sections,
                segments,
                topics,
                entityHints,
                relations),
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

}
