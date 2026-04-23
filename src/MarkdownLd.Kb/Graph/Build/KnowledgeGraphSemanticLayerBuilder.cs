using VDS.RDF;
using VDS.RDF.Ontology;
using VDS.RDF.Skos;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeGraphSemanticLayerBuilder(Uri baseUri)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);

    public void Apply(
        Graph graph,
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeGraphBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);
        if (graph.Triples.Count == 0)
        {
            return;
        }

        var semanticOptions = options.SemanticLayers;
        if (semanticOptions.IncludeOntologyLayer)
        {
            AddOntologyLayer(graph);
            ApplyDocumentAndAssertionTypes(graph, documents);
        }

        if (semanticOptions.IncludeSkosLayer)
        {
            AddSkosLayer(graph, documents, semanticOptions, semanticOptions.IncludeOntologyLayer);
        }
    }

    private void AddOntologyLayer(Graph graph)
    {
        var ontologyGraph = new OntologyGraph(_baseUri);
        KnowledgeGraphNamespaces.Register(ontologyGraph);
        AssertOntologyHeader(ontologyGraph);
        DeclareOntologyClasses(ontologyGraph);
        DeclareOntologyProperties(ontologyGraph);
        graph.Merge(ontologyGraph);
    }

    private void AssertOntologyHeader(OntologyGraph ontologyGraph)
    {
        var ontologyNode = ontologyGraph.CreateUriNode(_baseUri);
        ontologyGraph.Assert(new Triple(ontologyNode, ontologyGraph.CreateUriNode(RdfTypeUri), ontologyGraph.CreateUriNode(OwlOntologyUri)));
        ontologyGraph.Assert(new Triple(ontologyNode, ontologyGraph.CreateUriNode(RdfsLabelUri), ontologyGraph.CreateLiteralNode(DefaultOntologyLabel)));
        ontologyGraph.Assert(new Triple(ontologyNode, ontologyGraph.CreateUriNode(RdfsCommentUri), ontologyGraph.CreateLiteralNode(DefaultOntologyComment)));
    }

    private static void DeclareOntologyClasses(OntologyGraph ontologyGraph)
    {
        DeclareOntologyClass(ontologyGraph, KbMarkdownDocumentUri, SchemaArticleUri);
        DeclareOntologyClass(ontologyGraph, KbKnowledgeConceptUri, SchemaDefinedTermUri, SkosConceptUri);
        DeclareOntologyClass(ontologyGraph, KbKnowledgeConceptSchemeUri, SkosConceptSchemeUri);
        DeclareOntologyClass(ontologyGraph, KbKnowledgeAssertionUri, RdfStatementUri);
    }

    private static void DeclareOntologyProperties(OntologyGraph ontologyGraph)
    {
        DeclareOntologyProperty(ontologyGraph, KbRelatedToText, SchemaThingUri, SchemaThingUri);
        DeclareOntologyProperty(ontologyGraph, KbMemberOfText, SchemaThingUri, KbKnowledgeConceptUri);
        DeclareOntologyProperty(ontologyGraph, KbNextStepText, SchemaThingUri, SchemaThingUri);
        DeclareOntologyProperty(ontologyGraph, KbConfidenceText, RdfStatementUri, XsdDecimalUri);
        DeclareOntologyProperty(ontologyGraph, KbEntryTypeText, KbMarkdownDocumentUri, RdfsLiteralUri);
        DeclareOntologyProperty(ontologyGraph, KbSourceProjectText, KbMarkdownDocumentUri, RdfsLiteralUri);
    }

    private static void DeclareOntologyClass(
        OntologyGraph ontologyGraph,
        Uri classUri,
        params Uri[] superClasses)
    {
        var ontologyClass = ontologyGraph.CreateOntologyClass(classUri);
        foreach (var superClass in superClasses)
        {
            ontologyClass.AddSuperClass(superClass);
        }

        AssertResourceLabel(ontologyGraph, classUri);
    }

    private static void DeclareOntologyProperty(
        OntologyGraph ontologyGraph,
        string propertyText,
        Uri domainUri,
        Uri rangeUri)
    {
        var propertyUri = new Uri(propertyText);
        var ontologyProperty = ontologyGraph.CreateOntologyProperty(propertyUri);
        ontologyProperty.AddDomain(domainUri);
        ontologyProperty.AddRange(rangeUri);
        AssertResourceLabel(ontologyGraph, propertyUri);
    }

    private static void ApplyDocumentAndAssertionTypes(Graph graph, IReadOnlyList<MarkdownDocument> documents)
    {
        foreach (var document in documents)
        {
            if (!KnowledgeGraphDocumentMaterialization.ShouldMaterialize(document))
            {
                continue;
            }

            graph.Assert(new Triple(graph.CreateUriNode(document.DocumentUri), graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(KbMarkdownDocumentUri)));
        }

        var statementNodes = graph.Triples
            .Where(static triple => triple.Predicate is IUriNode predicateNode &&
                                    triple.Object is IUriNode objectNode &&
                                    predicateNode.Uri == RdfTypeUri &&
                                    objectNode.Uri == RdfStatementUri)
            .Select(static triple => triple.Subject)
            .Distinct()
            .ToArray();

        foreach (var statementNode in statementNodes)
        {
            graph.Assert(new Triple(statementNode, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(KbKnowledgeAssertionUri)));
        }
    }

    private void AddSkosLayer(
        Graph graph,
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeGraphSemanticLayerOptions options,
        bool includeOntologyTypes)
    {
        _ = new SkosGraph(graph);
        var conceptSchemeUri = ResolveConceptSchemeUri(options);
        var schemeNode = graph.CreateUriNode(conceptSchemeUri);
        _ = new SkosConceptScheme(schemeNode, graph);

        AssertConceptScheme(graph, schemeNode, options.ConceptSchemeLabel, includeOntologyTypes);

        var aboutLabels = CollectAboutLabels(documents);
        foreach (var conceptUri in CollectConceptUris(graph))
        {
            var conceptNode = graph.CreateUriNode(conceptUri);
            _ = new SkosConcept(conceptNode, graph);
            AssertConcept(graph, conceptNode, schemeNode, includeOntologyTypes && ShouldApplyOntologyConceptType(graph, conceptNode));
            AssertConceptLabels(graph, conceptNode, aboutLabels);
            MirrorSchemaSameAsAsSkosExactMatch(graph, conceptNode);
        }
    }

    private Uri ResolveConceptSchemeUri(KnowledgeGraphSemanticLayerOptions options)
    {
        if (Uri.TryCreate(options.ConceptSchemeId, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        var schemeKey = string.IsNullOrWhiteSpace(options.ConceptSchemeId)
            ? options.ConceptSchemeLabel
            : options.ConceptSchemeId;
        return new Uri(KnowledgeNaming.CreateEntityId(_baseUri, schemeKey!));
    }

    private static void AssertConceptScheme(
        Graph graph,
        IUriNode schemeNode,
        string label,
        bool includeOntologyTypes)
    {
        graph.Assert(new Triple(schemeNode, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(SkosConceptSchemeUri)));
        if (includeOntologyTypes)
        {
            graph.Assert(new Triple(schemeNode, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(KbKnowledgeConceptSchemeUri)));
        }

        graph.Assert(new Triple(schemeNode, graph.CreateUriNode(SkosPrefLabelUri), graph.CreateLiteralNode(label)));
        graph.Assert(new Triple(schemeNode, graph.CreateUriNode(RdfsLabelUri), graph.CreateLiteralNode(label)));
    }

    private static void AssertConcept(
        Graph graph,
        IUriNode conceptNode,
        IUriNode schemeNode,
        bool includeOntologyTypes)
    {
        graph.Assert(new Triple(conceptNode, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(SkosConceptUri)));
        if (includeOntologyTypes)
        {
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(KbKnowledgeConceptUri)));
        }

        graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosInSchemeUri), schemeNode));
        graph.Assert(new Triple(schemeNode, graph.CreateUriNode(SkosHasTopConceptUri), conceptNode));
        graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosTopConceptOfUri), schemeNode));
    }

    private static bool ShouldApplyOntologyConceptType(Graph graph, IUriNode conceptNode)
    {
        return HasType(graph, conceptNode, SchemaDefinedTermUri) ||
               TryGetLiteral(graph, conceptNode, SchemaNameUri) is not null;
    }

    private static HashSet<Uri> CollectConceptUris(Graph graph)
    {
        var conceptUris = new HashSet<Uri>();

        CollectTypedConceptUris(graph, conceptUris);
        CollectReferencedConceptUris(graph, conceptUris, SchemaAboutUri);
        CollectReferencedConceptUris(graph, conceptUris, KbMemberOfUri);
        return conceptUris;
    }

    private static void CollectTypedConceptUris(Graph graph, ISet<Uri> conceptUris)
    {
        foreach (var triple in graph.Triples.Where(static triple => triple.Subject is IUriNode && triple.Predicate is IUriNode predicateNode && predicateNode.Uri == RdfTypeUri && triple.Object is IUriNode))
        {
            if (triple.Subject is not IUriNode subjectNode || triple.Object is not IUriNode objectNode)
            {
                continue;
            }

            if (objectNode.Uri == SchemaDefinedTermUri || objectNode.Uri == SkosConceptUri || objectNode.Uri == KbKnowledgeConceptUri)
            {
                conceptUris.Add(subjectNode.Uri);
            }
        }
    }

    private static bool HasType(Graph graph, IUriNode subject, Uri typeUri)
    {
        return graph.Triples.Any(triple => triple.Subject.Equals(subject) &&
                                          triple.Predicate is IUriNode predicateNode &&
                                          predicateNode.Uri == RdfTypeUri &&
                                          triple.Object is IUriNode objectNode &&
                                          objectNode.Uri == typeUri);
    }

    private static void CollectReferencedConceptUris(Graph graph, ISet<Uri> conceptUris, Uri predicateUri)
    {
        foreach (var triple in graph.Triples.Where(triple => triple.Predicate is IUriNode predicateNode && predicateNode.Uri == predicateUri && triple.Object is IUriNode))
        {
            conceptUris.Add(((IUriNode)triple.Object).Uri);
        }
    }

    private Dictionary<Uri, string> CollectAboutLabels(IReadOnlyList<MarkdownDocument> documents)
    {
        var labels = new Dictionary<Uri, string>();
        foreach (var document in documents)
        {
            foreach (var about in ReadFrontMatterValues(document.FrontMatter, AboutKey))
            {
                labels[new Uri(KnowledgeNaming.CreateEntityId(_baseUri, about))] = about;
            }
        }

        return labels;
    }

    private static IEnumerable<string> ReadFrontMatterValues(IReadOnlyDictionary<string, object?> frontMatter, string key)
    {
        foreach (var entry in frontMatter)
        {
            if (!entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is IEnumerable<object?> values && entry.Value is not string)
            {
                foreach (var value in values)
                {
                    var text = value?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return text;
                    }
                }

                continue;
            }

            var singleValue = entry.Value?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(singleValue))
            {
                yield return singleValue;
            }
        }
    }

    private static void AssertConceptLabels(Graph graph, IUriNode conceptNode, IReadOnlyDictionary<Uri, string> aboutLabels)
    {
        var existingLabel = TryGetLiteral(graph, conceptNode, SchemaNameUri)
                            ?? TryGetLiteral(graph, conceptNode, SkosPrefLabelUri)
                            ?? TryGetLiteral(graph, conceptNode, RdfsLabelUri);
        if (existingLabel is not null)
        {
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosPrefLabelUri), graph.CreateLiteralNode(existingLabel)));
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(RdfsLabelUri), graph.CreateLiteralNode(existingLabel)));
            return;
        }

        if (aboutLabels.TryGetValue(conceptNode.Uri, out var authoredLabel))
        {
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosPrefLabelUri), graph.CreateLiteralNode(authoredLabel)));
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(RdfsLabelUri), graph.CreateLiteralNode(authoredLabel)));
        }
    }

    private static string? TryGetLiteral(Graph graph, IUriNode subject, Uri predicateUri)
    {
        return graph.Triples
            .Where(triple => triple.Subject.Equals(subject) && triple.Predicate is IUriNode predicateNode && predicateNode.Uri == predicateUri && triple.Object is ILiteralNode)
            .Select(triple => ((ILiteralNode)triple.Object).Value)
            .FirstOrDefault();
    }

    private static void MirrorSchemaSameAsAsSkosExactMatch(Graph graph, IUriNode conceptNode)
    {
        var sameAsTriples = graph.Triples
            .Where(triple => triple.Subject.Equals(conceptNode) &&
                             triple.Predicate is IUriNode predicateNode &&
                             predicateNode.Uri == SchemaSameAsUri &&
                             triple.Object is IUriNode)
            .ToArray();
        foreach (var triple in sameAsTriples)
        {
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosExactMatchUri), triple.Object));
        }
    }

    private static void AssertResourceLabel(IGraph graph, Uri resourceUri)
    {
        var label = GetResourceLabel(resourceUri);
        graph.Assert(new Triple(graph.CreateUriNode(resourceUri), graph.CreateUriNode(RdfsLabelUri), graph.CreateLiteralNode(label)));
    }

    private static string GetResourceLabel(Uri resourceUri)
    {
        if (!string.IsNullOrWhiteSpace(resourceUri.Fragment))
        {
            return resourceUri.Fragment.TrimStart('#');
        }

        var lastSegment = resourceUri.Segments[^1].TrimEnd('/');
        return string.IsNullOrWhiteSpace(lastSegment)
            ? resourceUri.AbsoluteUri
            : lastSegment;
    }
}
