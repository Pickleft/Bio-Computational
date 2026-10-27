namespace BioComp.Core.Neurons;

/// <summary>Neuron subtype. RS/IB are excitatory, FS/LTS inhibitory (Dale's law).</summary>
public enum NeuronType : byte
{
    RegularSpiking = 0,   // excitatory principal cell
    FastSpiking = 1,      // parvalbumin-like interneuron
    LowThresholdSpiking = 2, // somatostatin-like interneuron
    IntrinsicallyBursting = 3, // excitatory (deep layer / PFC-ish)
}

/// <summary>
/// Izhikevich (2003) parameter sets, IEEE Trans. Neural Netw. 14(6):1569. Indexed by
/// <see cref="NeuronType"/>. Units: v mV, t ms, I pA-scaled.
/// </summary>
public static class IzhikevichParams
{
    public const int Count = 4;

    public static readonly float[] A = [0.02f, 0.10f, 0.02f, 0.01f];
    public static readonly float[] B = [0.20f, 0.20f, 0.25f, 0.20f];
    public static readonly float[] C = [-65f, -65f, -65f, -55f];
    public static readonly float[] D = [8f, 2f, 2f, 4f];

    public const float SpikeThresholdMv = 30f;
}
