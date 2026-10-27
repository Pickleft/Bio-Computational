namespace BioComp.Core.Synapses;

/// <summary>Receptor kinds a single synapse can drive (pooled per post-neuron conductances).</summary>
[Flags]
public enum ReceptorKind : byte
{
    None = 0,
    Ampa = 1 << 0,
    Nmda = 1 << 1,
    Gabaa = 1 << 2,
    Gabab = 1 << 3,
    Excitatory = Ampa | Nmda,
    Inhibitory = Gabaa | Gabab,
}

/// <summary>
/// Dual-exponential kinetics (rise τr, decay τd) and reversal potentials.
/// τ values: Dayan &amp; Abbott, Theoretical Neuroscience (2001) ch. 5.
/// NMDA Mg-unblock: Jahr &amp; Stevens (1990) J. Neurosci. 10:3178.
/// </summary>
public static class ReceptorKinetics
{
    public const float TauRiseAmpaMs = 0.5f, TauDecayAmpaMs = 5f;
    public const float TauRiseNmdaMs = 10f, TauDecayNmdaMs = 100f;
    public const float TauRiseGabaaMs = 0.5f, TauDecayGabaaMs = 10f;
    public const float TauRiseGababMs = 25f, TauDecayGababMs = 200f;

    public const float RevExcitatoryMv = 0f;
    public const float RevInhibitoryMv = -80f;
    public const float RevGababMv = -90f;

    /// <summary>Conductance fraction carried by each receptor at a mixed synapse.</summary>
    public const float AmpaFraction = 0.65f, NmdaFraction = 0.35f;
    public const float GabaaFraction = 0.8f, GababFraction = 0.2f;

    /// <summary>Jahr &amp; Stevens Mg-block with [Mg]=1 mM: 1/(1 + 0.28·e^(−0.062·v)).</summary>
    public static float NmdaMgBlock(float vMv) =>
        1f / (1f + 0.28f * MathF.Exp(-0.062f * vMv));
}

/// <summary>Tsodyks–Markram vesicle class: depressing (E→I, strong) vs facilitating (modulatory).</summary>
public enum TmClass : byte
{
    Depressing = 0,
    Facilitating = 1,
}

/// <summary>
/// Tsodyks, Pawelzik &amp; Markram (1998) Neural Comput. 10:821 — per-synapse vesicle
/// resources R and utilization u. Defaults from PARAMETERS-SEED (docs/D0).
/// </summary>
public readonly record struct TmParams(float U, float TauRecMs, float TauFacMs)
{
    public static TmParams For(TmClass c) => c == TmClass.Depressing
        ? new TmParams(0.50f, 450f, 50f)
        : new TmParams(0.25f, 200f, 1000f);
}
