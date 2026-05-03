using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using Microsoft.Extensions.AI;
using PipelineMarkdownDocument = ManagedCode.MarkdownLd.Kb.Pipeline.MarkdownDocument;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgeBankBuild
{
    private readonly ChatClientKnowledgeAnswerService? _answerService;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;

    internal MarkdownKnowledgeBankBuild(
        MarkdownKnowledgeBuildResult result,
        ChatClientKnowledgeAnswerService? answerService,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        _answerService = answerService;
        _embeddingGenerator = embeddingGenerator;
    }

    public MarkdownKnowledgeBuildResult Result { get; }

    public IReadOnlyList<PipelineMarkdownDocument> Documents => Result.Documents;

    public KnowledgeExtractionResult Facts => Result.Facts;

    public KnowledgeGraph Graph => Result.Graph;

    public KnowledgeGraphContract Contract => Result.Contract;

    public MarkdownKnowledgeExtractionMode ExtractionMode => Result.ExtractionMode;

    public IReadOnlyList<string> Diagnostics => Result.Diagnostics;

    public KnowledgeGraphSemanticIndex? SemanticIndex { get; private set; }

    public KnowledgeGraphShaclValidationReport ValidateShacl(string? shapesTurtle = null)
    {
        return Result.ValidateShacl(shapesTurtle);
    }

    public async Task<KnowledgeGraphSemanticIndex> BuildSemanticIndexAsync(
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveGenerator = embeddingGenerator ??
                                 _embeddingGenerator ??
                                 throw new InvalidOperationException(MarkdownKnowledgeBankConstants.MissingEmbeddingGeneratorMessage);

        SemanticIndex = await Result
            .BuildSemanticIndexAsync(effectiveGenerator, cancellationToken)
            .ConfigureAwait(false);
        return SemanticIndex;
    }

    public Task<IReadOnlyList<KnowledgeGraphRankedSearchMatch>> SearchAsync(
        string query,
        KnowledgeGraphRankedSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Result.SearchRankedAsync(query, options, SemanticIndex, cancellationToken);
    }

    public Task<KnowledgeAnswerResult> AnswerAsync(
        string question,
        KnowledgeGraphRankedSearchOptions? searchOptions = null,
        CancellationToken cancellationToken = default)
    {
        return AnswerAsync(
            new KnowledgeAnswerRequest(question)
            {
                SearchOptions = searchOptions ?? new KnowledgeGraphRankedSearchOptions(),
            },
            cancellationToken);
    }

    public Task<KnowledgeAnswerResult> AnswerAsync(
        KnowledgeAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_answerService is null)
        {
            throw new InvalidOperationException(MarkdownKnowledgeBankConstants.MissingChatClientMessage);
        }

        var effectiveRequest = request.SemanticIndex is null && SemanticIndex is not null
            ? request with { SemanticIndex = SemanticIndex }
            : request;
        return _answerService.AnswerAsync(Result, effectiveRequest, cancellationToken);
    }
}
