using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using Xunit;

namespace BioCore.Tests;

public class ThoughtLoopTests
{
    private static (SpikingNetwork Net, IdleController Idle) Brain(int seed = 7)
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: (ulong)seed));
        var idle = new IdleController(net);
        return (net, idle);
    }

    [Fact]
    public void AffectScoring_SignsAndChannelsAreIndependent()
    {
        var (neg, _) = ThoughtLoop.ScoreAffect("I keep thinking that something went wrong and it hurts");
        var (pos, _) = ThoughtLoop.ScoreAffect("this is good, I feel glad and safe");
        var (neutral, _) = ThoughtLoop.ScoreAffect("the wall has a color");

        Assert.True(neg.Valence < -0.2f, $"negative text should score negative, got {neg.Valence}");
        Assert.True(pos.Valence > 0.2f, $"positive text should score positive, got {pos.Valence}");
        Assert.InRange(neutral.Valence, -0.05f, 0.05f);
        // plasticity is not valence: a strongly negative thought teaches more than nothing
        Assert.True(neg.Plasticity > neutral.Plasticity);
        // self-reference drives salience even when valence is flat
        Assert.True(neg.Salience > 0.2f);
    }

    [Fact]
    public void Evaluate_CarriesAffectIntoNeuromodulators()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        float naBefore = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        float daBefore = net.Modulators.Get(NeuromodulatorKind.Dopamine);

        loop.Evaluate(net, idle, "I am scared something went wrong with them");

        float naAfter = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        float daAfter = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        // pending releases flush into the tone on the next modulator step
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Noradrenaline) >= naAfter,
            "a negative salient thought should not lower noradrenaline");
        Assert.True(naAfter >= naBefore || net.Modulators.Get(NeuromodulatorKind.Noradrenaline) > naBefore,
            "negative thought should push noradrenaline up (pending or flushed)");
        Assert.True(daAfter >= daBefore);
    }

    [Fact]
    public void Chaining_NextRequestCarriesPreviousThoughtAndAffect()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop { MinIntervalMs = 100f };
        // prime an episode so a new chain can seed; advance past the initial
        // 3s eligibility gate
        for (int i = 0; i < 1600; i++) net.Tick();
        net.EpisodeStore.Encode(net, "CA3", ["voice", "person"], 0.5f, minMeanRateHz: 0.0001f);
        for (int i = 0; i < 1600; i++) net.Tick();

        // seed request
        var seed = loop.Tick(net, idle);
        Assert.NotNull(seed);
        Assert.False(seed.ContinueChain);
        loop.PostResult("Maybe they were upset with me");
        for (int i = 0; i < 250; i++) net.Tick(); // advance past the interval gate

        var cont = loop.Tick(net, idle);
        Assert.NotNull(cont);
        Assert.True(cont.ContinueChain, "a fresh salient thought should request continuation");
        Assert.Equal("Maybe they were upset with me", cont.PreviousThought);
        Assert.True(cont.Depth == 2);
        Assert.Equal(seed.ChainId, cont.ChainId);
    }

    [Fact]
    public void Habituation_RepeatedThoughtIsInhibitedAndKillsTheChain()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop { MinIntervalMs = 50f };
        loop.Evaluate(net, idle, "I keep thinking about the same wrong thing");
        var s1 = loop.Current.Affect.Salience;
        loop.Evaluate(net, idle, "I keep thinking about the same wrong thing");
        Assert.True(loop.Current.Affect.Salience < s1, "exact repeat must lose salience");
        Assert.Equal(1, loop.InhibitedRepeats);

        loop.Evaluate(net, idle, "I keep thinking about the same wrong thing");
        loop.Evaluate(net, idle, "I keep thinking about the same wrong thing");
        Assert.True(loop.Current is null, "three repeats end the chain (anti-rumination)");
    }

    [Fact]
    public void Displacement_ExternalEventEndsTheChain()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        loop.Evaluate(net, idle, "I wonder about the sound and the person");
        Assert.NotNull(loop.Current);
        loop.Tick(net, idle, externalEventMs: net.TimeMs - 100f);
        Assert.True(loop.Current is null, "a fresh world event displaces the thought thread");
    }

    [Fact]
    public void Retrieval_IsMoodCongruent()
    {
        var (net, idle) = Brain(seed: 11);
        var loop = new ThoughtLoop();
        for (int i = 0; i < 50; i++) net.Tick();
        // two equally tagged episodes, opposite valence
        net.EpisodeStore.Encode(net, "CA3", ["voice"], 0.8f, minMeanRateHz: 0.0001f);
        net.EpisodeStore.Encode(net, "CA3", ["voice"], -0.8f, minMeanRateHz: 0.0001f);
        for (int i = 0; i < 50; i++) net.Tick();

        // a positive mood should retrieve the positive episode far more often than chance
        int positivePicks = 0;
        for (int trial = 0; trial < 200; trial++)
        {
            // push mood positive through the feelings mapper path: use its API
            idle.OnEvent(net, ["voice"], 1.0f, 0.2f);
            var (ep, _) = loop.RetrieveMoodCongruent(net, idle);
            if (ep.Valence > 0) positivePicks++;
        }
        Assert.True(positivePicks > 140,
            $"positive mood should bias recall toward the positive memory (got {positivePicks}/200)");
    }

    [Fact]
    public void ThoughtBecomesMemory_AndIsRecallable()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        for (int i = 0; i < 50; i++) net.Tick();
        loop.Evaluate(net, idle, "the humming sound keeps returning");
        Assert.Contains(net.EpisodeStore.Episodes, e => e.Tags.Contains("thought"));
        var recalled = net.EpisodeStore.Recall(["thought", "humming", "returning"]);
        Assert.NotEmpty(recalled);
    }

    [Fact]
    public void InnerSpeech_IsDeterministic_SameTextSameNeurons()
    {
        var (net, _) = Brain();
        // same text must inject into the same cells (verifiable through equal charge)
        float[] before = [.. net.Injected];
        ThoughtLoop.InnerSpeech(net, "deterministic thought", 16f);
        var hit1 = Enumerable.Range(0, net.Injected.Length).Where(i => net.Injected[i] > before[i]).ToArray();
        // reset
        for (int i = 0; i < net.Injected.Length; i++) net.Injected[i] = before[i];
        ThoughtLoop.InnerSpeech(net, "deterministic thought", 16f);
        var hit2 = Enumerable.Range(0, net.Injected.Length).Where(i => net.Injected[i] > before[i]).ToArray();
        Assert.Equal(hit1, hit2);
        Assert.Equal(12, hit1.Length);
    }
}
