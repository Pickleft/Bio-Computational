namespace BioComp.Core.Synapses;

/// <summary>
/// Whole-network synapse store in CSR form plus per-synapse dynamics (Tsodyks-Markram
/// vesicles, axonal delay). Weights are Q15 fixed point (short) for memory locality.
/// Built by <see cref="Network.BrainConfig"/> wiring; immutable topology after build.
/// </summary>
public sealed class SynapseGraph
{
    public int NeuronCount { get; }
    public int SynapseCount { get; private set; }

    public int[] RowPtr = null!;      // NeuronCount+1
    public int[] PreIdx = null!;      // per synapse
    public int[] PostIdx = null!;
    public short[] WeightQ15 = null!; // |w| ≤ 1; sign: + excitatory, − inhibitory
    public ReceptorKind[] Receptors = null!;
    public byte[] DelayMs = null!;    // 1..MaxDelayMs
    public TmClass[] Tm = null!;

    // Per-synapse short-term plasticity state (updated at delivery).
    public float[] TmR = null!;
    public float[] TmU = null!;
    public float[] LastDeliveryMs = null!;

    public const int MaxDelayMs = 20;
    public const float Q15Scale = 1f / 32767f;

    public SynapseGraph(int neuronCount, int synapseCapacity)
    {
        NeuronCount = neuronCount;
        RowPtr = new int[neuronCount + 1];
        PreIdx = new int[synapseCapacity];
        PostIdx = new int[synapseCapacity];
        WeightQ15 = new short[synapseCapacity];
        Receptors = new ReceptorKind[synapseCapacity];
        DelayMs = new byte[synapseCapacity];
        Tm = new TmClass[synapseCapacity];
    }

    public static SynapseGraph Build(int neuronCount, int[] rowPtr, int[] preIdx, int[] postIdx,
        short[] wQ15, ReceptorKind[] receptors, byte[] delay, TmClass[] tm)
    {
        var g = new SynapseGraph(neuronCount, preIdx.Length)
        {
            RowPtr = rowPtr, PreIdx = preIdx, PostIdx = postIdx,
            WeightQ15 = wQ15, Receptors = receptors, DelayMs = delay, Tm = tm,
        };
        g.SynapseCount = preIdx.Length;
        return g;
    }

    /// <summary>Reset per-synapse dynamic state to fully recovered vesicles.</summary>
    public void ResetDynamics()
    {
        TmR ??= new float[SynapseCount];
        TmU ??= new float[SynapseCount];
        LastDeliveryMs ??= new float[SynapseCount];
        Array.Fill(TmR, 1f);
        Array.Fill(TmU, 0f);
        Array.Fill(LastDeliveryMs, -1e9f);
    }
}
