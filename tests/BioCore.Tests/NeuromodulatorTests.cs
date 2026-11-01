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

    [Fact]
    public void SaturatingUptake_NoEventStreamCanPegATransmitter()
    {
        var m = new NeuromodulatorSystem();
        float ceiling = NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Acetylcholine)
            + NeuromodulatorSystem.UptakeRange + 1e-3f;
        // sustained heavy release (an ignition every second, forever)
        for (int i = 0; i < 60_000; i++)   // 60 s
        {
            m.Release(NeuromodulatorKind.Acetylcholine, 0.5f);
            m.Step();
        }
        float ach = m.Get(NeuromodulatorKind.Acetylcholine);
        Assert.True(ach <= ceiling, $"ACh must saturate near baseline+range, got {ach}");
        // and it must be HIGH (saturation is not a squash of normal traffic)
        Assert.True(ach > 0.8f, $"sustained attention should still drive ACh high, got {ach}");
        // when the flood stops, tone relaxes back toward baseline
        for (int i = 0; i < 120_000; i++) m.Step();   // 120 s, tau is 5 s
        Assert.True(m.Get(NeuromodulatorKind.Acetylcholine) < 0.5f,
            "ACh should relax back toward baseline once the flood stops");
    }

    [Fact]
    public void SaturatingUptake_MildTrafficStaysWellBelowCeiling()
    {
        var m = new NeuromodulatorSystem();
        // sparse releases: one 0.3 burst every 5 s for a minute
        for (int i = 0; i < 60_000; i++)
        {
            if (i % 5_000 == 0) m.Release(NeuromodulatorKind.Noradrenaline, 0.3f);
            m.Step();
        }
        float na = m.Get(NeuromodulatorKind.Noradrenaline);
        // sparse surprise traffic must NOT look like overload (ceiling is 1.2)
        Assert.InRange(na, 0.2f, 0.6f);
    }
}
