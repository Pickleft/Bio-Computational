using BioComp.Core.Metrics;
using Xunit;

namespace BioCore.Tests;

public class LfpTests
{
    [Fact]
    public void Goertzel_DetectsAlphaInA10HzSignal()
    {
        var lfp = new LfpProxy();
        // two full analysis windows of a clean 10 Hz oscillation
        for (int ms = 0; ms < 3 * LfpProxy.WindowMs; ms++)
            lfp.AddSample(MathF.Sin(2f * MathF.PI * 10f * ms / 1000f));
        Assert.True(lfp.Bands[2] > 50f * lfp.Bands[0],
            $"alpha power should dominate delta: {lfp.Bands[2]:E2} vs {lfp.Bands[0]:E2}");
        Assert.True(lfp.AlphaRatio > 5f, $"alpha ratio {lfp.AlphaRatio:F1} should be large for pure alpha");
    }

    [Fact]
    public void Slope_IsNegativeForFallingSpectrum()
    {
        var lfp = new LfpProxy();
        // pink-ish input: slow large waves plus tiny fast ripple -> power falls with freq
        for (int ms = 0; ms < 3 * LfpProxy.WindowMs; ms++)
        {
            float slow = 5f * MathF.Sin(2f * MathF.PI * 2f * ms / 1000f);
            float fast = 0.02f * MathF.Sin(2f * MathF.PI * 60f * ms / 1000f);
            lfp.AddSample(slow + fast);
        }
        Assert.True(lfp.Slope < 0f, $"1/f slope should be negative, got {lfp.Slope:F2}");
    }

    [Fact]
    public void History_StaysBounded()
    {
        var lfp = new LfpProxy();
        for (int ms = 0; ms < 40 * LfpProxy.WindowMs; ms++)
            lfp.AddSample(MathF.Sin(ms * 0.01f));
        Assert.True(lfp.History.Count <= LfpProxy.HistoryColumns);
    }
}
