using ManagedCode.MarkdownLd.Kb.Parsing;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Parsing;

public sealed class MarkdownChunkEvaluationFlowTests
{
    private const string SourcePath = "docs/evaluation.md";
    private const string FrameworkAnswer = "ASP.NET Core Razor Pages";
    private const string MissingAnswer = "MongoDB";
    private const string Markdown = """
---
title: Evaluation Guide
---
# Evaluation Guide

The frontend uses ASP.NET Core Razor Pages and static files under wwwroot.

## Search Table

| Component | Choice |
| --- | --- |
| Embeddings | Optional Microsoft.Extensions.AI adapter |

## Code

```csharp
Console.WriteLine("hello");
```
""";

    [Test]
    public void Chunk_evaluator_reports_size_coverage_and_quality_samples()
    {
        var evaluator = new MarkdownChunkEvaluator();

        var result = evaluator.Evaluate(
            Markdown,
            SourcePath,
            [
                new MarkdownChunkCoverageExpectation("Which frontend framework is used?", FrameworkAnswer),
                new MarkdownChunkCoverageExpectation("Which database is used?", MissingAnswer),
            ],
            new MarkdownChunkEvaluationOptions
            {
                ParsingOptions = new MarkdownParsingOptions
                {
                    Chunking = new MarkdownChunkingOptions { ChunkTokenTarget = 20 },
                },
                QualitySampleSize = 2,
            });

        result.SizeDistribution.Total.ShouldBeGreaterThan(1);
        result.SizeDistribution.TooLarge.ShouldBe(0);
        result.CoverageRate.ShouldBe(0.5d);
        result.CoverageResults.Single(item => item.ExpectedAnswer == FrameworkAnswer).Found.ShouldBeTrue();
        result.CoverageResults.Single(item => item.ExpectedAnswer == MissingAnswer).Found.ShouldBeFalse();
        result.QualitySamples.Count.ShouldBe(2);
        result.QualitySamples.ShouldAllBe(sample => sample.Preview.Length > 0);
    }

    [Test]
    public void Chunk_evaluator_analyzes_already_parsed_documents()
    {
        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(Markdown, SourcePath));
        var evaluator = new MarkdownChunkEvaluator();

        var result = evaluator.AnalyzeDocument(
            document,
            [new MarkdownChunkCoverageExpectation("Which frontend framework is used?", FrameworkAnswer)]);

        result.SizeDistribution.Total.ShouldBe(document.Chunks.Count);
        result.CoverageRate.ShouldBe(1d);
    }

    [Test]
    public void Chunk_evaluator_reports_conventional_even_count_median()
    {
        var document = CreateDocumentWithTokenCounts(1, 2, 9, 10);
        var evaluator = new MarkdownChunkEvaluator();

        var result = evaluator.AnalyzeDocument(document);

        result.SizeDistribution.MedianTokens.ShouldBe(5.5d);
    }

    [Test]
    public void Chunk_evaluator_handles_empty_documents_and_no_expectations()
    {
        var evaluator = new MarkdownChunkEvaluator();

        var result = evaluator.Evaluate(
            string.Empty,
            SourcePath,
            options: new MarkdownChunkEvaluationOptions
            {
                QualitySampleSize = 0,
            });

        result.SizeDistribution.Total.ShouldBe(0);
        result.SizeDistribution.AverageTokens.ShouldBe(0d);
        result.CoverageResults.ShouldBeEmpty();
        result.CoverageRate.ShouldBe(0d);
        result.QualitySamples.ShouldBeEmpty();
    }

    [Test]
    public void Chunk_evaluator_rejects_invalid_threshold_order()
    {
        var evaluator = new MarkdownChunkEvaluator();

        Should.Throw<ArgumentException>(() =>
            evaluator.Evaluate(
                Markdown,
                SourcePath,
                options: new MarkdownChunkEvaluationOptions
                {
                    SmallTokenThreshold = 20,
                    LargeTokenThreshold = 10,
                }));
    }

    [Test]
    public void Chunk_evaluator_rejects_empty_coverage_expectations()
    {
        var evaluator = new MarkdownChunkEvaluator();

        Should.Throw<ArgumentException>(() =>
            evaluator.Evaluate(
                Markdown,
                SourcePath,
                [new MarkdownChunkCoverageExpectation("Which frontend framework is used?", string.Empty)]));

        Should.Throw<ArgumentException>(() =>
            evaluator.Evaluate(
                Markdown,
                SourcePath,
                [new MarkdownChunkCoverageExpectation(string.Empty, FrameworkAnswer)]));
    }

    private static MarkdownDocument CreateDocumentWithTokenCounts(params int[] tokenCounts)
    {
        var chunks = tokenCounts
            .Select((tokenCount, index) => new MarkdownChunk(
                $"chunk-{index}",
                "section-1",
                index,
                [],
                $"Chunk {index}",
                tokenCount,
                []))
            .ToArray();

        var section = new MarkdownSection(
            "section-1",
            0,
            1,
            "# Evaluation",
            "Evaluation",
            ["Evaluation"],
            Markdown,
            chunks,
            []);

        return new MarkdownDocument(
            "document-1",
            SourcePath,
            null,
            new MarkdownFrontMatter
            {
                RawYaml = string.Empty,
                Values = new Dictionary<string, object?>(),
            },
            Markdown,
            Markdown,
            [section],
            chunks,
            []);
    }
}
