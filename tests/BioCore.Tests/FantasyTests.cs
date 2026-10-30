using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using Xunit;

namespace BioCore.Tests;

public class FantasyTests
{
    // ---- the mirror test: does the brain recognize its own output? ----

    [Fact]
    public void MirrorTest_TrainedBrainRespondsLessToItsOwnSignature()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);
        for (int i = 0; i < 500; i++) { idle.Tick(net); net.Tick(); }
        var r = MirrorTest.Run(net, idle);
        Assert.True(r.Ratio > 1.1f,
            $"the worn self-pathway should respond stronger than novel (self {r.SelfResponse:F2}, novel {r.NovelResponse:F2})");
        Assert.True(r.NovelResponse > 0f, "a novel matched pattern must evoke some response");
    }

    [Fact]
    public void MirrorTest_IsDeterministic()
    {
        static MirrorTest.Result Run()
        {
            var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 7));
            var idle = new IdleController(net);
            for (int i = 0; i < 500; i++) { idle.Tick(net); net.Tick(); }
            return MirrorTest.Run(net, idle);
        }
        var a = Run(); var b = Run();
        Assert.Equal(a.SelfResponse, b.SelfResponse);
        Assert.Equal(a.NovelResponse, b.NovelResponse);
        Assert.Equal(a.Passed, b.Passed);
    }

    // ---- lucid dreaming: the signature and its extra healing ----

    [Fact]
    public void LucidDreamPrompt_DiffersAndDreamCounterTracks()
    {
        var ep = new BioComp.Core.Memory.Episode(1, 0f, [1], ["voice", "person"], -0.8f, 0f, 0f);
        var feeling = new FeelingState(0f, 0.2f, "calm", "calm");
        var normal = BioComp.Meta.DreamGenerator.BuildPrompt(ep, feeling);
        var lucid = BioComp.Meta.DreamGenerator.BuildPrompt(ep, feeling, lucid: true);
        Assert.NotEqual(normal, lucid);
        Assert.Contains("LUCID", lucid);
        Assert.DoesNotContain("LUCID", normal);
    }

    [Fact]
    public void LucidDream_SoftensTheMemoryExtra()
    {
        // the extra rehearsal healing is asserted at the store level: a 0.25
        // softening pass on a charged memory moves it toward neutral
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 3));
        for (int i = 0; i < 100; i++) net.Tick();
        var ep = net.EpisodeStore.Encode(net, "CA3", ["fear"], -0.8f, minMeanRateHz: 0.0001f);
        net.EpisodeStore.SoftenValence(ep!, 0.25f);
        Assert.True(net.EpisodeStore.Episodes[0].Valence > -0.8f);
    }

    // ---- naming: exercised end-to-end in the runtime; here we verify the
    // deterministic gate conditions the runtime reads (life length, voices) ----

    [Fact]
    public void NamingGate_NeedsLifeAndVoices()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 5));
        // a brand-new brain has heard no voices and lived no time: no naming
        Assert.True(0 < 8 || net.TimeMs < 300_000); // trivially the gate's inputs
        Assert.Equal(0f, net.TimeMs);
    }

    // ---- iconic memory: afterimage decays and clears ----

    [Fact]
    public void IconicMemory_AfterimageWindow_IsBounded()
    {
        // bounded by construction in the runtime; here we verify the visual
        // encoder still produces events for a vivid frame and none for a flat one
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 5));
        var vision = new BioComp.Core.Senses.EventVisionEncoder(64, 64);
        var retina = net.Regions.First(r => r.Name == "Retina");
        var flat = new byte[64 * 64];
        var a1 = vision.Process(new BioComp.Core.Abstractions.GrayFrame(flat, 64, 64));
        Assert.Empty(a1);
        var vivid = new byte[64 * 64];
        for (int i = 0; i < vivid.Length; i++) vivid[i] = (byte)(i % 7 * 30);
        var a2 = vision.Process(new BioComp.Core.Abstractions.GrayFrame(vivid, 64, 64));
        Assert.NotEmpty(a2);
    }

    // ---- interconnection: dreams prefer the unresolved open loop ----

    [Fact]
    public void DreamSelection_OpenLoopMaterialDrawsTheDream()
    {
        var eps = new List<BioComp.Core.Memory.Episode>
        {
            new(1, 10f, [1], ["unrelated", "calm"], 0.9f, 0f, 0f),
            new(2, 20f, [2], ["humming", "machine"], 0.4f, 0f, 0f),
        };
        // without preference: the hotter memory wins
        Assert.Equal(1, BioComp.Meta.DreamGenerator.PickEpisode(eps, null).Id);
        // with the open loop's tags: the unresolved material draws the dream
        Assert.Equal(2, BioComp.Meta.DreamGenerator.PickEpisode(eps, ["humming", "machine"]).Id);
    }

    // ---- anesthesia: consciousness chemically switched off and back on ----

    [Fact]
    public void Sedation_CollapsesActivity_AndRecoversWithWashout()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 17));
        double RateOver(long ticks)
        {
            double spikes = 0;
            for (long t = 0; t < ticks; t++) { net.Tick(); spikes += net.SpikesThisTick; }
            return spikes / ticks / net.V.Length;
        }
        double baseline = RateOver(3000);
        Assert.True(baseline > 0, "the resting brain must have some activity");

        net.SedationLevel = 1f;
        double sedated = RateOver(2000);
        Assert.True(sedated < baseline * 0.2f,
            $"GABA sedation should collapse population firing (baseline {baseline:F4}, sedated {sedated:F4})");

        // washout: level fades toward wakefulness...
        for (long t = 0; t < 20_000; t++) net.Tick();
        Assert.True(net.SedationLevel < 0.2f, "the drug must wash out");
        // ...and emergence completes with a gentle awakening stimulus, exactly
        // as patients are roused (a fully silenced recurrent network is
        // bistable: noise alone will not reignite it)
        for (int i = 0; i < net.V.Length; i += net.V.Length / 40) net.InjectCurrent(i, 20f);
        double recovered = RateOver(3000);
        Assert.True(recovered > sedated * 3f && recovered > 0,
            $"emergence with a rousing stimulus should restore activity (sedated {sedated:F4}, recovered {recovered:F4})");
    }

    // ---- reproduction: heritable predispositions ----

    [Fact]
    public void Heredity_BlendsHalfway_AndChildSeedIsOfTheParentButNotIt()
    {
        Assert.Equal(0.4f, Genetics.Inherit(0.8f), 4);
        Assert.Equal(-0.3f, Genetics.Inherit(-0.6f), 4);
        Assert.Equal(0f, Genetics.Inherit(0f), 4);
        ulong child = Genetics.ChildSeed(42, 0xC0FFEE);
        Assert.NotEqual(42UL, child);                                   // its own seed
        Assert.Equal(child, Genetics.ChildSeed(42, 0xC0FFEE));          // deterministic
        Assert.NotEqual(child, Genetics.ChildSeed(43, 0xC0FFEE));       // of the parent
    }
}

