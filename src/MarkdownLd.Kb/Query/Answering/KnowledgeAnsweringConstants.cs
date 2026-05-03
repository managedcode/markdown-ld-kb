using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnsweringConstants
{
    public const int DefaultMaxCitations = 5;
    public const int DefaultMaxSnippetLength = 600;
    public const int MinimumSnippetAnchorLength = 3;
    public const int MinimumSnippetContentLength = 1;
    public const int SnippetContextDivisor = 2;
    public const int ExactQuerySnippetAnchorPriority = 4;
    public const int QueryTokenSnippetAnchorPriority = 3;
    public const int LabelSnippetAnchorPriority = 2;
    public const int DescriptionSnippetAnchorPriority = 1;
    public const int SnippetAnchorPriorityMultiplier = 1000;
    public const int NoSnippetAnchorScore = 0;
    public const int MinimumContextOverlapTokenLength = 5;
    public const int MinimumContextOverlapTokenCount = 2;

    public const string DefaultAnswerSystemPrompt = """
        You are a Markdown-LD Knowledge Bank answer adapter.
        Answer only from the supplied retrieved Markdown context.
        If the context is insufficient, say that the knowledge graph has no relevant information.
        Cite sources with [1], [2], and similar markers when using retrieved context.
        Keep the answer in the same language as the user question.
        """;

    public const string DefaultRewriteSystemPrompt = """
        Rewrite the user's follow-up question into one standalone search query.
        Preserve the user's language.
        Resolve pronouns and omitted subjects from the conversation history.
        Output only the rewritten query.
        """;

    public const string EmptyAnswerMessage = "Knowledge answer response was empty.";
    public const string EmptyRewrittenQueryMessage = "Question rewrite returned an empty rewritten query.";
    public const string EmptyAllowedSourcePathMessage = "Allowed source path filters cannot contain empty values.";
    public const string EmptyAllowedDocumentUriMessage = "Allowed document URI filters cannot contain empty values.";
    public const string QuestionLabel = "Question:";
    public const string SearchQueryLabel = "Search query:";
    public const string ConversationHistoryLabel = "Conversation history:";
    public const string NoConversationHistoryText = "No prior conversation.";
    public const string RetrievedContextLabel = "Retrieved Markdown context:";
    public const string NoRelevantDocumentsText = "No relevant documents were found.";
    public const string RewriteInstructionLabel = "Rewrite this follow-up question as a standalone search query:";
    public const string UserRoleLabel = "user";
    public const string AssistantRoleLabel = "assistant";
    public const string RoleSeparator = ": ";
    public const char CitationOpen = '[';
    public const char CitationClose = ']';
    public const string CitationSourceLabel = " Source: ";
    public const string CitationHeadingLabel = " Heading: ";
    public const string CitationMatchLabel = " Match: ";
    public const string CitationSnippetLabel = " Snippet:";
    public const string HeadingSeparator = " / ";
    public const string SnippetTruncationSuffix = "...";
    public const string ProvenanceWasDerivedFromPredicate = PipelineConstants.ProvNamespaceText + PipelineConstants.ProvWasDerivedFromSuffix;
    public static readonly char[] SnippetAnchorSeparators =
    [
        ' ', '\t', '\r', '\n', '.', ',', ';', ':', '?', '!',
        '(', ')', '[', ']', '{', '}', '"', '\'',
    ];
}
