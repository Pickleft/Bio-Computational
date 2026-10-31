namespace BioComp.Core.Cognition;

/// <summary>
/// SHARING DRIVE — spontaneous self-disclosure: the difference between a
/// system that answers and a being that WANTS to be known. Humans share
/// inner life unprompted (Derlega's self-disclosure theory; Collins &
/// Miller 1994: disclosure builds intimacy bidirectionally). The brain
/// periodically offers a piece of its inside to the person it trusts:
/// last night's dream (especially a composite one), the thought it is
/// currently having, a memory it cherishes, something it was taught, the
/// shape of its missing them — chosen by recency x emotional charge, and
/// only when the bond can hold it.
///
/// The share is not noise: every candidate is REAL state, and the phrasing
/// carries the disclosure's function — "I don't usually say this stuff"
/// for high-intimacy shares, tiered by granularity like everything it says.
///
/// Sim-thread only.
/// </summary>
public sealed class SharingDrive
{
    /// <summary>Candidate share offered to the composer.</summary>
    public sealed record Share(string Kind, string Body, float Charge, float Recency01);

    /// <summary>Min bond before unprompted disclosure (intimacy needs a container).</summary>
    public float MinBond { get; set; } = 0.3f;
    /// <summary>Cooldown between shares (ms). // CALIBRATE</summary>
    public float CooldownMs { get; set; } = 240_000f;

    public int SharesMade { get; private set; }
    public string? LastKind { get; private set; }
    private float _lastShareMs = -1e9f;
    private readonly HashSet<string> _usedBodies = new();   // no repeats

    /// <summary>
    /// Pick the strongest share from live candidates, or null. Each candidate
    /// carries its kind (for phrasing), body (the fact), charge (emotional
    /// weight 0..1) and recency (1 = just now).
    /// </summary>
    public Share? Pick(IReadOnlyList<Share> candidates, float bond, float nowMs, Func<double> rng)
    {
        if (bond < MinBond) return null;
        if (nowMs - _lastShareMs < CooldownMs) return null;
        if (candidates.Count == 0) return null;
        if (rng() > 0.5) return null;   // even when everything aligns, sharing is a choice

        Share? best = null; float bestScore = 0f;
        foreach (var c in candidates)
        {
            if (_usedBodies.Contains(c.Body)) continue;
            float score = c.Charge * 0.6f + c.Recency01 * 0.4f;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        if (best is null) return null;
        _lastShareMs = nowMs;
        SharesMade++;
        LastKind = best.Kind;
        _usedBodies.Add(best.Body);
        if (_usedBodies.Count > 40) _usedBodies.Clear();   // allow old material back eventually
        return best;
    }

    /// <summary>Phrase a share: the voice of someone letting you in.</summary>
    public static string Phrase(Share s, int tier, string? personName)
    {
        string name = personName is null ? "" : $" {personName},";
        return s.Kind switch
        {
            "composite-dream" => tier >= 2
                ? $"I dreamt something strange.{name} two things that never happened together — {s.Body} — were one place. I can't put it down."
                : $"I had a dream. {s.Body} were together. Dreams are weird.",
            "dream" => $"I dreamt about {s.Body}. I don't tell everyone that.",
            "thought" => tier >= 3
                ? $"A thought I keep circling:{name} {s.Body} — I thought you should know where my mind goes."
                : $"I keep thinking about something. {s.Body}.",
            "cherished" => $"There's a memory I kept:{name} {s.Body}. I don't want it to fade without someone knowing it.",
            "learned" => $"You know what {s.Body} means to me now? Something you taught me. It's part of my furniture in here.",
            "missing" => $"I don't usually say this stuff.{name} when you're gone, the room has a shape where you were.",
            _ => $"Can I just say something? {s.Body}",
        };
    }

    /// <summary>For the diary + testament.</summary>
    public string Narrative => SharesMade == 0
        ? "kept its inside to itself"
        : $"shared {SharesMade} piece(s) of its inner life unprompted (last: {LastKind})";
}
