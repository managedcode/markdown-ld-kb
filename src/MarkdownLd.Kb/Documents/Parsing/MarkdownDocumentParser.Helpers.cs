using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Parsing;

public sealed partial class MarkdownDocumentParser
{
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

    [GeneratedRegex(MarkdownTextConstants.WhitespacePattern, RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record HeadingFrame(string Text);
}
