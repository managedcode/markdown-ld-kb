using System.Globalization;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed partial class KnowledgeGraphRuleExtractor(Uri baseUri)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);

    public KnowledgeGraphRuleExtractionResult Extract(
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeGraphBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);

        var facts = new KnowledgeExtractionResult();
        var diagnostics = new List<string>();
        AddConfiguredRules(facts, options, diagnostics);
        if (!options.IncludeFrontMatterRules)
        {
            return new KnowledgeGraphRuleExtractionResult(facts, diagnostics);
        }

        foreach (var document in documents)
        {
            AddFrontMatterRules(facts, document, diagnostics);
        }

        return new KnowledgeGraphRuleExtractionResult(facts, diagnostics);
    }

    private static void AddConfiguredRules(
        KnowledgeExtractionResult result,
        KnowledgeGraphBuildOptions options,
        ICollection<string> diagnostics)
    {
        for (var index = 0; index < options.Entities.Count; index++)
        {
            AddConfiguredEntityRule(result, diagnostics, options.Entities[index], index);
        }

        for (var index = 0; index < options.Edges.Count; index++)
        {
            AddConfiguredEdgeRule(result, diagnostics, options.Edges[index], index);
        }
    }

    private static void AddConfiguredEntityRule(
        KnowledgeExtractionResult result,
        ICollection<string> diagnostics,
        KnowledgeGraphEntityRule entity,
        int index)
    {
        if (string.IsNullOrWhiteSpace(entity.Label))
        {
            diagnostics.Add(CreateDiagnostic(GraphRuleConfiguredEntityRule, index, GraphRuleLabelRequiredMessage));
            return;
        }

        result.Entities.Add(new KnowledgeEntityFact
        {
            Id = entity.Id,
            Label = entity.Label,
            Type = entity.Type,
            SameAs = entity.SameAs.Where(static item => !string.IsNullOrWhiteSpace(item)).ToList(),
            Confidence = entity.Confidence,
            Source = entity.Source,
        });
    }

    private static void AddConfiguredEdgeRule(
        KnowledgeExtractionResult result,
        ICollection<string> diagnostics,
        KnowledgeGraphEdgeRule edge,
        int index)
    {
        if (string.IsNullOrWhiteSpace(edge.SubjectId))
        {
            diagnostics.Add(CreateDiagnostic(GraphRuleConfiguredEdgeRule, index, GraphRuleSubjectRequiredMessage));
            return;
        }

        if (!TryValidateConfiguredEdgePredicate(edge.Predicate, diagnostics, index, out var predicate) ||
            string.IsNullOrWhiteSpace(edge.ObjectId))
        {
            if (string.IsNullOrWhiteSpace(edge.ObjectId))
            {
                diagnostics.Add(CreateDiagnostic(GraphRuleConfiguredEdgeRule, index, GraphRuleObjectRequiredMessage));
            }

            return;
        }

        result.Assertions.Add(new KnowledgeAssertionFact
        {
            SubjectId = edge.SubjectId,
            Predicate = predicate,
            ObjectId = edge.ObjectId,
            Confidence = edge.Confidence,
            Source = edge.Source,
        });
    }

    private static bool TryValidateConfiguredEdgePredicate(
        string? predicate,
        ICollection<string> diagnostics,
        int index,
        out string canonicalPredicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            diagnostics.Add(CreateDiagnostic(GraphRuleConfiguredEdgeRule, index, GraphRulePredicateRequiredMessage));
            canonicalPredicate = string.Empty;
            return false;
        }

        if (TryValidatePredicate(predicate, out canonicalPredicate))
        {
            return true;
        }

        diagnostics.Add(CreateDiagnostic(GraphRuleConfiguredEdgeRule, index, GraphRuleSupportedPredicateRequiredMessage));
        return false;
    }

    private void AddFrontMatterRules(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        ICollection<string> diagnostics)
    {
        AddGraphEntities(result, document, diagnostics);
        AddGroupRules(result, document, diagnostics);
        AddTargetEdges(result, document, diagnostics, KbRelatedTo, GraphRelatedKey, GraphRelatedCamelKey);
        AddTargetEdges(result, document, diagnostics, KbNextStep, GraphNextStepsKey, GraphNextStepsCamelKey);
        AddGraphEdges(result, document, diagnostics);
    }

    private void AddGraphEntities(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        ICollection<string> diagnostics)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphEntitiesKey, GraphEntitiesCamelKey))
        {
            if (!TryReadNodeReference(item.Value, document, out var node))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleNodeRequiredMessage));
                continue;
            }

            result.Entities.Add(new KnowledgeEntityFact
            {
                Id = node.Id,
                Label = node.Label,
                Type = node.Type ?? DefaultSchemaThing,
                SameAs = node.SameAs,
                Confidence = node.Confidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private void AddGroupRules(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        ICollection<string> diagnostics)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphGroupsKey, GraphGroupsCamelKey))
        {
            if (!TryReadNodeReference(item.Value, document, out var group))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleNodeRequiredMessage));
                continue;
            }

            result.Entities.Add(new KnowledgeEntityFact
            {
                Id = group.Id,
                Label = group.Label,
                Type = group.Type ?? SchemaDefinedTermTypeText,
                SameAs = group.SameAs,
                Confidence = group.Confidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
            result.Assertions.Add(CreateDocumentEdge(document, KbMemberOf, group.Id));
        }
    }

    private void AddTargetEdges(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        ICollection<string> diagnostics,
        string predicate,
        string snakeKey,
        string camelKey)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, snakeKey, camelKey))
        {
            if (!TryReadNodeReference(item.Value, document, out var target))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleNodeRequiredMessage));
                continue;
            }

            AddTargetEntityIfNeeded(result, document, target);
            result.Assertions.Add(CreateDocumentEdge(document, predicate, target.Id));
        }
    }

    private void AddGraphEdges(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        ICollection<string> diagnostics)
    {
        foreach (var item in ReadFrontMatterItems(document.FrontMatter, GraphEdgesKey, GraphEdgesCamelKey))
        {
            if (item.Value is not IReadOnlyDictionary<string, object?> map)
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleMappingRequiredMessage));
                continue;
            }

            if (!TryReadString(map, PredicateKey, out var predicate))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRulePredicateRequiredMessage));
                continue;
            }

            if (!TryValidatePredicate(predicate, out var canonicalPredicate))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleSupportedPredicateRequiredMessage));
                continue;
            }

            var subject = ReadMapNodeId(map, document, SubjectIdKey, SubjectIdSnakeKey, SubjectKey)
                ?? document.DocumentUri.AbsoluteUri;
            var graphObject = ReadMapNodeId(map, document, ObjectIdKey, ObjectIdSnakeKey, ObjectKey, TargetIdKey, TargetIdSnakeKey, TargetKey);
            if (string.IsNullOrWhiteSpace(graphObject))
            {
                diagnostics.Add(CreateDiagnostic(item.RuleName, item.Index, GraphRuleObjectRequiredMessage));
                continue;
            }

            result.Assertions.Add(new KnowledgeAssertionFact
            {
                SubjectId = subject,
                Predicate = canonicalPredicate,
                ObjectId = graphObject,
                Confidence = FullConfidence,
                Source = document.DocumentUri.AbsoluteUri,
            });
        }
    }

    private static void AddTargetEntityIfNeeded(
        KnowledgeExtractionResult result,
        MarkdownDocument document,
        GraphNodeReference target)
    {
        if (!target.ShouldAddEntity)
        {
            return;
        }

        result.Entities.Add(new KnowledgeEntityFact
        {
            Id = target.Id,
            Label = target.Label,
            Type = target.Type ?? DefaultSchemaThing,
            SameAs = target.SameAs,
            Confidence = target.Confidence,
            Source = document.DocumentUri.AbsoluteUri,
        });
    }

    private static KnowledgeAssertionFact CreateDocumentEdge(
        MarkdownDocument document,
        string predicate,
        string objectId)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = document.DocumentUri.AbsoluteUri,
            Predicate = predicate,
            ObjectId = objectId,
            Confidence = FullConfidence,
            Source = document.DocumentUri.AbsoluteUri,
        };
    }

    private static bool TryValidatePredicate(string? predicate, out string canonicalPredicate)
    {
        if (string.IsNullOrWhiteSpace(predicate))
        {
            canonicalPredicate = string.Empty;
            return false;
        }

        canonicalPredicate = KnowledgeNaming.NormalizePredicate(predicate);
        return !string.IsNullOrWhiteSpace(canonicalPredicate);
    }

    private static string CreateDiagnostic(string ruleName, int index, string reason)
    {
        return GraphRuleDiagnosticPrefix +
               ruleName +
               OpenBracketText +
               index.ToString(CultureInfo.InvariantCulture) +
               CloseBracketText +
               SpaceText +
               reason;
    }
}
