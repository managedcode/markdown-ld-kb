using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphNormalizationContractTests
{
    [Test]
    [Arguments("mentions", "https://schema.org/mentions")]
    [Arguments("about", "https://schema.org/about")]
    [Arguments("author", "https://schema.org/author")]
    [Arguments("creator", "https://schema.org/creator")]
    [Arguments("haspart", "https://schema.org/hasPart")]
    [Arguments("sameas", "https://schema.org/sameAs")]
    [Arguments("relatedto", "urn:managedcode:markdown-ld-kb:vocab:relatedTo")]
    [Arguments("memberof", "urn:managedcode:markdown-ld-kb:vocab:memberOf")]
    [Arguments("nextstep", "urn:managedcode:markdown-ld-kb:vocab:nextStep")]
    [Arguments("schema:name", "https://schema.org/name")]
    [Arguments("kb:nextStep", "urn:managedcode:markdown-ld-kb:vocab:nextStep")]
    [Arguments("prov:wasDerivedFrom", "http://www.w3.org/ns/prov#wasDerivedFrom")]
    [Arguments("rdf:type", "http://www.w3.org/1999/02/22-rdf-syntax-ns#type")]
    [Arguments("rdfs:label", "http://www.w3.org/2000/01/rdf-schema#label")]
    [Arguments("owl:Class", "http://www.w3.org/2002/07/owl#Class")]
    [Arguments("skos:prefLabel", "http://www.w3.org/2004/02/skos/core#prefLabel")]
    [Arguments("xsd:decimal", "http://www.w3.org/2001/XMLSchema#decimal")]
    [Arguments("https://example.com/predicate", "https://example.com/predicate")]
    public void Predicate_resolver_supports_every_builtin_and_absolute_iri(
        string predicate,
        string expected)
    {
        KnowledgeGraphPredicateResolver.Resolve(predicate)!.AbsoluteUri.ShouldBe(expected);
    }

    [Test]
    public void Predicate_and_cycle_contracts_reject_invalid_options()
    {
        KnowledgeGraphPredicateResolver.Resolve("unsupported predicate").ShouldBeNull();
        KnowledgeGraphNormalizationReport.Empty.HasWarnings.ShouldBeFalse();

        using var graph = KnowledgeGraph.FromSnapshot(KnowledgeGraphSnapshot.Empty);
        Should.Throw<ArgumentException>(() => graph.FindCycles(new KnowledgeGraphCycleSearchOptions
        {
            PredicateIds = ["unsupported predicate"],
        }));
        Should.Throw<ArgumentOutOfRangeException>(() => graph.FindCycles(new KnowledgeGraphCycleSearchOptions
        {
            MaxComponents = 0,
        }));
    }
}
