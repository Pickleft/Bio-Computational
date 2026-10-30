namespace BioComp.Core.Memory;

/// <summary>
/// Semantic memory built from lived experience: an associative concept graph
/// where tags that co-occur in encoded episodes strengthen their link ("fire
/// together, wire together" at the symbolic level; the associative-network
/// flavor of Collins and Quillian 1969, built the Hopfield way - from
/// co-occurrence statistics, not from rules). Recall can then SPREAD: a
/// thought about "humming" reaches "machine" because they were experienced
/// together, even when no single episode contains both query tags. Links decay
/// slowly so associations that stop being reinforced fade (use it or lose it).
/// Sim-thread only.
/// </summary>
public sealed class ConceptGraph
{
    /// <summary>Strength gained per co-occurrence.</summary>
    public float LearningStep { get; set; } = 0.25f;
    /// <summary>Maximum link strength.</summary>
    public float MaxStrength { get; set; } = 3f;
    /// <summary>Links weaker than this are pruned when compacting.</summary>
    public float PruneBelow { get; set; } = 0.15f;
    /// <summary>Strength above which a new association is worth announcing.</summary>
    public float AnnouncementThreshold { get; set; } = 0.45f;

    private readonly Dictionary<string, Dictionary<string, float>> _edges = [];
    private readonly Dictionary<string, float> _use = [];
    private int _sinceCompact;

    public int ConceptCount => _use.Count;
    public int LinkCount { get; private set; }

    /// <summary>
    /// Wire the tags of one encoded episode together. Returns the pair whose
    /// link just crossed the announcement threshold (a newly learned
    /// association), or null.
    /// </summary>
    public (string A, string B)? OnEpisode(string[] tags)
    {
        var distinct = tags.Where(t => t.Length > 1).Distinct().ToArray();
        (string, string)? learned = null;
        foreach (var t in distinct) _use[t] = _use.TryGetValue(t, out var u) ? u + 1f : 1f;
        for (int i = 0; i < distinct.Length; i++)
            for (int j = i + 1; j < distinct.Length; j++)
            {
                var (a, b) = (distinct[i], distinct[j]);
                if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
                if (!_edges.TryGetValue(a, out var row)) _edges[a] = row = [];
                float before = row.TryGetValue(b, out var s) ? s : 0f;
                float after = MathF.Min(MaxStrength, before + LearningStep);
                if (before == 0f) LinkCount++;
                row[b] = after;
                if (before < AnnouncementThreshold && after >= AnnouncementThreshold)
                    learned = (a, b); // the freshest crossing wins
            }
        if (++_sinceCompact > 500) { Compact(); _sinceCompact = 0; }
        return learned;
    }

    /// <summary>
    /// <summary>Read the directed link strength a→b, if wired (read-only query).</summary>
    public bool TryGetLink(string a, string b, out float strength)
    {
        strength = 0f;
        return _edges.TryGetValue(a, out var row) && row.TryGetValue(b, out strength);
    }

    /// Spread activation one hop from the seed concepts: the associates most
    /// strongly linked to what the brain is currently thinking. Returned in
    /// descending activation; associates inherit the strongest incoming link
    /// scaled by the seed's own usage frequency.
    /// </summary>
    public List<string> Spread(IReadOnlyList<string> seedTags, int max = 4)
    {
        var activation = new Dictionary<string, float>();
        foreach (var seed in seedTags)
        {
            if (!_edges.TryGetValue(seed, out var row)) continue;
            float seedUse = MathF.Log2(1f + (_use.TryGetValue(seed, out var u) ? u : 1f));
            foreach (var (other, strength) in row)
            {
                float a = strength * MathF.Min(1f, seedUse / 3f);
                if (activation.TryGetValue(other, out float cur)) activation[other] = MathF.Max(cur, a);
                else activation[other] = a;
            }
            // edges pointing INTO the seed (stored under the other node)
            foreach (var (node, rowIn) in _edges)
                if (rowIn.TryGetValue(seed, out float s) && !seedTags.Contains(node))
                {
                    float a = s * 0.8f * MathF.Min(1f, seedUse / 3f);
                    if (activation.TryGetValue(node, out float cur)) activation[node] = MathF.Max(cur, a);
                    else activation[node] = a;
                }
        }
        return [.. activation.OrderByDescending(kv => kv.Value)
            .Where(kv => kv.Value > PruneBelow && !seedTags.Contains(kv.Key))
            .Take(max).Select(kv => kv.Key)];
    }

    /// <summary>The strongest single associate of a concept (for narration).</summary>
    public string? StrongestAssociate(string tag)
    {
        float best = 0f; string? bestNode = null;
        if (_edges.TryGetValue(tag, out var outRow))
            foreach (var (other, s) in outRow)
                if (s > best) { best = s; bestNode = other; }
        foreach (var (node, inRow) in _edges)
            if (inRow.TryGetValue(tag, out float s) && s > best) { best = s; bestNode = node; }
        return bestNode;
    }

    /// <summary>Drop the weakest links so the graph stays sparse.</summary>
    private void Compact()
    {
        foreach (var key in _edges.Keys.ToList())
        {
            var weak = _edges[key].Where(kv => kv.Value < PruneBelow).Select(kv => kv.Key).ToList();
            foreach (var w in weak) { _edges[key].Remove(w); LinkCount--; }
            if (_edges[key].Count == 0) _edges.Remove(key);
        }
    }
}