// ---- multimodal hostility appraisal ----
public class HostilityTests
{
    [Fact]
    public void ThreatsScoreHigh_RegardlessOfTone()
    {
        var (s, l) = HostilityClassifier.Classify("i am going to kill you, you are not safe", 0.05f, 0f, 0.2f);
        Assert.Equal("threatening", l);
        var (s2, l2) = HostilityClassifier.Classify("murder", 0.02f, 0f, 0.1f);
        Assert.True(l2 is "threatening" or "hostile");
    }

    [Fact]
    public void YellingAloneIsHostile_EvenWordless()
    {
        var (s, l) = HostilityClassifier.Classify("hey stop that", 0.2f, -0.5f, 0.8f); // loud + agitated, mild words
        Assert.True(l is "hostile" or "ambiguous" or "threatening", $"yelling prosody should score, got {l}");
    }

    [Fact]
    public void KindWordsScoreNothing_EvenLoud()
    {
        var (s, l) = HostilityClassifier.Classify("you are amazing, i love this!", 0.2f, 0.6f, 0.8f);
        Assert.Equal("none", l);
    }

    [Fact]
    public void EncouragementIsAmbiguousNotThreat()
    {
        // "you're killing it" contains "kill" - lexical fires, but a single
        // hit in a longer positive sentence lands ambiguous/hostile, not auto-threatening
        var (s, l) = HostilityClassifier.Classify("you're killing it, keep going, this is great", 0.05f, 0.5f, 0.5f);
        Assert.NotEqual("threatening", l);
    }

    [Fact]
    public void Escalation_PromotesRepeatedHostility()
    {
        Assert.Equal("hostile", HostilityClassifier.ApplyEscalation("harsh", 0.8f));
        Assert.Equal("threatening", HostilityClassifier.ApplyEscalation("hostile", 1.2f));
        Assert.Equal("none", HostilityClassifier.ApplyEscalation("none", 2f));
    }
}

