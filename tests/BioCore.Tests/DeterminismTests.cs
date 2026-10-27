using BioComp.Core.Network;
using BioComp.Core.Rng;
using Xunit;

namespace BioCore.Tests;

public class DeterminismTests
{
    /// <summary>Drives the net with sustained tonic current into a fixed random neuron sample
    /// (sensory-like drive; spontaneous-activity tuning arrives with M2 on D2).</summary>
    private static (ulong Hash, float MeanRate, long Spikes) RunScripted(ulong brainSeed, int ticks)
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: brainSeed));
        var script = new Xoshiro256StarStar(123); // same sensory script for every run
        int n = net.V.Length;
        var driven = new int[50];
        for (int k = 0; k < driven.Length; k++) driven[k] = script.NextInt(n);
        for (long t = 0; t < ticks; t++)
        {
            foreach (int d in driven) net.InjectCurrent(d, 10f);
            net.Tick();
        }
        return (net.ComputeTrajectoryHash(), net.Recorder.MeanRateHz(ticks), net.Recorder.TotalSpikes);
    }

    [Fact]
    public void SameSeedAndScript_IdenticalTrajectory()
    {
        var a = RunScripted(42, 1500);
        var b = RunScripted(42, 1500);
        Assert.Equal(a.Hash, b.Hash);
        Assert.Equal(a.Spikes, b.Spikes);
        Assert.Equal(a.MeanRate, b.MeanRate);
    }

    [Fact]
    public void DifferentSeed_DifferentTrajectory()
    {
        var a = RunScripted(42, 1500);
        var b = RunScripted(1337, 1500);
        Assert.NotEqual(a.Hash, b.Hash);
    }

    [Fact]
    public void DrivenNetwork_IsAliveButNotSeizing()
    {
        var (hash, meanRate, _) = RunScripted(42, 1500);
        Assert.NotEqual(0UL, hash);
        Assert.InRange(meanRate, 0.05f, 150f); // loose M1 bounds; criticality tuning is D2
    }
}
