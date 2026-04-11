using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeChatExtractionEnvelope
{
    public List<KnowledgeEntityFact> Entities { get; init; } = [];
    public List<KnowledgeAssertionFact> Assertions { get; init; } = [];
}

public sealed class ChatClientKnowledgeFactExtractor
{
    private readonly IChatClient _chatClient;

    public ChatClientKnowledgeFactExtractor(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<KnowledgeExtractionResult> ExtractAsync(MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(document);
        var response = await _chatClient.GetResponseAsync<KnowledgeChatExtractionEnvelope>(
            prompt,
            cancellationToken: cancellationToken);

        var envelope = response.Result ?? new KnowledgeChatExtractionEnvelope();
        return new KnowledgeExtractionResult
        {
            Entities = envelope.Entities ?? [],
            Assertions = envelope.Assertions ?? [],
        };
    }

    private static string BuildPrompt(MarkdownDocument document)
    {
        var sections = string.Join(
            Environment.NewLine + Environment.NewLine,
            document.Sections.Select(section =>
                $"SECTION: {section.Heading}{Environment.NewLine}{section.Text}"));

        return
            "Extract knowledge facts from the Markdown document. " +
            "Return only JSON matching the requested structured output envelope. " +
            $"DOCUMENT_URI: {document.DocumentUri.AbsoluteUri}{Environment.NewLine}" +
            $"TITLE: {document.Title}{Environment.NewLine}" +
            $"BODY:{Environment.NewLine}{document.Body}{Environment.NewLine}{Environment.NewLine}" +
            $"SECTIONS:{Environment.NewLine}{sections}";
    }
}
