using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Synapses;
using Xunit;

namespace BioCore.Tests;

public class StdpTests
{
    /// <summary>Two 16-neuron regions with one projection; returns a noiseless network plus
    /// an excitatory pre in region A and post in region B connected by one synapse.</summary>
    private static (SpikingNetwork Net, int Pre, int Post, int Syn) BuildPair()
    {
        var cfg = new BrainConfig();
        cfg.Regions.Add(new RegionSpec("A", 16, 0f, 0f));
        cfg.Regions.Add(new RegionSpec("B", 16, 2.5f, 0f));
        cfg.Projections.Add(new ProjectionSpec("A", "B", OutDegree: 6, WeightMean: 0.05f));
        cfg.NoiseSigmaMv = 0f;
        var net = new SpikingNetwork(cfg);

        for (int pre = 0; pre < 16; pre++)
        {
            if (net.Inhibitory[pre]) continue;
            for (int s = net.Syn.RowPtr[pre]; s < net.Syn.RowPtr[pre + 1]; s++)
            {
                int post = net.Syn.PostIdx[s];
                if (post >= 16 && !net.Inhibitory[post])
                    return (net, pre, post, s);
            }
        }
        throw new InvalidOperationException("no excitatory pair found");
    }

    private static (short Initial, short Final) WeightAfter(bool preFirst, float dopaminePerCycle, int cycles)
    {
        var (net, pre, post, s) = BuildPair();
        net.Learning.EtaBase = 0.02f; // visible in a short test
        short initial = net.Syn.WeightQ15[s];
        int first = preFirst ? pre : post;
        int second = preFirst ? post : pre;

        for (int c = 0; c < cycles; c++)
        {
            long t0 = net.TickIndex;
            for (int i = 0; i < 5; i++) { net.InjectCurrent(first, 30f); net.Tick(); }
            while (net.TickIndex - t0 < 10) net.Tick();   // 10 ms interval
            for (int i = 0; i < 5; i++) { net.InjectCurrent(second, 30f); net.Tick(); }
            if (dopaminePerCycle > 0f)
                net.Modulators.Release(NeuromodulatorKind.Dopamine, dopaminePerCycle);
            while (net.TickIndex - t0 < 50) net.Tick();   // 20 Hz pairing cycle
        }
        return (initial, net.Syn.WeightQ15[s]);
    }

    [Fact]
    public void PreBeforePost_WithDopamine_Potentiates()
    {
        var (initial, after) = WeightAfter(preFirst: true, dopaminePerCycle: 0.6f, cycles: 40);
        Assert.True(after > initial + 200, $"LTP too weak: {initial} -> {after}");
    }

    [Fact]
    public void PostBeforePre_WithDopamine_Depresses()
    {
        var (initial, after) = WeightAfter(preFirst: false, dopaminePerCycle: 0.6f, cycles: 40);
        Assert.True(after < initial - 100, $"LTD too weak: {initial} -> {after}");
    }

    [Fact]
    public void BaselineDopamine_NoDrift()
    {
        var (initial, after) = WeightAfter(preFirst: true, dopaminePerCycle: 0f, cycles: 40);
        Assert.InRange(after, initial - 150, initial + 150);
    }

    [Fact]
    public void DaleSurvivesPlasticity()
    {
        var (net, pre, post, _) = BuildPair();
        net.Learning.EtaBase = 0.05f;
        for (int c = 0; c < 30; c++)
        {
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.8f);
            for (int i = 0; i < 5; i++) { net.InjectCurrent(pre, 30f); net.InjectCurrent(post, 30f); net.Tick(); }
            for (int i = 0; i < 45; i++) net.Tick();
        }
        for (int s = 0; s < net.Syn.SynapseCount; s++)
        {
            bool inh = net.Inhibitory[net.Syn.PreIdx[s]];
            Assert.True(inh ? net.Syn.WeightQ15[s] < 0 : net.Syn.WeightQ15[s] > 0,
                $"Dale violated at synapse {s} after plasticity");
        }
    }
}
