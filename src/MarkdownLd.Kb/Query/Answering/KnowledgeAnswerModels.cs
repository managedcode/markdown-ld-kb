using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed record KnowledgeAnswerRequest(string Question)
{
    public IReadOnlyList<KnowledgeAnswerConversationMessage> ConversationHistory { get; init; } = Array.Empty<KnowledgeAnswerConversationMessage>();

    public bool RewriteQuestionWithHistory { get; init; } = true;

    public KnowledgeGraphRankedSearchOptions SearchOptions { get; init; } = new();

    public KnowledgeGraphSemanticIndex? SemanticIndex { get; init; }

    public int MaxCitations { get; init; } = KnowledgeAnsweringConstants.DefaultMaxCitations;

    public int MaxSnippetLength { get; init; } = KnowledgeAnsweringConstants.DefaultMaxSnippetLength;

    public IReadOnlyList<string> AllowedSourcePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedDocumentUris { get; init; } = Array.Empty<string>();
}

public sealed record KnowledgeAnswerConversationMessage(
    KnowledgeAnswerConversationRole Role,
    string Content);

public enum KnowledgeAnswerConversationRole
{
    User,
    Assistant,
}

public sealed record KnowledgeAnswerResult(
    string Question,
    string SearchQuery,
    string Answer,
    IReadOnlyList<KnowledgeAnswerCitation> Citations);

public sealed record KnowledgeAnswerCitation(
    int Index,
    string DocumentUri,
    string SourcePath,
    IReadOnlyList<string> HeadingPath,
    string Snippet,
    string MatchNodeId,
    string MatchLabel,
    double Score,
    KnowledgeGraphRankedSearchSource SearchSource);
