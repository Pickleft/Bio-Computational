namespace BioComp.Core.Neuromodulators;

public enum TransmitterKind : byte { Glutamate = 0, Gaba = 1, Dopamine = 2, Serotonin = 3 }

/// <summary>
/// Regional extracellular transmitter pools with Michaelis–Menten reuptake
/// (transporter clearance). Release events accumulate into a per-ms release rate;
/// <see cref="Step"/> integrates dT/dt = releaseRate − Vmax·T/(Km+T) in substeps
/// (the 1 ms lump would otherwise overshoot for fast transporters).
/// DA clearance is tuned to ~1–2 s extracellular half-time (Garris &amp; Wightman 1994);
/// 5-HT is a slow tone pool (Dayan &amp; Huys 2009). // CALIBRATE
/// </summary>
public sealed class TransmitterSystem
{
    public const int KindCount = 4;
    private const int Substeps = 4;

    private readonly float[][] _pools;           // [region][kind]
    private readonly float[][] _pendingRelease;  // [region][kind] — amount released this ms
    private readonly float[] _vMax = new float[KindCount];
    private readonly float[] _km = new float[KindCount];

    /// <summary>Total uptake flux per kind during the last tick (for the energy ledger).</summary>
    public readonly float[] LastUptakeFlux = new float[KindCount];

    public int RegionCount => _pools.Length;

    public TransmitterSystem(int regionCount)
    {
        _pools = new float[regionCount][];
        _pendingRelease = new float[regionCount][];
        for (int i = 0; i < regionCount; i++)
        {
            _pools[i] = new float[KindCount];
            _pendingRelease[i] = new float[KindCount];
        }
        _vMax[(int)TransmitterKind.Glutamate] = 4.0f;  _km[(int)TransmitterKind.Glutamate] = 0.8f; // fast local clearance
        _vMax[(int)TransmitterKind.Gaba] = 4.0f;       _km[(int)TransmitterKind.Gaba] = 0.8f;
        _vMax[(int)TransmitterKind.Dopamine] = 0.5f;   _km[(int)TransmitterKind.Dopamine] = 0.4f;  // ~1–2 s half-time
        _vMax[(int)TransmitterKind.Serotonin] = 0.05f; _km[(int)TransmitterKind.Serotonin] = 0.4f; // slow tonic
    }

    public void AddRelease(int region, TransmitterKind kind, float amount) =>
        _pendingRelease[region][(int)kind] += amount;

    public float Get(int region, TransmitterKind kind) => _pools[region][(int)kind];

    /// <summary>Externally set a tone (e.g., DA from a reward-prediction-error event, M1+).</summary>
    public void Set(int region, TransmitterKind kind, float value) =>
        _pools[region][(int)kind] = value;

    /// <summary>Advance one ms of transporter reuptapake / clearance.</summary>
    public void Step()
    {
        Array.Clear(LastUptakeFlux);
        float h = 1f / Substeps;
        for (int r = 0; r < _pools.Length; r++)
        {
            var pool = _pools[r];
            var release = _pendingRelease[r];
            for (int k = 0; k < KindCount; k++)
            {
                float rate = release[k];
                release[k] = 0f;
                float t = pool[k];
                if (rate == 0f && t <= 0f) { pool[k] = 0f; continue; }
                float vMax = _vMax[k], km = _km[k];
                for (int s = 0; s < Substeps; s++)
                {
                    float uptakeRate = t <= 0f ? 0f : vMax * t / (km + t);
                    t += (rate - uptakeRate) * h;
                    if (t < 0f) t = 0f;
                    LastUptakeFlux[k] += uptakeRate * h;
                }
                pool[k] = t;
            }
        }
    }

    /// <summary>Analytic steady state for a constant release rate R: solves Vmax·T/(Km+T) = R.</summary>
    public static float SteadyState(float releasePerMs, float vMax, float km) =>
        releasePerMs * km / (vMax - releasePerMs);
}
