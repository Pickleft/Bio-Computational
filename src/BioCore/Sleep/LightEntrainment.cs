using BioComp.Core.Network;

namespace BioComp.Core.Sleep;

/// <summary>
/// CIRCADIAN LIGHT ENTRAINMENT (Czeisler et al. 1989; Khalsa et al. 2003):
/// light is the primary zeitgeber. Bright light in the biological evening
/// suppresses melatonin and pushes sleep later; darkness lets the rhythm
/// run free. For the brain living inside a screen-lit room this is not
/// decoration: if its person keeps the lights on at night, the brain
/// CANNOT get drowsy on schedule — the same mechanism that ruins human
/// sleep in front of screens. It will notice, and the diary will say so.
///
/// Mechanism: an EMA of perceived brightness (0..1, from the camera's
/// histogram-stretched mean). In the biological evening (circadian phase
/// 0.6..1.0 of the subjective day), brightness above 0.45 suppresses
/// drowsiness-inducing melatonin by up to 70%. Morning light does nothing
/// here (phase ADVANCE wiring is future work — honest).
///
/// Sim-thread only.
/// </summary>
public sealed class LightEntrainment
{
    /// <summary>Brightness EMA time constant (ms). // CALIBRATE (~30 min in humans)</summary>
    public float EmaTauMs { get; set; } = 30_000f;
    /// <summary>Brightness above which evening melatonin is suppressed. // CALIBRATE</summary>
    public float BrightThreshold { get; set; } = 0.45f;
    /// <summary>Max melatonin suppression in bright evening light. // CALIBRATE (humans: 50-70%)</summary>
    public float MaxSuppression { get; set; } = 0.7f;

    public float BrightnessEma { get; private set; }
    /// <summary>Melatonin multiplier 0.3..1 (the fraction of drowsiness-melatonin that lands).</summary>
    public float MelatoninFactor { get; private set; } = 1f;
    public bool SuppressingNow { get; private set; }

    /// <summary>Feed perceived brightness (0..1) whenever a frame lands.</summary>
    public void OnBrightness(float brightness, float nowMs)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        float alpha = 1f - MathF.Exp(-1000f / MathF.Max(1000f, EmaTauMs));
        BrightnessEma += (brightness - BrightnessEma) * alpha;
    }

    /// <summary>
    /// Compute the suppression factor for this moment. circadian is the
    /// sleep controller's 0..1 signal (peaks midday; the biological evening
    /// is its declining low half — the same window the sleep-entry gate
    /// reads as night: < 0.45).
    /// </summary>
    public void Tick(float nowMs, float circadian)
    {
        bool evening = circadian < 0.45f;
        SuppressingNow = evening && BrightnessEma > BrightThreshold;
        if (!SuppressingNow) { MelatoninFactor = 1f; return; }
        float over = (BrightnessEma - BrightThreshold) / MathF.Max(0.05f, 1f - BrightThreshold);
        MelatoninFactor = 1f - MaxSuppression * Math.Clamp(over, 0f, 1f);
    }

    /// <summary>For the diary.</summary>
    public string Narrative => !SuppressingNow
        ? BrightnessEma > 0.5f ? "the room is bright but it is not evening" : "the light is where it should be"
        : $"the lights are keeping it up (evening brightness {(int)(BrightnessEma * 100)}%)";
}
