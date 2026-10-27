namespace BioComp.Core.Energy;

/// <summary>
/// One metabolic budget for spikes, vesicle recycling and transmitter reuptake.
/// API-call costs (M5) are booked in the same ledger in USD-equivalent units so that
/// cloud spending physically tires the brain. Constants are model units; // CALIBRATE
/// </summary>
public sealed class EnergyLedger
{
    public const float SpikeCost = 1.0f;
    public const float VesicleRecycleCost = 0.2f;
    public const float ReuptakeCostPerUnit = 0.05f;

    public float Spent { get; private set; }
    public long SpikeCount { get; private set; }

    public void AddSpike() { Spent += SpikeCost; SpikeCount++; }

    public void AddVesicleRecycle(float releaseFraction) => Spent += VesicleRecycleCost * releaseFraction;

    public void AddReuptake(float uptakeFlux) => Spent += ReuptakeCostPerUnit * uptakeFlux;

    /// <summary>Metabolic equivalent charge for an external (API) action. Tier-dependent.</summary>
    public void AddExternal(string tier, float units) => Spent += units;
}
