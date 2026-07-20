using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphNormalizer(Uri? baseUri = null)
{
    private const string InvalidAcyclicPredicateMessagePrefix = "Graph normalization acyclic predicate could not be resolved: ";

    private readonly KnowledgeFactMerger _merger = new(baseUri);

    public KnowledgeGraphNormalizationResult Normalize(
        KnowledgeExtractionResult facts,
        KnowledgeGraphNormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var mergeResult = _merger.MergeWithReport(facts);
        var warnings = new List<KnowledgeGraphNormalizationWarning>(mergeResult.Warnings);
        var provenanceNormalized = KnowledgeGraphProvenanceNormalizer.Normalize(mergeResult.Facts, warnings);
        var confidenceNormalized = NormalizeConfidence(provenanceNormalized.Assertions, warnings);
        var valid = RemoveInvalidAndSelfLoopEdges(confidenceNormalized, warnings);
        var symmetric = NormalizeSymmetricEdges(valid, warnings);
        var acyclicPredicates = ResolveAcyclicPredicates(options ?? KnowledgeGraphNormalizationOptions.Default);
        var normalized = KnowledgeGraphAcyclicEdgeNormalizer.Normalize(symmetric, acyclicPredicates, warnings);
        var normalizedFacts = provenanceNormalized with
        {
            Assertions = SortEdges(normalized),
        };
        return new KnowledgeGraphNormalizationResult(
            normalizedFacts,
            new KnowledgeGraphNormalizationReport(SortWarnings(warnings)));
    }

    private static List<KnowledgeAssertionFact> NormalizeConfidence(
        IReadOnlyList<KnowledgeAssertionFact> assertions,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var normalized = new List<KnowledgeAssertionFact>(assertions.Count);
        foreach (var assertion in assertions)
        {
            if (double.IsFinite(assertion.Confidence) && assertion.Confidence > FullConfidence)
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.ConfidenceNormalized,
                    assertion));
                normalized.Add(assertion with { Confidence = FullConfidence });
                continue;
            }

            normalized.Add(assertion);
        }

        return normalized;
    }

    private static List<KnowledgeAssertionFact> RemoveInvalidAndSelfLoopEdges(
        IReadOnlyList<KnowledgeAssertionFact> assertions,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var valid = new List<KnowledgeAssertionFact>(assertions.Count);
        foreach (var assertion in assertions)
        {
            if (!IsMaterializable(assertion))
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved,
                    assertion));
                continue;
            }

            if (assertion.SubjectId.Equals(assertion.ObjectId, StringComparison.Ordinal))
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.SelfLoopRemoved,
                    assertion));
                continue;
            }

            valid.Add(assertion);
        }

        return valid;
    }

    private static bool IsMaterializable(KnowledgeAssertionFact assertion)
    {
        return Uri.TryCreate(assertion.SubjectId, UriKind.Absolute, out _) &&
               Uri.TryCreate(assertion.ObjectId, UriKind.Absolute, out _) &&
               KnowledgeGraphPredicateResolver.Resolve(assertion.Predicate) is not null &&
               double.IsFinite(assertion.Confidence) &&
               assertion.Confidence >= ZeroConfidence;
    }

    private static List<KnowledgeAssertionFact> NormalizeSymmetricEdges(
        IReadOnlyList<KnowledgeAssertionFact> assertions,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var normalized = new List<KnowledgeAssertionFact>(assertions.Count);
        var related = new Dictionary<(string Left, string PredicateId, string Right), int>();
        foreach (var assertion in assertions)
        {
            var predicateId = ResolvePredicateId(assertion.Predicate);
            if (!predicateId.Equals(KbRelatedToText, StringComparison.Ordinal))
            {
                normalized.Add(assertion);
                continue;
            }

            var key = CreateSymmetricKey(assertion, predicateId);
            if (!related.TryGetValue(key, out var existingIndex))
            {
                related.Add(key, normalized.Count);
                normalized.Add(assertion);
                continue;
            }

            warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                KnowledgeGraphNormalizationWarningCode.SymmetricDuplicateEdgeRemoved,
                assertion));
            normalized[existingIndex] = MergeEdges(normalized[existingIndex], assertion);
        }

        return normalized;
    }

    private static (string Left, string PredicateId, string Right) CreateSymmetricKey(
        KnowledgeAssertionFact edge,
        string predicateId)
    {
        return string.Compare(edge.SubjectId, edge.ObjectId, StringComparison.Ordinal) <= 0
            ? (edge.SubjectId, predicateId, edge.ObjectId)
            : (edge.ObjectId, predicateId, edge.SubjectId);
    }

    private static KnowledgeAssertionFact MergeEdges(
        KnowledgeAssertionFact left,
        KnowledgeAssertionFact right)
    {
        return left with
        {
            Confidence = Math.Max(left.Confidence, right.Confidence),
            Source = KnowledgeFactSourceCollector.SelectPrimaryAssertionSource(left, right),
            Sources = KnowledgeFactSourceCollector.MergeAssertionSources(left, right),
        };
    }

    private static HashSet<string> ResolveAcyclicPredicates(KnowledgeGraphNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.AcyclicPredicates);

        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var predicate in options.AcyclicPredicates)
        {
            var predicateUri = KnowledgeGraphPredicateResolver.Resolve(predicate)
                ?? throw new ArgumentException(InvalidAcyclicPredicateMessagePrefix + predicate, nameof(options));

            resolved.Add(predicateUri.AbsoluteUri);
        }

        return resolved;
    }

    internal static string ResolvePredicateId(string predicate)
    {
        return KnowledgeGraphPredicateResolver.Resolve(predicate)?.AbsoluteUri ?? string.Empty;
    }

    private static List<KnowledgeAssertionFact> SortEdges(IEnumerable<KnowledgeAssertionFact> assertions)
    {
        return assertions
            .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => ResolvePredicateId(edge.Predicate), StringComparer.Ordinal)
            .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal)
            .ToList();
    }

    private static KnowledgeGraphNormalizationWarning[] SortWarnings(
        IEnumerable<KnowledgeGraphNormalizationWarning> warnings)
    {
        return warnings
            .OrderBy(static warning => warning.Code)
            .ThenBy(static warning => warning.Edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static warning => ResolvePredicateId(warning.Edge.Predicate), StringComparer.Ordinal)
            .ThenBy(static warning => warning.Edge.ObjectId, StringComparer.Ordinal)
            .ToArray();
    }
}
