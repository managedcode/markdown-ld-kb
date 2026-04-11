using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgePipeline
{
    private readonly MarkdownDocumentParser _parser;
    private readonly DeterministicKnowledgeFactExtractor _deterministicExtractor;
    private readonly KnowledgeFactMerger _factMerger;
    private readonly KnowledgeGraphBuilder _graphBuilder;
    private readonly ChatClientKnowledgeFactExtractor? _chatExtractor;

    public MarkdownKnowledgePipeline(Uri? baseUri = null, IChatClient? chatClient = null)
    {
        var effectiveBaseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri("https://example.com/", UriKind.Absolute));
        _parser = new MarkdownDocumentParser(effectiveBaseUri);
        _deterministicExtractor = new DeterministicKnowledgeFactExtractor(effectiveBaseUri);
        _factMerger = new KnowledgeFactMerger(effectiveBaseUri);
        _graphBuilder = new KnowledgeGraphBuilder(effectiveBaseUri);
        _chatExtractor = chatClient is null ? null : new ChatClientKnowledgeFactExtractor(chatClient);
    }

    public async Task<MarkdownKnowledgeBuildResult> BuildAsync(IEnumerable<MarkdownSourceDocument> sources, CancellationToken cancellationToken = default)
    {
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
