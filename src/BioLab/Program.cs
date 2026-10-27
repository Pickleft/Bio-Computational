using System.Diagnostics;
using BioComp.Core.Network;

// BioLab — headless experiment CLI. Commands grow with milestones (docs/D0/RUNBOOK.md).
var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (cmd)
{
    case "resting":
    {
        double seconds = args.Length > 1 ? ParseSeconds(args[1]) : 30;
        var net = new SpikingNetwork(BrainConfig.Default());
        var sw = Stopwatch.StartNew();
        long ticks = (long)(seconds * 1000);
        for (long t = 0; t < ticks; t++)
        {
            net.Tick();
            if (t > 0 && t % 1000 == 0)
                Console.WriteLine($"t={t / 1000.0:F0}s  rate={net.Recorder.MeanRateHz((float)t):F2}Hz  energy={net.Energy.Spent:F0}");
        }
        sw.Stop();
        Console.WriteLine($"ticks={ticks}  sim_ms={ticks}  wall_s={sw.Elapsed.TotalSeconds:F2}  " +
                          $"({ticks / sw.Elapsed.TotalMilliseconds:F1}x realtime)");
        Console.WriteLine($"mean_rate={net.Recorder.MeanRateHz((float)ticks):F3}Hz  spikes={net.Recorder.TotalSpikes}");
        Console.WriteLine($"trajectory_hash=0x{net.ComputeTrajectoryHash():X16}");
        return 0;
    }
    default:
        Console.WriteLine("""
            BioLab commands:
              resting <sec>   run the default brain headless, report rates + trajectory hash
            """);
        return cmd == "help" ? 0 : 1;
}

static double ParseSeconds(string s) =>
    s.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? double.Parse(s[..^1]) : double.Parse(s);
