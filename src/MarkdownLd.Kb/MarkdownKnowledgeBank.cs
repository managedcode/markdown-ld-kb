using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgeBank
{
    private readonly MarkdownKnowledgePipeline _pipeline;
    private readonly ChatClientKnowledgeAnswerService? _answerService;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly MarkdownChunkEvaluator _chunkEvaluator;
    private readonly MarkdownChunkEvaluationOptions _defaultChunkEvaluationOptions;

    public MarkdownKnowledgeBank(MarkdownKnowledgeBankOptions? options = null)
    {
        var effectiveOptions = options ?? new MarkdownKnowledgeBankOptions();
        ArgumentNullException.ThrowIfNull(effectiveOptions.PipelineOptions);

        var pipelineOptions = CreatePipelineOptions(effectiveOptions);
        _pipeline = new MarkdownKnowledgePipeline(pipelineOptions);
        _answerService = CreateAnswerService(effectiveOptions, pipelineOptions);
        _embeddingGenerator = effectiveOptions.EmbeddingGenerator;
        _chunkEvaluator = new MarkdownChunkEvaluator(pipelineOptions.MarkdownChunker);
        _defaultChunkEvaluationOptions = CreateChunkEvaluationOptions(pipelineOptions);
    }

    public async Task<MarkdownKnowledgeBankBuild> BuildAsync(
        IEnumerable<MarkdownSourceDocument> sources,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.BuildAsync(sources, cancellationToken).ConfigureAwait(false);
        return CreateBuild(result);
    }

    public async Task<MarkdownKnowledgeBankBuild> BuildAsync(
        IEnumerable<KnowledgeSourceDocument> sources,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline.BuildAsync(sources, cancellationToken).ConfigureAwait(false);
        return CreateBuild(result);
    }

    public async Task<MarkdownKnowledgeBankBuild> BuildFromMarkdownAsync(
        string markdown,
        string? path = null,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline
            .BuildFromMarkdownAsync(markdown, path, conversionOptions, cancellationToken)
            .ConfigureAwait(false);
        return CreateBuild(result);
    }

    public async Task<MarkdownKnowledgeBankBuild> BuildFromFileAsync(
        string filePath,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline
            .BuildFromFileAsync(filePath, conversionOptions, cancellationToken)
            .ConfigureAwait(false);
        return CreateBuild(result);
    }

    public async Task<MarkdownKnowledgeBankBuild> BuildFromDirectoryAsync(
        string directoryPath,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        string? searchPattern = null,
        SearchOption searchOption = SearchOption.AllDirectories,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline
            .BuildFromDirectoryAsync(directoryPath, conversionOptions, searchPattern, searchOption, cancellationToken)
            .ConfigureAwait(false);
        return CreateBuild(result);
    }

    public async Task<MarkdownKnowledgeBankIncrementalBuild> BuildIncrementalAsync(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline
            .BuildIncrementalAsync(sources, previousManifest, previousGraph, cancellationToken)
            .ConfigureAwait(false);
        return CreateIncrementalBuild(result);
    }

    public async Task<MarkdownKnowledgeBankIncrementalBuild> BuildIncrementalAsync(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _pipeline
            .BuildIncrementalAsync(sources, previousManifest, previousGraph, cancellationToken)
            .ConfigureAwait(false);
        return CreateIncrementalBuild(result);
    }

    public KnowledgeGraphSourceChangeSet PlanChanges(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null)
    {
        return KnowledgeGraphSourceManifest.CreateChangeSet(sources, previousManifest);
    }

    public KnowledgeGraphSourceChangeSet PlanChanges(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null)
    {
        return KnowledgeGraphSourceManifest.CreateChangeSet(sources, previousManifest);
    }

    public MarkdownChunkEvaluationResult EvaluateChunks(
        string markdown,
        string? path = null,
        IEnumerable<MarkdownChunkCoverageExpectation>? coverageExpectations = null,
        MarkdownChunkEvaluationOptions? options = null)
    {
        return _chunkEvaluator.Evaluate(markdown, path, coverageExpectations, options ?? _defaultChunkEvaluationOptions);
    }

    public MarkdownChunkEvaluationResult EvaluateChunks(
        MarkdownDocument document,
        IEnumerable<MarkdownChunkCoverageExpectation>? coverageExpectations = null,
        MarkdownChunkEvaluationOptions? options = null)
    {
        return _chunkEvaluator.AnalyzeDocument(document, coverageExpectations, options ?? _defaultChunkEvaluationOptions);
    }

    private MarkdownKnowledgeBankBuild CreateBuild(MarkdownKnowledgeBuildResult result)
    {
        return new MarkdownKnowledgeBankBuild(result, _answerService, _embeddingGenerator);
    }

    private MarkdownKnowledgeBankIncrementalBuild CreateIncrementalBuild(
        MarkdownKnowledgeIncrementalBuildResult result)
    {
        return new MarkdownKnowledgeBankIncrementalBuild(result, CreateBuild(result.BuildResult));
    }

    private static MarkdownKnowledgePipelineOptions CreatePipelineOptions(MarkdownKnowledgeBankOptions options)
    {
        var pipelineOptions = options.PipelineOptions;
        return pipelineOptions with
        {
            ChatClient = pipelineOptions.ChatClient ?? options.ChatClient,
            ChatOptions = pipelineOptions.ChatOptions ?? options.ChatOptions,
            MarkdownChunker = options.MarkdownChunker ?? pipelineOptions.MarkdownChunker,
        };
    }

    private static ChatClientKnowledgeAnswerService? CreateAnswerService(
        MarkdownKnowledgeBankOptions options,
        MarkdownKnowledgePipelineOptions pipelineOptions)
    {
        var chatClient = options.ChatClient ?? pipelineOptions.ChatClient;
        return chatClient is null
            ? null
            : new ChatClientKnowledgeAnswerService(
                chatClient,
                options.ChatOptions ?? pipelineOptions.ChatOptions,
                options.AnswerSystemPrompt,
                options.RewriteSystemPrompt);
    }

    private static MarkdownChunkEvaluationOptions CreateChunkEvaluationOptions(
        MarkdownKnowledgePipelineOptions pipelineOptions)
    {
        var baseUri = KnowledgeNaming.NormalizeBaseUri(
            pipelineOptions.BaseUri ?? new Uri(MarkdownKnowledgeDefaults.BaseUriText, UriKind.Absolute));
        return new MarkdownChunkEvaluationOptions
        {
            ParsingOptions = new MarkdownParsingOptions
            {
                DefaultBaseUrl = baseUri.AbsoluteUri,
                Chunking = pipelineOptions.ChunkingOptions ?? MarkdownChunkingOptions.Default,
            },
        };
    }
}
