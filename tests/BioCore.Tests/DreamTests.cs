using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Meta;
using Xunit;

namespace BioCore.Tests;

public class DreamTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"biodream-{Guid.NewGuid():N}");

    [Fact]
    public async Task Dream_RendersSalientEpisode_ToPngFile()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 211));
        var idle = new IdleController(net);
        var ca3 = net.Regions.First(r => r.Name == "CA3");
        for (long t = 0; t < 400; t++) net.Tick();
        for (int burst = 0; burst < 6; burst++)
        {
            foreach (int n in Enumerable.Range(ca3.Start, 32)) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        Assert.NotNull(net.EpisodeStore.Encode(net, "CA3", ["visitor", "voice"], 0.9f, minMeanRateHz: 0.005f));
        for (long t = 0; t < 600; t++) idle.Tick(net); // establish a feeling state

        var dreamer = new DreamGenerator(new MockImageGenerator())
        {
            DreamsDir = Path.Combine(_dir, "dreams"),
            MinInterval = TimeSpan.FromSeconds(5),
        };
        var (file, narrative) = await dreamer.MaybeDreamAsync(net, idle);
        Assert.NotNull(file);
        Assert.True(File.Exists(file));
        byte[] png = await File.ReadAllBytesAsync(file);
        Assert.True(png.Length > 8 && png[0] == 0x89 && png[1] == 0x50, "not a PNG");
        Assert.Contains("visitor", dreamer.Log[0].Prompt);   // dream content follows memory
        Assert.Contains("Mood:", dreamer.Log[0].Prompt);      // and current feeling

        // rate limited: immediate second dream is refused
        var (path2, _) = await dreamer.MaybeDreamAsync(net, idle);
        Assert.Null(path2);
    }

    [Fact]
    public void DreamPrompt_IsDeterministic()
    {
        var ep = new BioComp.Core.Memory.Episode(3, 1000, [42, 43, 44], ["person"], 0.8f, 0.3f, 0.5f);
        var feeling = new FeelingState(0.5f, 0.6f, "curious", "narrative");
        Assert.Equal(DreamGenerator.BuildPrompt(ep, feeling), DreamGenerator.BuildPrompt(ep, feeling));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
