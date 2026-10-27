using BioComp.Meta;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using Xunit;

namespace BioCore.Tests;

public class MetaTests : IDisposable
{
    private readonly string _log = Path.Combine(Path.GetTempPath(), $"biocost-{Guid.NewGuid():N}.jsonl");

    [Fact]
    public void CostGovernor_RefusesWhenBudgetExhausted()
    {
        var gov = new CostGovernor { DailyBudgetUsd = 0.05f, LogPath = _log };
        Assert.True(gov.TryBook(MetaTier.Chat));                       // ~0.002
        for (int i = 0; i < 30; i++) gov.TryBook(MetaTier.Chat);       // burn the daily budget
        Assert.False(gov.TryBook(MetaTier.Chat), "budget not enforced");
        Assert.True(gov.RefusedCalls > 0);
    }

    [Fact]
    public void CostGovernor_EnforcesHourlyCap()
    {
        var gov = new CostGovernor { DailyBudgetUsd = 100f, ChatHourlyCapUsd = 0.01f, LogPath = _log };
        int booked = 0;
        while (gov.TryBook(MetaTier.Chat) && booked < 100) booked++;
        Assert.InRange(booked, 1, 6); // 0.01 / 0.002 per call
    }

    [Fact]
    public void CostGovernor_WritesJsonlLog()
    {
        var gov = new CostGovernor { LogPath = _log };
        gov.Record(MetaTier.Image, 0.01f, 512.5, ok: true, note: "unit");
        Assert.True(File.Exists(_log));
        string line = File.ReadAllText(_log);
        Assert.Contains("\"tier\":\"Image\"", line);
        Assert.Contains("\"ok\":true", line);
    }

    [Fact]
    public async Task Mocks_AreDeterministic()
    {
        var seg = new MockSegmenter();
        var a = await seg.SegmentAsync([1, 2, 3, 4], "person");
        var b = await seg.SegmentAsync([1, 2, 3, 4], "person");
        Assert.Equal(a.Boxes[0], b.Boxes[0]);
        var c = await seg.SegmentAsync([9, 9, 9], "person");
        Assert.NotEqual(a.Boxes[0].X1, c.Boxes[0].X1); // different input, different output
        Assert.False(a.FromLiveApi);
    }

    [Fact]
    public void WavWriter_ProducesValidHeader()
    {
        var wav = WavWriter.ToWav(new short[160], 16_000);
        Assert.Equal('R', (char)wav[0]); Assert.Equal('I', (char)wav[1]);
        Assert.Equal('F', (char)wav[2]); Assert.Equal('F', (char)wav[3]);
        Assert.Equal(16, BitConverter.ToInt32(wav, 16));     // fmt size
        Assert.Equal(1, BitConverter.ToInt16(wav, 20));      // PCM
        Assert.Equal(1, BitConverter.ToInt16(wav, 22));      // mono
        Assert.Equal(16_000, BitConverter.ToInt32(wav, 24)); // rate
        Assert.Equal(44 + 320, wav.Length);
    }

    [Fact]
    public async Task Monologue_IsRateLimited_AndGroundedInState()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 111));
        var idle = new IdleController(net);
        for (long t = 0; t < 2_000; t++) idle.Tick(net);
        idle.VisionSalience = 2f;
        for (int t = 0; t < 800; t++) { idle.VisionSalience = 2f; idle.Tick(net); } // ignite

        var lm = new MockLanguageModel();
        var gen = new MonologueGenerator(lm) { MinInterval = TimeSpan.FromMilliseconds(10_000) };
        string? first = await gen.MaybeGenerateAsync(net, idle);
        Assert.NotNull(first);
        string? second = await gen.MaybeGenerateAsync(net, idle);
        Assert.Null(second); // rate limited
        Assert.Single(lm.SeenPrompts);
        Assert.Contains("feel", lm.SeenPrompts[0]);         // grounded in state
        Assert.Contains("happened", lm.SeenPrompts[0]);      // describes the event
    }

    public void Dispose() { try { File.Delete(_log); } catch { } }
}
