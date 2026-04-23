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
        var conceptUris = CollectConceptUris(graph);
        conceptUris.Remove(conceptSchemeUri);
        var conceptLabels = CollectConceptLabels(graph);
        var definedTerms = CollectTypedSubjects(graph, SchemaDefinedTermUri);
        var sameAsTargets = CollectUriTargets(graph, SchemaSameAsUri);

        foreach (var conceptUri in conceptUris)
        {
            var conceptNode = graph.CreateUriNode(conceptUri);
            _ = new SkosConcept(conceptNode, graph);
            AssertConcept(graph, conceptNode, schemeNode, includeOntologyTypes && ShouldApplyOntologyConceptType(conceptUri, definedTerms, conceptLabels));
            AssertConceptLabels(graph, conceptNode, conceptLabels, aboutLabels);
            MirrorSchemaSameAsAsSkosExactMatch(graph, conceptNode, sameAsTargets);
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

    private static bool ShouldApplyOntologyConceptType(
        Uri conceptUri,
        IReadOnlySet<Uri> definedTerms,
        IReadOnlyDictionary<Uri, string> labels)
    {
        return definedTerms.Contains(conceptUri) || labels.ContainsKey(conceptUri);
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

    private static HashSet<Uri> CollectTypedSubjects(Graph graph, Uri typeUri)
    {
        var subjects = new HashSet<Uri>();
        foreach (var triple in graph.Triples.Where(static triple => triple.Predicate is IUriNode predicateNode && predicateNode.Uri == RdfTypeUri))
        {
            if (triple.Subject is IUriNode subjectNode &&
                triple.Object is IUriNode objectNode &&
                objectNode.Uri == typeUri)
            {
                subjects.Add(subjectNode.Uri);
            }
        }

        return subjects;
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

    private static void AssertConceptLabels(
        Graph graph,
        IUriNode conceptNode,
        IReadOnlyDictionary<Uri, string> conceptLabels,
        IReadOnlyDictionary<Uri, string> aboutLabels)
    {
        var existingLabel = conceptLabels.GetValueOrDefault(conceptNode.Uri);
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

    private static Dictionary<Uri, string> CollectConceptLabels(Graph graph)
    {
        var labels = new Dictionary<Uri, LabelCandidate>();
        foreach (var triple in graph.Triples)
        {
            if (triple.Subject is not IUriNode subjectNode ||
                triple.Predicate is not IUriNode predicateNode ||
                triple.Object is not ILiteralNode literalNode)
            {
                continue;
            }

            var priority = GetLabelPriority(predicateNode.Uri);
            if (priority == 0)
            {
                continue;
            }

            var candidate = new LabelCandidate(literalNode.Value, priority);
            if (!labels.TryGetValue(subjectNode.Uri, out var existing) || candidate.Priority > existing.Priority)
            {
                labels[subjectNode.Uri] = candidate;
            }
        }

        return labels.ToDictionary(static pair => pair.Key, static pair => pair.Value.Value);
    }

    private static int GetLabelPriority(Uri predicateUri)
    {
        if (predicateUri == SchemaNameUri)
        {
            return 3;
        }

        if (predicateUri == SkosPrefLabelUri)
        {
            return 2;
        }

        return predicateUri == RdfsLabelUri ? 1 : 0;
    }

    private static Dictionary<Uri, IReadOnlyList<IUriNode>> CollectUriTargets(Graph graph, Uri predicateUri)
    {
        var targets = new Dictionary<Uri, List<IUriNode>>();
        foreach (var triple in graph.Triples.Where(triple => triple.Predicate is IUriNode predicateNode && predicateNode.Uri == predicateUri))
        {
            if (triple.Subject is not IUriNode subjectNode || triple.Object is not IUriNode objectNode)
            {
                continue;
            }

            if (!targets.TryGetValue(subjectNode.Uri, out var subjectTargets))
            {
                subjectTargets = [];
                targets[subjectNode.Uri] = subjectTargets;
            }

            subjectTargets.Add(objectNode);
        }

        return targets.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<IUriNode>)pair.Value);
    }

    private static void MirrorSchemaSameAsAsSkosExactMatch(
        Graph graph,
        IUriNode conceptNode,
        IReadOnlyDictionary<Uri, IReadOnlyList<IUriNode>> sameAsTargets)
    {
        if (!sameAsTargets.TryGetValue(conceptNode.Uri, out var targets))
        {
            return;
        }

        foreach (var target in targets)
        {
            graph.Assert(new Triple(conceptNode, graph.CreateUriNode(SkosExactMatchUri), target));
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

    private readonly record struct LabelCandidate(string Value, int Priority);
}
