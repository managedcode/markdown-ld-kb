using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraphBuilder
{
    private static void AddAssertion(
        KnowledgeGraphMaterializationContext context,
        KnowledgeAssertionFact assertion,
        bool includeAssertionReification)
    {
        if (!Uri.TryCreate(assertion.SubjectId, UriKind.Absolute, out var subjectUri) ||
            !Uri.TryCreate(assertion.ObjectId, UriKind.Absolute, out var objectUri) ||
            string.IsNullOrWhiteSpace(assertion.Predicate))
        {
            return;
        }

        var predicateUri = context.ResolvePredicateUri(assertion.Predicate);
        if (predicateUri is null)
        {
            return;
        }

        var graph = context.Graph;
        var subject = context.UriNode(subjectUri);
        graph.Assert(new Triple(subject, context.UriNode(predicateUri), context.UriNode(objectUri)));
        if (includeAssertionReification)
        {
            AddReifiedAssertion(context, subjectUri, predicateUri, objectUri, assertion);
        }

        AddSourceTriples(context, subject, context.UriNode(ProvWasDerivedFromUri), assertion);
    }

    private static void AddReifiedAssertion(
        KnowledgeGraphMaterializationContext context,
        Uri subjectUri,
        Uri predicateUri,
        Uri objectUri,
        KnowledgeAssertionFact assertion)
    {
        var graph = context.Graph;
        var statement = graph.CreateBlankNode();
        graph.Assert(new Triple(statement, context.UriNode(RdfTypeUri), context.UriNode(RdfStatementUri)));
        graph.Assert(new Triple(statement, context.UriNode(RdfSubjectUri), context.UriNode(subjectUri)));
        graph.Assert(new Triple(statement, context.UriNode(RdfPredicateUri), context.UriNode(predicateUri)));
        graph.Assert(new Triple(statement, context.UriNode(RdfObjectUri), context.UriNode(objectUri)));
        graph.Assert(new Triple(statement, context.UriNode(KbConfidenceUri), context.ConfidenceLiteral(assertion.Confidence)));
        AddSourceTriples(context, statement, context.UriNode(ProvWasDerivedFromUri), assertion);
    }

    private static void AddSourceTriple(
        KnowledgeGraphMaterializationContext context,
        INode subject,
        INode predicate,
        string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        context.Graph.Assert(new Triple(subject, predicate, context.UriOrLiteralNode(source)));
    }

    private static void AddSourceTriples(
        KnowledgeGraphMaterializationContext context,
        INode subject,
        INode predicate,
        KnowledgeEntityFact entity)
    {
        foreach (var source in KnowledgeFactSourceCollector.EnumerateEntitySources(entity).Distinct(StringComparer.Ordinal))
        {
            AddSourceTriple(context, subject, predicate, source);
        }
    }

    private static void AddSourceTriples(
        KnowledgeGraphMaterializationContext context,
        INode subject,
        INode predicate,
        KnowledgeAssertionFact assertion)
    {
        foreach (var source in KnowledgeFactSourceCollector.EnumerateAssertionSources(assertion).Distinct(StringComparer.Ordinal))
        {
            AddSourceTriple(context, subject, predicate, source);
        }
    }
}
