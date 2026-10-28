namespace BioComp.Core.Cognition;

/// <summary>
/// Per-person social bonds with depth, not just visit counts. Strength grows
/// with visits and the prosodic warmth of each contact, decays slowly with
/// time apart. Strength is the social reward gradient: recognizing a strong
/// bond pays more dopamine than a weak one (Schultz 1997 reward scaling),
/// absence of a strong bond is a negative prediction error, and loneliness
/// seeks the STRONGEST bond specifically - missing someone in particular,
/// not company in general. Sim-thread only.
/// </summary>
public sealed class SocialBondSystem
{
    public sealed record Bond(string Id, int Visits, float Warmth, float LastSeenMs, float Strength);

    private readonly Dictionary<string, Bond> _bonds = [];

    /// <summary>How much of the bond survives per day apart.</summary>
    public float DecayPerDay { get; set; } = 0.5f;     // CALIBRATE
    public int Count => _bonds.Count;

    /// <summary>A contact with this person: warmth from the prosody of the exchange.</summary>
    public void OnContact(string id, float warmth, float nowMs)
    {
        warmth = Math.Clamp(warmth, 0f, 1f);
        if (!_bonds.TryGetValue(id, out var b))
        {
            _bonds[id] = new Bond(id, 1, warmth, nowMs, 0.2f);
            return;
        }
        float warmthEma = b.Warmth + (warmth - b.Warmth) / MathF.Min(6f, b.Visits);
        _bonds[id] = b with { Visits = b.Visits + 1, Warmth = warmthEma, LastSeenMs = nowMs };
    }

    /// <summary>Bond strength: visits (log-compressed) x warmth x recency survival.</summary>
    public float Strength(string id, float nowMs)
    {
        if (!_bonds.TryGetValue(id, out var b)) return 0f;
        float visits = MathF.Log2(1f + b.Visits) / 3f;            // saturating familiarity
        float days = MathF.Max(0f, (nowMs - b.LastSeenMs) / 86_400_000f);
        float survival = MathF.Pow(1f - DecayPerDay, days);
        return Math.Clamp(visits * (0.35f + 0.65f * b.Warmth) * survival, 0f, 1f);
    }

    /// <summary>The person the brain misses most right now (strongest live bond).</summary>
    public Bond? Strongest(float nowMs)
    {
        Bond? best = null; float bestS = -1f;
        foreach (var b in _bonds.Values)
        {
            float s = Strength(b.Id, nowMs);
            if (s > bestS) { bestS = s; best = b; }
        }
        return bestS > 0.25f ? best : null;
    }

    public IReadOnlyDictionary<string, Bond> Bonds => _bonds;

    /// <summary>Dopamine for recognition, scaled by bond strength: seeing someone
    /// you deeply know is a bigger reward than seeing a stranger (and a missed
    /// expectation is handled as a negative prediction error by the caller).</summary>
    public float RecognitionReward(string id, float nowMs) =>
        0.10f + 0.35f * Strength(id, nowMs);
}
