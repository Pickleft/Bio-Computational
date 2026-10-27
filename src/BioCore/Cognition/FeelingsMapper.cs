using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Sleep;

namespace BioComp.Core.Cognition;

public readonly record struct FeelingState(
    float Valence,       // -1..1
    float Arousal,       // 0..1
    string Name,
    string Narrative);

/// <summary>
/// Computational proxy of feeling: neuromodulator tones, homeostatic deficits and
/// subjective time map onto the Russell circumplex (Russell 1980) plus named states.
/// This is a readout of internal state, not a claim about experience; the interpreter
/// may only report what this plus ignited content justify.
/// </summary>
public sealed class FeelingsMapper
{
    public FeelingState Last { get; private set; }

    public FeelingState Evaluate(SpikingNetwork net, SleepController sleep, Time.TimeSense time,
        float novelty, Abstractions.InteroceptionSample body)
    {
        float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        float sht = net.Modulators.Get(NeuromodulatorKind.Serotonin);
        float na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        float oxt = net.Modulators.Get(NeuromodulatorKind.Oxytocin);

        // homeostatic deficits: low battery or starvation pressure feel bad
        float energyDeficit = MathF.Max(
            body.BatteryPercent is byte b and not 255 && b < 30 ? 1f - b / 30f : 0f,
            sleep.SleepPressure * 0.6f);
        // stress proxy: low serotonergic tone with high noradrenergic drive
        float stress = Math.Clamp((0.6f - sht) * 1.5f, 0f, 1f) * Math.Clamp(na, 0f, 1f);

        float valence = Math.Clamp(
            1.1f * (da - NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Dopamine)) +
            0.5f * (oxt - NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Oxytocin)) -
            0.9f * energyDeficit -
            0.7f * stress,
            -1f, 1f);

        // dopamine is the seeking system: bursts raise activation as well as valence
        float daDrive = Math.Clamp((da - NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Dopamine)) * 1.5f, 0f, 1f);
        // noradrenaline is the primary arousal driver: events push it well above baseline
        // (the weight is high enough that a surprise NA burst crosses the curious threshold)
        float arousal = Math.Clamp(
            0.70f * na +
            0.25f * daDrive +
            0.10f * Math.Clamp(novelty, 0f, 2f) / 2f +
            0.25f * stress,
            0f, 1f);

        // subjective boredom: low novelty sustained over SUBJECTIVE time (the internal
        // clock runs slow when under-stimulated, so boredom accumulates at true pace)
        double subjectiveIdleMinutes = time.SubjectiveSinceEventMs / 60_000.0;
        bool bored = valence < 0.15f && arousal < 0.30f && novelty < 0.25f && subjectiveIdleMinutes > 1.0;
        bool tired = sleep.SleepPressure > 0.65f || sleep.State is BrainState.Drowsy or BrainState.NREM or BrainState.REM;

        string name = tired && arousal < 0.5f ? "tired"
            : bored ? "bored"
            : arousal > 0.40f && valence > 0.1f ? "curious"
            : arousal > 0.55f && valence <= 0.1f ? "stressed"
            : valence > 0.15f ? "calm"
            : "restless";
        if (name == "restless" && tired) name = "tired";

        string narrative =
            $"Feeling {name} (valence {valence:+0.0;-0.0}, arousal {arousal:F2}): " +
            (name == "bored" ? "nothing new for a while now, time is dragging"
             : name == "tired" ? "sleep pressure is high"
             : name == "curious" ? "something novel is happening"
             : name == "stressed" ? "tension with high alertness"
             : name == "calm" ? "steady and comfortable"
             : "unsettled");

        Last = new FeelingState(valence, arousal, name, narrative);
        return Last;
    }
}
