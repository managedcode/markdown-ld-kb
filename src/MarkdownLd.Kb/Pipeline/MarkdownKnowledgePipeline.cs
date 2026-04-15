using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class MarkdownKnowledgePipeline
{
    private readonly MarkdownDocumentParser _parser;
    private readonly KnowledgeFactMerger _factMerger;
    private readonly KnowledgeGraphBuilder _graphBuilder;
    private readonly KnowledgeGraphRuleExtractor _ruleExtractor;
    private readonly KnowledgeSourceDocumentConverter _documentConverter;
    private readonly ChatClientKnowledgeFactExtractor? _chatExtractor;
    private readonly TiktokenKnowledgeGraphExtractor? _tiktokenExtractor;
    private readonly MarkdownKnowledgeExtractionMode _configuredExtractionMode;
    private readonly KnowledgeGraphBuildOptions _buildOptions;

    public MarkdownKnowledgePipeline(
        Uri? baseUri = null,
        IChatClient? chatClient = null,
        MarkdownKnowledgeExtractionMode extractionMode = MarkdownKnowledgeExtractionMode.Auto,
        TiktokenKnowledgeGraphOptions? tiktokenOptions = null)
        : this(baseUri, chatClient, extractionMode, tiktokenOptions, null)
    {
    }

    private MarkdownKnowledgePipeline(
        Uri? baseUri,
        IChatClient? chatClient,
        MarkdownKnowledgeExtractionMode extractionMode,
        TiktokenKnowledgeGraphOptions? tiktokenOptions,
        KnowledgeGraphBuildOptions? buildOptions)
    {
        var effectiveBaseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
        _parser = new MarkdownDocumentParser(effectiveBaseUri);
        _factMerger = new KnowledgeFactMerger(effectiveBaseUri);
        _graphBuilder = new KnowledgeGraphBuilder(effectiveBaseUri);
        _ruleExtractor = new KnowledgeGraphRuleExtractor(effectiveBaseUri);
        _documentConverter = new KnowledgeSourceDocumentConverter();
        _chatExtractor = chatClient is null ? null : new ChatClientKnowledgeFactExtractor(chatClient, effectiveBaseUri);
        _tiktokenExtractor = extractionMode == MarkdownKnowledgeExtractionMode.Tiktoken
            ? new TiktokenKnowledgeGraphExtractor(effectiveBaseUri, tiktokenOptions)
            : null;
        _configuredExtractionMode = extractionMode;
        _buildOptions = buildOptions ?? KnowledgeGraphBuildOptions.Default;
    }

    public Task<MarkdownKnowledgeBuildResult> BuildAsync(IEnumerable<KnowledgeSourceDocument> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return BuildAsync(sources.Select(static source => source.ToMarkdownSourceDocument()), cancellationToken);
    }

    public Task<MarkdownKnowledgeBuildResult> BuildAsync(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphBuildOptions buildOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return BuildAsync(
            sources.Select(static source => source.ToMarkdownSourceDocument()),
            buildOptions,
            cancellationToken);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildFromFileAsync(
        string filePath,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var source = await _documentConverter.ConvertFileAsync(filePath, conversionOptions, cancellationToken).ConfigureAwait(false);
        return await BuildAsync([source], cancellationToken).ConfigureAwait(false);
    }

    public Task<MarkdownKnowledgeBuildResult> BuildFromMarkdownAsync(
        string markdown,
        string? path = null,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var source = _documentConverter.ConvertContent(markdown, path, conversionOptions);
        return BuildAsync([source], cancellationToken);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildFromDirectoryAsync(
        string directoryPath,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        string? searchPattern = null,
        SearchOption searchOption = SearchOption.AllDirectories,
        CancellationToken cancellationToken = default)
    {
        var sources = new List<KnowledgeSourceDocument>();
        await foreach (var source in _documentConverter.ConvertDirectoryAsync(
                           directoryPath,
                           conversionOptions,
                           searchPattern,
                           searchOption,
                           cancellationToken).ConfigureAwait(false))
        {
            sources.Add(source);
        }

        return await BuildAsync(sources, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildAsync(IEnumerable<MarkdownSourceDocument> sources, CancellationToken cancellationToken = default)
    {
        return await BuildAsync(sources, _buildOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildAsync(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphBuildOptions buildOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(buildOptions);

        var documents = new List<MarkdownDocument>();
        var effectiveMode = ResolveExtractionMode();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = _parser.Parse(source);
            documents.Add(document);
        }

        var extractionResults = new List<KnowledgeExtractionResult>();
        TokenizedKnowledgeExtractionResult? tokenResult = null;
        if (effectiveMode == MarkdownKnowledgeExtractionMode.ChatClient)
        {
            foreach (var document in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                extractionResults.Add(await _chatExtractor!.ExtractAsync(document, cancellationToken).ConfigureAwait(false));
            }
        }

        if (effectiveMode == MarkdownKnowledgeExtractionMode.Tiktoken)
        {
            tokenResult = _tiktokenExtractor!.Extract(documents);
            extractionResults.Add(tokenResult.Facts);
        }

        extractionResults.Add(_ruleExtractor.Extract(documents, buildOptions));

        var mergedFacts = _factMerger.Merge(extractionResults.ToArray());
        var tokenIndex = effectiveMode == MarkdownKnowledgeExtractionMode.Tiktoken
            ? _tiktokenExtractor!.CreateIndex(tokenResult!.Segments, tokenResult.VectorSpace)
            : null;
        var graph = _graphBuilder.Build(documents, mergedFacts, tokenIndex);
        return new MarkdownKnowledgeBuildResult(documents, mergedFacts, graph)
        {
            ExtractionMode = effectiveMode,
            Diagnostics = CreateDiagnostics(effectiveMode),
        };
    }

    private MarkdownKnowledgeExtractionMode ResolveExtractionMode()
    {
        if (_configuredExtractionMode == MarkdownKnowledgeExtractionMode.Auto)
        {
            return _chatExtractor is null
                ? MarkdownKnowledgeExtractionMode.None
                : MarkdownKnowledgeExtractionMode.ChatClient;
        }

        if (_configuredExtractionMode == MarkdownKnowledgeExtractionMode.ChatClient && _chatExtractor is null)
        {
            throw new InvalidOperationException(MissingChatClientMessage);
        }

        return _configuredExtractionMode;
    }

    private static IReadOnlyList<string> CreateDiagnostics(MarkdownKnowledgeExtractionMode effectiveMode)
    {
        return effectiveMode == MarkdownKnowledgeExtractionMode.None
            ? [NoExtractorDiagnostic]
            : [];
    }
}
