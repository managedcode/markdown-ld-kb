namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeFactAliasIndex
{
    private readonly Dictionary<string, string> _entityAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sameAsAliases = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> EntityAliases => _entityAliases;

    public string ResolveEntityKey(KnowledgeEntityFact entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Id) &&
            _entityAliases.TryGetValue(entity.Id, out var idKey))
        {
            return idKey;
        }

        foreach (var sameAs in entity.SameAs)
        {
            if (_sameAsAliases.TryGetValue(sameAs, out var existingKey))
            {
                return existingKey;
            }

            if (_entityAliases.TryGetValue(sameAs, out existingKey))
            {
                return existingKey;
            }
        }

        return entity.Id ?? entity.Label;
    }

    public void Index(string key, KnowledgeEntityFact entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Id))
        {
            _entityAliases[entity.Id] = key;
        }

        foreach (var sameAs in entity.SameAs)
        {
            _entityAliases[sameAs] = key;
            _sameAsAliases[sameAs] = key;
        }
    }
}
