using System.Text.Json.Serialization;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactEntity(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("sameAs")] IReadOnlyList<string>? SameAs = null);
