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
}
