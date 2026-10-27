namespace BioComp.Core.Metrics;

/// <summary>
/// Per-neuron spike counters plus a bounded ring of recent spike events (t ms, index)
/// for rasters. Enough for M1; LFP proxy / PSD / avalanches arrive with M2 metrics.
/// </summary>
public sealed class SpikeRecorder
{
    public readonly uint[] Counts;
    public readonly (float TMs, int Neuron)[] Events;
    public int EventHead { get; private set; }   // next write slot
    public int EventCount { get; private set; }  // valid entries (≤ capacity)
    public long TotalSpikes { get; private set; }

    public SpikeRecorder(int neuronCount, int eventCapacity = 8192)
    {
        Counts = new uint[neuronCount];
        Events = new (float, int)[eventCapacity];
    }

    public void Record(float tMs, int neuron)
    {
        Counts[neuron]++;
        TotalSpikes++;
        Events[EventHead] = (tMs, neuron);
        EventHead = (EventHead + 1) % Events.Length;
        if (EventCount < Events.Length) EventCount++;
    }

    /// <summary>Population mean firing rate over the whole recorded window.</summary>
    public float MeanRateHz(float windowMs) =>
        windowMs <= 0f ? 0f : TotalSpikes / (Counts.Length * windowMs * 0.001f);
}
