using BioComp.Core.Synapses;
using Xunit;

namespace BioCore.Tests;

public class ShortTermPlasticityTests
{
    [Fact]
    public void HighFrequencyTrain_DepressesRelease()
    {
        // Tsodyks–Markram depressing class: 20 Hz train, releases must strictly decrease.
        var p = TmParams.For(TmClass.Depressing);
        float r = 1f, u = 0f;
        float prev = float.MaxValue;
        for (int spike = 0; spike < 10; spike++)
        {
            float release = TsodyksMarkram.OnSpike(ref r, ref u, 50f, p); // ISI = 50 ms
            Assert.True(release < prev, $"release did not decrease at spike {spike}: {release} >= {prev}");
            prev = release;
        }
        // Depleted: release should be a small fraction of the first release.
        Assert.True(prev < 0.25f * TmParams.For(TmClass.Depressing).U, "pool did not deplete");
    }

    [Fact]
    public void LongPause_RecoversVesicles()
    {
        var p = TmParams.For(TmClass.Depressing);
        float r = 1f, u = 0f;
        float first = TsodyksMarkram.OnSpike(ref r, ref u, 10_000f, p);
        for (int i = 0; i < 10; i++) TsodyksMarkram.OnSpike(ref r, ref u, 50f, p); // deplete
        float recovered = TsodyksMarkram.OnSpike(ref r, ref u, 10_000f, p);         // 10 s rest
        Assert.True(recovered > 0.8f * first,
            $"recovery incomplete: {recovered:F3} vs first {first:F3} (τrec={p.TauRecMs} ms)");
    }

    [Fact]
    public void FacilitatingClass_ReleaseGrowsInitially()
    {
        var p = TmParams.For(TmClass.Facilitating);
        float r = 1f, u = 0f;
        float first = TsodyksMarkram.OnSpike(ref r, ref u, 100f, p);
        float second = TsodyksMarkram.OnSpike(ref r, ref u, 100f, p);
        float third = TsodyksMarkram.OnSpike(ref r, ref u, 100f, p);
        Assert.True(second > first && third > second,
            $"facilitation absent: {first:F3} -> {second:F3} -> {third:F3}");
    }
}
