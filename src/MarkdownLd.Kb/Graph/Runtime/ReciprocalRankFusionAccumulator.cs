namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class ReciprocalRankFusionAccumulator(KnowledgeGraphRankedSearchMatch initialMatch)
{
    private KnowledgeGraphRankedSearchMatch _match = initialMatch;

    public double Score { get; private set; }

    public double? CanonicalScore { get; private set; }

    public double? SemanticScore { get; private set; }

    public bool HasCanonical { get; private set; }

    public bool HasSemantic { get; private set; }

    public void Add(KnowledgeGraphRankedSearchMatch match, double reciprocalRankScore, bool isCanonical)
    {
        Score += reciprocalRankScore;
        if (isCanonical)
        {
            HasCanonical = true;
            CanonicalScore = match.CanonicalScore ?? match.Score;
            _match = match;
            return;
        }

        HasSemantic = true;
        SemanticScore = match.SemanticScore ?? match.Score;
        if (!HasCanonical)
        {
            _match = match;
        }
    }

    public KnowledgeGraphRankedSearchMatch ToMatch()
    {
        return _match with
        {
            Source = ResolveSource(),
            Score = Score,
            CanonicalScore = CanonicalScore,
            SemanticScore = SemanticScore,
        };
    }

    private KnowledgeGraphRankedSearchSource ResolveSource()
    {
        if (HasCanonical && HasSemantic)
        {
            return KnowledgeGraphRankedSearchSource.Merged;
        }

        return HasCanonical
            ? KnowledgeGraphRankedSearchSource.Canonical
            : KnowledgeGraphRankedSearchSource.Semantic;
    }
}
