using BioComp.Core.Energy;
using BioComp.Core.Metrics;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Neurons;
using BioComp.Core.Rng;
using BioComp.Core.Synapses;

namespace BioComp.Core.Network;

public sealed record RegionInfo(string Name, int Start, int Count);

/// <summary>
/// Deterministic whole-brain simulation. Owns every piece of neural state; the only way
/// in from the outside world is <see cref="InjectCurrent"/> (and, from M2, the sim-input
/// event queue). One 1 ms tick = spike delivery, two 0.5 ms Izhikevich substeps,
/// conductance decay, transmitter reuptake.
/// </summary>
public sealed class SpikingNetwork
{
    public const int RingSize = SynapseGraph.MaxDelayMs + 2;

    public BrainConfig Config { get; }
    public Xoshiro256StarStar Rng { get; }
    public SynapseGraph Syn { get; }
    public ConductanceStack Cond { get; }
    public TransmitterSystem Transmitters { get; }
    public EnergyLedger Energy { get; } = new();
    public SpikeRecorder Recorder { get; }

    public readonly float[] V, U, Injected, NoiseBuf;
    public readonly NeuronType[] Types;
    public readonly bool[] Inhibitory;
    public readonly ushort[] RegionOf;
    public readonly float[] PosX, PosY;
    public IReadOnlyList<RegionInfo> Regions { get; }

    public long TickIndex { get; private set; }
    public float TimeMs => TickIndex;

    private readonly List<int>[] _ring;
    private readonly ulong _noiseSalt;
    private readonly float _transmitterPerSpike = 0.02f; // CALIBRATE (pool scale)

