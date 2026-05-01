using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial record KnowledgeGraphContract
{
    private const string ContractJsonRequiredMessage = "Knowledge graph contract JSON content is required.";
    private const string ContractYamlRequiredMessage = "Knowledge graph contract YAML content is required.";
    private const string ContractJsonParseMessage = "Knowledge graph contract JSON did not contain a contract.";
    private const string ContractYamlParseMessage = "Knowledge graph contract YAML did not contain a contract.";
    private const string ShaclPrefix = "sh";
    private const string ShaclNamespaceText = "http://www.w3.org/ns/shacl#";
    private const string TurtlePrefixStart = "@prefix ";
    private const string TurtlePrefixMiddle = ": <";
    private const string TurtlePrefixEnd = "> .";
    private const string ShapeUriPrefix = "urn:managedcode:markdown-ld-kb:shape:";
    private const string ShapeHeader = "> a sh:NodeShape ;";
    private const string TargetClassPrefix = "  sh:targetClass ";
    private const string PropertyOpen = "  sh:property [";
    private const string PathPrefix = "    sh:path ";
    private const string MinCountRequired = "    sh:minCount 1 ;";
    private const string MinCountOptional = "    sh:minCount 0 ;";
    private const string HasValuePrefix = "    sh:hasValue ";
    private const string NodeKindIri = "    sh:nodeKind sh:IRI ;";
    private const string MessagePrefix = "    sh:message \"";
    private const string RequiredPredicateMessage = "Required graph search predicate is missing.";
    private const string RequiredFacetMessage = "Required graph search facet is missing.";
    private const string OptionalResourceMessage = "Graph search resource predicate should point to an IRI when present.";
    private const string PropertyCloseContinue = "  ] ;";
    private const string PropertyCloseEnd = "  ] .";
    private const string StatementEnd = " .";
    private const string TurtleSemicolon = ";";
    private const string TurtleTermOpen = "<";
    private const string TurtleTermClose = ">";
    private const string QuoteEscape = "\\\"";

    private static readonly JsonSerializerOptions ContractJsonOptions = CreateJsonOptions();

    private static readonly ISerializer ContractYamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer ContractYamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithAttemptingUnquotedStringTypeDeserialization()
        .IgnoreUnmatchedProperties()
        .Build();

    public string SerializeJson()
    {
        return JsonSerializer.Serialize(this, ContractJsonOptions);
    }

    public static KnowledgeGraphContract LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(ContractJsonRequiredMessage, nameof(json));
        }

        return JsonSerializer.Deserialize<KnowledgeGraphContract>(json, ContractJsonOptions) ??
               throw new InvalidOperationException(ContractJsonParseMessage);
    }

    public string SerializeYaml()
    {
        return ContractYamlSerializer.Serialize(this);
    }

    public static KnowledgeGraphContract LoadYaml(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new ArgumentException(ContractYamlRequiredMessage, nameof(yaml));
        }

        var payload = ContractYamlDeserializer.Deserialize<object>(yaml) ??
                      throw new InvalidOperationException(ContractYamlParseMessage);
        var json = JsonSerializer.Serialize(payload, ContractJsonOptions);
        return LoadJson(json);
    }

    public string GenerateShacl()
    {
        if (!string.IsNullOrWhiteSpace(ShaclShapesTurtle))
        {
            return ShaclShapesTurtle;
        }

        var prefixes = CreateShaclPrefixes(SearchProfile.Prefixes);
        var targetTypes = CreateShaclTargetTypes();
        var properties = CreateShaclProperties().ToArray();
        var builder = new StringBuilder();
        AppendShaclPrefixes(builder, prefixes);

        foreach (var type in targetTypes)
        {
            AppendShape(builder, type, properties, prefixes);
        }

        return builder.ToString();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static Dictionary<string, string> CreateShaclPrefixes(IReadOnlyDictionary<string, string> callerPrefixes)
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ShaclPrefix] = ShaclNamespaceText,
            [SchemaPrefix] = SchemaNamespaceText,
            [KbPrefix] = KbNamespaceText,
            [ProvPrefix] = ProvNamespaceText,
            [RdfPrefix] = RdfNamespaceText,
            [RdfsPrefix] = RdfsNamespaceText,
            [OwlPrefix] = OwlNamespaceText,
            [SkosPrefix] = SkosNamespaceText,
            [XsdPrefix] = XsdNamespaceText,
        };

        foreach (var pair in callerPrefixes)
        {
            prefixes[pair.Key] = pair.Value;
        }

        return prefixes;
    }

    private IReadOnlyList<string> CreateShaclTargetTypes()
    {
        if (SearchProfile.TypeFilters.Count > 0)
        {
            return SearchProfile.TypeFilters;
        }

        return Schema.RdfTypes.Select(static type => type.CompactName).ToArray();
    }

    private IEnumerable<ShaclPropertyShape> CreateShaclProperties()
    {
        foreach (var predicate in SearchProfile.TextPredicates)
        {
            yield return new ShaclPropertyShape(predicate.Predicate, null, Required: true, RequiresIri: false, RequiredPredicateMessage);
        }

        foreach (var filter in SearchProfile.FacetFilters)
        {
            yield return new ShaclPropertyShape(filter.Predicate, filter.Object, Required: true, RequiresIri: true, RequiredFacetMessage);
        }

        foreach (var relationship in SearchProfile.RelationshipPredicates)
        {
            var path = relationship.PredicatePath.Count == 0 ? [relationship.Predicate] : relationship.PredicatePath;
            foreach (var predicate in path)
            {
                yield return new ShaclPropertyShape(predicate, null, Required: false, RequiresIri: true, OptionalResourceMessage);
            }
        }

        foreach (var expansion in SearchProfile.ExpansionPredicates)
        {
            yield return new ShaclPropertyShape(expansion.Predicate, null, Required: false, RequiresIri: true, OptionalResourceMessage);
        }
    }

    private static void AppendShaclPrefixes(StringBuilder builder, IReadOnlyDictionary<string, string> prefixes)
    {
        foreach (var pair in prefixes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder
                .Append(TurtlePrefixStart)
                .Append(pair.Key)
                .Append(TurtlePrefixMiddle)
                .Append(pair.Value)
                .AppendLine(TurtlePrefixEnd);
        }

        builder.AppendLine();
    }

    private static void AppendShape(
        StringBuilder builder,
        string targetType,
        IReadOnlyList<ShaclPropertyShape> properties,
        IReadOnlyDictionary<string, string> prefixes)
    {
        builder
            .Append(LessThanCharacter)
            .Append(ShapeUriPrefix)
            .Append(KnowledgeNaming.Slugify(targetType))
            .AppendLine(ShapeHeader);
        builder
            .Append(TargetClassPrefix)
            .Append(RenderShaclTerm(targetType, prefixes))
            .AppendLine(properties.Count == 0 ? StatementEnd : SpaceText + TurtleSemicolon);

        for (var index = 0; index < properties.Count; index++)
        {
            AppendProperty(builder, properties[index], prefixes, index == properties.Count - 1);
        }

        builder.AppendLine();
    }

    private static void AppendProperty(
        StringBuilder builder,
        ShaclPropertyShape property,
        IReadOnlyDictionary<string, string> prefixes,
        bool last)
    {
        builder.AppendLine(PropertyOpen);
        builder.Append(PathPrefix).Append(RenderShaclTerm(property.Predicate, prefixes)).AppendLine(SpaceText + TurtleSemicolon);
        builder.AppendLine(property.Required ? MinCountRequired : MinCountOptional);

        if (property.RequiresIri)
        {
            builder.AppendLine(NodeKindIri);
        }

        if (property.HasValue is not null)
        {
            builder.Append(HasValuePrefix).Append(RenderShaclTerm(property.HasValue, prefixes)).AppendLine(SpaceText + TurtleSemicolon);
        }

        builder
            .Append(MessagePrefix)
            .Append(EscapeShaclMessage(property.Message))
            .AppendLine(last ? QuoteText : QuoteText + TurtleSemicolon);
        builder.AppendLine(last ? PropertyCloseEnd : PropertyCloseContinue);
    }

    private static string RenderShaclTerm(string value, IReadOnlyDictionary<string, string> prefixes)
    {
        var separatorIndex = value.IndexOf(Colon, StringComparison.Ordinal);
        if (separatorIndex > 0 &&
            prefixes.ContainsKey(value[..separatorIndex]) &&
            !value.Contains(UriAuthoritySeparator, StringComparison.Ordinal))
        {
            return value;
        }

        foreach (var pair in prefixes.OrderByDescending(static pair => pair.Value.Length))
        {
            if (value.StartsWith(pair.Value, StringComparison.Ordinal))
            {
                return pair.Key + Colon + value[pair.Value.Length..];
            }
        }

        return TurtleTermOpen + value + TurtleTermClose;
    }

    private static string EscapeShaclMessage(string value)
    {
        return value.Replace(QuoteText, QuoteEscape, StringComparison.Ordinal);
    }

    private sealed record ShaclPropertyShape(
        string Predicate,
        string? HasValue,
        bool Required,
        bool RequiresIri,
        string Message);
}
