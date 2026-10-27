using BioComp.Core.Metrics;
using Xunit;

namespace BioCore.Tests;

public class AvalancheTests
{
    [Fact]
    public void BranchingRatio_OnSyntheticSequence()
    {
        var m = new AvalancheMetrics();
        // bins: [1,3,2,0,5,0,1] - positive pairs are (1,3),(3,2) and the lone runs end before zeros
        int[] counts = [1, 3, 2, 0, 5, 0, 1];
        for (int i = 0; i < counts.Length; i++)
            for (int k = 0; k < 4; k++) m.OnTick(i * 4 + k, k == 0 ? counts[i] : 0);
        // sigma = (3+2) / (1+3) = 1.25
        Assert.InRange(m.BranchingRatio(), 1.24f, 1.26f);
    }

    [Fact]
    public void AvalancheSizes_CountRunsOfActiveBins()
    {
        var m = new AvalancheMetrics();
        int[] counts = [1, 3, 2, 0, 5, 0, 1];
        for (int i = 0; i < counts.Length; i++)
            for (int k = 0; k < 4; k++) m.OnTick(i * 4 + k, k == 0 ? counts[i] : 0);
        var sizes = m.AvalancheSizes();
        Assert.Equal([6, 5, 1], sizes);
    }

    [Fact]
    public void EmptyRecording_GivesZeroSigma()
    {
        var m = new AvalancheMetrics();
        Assert.Equal(0f, m.BranchingRatio());
    }
}
