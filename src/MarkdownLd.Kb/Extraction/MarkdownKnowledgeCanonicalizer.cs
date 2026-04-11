namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownKnowledgeCanonicalizer
{
    private static readonly Dictionary<string, int> TypePriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["schema:Person"] = 5,
        ["schema:Organization"] = 5,
        ["schema:SoftwareApplication"] = 5,
        ["schema:CreativeWork"] = 4,
        ["schema:Article"] = 4,
        ["schema:Thing"] = 1,
    };

    public static IReadOnlyList<MarkdownKnowledgeEntity> CanonicalizeEntities(
        IEnumerable<MarkdownKnowledgeEntityCandidate> candidates)
    {
        var groups = new List<EntityGroup?>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            MergeCandidate(candidate, groups, index);
        }

        return groups
            .Where(group => group is not null)
            .Select(group => group!.ToEntity())
            .OrderBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<MarkdownKnowledgeAssertion> CanonicalizeAssertions(
        IEnumerable<MarkdownKnowledgeAssertionCandidate> candidates,
        IReadOnlyDictionary<string, string> aliasToEntityId)
    {
        var best = new Dictionary<(string Subject, string Predicate, string Object), MarkdownKnowledgeAssertion>(new AssertionKeyComparer());

        foreach (var candidate in candidates)
        {
            var subjectId = ResolveEntityId(candidate.Subject, aliasToEntityId);
            var objectId = ResolveEntityId(candidate.Object, aliasToEntityId);
            var assertion = new MarkdownKnowledgeAssertion
            {
                SubjectId = subjectId,
                Predicate = candidate.Predicate,
                ObjectId = objectId,
                Confidence = candidate.Confidence,
                Source = candidate.Source,
            };

            var key = (subjectId, candidate.Predicate, objectId);
            if (!best.TryGetValue(key, out var existing) || assertion.Confidence > existing.Confidence)
            {
                best[key] = assertion;
            }
        }

        return best.Values
            .OrderBy(item => item.SubjectId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Predicate, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ObjectId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyDictionary<string, string> BuildAliasLookup(
        string articleId,
        string articleTitle,
        IEnumerable<MarkdownKnowledgeEntity> entities,
        IEnumerable<MarkdownTopic> about)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MarkdownKnowledgeIds.Slugify(articleId)] = articleId,
            [articleId] = articleId,
        };

        if (!string.IsNullOrWhiteSpace(articleTitle))
        {
            lookup[MarkdownKnowledgeIds.Slugify(articleTitle)] = articleId;
            lookup[articleTitle] = articleId;
        }

        foreach (var entity in entities)
        {
            AddLookup(lookup, entity.Id, entity.Id);
            AddLookup(lookup, entity.Label, entity.Id);
            foreach (var sameAs in entity.SameAs)
            {
                AddLookup(lookup, sameAs, entity.Id);
            }
        }

        foreach (var topic in about)
        {
            var topicId = MarkdownKnowledgeIds.BuildEntityId(topic.Label);
            AddLookup(lookup, topic.Label, topicId);
            if (!string.IsNullOrWhiteSpace(topic.SameAs))
            {
                AddLookup(lookup, topic.SameAs!, topicId);
            }
        }

        return lookup;
    }

    private static void MergeCandidate(
        MarkdownKnowledgeEntityCandidate candidate,
        List<EntityGroup?> groups,
        Dictionary<string, int> index)
    {
        var keys = GetKeys(candidate).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matches = keys
            .Select(key => index.TryGetValue(key, out var existing) ? existing : (int?)null)
            .Where(existing => existing.HasValue)
            .Select(existing => existing!.Value)
            .Distinct()
            .OrderBy(existing => existing)
            .ToArray();

        var groupIndex = matches.Length == 0 ? groups.Count : matches[0];
        if (matches.Length == 0)
        {
            groups.Add(new EntityGroup());
        }
        else if (matches.Length > 1)
        {
            for (var i = 1; i < matches.Length; i++)
            {
                var mergeIndex = matches[i];
                groups[groupIndex]!.MergeGroup(groups[mergeIndex]!);
                groups[mergeIndex] = null;
            }
        }

        var group = groups[groupIndex]!;
        group.Merge(candidate);

        foreach (var key in group.Keys)
        {
            index[key] = groupIndex;
        }
    }

    private static IEnumerable<string> GetKeys(MarkdownKnowledgeEntityCandidate candidate)
    {
        var canonicalId = MarkdownKnowledgeIds.BuildEntityId(candidate.Label);
        yield return canonicalId;
        yield return candidate.Label;

        foreach (var sameAs in candidate.SameAs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return sameAs;
        }
    }

    private static string ResolveEntityId(string value, IReadOnlyDictionary<string, string> aliasToEntityId)
    {
        if (aliasToEntityId.TryGetValue(value, out var entityId))
        {
            return entityId;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) || value.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return aliasToEntityId.TryGetValue(MarkdownKnowledgeIds.Slugify(value), out entityId)
            ? entityId
            : MarkdownKnowledgeIds.BuildEntityId(value);
    }

    private static void AddLookup(Dictionary<string, string> lookup, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            lookup.TryAdd(key, value);
            lookup.TryAdd(MarkdownKnowledgeIds.Slugify(key), value);
        }
    }

    private sealed class EntityGroup
    {
        private string? _label;
        private string _type = "schema:Thing";
        private readonly HashSet<string> _sameAs = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);

        public void Merge(MarkdownKnowledgeEntityCandidate candidate)
        {
            _label ??= candidate.Label;
            _sameAs.UnionWith(candidate.SameAs.Where(value => !string.IsNullOrWhiteSpace(value)));
            _keys.Add(MarkdownKnowledgeIds.BuildEntityId(candidate.Label));
            _keys.Add(candidate.Label);
            foreach (var sameAs in candidate.SameAs.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                _keys.Add(sameAs);
            }

            var currentPriority = TypePriority.TryGetValue(_type, out var priority) ? priority : 0;
            var candidatePriority = TypePriority.TryGetValue(candidate.Type, out var candidateTypePriority) ? candidateTypePriority : 0;
            if (candidatePriority > currentPriority)
            {
                _type = candidate.Type;
            }
        }

        public void MergeGroup(EntityGroup other)
        {
            if (other._label is not null && _label is null)
            {
                _label = other._label;
            }

            _sameAs.UnionWith(other._sameAs);
            _keys.UnionWith(other._keys);

            var currentPriority = TypePriority.TryGetValue(_type, out var priority) ? priority : 0;
            var otherPriority = TypePriority.TryGetValue(other._type, out var otherTypePriority) ? otherTypePriority : 0;
            if (otherPriority > currentPriority)
            {
                _type = other._type;
            }
        }

        public IReadOnlyCollection<string> Keys => _keys;

        public MarkdownKnowledgeEntity ToEntity()
        {
            var label = string.IsNullOrWhiteSpace(_label) ? "item" : _label!;
            return new MarkdownKnowledgeEntity
            {
                Id = MarkdownKnowledgeIds.BuildEntityId(label),
                Label = label,
                Type = _type,
                SameAs = _sameAs.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            };
        }
    }

    private sealed class AssertionKeyComparer : IEqualityComparer<(string Subject, string Predicate, string Object)>
    {
        public bool Equals((string Subject, string Predicate, string Object) x, (string Subject, string Predicate, string Object) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Subject, y.Subject)
               && StringComparer.OrdinalIgnoreCase.Equals(x.Predicate, y.Predicate)
               && StringComparer.OrdinalIgnoreCase.Equals(x.Object, y.Object);

        public int GetHashCode((string Subject, string Predicate, string Object) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Subject, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Predicate, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Object, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