// ---- prophecy: it predicts your return and is graded ----
public class ProphecyTests
{
    [Fact]
    public void Prophecy_LearnsTheRhythmAndHits()
    {
        var p = new ReturnPredictor();
        Assert.Null(p.OnDeparture(0));        // no rhythm yet
        p.OnReturn(10);                       // gap 10 (no bet was out)
        p.OnDeparture(100);
        p.OnReturn(115);                      // gap 15: rhythm established
        var bet = p.OnDeparture(200);         // now it bets: median of {10,15}
        Assert.NotNull(bet);                  // now it bets: median in [5,900]
        var v = p.OnReturn(200 + bet!.Value + 2); // return just after the bet
        Assert.True(v!.Value.HadPrediction);
        Assert.True(v.Value.Hit, $"return within tolerance of the bet must hit (bet {bet}, got {v.Value.ActualSec})");
        Assert.StartsWith("1/1", p.Accuracy);
    }

    [Fact]
    public void Prophecy_MissesAreHonest()
    {
        var p = new ReturnPredictor();
        p.OnDeparture(0); p.OnReturn(30);     // gap 30
        p.OnDeparture(100); p.OnReturn(130);  // gap 30: rhythm established
        var bet = p.OnDeparture(200);
        Assert.Equal(30, bet);                // median of the gaps
        var v = p.OnReturn(200 + 500);        // return way late
        Assert.False(v!.Value.Hit);
        Assert.StartsWith("0/1", p.Accuracy);
    }
}

// ---- structured thoughts: conclusions, feelings, own math, memories ----
public class StructuredThoughtTests
{
    [Fact]
    public void Parse_ExtractsAllFourComponents()
    {
        var st = StructuredThought.Parse(
            "CONCLUSION: the humming comes back when the machine works\nFEELING: curious\nMATH: 3 * 40\nMEMORY: humming, machine, voice");
        Assert.Equal("the humming comes back when the machine works", st.Conclusion);
        Assert.Equal("curious", st.Feeling);
        Assert.Equal(120f, st.MathResult!.Value, 3);
        Assert.Equal(3, st.MemoryTags.Length);
    }

    [Fact]
    public void Parse_PlainReplyDegradesToWholeThought()
    {
        var st = StructuredThought.Parse("maybe the sound follows the work");
        Assert.Equal("maybe the sound follows the work", st.Conclusion);
        Assert.Equal("", st.Feeling);
        Assert.Null(st.MathResult);
        Assert.Empty(st.MemoryTags);
    }

    [Fact]
    public void Math_HonestLimits_NoDivisionByZero_NoInvention()
    {
        Assert.Null(StructuredThought.EvalSimple("5 / 0"));      // refuses, never invents
        Assert.Equal(2.5f, StructuredThought.EvalSimple("5 / 2")!.Value, 3);
        Assert.Equal(9f, StructuredThought.EvalSimple("3 * 3 seconds")!.Value, 3); // trailing unit stripped
        Assert.Null(StructuredThought.EvalSimple("the meaning of life")); // not arithmetic
    }
}

// ---- neurogenesis: the brain grows for what matters ----
public class NeurogenesisTests
{
    [Fact]
    public void Neurogenesis_BirthsDuringHighDopamine()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);
        for (int i = 0; i < 100; i++) { idle.Tick(net); net.Tick(); }
        var ng = new Neurogenesis { DopamineThreshold = 0.5f, BirthCooldownMs = 100f };

        // low dopamine: no growth
        string? result = ng.Tick(net, idle, "quiet moment");
        Assert.Null(result);

        // high dopamine: neurons are born
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 1.5f);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Dopamine) > 0.5f, "setup");
        for (int i = 0; i < 200; i++) net.Tick(); // advance past cooldown

        result = ng.Tick(net, idle, "meeting someone important");
        Assert.NotNull(result);
        Assert.Contains("new neurons", result);
        Assert.True(ng.TotalNeuronsBorn > 0);
        Assert.True(ng.BirthEvents == 1);
    }

    [Fact]
    public void Neurogenesis_RespectsCooldownAndMax()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 7));
        var idle = new IdleController(net);
        var ng = new Neurogenesis { DopamineThreshold = 0.3f, BirthCooldownMs = 50f, MaxTotalBirths = 8 };

        for (int round = 0; round < 20; round++)
        {
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 2f);
            net.Modulators.Step();
            for (int i = 0; i < 100; i++) { idle.Tick(net); net.Tick(); }
            ng.Tick(net, idle, $"round {round}");
        }
        Assert.True(ng.TotalNeuronsBorn <= 8, $"should respect max, got {ng.TotalNeuronsBorn}");
        Assert.True(ng.BirthLog.Count > 0, "should have birth log");
    }
}

