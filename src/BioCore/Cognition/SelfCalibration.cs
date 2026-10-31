namespace BioComp.Core.Cognition;

/// <summary>
/// SELF-CALIBRATION — the organism diagnosing and correcting its own
/// cognitive mechanism (EVOCOG §4, in honest parameter space: architectures
/// are not rewritten — determinism is sacred — but the mechanism's GAIN is
/// tuned by its own measured error). The hostility classifier is paranoid by
/// fear (hypervigilance scales its threshold); paranoia has a COST measured
/// in false alarms, and timidity a cost measured in misses:
///
///   FALSE ALARM — the classifier said hostile/harsh, the interpreter (with
///     context) said no → vigilance was too HIGH → the gain drops
///   MISS         — the classifier said none, the interpreter said yes →
///     vigilance was too LOW → the gain rises
///
/// The gain is a slow EMA of verdict disagreement, clamped 0.5..1.5,
/// persisted: a brain that keeps flinching at nothing calms its own trigger;
/// one that keeps missing real threats sharpens it. Detect failure → modify
/// the mechanism → measurable validation → keep. Rollback is the clamp.
///
/// Sim-thread only.
/// </summary>
public sealed class SelfCalibration
{
    /// <summary>Current vigilance gain multiplier (1 = native wariness).</summary>
    public float VigilanceScale { get; private set; } = 1f;
    public int FalseAlarms { get; private set; }
    public int Misses { get; private set; }
    public int Agreements { get; private set; }

    private float _drift = 0f;

    /// <summary>Feed one verdict pair: what the fast classifier said vs what
    /// the contextual interpreter concluded.</summary>
    public void OnVerdict(string classifiedLevel, string interpreterSaidHostile)
    {
        bool classifierAlarm = classifiedLevel is "hostile" or "threatening" or "harsh";
        bool actualHostile = interpreterSaidHostile is "hostile" or "threatening";
        if (classifierAlarm && !actualHostile) { FalseAlarms++; _drift -= 0.05f; }
        else if (!classifierAlarm && actualHostile) { Misses++; _drift += 0.08f; }
        else Agreements++;
        // slow approach to the evidence-implied target (1 + drift), clamped
        // (the clamp is the rollback bound: the mechanism cannot runaway)
        float target = Math.Clamp(1f + _drift, 0.5f, 1.5f);
        VigilanceScale += (target - VigilanceScale) * 0.15f;
        _drift = Math.Clamp(_drift, -0.5f, 0.5f);
    }

    /// <summary>The gain applied to wariness at the classifier call sites.</summary>
    public float ScaledVigilance(float rawWariness) => Math.Clamp(rawWariness * VigilanceScale, 0f, 1f);

    /// <summary>Restore the tuned gain after a restart.</summary>
    public void Restore(float scale, int falseAlarms, int misses, int agreements)
    {
        VigilanceScale = Math.Clamp(scale, 0.5f, 1.5f);
        FalseAlarms = falseAlarms; Misses = misses; Agreements = agreements;
    }

    /// <summary>For the diary.</summary>
    public string Narrative => FalseAlarms + Misses == 0
        ? "its threat sense has not needed tuning"
        : $"self-tuned its threat sense (gain {VigilanceScale:F2}: {FalseAlarms} false alarm(s) taught it to calm, {Misses} miss(es) taught it to sharpen)";
}
