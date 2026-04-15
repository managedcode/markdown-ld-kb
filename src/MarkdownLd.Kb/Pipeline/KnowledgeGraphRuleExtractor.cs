using System.Diagnostics.CodeAnalysis;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeGraphRuleExtractor(Uri baseUri)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);

    public KnowledgeExtractionResult Extract(
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeGraphBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);

        var result = new KnowledgeExtractionResult();
        AddConfiguredRules(result, options);
        if (!options.IncludeFrontMatterRules)
        {
            return result;
        }

        foreach (var document in documents)
        {
            AddFrontMatterRules(result, document);
        }

        return result;
    }

    private static void AddConfiguredRules(
        KnowledgeExtractionResult result,
        KnowledgeGraphBuildOptions options)
    {
        foreach (var entity in options.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Label))
            {
                continue;
            }

            result.Entities.Add(new KnowledgeEntityFact
            {
                Id = entity.Id,
                Label = entity.Label,
                Type = entity.Type,
                SameAs = entity.SameAs.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
                Confidence = entity.Confidence,
                Source = entity.Source,
            });
        }

        foreach (var edge in options.Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.SubjectId) ||
                string.IsNullOrWhiteSpace(edge.ObjectId) ||
                string.IsNullOrWhiteSpace(edge.Predicate))
            {
                continue;
            }

            result.Assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = edge.SubjectId,
                Predicate = edge.Predicate,
                ObjectId = edge.ObjectId,
                Confidence = edge.Confidence,
                Source = edge.Source,
            });
        }
    }

    private void AddFrontMatterRules(KnowledgeExtractionResult result, MarkdownDocument document)
    {
        AddGraphEntities(result, document);
        AddGroupRules(result, document);
        AddTargetEdges(result, document, KbRelatedTo, GraphRelatedKey, GraphRelatedCamelKey);
        AddTargetEdges(result, document, KbNextStep, GraphNextStepsKey, GraphNextStepsCamelKey);
        AddGraphEdges(result, document);
    }

    private void AddGraphEntities(KnowledgeExtractionResult result, MarkdownDocument document)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphEntitiesKey, GraphEntitiesCamelKey))
        {
            if (!TryReadNodeReference(item, document, out var node))
            {
                continue;
            }

            result.Entities.Add(new KnowledgeEntityFact
            {
                Id = node.Id,
                Label = node.Label,
                Type = node.Type ?? DefaultSchemaThing,
                SameAs = node.SameAs,
                Confidence = node.Confidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private void AddGroupRules(KnowledgeExtractionResult result, MarkdownDocument document)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphGroupsKey, GraphGroupsCamelKey))
        {
            if (!TryReadNodeReference(item, document, out var group))
            {
                continue;
            }

            result.Entities.Add(new KnowledgeEntityFact
            {
                Id = group.Id,
                Label = group.Label,
                Type = group.Type ?? SchemaDefinedTermTypeText,
                SameAs = group.SameAs,
                Confidence = group.Confidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
            result.Assertions.Add(CreateDocumentEdge(document, KbMemberOf, group.Id));
        }
    }

    private void AddTargetEdges(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        string predicate,
        string snakeKey,
        string camelKey)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, snakeKey, camelKey))
        {
            if (!TryReadNodeReference(item, document, out var target))
            {
                continue;
            }

            result.Assertions.Add(CreateDocumentEdge(document, predicate, target.Id));
        }
    }

    private void AddGraphEdges(KnowledgeExtractionResult result, MarkdownDocument document)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphEdgesKey, GraphEdgesCamelKey))
        {
            if (item is not IReadOnlyDictionary<string, object?> map ||
                !TryReadString(map, PredicateKey, out var predicate))
            {
                continue;
            }

            var subject = ReadMapNodeId(map, document, SubjectIdKey, SubjectIdSnakeKey, SubjectKey)
                ?? document.DocumentUri.AbsoluteUri;
            var graphObject = ReadMapNodeId(map, document, ObjectIdKey, ObjectIdSnakeKey, ObjectKey, TargetIdKey, TargetIdSnakeKey, TargetKey);
            if (string.IsNullOrWhiteSpace(graphObject))
            {
                continue;
            }

            result.Assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = subject,
                Predicate = predicate,
                ObjectId = graphObject,
                Confidence = FullConfidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private static KnowledgeAssertionFact CreateDocumentEdge(
        MarkdownDocument document,
        string predicate,
        string objectId)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = document.DocumentUri.AbsoluteUri,
            Predicate = predicate,
            ObjectId = objectId,
            Confidence = FullConfidence,
            Source = document.DocumentUri.AbsoluteUri,
        };
    }

    private string? ReadMapNodeId(
        IReadOnlyDictionary<string, object?> map,
        MarkdownDocument document,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryReadString(map, key, out var value))
            {
                return ResolveNodeId(document, value);
            }
        }

        return null;
    }

    private bool TryReadNodeReference(
        object? item,
        MarkdownDocument document,
        [NotNullWhen(true)] out GraphNodeReference? node)
    {
        if (item is IReadOnlyDictionary<string, object?> map)
        {
            var label = ReadFirstString(map, LabelKey, NameKey, ValueKey, TargetKey, ObjectKey);
            var idText = ReadFirstString(map, IdKey, TargetIdKey, TargetIdSnakeKey, ObjectIdKey, ObjectIdSnakeKey);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = idText;
            }

            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(idText))
            {
                node = null;
                return false;
            }

            node = new GraphNodeReference(
                string.IsNullOrWhiteSpace(idText)
                    ? ResolveNodeId(document, label!)
                    : ResolveNodeId(document, idText),
                label ?? idText!,
                ReadFirstString(map, TypeKey),
                ReadStringList(map, SameAsKey, SameAsSnakeKey).ToList(),
                FullConfidence);
            return true;
        }

        var text = item?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            node = null;
            return false;
        }

        node = new GraphNodeReference(
            ResolveNodeId(document, text),
            text,
            null,
            [],
            FullConfidence);
        return true;
    }

    private string ResolveNodeId(MarkdownDocument document, string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) ||
            text.Equals(ArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(ThisArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(DefaultDocument, StringComparison.OrdinalIgnoreCase))
        {
            return document.DocumentUri.AbsoluteUri;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsoluteUri;
        }

        if (text.StartsWith(UriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return KnowledgeNaming.CreateEntityId(_baseUri, text);
    }

    private static IEnumerable<object?> ReadFrontMatterItems(
        IReadOnlyDictionary<string, object?> frontMatter,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!frontMatter.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw is IEnumerable<object?> list && raw is not string)
            {
                foreach (var item in list)
                {
                    yield return item;
                }
            }
            else
            {
                yield return raw;
            }
        }
    }

    private static string? ReadFirstString(IReadOnlyDictionary<string, object?> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryReadString(map, key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> ReadStringList(
        IReadOnlyDictionary<string, object?> map,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw is IEnumerable<object?> list && raw is not string)
            {
                foreach (var item in list)
                {
                    var text = item?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return text;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(raw?.ToString()))
            {
                yield return raw.ToString()!.Trim();
            }
        }
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> map,
        string key,
        [NotNullWhen(true)] out string? value)
    {
        if (map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw?.ToString()))
        {
            value = raw.ToString()!.Trim();
            return true;
        }

        value = null;
        return false;
    }

    private sealed record GraphNodeReference(
        string Id,
        string Label,
        string? Type,
        List<string> SameAs,
        double Confidence);
}
