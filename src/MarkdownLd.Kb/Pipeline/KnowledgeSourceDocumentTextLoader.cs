using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeSourceDocumentTextLoader
{
    private static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf8BomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16LittleEndianEncoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16BigEndianEncoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf32LittleEndianEncoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
    private static readonly Encoding StrictUtf32BigEndianEncoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
    private static readonly IReadOnlyList<Encoding> BomEncodings =
    [
        StrictUtf32LittleEndianEncoding,
        StrictUtf32BigEndianEncoding,
        StrictUtf8BomEncoding,
        StrictUtf16LittleEndianEncoding,
        StrictUtf16BigEndianEncoding,
    ];

    public static async Task<string> ReadTextAsync(
        string filePath,
        Encoding? encoding,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Decode(filePath, bytes, encoding);
    }

    private static string Decode(string filePath, byte[] bytes, Encoding? encoding)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (encoding is not null)
        {
            return DecodeWithEncoding(filePath, bytes, CreateStrictEncoding(encoding));
        }

        if (TryGetBomEncoding(bytes) is { } bomEncoding)
        {
            return DecodeWithEncoding(filePath, bytes, bomEncoding);
        }

        if (ContainsNullByte(bytes) || ContainsDisallowedControlByte(bytes))
        {
            throw BuildInvalidDataException(filePath);
        }

        return DecodeWithEncoding(filePath, bytes, StrictUtf8Encoding);
    }

    private static string DecodeWithEncoding(string filePath, byte[] bytes, Encoding encoding)
    {
        try
        {
            return TrimByteOrderMark(encoding.GetString(bytes));
        }
        catch (DecoderFallbackException exception)
        {
            throw BuildInvalidDataException(filePath, exception);
        }
    }

    private static InvalidDataException BuildInvalidDataException(string filePath, Exception? innerException = null)
    {
        return innerException is null
            ? new InvalidDataException(string.Concat(UndecodableTextFileMessagePrefix, filePath, UndecodableTextFileMessageSuffix))
            : new InvalidDataException(string.Concat(UndecodableTextFileMessagePrefix, filePath, UndecodableTextFileMessageSuffix), innerException);
    }

    private static Encoding CreateStrictEncoding(Encoding encoding)
    {
        var strictEncoding = (Encoding)encoding.Clone();
        strictEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        strictEncoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        return strictEncoding;
    }

    private static Encoding? TryGetBomEncoding(byte[] bytes)
    {
        foreach (var encoding in BomEncodings)
        {
            if (HasPreamble(bytes, encoding))
            {
                return encoding;
            }
        }

        return null;
    }

    private static bool HasPreamble(byte[] bytes, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || preamble.Length > bytes.Length)
        {
            return false;
        }

        return bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble);
    }

    private static bool ContainsNullByte(byte[] bytes)
    {
        foreach (var value in bytes)
        {
            if (value == NullByteCharacter)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDisallowedControlByte(byte[] bytes)
    {
        foreach (var value in bytes)
        {
            if (value < SpaceByte &&
                value != HorizontalTabByte &&
                value != LineFeedByte &&
                value != FormFeedByte &&
                value != CarriageReturnByte)
            {
                return true;
            }

            if (value == DeleteControlByte)
            {
                return true;
            }
        }

        return false;
    }

    private static string TrimByteOrderMark(string text)
    {
        return text.Length > 0 && text[0] == UnicodeByteOrderMarkCharacter
            ? text[1..]
            : text;
    }
}
