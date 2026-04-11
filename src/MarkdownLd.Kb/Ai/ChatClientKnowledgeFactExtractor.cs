using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.KnowledgeFactConstants;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class ChatClientKnowledgeFactExtractor
{
    private static readonly IReadOnlyDictionary<string, int> TypePriority = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [SchemaPerson] = 5,
        [SchemaOrganization] = 5,
        [SchemaSoftwareApplication] = 5,
        [SchemaCreativeWork] = 4,
        [SchemaArticle] = 4,
        [SchemaThing] = 1,
    };

    private readonly IChatClient _chatClient;
    private readonly string _graphBaseUrl;
    private readonly ChatOptions _chatOptions;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _systemPrompt;

    public ChatClientKnowledgeFactExtractor(
        IChatClient chatClient,
        string graphBaseUrl = DefaultGraphBaseUrl,
        ChatOptions? chatOptions = null,
        JsonSerializerOptions? serializerOptions = null,
        string? systemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _graphBaseUrl = NormalizeGraphBaseUrl(graphBaseUrl);
        _chatOptions = chatOptions?.Clone() ?? new ChatOptions();
        _serializerOptions = serializerOptions is null
            ? CreateSerializerOptions()
            : PrepareSerializerOptions(serializerOptions);
        _systemPrompt = systemPrompt ?? KnowledgeFactConstants.DefaultSystemPrompt;
    }

    public async Task<KnowledgeFactExtractionResult> ExtractAsync(
        KnowledgeFactExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DocumentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ChunkId);

        if (string.IsNullOrWhiteSpace(request.Markdown))
        {
            return new KnowledgeFactExtractionResult(
                request.DocumentId,
                request.ChunkId,
                [],
                [],
                EmptyString);
        }

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, _systemPrompt),
            new ChatMessage(ChatRole.User, BuildUserPrompt(request)),
        };

        var response = await _chatClient.GetResponseAsync<KnowledgeFactExtractionEnvelope>(
            messages,
            _serializerOptions,
            BuildChatOptions(),
            useJsonSchemaResponseFormat: true,
            cancellationToken).ConfigureAwait(false);

        if (!response.TryGetResult(out var envelope) || envelope is null)
        {
            return new KnowledgeFactExtractionResult(
                request.DocumentId,
                request.ChunkId,
                [],
                [],
                response.Text ?? EmptyString);
        }

        var entities = NormalizeEntities(envelope.Entities);
        var assertions = NormalizeAssertions(envelope.Assertions, request);

        return new KnowledgeFactExtractionResult(
            request.DocumentId,
            request.ChunkId,
            entities,
            assertions,
            response.Text ?? EmptyString);
    }

    private ChatOptions BuildChatOptions()
    {
        var options = _chatOptions.Clone();
        options.Temperature = 0;
        return options;
    }

    private static string BuildUserPrompt(KnowledgeFactExtractionRequest request)
    {
        var builder = new StringBuilder();
        builder.Append(ArticleIdLabel).AppendLine(request.DocumentId);
        builder.Append(ChunkIdLabel).AppendLine(request.ChunkId);
        builder.Append(ChunkSourceLabel).AppendLine(request.ChunkSourceUri);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            builder.Append(TitleLabel).AppendLine(request.Title);
        }

        if (!string.IsNullOrWhiteSpace(request.SectionPath))
        {
            builder.Append(SectionPathLabel).AppendLine(request.SectionPath);
        }

        if (request.FrontMatter is { Count: > 0 })
        {
            builder.AppendLine(FrontMatterLabel);
            foreach (var pair in request.FrontMatter.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder
                    .Append(FrontMatterItemPrefix)
                    .Append(pair.Key)
                    .Append(KeyValueSeparator)
                    .AppendLine(pair.Value ?? EmptyString);
            }
        }

        builder.AppendLine();
        builder.AppendLine(ExtractInstruction);
        builder.AppendLine(ExplicitFactsInstruction);
        builder.AppendLine(ArticleIdInstruction);
        builder.AppendLine();
        builder.AppendLine(MarkdownLabel);
        builder.AppendLine(request.Markdown);

        return builder.ToString();
    }

    private IReadOnlyList<KnowledgeFactEntity> NormalizeEntities(IReadOnlyList<KnowledgeFactEntity>? entities)
    {
        if (entities is null || entities.Count == 0)
        {
            return [];
        }

        var merged = new Dictionary<string, KnowledgeFactEntity>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            if (entity is null)
            {
                continue;
            }

            var label = entity.Label?.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var normalizedId = NormalizeEntityId(entity.Id, label);
            var normalizedType = NormalizeEntityType(entity.Type);
            var sameAs = NormalizeSameAs(entity.SameAs);
            var slug = Slugify(label);

            if (merged.TryGetValue(slug, out var existing))
            {
                merged[slug] = existing with
                {
                    Id = ChooseCanonicalId(existing.Id, normalizedId),
                    Type = ChooseRicherType(existing.Type, normalizedType),
                    SameAs = MergeSameAs(existing.SameAs, sameAs),
                };
            }
            else
            {
                merged[slug] = new KnowledgeFactEntity(
                    normalizedId,
                    normalizedType,
                    label,
                    sameAs);
            }
        }

        return merged.Values.OrderBy(entity => entity.Label, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<KnowledgeFactAssertion> NormalizeAssertions(
        IReadOnlyList<KnowledgeFactAssertion>? assertions,
        KnowledgeFactExtractionRequest request)
    {
        if (assertions is null || assertions.Count == 0)
        {
            return [];
        }

        var merged = new Dictionary<(string SubjectId, string Predicate, string ObjectId), KnowledgeFactAssertion>();

        foreach (var assertion in assertions)
        {
            if (assertion is null)
            {
                continue;
            }

            var subjectId = NormalizeSubjectId(assertion.SubjectId, request.DocumentId);
            var predicate = assertion.Predicate?.Trim();
            var objectId = assertion.ObjectId?.Trim();

            if (string.IsNullOrWhiteSpace(subjectId) ||
                string.IsNullOrWhiteSpace(predicate) ||
                string.IsNullOrWhiteSpace(objectId))
            {
                continue;
            }

            var normalized = new KnowledgeFactAssertion(
                subjectId,
                predicate,
                objectId,
                NormalizeConfidence(assertion.Confidence),
                string.IsNullOrWhiteSpace(assertion.Source) ? request.ChunkSourceUri : assertion.Source.Trim());

            var key = (normalized.SubjectId, normalized.Predicate, normalized.ObjectId);
            if (merged.TryGetValue(key, out var existing))
            {
                merged[key] = existing.Confidence >= normalized.Confidence ? existing : normalized;
            }
            else
            {
                merged[key] = normalized;
            }
        }

        return merged.Values.ToArray();
    }

    private string NormalizeEntityId(string? id, string label)
    {
        var trimmed = id?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && !IsPlaceholder(trimmed))
        {
            return trimmed;
        }

        return string.Concat(_graphBaseUrl, EntityPathSegment, Slugify(label));
    }

    private static string NormalizeEntityType(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? SchemaThing : type.Trim();
    }

    private static IReadOnlyList<string> NormalizeSameAs(IReadOnlyList<string>? sameAs)
    {
        if (sameAs is null || sameAs.Count == 0)
        {
            return [];
        }

        return sameAs
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray()!;
    }

    private static IReadOnlyList<string> MergeSameAs(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        return (left ?? [])
            .Concat(right ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ChooseCanonicalId(string left, string right)
    {
        return IsPlaceholder(left) || string.IsNullOrWhiteSpace(left) ? right : left;
    }

    private static string ChooseRicherType(string left, string right)
    {
        var leftPriority = TypePriority.TryGetValue(left, out var l) ? l : 0;
        var rightPriority = TypePriority.TryGetValue(right, out var r) ? r : 0;
        return rightPriority > leftPriority ? right : left;
    }

    private static string NormalizeSubjectId(string? subjectId, string documentId)
    {
        var trimmed = subjectId?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || IsPlaceholder(trimmed))
        {
            return documentId;
        }

        return trimmed;
    }

    private static double NormalizeConfidence(double confidence)
    {
        if (double.IsNaN(confidence) || double.IsInfinity(confidence))
        {
            return 0.5;
        }

        return Math.Clamp(confidence, 0, 1);
    }

    private static bool IsPlaceholder(string value)
    {
        return value is ArticleIdPlaceholder or ArticlePlaceholder or ArticleIdToken or ArticleToken or BracedArticleIdToken;
    }

    private static string NormalizeGraphBaseUrl(string graphBaseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphBaseUrl);
        return graphBaseUrl.TrimEnd('/');
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
    }

    private static JsonSerializerOptions PrepareSerializerOptions(JsonSerializerOptions serializerOptions)
    {
        var options = new JsonSerializerOptions(serializerOptions)
        {
            TypeInfoResolver = serializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver(),
        };

        return options;
    }

    private static string Slugify(string label)
    {
        var value = label.ToLowerInvariant().Trim();
        var builder = new StringBuilder(value.Length);
        var pendingDash = false;

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(ch);
                pendingDash = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                pendingDash = builder.Length > 0;
            }
        }

        return builder.ToString().Trim('-');
    }

    private sealed record KnowledgeFactExtractionEnvelope(
        [property: JsonPropertyName(EntitiesJsonName)] IReadOnlyList<KnowledgeFactEntity>? Entities,
        [property: JsonPropertyName(AssertionsJsonName)] IReadOnlyList<KnowledgeFactAssertion>? Assertions);
}
