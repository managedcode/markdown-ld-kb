using System.Text.Json.Serialization;
using static ManagedCode.MarkdownLd.Kb.KnowledgeFactConstants;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactAssertion(
    [property: JsonPropertyName(AssertionSubjectJsonName)] string SubjectId,
    [property: JsonPropertyName(AssertionPredicateJsonName)] string Predicate,
    [property: JsonPropertyName(AssertionObjectJsonName)] string ObjectId,
    [property: JsonPropertyName(AssertionConfidenceJsonName)] double Confidence = 0.5,
    [property: JsonPropertyName(AssertionSourceJsonName)] string? Source = null);
