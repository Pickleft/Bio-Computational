using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// Emotional contagion from voice prosody (Hatfield, Cacioppo and Rapson 1993;
/// prosody-to-affect mapping after Juslin and Scherer 2005): the brain
/// estimates the SPEAKER's valence and arousal from real acoustic features
/// (energy, pitch proxy via zero-crossing rate, and their variabilities,
/// personalized against the speaker's own running baseline), then its own
/// neuromodulatory state drifts TOWARD that estimate - the mechanistic core
/// of empathy: feeling with someone because their voice moved you. Estimates
/// decay toward unconfident when nobody speaks. Sim-thread only.
/// </summary>
public sealed class EmotionalContagion
{
    /// <summary>How fast the brain's state drifts toward the speaker's (per second).</summary>
    public float ContagionRate { get; set; } = 0.15f;   // CALIBRATE
    /// <summary>Estimates decay toward zero confidence after this many seconds of silence.</summary>
    public float DecayTauSec { get; set; } = 30f;

    /// <summary>Estimated speaker valence -1..1 and arousal 0..1, with confidence 0..1.</summary>
    public float UserValence { get; private set; }
    public float UserArousal { get; private set; }
    public float Confidence { get; private set; }
    public string UserMoodLabel =>
        Confidence < 0.25f ? "unknown"
        : UserArousal > 0.6f ? (UserValence > 0.15f ? "excited" : UserValence < -0.15f ? "agitated" : "tense")
        : UserArousal < 0.3f ? (UserValence < -0.15f ? "down" : "quiet")
        : (UserValence > 0.15f ? "warm" : UserValence < -0.15f ? "cold" : "neutral");

    private float _lastVoiceSec = -1e9f;

    /// <summary>
    /// Feed one heard speech clip. rms in 0..1; pitchZcr is the zero-crossing
    /// rate pitch proxy; baseline is the speaker's long-run pitch proxy (from
    /// VoiceSelf); energy/pitch variability drive the arousal/valence split.
    /// </summary>
    public void OnHeardVoice(float rms, float pitchZcr, float baselineZcr,
        float zcrVariability, float rmsVariability, float durationSec, float timeSec)
    {
        // arousal: loud and/or highly variable energy
        float arousal = Math.Clamp(rms * 3.5f + rmsVariability * 4f, 0f, 1f);
        // pitch deviation from the speaker's OWN baseline, and how flat it is
        float dev = pitchZcr - baselineZcr;                     // above/below their usual pitch
        float flat = MathF.Max(0f, 0.06f - zcrVariability) / 0.06f; // 1 = monotone
        float valence;
        if (dev > 0.015f && flat > 0.5f && rms > 0.08f) valence = -0.6f;      // loud, high, flat: tense/aggressive
        else if (dev > 0.010f && zcrVariability > 0.02f) valence = 0.6f;      // high, lively: excited/warm
        else if (dev < -0.010f && arousal < 0.45f) valence = -0.4f;           // low, slow: down
        else valence = 0.1f;                                                  // mid, easy: mildly warm
        // longer clips are more reliable evidence
        float conf = Math.Clamp(durationSec / 1.5f, 0.2f, 1f);

        // integrate: EMA with confidence weighting
        float w = 0.5f * conf;
        UserValence = Math.Clamp(UserValence + w * (valence - UserValence), -1f, 1f);
        UserArousal = Math.Clamp(UserArousal + w * (arousal - UserArousal), 0f, 1f);
        Confidence = Math.Clamp(Confidence + 0.4f * (conf - Confidence), 0f, 1f);
        _lastVoiceSec = timeSec;
    }

    /// <summary>
    /// Sim tick: decay confidence in silence; while confident, the brain DRIFTS
    /// toward the speaker's state - dopamine/serotonin toward their valence,
    /// noradrenaline toward their arousal, oxytocin when the exchange is warm.
    /// This is the mechanistic form of catching someone's mood.
    /// </summary>
    public void Tick(SpikingNetwork net, float timeSec)
    {
        float sinceVoice = timeSec - _lastVoiceSec;
        if (sinceVoice > DecayTauSec) { Confidence *= MathF.Exp(-(sinceVoice - DecayTauSec) / DecayTauSec); return; }
        if (Confidence < 0.2f) return;
        float k = ContagionRate * Confidence;
        if (UserValence > 0.15f)
        {
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.2f * k);
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin, 0.15f * k);
        }
        else if (UserValence < -0.15f)
        {
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin, -0.1f * k);
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.1f * k);
        }
        net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline,
            (UserArousal - 0.4f) * 0.15f * k);
    }
}
