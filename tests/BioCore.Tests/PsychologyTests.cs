using BioComp.Core.Cognition;
using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using Xunit;

namespace BioCore.Tests;

public class PsychologyTests
{
    private static (SpikingNetwork Net, IdleController Idle) Brain(int seed = 5)
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: (ulong)seed));
        return (net, new IdleController(net));
    }

    // ---- curiosity: intrinsic reward from learning progress ----

    [Fact]
    public void Curiosity_RewardsLearningProgress_AtTheEdgeOfLearnability()
    {
        var c = new CuriosityModule();
        // accuracy improving while novelty is mid (the Goldilocks zone)
        for (float t = 0; t < 60; t += 0.5f) c.OnPredictionSample(0.3f + t / 100f, 0.5f, t);
        Assert.True(c.LearningProgress > 0.05f, $"improving predictions should show progress, got {c.LearningProgress}");
        Assert.True(c.IntrinsicReward > 0.01f, "progress at mid novelty should pay intrinsic reward");
        // the gate: nothing to learn (novelty 0) or nothing graspable (novelty 1)
        var c2 = new CuriosityModule();
        for (float t = 0; t < 60; t += 0.5f) c2.OnPredictionSample(0.3f + t / 100f, 0f, t);
        Assert.Equal(0f, c2.IntrinsicReward);
        var c3 = new CuriosityModule();
        for (float t = 0; t < 60; t += 0.5f) c3.OnPredictionSample(0.3f + t / 100f, 1f, t);
        Assert.Equal(0f, c3.IntrinsicReward);
        // getting WORSE at predicting never pays
        var c4 = new CuriosityModule();
        for (float t = 0; t < 60; t += 0.5f) c4.OnPredictionSample(0.8f - t / 200f, 0.5f, t);
        Assert.True(c4.LearningProgress < 0f);
        Assert.Equal(0f, c4.IntrinsicReward);
    }

    [Fact]
    public void Curiosity_ReleasesDopamine()
    {
        var (net, _) = Brain();
        var c = new CuriosityModule();
        for (float t = 0; t < 60; t += 0.5f) c.OnPredictionSample(0.3f + t / 100f, 0.5f, t);
        float daBefore = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        c.Tick(net);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Dopamine) > daBefore,
            "intrinsic reward should raise dopamine");
    }

    // ---- emotional contagion: prosody to mood to drift ----

    [Fact]
    public void Contagion_ProsodyMapsToMood()
    {
        var e = new EmotionalContagion();
        // loud, high, flat: aggressive -> negative valence, high arousal
        // (three clips: the estimate integrates evidence, as real perception does)
        for (int i = 0; i < 3; i++)
            e.OnHeardVoice(rms: 0.20f, pitchZcr: 0.16f, baselineZcr: 0.10f, zcrVariability: 0.005f,
                rmsVariability: 0.01f, durationSec: 2f, timeSec: 1f + i * 2f);
        Assert.True(e.UserValence < -0.2f, $"tense prosody should read negative, got {e.UserValence}");
        Assert.True(e.UserArousal > 0.5f);
        Assert.Equal("agitated", e.UserMoodLabel);

        // high and lively: excited -> positive
        var e2 = new EmotionalContagion();
        e2.OnHeardVoice(rms: 0.08f, pitchZcr: 0.15f, baselineZcr: 0.10f, zcrVariability: 0.03f,
            rmsVariability: 0.02f, durationSec: 2f, timeSec: 1f);
        Assert.True(e2.UserValence > 0.2f, $"lively prosody should read positive, got {e2.UserValence}");

        // low and slow: down -> negative, low arousal
        var e3 = new EmotionalContagion();
        e3.OnHeardVoice(rms: 0.03f, pitchZcr: 0.06f, baselineZcr: 0.10f, zcrVariability: 0.01f,
            rmsVariability: 0.005f, durationSec: 2f, timeSec: 1f);
        Assert.True(e3.UserValence < 0f);
        Assert.True(e3.UserArousal < 0.45f);
    }

    [Fact]
    public void Contagion_BrainDriftsTowardSpeakerMood()
    {
        var (net, _) = Brain();
        var e = new EmotionalContagion();
        e.OnHeardVoice(rms: 0.08f, pitchZcr: 0.15f, baselineZcr: 0.10f, zcrVariability: 0.03f,
            rmsVariability: 0.02f, durationSec: 2f, timeSec: 1f);
        float oxtBefore = net.Modulators.Get(NeuromodulatorKind.Oxytocin);
        e.Tick(net, timeSec: 5f);
        net.Modulators.Step();
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Oxytocin) >= oxtBefore,
            "a warm confident voice should nudge oxytocin up");
        // long silence decays confidence
        e.Tick(net, timeSec: 200f);
        Assert.True(e.Confidence < 0.2f);
    }

    // ---- attachment: styles from lived history ----

    [Fact]
    public void Attachment_WarmResponsiveCareGrowsSecure()
    {
        var (net, _) = Brain();
        var a = new AttachmentSystem { MinEvidence = 4 };
        for (int i = 0; i < 6; i++) { a.OnSocialContact(0.8f); a.Tick(net); }
        Assert.Equal(AttachmentStyle.Secure, a.Style);
        Assert.Equal(1f, a.CallOutMultiplier);
    }

    [Fact]
    public void Attachment_WarmButAbandonedCareGrowsAnxious()
    {
        var (net, _) = Brain();
        var a = new AttachmentSystem { MinEvidence = 4 };
        // warm contacts, but many unanswered bids and separations between them
        for (int i = 0; i < 4; i++)
        {
            a.OnSocialContact(0.85f);
            for (int b = 0; b < 8; b++) a.OnBidUnanswered();
            a.OnSeparation();
            a.Tick(net);
        }
        Assert.Equal(AttachmentStyle.Anxious, a.Style);
        Assert.True(a.CallOutMultiplier > 1.5f, "anxious attachment should call out far more");
    }

    [Fact]
    public void Attachment_ColdCareGrowsAvoidant()
    {
        var (net, _) = Brain();
        var a = new AttachmentSystem { MinEvidence = 4 };
        for (int i = 0; i < 6; i++) { a.OnSocialContact(0.05f); a.Tick(net); }
        Assert.Equal(AttachmentStyle.Avoidant, a.Style);
        Assert.True(a.CallOutMultiplier < 0.6f);
    }

    [Fact]
    public void Attachment_StyleChangeIsVisible()
    {
        var (net, _) = Brain();
        var a = new AttachmentSystem { MinEvidence = 3 };
        AttachmentStyle? seen = null;
        a.StyleChanged += s => seen = s;
        for (int i = 0; i < 4; i++) { a.OnSocialContact(0.05f); a.Tick(net); }
        Assert.Equal(AttachmentStyle.Avoidant, seen);
    }

    // ---- Ebbinghaus: forgetting and reactivation strengthening ----

    [Fact]
    public void RetrievalStrength_DecaysWithAgeAndGrowsWithAccess()
    {
        // encoded 9 minutes ago vs 1 minute ago, both measured now
        var older = new Episode(1, 60_000f, [1, 2], ["a"], 0.5f, 0f, 0f);
        var newer = new Episode(2, 540_000f, [1, 2], ["a"], 0.5f, 0f, 0f);
        var revisited = new Episode(3, 60_000f, [1, 2], ["a"], 0.5f, 0f, 0f, AccessCount: 5);
        float now = 600_000f;
        Assert.True(older.RetrievalStrength(now) < newer.RetrievalStrength(now),
            "unvisited memories should fade with age");
        Assert.True(revisited.RetrievalStrength(now) > newer.RetrievalStrength(now),
            "each reactivation should resist forgetting");
    }

    [Fact]
    public void RecallTouchesMemories_SpacingEffect()
    {
        var (net, _) = Brain();
        for (int i = 0; i < 50; i++) net.Tick();
        net.EpisodeStore.Encode(net, "CA3", ["voice"], 0.5f, minMeanRateHz: 0.0001f);
        net.EpisodeStore.Encode(net, "CA3", ["sound"], -0.5f, minMeanRateHz: 0.0001f);
        var recalled = net.EpisodeStore.Recall(["voice"]);
        Assert.NotEmpty(recalled);
        Assert.All(recalled, e => Assert.True(e.AccessCount >= 1));
        // the stored copy was updated, not just the returned one
        Assert.Contains(net.EpisodeStore.Episodes, e => e.Tags.Contains("voice") && e.AccessCount >= 1);
    }

    [Fact]
    public void ScoreEpisode_RetrievalStrengthBoostsFadedButRevisited()
    {
        float fresh = ThoughtLoop.ScoreEpisode(1, 0f, 0f, 0f, 1f, 0f, retrievalStrength: 1f);
        float revisited = ThoughtLoop.ScoreEpisode(1, 0f, 0f, 0f, 1f, 0f, retrievalStrength: 2f);
        Assert.True(revisited > fresh, "a reactivated memory should surface more easily");
    }
}
