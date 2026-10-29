using BioComp.Core.Energy;
using BioComp.Core.Learning;
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
    public NeuromodulatorSystem Modulators { get; }
    public ThreeFactorStdp Learning { get; private set; } = null!;
    public AvalancheMetrics Avalanches { get; } = new();
    public BioComp.Core.Memory.EpisodeMemory EpisodeStore { get; } = new();

    public readonly float[] V, U, Injected, NoiseBuf, LastPostSpikeMs;
    public readonly uint[] WindowCounts;
    public readonly NeuronType[] Types;
    public readonly bool[] Inhibitory;
    public readonly ushort[] RegionOf;
    public readonly float[] PosX, PosY;
    public IReadOnlyList<RegionInfo> Regions { get; }

    public long TickIndex { get; internal set; }
    public float TimeMs => TickIndex;
    public int SpikesThisTick { get; internal set; }
    public float RewardPredictor { get; set; }
    /// <summary>Anesthesia depth 0..1: GABA-A-like hyperpolarizing bias on all
    /// neurons (Brown, Lydic and Schiff 2010). Set by the host; washes out
    /// passively so emergence is gradual.</summary>
    public float SedationLevel { get; set; }

    public const int HomeostasisIntervalTicks = 60_000;

    private readonly List<int>[] _ring;
    private readonly ulong _noiseSalt;
    private readonly float _transmitterPerSpike = 0.02f; // CALIBRATE (pool scale)
    private float _achCaptureScale = 1f;
    private float _noradrenalineGain = 0.2f;

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
        LastPostSpikeMs = new float[n]; WindowCounts = new uint[n];
        Array.Fill(LastPostSpikeMs, -1e9f);
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
        Modulators = new NeuromodulatorSystem();
        Learning = new ThreeFactorStdp(Syn);

        _ring = new List<int>[RingSize];
        for (int i = 0; i < RingSize; i++) _ring[i] = [];
    }

    /// <summary>External current injection (sensory encoders, perturbation tests). Persists for this tick.</summary>
    public void InjectCurrent(int neuron, float pA) => Injected[neuron] += pA;

    /// <summary>Delay-ring access for checkpointing.</summary>
    public List<int> RingAt(int slot) => _ring[slot];

    /// <summary>
    /// External reward (0..1). Computes a scalar reward prediction error against a running
    /// expectation and releases dopamine accordingly: bursts on positive error, pauses on
    /// negative (Schultz 1997). Positive prediction errors also nudge oxytocin.
    /// </summary>
    public void ApplyReward(float reward)
    {
        float rpe = reward - RewardPredictor;
        RewardPredictor += 0.05f * rpe;
        Modulators.Release(NeuromodulatorKind.Dopamine, 1.2f * rpe);
        if (rpe > 0.05f) Modulators.Release(NeuromodulatorKind.Oxytocin, 0.15f * rpe);
    }

    /// <summary>Social feedback on the agent's own output (was speaking welcomed?).</summary>
    public void ApplySocialFeedback(bool positive)
    {
        if (positive) Modulators.Release(NeuromodulatorKind.Oxytocin, 0.3f);
        else Modulators.Release(NeuromodulatorKind.Dopamine, -0.3f);
    }

    /// <summary>Advance the network by 1 ms of simulated time.</summary>
    public void Tick()
    {
        SpikesThisTick = 0;
        _achCaptureScale = 0.5f + Modulators.Get(NeuromodulatorKind.Acetylcholine);
        _noradrenalineGain = Modulators.Get(NeuromodulatorKind.Noradrenaline);

        // ANESTHESIA (Brown, Lydic and Schiff 2010): a hyperpolarizing bias on
        // every neuron - the network effect of GABA-A potentiation (propofol).
        // Fades passively so emergence is gradual, like washout.
        float gaba = SedationLevel * 8f;
        if (SedationLevel > 0.001f && TickIndex % 20 == 0) SedationLevel *= 0.99f;

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
                float iTotal = Injected[i] + NoiseBuf[i] + Cond.Current(i, v) - gaba;
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

        // Three-factor weight update: eligibility traces decay, dopamine tone gates.
        Learning.DecayAndApply(Syn, Modulators.Get(NeuromodulatorKind.Dopamine),
            Modulators.Get(NeuromodulatorKind.Serotonin));

        Cond.DecayAll();
        Transmitters.Step();
        float flux = 0f;
        for (int k = 0; k < TransmitterSystem.KindCount; k++) flux += Transmitters.LastUptakeFlux[k];
        Energy.AddReuptake(flux);
        Modulators.Step();
        Avalanches.OnTick(TickIndex, SpikesThisTick);
        if (TickIndex > 0 && TickIndex % HomeostasisIntervalTicks == 0)
        {
            Learning.HomeostaticScaling(Syn, WindowCounts, HomeostasisIntervalTicks / 1000f);
            Array.Clear(WindowCounts);
        }
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
            // Noradrenaline raises excitatory gain during surprise (arousal).
            float gated = inhibitory
                ? amount * Config.GlobalGabaGain
                : amount * (0.7f + 0.6f * _noradrenalineGain);
            Learning.OnPreDelivery(s, post, TickIndex, LastPostSpikeMs, _achCaptureScale);
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
        float t = TickIndex + 0.5f;
        Recorder.Record(t, neuron);
        Energy.AddSpike();
        SpikesThisTick++;
        LastPostSpikeMs[neuron] = t;
        WindowCounts[neuron]++;
        Learning.OnPostSpike(neuron, t, Syn, _achCaptureScale);
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