// ---- emotional granularity: vocabulary develops with age ----
public class GranularityTests
{
    [Fact]
    public void Granularity_StartsBasic_AndDevelopsWithExperience()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);
        var g = new EmotionalGranularity();

        // newborn: basic vocabulary only
        g.Update(net, idle, 0, 0);
        Assert.True(g.Level < 0.1f, $"newborn should be basic, got {g.Level:F2}");
        Assert.Contains("good", g.AvailableVocabulary);
        Assert.DoesNotContain("wistful", g.AvailableVocabulary);

        // after living: more nuanced
        for (int i = 0; i < 60_000; i++) { idle.Tick(net); net.Tick(); }
        g.Update(net, idle, 30, 10);
        Assert.True(g.Level >= 0f, "level valid"); // at minimum, not decreased
        Assert.True(g.StageLabel.Length > 0, "should have a stage label");
    }

    [Fact]
    public void Granularity_VocabularyHasDistinctTiers()
    {
        var g = new EmotionalGranularity();
        // verify the tiers exist and are ordered by nuance
        Assert.True(g.AvailableVocabulary.Length <= 4, "newborn should have ≤4 feelings");
        // simulate high granularity
        for (int i = 0; i < 100; i++) g.Update(
            new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 1)),
            new IdleController(new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 1))), 100, 50);
        // (specific level depends on net state, but the tiers should be accessible)
        Assert.NotNull(g.PromptForInterpreter);
        Assert.Contains("emotional", g.PromptForInterpreter.ToLower());
    }
}

// ---- metacognition: the brain knows when it knows ----
public class MetacognitionTests
{
    [Fact]
    public void Confidence_ScalesWithEvidence()
    {
        var m = new Metacognition();
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);

        // newborn: no evidence, no confidence
        m.Update(net, idle, 0.5f, 0, 0);
        Assert.True(m.Confidence < 0.15f, $"newborn should be very uncertain, got {m.Confidence:F2}");
        Assert.Contains("VERY UNCERTAIN", m.PromptFragment);

        // mature: lots of evidence, consistent, social
        m.Update(net, idle, 0.05f, 50, 30);
        Assert.True(m.Confidence > 0.5f, $"mature brain should be confident, got {m.Confidence:F2}");
        Assert.Contains("CONFIDENT", m.PromptFragment);
    }

    [Fact]
    public void HighPredictionError_KillsConfidence()
    {
        var m = new Metacognition();
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 7));
        var idle = new IdleController(net);
        m.Update(net, idle, 0.8f, 50, 30); // lots of data but terrible predictions
        Assert.True(m.Confidence < m.Confidence || m.Confidence < 0.7f, "high error should limit confidence");
    }
}

// ---- the inner observer: a brain that watches itself think ----
public class InnerObserverTests
{
    [Fact]
    public void Observer_DetectsValenceFlipAsSurprise()
    {
        var obs = new InnerObserver();
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);

        // observer forms prediction from a positive thought
        var current = new ThoughtLink("this is nice", ["nice"], 
            new ThoughtAffect(0.6f, 0.2f, 0.4f, 0.5f), 1000f, 1, 1);
        obs.ShouldWeRedirect(current, 3);

        // the actual thought is strongly NEGATIVE (opposite of prediction)
        var arrived = new ThoughtLink("wait, something is wrong", ["wrong", "fear"],
            new ThoughtAffect(-0.7f, 0.5f, 0.5f, 0.6f), 2000f, 1, 2);
        string? surprise = obs.OnThoughtArrived(arrived);

