using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed partial class TokenKeyphraseCandidateBuilder
{
    private readonly TiktokenKnowledgeGraphOptions _options;

    public TokenKeyphraseCandidateBuilder(TiktokenKnowledgeGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public KeyphraseCandidateGroup[] CreateCandidateGroups(IReadOnlyList<TokenizedSegmentCandidate> segments)
    {
        var candidates = new KeyphraseCandidateGroup[segments.Count];
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            candidates[index] = new KeyphraseCandidateGroup(segment, CreateCandidates(segment));
        }

        return candidates;
    }

    private KeyphraseCandidate[] CreateCandidates(TokenizedSegmentCandidate segment)
    {
        var words = ReadWords(segment.Text);
        var candidates = new List<KeyphraseCandidate>(words.Length * 2);

        for (var start = 0; start < words.Length; start++)
        {
            AddCandidatesFromStart(candidates, words, start);
        }

        return candidates.ToArray();
    }

    private void AddCandidatesFromStart(List<KeyphraseCandidate> candidates, string[] words, int start)
    {
        var maxLength = Math.Min(_options.MaxTopicPhraseWords, words.Length - start);
        for (var length = 1; length <= maxLength; length++)
        {
            if (!HasMinimumTopicWordLength(words, start, length, _options.MinimumTopicWordLength))
            {
                continue;
            }

            var label = CreatePhraseLabel(words, start, length);
            candidates.Add(new KeyphraseCandidate(
                NormalizeKey(label),
                label,
                length,
                label.Length));
        }
    }

    private static string[] ReadWords(string text)
    {
        var matches = WordRegex().Matches(text);
        var words = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            var word = match.Value.Trim();
            if (word.Length > 0)
            {
                words.Add(word);
            }
        }

        return words.ToArray();
    }

    private static bool HasMinimumTopicWordLength(
        string[] words,
        int start,
        int length,
        int minimumLength)
    {
        for (var index = start; index < start + length; index++)
        {
            if (words[index].Length >= minimumLength)
            {
                return true;
            }
        }

        return false;
    }

    private static string CreatePhraseLabel(string[] words, int start, int length)
    {
        if (length == 1)
        {
            return words[start];
        }

        var characterCount = length - 1;
        for (var index = start; index < start + length; index++)
        {
            characterCount += words[index].Length;
        }

        return string.Create(characterCount, new PhraseSource(words, start, length), WritePhraseLabel);
    }

    private static void WritePhraseLabel(Span<char> destination, PhraseSource source)
    {
        var offset = 0;
        for (var index = source.Start; index < source.Start + source.Length; index++)
        {
            if (index > source.Start)
            {
                destination[offset] = SpaceText[0];
                offset++;
            }

            source.Words[index].AsSpan().CopyTo(destination[offset..]);
            offset += source.Words[index].Length;
        }
    }

    private static string NormalizeKey(string label)
    {
        return label.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(TopicWordPattern, RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private readonly record struct PhraseSource(string[] Words, int Start, int Length);
}

internal sealed record KeyphraseCandidateGroup(
    TokenizedSegmentCandidate Segment,
    IReadOnlyList<KeyphraseCandidate> Candidates);

internal sealed record KeyphraseCandidate(
    string Key,
    string Label,
    int WordCount,
    int CharacterCount);
