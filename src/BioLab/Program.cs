using System.Diagnostics;
using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Senses;
using BioComp.Core.Sleep;
using BioComp.Lab;
using BioComp.Meta;

// BioLab - headless experiment CLI. Commands grow with milestones (docs/plan/RUNBOOK.md).
var cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

switch (cmd)
{
    case "paradigm":
    {
        string name = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        int trials = args.FirstOrDefault(a => a.StartsWith("--trials=")) is { } t && int.TryParse(t["--trials=".Length..], out var n) ? n : 0;
        string dir = args.FirstOrDefault(a => a.StartsWith("--out="))?["--out=".Length..] ?? "exports";
        switch (name)
        {
            case "mmn":
            {
                var (std, dev, mmn) = Paradigms.Mmn(trials > 0 ? trials : 300, outDir: dir);
                Console.WriteLine($"[mmn] band rates: standard={std:F2} Hz deviant={dev:F2} Hz; " +
                                  $"deviance (prediction error): deviants exceed standards by {mmn:F1} sigma");
                Console.WriteLine($"exports: {Path.Combine(dir, "mmn.csv")}, {Path.Combine(dir, "mmn.svg")}");
                return 0;
            }
            case "conditioning":
            {
                int acq = trials > 0 ? trials : 30;
                var (early, late, ext) = Paradigms.Conditioning(acq, Math.Max(10, acq * 2 / 3), outDir: dir);
                Console.WriteLine($"[conditioning] CS early={early:F3} Hz  late acquisition={late:F3} Hz  extinction end={ext:F3} Hz");
                Console.WriteLine($"exports: {Path.Combine(dir, "conditioning.csv")}, {Path.Combine(dir, "conditioning.svg")}");
                return 0;
            }
            case "discounting":
            {
                var rows = Paradigms.Discounting(dir);
                Console.WriteLine("[discounting] serotonin tone -> k -> indifference delay (s)");
                foreach (var (sht, k, d) in rows)
                    Console.WriteLine($"  {sht:F2}  {k:F3}  {d:F1}");
                return 0;
            }
            default:
                Console.WriteLine("paradigms: mmn, conditioning, discounting  (options: --trials=N --out=dir)");
                return 1;
        }
    }
    case "wonder":
    {
        // Autonomous research agent: mock interpreter runs the fixed protocol keyless;
        // with a key and --live it points at Muse Wonder through the cost governor.
        string goal = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? "state of the brain";
        ILanguageModel lm;
        if (args.Contains("--live"))
        {
            var mo = MetaOptions.Load();
            if (!mo.HasKey) { Console.WriteLine("no api key; running the mock protocol"); lm = new MockLanguageModel(); }
            else lm = new MetaClients(mo, new CostGovernor { DailyBudgetUsd = 5f });
        }
        else lm = new MockLanguageModel();

        var agent = new WonderAgent(lm);
        Console.WriteLine($"wonder agent: {goal}");
        var report = await agent.RunAsync(goal);
        foreach (var line in agent.Trace) Console.WriteLine($"  {line}");
        Console.WriteLine($"report: {report.Path}");
        Console.WriteLine(report.Summary);
        return 0;
    }
    case "eeg":
    {
        // Resting-state LFP probe: run quietly, analyze the thalamocortical signal.
        double seconds = args.Length > 1 ? ParseSeconds(args[1]) : 30;
        var net = new SpikingNetwork(BrainConfig.Default());
        var lfp = new BioComp.Core.Metrics.LfpProxy();
        var idle = new BioComp.Core.Cognition.IdleController(net);
        idle.Sleep.DayLengthMs = 3_600_000; // stay awake during the probe
        var thal = net.Regions.First(r => r.Name == "Thal");
        var v1 = net.Regions.First(r => r.Name == "V1");
        var cond = net.Cond;
        for (long t = 0; t < (long)(seconds * 1000); t++)
        {
            net.Tick();
            idle.Sleep.Tick(net, 0);
            float s = 0;
            for (int i = thal.Start; i < thal.Start + thal.Count; i++)
                s += cond.GAmpa[i] + cond.GNmda[i] - cond.GGabaa[i] - cond.GGabab[i];
            for (int i = v1.Start; i < v1.Start + v1.Count; i++)
                s += cond.GAmpa[i] + cond.GNmda[i] - cond.GGabaa[i] - cond.GGabab[i];
            lfp.AddSample(s);
        }
        Console.WriteLine("bands (delta theta alpha beta gamma): " +
                          string.Join(" ", lfp.Bands.Select(b => b.ToString("E2"))));
        var ratios = lfp.History.Select(h => h[2] / MathF.Max(1e-12f, (h[1] + h[3]) / 2f)).ToArray();
        Console.WriteLine($"1/f slope = {lfp.Slope:F2}   alpha ratio (last) = {lfp.AlphaRatio:F2}   " +
                          $"(max {ratios.Max():F2}, median {ratios.OrderBy(r => r).ToArray()[ratios.Length / 2]:F2} across {ratios.Length} epochs)   " +
                          $"sleep state = {idle.Sleep.State}");
        return 0;
    }
    case "resting":
    {
        double seconds = args.Length > 1 ? ParseSeconds(args[1]) : 30;
        RunAndReport(new SpikingNetwork(BrainConfig.Default()), seconds, "resting");
        return 0;
    }
    case "meta-check":
    {
        // Auth + endpoint smoke. Cheap by default (one 10-token chat); --all also tests
        // SAM, voice and one image (~1 cent). Keyless: verifies the mock path instead.
        var opts = MetaOptions.Load();
        bool all = args.Contains("--all");
        if (!opts.HasKey)
        {
            Console.WriteLine("no api key found (.secrets/key.txt or MODEL_API_KEY); verifying mock path only");
            var mock = new MetaClients(opts, new CostGovernor());
            var seg = await new MockSegmenter().SegmentAsync([1, 2, 3], "person");
            var tr = await new MockTranscriber().TranscribeAsync(new short[1600]);
            var img = await new MockImageGenerator().GenerateAsync("test");
            var mono = await new MockLanguageModel().CompleteAsync("s", "state: feeling=calm");
            Console.WriteLine($"mocks ok: sam box=({seg.Boxes[0].X1},{seg.Boxes[0].Y1}) voice=\"{tr.Transcript}\" image={img.Length}B monologue=\"{mono[..Math.Min(60, mono.Length)]}\"");
            return 0;
        }

        var cost = new CostGovernor();
        var clients = new MetaClients(opts, cost);
        Console.WriteLine($"key loaded (prefix {opts.ApiKey![..8]}...), testing chat auth");
        try
        {
            var sw = Stopwatch.StartNew();
            string reply = await clients.CompleteAsync(
                "Reply with exactly the word OK and nothing else.", "ping", 10);
            Console.WriteLine($"chat: OK in {sw.Elapsed.TotalSeconds:F1}s -> \"{reply.Trim()}\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"chat FAILED: {ex.Message}`" + (cost.LastError is { } e ? $" (endpoint: {e})`" : "") + (cost.RefusedCalls > 0 ? " [budget refused]" : ""));
            return 1;
        }

        if (all)
        {
            try
            {
                using var mat = new OpenCvSharp.Mat(64, 64, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(40, 40, 40));
                OpenCvSharp.Cv2.Rectangle(mat, new OpenCvSharp.Rect(20, 12, 22, 40), new OpenCvSharp.Scalar(220, 200, 180), -1);
                OpenCvSharp.Cv2.ImEncode(".png", mat, out byte[] png);
                var seg = await clients.SegmentAsync(png, "person");
                Console.WriteLine($"sam: {seg.Boxes.Length} box(es), raw=\"{seg.RawOutput[..Math.Min(70, seg.RawOutput.Length)]}...\"");
            }
            catch (Exception ex) { Console.WriteLine($"sam FAILED: {ex.Message}"); }

            try
            {
                var pcm = new short[16_000];
                for (int i = 0; i < pcm.Length; i++)
                    pcm[i] = (short)(6000 * Math.Sin(2 * Math.PI * 440 * i / 16_000.0));
                var tr = await clients.TranscribeAsync(pcm);
                Console.WriteLine($"voice: transcript=\"{tr.Transcript}\" turns={tr.Turns.Length}");
            }
            catch (Exception ex) { Console.WriteLine($"voice FAILED: {ex.Message}"); }

            try
            {
                byte[] img = await clients.GenerateAsync("a dim blue-green glow, abstract neural texture, calm");
                Console.WriteLine($"image: {img.Length} bytes back");
            }
            catch (Exception ex) { Console.WriteLine($"image FAILED: {ex.Message}"); }
        }
        Console.WriteLine($"spend so far: ${cost.SpentTodayUsd:F3} (log: {cost.LogPath})");
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
        var cochlea = new Cochlea();
        var audio = new MockAudioSource(63, voiceAtMs: 100_000);
        ILanguageModel lm = new MockLanguageModel();
        ITranscriber transcriber = new MockTranscriber();
        if (args.Contains("--live"))
        {
            var mo = MetaOptions.Load();
            if (mo.HasKey)
            {
                var mc = new MetaClients(mo, new CostGovernor());
                lm = mc;
                transcriber = mc;
                Console.WriteLine("meta: live Spark monologue + Voice Transcribe");
            }
            else Console.WriteLine("meta: no key found, staying on mocks");
        }
        var monologue = new MonologueGenerator(lm) { MinInterval = TimeSpan.FromSeconds(5) }; // demo pacing, not the 2-min default
        var audioBuffer = new List<short>();
        bool wasVoice = false;
        long voiceCooldownUntil = 0;
        int lastIgnition = 0;

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
                if (t == personAt) { mock.TriggerPerson(); audio.TriggerVoice(); } // visitor speaks too
            }
            audio.Advance(1);
            if (audio.TryReadNext(out var block))
            {
                cochlea.Process(block);
                salience.OnAudioBlock(net, cochlea.OnsetThisBlock, cochlea.EnergyRms);
                if (cochlea.VoiceLikely) idle.AudioSalience = MathF.Max(idle.AudioSalience, 1.0f);
                else if (cochlea.OnsetThisBlock) idle.AudioSalience = MathF.Max(idle.AudioSalience, 0.7f);
                audioBuffer.AddRange(block.Samples);
                if (cochlea.VoiceLikely && !wasVoice && t > voiceCooldownUntil)
                {
                    wasVoice = true;
                    voiceCooldownUntil = t + 10_000;
                    if (audioBuffer.Count > 16_000 * 4)
                        audioBuffer.RemoveRange(0, audioBuffer.Count - 16_000 * 4); // last ~4 s
                    try
                    {
                        var tr = await transcriber.TranscribeAsync(audioBuffer.ToArray());
                        if (!string.IsNullOrWhiteSpace(tr.Transcript))
                        {
                            idle.OnEvent(net, ["voice", tr.Transcript[..Math.Min(40, tr.Transcript.Length)]], 0.3f, 0.8f);
                            Console.WriteLine($"t={t / 1000.0:F1}s heard: \"{tr.Transcript}\" {(tr.FromLiveApi ? "(live)" : "(mock)")}");
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"transcribe failed: {ex.Message}"); }
                    audioBuffer.Clear();
                }
                if (!cochlea.VoiceLikely) wasVoice = false;
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
            if (idle.Workspace.IgnitionCount > lastIgnition)
            {
                lastIgnition = idle.Workspace.IgnitionCount;
                try
                {
                    string? text = await monologue.MaybeGenerateAsync(net, idle);
                    if (text is not null)
                        Console.WriteLine($"t={t / 1000.0:F1}s monologue ({idle.Workspace.LastIgnition.Tag}): \"{text}\"");
                }
                catch (Exception ex) { Console.WriteLine($"monologue failed: {ex.Message}"); }
            }
            if (t > 0 && t % 5_000 == 0)
                Console.WriteLine($"t={t / 1000.0:F0}s state={idle.Sleep.State} novelty={salience.VisionModel.Novelty:F2} " +
                                  $"na={net.Modulators.Get(NeuromodulatorKind.Noradrenaline):F2} " +
                                  $"episodes={net.EpisodeStore.Episodes.Count} surprises={salience.SurpriseEvents}");
        }
        sw.Stop();
        Console.WriteLine($"[surprise] sim_s={seconds} wall_s={sw.Elapsed.TotalSeconds:F1} frames={frameCount} " +
                          $"states={string.Join(">", idle.Sleep.Transitions.Select(tr => tr.State))}");
        Console.WriteLine($"surprises={salience.SurpriseEvents} ({string.Join(", ", salience.SurpriseLog.Select(x => x.Tag + "@" + (x.TimeMs / 1000) + "s"))}) " +
                          $"episodes={net.EpisodeStore.Episodes.Count} monologues={monologue.Log.Count}");
        foreach (var (mt, text) in monologue.Log)
            Console.WriteLine($"  [{mt / 1000:F0}s] {text}");
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
              eeg <sec>           resting-state lfp probe: band powers, 1/f slope, alpha ratio
              tune <sec>          sweep noise x gaba gain, report rate/branching/avalanches
              day <sec>           compressed full day: wake, episodes, sleep with replay, wake
              surprise <sec>      quiet room, sleep, then wake-on-surprise (add --camera for a live webcam)
              paradigm <name>     mmn | conditioning | discounting  (--trials=N --out=dir)
              wonder [goal]       autonomous research agent (mock protocol keyless; --live uses Muse Wonder)
              meta-check [--all]  verify the meta tier (mocks keyless; live with a key)
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
