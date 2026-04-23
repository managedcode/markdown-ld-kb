namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphDocumentMaterialization
{
    public static bool ShouldMaterialize(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Sections.Count != 0 ||
               document.FrontMatter.Count != 0 ||
               !string.IsNullOrWhiteSpace(document.Body);
    }
}
