using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownKnowledgeBankOptions
{
    public MarkdownKnowledgePipelineOptions PipelineOptions { get; init; } = new();

    public IChatClient? ChatClient { get; init; }

    public ChatOptions? ChatOptions { get; init; }

    public IEmbeddingGenerator<string, Embedding<float>>? EmbeddingGenerator { get; init; }

    public IMarkdownChunker? MarkdownChunker { get; init; }

    public string? AnswerSystemPrompt { get; init; }

    public string? RewriteSystemPrompt { get; init; }
}

internal static class MarkdownKnowledgeBankConstants
{
    public const string MissingChatClientMessage = "MarkdownKnowledgeBank requires an IChatClient to answer questions with citations.";
    public const string MissingEmbeddingGeneratorMessage = "MarkdownKnowledgeBank requires an IEmbeddingGenerator to build a semantic index.";
}
