using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class DeterministicKnowledgeFactExtractor
{
    private static readonly Regex WikiLinkRegex = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[(?<label>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex ArrowRegex = new(@"^(?<subject>.+?)\s*--(?<predicate>[^-]+?)-->\s*(?<object>.+?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly Uri _baseUri;

    public DeterministicKnowledgeFactExtractor(Uri? baseUri = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri("https://example.com/", UriKind.Absolute));
    }

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
        foreach (var hint in ReadFrontMatterSequence(document.FrontMatter, "entity_hints"))
        {
            if (hint is not IDictionary<string, object?> hintMap)
            {
                continue;
            }

            var label = ReadString(hintMap, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, ReadString(hintMap, "type") ?? "schema:Thing");
            entity = entity with
            {
                SameAs = ReadStringSequence(hintMap, "sameAs").ToList(),
                Confidence = 0.95,
                Source = document.DocumentUri.AbsoluteUri,
            };

            UpsertEntity(entities, entity);
        }

        foreach (var author in ReadFrontMatterSequence(document.FrontMatter, "author"))
        {
            var (label, sameAs, type) = ReadNamedEntity(author, "schema:Person");
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
                Predicate = "schema:author",
                ObjectId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label),
                Confidence = 0.9,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }

        foreach (var about in ReadFrontMatterSequence(document.FrontMatter, "about"))
        {
            var label = ReadScalarLabel(about);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var entity = CreateEntityFact(label, "schema:Thing");
            entity = entity with
            {
                Confidence = 0.85,
                Source = document.DocumentUri.AbsoluteUri,
            };

            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = "schema:about",
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

            var entity = CreateEntityFact(label, "schema:Thing") with
            {
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            };
            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = "schema:mentions",
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
            var label = match.Groups["label"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var absoluteUrl))
            {
                continue;
            }

            var entity = CreateEntityFact(label, "schema:Thing") with
            {
                SameAs = [absoluteUrl.AbsoluteUri],
                Confidence = 0.8,
                Source = document.DocumentUri.AbsoluteUri,
            };
            UpsertEntity(entities, entity);
            UpsertAssertion(assertions, new KnowledgeAssertionFact
            {
                SubjectId = articleId,
                Predicate = "schema:mentions",
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
            var subjectText = match.Groups["subject"].Value.Trim();
            var predicateText = match.Groups["predicate"].Value.Trim();
            var objectText = match.Groups["object"].Value.Trim();

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

        var (label, sameAs, type) = ReadNamedEntity(trimmed, "schema:Thing");
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
        return !string.IsNullOrWhiteSpace(operand) && operand != "--" && operand != "-->";
    }

    private static bool IsArticleReference(MarkdownDocument document, string text)
    {
        if (text.Equals("article", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("this article", StringComparison.OrdinalIgnoreCase) ||
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
            Type = string.IsNullOrWhiteSpace(type) ? "schema:Thing" : type.Trim(),
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
        var key = $"{assertion.SubjectId}||{assertion.Predicate}||{assertion.ObjectId}";
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
            "schema:Person" => 5,
            "schema:Organization" => 5,
            "schema:SoftwareApplication" => 5,
            "schema:CreativeWork" => 4,
            "schema:Article" => 4,
            "schema:Thing" => 1,
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

        var label = ReadString(map, "label") ?? ReadString(map, "name") ?? string.Empty;
        var type = ReadString(map, "type") ?? defaultType;
        var sameAs = ReadStringSequence(map, "sameAs");
        return (label, sameAs, type);
    }

    private static string? ReadScalarLabel(object? node)
    {
        if (node is string text)
        {
            return text.Trim();
        }

        if (node is IDictionary<string, object?> map)
        {
            return ReadString(map, "label") ?? ReadString(map, "name");
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
