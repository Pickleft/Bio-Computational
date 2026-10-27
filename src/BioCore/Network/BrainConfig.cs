using BioComp.Core.Synapses;

namespace BioComp.Core.Network;

public sealed record RegionSpec(string Name, int Size, float SheetX, float SheetY);

public sealed record ProjectionSpec(
    string Pre,
    string Post,
    int OutDegree,
    float WeightMean,
    float Lambda = 1.2f,
    byte DelayMeanMs = 4,
    byte DelaySigmaMs = 2,
    TmClass Tm = TmClass.Depressing);

/// <summary>
/// Declarative brain wiring. Distances between neurons derive from region anchors on a
/// cortical sheet (grid units) plus per-neuron jitter; connection probability per
/// candidate falls off as exp(−d/λ). Sizes scale with <paramref name="scale"/>.
/// </summary>
public sealed class BrainConfig
{
    public ulong Seed { get; init; } = 42;
    // Tunables are mutable: BioLab sweeps (branching ratio, noise) adjust them before construction.
    // 3 mV sits near criticality for the default wiring (biolab tune, Oct 27): branching ratio
    // ~1.00 with discrete avalanches at 0.13 Hz population rate; 5 mV and above runs away.
    public float NoiseSigmaMv { get; set; } = 3.0f;
    public float GlobalGabaGain { get; set; } = 1.0f;
    public float InhibitoryFraction { get; init; } = 0.20f;

    public List<RegionSpec> Regions { get; } = [];
    public List<ProjectionSpec> Projections { get; } = [];

    /// <summary>Twelve-region default brain (~6k neurons at scale 1, ~24k at scale 4).</summary>
    public static BrainConfig Default(int scale = 1, ulong? seed = null)
    {
        var c = new BrainConfig { Seed = seed ?? 42 };
        int S(int n) => Math.Max(8, n * scale);
        // Sheet anchors: sensory posterior-left, association center, output anterior-right.
        c.Regions.AddRange([
            new RegionSpec("Retina", S(512), 0f, 0f),
            new RegionSpec("Thal",   S(128), 2.5f, 0f),
            new RegionSpec("V1",     S(1536), 5f, 0f),
            new RegionSpec("A1",     S(1024), 0f, 2.5f),
            new RegionSpec("CA3",    S(512), 2.5f, 2.5f),
            new RegionSpec("CA1",    S(256), 2.5f, 5f),
            new RegionSpec("PFC",    S(512), 5f, 2.5f),
            new RegionSpec("TaskPos", S(512), 5f, 5f),
            new RegionSpec("DMN",    S(512), 2.5f, 7.5f),
            new RegionSpec("Intero", S(128), 0f, 5f),
            new RegionSpec("Lang",   S(128), 0f, 7.5f),
            new RegionSpec("Motor",  S(256), 7.5f, 2.5f),
        ]);

        // Local inhibition everywhere (Dale turns I presynaptic cells into GABA synapses).
        foreach (var r in c.Regions)
            c.Projections.Add(new ProjectionSpec(r.Name, r.Name, 4, 0.06f, 0.8f, 2, 1));

        c.Projections.AddRange([
            // Pre-thinking: sensory lift-up
            new("Retina", "Thal", 4, 0.08f),
            new("A1", "Thal", 4, 0.08f),
            new("Thal", "V1", 6, 0.07f),
            new("V1", "V1", 8, 0.03f, 0.8f),
            // Central integrator: hippocampal loop + PFC
            new("V1", "TaskPos", 4, 0.05f),
            new("TaskPos", "PFC", 5, 0.05f),
            new("PFC", "PFC", 10, 0.02f, 0.8f, Tm: TmClass.Facilitating),
            new("CA3", "CA3", 12, 0.02f, 0.6f, Tm: TmClass.Facilitating),
            new("CA3", "CA1", 6, 0.06f),
            new("CA1", "PFC", 5, 0.05f),
            // Default-mode coupling
            new("PFC", "DMN", 4, 0.04f),
            new("DMN", "PFC", 4, 0.04f),
            new("DMN", "CA3", 6, 0.05f),
            new("DMN", "DMN", 8, 0.02f, 0.8f),
            // Interoception / language / output
            new("Intero", "PFC", 4, 0.05f),
            new("Lang", "PFC", 4, 0.06f),
            new("PFC", "Motor", 6, 0.07f),
            new("TaskPos", "V1", 3, 0.03f), // top-down feedback
            new("V1", "Thal", 3, 0.035f, 1.2f, 25, 6), // thalamocortical loop: ~8-12 Hz alpha at rest
        ]);
        return c;
    }
}
