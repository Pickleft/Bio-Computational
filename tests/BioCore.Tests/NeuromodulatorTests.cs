using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using Xunit;

namespace BioCore.Tests;

public class NeuromodulatorTests
{
    [Fact]
    public void StartsAtBaseline()
    {
        var m = new NeuromodulatorSystem();
        foreach (NeuromodulatorKind k in Enum.GetValues<NeuromodulatorKind>())
            Assert.Equal(NeuromodulatorSystem.BaselineOf(k), m.Get(k), 5);
    }

    [Fact]
    public void ReleaseRaisesTone_ThenRelaxes()
    {
        var m = new NeuromodulatorSystem();
        float baseline = NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Dopamine);
        m.Release(NeuromodulatorKind.Dopamine, 0.5f);
        m.Step();
        Assert.True(m.Get(NeuromodulatorKind.Dopamine) > baseline, "dopamine burst did not register");
        for (int t = 0; t < 20_000; t++) m.Step(); // several dopamine time constants
        Assert.InRange(m.Get(NeuromodulatorKind.Dopamine), baseline, baseline + 0.02f);
    }

    [Fact]
    public void NegativeRpe_CausesDopaminePause()
    {
        var m = new NeuromodulatorSystem();
        float baseline = NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Dopamine);
        m.Release(NeuromodulatorKind.Dopamine, -0.5f);
        m.Step();
        Assert.True(m.Get(NeuromodulatorKind.Dopamine) < baseline, "negative error should dip dopamine below baseline");
        Assert.True(m.Get(NeuromodulatorKind.Dopamine) >= 0f, "tone must clamp at zero");
    }

    [Fact]
    public void RepeatedReward_AdaptingPredictionError()
    {
        // Schultz 1997: identical repeated rewards shrink the dopamine response as the
        // running prediction approaches the reward.
        var net = new SpikingNetwork(BrainConfig.Default(seed: 3));
        float d1 = DaJump(net, 1f);
        float dLate = 0f;
        for (int i = 0; i < 60; i++) dLate = DaJump(net, 1f);
        Assert.True(d1 > dLate + 0.01f, $"dopamine response should adapt: first {d1:F3} vs late {dLate:F3}");
        Assert.True(net.RewardPredictor > 0.9f, $"predictor should converge, is {net.RewardPredictor:F3}");

        static float DaJump(SpikingNetwork net, float reward)
        {
            float before = net.Modulators.Get(NeuromodulatorKind.Dopamine);
            net.ApplyReward(reward);
            net.Modulators.Step();
            return net.Modulators.Get(NeuromodulatorKind.Dopamine) - before;
        }
    }
}
