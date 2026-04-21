using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Shacl;
using VDS.RDF.Shacl.Validation;
using VDS.RDF.Writing;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using ShaclPath = VDS.RDF.Shacl.Path;
using StringWriter = System.IO.StringWriter;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public KnowledgeGraphShaclValidationReport ValidateShacl(string? shapesTurtle = null)
    {
        var shapes = ParseShapes(shapesTurtle ?? KnowledgeGraphShaclShapes.DefaultTurtle);
        var shapesGraph = new ShapesGraph(shapes);

        _graphLock.EnterReadLock();
        try
        {
            var report = shapesGraph.Validate(_graph);
            return CreateValidationReport(report);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private static Graph ParseShapes(string shapesTurtle)
    {
        var graph = new Graph();
        RegisterValidationNamespaces(graph);
        new TurtleParser().Load(graph, new StringReader(shapesTurtle));
        return graph;
    }

    private static void RegisterValidationNamespaces(IGraph graph)
    {
        graph.NamespaceMap.AddNamespace(SchemaPrefix, SchemaNamespaceUri);
        graph.NamespaceMap.AddNamespace(KbPrefix, KbNamespaceUri);
        graph.NamespaceMap.AddNamespace(ProvPrefix, ProvNamespaceUri);
        graph.NamespaceMap.AddNamespace(RdfPrefix, RdfNamespaceUri);
        graph.NamespaceMap.AddNamespace(XsdPrefix, XsdNamespaceUri);
    }

    private static KnowledgeGraphShaclValidationReport CreateValidationReport(Report report)
    {
        return new KnowledgeGraphShaclValidationReport(
            report.Conforms,
            report.Results.Select(CreateValidationIssue).ToArray(),
            SerializeValidationReport(report));
    }

    private static KnowledgeGraphShaclValidationIssue CreateValidationIssue(Result result)
    {
        return new KnowledgeGraphShaclValidationIssue(
            RenderValidationNode(result.Severity),
            RenderValidationNode(result.FocusNode),
            RenderValidationNode(result.ResultValue),
            RenderValidationNode(result.SourceShape),
            RenderValidationPath(result.ResultPath),
            RenderValidationNode(result.SourceConstraintComponent),
            RenderValidationNode(result.Message));
    }

    private static string SerializeValidationReport(Report report)
    {
        using var writer = new StringWriter();
        var turtleWriter = new CompressingTurtleWriter();
        turtleWriter.Save(report.Normalised, writer);
        return writer.ToString();
    }

    private static string RenderValidationPath(ShaclPath? path)
    {
        return path?.ToString() ?? string.Empty;
    }

    private static string RenderValidationNode(INode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var rendered = node.ToString()
            .Trim(LessThanCharacter, GreaterThanCharacter)
            .Trim(DoubleQuoteCharacter);
        var datatypeIndex = rendered.IndexOf(LiteralDatatypeSeparator, StringComparison.Ordinal);
        return datatypeIndex > 0
            ? rendered[..datatypeIndex].Trim(DoubleQuoteCharacter)
            : rendered;
    }
}
