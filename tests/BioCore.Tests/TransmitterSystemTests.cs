using BioComp.Core.Neuromodulators;
using Xunit;

namespace BioCore.Tests;

public class TransmitterSystemTests
{
    [Fact]
    public void ConstantRelease_ConvergesToAnalyticSteadyState()
    {
        // Release R per ms into one region; steady state solves Vmax·T/(Km+T) = R.
        var sys = new TransmitterSystem(regionCount: 2);
        float vMax = 0.5f, km = 0.4f, releasePerMs = 0.05f;
        float expected = TransmitterSystem.SteadyState(releasePerMs, vMax, km);

        for (int t = 0; t < 200; t++) // ~200·τ around the linearized fixed point
        {
            sys.AddRelease(0, TransmitterKind.Dopamine, releasePerMs);
            sys.Step();
        }
        float actual = sys.Get(0, TransmitterKind.Dopamine);
        Assert.InRange(actual, expected * 0.9f, expected * 1.1f);
    }

    [Fact]
    public void NoRelease_PoolDecaysToZero()
    {
        var sys = new TransmitterSystem(1);
        sys.Set(0, TransmitterKind.Glutamate, 1.0f);
        for (int t = 0; t < 200; t++) sys.Step();
        Assert.True(sys.Get(0, TransmitterKind.Glutamate) < 0.01f, "pool did not clear");
    }

    [Fact]
    public void UptakeFlux_IsReportedForEnergy()
    {
        var sys = new TransmitterSystem(1);
        sys.Set(0, TransmitterKind.Gaba, 1.0f);
        sys.Step();
        Assert.True(sys.LastUptakeFlux[(int)TransmitterKind.Gaba] > 0f);
    }
}
