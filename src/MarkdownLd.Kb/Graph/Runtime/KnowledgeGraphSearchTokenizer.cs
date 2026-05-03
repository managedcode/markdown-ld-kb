using System.Globalization;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphSearchTokenizer
{
    private const int NoTokenStart = -1;
    private const int SurrogatePairLength = 2;
    private const int SingleCharLength = 1;

    public static string[] TokenizeDistinct(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokenStart = NoTokenStart;
        for (var index = 0; index < text.Length;)
        {
            var charLength = GetCharLength(text, index);
            if (IsTokenCharacter(text, index))
            {
                tokenStart = tokenStart == NoTokenStart ? index : tokenStart;
            }
            else if (tokenStart != NoTokenStart)
            {
                AddDistinctToken(tokens, seen, CreateToken(text, tokenStart, index - tokenStart));
                tokenStart = NoTokenStart;
            }

            index += charLength;
        }

        if (tokenStart != NoTokenStart)
        {
            AddDistinctToken(tokens, seen, CreateToken(text, tokenStart, text.Length - tokenStart));
        }

        return tokens.ToArray();
    }

    public static int CountTermFrequencies(string text, Dictionary<string, int> frequencies)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(frequencies);

        var tokenCount = 0;
        var tokenStart = NoTokenStart;
        for (var index = 0; index < text.Length;)
        {
            var charLength = GetCharLength(text, index);
            if (IsTokenCharacter(text, index))
            {
                tokenStart = tokenStart == NoTokenStart ? index : tokenStart;
            }
            else if (tokenStart != NoTokenStart)
            {
                AddFrequency(frequencies, CreateToken(text, tokenStart, index - tokenStart));
                tokenCount++;
                tokenStart = NoTokenStart;
            }

            index += charLength;
        }

        if (tokenStart != NoTokenStart)
        {
            AddFrequency(frequencies, CreateToken(text, tokenStart, text.Length - tokenStart));
            tokenCount++;
        }

        return tokenCount;
    }

    private static bool IsTokenCharacter(string text, int index)
    {
        return CharUnicodeInfo.GetUnicodeCategory(text, index) switch
        {
            UnicodeCategory.UppercaseLetter or
                UnicodeCategory.LowercaseLetter or
                UnicodeCategory.TitlecaseLetter or
                UnicodeCategory.ModifierLetter or
                UnicodeCategory.OtherLetter or
                UnicodeCategory.DecimalDigitNumber or
                UnicodeCategory.LetterNumber or
                UnicodeCategory.OtherNumber => true,
            _ => false,
        };
    }

    private static int GetCharLength(string text, int index)
    {
        return char.IsHighSurrogate(text[index]) &&
               index + 1 < text.Length &&
               char.IsLowSurrogate(text[index + 1])
            ? SurrogatePairLength
            : SingleCharLength;
    }

    private static string CreateToken(string text, int start, int length)
    {
        var token = text.AsSpan(start, length);
        return RequiresLowercase(token)
            ? string.Create(length, new TokenSource(text, start), WriteLowercaseToken)
            : token.ToString();
    }

    private static void AddFrequency(Dictionary<string, int> frequencies, string token)
    {
        frequencies[token] = frequencies.GetValueOrDefault(token) + 1;
    }

    private static void AddDistinctToken(List<string> tokens, HashSet<string> seen, string token)
    {
        if (seen.Add(token))
        {
            tokens.Add(token);
        }
    }

    private static void WriteLowercaseToken(Span<char> destination, TokenSource source)
    {
        var token = source.Text.AsSpan(source.Start, destination.Length);
        for (var index = 0; index < token.Length; index++)
        {
            destination[index] = char.ToLowerInvariant(token[index]);
        }
    }

    private static bool RequiresLowercase(ReadOnlySpan<char> token)
    {
        for (var index = 0; index < token.Length; index++)
        {
            if (char.ToLowerInvariant(token[index]) != token[index])
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct TokenSource(string Text, int Start);
}
