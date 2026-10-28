using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// Allostasis - regulation in ANTICIPATION of need rather than reaction to it
/// (Sterling 2012; interoceptive inference after Barrett 2017 and Craig 2009):
/// the brain tracks its body's energy trajectory (battery slope through the
/// interoception channel), predicts when reserves run low, and shifts state
/// BEFORE the deficit arrives - preparatory melatonin (early sleepiness),
/// dialed-down histamine (subdued wakefulness). Interoceptive prediction
/// error (body behaving differently than predicted) is the raw material of
/// malaise and vitality in the feelings layer. Sim-thread only; body samples
/// arrive as events.
/// </summary>
public sealed class BodyBudget
{
    /// <summary>Reserve level below which action must be taken (percent).</summary>
    public float ReserveFloorPercent { get; set; } = 20f;
    /// <summary>How strongly anticipated deficit advances sleepiness.</summary>
    public float AnticipationGain { get; set; } = 0.08f;   // CALIBRATE

    public float PredictedDeficitMin { get; private set; } = float.MaxValue;
    /// <summary>EMA of |actual - predicted| body readings: interoceptive error.</summary>
    public float InteroceptiveError { get; private set; }

    private float _batteryEma = 100f, _batterySlopePerMin;
    private float _predictedNext;
    private bool _primed;

    /// <summary>Feed one interoception sample (battery percent, load percent).</summary>
    public void OnBody(float batteryPercent, float cpuLoadPercent, float timeSec)
    {
        if (!_primed) { _batteryEma = batteryPercent; _predictedNext = batteryPercent; _primed = true; return; }
        // prediction error of the body itself: how surprising is this reading?
        float err = MathF.Abs(batteryPercent - _predictedNext);
        InteroceptiveError += (MathF.Min(err, 50f) - InteroceptiveError) / 20f;
        _batteryEma += (batteryPercent - _batteryEma) / 10f;
        // slope from a slow EMA difference over a 60 s window
        _batterySlopePerMin = _batterySlopePerMin * 0.9f
            + (batteryPercent - _batteryEma) * 0.1f * 60f;
        if (_batterySlopePerMin < -0.05f)
            PredictedDeficitMin = (_batteryEma - ReserveFloorPercent) / -_batterySlopePerMin;
        else
            PredictedDeficitMin = float.MaxValue;
        _predictedNext = _batteryEma + _batterySlopePerMin; // one minute ahead
    }

    /// <summary>Anticipatory regulation: advance sleepiness before the deficit,
    /// and let interoceptive error register as malaise.</summary>
    public void Tick(SpikingNetwork net)
    {
        if (PredictedDeficitMin < 30f)
        {
            float urgency = 1f - MathF.Min(1f, PredictedDeficitMin / 30f);
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Melatonin, AnticipationGain * urgency);
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Histamine, -0.04f * urgency);
        }
        if (InteroceptiveError > 15f)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin, -0.03f);
    }

    /// <summary>For the interpreter: the body's story in one clause.</summary>
    public string Narrative =>
        PredictedDeficitMin < 30f
            ? $"reserves falling; exhaustion in about {PredictedDeficitMin:F0} minutes if this continues"
            : InteroceptiveError > 15f ? "the body feels wrong, off its predictions" : "body steady";
}
