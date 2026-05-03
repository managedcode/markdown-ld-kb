using System.Globalization;
using System.Runtime.InteropServices;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphSearchTokenizer
{
    private const int NoTokenStart = -1;
    private const int SurrogatePairLength = 2;
    private const int SingleCharLength = 1;
    private const int AsciiLimit = 128;
    private const int AsciiLowercaseOffset = 32;
    private const int StackLowercaseTokenLengthLimit = 256;

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

    public static int CountSelectedTermFrequencies(
        string text,
        Dictionary<string, int> selectedTermIndexes,
        Span<double> selectedTermFrequencies)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(selectedTermIndexes);

        var tokenCount = 0;
        var tokenStart = NoTokenStart;
        var selectedTermLookup = selectedTermIndexes.GetAlternateLookup<ReadOnlySpan<char>>();
        for (var index = 0; index < text.Length;)
        {
            var charLength = GetCharLength(text, index);
            if (IsTokenCharacter(text, index))
            {
                tokenStart = tokenStart == NoTokenStart ? index : tokenStart;
            }
            else if (tokenStart != NoTokenStart)
            {
                AddSelectedFrequency(
                    text,
                    tokenStart,
                    index - tokenStart,
                    selectedTermIndexes,
                    selectedTermLookup,
                    selectedTermFrequencies);
                tokenCount++;
                tokenStart = NoTokenStart;
            }

            index += charLength;
        }

        if (tokenStart != NoTokenStart)
        {
            AddSelectedFrequency(
                text,
                tokenStart,
                text.Length - tokenStart,
                selectedTermIndexes,
                selectedTermLookup,
                selectedTermFrequencies);
            tokenCount++;
        }

        return tokenCount;
    }

    private static bool IsTokenCharacter(string text, int index)
    {
        var value = text[index];
        if (value < AsciiLimit)
        {
            return IsAsciiTokenCharacter(value);
        }

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
        ref var frequency = ref CollectionsMarshal.GetValueRefOrAddDefault(frequencies, token, out _);
        frequency++;
    }

    private static void AddSelectedFrequency(
        string text,
        int start,
        int length,
        Dictionary<string, int> selectedTermIndexes,
        Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> selectedTermLookup,
        Span<double> selectedTermFrequencies)
    {
        var token = text.AsSpan(start, length);
        if (TryGetSelectedTermIndex(
                token,
                text,
                start,
                selectedTermIndexes,
                selectedTermLookup,
                out var termIndex))
        {
            selectedTermFrequencies[termIndex] += PipelineConstants.TokenCountIncrement;
        }
    }

    private static bool TryGetSelectedTermIndex(
        ReadOnlySpan<char> token,
        string text,
        int start,
        Dictionary<string, int> selectedTermIndexes,
        Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> selectedTermLookup,
        out int termIndex)
    {
        if (!RequiresLowercase(token))
        {
            return selectedTermLookup.TryGetValue(token, out termIndex);
        }

        if (token.Length > StackLowercaseTokenLengthLimit)
        {
            return selectedTermIndexes.TryGetValue(CreateToken(text, start, token.Length), out termIndex);
        }

        Span<char> lowercase = stackalloc char[token.Length];
        CopyLowercaseToken(token, lowercase);
        return selectedTermLookup.TryGetValue(lowercase, out termIndex);
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
        CopyLowercaseToken(token, destination);
    }

    private static void CopyLowercaseToken(ReadOnlySpan<char> token, Span<char> destination)
    {
        for (var index = 0; index < token.Length; index++)
        {
            destination[index] = ToLowerInvariant(token[index]);
        }
    }

    private static bool RequiresLowercase(ReadOnlySpan<char> token)
    {
        for (var index = 0; index < token.Length; index++)
        {
            var value = token[index];
            if (IsAsciiUppercase(value) || (value >= AsciiLimit && char.ToLowerInvariant(value) != value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiTokenCharacter(char value)
    {
        return IsAsciiUppercase(value) || IsAsciiLowercase(value) || IsAsciiDigit(value);
    }

    private static bool IsAsciiUppercase(char value)
    {
        return (uint)(value - 'A') <= 'Z' - 'A';
    }

    private static bool IsAsciiLowercase(char value)
    {
        return (uint)(value - 'a') <= 'z' - 'a';
    }

    private static bool IsAsciiDigit(char value)
    {
        return (uint)(value - '0') <= '9' - '0';
    }

    private static char ToLowerInvariant(char value)
    {
        return IsAsciiUppercase(value)
            ? (char)(value + AsciiLowercaseOffset)
            : char.ToLowerInvariant(value);
    }

    private readonly record struct TokenSource(string Text, int Start);
}
