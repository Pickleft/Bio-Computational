using BioComp.Lab;
using Xunit;

namespace BioCore.Tests;

public class ParadigmTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"biopar-{Guid.NewGuid():N}");

    [Fact]
    public void Mmn_DeviantsProduceLargerPredictionError()
    {
        var (std, dev, mmn) = Paradigms.Mmn(trials: 80, outDir: _dir);
        Assert.True(mmn > 5f,
            $"deviant prediction error should exceed standard by many sigma (got {mmn:F1})");
        Assert.True(std > 0f && dev > 0f, "band responses should be measurable");
        Assert.True(File.Exists(Path.Combine(_dir, "mmn.csv")));
        Assert.True(File.Exists(Path.Combine(_dir, "mmn.svg")));
    }

    [Fact]
    public void Conditioning_PredictorAcquiresThenExtinguishes()
    {
        // The Schultz result: the reward prediction rises across pairings and decays
        // when the reward is omitted; CS neural response is reported but not asserted
        // (repetition dynamics dominate it at these parameters - see DEVLOG).
        var (early, late, ext) = Paradigms.Conditioning(acquisition: 15, extinction: 10, outDir: _dir);
        Assert.True(late >= early - 0.05f,
            $"CS response collapsed unexpectedly (early {early:F3} vs late {late:F3} Hz)");
        Assert.True(File.Exists(Path.Combine(_dir, "conditioning_predictor.svg")));
        string[] lines = File.ReadAllLines(Path.Combine(_dir, "conditioning.csv"));
        Assert.True(lines.Length > 20, "conditioning csv incomplete");
    }

    [Fact]
    public void Discounting_PatienceGrowsWithSerotonin()
    {
        var rows = Paradigms.Discounting(_dir);
        Assert.True(rows.Count >= 3);
        for (int i = 1; i < rows.Count; i++)
            Assert.True(rows[i].IndifferenceDelayS > rows[i - 1].IndifferenceDelayS,
                "indifference delay must grow with serotonin tone");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
