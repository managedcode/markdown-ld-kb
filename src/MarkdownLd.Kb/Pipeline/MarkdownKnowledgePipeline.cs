using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class MarkdownKnowledgePipeline
{
    private readonly MarkdownDocumentParser _parser;
    private readonly DeterministicKnowledgeFactExtractor _deterministicExtractor;
    private readonly KnowledgeFactMerger _factMerger;
    private readonly KnowledgeGraphBuilder _graphBuilder;
    private readonly KnowledgeSourceDocumentConverter _documentConverter;
    private readonly ChatClientKnowledgeFactExtractor? _chatExtractor;

    public MarkdownKnowledgePipeline(Uri? baseUri = null, IChatClient? chatClient = null)
    {
        var effectiveBaseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
        _parser = new MarkdownDocumentParser(effectiveBaseUri);
        _deterministicExtractor = new DeterministicKnowledgeFactExtractor(effectiveBaseUri);
        _factMerger = new KnowledgeFactMerger(effectiveBaseUri);
        _graphBuilder = new KnowledgeGraphBuilder(effectiveBaseUri);
        _documentConverter = new KnowledgeSourceDocumentConverter();
        _chatExtractor = chatClient is null ? null : new ChatClientKnowledgeFactExtractor(chatClient);
    }

    public Task<MarkdownKnowledgeBuildResult> BuildAsync(IEnumerable<KnowledgeSourceDocument> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return BuildAsync(sources.Select(static source => source.ToMarkdownSourceDocument()), cancellationToken);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildFromFileAsync(
        string filePath,
        KnowledgeDocumentConversionOptions? conversionOptions = null,
        CancellationToken cancellationToken = default)
    {
        var source = await _documentConverter.ConvertFileAsync(filePath, conversionOptions, cancellationToken).ConfigureAwait(false);
        return await BuildAsync([source], cancellationToken).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(sources);

        var documents = new List<MarkdownDocument>();
        var deterministicResults = new List<KnowledgeExtractionResult>();
        var chatResults = new List<KnowledgeExtractionResult>();

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = _parser.Parse(source);
            documents.Add(document);

            var deterministic = _deterministicExtractor.Extract(document);
            deterministicResults.Add(deterministic);

            if (_chatExtractor is not null)
            {
                try
                {
                    var chatResult = await _chatExtractor.ExtractAsync(document, cancellationToken).ConfigureAwait(false);
                    chatResults.Add(chatResult);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    chatResults.Add(new KnowledgeExtractionResult());
                }
            }
        }

        var mergedFacts = _factMerger.Merge(deterministicResults.Concat(chatResults).ToArray());
        var graph = _graphBuilder.Build(documents, mergedFacts);
        return new MarkdownKnowledgeBuildResult(documents, mergedFacts, graph);
    }
}
