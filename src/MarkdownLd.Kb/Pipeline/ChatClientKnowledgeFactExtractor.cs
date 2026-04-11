using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using RootChatClientKnowledgeFactExtractor = ManagedCode.MarkdownLd.Kb.ChatClientKnowledgeFactExtractor;
using RootKnowledgeFactExtractionRequest = ManagedCode.MarkdownLd.Kb.KnowledgeFactExtractionRequest;
using RootKnowledgeFactExtractionResult = ManagedCode.MarkdownLd.Kb.KnowledgeFactExtractionResult;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class ChatClientKnowledgeFactExtractor(IChatClient chatClient)
{
    private readonly RootChatClientKnowledgeFactExtractor _extractor = new(chatClient);

    public async Task<KnowledgeExtractionResult> ExtractAsync(MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var request = BuildRequest(document);
        var result = await _extractor.ExtractAsync(request, cancellationToken).ConfigureAwait(false);
        return Convert(result);
    }

    private static RootKnowledgeFactExtractionRequest BuildRequest(MarkdownDocument document)
    {
        var frontMatter = document.FrontMatter.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var sectionPath = document.Sections.Count == 0
            ? null
            : string.Join(PathSeparator, document.Sections[0].HeadingPath);

        return new RootKnowledgeFactExtractionRequest(
            document.DocumentUri.AbsoluteUri,
            KnowledgeNaming.Slugify(document.SourcePath),
            document.Body,
            document.Title,
            sectionPath,
            frontMatter);
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
