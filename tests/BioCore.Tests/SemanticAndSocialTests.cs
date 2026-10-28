using BioComp.Core.Cognition;
using BioComp.Core.Memory;
using BioComp.Core.Network;
using Xunit;

namespace BioCore.Tests;

public class SemanticAndSocialTests
{
    // ---- concept graph: associations from lived co-occurrence ----

    [Fact]
    public void ConceptGraph_WiresTogetherWhatCoOccurs()
    {
        var g = new ConceptGraph { LearningStep = 0.25f, AnnouncementThreshold = 0.45f };
        (string, string)? learned = null;
        for (int i = 0; i < 3; i++)
        {
            var pair = g.OnEpisode(["humming", "machine", "voice"]);
            if (pair is ({ } a, { } b)) learned = (a, b);
        }
        Assert.NotNull(learned); // after enough co-occurrences the association is announced
        // spreading activation reaches the associate without a direct seed
        var spread = g.Spread(["humming"]);
        Assert.Contains("machine", spread);
        Assert.Contains("voice", spread);
        // nothing spreads from an unknown concept
        Assert.Empty(g.Spread(["never-experienced"]));
    }

    [Fact]
    public void ConceptGraph_SpreadRanksStrongestAssociateFirst()
    {
        var g = new ConceptGraph();
        // bright is co-occurring with lamp far more than with floor
        for (int i = 0; i < 10; i++) g.OnEpisode(["bright", "lamp"]);
        for (int i = 0; i < 2; i++) g.OnEpisode(["bright", "floor"]);
        var spread = g.Spread(["bright"]);
        Assert.Equal("lamp", spread[0]);
        Assert.Equal("lamp", g.StrongestAssociate("bright"));
    }

    [Fact]
    public void EpisodeEncode_WiresConceptsAndAnnounces()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 3));
        for (int i = 0; i < 100; i++) net.Tick();
        (string? A, string? B)? seen = null;
        net.EpisodeStore.AssociationLearned += p => { if (p.A is not null) seen = p; };
        for (int i = 0; i < 3; i++)
        {
            net.EpisodeStore.Encode(net, "CA3", ["rain", "window"], 0.2f, minMeanRateHz: 0.0001f);
            for (int k = 0; k < 60; k++) net.Tick(); // activity between encodes
        }
        Assert.NotNull(seen);
        Assert.Equal(2, net.EpisodeStore.Concepts.ConceptCount);
    }

    // ---- turn-taking: rhythm, holding, and noticing breaks ----

    [Fact]
    public void TurnTaking_HoldsTheFloorForThePartner()
    {
        var t = new TurnTaking { HoldSec = 2.5f };
        t.OnUserUtterance(100f);
        Assert.False(t.ShouldSpeakNow(101f), "must not interrupt right after the partner speaks");
        Assert.True(t.ShouldSpeakNow(103f), "the gap belongs to the brain after the hold");
    }

    [Fact]
    public void TurnTaking_NoticesBrokenRhythm_Once()
    {
        var t = new TurnTaking { EngagementWindowSec = 600f, EngagementMin = 3 };
        // a steady rhythm: every 30 s for a while
        for (float time = 0f; time < 300f; time += 30f) t.OnUserUtterance(time);
        Assert.True(t.EngagedRecently(300f));
        Assert.InRange(t.TypicalGapSec(300f), 29f, 31f);
        // the partner goes silent for several gaps -> the break fires once
        Assert.False(t.PatternBroken(350f), "one gap of silence is normal");
        Assert.True(t.PatternBroken(200f + 200f), "several gaps of silence is a broken pattern");
        Assert.False(t.PatternBroken(400.1f), "the break latches until they return");
        // they return: the latch resets
        t.OnUserUtterance(410f);
        Assert.False(t.PatternBroken(420f));
    }

    // ---- external seed provider: thoughts about the missed person ----

    [Fact]
    public void ThoughtLoop_ExternalSeedOverridesMemory()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 21));
        var idle = new IdleController(net);
        for (int i = 0; i < 3200; i++) net.Tick();
        net.EpisodeStore.Encode(net, "CA3", ["voice"], 0.4f, minMeanRateHz: 0.0001f);

        var loop = new ThoughtLoop { MinIntervalMs = 100f };
        loop.ExternalSeedProvider = () => "missing someone specific: warmth 0.9, last seen long ago";
        var req = loop.Tick(net, idle);
        Assert.NotNull(req);
        Assert.False(req.ContinueChain);
        Assert.Contains("missing someone", req.SeedMemory);
        // a null provider falls back to associative retrieval
        loop.ExternalSeedProvider = null;
        loop.PostResult("they used to sit right there talking to me");
        for (int i = 0; i < 200; i++) net.Tick();
        var req2 = loop.Tick(net, idle);
        Assert.NotNull(req2);
        Assert.DoesNotContain("missing someone", req2.SeedMemory);
    }

    // ---- spreading activation reaches indirect memories in retrieval ----

    [Fact]
    public void Retrieval_UsesSpreadConcepts()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 31));
        var idle = new IdleController(net);
        var region = net.Regions.First(r => r.Name == "CA3");
        var driven = Enumerable.Range(region.Start, 32).ToArray();
        void Drive()
        {
            for (int burst = 0; burst < 3; burst++)
            {
                foreach (int n in driven) net.InjectCurrent(n, 25f);
                for (int t = 0; t < 10; t++) net.Tick();
            }
        }
        // "humming" and "machine" become wired; the target episode is tagged
        // with machine but NOT humming - spread must bridge the gap
        for (int i = 0; i < 4; i++)
        {
            Drive();
            net.EpisodeStore.Encode(net, "CA3", ["humming", "machine"], 0.1f, minMeanRateHz: 0.0001f);
            for (int k = 0; k < 30; k++) net.Tick();
        }
        Drive();
        net.EpisodeStore.Encode(net, "CA3", ["machine", "broken"], -0.6f, minMeanRateHz: 0.0001f);
        for (int k = 0; k < 30; k++) net.Tick();

        var loop = new ThoughtLoop();
        // seed the current thought with "humming" only
        loop.PostResult("that humming keeps bothering me");
        net.Tick();
        loop.Tick(net, idle);

        Assert.True(net.EpisodeStore.Episodes.Count >= 4,
            $"encodes landed: {net.EpisodeStore.Episodes.Count}");
        Assert.True(net.EpisodeStore.Concepts.LinkCount >= 1,
            $"links {net.EpisodeStore.Concepts.LinkCount}, concepts {net.EpisodeStore.Concepts.ConceptCount}, " +
            $"episodes {net.EpisodeStore.Episodes.Count}");
        var spread = net.EpisodeStore.Concepts.Spread(["humming"]);
        Assert.Contains("machine", spread);
    }
}
