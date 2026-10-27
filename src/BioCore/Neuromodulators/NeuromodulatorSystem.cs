namespace BioComp.Core.Neuromodulators;

/// <summary>Slow, global neuromodulator tones (dimensionless, roughly 0..3).</summary>
public enum NeuromodulatorKind : byte
{
    Dopamine = 0,       // reward prediction error, gates plasticity (Schultz 1997)
    Serotonin,          // patience / stability / learning rate (Dayan & Huys 2009)
    Acetylcholine,      // attention and encoding of new traces (Hasselmo 2006)
    Noradrenaline,      // surprise and arousal gain (Aston-Jones & Cohen 2005)
    Oxytocin,           // social reward and bonding signal (Dolen et al. 2013)
}

/// <summary>
/// Global tone state for the five modulators. Tones relax toward their baselines with
/// first-order kinetics; events inject release (a negative amount for a pause, e.g.
/// dopamine dips below baseline on a negative reward prediction error).
/// Fast regional transmitter pools (glutamate, GABA) live in TransmitterSystem.
/// </summary>
public sealed class NeuromodulatorSystem
{
    public const int Count = 5;

    // baseline tone, and relaxation time constant in ms // CALIBRATE
    private static readonly float[] Baseline = [0.30f, 0.50f, 0.30f, 0.20f, 0.20f];
    private static readonly float[] TauMs = [2000f, 60_000f, 5000f, 1500f, 30_000f];

    public readonly float[] Tone = new float[Count];
    public readonly float[] PendingRelease = new float[Count];

    private readonly float[] _relaxPerMs = new float[Count];

    public NeuromodulatorSystem()
    {
        for (int k = 0; k < Count; k++)
        {
            Tone[k] = Baseline[k];
            _relaxPerMs[k] = MathF.Exp(-1f / TauMs[k]);
        }
    }

    public float Get(NeuromodulatorKind kind) => Tone[(int)kind];

    /// <summary>Event release. Negative amounts pull the tone below baseline (a pause).</summary>
    public void Release(NeuromodulatorKind kind, float amount) =>
        PendingRelease[(int)kind] += amount;

    /// <summary>Advance 1 ms of relaxation toward baseline.</summary>
    public void Step()
    {
        for (int k = 0; k < Count; k++)
        {
            float value = Tone[k] + PendingRelease[k];
            Tone[k] = Math.Clamp(Baseline[k] + (value - Baseline[k]) * _relaxPerMs[k], 0f, 3f);
            PendingRelease[k] = 0f;
        }
    }

    public static float BaselineOf(NeuromodulatorKind kind) => Baseline[(int)kind];
}
