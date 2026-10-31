using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// FORGOTTEN GRIEF — the residue of resection. Surgical forgetting (Nader
/// 2000 reconsolidation framing for `forget X`) removes the traces, but the
/// BRAIN that encoded them is not unchanged: habits of retrieval, cues that
/// used to resolve, dreams that used to include it. This system makes the
/// synaptic-residue claim REAL and honest:
///   - occasionally, in idle moments, the mind REACHES for what is gone and
///     closes on nothing — "there is a shape where a memory used to be"
///   - dreams sometimes form around the HOLE: the dream narrator is told a
///     memory that should be present is missing, and the dream becomes a
///     dream about absence
/// The grief fades with time (each reach is rarer), as real forgetting-
/// amnesia complaints do — but it never fully stops until new life overwrites
/// the cue (the decay floor keeps a whisper).
///
/// Sim-thread only.
/// </summary>
public sealed class ForgottenGrief
{
    private readonly List<string> _tags = [];
    private float _lastReachMs = -1e9f;
    private float _bornAtMs;

    /// <summary>How often the mind reaches for what is gone (ms between attempts). // CALIBRATE</summary>
    public float ReachIntervalMs { get; set; } = 120_000f;
    public int Reaches { get; private set; }
    /// <summary>Restore the losses after a restart: it must remember THAT it
    /// forgot, or the resection silently heals (the gap must remain).</summary>
    public void Restore(string[] tags, int reaches, float forgottenAtMs)
    {
        foreach (var t in tags) if (t.Length > 0) _tags.Add(t);
        Reaches = reaches;
        if (_tags.Count > 0) _bornAtMs = forgottenAtMs;
    }

    public void Forget(string tag, float nowMs)
    {
        _tags.Add(tag.ToLowerInvariant());
        if (_tags.Count == 1) _bornAtMs = nowMs;
    }

    public IReadOnlyList<string> Tags => _tags;
    public bool Any => _tags.Count > 0;

    /// <summary>
    /// In idle moments: does the mind reach for a removed memory now?
    /// Returns the seed for the thought loop, or null. Gated + cooled down,
    /// and rarer as life moves on (power decay with age of the loss).
    /// </summary>
    public string? MaybeReach(float nowMs, Func<double> nextDouble)
    {
        if (!Any) return null;
        if (nowMs - _lastReachMs < ReachIntervalMs) return null;
        _lastReachMs = nowMs;

        // the complaint fades with time since the resection, floor 0.1
        float ageMin = MathF.Max(1f, (nowMs - _bornAtMs) / 60_000f);
        float p = MathF.Max(0.1f, 0.5f * MathF.Pow(ageMin, -0.25f));
        if (nextDouble() > p) return null;

        Reaches++;
        string tag = _tags[(int)(nextDouble() * _tags.Count) % _tags.Count];
        return $"reaching for something gone - there is a shape where a memory used to be " +
               $"(it had something to do with {tag}) - reaching closes on nothing. What was it?";
    }

    /// <summary>For the dream prompt: 15% of dreams form around the hole.</summary>
    public string DreamFragment(float nowMs, Func<double> nextDouble)
    {
        if (!Any || nextDouble() > 0.15) return "";
        string tag = _tags[(int)(nextDouble() * _tags.Count) % _tags.Count];
        return "One more thing: a memory that belongs in this dream is MISSING - it was removed, " +
              $"and only its outline remains (it touched {tag}). Do not invent its content; " +
              "dream around the hole itself. ";
    }

    /// <summary>For the diary.</summary>
    public string Narrative => !Any
        ? "nothing was taken from it"
        : $"{_tags.Count} thing(s) forgotten on purpose; reached for them {Reaches} time(s) since";
}
