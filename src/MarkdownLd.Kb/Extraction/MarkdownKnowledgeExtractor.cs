using static ManagedCode.MarkdownLd.Kb.Extraction.MarkdownKnowledgeConstants;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

public sealed class MarkdownKnowledgeExtractor
{
    public MarkdownKnowledgeExtractionResult Extract(string markdown, string? sourcePath = null)
    {
        var parsed = MarkdownFrontMatterParser.Parse(markdown);
        var scan = MarkdownKnowledgeScanner.Scan(parsed.Body);

        var title = ResolveTitle(parsed.FrontMatter.Title, scan.Title, sourcePath);
        var articleId = MarkdownKnowledgeIds.BuildArticleId(title, sourcePath, parsed.FrontMatter.CanonicalUrl);
        var article = new MarkdownArticleMetadata
        {
            Id = articleId,
            Title = title,
            Summary = parsed.FrontMatter.Summary,
            CanonicalUrl = parsed.FrontMatter.CanonicalUrl,
            DatePublished = parsed.FrontMatter.DatePublished,
            DateModified = parsed.FrontMatter.DateModified,
            Authors = parsed.FrontMatter.Authors,
            Tags = parsed.FrontMatter.Tags,
            About = parsed.FrontMatter.About,
        };

        var candidates = BuildEntityCandidates(parsed.FrontMatter, scan, title);
        var entities = MarkdownKnowledgeCanonicalizer.CanonicalizeEntities(candidates);
        var lookup = MarkdownKnowledgeCanonicalizer.BuildAliasLookup(article.Id, article.Title, entities, article.About);
        var assertions = MarkdownKnowledgeCanonicalizer.CanonicalizeAssertions(BuildAssertionCandidates(article, scan), lookup);

        return new MarkdownKnowledgeExtractionResult
        {
            Article = article,
            Entities = entities,
            Assertions = assertions,
        };
    }

    private static string ResolveTitle(string? frontMatterTitle, string? scannedTitle, string? sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(frontMatterTitle))
        {
            return frontMatterTitle!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(scannedTitle))
        {
            return scannedTitle!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            return MarkdownKnowledgeIds.HumanizeLabel(System.IO.Path.GetFileNameWithoutExtension(sourcePath));
        }

        return UntitledTitle;
    }

    private static IReadOnlyList<MarkdownKnowledgeEntityCandidate> BuildEntityCandidates(
        MarkdownFrontMatter frontMatter,
        MarkdownKnowledgeScanResult scan,
        string articleTitle)
    {
        var candidates = new List<MarkdownKnowledgeEntityCandidate>();

        AddAuthorEntities(candidates, frontMatter.Authors);
        AddTopicEntities(candidates, frontMatter.About);
        AddHintEntities(candidates, frontMatter.EntityHints);
        AddScanEntities(candidates, scan.Entities);
        AddAssertionEndpointEntities(candidates, scan.Assertions, articleTitle);

        return candidates;
    }

    private static void AddAuthorEntities(ICollection<MarkdownKnowledgeEntityCandidate> candidates, IEnumerable<MarkdownAuthor> authors)
    {
        foreach (var author in authors.Where(author => !string.IsNullOrWhiteSpace(author.Name)))
        {
            candidates.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = author.Name.Trim(),
                Type = string.IsNullOrWhiteSpace(author.Type) ? SchemaPerson : author.Type!,
                SameAs = string.IsNullOrWhiteSpace(author.SameAs) ? [] : [author.SameAs!],
                SourceKind = FrontMatterSource,
            });
        }
    }

    private static void AddTopicEntities(ICollection<MarkdownKnowledgeEntityCandidate> candidates, IEnumerable<MarkdownTopic> about)
    {
        foreach (var topic in about.Where(topic => !string.IsNullOrWhiteSpace(topic.Label)))
        {
            candidates.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = topic.Label.Trim(),
                Type = SchemaThing,
                SameAs = string.IsNullOrWhiteSpace(topic.SameAs) ? [] : [topic.SameAs!],
                SourceKind = FrontMatterSource,
            });
        }
    }

    private static void AddHintEntities(ICollection<MarkdownKnowledgeEntityCandidate> candidates, IEnumerable<MarkdownEntityHint> hints)
    {
        foreach (var hint in hints.Where(hint => !string.IsNullOrWhiteSpace(hint.Label)))
        {
            candidates.Add(new MarkdownKnowledgeEntityCandidate
            {
                Label = hint.Label.Trim(),
                Type = string.IsNullOrWhiteSpace(hint.Type) ? SchemaThing : hint.Type!,
                SameAs = string.IsNullOrWhiteSpace(hint.SameAs) ? [] : [hint.SameAs!],
                SourceKind = FrontMatterSource,
            });
        }
    }

    private static void AddScanEntities(ICollection<MarkdownKnowledgeEntityCandidate> candidates, IEnumerable<MarkdownKnowledgeEntityCandidate> scanEntities)
    {
        foreach (var entity in scanEntities.Where(entity => !string.IsNullOrWhiteSpace(entity.Label)))
        {
            candidates.Add(entity);
        }
    }

    private static void AddAssertionEndpointEntities(
        ICollection<MarkdownKnowledgeEntityCandidate> candidates,
        IEnumerable<MarkdownKnowledgeAssertionCandidate> assertions,
        string articleTitle)
    {
        var existingLabels = new HashSet<string>(
            candidates.Select(candidate => MarkdownKnowledgeIds.Slugify(candidate.Label)),
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(articleTitle))
        {
            existingLabels.Add(MarkdownKnowledgeIds.Slugify(articleTitle));
        }

        foreach (var endpoint in assertions.SelectMany(assertion => new[] { assertion.Subject, assertion.Object }))
        {
            var label = endpoint.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var slug = MarkdownKnowledgeIds.Slugify(label);
            if (existingLabels.Add(slug))
            {
                candidates.Add(new MarkdownKnowledgeEntityCandidate
                {
                    Label = label,
                    Type = SchemaThing,
                    SameAs = [],
                    SourceKind = ArrowSource,
                });
            }
        }
    }

    private static IReadOnlyList<MarkdownKnowledgeAssertionCandidate> BuildAssertionCandidates(
        MarkdownArticleMetadata article,
        MarkdownKnowledgeScanResult scan)
    {
        var assertions = new List<MarkdownKnowledgeAssertionCandidate>();

        assertions.AddRange(article.Authors
            .Where(author => !string.IsNullOrWhiteSpace(author.Name))
            .Select(author => new MarkdownKnowledgeAssertionCandidate
            {
                Subject = article.Title,
                Predicate = SchemaAuthor,
                Object = author.Name,
                Confidence = 1.0,
                Source = FrontMatterSource,
            }));

        assertions.AddRange(article.About
            .Where(topic => !string.IsNullOrWhiteSpace(topic.Label))
            .Select(topic => new MarkdownKnowledgeAssertionCandidate
            {
                Subject = article.Title,
                Predicate = SchemaAbout,
                Object = topic.Label,
                Confidence = 1.0,
                Source = FrontMatterSource,
            }));

        assertions.AddRange(scan.Entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.Label))
            .Select(entity => new MarkdownKnowledgeAssertionCandidate
            {
                Subject = article.Title,
                Predicate = SchemaMentions,
                Object = entity.Label,
                Confidence = entity.SourceKind == WikiLinkSource ? 0.95 : entity.SourceKind == MarkdownLinkSource ? 0.85 : 0.9,
                Source = MarkdownSource,
            }));

        assertions.AddRange(scan.Assertions);

        return assertions;
    }
}
