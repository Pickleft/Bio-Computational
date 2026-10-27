namespace BioComp.Core.Synapses;

/// <summary>
/// Event-driven Tsodyks-Markram short-term plasticity. Pure function of the inter-spike
/// interval so it is trivially deterministic and unit-testable. Update happens at spike
/// delivery; between spikes no per-tick work is needed.
/// </summary>
public static class TsodyksMarkram
{
    /// <summary>
    /// Apply a pre-spike after <paramref name="isiMs"/> of silence. Muts R (available
    /// resources) and u (utilization), returns the fraction of resources released.
    /// </summary>
    public static float OnSpike(ref float r, ref float u, float isiMs, in TmParams p)
    {
        float uDecay = MathF.Exp(-isiMs / p.TauFacMs);
        float rDecay = MathF.Exp(-isiMs / p.TauRecMs);
        float uNew = p.U + u * (1f - p.U) * uDecay;
        float rNew = 1f + (r - u * r - 1f) * rDecay;
        u = uNew;
        r = rNew;
        return u * r;
    }
}
