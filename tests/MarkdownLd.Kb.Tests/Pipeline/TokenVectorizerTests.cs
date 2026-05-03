using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Pipeline;

public sealed class TokenVectorizerTests
{
    private const double Tolerance = 0.0000001;
    private const int CommonToken = 1;
    private const int TopicToken = 2;
    private const int RareToken = 3;
    private const int FirstOtherToken = 4;
    private const int SecondOtherToken = 5;
    private const int ThirdOtherToken = 6;
    private const int FirstQueryToken = 10;
    private const int SecondQueryToken = 20;
    private const int FirstDistractorToken = 30;
    private const int SecondDistractorToken = 40;

    [Test]
    public void Term_frequency_vectors_are_l2_normalized_from_token_counts()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus([CommonToken, CommonToken, TopicToken]),
            TokenVectorWeighting.TermFrequency);

        var vector = vectorSpace.CreateVector([CommonToken, CommonToken, TopicToken]);

        vector.Weights[CommonToken].ShouldBe(2 / Math.Sqrt(5), Tolerance);
        vector.Weights[TopicToken].ShouldBe(1 / Math.Sqrt(5), Tolerance);
        Magnitude(vector).ShouldBe(1, Tolerance);
    }

    [Test]
    public void Binary_vectors_ignore_repeated_token_counts_before_normalization()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus([CommonToken, CommonToken, TopicToken]),
            TokenVectorWeighting.Binary);

        var vector = vectorSpace.CreateVector([CommonToken, CommonToken, TopicToken]);

        vector.Weights[CommonToken].ShouldBe(1 / Math.Sqrt(2), Tolerance);
        vector.Weights[TopicToken].ShouldBe(1 / Math.Sqrt(2), Tolerance);
        Magnitude(vector).ShouldBe(1, Tolerance);
    }

    [Test]
    public void Subword_tfidf_downweights_corpus_common_tokens()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus(
                [CommonToken, RareToken],
                [CommonToken, FirstOtherToken],
                [CommonToken, SecondOtherToken],
                [CommonToken, ThirdOtherToken]),
            TokenVectorWeighting.SubwordTfIdf);

        var vector = vectorSpace.CreateVector([CommonToken, RareToken]);

        vector.Weights[RareToken].ShouldBeGreaterThan(vector.Weights[CommonToken]);
        Magnitude(vector).ShouldBe(1, Tolerance);
    }

    [Test]
    public void Euclidean_distance_reaches_sparse_orthogonal_maximum_after_normalization()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus([CommonToken], [TopicToken]),
            TokenVectorWeighting.TermFrequency);
        var left = vectorSpace.CreateVector([CommonToken]);
        var right = vectorSpace.CreateVector([TopicToken]);

        left.EuclideanDistanceTo(right).ShouldBe(Math.Sqrt(2), Tolerance);
    }

    [Test]
    public void Euclidean_distance_prefers_matching_token_distribution()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus(
                [FirstQueryToken, FirstQueryToken, SecondQueryToken],
                [FirstDistractorToken, SecondDistractorToken]),
            TokenVectorWeighting.TermFrequency);
        var query = vectorSpace.CreateVector([FirstQueryToken, SecondQueryToken]);
        var matching = vectorSpace.CreateVector([FirstQueryToken, FirstQueryToken, SecondQueryToken]);
        var distractor = vectorSpace.CreateVector([FirstDistractorToken, SecondDistractorToken]);

        query.EuclideanDistanceTo(matching).ShouldBeLessThan(query.EuclideanDistanceTo(distractor));
    }

    [Test]
    public void Euclidean_distance_handles_empty_sparse_vectors_after_dot_product_shortcut()
    {
        var vectorSpace = TokenVectorSpace.Fit(
            Corpus([], [TopicToken]),
            TokenVectorWeighting.TermFrequency);
        var empty = vectorSpace.CreateVector([]);
        var topic = vectorSpace.CreateVector([TopicToken]);

        empty.EuclideanDistanceTo(empty).ShouldBe(0, Tolerance);
        empty.EuclideanDistanceTo(topic).ShouldBe(1, Tolerance);
        topic.EuclideanDistanceTo(empty).ShouldBe(1, Tolerance);
    }

    private static IReadOnlyList<IReadOnlyList<int>> Corpus(params int[][] documents)
    {
        return documents.Select(static document => (IReadOnlyList<int>)document).ToArray();
    }

    private static double Magnitude(TokenVector vector)
    {
        return Math.Sqrt(vector.Weights.Values.Sum(static weight => weight * weight));
    }
}
