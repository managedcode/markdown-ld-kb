using System.Text;

namespace ManagedCode.MarkdownLd.Kb.Query;

internal static class KnowledgeAnswerPromptBuilder
{
    public static string BuildRewritePrompt(
        string question,
        IReadOnlyList<KnowledgeAnswerConversationMessage> history)
    {
        var builder = new StringBuilder();
        AppendHistory(builder, history);
        builder.AppendLine();
        builder.AppendLine(KnowledgeAnsweringConstants.RewriteInstructionLabel);
        builder.AppendLine(question.Trim());
        return builder.ToString();
    }

    public static string BuildAnswerPrompt(
        KnowledgeAnswerRequest request,
        string searchQuery,
        IReadOnlyList<KnowledgeAnswerCitation> citations)
    {
        var builder = new StringBuilder();
        builder.AppendLine(KnowledgeAnsweringConstants.QuestionLabel);
        builder.AppendLine(request.Question.Trim());
        builder.AppendLine();
        builder.AppendLine(KnowledgeAnsweringConstants.SearchQueryLabel);
        builder.AppendLine(searchQuery);
        builder.AppendLine();
        AppendHistory(builder, request.ConversationHistory);
        builder.AppendLine();
        AppendCitations(builder, citations);
        return builder.ToString();
    }

    private static void AppendHistory(
        StringBuilder builder,
        IReadOnlyList<KnowledgeAnswerConversationMessage> history)
    {
        builder.AppendLine(KnowledgeAnsweringConstants.ConversationHistoryLabel);
        if (history.Count == 0)
        {
            builder.AppendLine(KnowledgeAnsweringConstants.NoConversationHistoryText);
            return;
        }

        foreach (var message in history)
        {
            builder
                .Append(FormatRole(message.Role))
                .Append(KnowledgeAnsweringConstants.RoleSeparator)
                .AppendLine(message.Content.Trim());
        }
    }

    private static void AppendCitations(
        StringBuilder builder,
        IReadOnlyList<KnowledgeAnswerCitation> citations)
    {
        builder.AppendLine(KnowledgeAnsweringConstants.RetrievedContextLabel);
        if (citations.Count == 0)
        {
            builder.AppendLine(KnowledgeAnsweringConstants.NoRelevantDocumentsText);
            return;
        }

        foreach (var citation in citations)
        {
            AppendCitation(builder, citation);
        }
    }

    private static void AppendCitation(StringBuilder builder, KnowledgeAnswerCitation citation)
    {
        builder
            .Append(KnowledgeAnsweringConstants.CitationOpen)
            .Append(citation.Index)
            .Append(KnowledgeAnsweringConstants.CitationClose)
            .Append(KnowledgeAnsweringConstants.CitationSourceLabel)
            .AppendLine(citation.SourcePath);

        AppendHeading(builder, citation);
        builder
            .Append(KnowledgeAnsweringConstants.CitationMatchLabel)
            .AppendLine(citation.MatchLabel)
            .AppendLine(KnowledgeAnsweringConstants.CitationSnippetLabel)
            .AppendLine(citation.Snippet);
    }

    private static void AppendHeading(StringBuilder builder, KnowledgeAnswerCitation citation)
    {
        if (citation.HeadingPath.Count == 0)
        {
            return;
        }

        builder
            .Append(KnowledgeAnsweringConstants.CitationHeadingLabel)
            .AppendLine(string.Join(KnowledgeAnsweringConstants.HeadingSeparator, citation.HeadingPath));
    }

    private static string FormatRole(KnowledgeAnswerConversationRole role)
    {
        return role == KnowledgeAnswerConversationRole.User
            ? KnowledgeAnsweringConstants.UserRoleLabel
            : KnowledgeAnsweringConstants.AssistantRoleLabel;
    }
}
