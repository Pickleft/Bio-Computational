namespace BioComp.Core.Cognition;

/// <summary>
/// THE PHRASEBOOK: reply scaffolds that GROW. Every family starts with its
/// born (authored) seeds; when a line the brain actually said lands well —
/// the person laughed, or said they loved it — the line is learned into the
/// family it belonged to, and future draws can come from the brain's own
/// earned phrases instead of only its birth vocabulary. The words stay
/// authored (nothing is invented here); which words SURVIVE is learned.
/// Sim-thread only.
/// </summary>
public sealed class PhraseBook
{
    private readonly Dictionary<string, List<string>> _families = new();
    public const int MaxPerFamily = 24;

    public IReadOnlyList<string> Family(string name) => _families.TryGetValue(name, out var v) ? v : [];

    /// <summary>Seed the born variants (idempotent).</summary>
    public void Seed(string family, params string[] seeds)
    {
        if (!_families.TryGetValue(family, out var v)) _families[family] = v = [];
        foreach (var s in seeds)
            if (!v.Contains(s)) v.Add(s);
    }

    /// <summary>A line it said landed well — earn it into the family.</summary>
    public void Learn(string family, string phrase)
    {
        phrase = phrase.Trim();
        if (phrase.Length < 4 || phrase.Length > 120) return;
        if (!_families.TryGetValue(family, out var v)) _families[family] = v = [];
        if (v.Contains(phrase)) return;
        v.Add(phrase);
        if (v.Count > MaxPerFamily) v.RemoveAt(0);   // the oldest seed fades first
    }

    /// <summary>Draw a phrase. The caller passes its born seed count —
    /// entries beyond that are EARNED lines, weighted 65/35 toward what the
    /// brain became over where it started.</summary>
    public string Pick(string family, int bornCount, Func<double> rng)
    {
        if (!_families.TryGetValue(family, out var v) || v.Count == 0) return "";
        int born = Math.Min(bornCount, v.Count);
        if (born < v.Count && rng() < 0.65) return v[born + (int)(rng() * (v.Count - born))];
        return v[(int)(rng() * born)];
    }

    public string[] Save() => _families.SelectMany(kv => kv.Value.Select(p => kv.Key + "|" + p)).ToArray();

    public void Restore(string[]? rows)
    {
        if (rows is null) return;
        foreach (var row in rows)
        {
            int i = row.IndexOf('|');
            if (i > 0 && i < row.Length - 1) Learn(row[..i], row[(i + 1)..]);
        }
    }
}
