using System.Diagnostics;
using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Senses;
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
    case "surprise":
    {
        // Wake-on-surprise demo: the brain lives in a quiet room (mock or real camera),
        // falls asleep on its own, and must wake unprompted when someone enters.
        bool liveCamera = args.Contains("--camera");
        double seconds = args.FirstOrDefault(a => a.EndsWith("s") && a != "resting") is string s && double.TryParse(s.TrimEnd('s'), out var v) ? v : 60;

        var net = new SpikingNetwork(BrainConfig.Default(seed: 61));
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 30_000;
        var encoder = new EventVisionEncoder(64, 64);
        var salience = new SalienceMonitor();
        var retina = net.Regions.First(r => r.Name == "Retina");

        IFrameSource camera;
        if (liveCamera)
        {
            try { camera = new BioComp.Senses.OpenCvCameraSource(); Console.WriteLine("camera: live"); }
            catch (Exception ex) { Console.WriteLine($"camera unavailable ({ex.Message}); using mock"); camera = new MockFrameSource(62, personAtMs: 100_000); }
        }
        else camera = new MockFrameSource(62, personAtMs: 100_000);
        using var cam = camera;

        long personAt = (long)(seconds * 1000 * 0.55); // person enters past the halfway point
        var sw = Stopwatch.StartNew();
        int frameCount = 0;
        for (long t = 0; t < (long)(seconds * 1000); t++)
        {
            if (camera is MockFrameSource mock)
            {
                mock.Advance(1);
                if (t == personAt) mock.TriggerPerson();
            }
            if (camera.TryReadNext(out var frame))
            {
                frameCount++;
                var events = encoder.Process(frame);
                encoder.Inject(net, retina.Start, retina.Count, events);
                salience.OnVisionFrame(net, encoder.ChangeEnergyLastFrame);
                if (salience.SurpriseEvents > 0 && salience.ExternalArousal > 0.5f)
                    idle.OnEvent(net, [salience.SurpriseLog[^1].Tag], 0.2f, salience.ExternalArousal);
            }
            idle.Tick(net, salience.ExternalArousal);
            if (t > 0 && t % 5_000 == 0)
                Console.WriteLine($"t={t / 1000.0:F0}s state={idle.Sleep.State} novelty={salience.VisionModel.Novelty:F2} " +
                                  $"na={net.Modulators.Get(NeuromodulatorKind.Noradrenaline):F2} " +
                                  $"episodes={net.EpisodeStore.Episodes.Count} surprises={salience.SurpriseEvents}");
        }
        sw.Stop();
        Console.WriteLine($"[surprise] sim_s={seconds} wall_s={sw.Elapsed.TotalSeconds:F1} frames={frameCount} " +
                          $"states={string.Join(">", idle.Sleep.Transitions.Select(tr => tr.State))}");
        Console.WriteLine($"surprises={salience.SurpriseEvents} ({string.Join(", ", salience.SurpriseLog.Select(x => x.Tag + "@" + (x.TimeMs / 1000) + "s"))}) " +
                          $"episodes={net.EpisodeStore.Episodes.Count}");
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
              resting <sec>       run the default brain headless, report rates + trajectory hash
              tune <sec>          sweep noise x gaba gain, report rate/branching/avalanches
              day <sec>           compressed full day: wake, episodes, sleep with replay, wake
              surprise <sec>      quiet room, sleep, then wake-on-surprise (add --camera for a live webcam)
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