    public SpikingNetwork(BrainConfig config)
    {
        Config = config;
        Rng = new Xoshiro256StarStar(config.Seed);
        _noiseSalt = Rng.NextUInt64();

        // Regions, neuron allocation -----------------------------------------------------
        var regions = new List<RegionInfo>(config.Regions.Count);
        RegionOf = new ushort[config.Regions.Sum(r => r.Size)];
        int n = RegionOf.Length;
        V = new float[n]; U = new float[n]; Injected = new float[n]; NoiseBuf = new float[n];
        Types = new NeuronType[n]; Inhibitory = new bool[n];
        PosX = new float[n]; PosY = new float[n];

        int cursor = 0;
        for (ushort r = 0; r < config.Regions.Count; r++)
        {
            var spec = config.Regions[r];
            regions.Add(new RegionInfo(spec.Name, cursor, spec.Size));
            for (int i = 0; i < spec.Size; i++)
            {
                int g = cursor + i;
                RegionOf[g] = r;
                PosX[g] = spec.SheetX + (float)Rng.NextDouble() * 2.5f;
                PosY[g] = spec.SheetY + (float)Rng.NextDouble() * 2.5f;
                bool inh = Rng.NextDouble() < config.InhibitoryFraction;
                Inhibitory[g] = inh;
                if (inh)
                    Types[g] = Rng.NextDouble() < 0.75 ? NeuronType.FastSpiking : NeuronType.LowThresholdSpiking;
                else
                    Types[g] = Rng.NextDouble() < 0.10 ? NeuronType.IntrinsicallyBursting : NeuronType.RegularSpiking;
                V[g] = IzhikevichParams.C[(int)Types[g]];
                U[g] = IzhikevichParams.B[(int)Types[g]] * V[g];
            }
            cursor += spec.Size;
        }
        Regions = regions;
        int regionIndex(string name) => config.Regions.FindIndex(r => r.Name == name);

        // Synapses -------------------------------------------------------------------------
        var rowCounts = new int[n];
        var preL = new List<int>();
        var postL = new List<int>();
        var wL = new List<short>();
        var recL = new List<ReceptorKind>();
        var delL = new List<byte>();
        var tmL = new List<TmClass>();

        void Wire(ProjectionSpec p, int preStart, int preCount, int postStart, int postCount, Func<int, bool> isInh)
        {
            for (int i = 0; i < preCount; i++)
            {
                int pre = preStart + i;
                bool inh = isInh(pre);
                for (int k = 0; k < p.OutDegree; k++)
                {
                    int post = postStart + Rng.NextInt(postCount);
                    if (post == pre) continue;
                    float dx = PosX[pre] - PosX[post], dy = PosY[pre] - PosY[post];
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (Rng.NextDouble() > MathF.Exp(-dist / p.Lambda)) continue;
                    float wAbs = p.WeightMean * (0.5f + (float)Rng.NextDouble());
                    wL.Add((short)MathF.Round((inh ? -wAbs : wAbs) * 32767f));
                    preL.Add(pre); postL.Add(post);
                    recL.Add(inh ? ReceptorKind.Inhibitory : ReceptorKind.Excitatory);
                    int d = (int)MathF.Round(p.DelayMeanMs + p.DelaySigmaMs * (float)Rng.NextGaussian());
                    delL.Add((byte)Math.Clamp(d, 1, SynapseGraph.MaxDelayMs));
                    tmL.Add(p.Tm);
                    rowCounts[pre]++;
                }
            }
        }

        // Dale's law is applied per presynaptic cell: inhibitory pre ⇒ GABA receptors and
        // negative weight; excitatory pre ⇒ AMPA/NMDA and positive weight.
        foreach (var p in config.Projections)
        {
            int pre = regionIndex(p.Pre), post = regionIndex(p.Post);
            if (pre < 0 || post < 0) throw new ArgumentException($"Unknown region in projection {p.Pre}->{p.Post}");
            var preR = regions[pre]; var postR = regions[post];
            Wire(p, preR.Start, preR.Count, postR.Start, postR.Count, i => Inhibitory[i]);
        }

        // Edges were generated projection-by-projection; sort by presynaptic index to form CSR.
        var order = new int[preL.Count];
        for (int e = 0; e < order.Length; e++) order[e] = e;
        Array.Sort(order, (a, b) => preL[a].CompareTo(preL[b]));
        var rowPtr = new int[n + 1];
        Array.Clear(rowCounts, 0, n);
        foreach (var e in order) rowCounts[preL[e]]++;
        for (int i = 0; i < n; i++) rowPtr[i + 1] = rowPtr[i] + rowCounts[i];

        var preArr = new int[order.Length]; var postArr = new int[order.Length];
        var wArr = new short[order.Length]; var recArr = new ReceptorKind[order.Length];
        var delArr = new byte[order.Length]; var tmArr = new TmClass[order.Length];
        var cur = new int[n];
        Array.Copy(rowPtr, cur, n);
        for (int idx = 0; idx < order.Length; idx++)
        {
            int e = order[idx], pre = preL[e], slot = cur[pre]++;
            preArr[slot] = pre; postArr[slot] = postL[e]; wArr[slot] = wL[e];
            recArr[slot] = recL[e]; delArr[slot] = delL[e]; tmArr[slot] = tmL[e];
        }

        Syn = SynapseGraph.Build(n, rowPtr, preArr, postArr, wArr, recArr, delArr, tmArr);
        Syn.ResetDynamics();
        Cond = new ConductanceStack(n);
        Transmitters = new TransmitterSystem(config.Regions.Count);
        Recorder = new SpikeRecorder(n);

        _ring = new List<int>[RingSize];
        for (int i = 0; i < RingSize; i++) _ring[i] = [];
    }

    /// <summary>External current injection (sensory encoders, perturbation tests). Persists for this tick.</summary>
    public void InjectCurrent(int neuron, float pA) => Injected[neuron] += pA;

