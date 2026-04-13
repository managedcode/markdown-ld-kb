using System.Globalization;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TiktokenKnowledgeGraphExtractor
{
    private readonly Uri _baseUri;
    private readonly TiktokenKnowledgeGraphOptions _options;
    private readonly TokenVectorizer _vectorizer;
    private readonly TokenKeyphraseExtractor _topicExtractor;
    private readonly TokenizedEntityHintExtractor _entityHintExtractor;

    public TiktokenKnowledgeGraphExtractor(Uri? baseUri = null, TiktokenKnowledgeGraphOptions? options = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
        _options = ValidateOptions(options ?? new TiktokenKnowledgeGraphOptions());
        _vectorizer = new TokenVectorizer(_options.ModelName);
        _topicExtractor = new TokenKeyphraseExtractor(_baseUri, _options);
        _entityHintExtractor = new TokenizedEntityHintExtractor(_baseUri);
    }

    public TokenizedKnowledgeExtractionResult Extract(IReadOnlyList<MarkdownDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var sections = documents.SelectMany(BuildSections).ToArray();
        var candidates = documents.SelectMany(BuildSegmentCandidates).ToArray();
        var vectorSpace = TokenVectorSpace.Fit(candidates.Select(static candidate => candidate.TokenIds).ToArray(), _options.Weighting);
        var segments = candidates.Select(candidate => CreateSegment(candidate, vectorSpace)).ToArray();
        var topics = _topicExtractor.Extract(candidates);
        var entityHints = _entityHintExtractor.Extract(documents);
        var relations = BuildRelations(segments).ToArray();
        var facts = TokenizedKnowledgeFactFactory.Build(sections, segments, topics, entityHints, relations);
        return new TokenizedKnowledgeExtractionResult(facts, segments, vectorSpace);
    }

    internal TokenizedKnowledgeIndex CreateIndex(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        TokenVectorSpace vectorSpace)
    {
        return new TokenizedKnowledgeIndex(_vectorizer, vectorSpace, segments);
    }

    private static TiktokenKnowledgeGraphOptions ValidateOptions(TiktokenKnowledgeGraphOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRelatedSegments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MinimumTokenCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumRelatedDistance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxTopicLabelsPerSegment);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxTopicPhraseWords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MinimumTopicWordLength);
        return options;
    }

    private IEnumerable<TokenizedKnowledgeSection> BuildSections(MarkdownDocument document)
    {
        for (var index = 0; index < document.Sections.Count; index++)
        {
            var section = document.Sections[index];
            if (string.IsNullOrWhiteSpace(section.Text))
            {
                continue;
            }

            yield return new TokenizedKnowledgeSection(
                CreateSectionId(document, index),
                document.DocumentUri.AbsoluteUri,
                ResolveSectionLabel(document, section));
        }
    }

    private IEnumerable<TokenizedSegmentCandidate> BuildSegmentCandidates(MarkdownDocument document)
    {
        var order = 0;
        for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];
            var parentId = CreateSectionId(document, sectionIndex);

            foreach (var text in SplitSegmentBlocks(section.Text))
            {
                var tokenIds = _vectorizer.Tokenize(text);
                if (tokenIds.Count < _options.MinimumTokenCount)
                {
                    continue;
                }

                yield return new TokenizedSegmentCandidate(
                    CreateSegmentId(document, order),
                    document.DocumentUri.AbsoluteUri,
                    parentId,
                    text,
                    order,
                    tokenIds);
                order++;
            }
        }
    }

    private static IEnumerable<string> SplitSegmentBlocks(string text)
    {
        foreach (var paragraph in text.Split(DoubleNewLineDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lines = paragraph.Split(NewLineDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length > 1)
            {
                foreach (var line in lines)
                {
                    yield return line;
                }
            }
            else if (lines.Length == 1)
            {
                yield return lines[0];
            }
        }
    }

    private IEnumerable<TokenizedKnowledgeRelation> BuildRelations(IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        foreach (var segment in segments)
        {
            foreach (var related in FindRelatedSegments(segment, segments))
            {
                yield return new TokenizedKnowledgeRelation(segment.Id, related.Segment.Id, related.Distance);
            }
        }
    }

    private IEnumerable<(TokenizedKnowledgeSegment Segment, double Distance)> FindRelatedSegments(
        TokenizedKnowledgeSegment source,
        IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        return segments
            .Where(segment => segment.Id != source.Id)
            .Select(segment => (Segment: segment, Distance: source.Vector.EuclideanDistanceTo(segment.Vector)))
            .Where(related => related.Distance <= _options.MaximumRelatedDistance)
            .OrderBy(related => related.Distance)
            .ThenBy(related => related.Segment.Id, StringComparer.Ordinal)
            .Take(_options.MaxRelatedSegments);
    }

    private static TokenizedKnowledgeSegment CreateSegment(
        TokenizedSegmentCandidate candidate,
        TokenVectorSpace vectorSpace)
    {
        return new TokenizedKnowledgeSegment(
            candidate.Id,
            candidate.DocumentId,
            candidate.ParentId,
            candidate.Text,
            candidate.LineNumber,
            vectorSpace.CreateVector(candidate.TokenIds));
    }

    private static string ResolveSectionLabel(MarkdownDocument document, MarkdownSection section)
    {
        if (!string.IsNullOrWhiteSpace(section.Heading))
        {
            return section.Heading;
        }

        return !string.IsNullOrWhiteSpace(document.Title)
            ? document.Title
            : KnowledgeNaming.NormalizeSourcePath(document.SourcePath);
    }

    private string CreateSegmentId(MarkdownDocument document, int lineIndex)
    {
        var slug = KnowledgeNaming.Slugify(document.SourcePath);
        var line = lineIndex.ToString(CultureInfo.InvariantCulture);
        return new Uri(_baseUri, TokenSegmentIdPrefix + slug + Hyphen + line).AbsoluteUri;
    }

    private string CreateSectionId(MarkdownDocument document, int sectionIndex)
    {
        var slug = KnowledgeNaming.Slugify(document.SourcePath);
        var section = sectionIndex.ToString(CultureInfo.InvariantCulture);
        return new Uri(_baseUri, TokenSectionIdPrefix + slug + Hyphen + section).AbsoluteUri;
    }
}

internal sealed record TokenizedKnowledgeExtractionResult(
    KnowledgeExtractionResult Facts,
    IReadOnlyList<TokenizedKnowledgeSegment> Segments,
    TokenVectorSpace VectorSpace);
