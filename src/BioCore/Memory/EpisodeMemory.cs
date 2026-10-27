using System.Text.Json;
using BioComp.Core.Network;

namespace BioComp.Core.Memory;

/// <summary>A stored episode: a sparse hippocampal code plus symbolic context.</summary>
public sealed record Episode(
    int Id,
    float TimeMs,
    int[] CodeNeurons,
    string[] Tags,
    float Valence,
    float DopamineContext,
    float SerotoninContext);

/// <summary>
/// Hippocampal-like fast episodic store. Encoding captures the currently active sparse
/// code of a region (CA3) as the neurons with the highest rate since the last snapshot;
/// recall ranks by tag overlap, recency and valence (time-indexed, pattern-separated
/// by construction: codes are winner-take-all snapshots). Persistence: jsonl.
/// </summary>
public sealed class EpisodeMemory
{
    public const int CodeSize = 48;
    private readonly List<Episode> _episodes = [];
    private uint[] _lastCountsSnapshot = [];
    private float _lastSnapshotMs;
    private int _nextId;

    public IReadOnlyList<Episode> Episodes => _episodes;
    public int MaxEpisodes { get; set; } = 500;

    /// <summary>Snapshot-driven encode: rate is measured between snapshots of cumulative counters.</summary>
    public Episode? Encode(SpikingNetwork net, string regionName, string[] tags, float valence)
    {
        int region = -1;
        for (int i = 0; i < net.Regions.Count; i++)
            if (net.Regions[i].Name == regionName) { region = i; break; }
        if (region < 0) return null;

        var counts = net.Recorder.Counts;
        if (_lastCountsSnapshot.Length != counts.Length)
        {
            // first call: baseline is an all-zero net at time zero; rates then average
            // over the whole run so far, which is still a valid activity estimate
            _lastCountsSnapshot = new uint[counts.Length];
            _lastSnapshotMs = 0f;
        }
        float dtSec = MathF.Max(0.001f, (net.TimeMs - _lastSnapshotMs) / 1000f);
        var (start, count) = (net.Regions[region].Start, net.Regions[region].Count);

        // top-K rate neurons in the region = the sparse code
        var rates = new (int Neuron, float Rate)[count];
        for (int i = 0; i < count; i++)
            rates[i] = (start + i, (counts[start + i] - _lastCountsSnapshot[start + i]) / dtSec);
        Array.Sort(rates, (a, b) => b.Rate.CompareTo(a.Rate));
        float meanRate = 0f;
        for (int i = 0; i < count; i++) meanRate += rates[i].Rate;
        meanRate /= count;
        if (meanRate < 0.05f) return null; // nothing meaningful happening; do not store noise

        var code = new int[Math.Min(CodeSize, count)];
        for (int i = 0; i < code.Length; i++) code[i] = rates[i].Neuron;

        var ep = new Episode(_nextId++, net.TimeMs, code, tags, valence,
            net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine),
            net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin));
        _episodes.Add(ep);
        while (_episodes.Count > MaxEpisodes)
            _episodes.RemoveAt(0); // drop oldest; slow cortical store is the network itself
        _lastCountsSnapshot = (uint[])counts.Clone();
        _lastSnapshotMs = net.TimeMs;
        return ep;
    }

    /// <summary>Recall: rank by tag overlap x recency x valence.</summary>
    public List<Episode> Recall(IReadOnlyList<string> queryTags, int max = 5)
    {
        float Score(Episode e)
        {
            int overlap = 0;
            foreach (var q in queryTags)
                foreach (var t in e.Tags)
                    if (string.Equals(q, t, StringComparison.OrdinalIgnoreCase)) { overlap++; break; }
            float ageHr = MathF.Max(0.25f, e.TimeMs / 3_600_000f);
            return overlap * 2f + 1f / ageHr + MathF.Max(0f, e.Valence) * 0.5f;
        }
        return _episodes.OrderByDescending(Score).Take(max).ToList();
    }

    /// <summary>Episode selection for replay: biased toward emotionally salient episodes
    /// (high absolute valence, rewarding or aversive), random among the top third.</summary>
    public Episode? PickForReplay(Func<int, int> nextInt)
    {
        if (_episodes.Count == 0) return null;
        var candidates = _episodes
            .OrderByDescending(e => MathF.Abs(e.Valence))
            .Take(Math.Max(1, _episodes.Count / 3))
            .ToList();
        return candidates[nextInt(candidates.Count)];
    }

    public void Save(string path)
    {
        using var w = new StreamWriter(path);
        foreach (var e in _episodes)
            w.WriteLine(JsonSerializer.Serialize(e));
    }

    public static EpisodeMemory Load(string path)
    {
        var m = new EpisodeMemory();
        if (!File.Exists(path)) return m;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var e = JsonSerializer.Deserialize<Episode>(line);
            if (e is not null) m._episodes.Add(e);
        }
        if (m._episodes.Count > 0) m._nextId = m._episodes[^1].Id + 1;
        return m;
    }
}
