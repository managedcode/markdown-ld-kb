using System.Text;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using static ManagedCode.MarkdownLd.Kb.Query.NaturalLanguageSparqlConstants;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed class ChatClientNaturalLanguageSparqlTranslator : INaturalLanguageSparqlTranslator
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions _chatOptions;
    private readonly string _systemPrompt;

    public ChatClientNaturalLanguageSparqlTranslator(
        IChatClient chatClient,
        ChatOptions? chatOptions = null,
        string? systemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _chatOptions = chatOptions?.Clone() ?? new ChatOptions();
        _systemPrompt = systemPrompt ?? DefaultSystemPrompt;
    }

    public async Task<NaturalLanguageSparqlTranslation> TranslateAsync(
        KnowledgeGraph graph,
        string question,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, _systemPrompt),
            new ChatMessage(ChatRole.User, BuildUserPrompt(graph, question)),
        };

        var response = await _chatClient.GetResponseAsync(
            messages,
            BuildChatOptions(),
            cancellationToken).ConfigureAwait(false);

        var queryText = NormalizeQuery(response.Text);
        if (!KnowledgeNaming.IsReadOnlySparql(queryText, out var failureReason))
        {
            throw new ReadOnlySparqlQueryException(failureReason ?? ReadOnlyFailureMessage);
        }

        return new NaturalLanguageSparqlTranslation(
            question,
            queryText,
            ResolveQueryKind(queryText));
    }

    public async Task<NaturalLanguageSparqlExecutionResult> ExecuteAsync(
        KnowledgeGraph graph,
        string question,
        CancellationToken cancellationToken = default)
    {
        var translation = await TranslateAsync(graph, question, cancellationToken).ConfigureAwait(false);
        if (translation.QueryKind == NaturalLanguageSparqlQueryKind.Ask)
        {
            var askResult = await graph.ExecuteAskAsync(translation.QueryText, cancellationToken).ConfigureAwait(false);
            return new NaturalLanguageSparqlExecutionResult(translation, null, askResult);
        }

        var selectResult = await graph.ExecuteSelectAsync(translation.QueryText, cancellationToken).ConfigureAwait(false);
        return new NaturalLanguageSparqlExecutionResult(translation, selectResult, null);
    }

    private ChatOptions BuildChatOptions()
    {
        var options = _chatOptions.Clone();
        options.Temperature = 0;
        return options;
    }

    private static string BuildUserPrompt(KnowledgeGraph graph, string question)
    {
        var snapshot = graph.ToSnapshot();
        var nodesById = snapshot.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var types = snapshot.Edges
            .Where(static edge => edge.PredicateId == PipelineConstants.RdfTypeText)
            .Select(edge => nodesById.TryGetValue(edge.ObjectId, out var node) ? node.Label : edge.ObjectId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static typeId => typeId, StringComparer.Ordinal)
            .ToArray();
        var predicates = snapshot.Edges
            .Select(static edge => edge.PredicateLabel)
            .Where(static predicate => !string.Equals(predicate, PipelineConstants.RdfPrefix + PipelineConstants.Colon + PipelineConstants.TypeKey, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static predicate => predicate, StringComparer.Ordinal)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine(SchemaSummaryLabel);
        builder.AppendLine(TypesLabel);
        AppendItems(builder, types);
        builder.AppendLine(PredicatesLabel);
        AppendItems(builder, predicates);
        builder.AppendLine();
        builder.AppendLine(InstructionLabel);
        builder.AppendLine(BulletPrefix + ReadOnlyFailureMessage);
        builder.AppendLine();
        builder.AppendLine(QuestionLabel);
        builder.AppendLine(question.Trim());
        return builder.ToString();
    }

    private static void AppendItems(StringBuilder builder, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            builder.AppendLine(EmptySchemaPlaceholder);
            return;
        }

        foreach (var value in values)
        {
            builder.Append(BulletPrefix).AppendLine(value);
        }
    }

    private static string NormalizeQuery(string text)
    {
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException(EmptyTranslationMessage);
        }

        var fenced = ExtractFencedQuery(trimmed, SparqlFence, StringComparison.OrdinalIgnoreCase);
        if (fenced is not null)
        {
            return fenced;
        }

        fenced = ExtractFencedQuery(trimmed, CodeFence, StringComparison.Ordinal);
        if (fenced is not null)
        {
            return fenced;
        }

        if (trimmed.StartsWith(SparqlFence, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[SparqlFence.Length..].Trim();
        }
        else if (trimmed.StartsWith(CodeFence, StringComparison.Ordinal))
        {
            trimmed = trimmed[CodeFence.Length..].Trim();
        }

        return trimmed.EndsWith(CodeFence, StringComparison.Ordinal)
            ? trimmed[..^CodeFence.Length].Trim()
            : trimmed;
    }

    private static string? ExtractFencedQuery(string text, string openingFence, StringComparison comparison)
    {
        var start = text.IndexOf(openingFence, comparison);
        if (start < 0)
        {
            return null;
        }

        var contentStart = start + openingFence.Length;
        var content = text[contentStart..].Trim();
        var end = content.IndexOf(CodeFence, StringComparison.Ordinal);
        return end >= 0
            ? content[..end].Trim()
            : content;
    }

    private static NaturalLanguageSparqlQueryKind ResolveQueryKind(string queryText)
    {
        var parser = new SparqlQueryParser();
        var query = parser.ParseFromString(queryText);
        return query.QueryType == SparqlQueryType.Ask
            ? NaturalLanguageSparqlQueryKind.Ask
            : NaturalLanguageSparqlQueryKind.Select;
    }
}
