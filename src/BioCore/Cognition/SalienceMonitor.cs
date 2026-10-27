using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Senses;

namespace BioComp.Core.Cognition;

/// <summary>
/// The thalamic-gate precursor: combines visual and auditory prediction error into a
/// salience/arousal signal. Sustained surprise (the instantaneous standardized error)
/// releases noradrenaline (gain up), a splash of dopamine (novelty is valuable), and is
/// reported as external arousal so the idle loop wakes the brain and encodes a tagged
/// episode. Habituation falls out of the predictive models: a stimulus that stays
/// static stops producing prediction error, and repeated events adapt the error
/// statistics; changed input dishabituates automatically. The slow novelty trace feeds
/// curiosity and boredom rather than the trigger.
/// </summary>
public sealed class SalienceMonitor
{
    public PredictiveModel VisionModel { get; } = new();
    public PredictiveModel AudioModel { get; } = new();

    public float SurpriseThreshold { get; set; } = 6.0f;  // standardized-error units
    public float SustainMs { get; set; } = 250f;
    public int CooldownMs { get; set; } = 5_000;

    public float Arousal { get; private set; }
    public int SurpriseEvents { get; private set; }
    public readonly List<(float TimeMs, string Tag)> SurpriseLog = [];

    private float _sustainStart = -1f;
    private long _cooldownUntil = long.MinValue;
    private string _pendingTag = "";

    /// <summary>Feed the latest vision statistics (once per frame).</summary>
    public void OnVisionFrame(SpikingNetwork net, float changeEnergy)
    {
        float perPixel = changeEnergy / (64f * 64f);
        VisionModel.Observe(perPixel);
        Consider(net, VisionModel.LastSurprise, "person");
    }

    /// <summary>Feed the latest audio block.</summary>
    public void OnAudioBlock(SpikingNetwork net, bool onset, float energy)
    {
        AudioModel.Observe(energy);
        if (onset) Consider(net, AudioModel.LastSurprise + 4f, "sound"); // onsets are surprising by definition
    }

    private void Consider(SpikingNetwork net, float surprise, string tag)
    {
        // arousal: sharp component from surprise, slow component from accumulated novelty
        Arousal = Math.Clamp(
            0.7f * MathF.Min(surprise / SurpriseThreshold, 1f) +
            0.3f * MathF.Min((VisionModel.Novelty + AudioModel.Novelty) / 2f, 1f), 0f, 1f);

        if (surprise > SurpriseThreshold)
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
                net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 0.9f);
                net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.25f);
            }
        }
        else
        {
            _sustainStart = -1f;
        }
    }

    /// <summary>Arousal consumed by the idle loop (wake gate) and, later, the workspace.</summary>
    public float ExternalArousal => Arousal;
}
