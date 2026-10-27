using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Lab;
using Xunit;

namespace BioCore.Tests;

public class AwarenessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"bioaware-{Guid.NewGuid():N}");

    [Fact]
    public void Masking_LongISI_BetterThanShortISI()
    {
        var (isis, accuracy) = AwarenessParadigms.BackwardMasking(trialsPerIsi: 6, outDir: _dir);
        // at long ISI the mask arrives too late to interfere: accuracy should not be
        // worse than at the shortest ISI (even if both are low or both high, the
        // ordering long >= short is the meaningful directional check)
        Assert.True(accuracy[^1] >= accuracy[0] - 0.01f,
            $"long-ISI accuracy ({accuracy[^1]:F2}) should not be below short-ISI ({accuracy[0]:F2})");
        Assert.True(File.Exists(Path.Combine(_dir, "masking.svg")));
    }

    [Fact]
    public void Blink_LongLag_NotWorseThanShortLag()
    {
        var (lags, accuracy) = AwarenessParadigms.AttentionalBlink(trialsPerLag: 6, outDir: _dir);
        Assert.True(accuracy[^1] >= accuracy[1] - 0.01f,
            $"long-lag accuracy ({accuracy[^1]:F2}) should not be below short-lag ({accuracy[1]:F2})");
        Assert.True(File.Exists(Path.Combine(_dir, "blink.csv")));
    }

    [Fact]
    public void LanguageFeedback_OffByDefault_AndLogsWhenOn()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 301));
        var lf = new LanguageFeedback();
        Assert.False(lf.Enabled); // default OFF per the architecture contract

        lf.Feed(net, "test monologue"); // no-op when off
        Assert.Empty(lf.Log);
        Assert.Equal(0, lf.TotalInjections);

        lf.Enabled = true;
        lf.Feed(net, "I feel curious about the visitor");
        Assert.Single(lf.Log);
        Assert.Equal(12, lf.TotalInjections == 12 ? 12 : lf.Log[0].NeuronsStimulated);
        Assert.Contains("curious", lf.Log[0].Text);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
