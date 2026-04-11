using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class DeterministicKnowledgeFactExtractor(Uri? baseUri = null)
{
    private static readonly Regex WikiLinkRegex = new(WikiLinkPattern, RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(MarkdownLinkPattern, RegexOptions.Compiled);
    private static readonly Regex ArrowRegex = new(ArrowPattern, RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeExtractionResult Extract(MarkdownDocument document)
    {
        var result = new KnowledgeExtractionResult();
        var entityMap = new Dictionary<string, KnowledgeEntityFact>(StringComparer.OrdinalIgnoreCase);
        var assertionMap = new Dictionary<string, KnowledgeAssertionFact>(StringComparer.OrdinalIgnoreCase);
        var articleId = document.DocumentUri.AbsoluteUri;

        AddFrontMatterEntities(document, entityMap, assertionMap, articleId);
        AddWikilinks(document, entityMap, assertionMap, articleId);
        AddMarkdownLinks(document, entityMap, assertionMap, articleId);
        AddArrowAssertions(document, entityMap, assertionMap, articleId);

        result.Entities.AddRange(entityMap.Values.OrderBy(entity => entity.Label, StringComparer.OrdinalIgnoreCase));
        result.Assertions.AddRange(assertionMap.Values.OrderBy(assertion => assertion.SubjectId, StringComparer.OrdinalIgnoreCase));
        return result;
    }

    private void AddFrontMatterEntities(
        MarkdownDocument document,
        IDictionary<string, KnowledgeEntityFact> entities,
        IDictionary<string, KnowledgeAssertionFact> assertions,
        string articleId)
    {
        foreach (var hint in ReadFrontMatterSequence(document.FrontMatter, EntityHintsKey))
        {
            var (label, sameAs, type) = ReadNamedEntity(hint, DefaultSchemaThing);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, type);
            entity = entity with
            {
                SameAs = sameAs.ToList(),
                Confidence = 0.95,
                Source = document.DocumentUri.AbsoluteUri,
            };

            UpsertEntity(entities, entity);
        }

        foreach (var author in ReadFrontMatterSequence(document.FrontMatter, AuthorKey))
        {
            var (label, sameAs, type) = ReadNamedEntity(author, SchemaPersonTypeText);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, type);
            entity = entity with
            {
                SameAs = sameAs.ToList(),
                Confidence = 0.9,
                Source = document.DocumentUri.AbsoluteUri,
            };

            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = SchemaAuthorText,
                ObjectId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label),
                Confidence = 0.9,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }

        foreach (var about in ReadFrontMatterSequence(document.FrontMatter, AboutKey))
        {
            var label = ReadScalarLabel(about);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, DefaultSchemaThing);
            entity = entity with
            {
                Confidence = 0.85,
                Source = document.DocumentUri.AbsoluteUri,
            };

            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = SchemaAboutText,
                ObjectId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label),
                Confidence = 0.85,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private void AddWikilinks(
        MarkdownDocument document,
        IDictionary<string, KnowledgeEntityFact> entities,
        IDictionary<string, KnowledgeAssertionFact> assertions,
        string articleId)
    {
        foreach (Match match in WikiLinkRegex.Matches(document.Body))
        {
            var label = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, DefaultSchemaThing) with
            {
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            };
            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = SchemaMentionsText,
                ObjectId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label),
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private void AddMarkdownLinks(
        MarkdownDocument document,
        IDictionary<string, KnowledgeEntityFact> entities,
        IDictionary<string, KnowledgeAssertionFact> assertions,
        string articleId)
    {
        foreach (Match match in MarkdownLinkRegex.Matches(document.Body))
        {
            var label = match.Groups[MatchLabelGroup].Value.Trim();
            var url = match.Groups[MatchUrlGroup].Value.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var absoluteUrl))
            {
                continue;
            }

            var entity = CreateEntityFact(label, DefaultSchemaThing) with
            {
                SameAs = [absoluteUrl.AbsoluteUri],
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            };
            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = SchemaMentionsText,
                ObjectId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label),
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private void AddArrowAssertions(
        MarkdownDocument document,
        IDictionary<string, KnowledgeEntityFact> entities,
        IDictionary<string, KnowledgeAssertionFact> assertions,
        string articleId)
    {
        foreach (Match match in ArrowRegex.Matches(document.Body))
        {
            var subjectText = NormalizeArrowOperand(match.Groups[MatchSubjectGroup].Value);
            var predicateText = match.Groups[MatchPredicateGroup].Value.Trim();
            var objectText = NormalizeArrowOperand(match.Groups[MatchObjectGroup].Value);

            if (!TryResolveNodeReference(document, subjectText, articleId, entities, out var subjectId) ||
                !TryResolveNodeReference(document, objectText, articleId, entities, out var objectId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(predicateText))
            {
                continue;
            }

            if (!IsValidArrowOperand(subjectText) || !IsValidArrowOperand(objectText))
            {
                continue;
            }

            var predicate = KnowledgeNaming.NormalizePredicate(predicateText);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = subjectId,
                Predicate = predicate,
                ObjectId = objectId,
                Confidence = 0.9,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private static string NormalizeArrowOperand(string operand)
    {
        var normalized = operand.Trim().Trim(ArrowOperandTrimChars);
        if (normalized.StartsWith(ListItemPrefix, StringComparison.Ordinal))
        {
            normalized = normalized[ListItemPrefix.Length..].Trim();
        }

        return normalized.Trim(ArrowOperandTrimChars);
    }

    private bool TryResolveNodeReference(
        MarkdownDocument document,
        string raw,
        string articleId,
        IDictionary<string, KnowledgeEntityFact> entities,
        out string id)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            id = string.Empty;
            return false;
        }

        if (IsArticleReference(document, trimmed))
        {
            id = articleId;
            return true;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUrl))
        {
            id = absoluteUrl.AbsoluteUri;
            return true;
        }

        var (label, sameAs, type) = ReadNamedEntity(trimmed, DefaultSchemaThing);
        var entity = CreateEntityFact(label, type) with
        {
            SameAs = sameAs.ToList(),
            Confidence = 0.75,
            Source = document.DocumentUri.AbsoluteUri,
        };
        UpsertEntity(entities, entity);
        id = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label);
        return true;
    }

    private static bool IsValidArrowOperand(string operand)
    {
        return !string.IsNullOrWhiteSpace(operand) && operand != ArrowSeparator && operand != ArrowTail;
    }

    private static bool IsArticleReference(MarkdownDocument document, string text)
    {
        if (text.Equals(ArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(ThisArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(document.Title, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private KnowledgeEntityFact CreateEntityFact(string label, string type)
    {
        var trimmedLabel = label.Trim();
        return new KnowledgeEntityFact
        {
            Id = KnowledgeNaming.CreateEntityId(_baseUri, trimmedLabel),
            Label = trimmedLabel,
            Type = string.IsNullOrWhiteSpace(type) ? DefaultSchemaThing : type.Trim(),
        };
    }

    private static void UpsertEntity(IDictionary<string, KnowledgeEntityFact> entities, KnowledgeEntityFact entity)
    {
        var key = entity.Id ?? entity.Label;
        if (!entities.TryGetValue(key, out var existing))
        {
            entities[key] = entity;
            return;
        }

        entities[key] = MergeEntity(existing, entity);
    }

    private static KnowledgeEntityFact MergeEntity(KnowledgeEntityFact left, KnowledgeEntityFact right)
    {
        var priorityLeft = EntityTypePriority(left.Type);
        var priorityRight = EntityTypePriority(right.Type);
        return left with
        {
            Label = left.Label.Length >= right.Label.Length ? left.Label : right.Label,
            Type = priorityRight > priorityLeft ? right.Type : left.Type,
            SameAs = left.SameAs.Concat(right.SameAs).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Confidence = Math.Max(left.Confidence, right.Confidence),
            Source = string.IsNullOrWhiteSpace(left.Source) ? right.Source : left.Source,
        };
    }

    private static void UpsertAssertion(IDictionary<string, KnowledgeAssertionFact> assertions, KnowledgeAssertionFact assertion)
    {
        var key = assertion.SubjectId + AssertionKeySeparator + assertion.Predicate + AssertionKeySeparator + assertion.ObjectId;
        if (!assertions.TryGetValue(key, out var existing))
        {
            assertions[key] = assertion;
            return;
        }

        assertions[key] = existing with
        {
            Confidence = Math.Max(existing.Confidence, assertion.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? assertion.Source : existing.Source,
        };
    }

    private static int EntityTypePriority(string type)
    {
        return type switch
        {
            SchemaPersonTypeText => 5,
            SchemaOrganizationTypeText => 5,
            SchemaSoftwareApplicationTypeText => 5,
            SchemaCreativeWorkTypeText => 4,
            SchemaArticleTypeText => 4,
            SchemaThingTypeText => 1,
            _ => 0,
        };
    }

    private static IEnumerable<object?> ReadFrontMatterSequence(IReadOnlyDictionary<string, object?> frontMatter, string key)
    {
        if (!TryGetValue(frontMatter, key, out var value))
        {
            return [];
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            return sequence;
        }

        return [value];
    }

    private static (string Label, IEnumerable<string> SameAs, string Type) ReadNamedEntity(object? node, string defaultType)
    {
        if (node is string text)
        {
            return (text.Trim(), [], defaultType);
        }

        if (node is not IDictionary<string, object?> map)
        {
            return (string.Empty, [], defaultType);
        }

        var label = ReadString(map, LabelKey) ?? ReadString(map, NameKey) ?? string.Empty;
        var type = NormalizeEntityTypeText(ReadString(map, TypeKey), defaultType);
        var sameAs = ReadStringSequence(map, SameAsKey);
        return (label, sameAs, type);
    }

    private static string NormalizeEntityTypeText(string? type, string defaultType)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return defaultType;
        }

        var trimmed = type.Trim();
        return trimmed.Contains(Colon, StringComparison.Ordinal)
            ? trimmed
            : string.Concat(SchemaPrefix, Colon, trimmed);
    }

    private static string? ReadScalarLabel(object? node)
    {
        if (node is string text)
        {
            return text.Trim();
        }

        if (node is IDictionary<string, object?> map)
        {
            return ReadString(map, LabelKey) ?? ReadString(map, NameKey);
        }

        return null;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> frontMatter, string key, out object? value)
    {
        foreach (var entry in frontMatter)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetValue(IDictionary<string, object?> frontMatter, string key, out object? value)
    {
        foreach (var entry in frontMatter)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string? ReadString(IDictionary<string, object?> map, string key)
    {
        return TryGetValue(map, key, out var value) ? value?.ToString() : null;
    }

    private static IEnumerable<string> ReadStringSequence(IDictionary<string, object?> map, string key)
    {
        if (!TryGetValue(map, key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            return sequence.Select(item => item?.ToString()?.Trim()).Where(item => !string.IsNullOrWhiteSpace(item))!;
        }

        var scalar = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(scalar) ? [] : [scalar!];
    }
}