    /// <summary>Advance the network by 1 ms of simulated time.</summary>
    public void Tick()
    {
        int slot = (int)(TickIndex % RingSize);
        var due = _ring[slot];
        _ring[slot] = [];
        DeliverDue(due);

        // Per-tick intrinsic noise (deterministic hash, seed-mixed; one draw per neuron).
        for (int i = 0; i < NoiseBuf.Length; i++)
            NoiseBuf[i] = Config.NoiseSigmaMv * HashNoise(i, TickIndex, _noiseSalt);

        const float dt = 0.5f;
        for (int sub = 0; sub < 2; sub++)
        {
            for (int i = 0; i < V.Length; i++)
            {
                int t = (int)Types[i];
                float v = V[i], u = U[i];
                float iTotal = Injected[i] + NoiseBuf[i] + Cond.Current(i, v);
                v += dt * (0.04f * v * v + 5f * v + 140f - u + iTotal);
                u += dt * (IzhikevichParams.A[t] * (IzhikevichParams.B[t] * v - u));
                if (v >= IzhikevichParams.SpikeThresholdMv)
                {
                    float tc = IzhikevichParams.C[t];
                    v = tc;
                    u += IzhikevichParams.D[t];
                    OnSpike(i);
                }
                V[i] = v; U[i] = u;
            }
        }

        Cond.DecayAll();
        Transmitters.Step();
        float flux = 0f;
        for (int k = 0; k < TransmitterSystem.KindCount; k++) flux += Transmitters.LastUptakeFlux[k];
        Energy.AddReuptake(flux);
        Array.Clear(Injected);
        TickIndex++;
    }

    private void DeliverDue(List<int> due)
    {
        foreach (int s in due)
        {
            float isi = TickIndex - Syn.LastDeliveryMs[s];
            Syn.LastDeliveryMs[s] = TickIndex;
            float release = TsodyksMarkram.OnSpike(ref Syn.TmR[s]!, ref Syn.TmU[s]!, isi, TmParams.For(Syn.Tm[s]));
            float w = Syn.WeightQ15[s] * SynapseGraph.Q15Scale; // signed
            float amount = MathF.Abs(w) * release;
            int post = Syn.PostIdx[s];
            bool inhibitory = w < 0f;
            float gated = inhibitory ? amount * Config.GlobalGabaGain : amount;
            Cond.Deliver(post, Syn.Receptors[s], gated);
            Transmitters.AddRelease(
                RegionOf[post],
                inhibitory ? TransmitterKind.Gaba : TransmitterKind.Glutamate,
                amount * _transmitterPerSpike);
            Energy.AddVesicleRecycle(release);
        }
    }

    private void OnSpike(int neuron)
    {
        Recorder.Record(TickIndex + 0.5f, neuron);
        Energy.AddSpike();
        for (int s = Syn.RowPtr[neuron]; s < Syn.RowPtr[neuron + 1]; s++)
        {
            int slot = (int)((TickIndex + Syn.DelayMs[s]) % RingSize);
            _ring[slot].Add(s);
        }
    }

    /// <summary>SplitMix-hash uniform noise in [−1,1) - cheap, seed-mixed, call-order independent.</summary>
    private static float HashNoise(int i, long tick, ulong salt)
    {
        ulong z = (unchecked((ulong)(uint)i) * 0x9E3779B97F4A7C15UL)
                  ^ (unchecked((ulong)tick) * 0xC2B2AE3D27D4EB4FUL) ^ salt;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 40) * (1f / (1 << 24)) - 0.5f; // [−0.5, 0.5)
    }

    /// <summary>FNV-1a over membrane/recovery state, RNG state and counters - the determinism test key.</summary>
    public ulong ComputeTrajectoryHash()
    {
        ulong h = 0xCBF29CE484222325UL;
        void Mix(ulong x) { h ^= x; h *= 0x100000001B3UL; }
        for (int i = 0; i < V.Length; i++)
        {
            Mix(unchecked((ulong)BitConverter.SingleToInt32Bits(V[i])));
            Mix(unchecked((ulong)BitConverter.SingleToInt32Bits(U[i])));
        }
        foreach (var s in Rng.GetState()) Mix(s);
        Mix((ulong)Recorder.TotalSpikes);
        Mix(unchecked((ulong)BitConverter.SingleToInt32Bits(Energy.Spent)));
        return h;
    }
}
