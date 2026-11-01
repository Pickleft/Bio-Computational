using BioComp.Core.Cognition;
using BioComp.Core.Network;
using Xunit;

namespace BioCore.Tests;

public class LifeStreamTests
{
    private static SpikingNetwork Brain(int seed = 7) =>
        new(BrainConfig.Default(scale: 1, seed: (ulong)seed));

    private static void RunFor(LifeStream life, SpikingNetwork net, string thought, int chains,
        int tickCount = 5000)
    {
        for (int i = 0; i < tickCount; i++)
        {
            net.Tick();
            life.Tick(net, thought, chains, null, null, 0f, personPresent: true);
        }
    }

    [Fact]
    public void Preoccupation_EmergesOnlyFromCrossChainRecurrence()
    {
        var net = Brain();
        var life = new LifeStream();
        // same theme across THREE different chains = sustained concern
        RunFor(life, net, "the door did not open again today", chains: 1);
        RunFor(life, net, "still thinking about the door and the quiet", chains: 2);
        RunFor(life, net, "the door, the door — what happens after the door", chains: 3);
        Assert.NotNull(life.Preoccupation);
        Assert.True(life.PreoccupationStrength > 0.3f,
            $"sustained cross-chain theme should build strength, got {life.PreoccupationStrength}");
    }

    [Fact]
    public void Preoccupation_IgnoredWhenThemeAppearsInOneChainOnly()
    {
        var net = Brain();
        var life = new LifeStream();
        // same chain the whole time (chainsStarted never advances)
        RunFor(life, net, "the door did not open again today", chains: 1);
        Assert.Null(life.Preoccupation);
        Assert.Equal(0f, life.PreoccupationStrength);
    }

    [Fact]
    public void Determinism_SameSeedSameLifeStream()
    {
        string Describe(LifeStream life) =>
            $"{life.Preoccupation}|{life.PreoccupationStrength:F3}|{life.SelfReflection}";

        var a = new LifeStream();
        var na = Brain(seed: 42);
        RunFor(a, na, "wondering what the garden sounds like at night", chains: 2);
        RunFor(a, na, "the garden again — it keeps coming back", chains: 3);

        var b = new LifeStream();
        var nb = Brain(seed: 42);
        RunFor(b, nb, "wondering what the garden sounds like at night", chains: 2);
        RunFor(b, nb, "the garden again — it keeps coming back", chains: 3);

        Assert.Equal(Describe(a), Describe(b));
    }

    [Fact]
    public void ShareLine_RotatesAndNeverRepeatsBackToBack()
    {
        var net = Brain();
        var life = new LifeStream();
        RunFor(life, net, "the unanswered question about the signal", chains: 1);
        RunFor(life, net, "the signal keeps returning in different forms", chains: 2);
        var first = Assert.IsType<string>(life.ShareLine(null));
        var second = life.ShareLine(null);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ChooseResponse_ShareWhenConcernIsStrongAndBonded()
    {
        var net = Brain();
        var life = new LifeStream();
        RunFor(life, net, "the door did not open again today", chains: 1);
        RunFor(life, net, "still thinking about the door and the quiet", chains: 2);
        // strong preoccupation + bond + rng draw in the share window
        Assert.Equal(LifeStream.ResponseMode.Share,
            life.ChooseResponse("hello", 0f, bond: 0.6f, life.PreoccupationStrength, () => 0.1));
        // no bond to hold it -> echo instead
        Assert.Equal(LifeStream.ResponseMode.Echo,
            life.ChooseResponse("hello", 0f, bond: 0.05f, life.PreoccupationStrength, () => 0.1));
    }

    [Fact]
    public void Restore_KeepsConcernAliveAcrossRestart()
    {
        var net = Brain();
        var life = new LifeStream();
        life.Restore("the sound of the door not opening", 0.7f, "noticing the quiet is affecting me", net.TimeMs);
        Assert.Equal("the sound of the door not opening", life.Preoccupation);
        Assert.Equal(0.7f, life.PreoccupationStrength, 2);
        Assert.NotNull(life.SelfReflection);
        // the restored concern still drives a Share when bonded
        Assert.Equal(LifeStream.ResponseMode.Share,
            life.ChooseResponse("hi", 0f, 0.6f, life.PreoccupationStrength, () => 0.1));
    }
}
