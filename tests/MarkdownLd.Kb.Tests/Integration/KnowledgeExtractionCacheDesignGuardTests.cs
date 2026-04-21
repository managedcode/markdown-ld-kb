using System.Reflection;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeExtractionCacheDesignGuardTests
{
    [Test]
    public void Cache_types_do_not_define_non_literal_static_fields()
    {
        var cacheNamespace = typeof(FileKnowledgeExtractionCache).Namespace;
        var cacheTypes = typeof(FileKnowledgeExtractionCache).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == cacheNamespace &&
                (typeof(IKnowledgeExtractionCache).IsAssignableFrom(type) ||
                 type.Name.StartsWith("KnowledgeExtractionCache", StringComparison.Ordinal) ||
                 type == typeof(KnowledgeExtractionChunkFingerprint)))
            .ToArray();

        var offenders = cacheTypes
            .SelectMany(type => type
                .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsLiteral)
                .Select(field => string.Concat(type.FullName, ".", field.Name)))
            .ToArray();

        offenders.ShouldBeEmpty();
    }
}
