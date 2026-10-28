using BioComp.Core.Cognition;
using BioComp.Core.Memory;
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

    [Fact]
    public void CodeSimilarity_IsJaccard()
    {
        Assert.Equal(1f, EpisodeMemory.CodeSimilarity([1, 2, 3], [1, 2, 3]));
        Assert.Equal(0f, EpisodeMemory.CodeSimilarity([1, 2, 3], [4, 5, 6]));
        // {1,2} vs {2,3}: intersection 1, union 3
        Assert.Equal(1f / 3f, EpisodeMemory.CodeSimilarity([1, 2], [2, 3]), 3);
        Assert.Equal(0f, EpisodeMemory.CodeSimilarity([], [1, 2]));
    }

    [Fact]
    public void CurrentCode_ReturnsRegionNeurons_NoEncodeSideEffects()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        for (int i = 0; i < 200; i++) net.Tick();
        var lang = net.Regions.First(r => r.Name == "Lang");
        var code = net.EpisodeStore.CurrentCode(net, "Lang", 16);
        Assert.Equal(16, code.Length);
        Assert.All(code, n => Assert.InRange(n, lang.Start, lang.Start + lang.Count - 1));
        // read-only: encoding still works afterwards
        var before = net.EpisodeStore.Episodes.Count;
        loop.Evaluate(net, idle, "a side effect check thought");
        Assert.True(net.EpisodeStore.Episodes.Count >= before);
    }

    [Fact]
    public void ScoreEpisode_DevelopmentShiftsWordsToNeurons()
    {
        // young brain (wCode=0): tags dominate, code similarity is ignored
        float byTags = ThoughtLoop.ScoreEpisode(tagOverlap: 3, codeSimilarity: 0f,
            moodValence: 0f, episodeValence: 0f, ageSec: 10f, wCode: 0f);
        float byCode = ThoughtLoop.ScoreEpisode(tagOverlap: 0, codeSimilarity: 0.9f,
            moodValence: 0f, episodeValence: 0f, ageSec: 10f, wCode: 0f);
        Assert.True(byTags > byCode, "a young brain should retrieve by word tags");
        // experienced brain (wCode=1): neural similarity dominates
        float byTags2 = ThoughtLoop.ScoreEpisode(tagOverlap: 3, codeSimilarity: 0f,
            moodValence: 0f, episodeValence: 0f, ageSec: 10f, wCode: 1f);
        float byCode2 = ThoughtLoop.ScoreEpisode(tagOverlap: 0, codeSimilarity: 0.9f,
            moodValence: 0f, episodeValence: 0f, ageSec: 10f, wCode: 1f);
        Assert.True(byCode2 > byTags2, "an experienced brain should retrieve by neural code");
        // mood congruence always contributes
        float congruent = ThoughtLoop.ScoreEpisode(0, 0f, -0.5f, -0.5f, 10f, 0.5f);
        float opposite = ThoughtLoop.ScoreEpisode(0, 0f, -0.5f, 0.5f, 10f, 0.5f);
        Assert.True(congruent > opposite, "mood-congruent memories score higher");
    }

    [Fact]
    public void ResonanceFromRate_ClampsAndScales()
    {
        Assert.Equal(0f, ThoughtLoop.ResonanceFromRate(0f));
        Assert.Equal(1f, ThoughtLoop.ResonanceFromRate(5f));   // saturated
        Assert.InRange(ThoughtLoop.ResonanceFromRate(0.4f), 0.49f, 0.51f); // half
    }

    [Fact]
    public void SomaticFeedback_MeasuredAfterDelay_AndAdjustsSalience()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        float? seen = null;
        ThoughtLink? linkSeen = null;
        loop.SomaticFeedback += (l, r) => { seen = r; linkSeen = l; };

        loop.Evaluate(net, idle, "the pattern keeps glowing and returning");
        float salBefore = loop.Current.Affect.Salience;
        for (int i = 0; i < 400; i++) net.Tick(); // advance past the 200 ms delay

        loop.Tick(net, idle);
        Assert.NotNull(seen);
        Assert.NotNull(linkSeen);
        Assert.InRange(seen.Value, 0f, 1f);
        // whatever the substrate did, the adjusted salience stays valid
        Assert.InRange(loop.Current.Affect.Salience, 0f, 1f);
        // the measurement consumed itself: a second immediate tick does not re-fire
        float? seen2 = seen;
        loop.Tick(net, idle);
        Assert.Equal(seen2, seen);
    }

    [Fact]
    public void Valence_TintedByCurrentRewardState()
    {
        var (net, idle) = Brain();
        var loop = new ThoughtLoop();
        // drive dopamine far above baseline, then a neutral word thought should
        // appraise slightly positive through the somatic marker
        for (int i = 0; i < 100; i++) net.Tick();
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 3f);
        net.Modulators.Step();
        var link = loop.Evaluate(net, idle, "the wall has a color");
        Assert.True(link.Affect.Valence > 0f,
            $"high DA should tint neutral content positive, got {link.Affect.Valence}");
    }
}
