using System.Text.Json.Serialization;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactAssertion(
    [property: JsonPropertyName("s")] string SubjectId,
    [property: JsonPropertyName("p")] string Predicate,
    [property: JsonPropertyName("o")] string ObjectId,
    [property: JsonPropertyName("confidence")] double Confidence = 0.5,
    [property: JsonPropertyName("source")] string? Source = null);
