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
    /// Also sets the engagement level on the predictive models so novelty persists
    /// longer during sustained interaction (state-dependent processing).
    /// </summary>
    private void ReleaseFromSurprise(SpikingNetwork net, float surprise, string tag)
    {
        var mods = net.Modulators;

        // engagement = how activated the brain is right now (NA + novelty)
        float novelty = (VisionModel.Novelty + AudioModel.Novelty) / 2f;
        float na = mods.Get(NeuromodulatorKind.Noradrenaline);
        float engagement = Math.Clamp(na * 1.5f + novelty * 0.5f, 0f, 1f);

        // engagement makes novelty persist longer: engaged brains hold curiosity
        VisionModel.Engagement = engagement;
        AudioModel.Engagement = engagement;

        // noradrenaline: proportional to surprise magnitude (the LC responds to
        // stimulus intensity, not just binary detection)
        if (surprise > 1.5f)
        {
            float naAmount = Math.Clamp(surprise * 0.04f, 0.05f, 0.8f);
            mods.Release(NeuromodulatorKind.Noradrenaline, naAmount);
            // surprising events are also rewarding: the brain wants to understand
            // them (VTA responds to stimulus novelty — Kakade and Dayan 2002)
            float daAmount = Math.Clamp(surprise * 0.02f, 0.02f, 0.4f);
            mods.Release(NeuromodulatorKind.Dopamine, daAmount);
        }

        // novelty is intrinsically rewarding (sustained: doesn't die in 2s when engaged)
        if (novelty > 0.3f)
            mods.Release(NeuromodulatorKind.Dopamine, novelty * 0.08f);
        else if (novelty < 0.05f && engagement < 0.1f)
        {
            // only when truly disengaged: DA sinks below baseline (boredom)
            mods.Release(NeuromodulatorKind.Dopamine, -0.002f);
        }

        // SOCIAL ENGAGEMENT FLOOR: ongoing interaction maintains DA and novelty
        // (social bonding is intrinsically rewarding — not just novel)
        if (engagement > 0.3f)
        {
            // keep DA at least at baseline during sustained engagement
            float da = mods.Get(NeuromodulatorKind.Dopamine);
            if (da < 0.35f)
                mods.Release(NeuromodulatorKind.Dopamine, 0.05f);
        }

        // arousal for the workspace: sharp from surprise, sustained from engagement
        Arousal = Math.Clamp(
            0.5f * Math.Min(surprise / SurpriseThreshold, 1f) +
            0.3f * Math.Min(novelty, 1f) +
            0.2f * engagement,
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
