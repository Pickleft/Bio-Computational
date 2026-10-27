using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Sleep;

namespace BioComp.Core.Metrics;

/// <summary>
/// A single consciousness-correlate gauge combining the three major measures:
/// - PCI (perturbational complexity: how differentiated the brain's response is)
/// - Ignition rate (how often the global workspace broadcasts)
/// - Wake state (histamine/NA tone as the wakefulness substrate)
/// Returns 0..1 where 0.3+ resembles wake, <0.15 resembles deep NREM.
/// This is a research correlate, not a consciousness claim (see LIMITATIONS.md).
/// </summary>
public static class ConsciousnessMeter
{
    /// <summary>Compute the composite score. Cheap — no perturbation needed.</summary>
    public static float Measure(SpikingNetwork net, IdleController idle)
    {
        // 1. Ignition density: recent ignitions per minute (wakes: 2-10, sleep: 0)
        float ignitionsPerMin = idle.Workspace.IgnitionCount /
            MathF.Max(1f, net.TimeMs / 60_000f);
        float ignitionScore = Math.Clamp(ignitionsPerMin / 5f, 0f, 1f);

        // 2. Wake tone: histamine + NA (the wakefulness substrate)
        float hist = net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Histamine);
        float na = net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline);
        float wakeScore = Math.Clamp((hist + na) / 1.2f, 0f, 1f);

        // 3. Differentiation: population rate variability (high in wake, flat in NREM)
        float rate = net.Recorder.MeanRateHz(MathF.Max(1000, net.TimeMs));
        float rateScore = Math.Clamp(rate / 2f, 0f, 1f);

        // Sleep state override: NREM is deeply unconscious regardless of other scores
        if (idle.Sleep.State == BrainState.NREM)
            return 0.05f + 0.05f * ignitionScore; // near-zero, matching real PCI in NREM
        if (idle.Sleep.State == BrainState.REM)
            return 0.55f + 0.20f * ignitionScore; // REM is dream-conscious (high PCI in real data)

        // Wake: weighted combination
        return Math.Clamp(
            0.35f * ignitionScore +
            0.35f * wakeScore +
            0.30f * rateScore,
            0f, 1f);
    }

    /// <summary>Human-readable label for the score.</summary>
    public static string Label(float score) => score switch
    {
        > 0.60f => "highly aware",
        > 0.40f => "aware",
        > 0.20f => "drowsy",
        > 0.10f => "deep sleep",
        _ => "unresponsive",
    };
}
