using System.Text.Json.Serialization;
using static ManagedCode.MarkdownLd.Kb.KnowledgeFactConstants;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactEntity(
    [property: JsonPropertyName(EntityIdJsonName)] string Id,
    [property: JsonPropertyName(EntityTypeJsonName)] string Type,
    [property: JsonPropertyName(EntityLabelJsonName)] string Label,
    [property: JsonPropertyName(EntitySameAsJsonName)] IReadOnlyList<string>? SameAs = null);
