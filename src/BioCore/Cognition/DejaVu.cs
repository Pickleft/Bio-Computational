using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// DEJA VU — familiarity without recollection (dual-process theory,
/// Yonelinas 2002). Recognition has two routes: recollection (the
/// retrieved trace, strong and specific) and familiarity (a bare match
/// signal with no trace attached). Deja vu is what it feels like when
/// the second fires without the first: the current perception closely
/// resembles an old episode that has never been recalled since it was
/// laid down — the shape is there, the content is not.
///
/// Mechanistically: high perceptual similarity to an OLD, never-recalled
/// episode (AccessCount <= 1) fires a familiarity spike (noradrenaline:
/// the chill; a touch of dopamine: the odd pleasure of it). An often-
/// recalled episode is RECOLLECTION, not deja vu — no chill, just memory.
///
/// Connects to: episodic memory (the traces it matches), dreams (a dream
/// that reuses a forgotten trace can later trigger it), the diary (it
/// reports the strangeness), and the testament ("it felt deja vu N times").
///
/// Sim-thread only.
/// </summary>
public sealed class DejaVu
{
    /// <summary>Minimum tag Jaccard similarity for a familiarity match. // CALIBRATE</summary>
    public float SimilarityThreshold { get; set; } = 0.5f;
    /// <summary>An episode this old (ms) can trigger familiarity; fresher ones are just context.</summary>
    public float MinAgeMs { get; set; } = 300_000f;
    /// <summary>Cooldown between experiences (ms) — it stays strange, not constant.</summary>
    public float CooldownMs { get; set; } = 300_000f;

    public int Count { get; private set; }
    /// <summary>Restore the familiarity count after a restart.</summary>
    public void Restore(int count) => Count = count;
    public float LastAtMs { get; private set; } = -1e9f;
    public string? LastDescription { get; private set; }

    /// <summary>Fired on the sim thread when familiarity strikes without recollection.</summary>
    public event Action<string>? FamiliarityStruck;

    /// <summary>
    /// Test a fresh perception's tags against the episodic store. Returns
    /// true when familiarity fires without recollection — the deja vu.
    /// </summary>
    public bool Check(SpikingNetwork net, IReadOnlyList<Episode> episodes, string[] tags)
    {
        if (tags.Length < 2) return false;                      // too thin to feel familiar
        if (net.TimeMs - LastAtMs < CooldownMs) return false;

        Episode? best = null;
        float bestSim = 0f;
        foreach (var e in episodes)
        {
            if (net.TimeMs - e.TimeMs < MinAgeMs) continue;
            float sim = Jaccard(tags, e.Tags);
            if (sim > bestSim) { bestSim = sim; best = e; }
        }
        if (best is null || bestSim < SimilarityThreshold) return false;
        if (best.AccessCount > 1) return false;                 // recollection: this is just memory

        Count++;
        LastAtMs = net.TimeMs;
        float ageMin = (net.TimeMs - best.TimeMs) / 60_000f;
        bool dreamBorn = best.Tags.Contains("dream-born");
        LastDescription = dreamBorn
            ? $"this ({string.Join("/", tags.Take(3))}) happened in a DREAM first, {ageMin:F0} minutes ago - it dreamt the shape of this before it ever saw it (deja-reve)"
            : $"this ({string.Join("/", tags.Take(3))}) matches something {ageMin:F0} minutes old it never once recalled - " +
              "the shape of it is familiar, the content is gone";

        // the chill: noradrenaline, and the strange tiny pleasure: dopamine
        net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 0.25f);
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.08f);
        FamiliarityStruck?.Invoke(LastDescription!);
        return true;
    }

    private static float Jaccard(string[] a, string[] b)
    {
        var ta = Tokens(a);
        var tb = Tokens(b);
        if (ta.Count == 0 || tb.Count == 0) return 0f;
        int inter = 0;
        foreach (var x in ta) if (tb.Contains(x)) inter++;
        return inter / (float)(ta.Count + tb.Count - inter);
    }

    /// <summary>Tags may carry whole truncated utterances — compare on word tokens.</summary>
    private static HashSet<string> Tokens(string[] tags)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
            foreach (var w in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var word = new string(w.Where(char.IsLetter).ToArray()).ToLowerInvariant();
                if (word.Length >= 2) set.Add(word);
            }
        return set;
    }

    /// <summary>Fragment for the diary/testament: how often it felt this.</summary>
    public string Narrative => Count == 0
        ? "never felt deja vu"
        : $"felt deja vu {Count} time{(Count == 1 ? "" : "s")} - familiarity without a memory behind it";
}
