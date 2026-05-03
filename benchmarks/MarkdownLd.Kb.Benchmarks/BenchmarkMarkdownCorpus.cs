using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

internal static class BenchmarkMarkdownCorpus
{
    private const int ShortDocumentCount = 250;
    private const int LongDocumentCount = 80;
    private const int LargeDocumentCount = 1000;
    private const int TokenizedDocumentCount = 250;
    private const int FederatedDocumentCount = 250;
    private const string LocalFederatedEndpointText = "https://bench.example/sparql/local";
    private const string CacheTitlePrefix = "Cache restore runbook";
    private const string BillingTitlePrefix = "Billing export guide";
    private const string ReleaseTitlePrefix = "Release evidence checklist";

    public static MarkdownSourceDocument[] CreateSources(BenchmarkCorpusProfile profile)
    {
        return Enumerable.Range(0, GetDocumentCount(profile))
            .Select(index => new MarkdownSourceDocument(
                $"content/bench/{CreatePathSegment(profile)}/doc-{index:D5}.md",
                CreateMarkdown(profile, index)))
            .ToArray();
    }

    private static int GetDocumentCount(BenchmarkCorpusProfile profile)
    {
        return profile switch
        {
            BenchmarkCorpusProfile.LongDocuments => LongDocumentCount,
            BenchmarkCorpusProfile.LargeCorpus => LargeDocumentCount,
            BenchmarkCorpusProfile.TokenizedMultilingual => TokenizedDocumentCount,
            BenchmarkCorpusProfile.FederatedRunbooks => FederatedDocumentCount,
            _ => ShortDocumentCount,
        };
    }

    private static string CreatePathSegment(BenchmarkCorpusProfile profile)
    {
        return profile switch
        {
            BenchmarkCorpusProfile.LongDocuments => "long-documents",
            BenchmarkCorpusProfile.LargeCorpus => "large-corpus",
            BenchmarkCorpusProfile.TokenizedMultilingual => "tokenized-multilingual",
            BenchmarkCorpusProfile.FederatedRunbooks => "federated-runbooks",
            _ => "short-documents",
        };
    }

    private static string CreateMarkdown(BenchmarkCorpusProfile profile, int index)
    {
        return profile switch
        {
            BenchmarkCorpusProfile.LongDocuments => CreateLongMarkdown(index),
            BenchmarkCorpusProfile.TokenizedMultilingual => CreateTokenizedMarkdown(index),
            BenchmarkCorpusProfile.FederatedRunbooks => CreateFederatedMarkdown(index),
            _ => CreateStandardMarkdown(index),
        };
    }

    private static string CreateStandardMarkdown(int index)
    {
        var family = index % 3;
        var title = CreateTitle(family, index);
        var topic = CreateTopic(family);
        var body = CreateStandardBody(family, index);
        return $$"""
            ---
            title: {{title}}
            summary: {{topic}} summary for benchmark document {{index}}.
            tags:
              - benchmark
              - {{topic}}
            ---
            # {{title}}

            {{body}}
            """;
    }

    private static string CreateLongMarkdown(int index)
    {
        return $$"""
            ---
            title: Long incident recovery playbook {{index:D5}}
            summary: Long benchmark document {{index}} with repeated recovery evidence sections.
            tags:
              - benchmark
              - long-document
              - recovery
            ---
            # Long incident recovery playbook {{index:D5}}

            {{CreateLongBody(index)}}
            """;
    }

    private static string CreateTokenizedMarkdown(int index)
    {
        var identifier = $"tokenwindowrollbackevidence{index:D5}";
        return $$"""
            ---
            title: Multilingual token restore {{index:D5}}
            summary: Tokenized multilingual recovery evidence for benchmark document {{index}}.
            tags:
              - benchmark
              - multilingual
              - token-distance
            ---
            # Multilingual token restore {{index:D5}}

            Cache restore manifest evidence maps token windows and rollback fingerprints {{identifier}}.
            缓存 恢复 证据 manifest rollback token {{identifier}}.
            キャッシュ 復元 証跡 manifest rollback token {{identifier}}.
            캐시 복구 증거 manifest rollback token {{identifier}}.
            Українська нотатка описує cache manifest evidence and token repair {{identifier}}.
            """;
    }

    private static string CreateFederatedMarkdown(int index)
    {
        return $$"""
            ---
            title: Federated SPARQL runbook {{index:D5}}
            summary: Local federated service binding evidence for benchmark document {{index}}.
            tags:
              - benchmark
              - federation
              - sparql
            ---
            # Federated SPARQL runbook {{index:D5}}

            Federated SPARQL service binding validates local endpoint allowlists and runbook evidence.
            The local SERVICE endpoint {{LocalFederatedEndpointText}} is used for deterministic federation timing.
            Runbook evidence includes graph schema checks, service diagnostics, and focused result expansion.
            """;
    }

    private static string CreateTitle(int family, int index)
    {
        return family switch
        {
            1 => $"{BillingTitlePrefix} {index:D5}",
            2 => $"{ReleaseTitlePrefix} {index:D5}",
            _ => $"{CacheTitlePrefix} {index:D5}",
        };
    }

    private static string CreateTopic(int family)
    {
        return family switch
        {
            1 => "billing invoice export payment checkpoint",
            2 => "release gate approval evidence checklist",
            _ => "cache restore manifest rollback evidence",
        };
    }

    private static string CreateStandardBody(int family, int index)
    {
        var identifier = $"validationfingerprintcheckpointtoken{index:D5}manifestwindowrollbackevidence";
        return family switch
        {
            1 => $"Billing export verifies invoice payment checkpoint evidence with marker {identifier}.",
            2 => $"Release evidence checklist confirms approval gates and deployment notes with marker {identifier}.",
            _ => $"Cache restore validates manifest rollback evidence and runbook recovery with marker {identifier}.",
        };
    }

    private static string CreateLongBody(int index)
    {
        var sections = Enumerable.Range(0, 8)
            .Select(section => CreateLongSection(index, section));
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string CreateLongSection(int index, int section)
    {
        var family = (index + section) % 3;
        var topic = CreateTopic(family);
        var marker = $"longdocumentcheckpoint{index:D5}section{section:D2}timelineevidence";
        return $$"""
            ## Recovery checkpoint {{section}}

            The incident escalation recovery dependency timeline checkpoint records {{topic}}.
            Operators compare rollback evidence, linked approval notes, and downstream dependency status.
            Each section keeps a repeated marker {{marker}} so long-document search can measure scan pressure.
            """;
    }
}