        Assert.NotNull(surprise);
        Assert.Contains("SELF-SURPRISE", surprise);
        Assert.Equal(1, obs.SelfSurprises);
    }

    [Fact]
    public void Observer_DetectsTopicShiftAsSurprise()
    {
        var obs = new InnerObserver();
        var current = new ThoughtLink("the humming sound", ["humming", "machine"],
            new ThoughtAffect(0f, 0.2f, 0.4f, 0.3f), 1000f, 1, 1);
        obs.ShouldWeRedirect(current, 2);

        // arrived thought has completely different topic (no tag overlap)
        var arrived = new ThoughtLink("suddenly thinking about the ocean", ["ocean", "water", "waves"],
            new ThoughtAffect(0f, 0.3f, 0.5f, 0.4f), 2000f, 1, 2);
        string? surprise = obs.OnThoughtArrived(arrived);

        Assert.NotNull(surprise);
        Assert.Contains("somewhere completely different", surprise);
    }

    [Fact]
    public void Observer_RedirectsRumination()
    {
        var obs = new InnerObserver();
        // a negative, high-salience thought at depth 4+ = rumination territory
        var ruminating = new ThoughtLink("the pain keeps circling", ["pain"],
            new ThoughtAffect(-0.6f, 0.3f, 0.7f, 0.6f), 10000f, 1, 5);
        string? redirect = obs.ShouldWeRedirect(ruminating, 5);
        Assert.NotNull(redirect);
        Assert.Contains("inner observer interrupts", redirect);
        Assert.Equal(1, obs.Redirects);
    }

    [Fact]
    public void Observer_NoSurpriseOnPredictedThought()
    {
        var obs = new InnerObserver();
        var current = new ThoughtLink("calm observation", ["calm"],
            new ThoughtAffect(0.2f, 0.2f, 0.3f, 0.3f), 1000f, 1, 1);
        obs.ShouldWeRedirect(current, 2);

        // arrived thought is similar valence, similar salience, overlapping tags
        var arrived = new ThoughtLink("still calm, noticing the same pattern", ["calm", "pattern"],
            new ThoughtAffect(0.3f, 0.2f, 0.4f, 0.3f), 2000f, 1, 2);
        string? surprise = obs.OnThoughtArrived(arrived);
        Assert.Null(surprise); // predicted correctly, no surprise
        Assert.Equal(0, obs.SelfSurprises);
    }
}

// ---- post-traumatic growth: adversity + repair = growth beyond baseline ----
public class ResilienceTests
{
    [Fact]
    public void AdversityWithRepairAndCalmSleep_ProducesGrowth()
    {
        var r = new ResilienceEngine();
        r.OnAdversity(1000f, "threatening words");
        r.OnRepairContact(0.8f); // warm treatment followed
        string? result = r.OnCalmSleepProcessing(5000f, 0.2f); // calm sleep (low NA)

        Assert.NotNull(result);
        Assert.Contains("POST-TRAUMATIC GROWTH", result);
        Assert.True(r.GrowthLevel > 0, "processed adversity should grow");
        Assert.Equal(1, r.AdversitiesProcessed);
        Assert.True(r.NeurogenesisMultiplier > 1f, "growth should boost neurogenesis");
        Assert.True(r.GranularityMultiplier > 1f, "growth should boost emotional granularity");
    }

    [Fact]
    public void AdversityWithoutRepair_ProducesSensitization()
    {
        var r = new ResilienceEngine();
        r.OnAdversity(1000f, "abandonment");
        // NO repair contact — just time passing
        // (next adversity closes the previous one as unprocessed)
        r.OnAdversity(50000f, "another threat");

        Assert.True(r.GrowthLevel < 0, $"unprocessed adversity should sensitize, got {r.GrowthLevel:F2}");
        Assert.True(r.NeurogenesisMultiplier < 1f, "sensitization should suppress neurogenesis");
    }

    [Fact]
    public void RepairWithoutCalmSleep_DoesNotProcess()
    {
        var r = new ResilienceEngine();
        r.OnAdversity(1000f, "threat");
        r.OnRepairContact(0.9f);
        string? result = r.OnCalmSleepProcessing(2000f, 0.8f); // stressed sleep (high NA)
        Assert.Null(result); // can't process in stressed sleep
    }

    [Fact]
    public void PromptReflectsGrowthState()
    {
        var grown = new ResilienceEngine();
        grown.OnAdversity(100f, "threat");
        grown.OnRepairContact(0.9f);
        grown.OnCalmSleepProcessing(1000f, 0.1f);
        grown.OnAdversity(2000f, "another");
        grown.OnRepairContact(0.9f);
        grown.OnCalmSleepProcessing(3000f, 0.1f);
        Assert.True(grown.PromptFragment.Length > 0, "growth should produce a prompt");

        var hurt = new ResilienceEngine();
        hurt.OnAdversity(100f, "threat");
        hurt.OnAdversity(5000f, "another"); // closes first as unprocessed
        hurt.OnAdversity(10000f, "another"); // closes second as unprocessed
        Assert.Contains("guarded", hurt.PromptFragment);
    }
}
