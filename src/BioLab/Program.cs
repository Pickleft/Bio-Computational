using System.Diagnostics;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Sleep;

// BioLab - headless experiment CLI. Commands grow with milestones (docs/plan/RUNBOOK.md).
var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (cmd)
{
    case "resting":
    {
        double seconds = args.Length > 1 ? ParseSeconds(args[1]) : 30;
        RunAndReport(new SpikingNetwork(BrainConfig.Default()), seconds, "resting");
        return 0;
    }
    case "day":
    {
        // A compressed full day: wake, ambient episodes, sleep with replay, wake again.
        double simSeconds = args.Length > 1 ? ParseSeconds(args[1]) : 240;
        var net = new SpikingNetwork(BrainConfig.Default());
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = simSeconds * 1000;
        var sw = Stopwatch.StartNew();
        long ticks = (long)(simSeconds * 1000);
        for (long t = 0; t < ticks; t++)
        {
            idle.Tick(net);
            if (t > 0 && t % (15_000) == 0)
                Console.WriteLine($"t={t / 1000.0:F0}s state={idle.Sleep.State} pressure={idle.Sleep.SleepPressure:F2} " +
                                  $"circadian={idle.Sleep.Circadian:F2} rate={net.Recorder.MeanRateHz((float)t):F2}Hz " +
                                  $"replays={idle.Replay.ReplayCount} episodes={net.EpisodeStore.Episodes.Count}");
        }
        sw.Stop();
        Console.WriteLine($"[day] sim_s={simSeconds} wall_s={sw.Elapsed.TotalSeconds:F1} states=" +
                          string.Join(">", idle.Sleep.Transitions.Select(tr => tr.State)));
        Console.WriteLine($"replays={idle.Replay.ReplayCount} episodes={net.EpisodeStore.Episodes.Count} " +
                          $"dmn_taskpos_r={idle.DmnTaskPosCorrelation.Pearson():F2}");
        Console.WriteLine($"trajectory_hash=0x{net.ComputeTrajectoryHash():X16}");
        return 0;
    }
    case "tune":
    {
        // Criticality sweep: intrinsic noise x global GABA gain, undriven.
        double seconds = args.Length > 1 ? ParseSeconds(args[1]) : 15;
        float[] noises = [1f, 2f, 3f, 5f, 8f, 12f, 16f];
        float[] gabas = [0.6f, 0.8f, 1.0f, 1.2f];
        Console.WriteLine("noise sigma | gaba gain | mean rate Hz | branching sigma | avalanches | max size | verdict");
        foreach (float noise in noises)
        foreach (float gaba in gabas)
        {
            var cfg = BrainConfig.Default();
            cfg.NoiseSigmaMv = noise;
            cfg.GlobalGabaGain = gaba;
            var net = new SpikingNetwork(cfg);
            for (long t = 0; t < (long)(seconds * 1000); t++) net.Tick();
            float rate = net.Recorder.MeanRateHz((float)(seconds * 1000));
            float sigma = net.Avalanches.BranchingRatio();
            var sizes = net.Avalanches.AvalancheSizes();
            int max = sizes.Count > 0 ? sizes.Max() : 0;
            string verdict =
                rate < 0.05f ? "silent" :
                rate > 60f || max > net.V.Length / 2 ? "seizure" :
                sigma is > 0.7f and < 1.15f ? "ALIVE-near-critical" : "alive";
            Console.WriteLine($"{noise,11:F0} | {gaba,9:F1} | {rate,12:F2} | {sigma,15:F2} | {sizes.Count,10} | {max,9} | {verdict}");
        }
        return 0;
    }
    default:
        Console.WriteLine("""
            BioLab commands:
              resting <sec>   run the default brain headless, report rates + trajectory hash
              tune <sec>      sweep noise x gaba gain, report rate/branching/avalanches
              day <sec>       compressed full day: wake, episodes, sleep with replay, wake
            """);
        return cmd == "help" ? 0 : 1;
}

static void RunAndReport(SpikingNetwork net, double seconds, string label)
{
    var sw = Stopwatch.StartNew();
    long ticks = (long)(seconds * 1000);
    for (long t = 0; t < ticks; t++)
    {
        net.Tick();
        if (t > 0 && t % 5000 == 0)
            Console.WriteLine($"t={t / 1000.0:F0}s rate={net.Recorder.MeanRateHz((float)t):F2}Hz " +
                              $"sigma={net.Avalanches.BranchingRatio():F2} energy={net.Energy.Spent:F0} " +
                              $"da={net.Modulators.Get(NeuromodulatorKind.Dopamine):F2}");
    }
    sw.Stop();
    Console.WriteLine($"[{label}] ticks={ticks} wall_s={sw.Elapsed.TotalSeconds:F2} ({ticks / sw.Elapsed.TotalMilliseconds:F1}x realtime)");
    Console.WriteLine($"mean_rate={net.Recorder.MeanRateHz((float)ticks):F3}Hz spikes={net.Recorder.TotalSpikes} " +
                      $"branching={net.Avalanches.BranchingRatio():F2}");
    Console.WriteLine($"trajectory_hash=0x{net.ComputeTrajectoryHash():X16}");
}

static double ParseSeconds(string s) =>
    s.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? double.Parse(s[..^1]) : double.Parse(s);
