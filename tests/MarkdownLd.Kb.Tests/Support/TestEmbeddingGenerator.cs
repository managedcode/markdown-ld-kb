using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

internal sealed class TestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly string[] NotificationTerms =
    [
        "notification",
        "notifications",
        "notify",
        "alert",
        "alerts",
        "delivery",
        "inbox",
        "нотиф",
        "сповіщ",
        "повідомл",
    ];

    private static readonly string[] TreeTerms =
    [
        "tree",
        "parent",
        "father",
        "mother",
        "person",
        "genealogy",
        "дерев",
        "батьк",
        "мати",
        "родин",
    ];

    private static readonly string[] BillingTerms =
    [
        "billing",
        "invoice",
        "payment",
        "payments",
        "рахун",
        "оплат",
    ];

    public object? GetService(Type serviceType, object? serviceKey)
    {
        return serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>) ? this : null;
    }

    public void Dispose()
    {
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = values
            .Select(CreateEmbedding)
            .ToArray();

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    private static Embedding<float> CreateEmbedding(string value)
    {
        return new Embedding<float>(CreateVector(value));
    }

    private static ReadOnlyMemory<float> CreateVector(string value)
    {
        var normalized = value.ToLowerInvariant();
        var vector = new float[3];

        if (ContainsAny(normalized, NotificationTerms))
        {
            vector[0] = 1;
        }

        if (ContainsAny(normalized, TreeTerms))
        {
            vector[1] = 1;
        }

        if (ContainsAny(normalized, BillingTerms))
        {
            vector[2] = 1;
        }

        return vector;
    }

    private static bool ContainsAny(string value, IEnumerable<string> candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.Ordinal));
    }
}
