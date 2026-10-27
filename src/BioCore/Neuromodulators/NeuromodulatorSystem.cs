namespace BioComp.Core.Neuromodulators;

/// <summary>Slow, global neuromodulator tones (dimensionless, roughly 0..3).</summary>
public enum NeuromodulatorKind : byte
{
    Dopamine = 0,       // reward prediction error, gates plasticity (Schultz 1997)
    Serotonin,          // patience / stability / learning rate (Dayan and Huys 2009)
    Acetylcholine,      // attention and encoding of new traces (Hasselmo 2006)
    Noradrenaline,      // surprise and arousal gain (Aston-Jones and Cohen 2005)
    Oxytocin,           // social reward and bonding signal (Dolen et al. 2013)
    Histamine,          // wakefulness drive (Haas et al. 2008)
    Melatonin,          // circadian sleep promotion (Borbely 1982 process C output)
}

/// <summary>
/// Global tone state for the seven modulators. Tones relax toward their baselines
/// with first-order kinetics; events inject release (negative amounts pull below
/// baseline). Histamine and melatonin are driven by the sleep controller: histamine
/// is the wakefulness flip-flop (high in wake, near-zero in NREM), melatonin follows
/// the circadian oscillator (rises at biological night). Fast regional transmitter
/// pools (glutamate, GABA) live in TransmitterSystem.
/// </summary>
public sealed class NeuromodulatorSystem
{
    public const int Count = 7;

    // baseline tone, and relaxation time constant in ms // CALIBRATE
    private static readonly float[] Baseline =
    [
        0.30f, // dopamine
        0.50f, // serotonin
        0.30f, // acetylcholine
        0.20f, // noradrenaline
        0.15f, // oxytocin (lower baseline: social bonding is specific, not tonic)
        0.60f, // histamine (wake-promoting, high baseline during wake)
        0.10f, // melatonin (low in daytime, rises at night)
    ];
    private static readonly float[] TauMs =
    [
        2000f,   // dopamine (fast)
        60_000f, // serotonin (slow)
        5000f,   // acetylcholine
        1500f,   // noradrenaline (fast)
        30_000f, // oxytocin (slow social trace)
        10_000f, // histamine (minutes-scale wake state)
        120_000f, // melatonin (very slow circadian)
    ];

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
