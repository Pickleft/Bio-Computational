using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Time;
using Xunit;

namespace BioCore.Tests;

public class TimeSenseTests
{
    [Fact]
    public void AtBaselineTime_PassesAtRealRate()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 2));
        var t = new TimeSense();
        for (int i = 0; i < 10_000; i++) t.Tick(net);
        Assert.InRange(t.ClockRate, 0.99f, 1.01f);
        Assert.InRange(t.SubjectiveMs, 9_900, 10_100);
    }

    [Fact]
    public void DopamineBurst_SpeedsTheClock()
    {
        // Two independent runs: baseline vs a dopamine burst sustained through the window.
        double Run(bool withDopamine)
        {
            var net = new SpikingNetwork(BrainConfig.Default(seed: 2));
            var t = new TimeSense();
            for (int i = 0; i < 10_000; i++)
            {
                if (withDopamine && i % 100 == 0)
                    net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.5f);
                net.Tick(); // advances modulator tones so the release takes effect
                t.Tick(net);
            }
            return t.SubjectiveMs;
        }
        double baseline = Run(false);
        double arousedMs = Run(true);
        Assert.True(arousedMs > baseline * 1.2,
            $"subjective time under dopamine ({arousedMs:F0}) should outrun baseline ({baseline:F0})");
    }

    [Fact]
    public void CellsDecay_AndResetOnEvent()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 2));
        var t = new TimeSense();
        t.OnEvent();
        Assert.All(t.Cells, c => Assert.Equal(1f, c, 3));
        for (int i = 0; i < 60_000; i++) t.Tick(net); // one minute
        Assert.True(t.Cells[0] < 0.01f, "short-tau cells should be gone");
        for (int i = 1; i < t.Cells.Length; i++)
            Assert.True(t.Cells[i - 1] <= t.Cells[i] + 1e-6f, "decay must be ordered by tau");
        Assert.True(t.SubjectiveSinceEventMs > 50_000);
    }

    [Fact]
    public void Context_SnapshotsDifferOverTime()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 2));
        var t = new TimeSense();
        t.OnEvent();
        var early = t.Context();
        for (int i = 0; i < 5_000; i++) t.Tick(net);
        var late = t.Context();
        Assert.NotEqual(early, late);
    }
}
