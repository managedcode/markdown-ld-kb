using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Parsing;

public sealed partial class MarkdownDocumentParser
{
    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static object GetLinkKey(MarkdownLinkReference link) =>
        (
            link.Kind,
            link.Target,
            link.DisplayText,
            link.Destination,
            link.Title,
            link.IsExternal,
            link.IsImage,
            link.IsDocumentLink,
            link.ResolvedTarget
        );

    [GeneratedRegex(MarkdownTextConstants.HeadingPattern, RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(MarkdownTextConstants.WikiLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownImageLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownImageLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.WhitespacePattern, RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record HeadingFrame(string Text);
}
