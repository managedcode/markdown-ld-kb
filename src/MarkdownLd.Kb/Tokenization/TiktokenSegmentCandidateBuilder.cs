using System.Globalization;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TiktokenSegmentCandidateBuilder
{
    private readonly Uri _baseUri;
    private readonly TiktokenKnowledgeGraphOptions _options;
    private readonly TokenVectorizer _vectorizer;

    public TiktokenSegmentCandidateBuilder(
        Uri baseUri,
        TiktokenKnowledgeGraphOptions options,
        TokenVectorizer vectorizer)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(vectorizer);

        _baseUri = baseUri;
        _options = options;
        _vectorizer = vectorizer;
    }

    public TokenizedKnowledgeSection[] BuildSections(IReadOnlyList<MarkdownDocument> documents)
    {
        var sections = new List<TokenizedKnowledgeSection>();
        foreach (var document in documents)
        {
            AddSections(sections, document);
        }

        return sections.ToArray();
    }

    public TokenizedSegmentCandidate[] BuildSegmentCandidates(IReadOnlyList<MarkdownDocument> documents)
    {
        var candidates = new List<TokenizedSegmentCandidate>();
        foreach (var document in documents)
        {
            var order = 0;
            for (var sectionIndex = 0; sectionIndex < document.Sections.Count; sectionIndex++)
            {
                AddSegmentCandidates(candidates, document, sectionIndex, ref order);
            }
        }

        return candidates.ToArray();
    }

    private void AddSections(ICollection<TokenizedKnowledgeSection> sections, MarkdownDocument document)
    {
        for (var index = 0; index < document.Sections.Count; index++)
        {
            var section = document.Sections[index];
            if (string.IsNullOrWhiteSpace(section.Text))
            {
                continue;
            }

            sections.Add(new TokenizedKnowledgeSection(
                CreateSectionId(document, index),
                document.DocumentUri.AbsoluteUri,
                ResolveSectionLabel(document, section)));
        }
    }

    private void AddSegmentCandidates(
        List<TokenizedSegmentCandidate> candidates,
        MarkdownDocument document,
        int sectionIndex,
        ref int order)
    {
        var section = document.Sections[sectionIndex];
        var parentId = CreateSectionId(document, sectionIndex);
        var paragraphStart = 0;
        while (paragraphStart < section.Text.Length)
        {
            var delimiterIndex = section.Text.AsSpan(paragraphStart).IndexOf(DoubleNewLineDelimiter.AsSpan());
            var paragraphEnd = delimiterIndex < 0
                ? section.Text.Length
                : paragraphStart + delimiterIndex;
            AddParagraphSegmentCandidates(
                candidates,
                document,
                parentId,
                section.Text,
                paragraphStart,
                paragraphEnd,
                ref order);
            if (delimiterIndex < 0)
            {
                break;
            }

            paragraphStart = paragraphEnd + DoubleNewLineDelimiter.Length;
        }
    }

    private void AddParagraphSegmentCandidates(
        List<TokenizedSegmentCandidate> candidates,
        MarkdownDocument document,
        string parentId,
        string text,
        int paragraphStart,
        int paragraphEnd,
        ref int order)
    {
        var lineCount = 0;
        string? firstLine = null;
        var lineStart = paragraphStart;
        while (lineStart <= paragraphEnd)
        {
            var lineEnd = text.AsSpan(lineStart, paragraphEnd - lineStart).IndexOf(NewLineDelimiter.AsSpan());
            lineEnd = lineEnd < 0 ? paragraphEnd : lineStart + lineEnd;
            var line = text.AsSpan(lineStart, lineEnd - lineStart).Trim();
            if (!line.IsEmpty)
            {
                lineCount++;
                if (lineCount == 1)
                {
                    firstLine = line.ToString();
                }
                else
                {
                    if (lineCount == 2)
                    {
                        TryAddSegmentCandidate(candidates, document, parentId, firstLine!, ref order);
                    }

                    TryAddSegmentCandidate(candidates, document, parentId, line.ToString(), ref order);
                }
            }

            if (lineEnd >= paragraphEnd)
            {
                break;
            }

            lineStart = lineEnd + NewLineDelimiter.Length;
        }

        if (lineCount == 1)
        {
            TryAddSegmentCandidate(candidates, document, parentId, firstLine!, ref order);
        }
    }

    private void TryAddSegmentCandidate(
        ICollection<TokenizedSegmentCandidate> candidates,
        MarkdownDocument document,
        string parentId,
        string text,
        ref int order)
    {
        var tokenIds = _vectorizer.Tokenize(text);
        if (tokenIds.Count < _options.MinimumTokenCount)
        {
            return;
        }

        candidates.Add(new TokenizedSegmentCandidate(
            CreateSegmentId(document, order),
            document.DocumentUri.AbsoluteUri,
            parentId,
            text,
            order,
            tokenIds));
        order++;
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
