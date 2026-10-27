using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Sleep;
using BioComp.Core.Synapses;
using Xunit;

namespace BioCore.Tests;

public class IdleTests
{
    /// <summary>Compact brain (about 350 neurons) so full-day tests run in seconds.</summary>
    private static SpikingNetwork TinyNet(ulong seed = 21)
    {
        var cfg = new BrainConfig { Seed = seed };
        cfg.Regions.AddRange([
            new RegionSpec("Retina", 32, 0f, 0f),
            new RegionSpec("CA3", 64, 2.5f, 2.5f),
            new RegionSpec("DMN", 64, 2.5f, 7.5f),
            new RegionSpec("TaskPos", 64, 5f, 5f),
            new RegionSpec("PFC", 64, 5f, 2.5f),
            new RegionSpec("Motor", 32, 7.5f, 2.5f),
        ]);
        foreach (var r in cfg.Regions)
            cfg.Projections.Add(new ProjectionSpec(r.Name, r.Name, 4, 0.06f, 0.8f, 2, 1));
        cfg.Projections.AddRange([
            new("CA3", "CA3", 8, 0.04f, 0.6f, Tm: TmClass.Facilitating),
            new("CA3", "PFC", 5, 0.06f),
            new("PFC", "DMN", 4, 0.05f),
            new("DMN", "CA3", 5, 0.05f),
            new("TaskPos", "PFC", 4, 0.05f),
            new("PFC", "Motor", 5, 0.07f),
        ]);
        return new SpikingNetwork(cfg);
    }

    [Fact]
    public void SleepWalksAFullCompressedDay()
    {
        var net = TinyNet();
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 4 * 60_000; // 4-minute day

        // salient event early so replay has material
        for (long t = 0; t < 300; t++) net.Tick();
        var ca3 = net.Regions.First(r => r.Name == "CA3");
        for (int burst = 0; burst < 6; burst++)
        {
            foreach (int n in Enumerable.Range(ca3.Start, 32)) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        Assert.NotNull(net.EpisodeStore.Encode(net, "CA3", ["visitor"], 0.8f));

        for (long t = 0; t < 6 * 60_000; t++) idle.Tick(net); // 1.5 days: night passes, morning comes

        var states = idle.Sleep.Transitions.Select(tr => tr.State).ToList();
        Assert.Contains(BrainState.NREM, states);
        int nremIdx = states.IndexOf(BrainState.NREM);
        Assert.Contains(BrainState.Wake, states.Skip(nremIdx + 1)); // woke after sleeping
        Assert.True(idle.Replay.ReplayCount > 0, "no replay happened during sleep");
        Assert.NotEmpty(idle.EventLog);
    }

    [Fact]
    public void ForceWake_FromSleep()
    {
        var net = TinyNet(31);
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 30_000; // night arrives within ~19 s of the cycle
        long t = 0;
        for (; t < 30_000 && idle.Sleep.State is not (BrainState.NREM or BrainState.REM); t++)
            idle.Tick(net);
        Assert.True(idle.Sleep.State is BrainState.NREM or BrainState.REM,
            $"never fell asleep (state={idle.Sleep.State}, pressure={idle.Sleep.SleepPressure:F2})");

        idle.Tick(net, externalArousal: 1f);
        Assert.True(idle.Sleep.State is BrainState.Wake or BrainState.Drowsy,
            $"surprise failed to wake (state={idle.Sleep.State})"); // may instantly doze again
    }

    [Fact]
    public void Nrem_ProducesSlowOscillation()
    {
        var net = TinyNet(33);
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 30_000;
        long t = 0;
        for (; t < 30_000 && idle.Sleep.State != BrainState.NREM; t++) idle.Tick(net);
        Assert.Equal(BrainState.NREM, idle.Sleep.State);

        // 10 s of NREM-only windows: down states (silence) alternating with up states (bursts)
        // should show up as high variability of 100 ms population counts.
        var binCounts = new List<int>();
        int binSpikes = 0;
        bool binValid = true;
        long startSpikes = net.Recorder.TotalSpikes;
        for (; binCounts.Count < 80 && t < 60_000; t++)
        {
            if (idle.Sleep.State != BrainState.NREM) binValid = false;
            idle.Tick(net);
            binSpikes = (int)(net.Recorder.TotalSpikes - startSpikes);
            if (t % 100 == 99)
            {
                if (binValid) binCounts.Add(binSpikes);
                binValid = true;
                startSpikes = net.Recorder.TotalSpikes;
                binSpikes = 0;
            }
        }
        Assert.True(binCounts.Count >= 20, $"not enough NREM windows ({binCounts.Count})");
        float mean = (float)binCounts.Average();
        float variance = binCounts.Select(c => (c - mean) * (c - mean)).Average();
        float cv = mean > 0 ? MathF.Sqrt(variance) / mean : 0f;
        Assert.True(cv > 0.8f, $"NREM population rate too uniform (CV={cv:F2}); slow oscillation missing");
    }

    [Fact]
    public void Replay_LogsAndRepeats()
    {
        var net = TinyNet(44);
        var ca3 = net.Regions.First(r => r.Name == "CA3");
        for (long t = 0; t < 400; t++) net.Tick();
        for (int burst = 0; burst < 6; burst++)
        {
            foreach (int n in Enumerable.Range(ca3.Start, 32)) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        Assert.NotNull(net.EpisodeStore.Encode(net, "CA3", ["visitor"], 0.9f));

        var replay = new BioComp.Core.Replay.ReplayScheduler { Cooldown = TimeSpan.FromMilliseconds(200) };
        for (long t = 0; t < 3_000; t++)
        {
            replay.Tick(net, replayAllowed: true);
            net.Tick();
        }

        Assert.True(replay.ReplayCount >= 2, $"expected repeated replays, got {replay.ReplayCount}");
        Assert.All(replay.Log, entry => Assert.Equal("visitor", entry.Tags));
    }

    [Fact]
    public void AntiCorrelation_ComputesOverDay()
    {
        var net = TinyNet(55);
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 15_000;
        for (long t = 0; t < 15_000; t++) idle.Tick(net);
        float r = idle.DmnTaskPosCorrelation.Pearson();
        Assert.True(r is >= -1f and <= 1f, "correlation out of range");
    }
}
