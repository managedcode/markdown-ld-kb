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

        AddTopicEntities(entities, topics);

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
            Confidence = topic.Confidence,
            Source = topic.DocumentId,
        };
    }

    private static void AddTopicEntities(
        List<KnowledgeEntityFact> entities,
        IReadOnlyList<TokenizedKnowledgeTopic> topics)
    {
        var indexes = new Dictionary<string, int>(topics.Count, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>>? sourcesByTopic = null;
        foreach (var topic in topics)
        {
            var candidate = CreateTopicEntity(topic);
            if (!indexes.TryGetValue(topic.Id, out var index))
            {
                indexes.Add(topic.Id, entities.Count);
                entities.Add(candidate);
                continue;
            }

            var existing = entities[index];
            sourcesByTopic ??= new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            if (!sourcesByTopic.TryGetValue(topic.Id, out var sources))
            {
                sources = new HashSet<string>(StringComparer.Ordinal) { existing.Source };
                sourcesByTopic.Add(topic.Id, sources);
            }

            sources.Add(candidate.Source);
            entities[index] = existing with
            {
                Label = existing.Label.Length >= candidate.Label.Length ? existing.Label : candidate.Label,
                Confidence = Math.Max(existing.Confidence, candidate.Confidence),
                Source = string.IsNullOrWhiteSpace(existing.Source) ? candidate.Source : existing.Source,
            };
        }

        if (sourcesByTopic is null)
        {
            return;
        }

        foreach (var pair in sourcesByTopic)
        {
            var index = indexes[pair.Key];
            entities[index] = entities[index] with
            {
                Sources = pair.Value
                    .Where(static source => !string.IsNullOrWhiteSpace(source))
                    .Order(StringComparer.Ordinal)
                    .ToList(),
            };
        }
    }
}
