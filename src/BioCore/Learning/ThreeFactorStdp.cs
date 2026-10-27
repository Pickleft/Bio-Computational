using BioComp.Core.Synapses;

namespace BioComp.Core.Learning;

/// <summary>
/// Dopamine-gated, eligibility-based STDP (three-factor rule).
/// Pairing writes an eligibility trace per synapse; each millisecond of simulated time
/// the trace decays and weights move by eta * tanh((DA - DA0) / s) * eligibility.
/// Izhikevich 2007 Neural Comput 19; Fremaux & Gerstner 2016 Front Comput Neurosci.
/// Window constants: Bi & Poo 1998. Dale's law is preserved by sign-preserving clamps.
/// </summary>
public sealed class ThreeFactorStdp
{
    public readonly float[] Eligibility;

    // Pairing window (ms) and amplitudes. // CALIBRATE
    public float APlus = 1.0f;
    public float AMinus = 0.85f;
    public float TauPlusMs = 20f;
    public float TauMinusMs = 40f;
    public float TauEligMs = 2000f;

    /// <summary>Base learning rate; scaled down by serotonin (patience/stability).</summary>
    public float EtaBase = 0.005f;
    public float DaBaseline = 0.30f;
    public float DaGateSlope = 0.25f;

    /// <summary>Weight bounds as a fraction of full scale, sign preserving.</summary>
    public const float MaxWeight = 0.90f;
    public const float MinWeight = 0.004f;

    private readonly float _eligDecayPerMs;

    /// <summary>Reverse CSR: for each post neuron, the synapse indices targeting it.</summary>
    public readonly int[] IncomingPtr;
    public readonly int[] IncomingSyn;

    public ThreeFactorStdp(SynapseGraph graph)
    {
        Eligibility = new float[graph.SynapseCount];
        _eligDecayPerMs = MathF.Exp(-1f / TauEligMs);

        int n = graph.NeuronCount;
        IncomingPtr = new int[n + 1];
        IncomingSyn = new int[graph.SynapseCount];
        var counts = new int[n];
        for (int s = 0; s < graph.SynapseCount; s++) counts[graph.PostIdx[s]]++;
        for (int i = 0; i < n; i++) IncomingPtr[i + 1] = IncomingPtr[i] + counts[i];
        var cursor = new int[n];
        Array.Copy(IncomingPtr, cursor, n);
        for (int s = 0; s < graph.SynapseCount; s++)
            IncomingSyn[cursor[graph.PostIdx[s]]++] = s;
    }

    /// <summary>Post spike: potentiation eligibility from recent presynaptic delivery.</summary>
    public void OnPostSpike(int post, float tMs, SynapseGraph graph, float achScale)
    {
        for (int i = IncomingPtr[post]; i < IncomingPtr[post + 1]; i++)
        {
            int s = IncomingSyn[i];
            float dt = tMs - graph.LastDeliveryMs[s];
            if (dt >= 0f && dt < 100f)
                Eligibility[s] += APlus * MathF.Exp(-dt / TauPlusMs) * achScale;
        }
    }

    /// <summary>Presynaptic delivery: depression eligibility from recent post spikes.</summary>
    public void OnPreDelivery(int syn, int post, float tMs, float[] lastPostSpikeMs, float achScale)
    {
        float dt = tMs - lastPostSpikeMs[post];
        if (dt >= 0f && dt < 100f)
            Eligibility[syn] -= AMinus * MathF.Exp(-dt / TauMinusMs) * achScale;
    }

    /// <summary>One ms of trace decay plus the gated weight update. Returns true if any weight changed.</summary>
    public bool DecayAndApply(SynapseGraph graph, float daTone, float serotoninTone)
    {
        float gate = MathF.Tanh((daTone - DaBaseline) / DaGateSlope);
        if (MathF.Abs(gate) < 1e-4f) return false;
        float eta = EtaBase * (1.2f - 0.6f * serotoninTone);
        bool changed = false;
        for (int s = 0; s < Eligibility.Length; s++)
        {
            float e = Eligibility[s] * _eligDecayPerMs;
            if (e == 0f && Eligibility[s] == 0f) { Eligibility[s] = 0f; continue; }
            Eligibility[s] = e;
            float dw = eta * gate * e;
            if (dw == 0f) continue;
            float w = graph.WeightQ15[s] * SynapseGraph.Q15Scale + dw;
            bool inhibitory = graph.WeightQ15[s] < 0;
            float bound = inhibitory ? -MinWeight : MinWeight;
            float cap = inhibitory ? -MaxWeight : MaxWeight;
            w = Math.Clamp(w, Math.Min(bound, cap), Math.Max(bound, cap));
            graph.WeightQ15[s] = (short)MathF.Round(w * 32767f);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Homeostatic synaptic scaling (Turrigiano 1998): once per window, multiply each
    /// neuron's incoming weights by (target/rate)^beta, clamped to a gentle band.
    /// </summary>
    public void HomeostaticScaling(SynapseGraph graph, uint[] windowCounts, float windowSeconds,
        float targetHz = 5f, float beta = 0.02f)
    {
        for (int n = 0; n < graph.NeuronCount; n++)
        {
            if (windowCounts[n] == 0) continue;
            float rate = windowCounts[n] / windowSeconds;
            float scale = MathF.Pow(targetHz / MathF.Max(rate, 0.01f), beta);
            scale = Math.Clamp(scale, 0.95f, 1.05f);
            if (MathF.Abs(scale - 1f) < 1e-4f) continue;
            for (int i = IncomingPtr[n]; i < IncomingPtr[n + 1]; i++)
            {
                int s = IncomingSyn[i];
                float w = graph.WeightQ15[s] * SynapseGraph.Q15Scale * scale;
                bool inhibitory = graph.WeightQ15[s] < 0;
                float bound = inhibitory ? -MinWeight : MinWeight;
                float cap = inhibitory ? -MaxWeight : MaxWeight;
                w = Math.Clamp(w, Math.Min(bound, cap), Math.Max(bound, cap));
                graph.WeightQ15[s] = (short)MathF.Round(w * 32767f);
            }
        }
    }
}
