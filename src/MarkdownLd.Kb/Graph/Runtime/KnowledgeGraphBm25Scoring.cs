using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBm25Scoring
{
    private const double K1 = 1.2d;
    private const double B = 0.75d;
    private const double Half = 0.5d;
    private const double IdfOffset = 1d;

    public static double ScoreTerm(
        int documentLength,
        double frequency,
        int documentFrequency,
        int documentCount,
        double averageDocumentLength)
    {
        if (documentFrequency == 0 || documentLength == 0 || frequency <= ZeroConfidence)
        {
            return ZeroConfidence;
        }

        var idf = Math.Log(IdfOffset + ((documentCount - documentFrequency + Half) / (documentFrequency + Half)));
        var denominator = frequency + K1 * (IdfOffset - B + (B * documentLength / averageDocumentLength));
        return idf * ((frequency * (K1 + IdfOffset)) / denominator);
    }
}
