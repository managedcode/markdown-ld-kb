using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using RootChatClientKnowledgeFactExtractor = ManagedCode.MarkdownLd.Kb.ChatClientKnowledgeFactExtractor;
using RootKnowledgeFactConstants = ManagedCode.MarkdownLd.Kb.KnowledgeFactConstants;
using RootKnowledgeFactExtractionRequest = ManagedCode.MarkdownLd.Kb.KnowledgeFactExtractionRequest;
using RootKnowledgeFactExtractionResult = ManagedCode.MarkdownLd.Kb.KnowledgeFactExtractionResult;
using RootMarkdownChunk = ManagedCode.MarkdownLd.Kb.MarkdownChunk;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class ChatClientKnowledgeFactExtractor
{
    private readonly RootChatClientKnowledgeFactExtractor _extractor;
    private readonly string _modelId;

    public ChatClientKnowledgeFactExtractor(
        IChatClient chatClient,
        Uri baseUri,
        ChatOptions? chatOptions = null,
        string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(baseUri);

        _extractor = new RootChatClientKnowledgeFactExtractor(chatClient, baseUri.AbsoluteUri, chatOptions);
        _modelId = string.IsNullOrWhiteSpace(modelId) ? UnknownChatModelId : modelId.Trim();
    }

    public async Task<KnowledgeExtractionResult> ExtractAsync(
        MarkdownDocument document,
        string chunkerProfileId,
        IKnowledgeExtractionCache? cache = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(chunkerProfileId);

        if (document.Chunks.Count == 0)
        {
            return new KnowledgeExtractionResult();
        }

        var cacheKey = CreateCacheKey(document, chunkerProfileId);
        IReadOnlyList<RootKnowledgeFactExtractionResult> chunkResults;

        if (cache is not null)
        {
            var cached = await cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                chunkResults = cached.ChunkResults;
                return Convert(chunkResults);
            }
        }

        chunkResults = await ExtractChunksAsync(document, cancellationToken).ConfigureAwait(false);

        if (cache is not null)
        {
            var entry = new KnowledgeExtractionCacheEntry(cacheKey, chunkResults, DateTimeOffset.UtcNow);
            await cache.SetAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return Convert(chunkResults);
    }

    private async Task<IReadOnlyList<RootKnowledgeFactExtractionResult>> ExtractChunksAsync(
        MarkdownDocument document,
        CancellationToken cancellationToken)
    {
        var results = new List<RootKnowledgeFactExtractionResult>(document.Chunks.Count);

        foreach (var chunk in document.Chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await _extractor.ExtractAsync(BuildRequest(document, chunk), cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private KnowledgeExtractionCacheKey CreateCacheKey(MarkdownDocument document, string chunkerProfileId)
    {
        var chunkFingerprints = document.Chunks
            .Select(chunk => new KnowledgeExtractionChunkFingerprint(
                chunk.ChunkId,
                RootMarkdownDocumentParser.ComputeChunkId(chunk.Markdown),
                chunk.Order))
            .ToArray();

        return new KnowledgeExtractionCacheKey(
            document.DocumentUri.AbsoluteUri,
            document.SourcePath,
            chunkerProfileId,
            RootKnowledgeFactConstants.PromptVersion,
            _modelId,
            chunkFingerprints);
    }

    private static RootKnowledgeFactExtractionRequest BuildRequest(
        MarkdownDocument document,
        RootMarkdownChunk chunk)
    {
        var frontMatter = document.FrontMatter.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var sectionPath = chunk.HeadingPath.Count == 0
            ? null
            : string.Join(PathSeparator, chunk.HeadingPath);

        return new RootKnowledgeFactExtractionRequest(
            document.DocumentUri.AbsoluteUri,
            chunk.ChunkId,
            chunk.Markdown,
            document.Title,
            sectionPath,
            frontMatter);
    }

    private static KnowledgeExtractionResult Convert(IReadOnlyList<RootKnowledgeFactExtractionResult> results)
    {
        var entities = new List<KnowledgeEntityFact>();
        var assertions = new List<KnowledgeAssertionFact>();

        foreach (var result in results)
        {
            var converted = Convert(result);
            entities.AddRange(converted.Entities);
            assertions.AddRange(converted.Assertions);
        }

        return new KnowledgeExtractionResult
        {
            Entities = entities,
            Assertions = assertions,
        };
    }

    private static KnowledgeExtractionResult Convert(RootKnowledgeFactExtractionResult result)
    {
        return new KnowledgeExtractionResult
        {
            Entities = result.Entities
                .Select(entity => new KnowledgeEntityFact
                {
                    Id = string.IsNullOrWhiteSpace(entity.Id) ? null : entity.Id,
                    Label = entity.Label,
                    Type = entity.Type,
                    SameAs = entity.SameAs?.ToList() ?? [],
                    Source = result.DocumentId,
                })
                .ToList(),
            Assertions = result.Assertions
                .Select(assertion => new KnowledgeAssertionFact
                {
                    SubjectId = assertion.SubjectId,
                    Predicate = assertion.Predicate,
                    ObjectId = assertion.ObjectId,
                    Confidence = assertion.Confidence,
                    Source = assertion.Source ?? result.DocumentId,
                })
                .ToList(),
        };
    }
}
