using BioComp.Core.Memory;
using BioComp.Core.Network;
using Xunit;

namespace BioCore.Tests;

public class EpisodeMemoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"bioep-{Guid.NewGuid():N}.jsonl");

    private static (SpikingNetwork Net, IdleDsl Idle) AliveNet(ulong seed = 5)
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: seed));
        return (net, new IdleDsl(net));
    }

    private sealed record IdleDsl(SpikingNetwork Net)
    {
        public void Run(long ms) { for (long t = 0; t < ms; t++) Net.Tick(); }
    }

    [Fact]
    public void Encode_CapturesActiveCode_RecallByTag()
    {
        var (net, idle) = AliveNet();
        var region = net.Regions.First(r => r.Name == "CA3");
        idle.Run(500); // let spontaneous activity produce rates

        // strong drive = a salient event
        var driven = Enumerable.Range(region.Start, 64).ToArray();
        for (int burst = 0; burst < 10; burst++)
        {
            foreach (int n in driven) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        var ep = net.EpisodeStore.Encode(net, "CA3", ["person", "entrance"], valence: 0.8f);
        Assert.NotNull(ep);
        Assert.Contains(ep!.CodeNeurons, n => driven.Contains(n));

        var recalled = net.EpisodeStore.Recall(["person"]);
        // recall now STRENGTHENS the trace (spacing effect): the returned copy
        // carries the bumped access count
        Assert.Contains(recalled, r => r.Id == ep!.Id && r.AccessCount >= 1);
    }

    [Fact]
    public void Encode_OnQuietNetwork_ReturnsNull()
    {
        var cfg = BrainConfig.Default(seed: 11);
        cfg.NoiseSigmaMv = 0f; // silence
        var net = new SpikingNetwork(cfg);
        for (long t = 0; t < 300; t++) net.Tick();
        net.EpisodeStore.Encode(net, "CA3", ["x"], 0f); // primes baseline
        for (long t = 0; t < 300; t++) net.Tick();
        Assert.Null(net.EpisodeStore.Encode(net, "CA3", ["x"], 0f));
    }

    [Fact]
    public void Jsonl_RoundTrip()
    {
        var (net, idle) = AliveNet(7);
        var region = net.Regions.First(r => r.Name == "CA3");
        idle.Run(400);
        for (int burst = 0; burst < 8; burst++)
        {
            foreach (int n in Enumerable.Range(region.Start, 48)) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        Assert.NotNull(net.EpisodeStore.Encode(net, "CA3", ["sound"], 0.5f));

        net.EpisodeStore.Save(_path);
        var loaded = EpisodeMemory.Load(_path);
        Assert.Single(loaded.Episodes);
        Assert.Equal(["sound"], loaded.Episodes[0].Tags);
        Assert.Equal(0.5f, loaded.Episodes[0].Valence);
    }

    public void Dispose() { try { File.Delete(_path); } catch { } }
}
