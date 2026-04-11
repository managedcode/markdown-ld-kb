namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownKnowledgeConstants
{
    internal const string ArticleKind = "article";
    internal const string EntityKind = "entity";
    internal const string IdSeparator = "/";
    internal const string ItemLabel = "item";
    internal const string UntitledLabel = "untitled";
    internal const string UntitledTitle = "Untitled";
    internal const string Space = " ";
    internal const string Hyphen = "-";
    internal const string SameAsKeySeparator = "|";

    internal const string SlugInvalidCharactersPattern = @"[^a-z0-9\s-]";
    internal const string SlugWhitespacePattern = @"[\s_]+";
    internal const string SlugHyphenPattern = @"-+";
    internal const string CamelBoundaryPattern = @"([a-z0-9])([A-Z])";
    internal const string CamelBoundaryReplacement = "$1 $2";
    internal const string LabelSeparatorPattern = @"[-_/]+";
    internal const string WhitespacePattern = @"\s+";
    internal const string ExtensionPattern = @"\.[^.\/]+$";

    internal const string FrontMatterFence = "---";
    internal const string FrontMatterStart = "---\n";
    internal const string CarriageReturnLineFeed = "\r\n";
    internal const string LineFeed = "\n";

    internal const string TitleKey = "title";
    internal const string SummaryKey = "summary";
    internal const string CanonicalUrlKey = "canonical_url";
    internal const string DatePublishedKey = "date_published";
    internal const string DateModifiedKey = "date_modified";
    internal const string AuthorsKey = "authors";
    internal const string TagsKey = "tags";
    internal const string AboutKey = "about";
    internal const string NameKey = "name";
    internal const string LabelKey = "label";
    internal const string SameAsKey = "sameAs";
    internal const string SameAsSnakeKey = "same_as";
    internal const string TypeKey = "type";
    internal const string ValueKey = "value";
    internal const string EntityHintsKey = "entity_hints";

    internal const string SchemaPrefix = "schema:";
    internal const string SchemaPerson = "schema:Person";
    internal const string SchemaOrganization = "schema:Organization";
    internal const string SchemaSoftwareApplication = "schema:SoftwareApplication";
    internal const string SchemaCreativeWork = "schema:CreativeWork";
    internal const string SchemaArticle = "schema:Article";
    internal const string SchemaThing = "schema:Thing";
    internal const string SchemaAuthor = "schema:author";
    internal const string SchemaAbout = "schema:about";
    internal const string SchemaMentions = "schema:mentions";
    internal const string UrnSchemePrefix = "urn:";

    internal const string FrontMatterSource = "front-matter";
    internal const string ArrowSource = "arrow";
    internal const string WikiLinkSource = "wikilink";
    internal const string MarkdownLinkSource = "markdown-link";
    internal const string MarkdownSource = "markdown";
    internal const string MarkdownArrowSource = "markdown-arrow";

    internal const string HeadingPattern = @"^\s*#\s+(?<title>.+?)\s*$";
    internal const string WikiLinkPattern = @"\[\[(?<target>[^\[\]\|]+)(?:\|(?<alias>[^\[\]]+))?\]\]";
    internal const string MarkdownLinkPattern = @"\[(?<label>[^\]]+)\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)";
    internal const string ArrowPattern = @"(?<subject>[^-\r\n]+?)\s*--(?<predicate>[^>\r\n]+?)-->\s*(?<object>.+?)(?=$|\s{2,}|[.!?;,])";
    internal const string HeadingGroup = "title";
    internal const string TargetGroup = "target";
    internal const string AliasGroup = "alias";
    internal const string LabelGroup = "label";
    internal const string SubjectGroup = "subject";
    internal const string PredicateGroup = "predicate";
    internal const string ObjectGroup = "object";
    internal const string InvalidFrontMatterMessage = "Markdown front matter is invalid.";
    internal const string MissingFrontMatterTerminatorMessage = "Markdown front matter closing fence is missing.";
    internal const string CodeFenceBacktick = "```";
    internal const string CodeFenceTilde = "~~~";
    internal const string WikiLinkStart = "[[";
    internal const string WikiLinkEnd = "]]";
    internal const string InlineCodeMarker = "`";
    internal const string EmphasisMarker = "*";
    internal const string Underscore = "_";
    internal const string UriSchemeSeparator = "://";

    internal const char SpaceCharacter = ' ';
    internal const char TabCharacter = '\t';
    internal const char DotCharacter = '.';
    internal const char CommaCharacter = ',';
    internal const char SemicolonCharacter = ';';
    internal const char ColonCharacter = ':';
    internal const char ExclamationCharacter = '!';
    internal const char QuestionCharacter = '?';
    internal const char RightParenthesisCharacter = ')';
    internal const char LeftParenthesisCharacter = '(';
    internal const char LeftBracketCharacter = '[';
    internal const char RightBracketCharacter = ']';
    internal const char DoubleQuoteCharacter = '"';
    internal const char SingleQuoteCharacter = '\'';

    internal static readonly char[] SurfaceTrimCharacters =
    [
        SpaceCharacter,
        TabCharacter,
        DotCharacter,
        CommaCharacter,
        SemicolonCharacter,
        ColonCharacter,
        ExclamationCharacter,
        QuestionCharacter,
        RightParenthesisCharacter,
        LeftParenthesisCharacter,
        LeftBracketCharacter,
        RightBracketCharacter,
        DoubleQuoteCharacter,
        SingleQuoteCharacter,
    ];
}
