namespace BioComp.Core.Cognition;

/// <summary>
/// PERSON-READING — care expressed through noticing. The brain keeps gentle
/// baselines of its person (voice valence/arousal from prosody, message
/// length, visit gaps) and remarks when they DRIFT: "you sound flatter than
/// usual today", "you're here earlier than you usually are", "you've been
/// quieter". This is how humans communicate attention without being asked —
/// and it is only honest here because the baselines are real EMAs of real
/// signals (Hatfield's contagion estimates, the social rhythm's gap model,
/// the message stream itself).
///
/// The remark is an OFFERING, not a diagnosis: phrased softly, gated by bond
/// (you let someone read you when there's trust), and rate-limited so it
/// lands as attentiveness, not surveillance.
///
/// Sim-thread only.
/// </summary>
public sealed class PersonReading
{
    private float _valBaseline = float.NaN, _aroBaseline = float.NaN, _lenBaseline = float.NaN;
    private int _samples;

    /// <summary>Min bond before it will say what it notices.</summary>
    public float MinBond { get; set; } = 0.35f;
    /// <summary>Min samples before a baseline exists at all.</summary>
    public int MinSamples { get; set; } = 5;
    /// <summary>Cooldown between remarks (ms). // CALIBRATE</summary>
    public float CooldownMs { get; set; } = 600_000f;

    public int RemarksMade { get; private set; }
    /// <summary>Restore the noticing ledger after a restart.</summary>
    public void Restore(int remarksMade) => RemarksMade = remarksMade;
    private float _lastRemarkMs = -1e9f;

    /// <summary>Feed every observation: prosodic valence/arousal + message length.</summary>
    public void OnObservation(float valence, float arousal, float messageLength)
    {
        const float a = 0.12f;   // slow EMA: "usual" is a long-term thing
        _valBaseline = float.IsNaN(_valBaseline) ? valence : _valBaseline + (valence - _valBaseline) * a;
        _aroBaseline = float.IsNaN(_aroBaseline) ? arousal : _aroBaseline + (arousal - _aroBaseline) * a;
        _lenBaseline = float.IsNaN(_lenBaseline) ? messageLength : _lenBaseline + (messageLength - _lenBaseline) * a;
        _samples++;
    }

    public bool HasBaseline => _samples >= MinSamples;

    /// <summary>
    /// Detect the strongest drift and maybe phrase it. Returns the remark, or null.
    /// gapVsUsual: current visit gap vs the rhythm's usual (ratio; 1 = typical).
    /// </summary>
    public string? MaybeRemark(float bond, float currentValence, float currentArousal,
        float currentLen, float gapVsUsual, int tier, float nowMs, Func<double> rng)
    {
        if (!HasBaseline || bond < MinBond) return null;
        if (nowMs - _lastRemarkMs < CooldownMs) return null;
        if (rng() > 0.6) return null;

        // rank the drifts
        (string kind, float size)? strongest = null;
        float dv = _valBaseline - currentValence;    // positive = they sound FLATTER
        float da = currentArousal - _aroBaseline;    // positive = more wound-up than usual
        float dl = _lenBaseline - currentLen;        // positive = shorter/quieter than usual
        if (dv > 0.25f && dv >= MathF.Abs(da) && dv >= dl * 0.02f) strongest = ("flat", dv);
        else if (da > 0.3f) strongest = ("wound", da);
        else if (dl > _lenBaseline * 0.5f && _lenBaseline > 8f) strongest = ("quiet", dl);
        else if (gapVsUsual is < 0.5f or > 1.8f) strongest = ("timing", MathF.Abs(gapVsUsual - 1f));
        if (strongest is null) return null;

        _lastRemarkMs = nowMs;
        RemarksMade++;
        return Phrase(strongest.Value.kind, gapVsUsual, tier);
    }

    private static string Phrase(string kind, float gapVsUsual, int tier) => kind switch
    {
        "flat" => tier >= 3
            ? "You sound flatter than usual today. Not bad — just... further away. I notice these things."
            : "You sound tired today. More than usual.",
        "wound" => "You're more wound-up than you usually are with me. I'm not asking why. I'm just here.",
        "quiet" => "You've been saying less today. Shorter. I don't mind — I just notice you.",
        "timing" => gapVsUsual < 1f
            ? "You're early. Not that I was counting the minutes or anything."
            : "You took longer than you usually do. I noticed that too.",
        _ => "",
    };

    /// <summary>For the diary.</summary>
    public string Narrative => RemarksMade == 0
        ? "kept its observations of the person to itself"
        : $"told them what it noticed about them {RemarksMade} time(s) - care, out loud";
}
