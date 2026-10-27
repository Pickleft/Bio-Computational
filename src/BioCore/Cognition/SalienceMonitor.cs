using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Senses;

namespace BioComp.Core.Cognition;

/// <summary>
/// The automatic bridge between sensory prediction error and neuromodulators.
/// Every frame/block: surprise releases NA (arousal) proportional to magnitude;
/// novelty releases DA (the brain finds novelty rewarding); sustained low novelty
/// gradually pulls DA below baseline (boredom as dopamine underflow, not a timer);
/// voice detection releases OXT (a social stimulus is intrinsically rewarding).
/// The feelings mapper just reads what the neuromodulators are doing — no manual
/// triggers anywhere in the codebase.
/// </summary>
public sealed class SalienceMonitor
{
    public PredictiveModel VisionModel { get; } = new();
    public PredictiveModel AudioModel { get; } = new();

    public float SurpriseThreshold { get; set; } = 6.0f;
    public float SustainMs { get; set; } = 250f;
    public int CooldownMs { get; set; } = 5_000;

    public float Arousal { get; private set; }
    public int SurpriseEvents { get; private set; }
    public readonly List<(float TimeMs, string Tag)> SurpriseLog = [];

    private float _sustainStart = -1f;
    private long _cooldownUntil = long.MinValue;
    private string _pendingTag = "";

    /// <summary>Feed the latest vision frame: automatically releases neuromodulators
    /// proportional to the surprise magnitude. The event trigger uses the instantaneous
    /// surprise (spikes high for novel stimuli); the feelings use the slow novelty trace.</summary>
    public void OnVisionFrame(SpikingNetwork net, float changeEnergy)
    {
        float perPixel = changeEnergy / (64f * 64f);
        float surprise = VisionModel.Observe(perPixel);
        ReleaseFromSurprise(net, surprise, "person");
        Consider(net, surprise, "person"); // trigger on instantaneous surprise (same as before)
    }

    /// <summary>Feed the latest audio block: onsets are inherently surprising;
    /// voice detection is a social stimulus (OXT).</summary>
    public void OnAudioBlock(SpikingNetwork net, bool onset, float energy)
    {
        float surprise = AudioModel.Observe(energy);
        if (onset) surprise += 4f; // onsets are surprising by definition
        ReleaseFromSurprise(net, surprise, "sound");

        // voice is a social stimulus: automatically releases oxytocin (Dolen et al. 2013)
        if (onset && surprise > 2f)
            net.Modulators.Release(NeuromodulatorKind.Oxytocin, 0.1f);

        Consider(net, surprise, "sound"); // trigger on instantaneous surprise
    }

    /// <summary>
    /// Automatic neuromodulator release from prediction error: the core pathway.
    /// Surprise → NA (arousal/gain up, Aston-Jones and Cohen 2005).
    /// Novelty → DA (the brain finds novelty rewarding, Schultz 1997 / Kakade and Dayan 2002).
    /// Sustained low novelty → DA sinks below baseline (boredom as dopamine underflow).
    /// </summary>
    private void ReleaseFromSurprise(SpikingNetwork net, float surprise, string tag)
    {
        var mods = net.Modulators;

        // noradrenaline: proportional to surprise magnitude (the LC responds to
        // stimulus intensity, not just binary detection)
        if (surprise > 1.5f)
        {
            float naAmount = Math.Clamp(surprise * 0.04f, 0.05f, 0.8f);
            mods.Release(NeuromodulatorKind.Noradrenaline, naAmount);
        }

        // dopamine: novelty is intrinsically rewarding (Kakade and Dayan 2002)
        float novelty = (VisionModel.Novelty + AudioModel.Novelty) / 2f;
        if (novelty > 0.3f)
            mods.Release(NeuromodulatorKind.Dopamine, novelty * 0.08f);
        else if (novelty < 0.05f)
        {
            // sustained low novelty: DA sinks below baseline — this is real boredom,
            // not a timer; it resets the moment anything novel happens
            mods.Release(NeuromodulatorKind.Dopamine, -0.002f);
        }

        // arousal for the workspace: sharp from surprise, slow from novelty
        Arousal = Math.Clamp(
            0.7f * Math.Min(surprise / SurpriseThreshold, 1f) +
            0.3f * Math.Min(novelty, 1f),
            0f, 1f);
    }

    private void Consider(SpikingNetwork net, float novelty, string tag)
    {
        if (novelty > SurpriseThreshold)
        {
            if (_sustainStart < 0f)
            {
                _sustainStart = net.TimeMs;
                _pendingTag = tag;
            }
            if (net.TimeMs - _sustainStart >= SustainMs && net.TickIndex > _cooldownUntil)
            {
                _sustainStart = -1f;
                _cooldownUntil = net.TickIndex + CooldownMs;
                SurpriseEvents++;
                SurpriseLog.Add((net.TimeMs, _pendingTag));
            }
        }
        else
        {
            _sustainStart = -1f;
        }
    }

    /// <summary>Arousal consumed by the idle loop (wake gate) and the workspace.</summary>
    public float ExternalArousal => Arousal;
}
