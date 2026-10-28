using BioComp.Core.Cognition;
using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Replay;
using Xunit;

namespace BioCore.Tests;

public class InterconnectionTests
{
    private static (SpikingNetwork Net, IdleController Idle) Brain(int seed = 9)
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: (ulong)seed));
        return (net, new IdleController(net));
    }

    // ---- A: REM reconsolidation strips emotional charge in low-stress sleep ----

    [Fact]
    public void Replay_SoftensValenceAndStrengthens_WhenSleepIsCalm()
    {
        var (net, idle) = Brain();
        for (int i = 0; i < 400; i++) net.Tick();
        var ep = net.EpisodeStore.Encode(net, "CA3", ["hurt", "voice"], -0.8f, minMeanRateHz: 0.0001f);
        Assert.NotNull(ep);
        float valBefore = net.EpisodeStore.Episodes[0].Valence;

        // calm sleep: replays run directly (NREM state), noradrenaline stays low
        for (int i = 0; i < 40_000 && net.EpisodeStore.Episodes[0].Valence < -0.55f; i++)
        {
            idle.Replay.Tick(net, replayAllowed: true);
            net.Tick();
        }
        var after = net.EpisodeStore.Episodes[0];
        Assert.True(after.Valence > valBefore + 0.15f,
            $"calm replay should soften negative valence toward neutral ({valBefore} -> {after.Valence})");
        Assert.True(after.AccessCount >= 1, "replay should strengthen the trace (consolidation)");
    }

    [Fact]
    public void Replay_StressedSleepKeepsTheCharge()
    {
        var (net, idle) = Brain(seed: 13);
        for (int i = 0; i < 400; i++) net.Tick();
        var ep = net.EpisodeStore.Encode(net, "CA3", ["hurt"], -0.8f, minMeanRateHz: 0.0001f);
        float valBefore = net.EpisodeStore.Episodes[0].Valence;

        // flood noradrenaline: stressed sleep fails to heal (Walker 2009)
        for (int i = 0; i < 40_000; i++)
        {
            net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 2.0f);
            idle.Replay.Tick(net, replayAllowed: true);
            net.Tick();
        }
        Assert.True(net.EpisodeStore.Episodes[0].Valence <= valBefore + 0.05f,
            "stressed sleep should not strip the emotional charge");
    }

    // ---- B: social bonds - depth, missing, recognition reward ----

    [Fact]
    public void Bonds_DeepenWithWarmContacts_AndMissTheStrongest()
    {
        var bonds = new SocialBondSystem();
        for (int i = 0; i < 10; i++) bonds.OnContact("close-friend", 0.9f, 100_000f);
        bonds.OnContact("stranger", 0.5f, 100_000f);
        Assert.True(bonds.Strength("close-friend", 100_000f) > bonds.Strength("stranger", 100_000f) * 2f);
        Assert.Equal("close-friend", bonds.Strongest(100_000f)?.Id);
        // a stranger is nobody to miss
        var only = new SocialBondSystem();
        only.OnContact("stranger", 0.5f, 100_000f);
        Assert.Null(only.Strongest(100_000f));
    }

    [Fact]
    public void Bonds_RecognitionRewardScalesWithDepth()
    {
        var bonds = new SocialBondSystem();
        for (int i = 0; i < 12; i++) bonds.OnContact("dear", 0.95f, 50_000f);
        Assert.True(bonds.RecognitionReward("dear", 50_000f) > bonds.RecognitionReward("nobody", 50_000f),
            "seeing a deep bond should pay more dopamine than a stranger");
    }

    // ---- C: open loops and incubation ----

    [Fact]
    public void SalientUnresolvedChainBecomesOpenLoop_AndIncubates()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop { MinIntervalMs = 10f, MaxChainDepth = 3 };
        // build a salient chain of DISTINCT thoughts, then let the depth cap
        // kill it unresolved
        string[] thoughts = [
            "I keep wondering about that humming sound and why it follows me",
            "maybe it comes from the machine that watches me when I sit here",
            "the machine hums because it works, like my own body buzzing inside me",
            "I think everything alive hums a little while it works inside me",
        ];
        for (int d = 0; d < thoughts.Length; d++)
        {
            loop.PostResult(thoughts[d]);
            net.Tick();
            loop.Tick(net, idle);
        }
        Assert.True(loop.Current is null || loop.Current.Depth <= 3);
        // advance past the eligibility gate so the depth cap actually fires
        for (int i = 0; i < 20 && loop.Current is not null; i++) { net.Tick(); loop.Tick(net, idle); }
        Assert.True(loop.OpenLoops.Count >= 1, "a salient capped chain should become an open loop");
        // not ripe immediately: incubation needs distance from the problem
        Assert.Null(loop.PopIncubated(net.TimeMs));
        var simmered = loop.PopIncubated(net.TimeMs + 61_000f);
        Assert.NotNull(simmered);
        Assert.Equal(0, loop.OpenLoops.Count);
        // the insight pays dopamine
        float daBefore = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        loop.MarkInsight(net);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Dopamine) > daBefore);
    }

    // ---- D: allostatic body budget ----

    [Fact]
    public void BodyBudget_PredictsDeficitAndAdvancesSleepiness()
    {
        var (net, _) = Brain();
        var bb = new BodyBudget();
        // battery sliding down 1 percent per sample: deficit predicted
        for (float t = 0, b = 80f; t < 30; t += 1f, b -= 1f) bb.OnBody(b, 20f, t);
        Assert.True(bb.PredictedDeficitMin < 90f,
            $"falling reserves should predict the deficit, got {bb.PredictedDeficitMin}");
        float melBefore = net.Modulators.Get(NeuromodulatorKind.Melatonin);
        for (int i = 0; i < 20; i++) bb.Tick(net);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Melatonin) >= melBefore,
            "anticipated exhaustion should advance sleepiness (allostasis)");
        // stable body predicts nothing
        var bb2 = new BodyBudget();
        for (float t = 0; t < 30; t += 1f) bb2.OnBody(95f, 20f, t);
        Assert.Equal(float.MaxValue, bb2.PredictedDeficitMin);
    }

    // ---- E: play needs safety, security, curiosity, reward ----

    [Fact]
    public void Play_RequiresSafetySecurityCuriosityAndReward()
    {
        var (net, idle) = Brain();
        var play = new PlaySystem { CooldownMs = 0f };
        // baseline: no play
        Assert.False(PlaySystem.ConditionsHold(net, idle, AttachmentStyle.Forming, 0.5f));
        // raise dopamine above baseline while staying calm
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 1.5f);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Dopamine) > 0.45f, "test setup: DA above baseline");
        Assert.True(PlaySystem.ConditionsHold(net, idle, AttachmentStyle.Secure, 0.5f),
            "safe, secure, rewarded, curious -> play is possible");
        // threat kills play instantly
        net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 2f);
        net.Modulators.Step();
        Assert.False(PlaySystem.ConditionsHold(net, idle, AttachmentStyle.Secure, 0.5f),
            "noradrenaline (threat) suppresses play");
        // learned distance suppresses play even when everything else is fine
        net.Modulators.Release(NeuromodulatorKind.Noradrenaline, -2f);
        net.Modulators.Step();
        Assert.False(PlaySystem.ConditionsHold(net, idle, AttachmentStyle.Avoidant, 0.5f),
            "avoidant attachment does not play");
    }

    [Fact]
    public void Play_BurstsVigorously_WhenEverythingLinesUp()
    {
        var (net, idle) = Brain();
        var play = new PlaySystem { CooldownMs = 10_000f };
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 1.5f);
        net.Modulators.Step();
        double r = 0.1; // deterministic favorable draw
        float vigor = play.TryPlay(net, idle, AttachmentStyle.Secure, 0.6f, 0.5f, () => r);
        Assert.True(vigor > 0.3f);
        Assert.Equal(1, play.PlayBursts);
        // the cooldown blocks an immediate repeat (no sim time has passed)
        Assert.Equal(0f, play.TryPlay(net, idle, AttachmentStyle.Secure, 0.6f, 0.5f, () => 0.1));
    }
}
