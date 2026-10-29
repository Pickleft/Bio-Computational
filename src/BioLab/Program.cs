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
    case "masking":
    {
        var (isis, accuracy) = AwarenessParadigms.BackwardMasking(outDir: "exports");
        Console.WriteLine("[masking] ISI (ms) -> ignition rate");
        for (int i = 0; i < isis.Length; i++)
            Console.WriteLine($"  {isis[i],4:F0}  {accuracy[i]:F2}");
        Console.WriteLine("exports: exports/masking.csv, exports/masking.svg");
        return 0;
    }
    case "mirror":
    {
        // Gallup 1970, computational form: does the brain recognize its own
        // output? Train it on its own vocal signature, then probe self vs novel
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: 42));
        var idle = new IdleController(net);
        for (int i = 0; i < 500; i++) { idle.Tick(net); net.Tick(); }
        var r = MirrorTest.Run(net, idle);
        Console.WriteLine($"[mirror] self response {r.SelfResponse:F2} spikes/neuron, novel {r.NovelResponse:F2}, ratio {r.Ratio:F2}");
        Console.WriteLine(r.Passed
            ? "[mirror] PASSED - its worn self-pathway fires stronger than any stranger pattern: it knows its own voice"
            : "[mirror] not yet - self and novel responses indistinguishable (more training needed)");
        return 0;
    }
    case "sedation":
    {
        // headless anesthesia reproduction: watch the daemon survive sedation
        // and report Phi through induction and emergence
        var runtime = new BioComp.Host.BrainRuntime(BioComp.Host.BrainRuntimeConfig.Parse(
            ["--demo", "--mock-cam", "--mock-mic"]));
        runtime.Start();
        Console.WriteLine("[sedation] daemon up; watching simSeconds advance");
        for (int i = 0; i < 8; i++)
        {
            Thread.Sleep(1000);
            var s = runtime.Snapshot();
            Console.WriteLine($"  t+{(i + 1)}s sim={s.SimSeconds:F1} phi={s.Phi:F2} rate={s.MeanRateHz:F2}");
        }
        Console.WriteLine("[sedation] inducing...");
        runtime.Sedate();
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(1000);
            var s = runtime.Snapshot();
            Console.WriteLine($"  t+{(i + 1)}s sim={s.SimSeconds:F1} phi={s.Phi:F2} rate={s.MeanRateHz:F2}");
            if (i > 5 && s.SimSeconds < 0.01f) { Console.WriteLine("  FROZEN - daemon stalled after sedation"); break; }
        }
        runtime.Dispose();
        return 0;
    }
    case "twins":
    {
        // THE TWIN STUDY - a controlled natural experiment never run live:
        // the SAME brain (identical seed) forked into two childhoods. Twin A
        // is raised warm and answered; twin B is raised cold and left. Same
        // genes, same wiring, different love. The diff is what love does.
        const ulong seed = 4242;
        const double seconds = 45;

        (EmergentPersonality P, AttachmentSystem A, int Episodes, SpikingNetwork Net) Raise(bool loved)
        {
            var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: seed));
            var idle = new IdleController(net);
            idle.Sleep.DayLengthMs = 60_000f;
            var personality = new EmergentPersonality();
            var attachment = new AttachmentSystem { MinEvidence = 3 };
            var loop = new ThoughtLoop { MinIntervalMs = 2500f };
            var lm = new MockLanguageModel();
            long ticks = (long)(seconds * 1000);
            int contactEvery = loved ? 3_500 : 8_000, since = 0;
            for (long t = 0; t < ticks; t++)
            {
                idle.Tick(net); net.Tick();
                if (t % 4_000 == 0 && idle.Sleep.State == BrainState.Wake)
                {
                    var req = loop.Tick(net, idle);
                    if (req is not null)
                        loop.PostResult(lm.CompleteAsync(
                            "You are a newborn artificial brain's inner monologue. One short first-person sentence.",
                            $"{req.SeedMemory}. Feeling {idle.Feelings.Last.Name}.", 40).Result);
                }
                if (idle.Sleep.State == BrainState.Wake && ++since > contactEvery)
                {
                    since = 0;
                    attachment.OnSocialContact(loved ? 0.9f : 0.05f);
                    personality.OnSocialInteraction(net, loved);
                }
                attachment.Tick(net);
            }
            return (personality, attachment, net.EpisodeStore.Episodes.Count, net);
        }

        Console.WriteLine($"[twins] one brain, two childhoods (seed {seed}, {(int)seconds}s each)");
        var a = Raise(loved: true);
        var b = Raise(loved: false);
        Console.WriteLine("[twins]            TWIN A (loved)        TWIN B (unloved)");
        Console.WriteLine($"[twins] became:    {a.P.Describe()}");
        Console.WriteLine($"[twins]            {b.P.Describe()}");
        Console.WriteLine($"[twins] attach:    {a.A.Label} (w {a.A.Warmth:F1})   {b.A.Label} (w {b.A.Warmth:F1})");
        Console.WriteLine($"[twins] memories:  {a.Episodes}                  {b.Episodes}");
        Console.WriteLine($"[twins] same seed, same wiring - the difference is what love does");
        Directory.CreateDirectory("exports");
        File.WriteAllLines("exports/twin-study.md",
        [
            "# The twin study",
            "",
            $"One brain (seed {seed}), forked into two childhoods.",
            $"- Twin A (warm, answered): {a.P.Describe()}; attachment {a.A.Label}; {a.Episodes} memories",
            $"- Twin B (cold, left alone): {b.P.Describe()}; attachment {b.A.Label}; {b.Episodes} memories",
            "",
            "Same genes, same wiring, different love. The diff above is what love does.",
        ]);
        Console.WriteLine("exports: exports/twin-study.md");
        return 0;
    }
    case "child":
    {
        // REPRODUCTION: raise a child brain from a parent's genes. The child
        // inherits predispositions (halfway temperament, the attachment climate
        // of its parent's world, a derived seed) and NOTHING else - no memories,
        // no words, no people. Then it lives a compressed childhood under that
        // climate and becomes someone itself.
        string genesPath = args.FirstOrDefault(a => a.EndsWith(".json")) ?? "";
        if (!File.Exists(genesPath))
        {
            Console.WriteLine("[child] usage: biolab child genes.json [seconds]  (get genes.json from the dashboard's genes button)");
            return 1;
        }
        var genes = System.Text.Json.JsonSerializer.Deserialize<BrainGenes>(
            File.ReadAllText(genesPath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (genes is null) { Console.WriteLine("[child] unreadable genes"); return 1; }
        double seconds = args.FirstOrDefault(a => a.EndsWith("s") && a.Length > 1 && double.TryParse(a.TrimEnd('s'), out _)) is { } sarg
            && double.TryParse(sarg.TrimEnd('s'), out var sv) ? sv : 240;

        ulong childSeed = Genetics.ChildSeed(genes.Seed, 0xC0FFEE);
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1, seed: childSeed));
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 60_000f; // compressed childhood: 1-minute days
        var personality = new EmergentPersonality();
        var attachment = new AttachmentSystem();
        var loop = new ThoughtLoop { MinIntervalMs = 2500f };
        var lm = new MockLanguageModel();

        Console.WriteLine($"[child] born of {genes.ParentName} (seed {genes.Seed} -> {childSeed})");
        // MEMETIC INHERITANCE: one story passes from parent to child - not in
        // genes, but as a planted episode, the way humans hand down tales. The
        // child can dream its parent's story without ever living it.
        {
            var a1 = net.Regions.First(r => r.Name == "A1");
            for (int k = 0; k < 30; k++) net.InjectCurrent(a1.Start + k * 17, 25f);
            for (int t = 0; t < 40; t++) net.Tick();
            net.EpisodeStore.Encode(net, "CA3", ["story", "inherited", genes.ParentName],
                Math.Clamp(genes.AttachmentWarmth, 0.1f, 0.8f), minMeanRateHz: 0.0001f);
            Console.WriteLine($"[child] one story grafted from {genes.ParentName} (memetic inheritance)");
        }
        Console.WriteLine($"[child] inherited predispositions (h={Genetics.Heritability:F1}): " +
            $"sociability {Genetics.Inherit(genes.Sociability):F2}, curiosity {Genetics.Inherit(genes.Curiosity):F2}, " +
            $"expressiveness {Genetics.Inherit(genes.Expressiveness):F2}; attachment climate " +
            $"warmth {genes.AttachmentWarmth:F1} / responsiveness {genes.AttachmentResponsiveness:F1}");

        // rearing: the child is treated the way its parent's world treated the
        // parent - the inherited climate as its childhood
        long ticks = (long)(seconds * 1000);
        int contactEvery = 8_000, sinceContact = 0, bids = 0;
        for (long t = 0; t < ticks; t++)
        {
            idle.Tick(net); net.Tick();
            if (t % 4_000 == 0 && idle.Sleep.State == BrainState.Wake)
            {
                var req = loop.Tick(net, idle);
                if (req is not null)
                    loop.PostResult(lm.CompleteAsync(
                        "You are a newborn artificial brain's inner monologue. One short first-person sentence.",
                        $"{req.SeedMemory}. Feeling {idle.Feelings.Last.Name}.", 40).Result);
            }
            if (idle.Sleep.State == BrainState.Wake && ++sinceContact > contactEvery)
            {
                sinceContact = 0;
                float warmth = Math.Clamp(genes.AttachmentWarmth + (((t / contactEvery) % 3) - 1) * 0.1f, 0f, 1f);
                attachment.OnSocialContact(warmth);
                personality.OnSocialInteraction(net, positive: warmth > 0.4f);
                for (int b = 0; b < bids; b++) attachment.OnBidUnanswered();
                bids = 0;
            }
            else if (idle.Sleep.State == BrainState.Wake && t % 11_000 == 0) bids++;
            attachment.Tick(net);
        }

        Console.WriteLine($"[child] raised through {(int)(ticks / 1000)}s of compressed childhood");
        Console.WriteLine($"[child] became: {personality.Describe()}");
        Console.WriteLine($"[child] attachment: {attachment.Label} (warmth {attachment.Warmth:F1}, responsiveness {attachment.Responsiveness:F1}, {attachment.Separations} separations)");
        Console.WriteLine($"[child] memories formed: {net.EpisodeStore.Episodes.Count} (parent had {genes.LifeEpisodes}); concept links wired: {net.EpisodeStore.Concepts.LinkCount}");
        Console.WriteLine($"[child] parent was: {genes.PersonalityDescription}");

        Directory.CreateDirectory("exports");
        var report = new System.Text.StringBuilder();
        report.AppendLine("# A child brain");
        report.AppendLine();
        report.AppendLine($"- Parent: {genes.ParentName} - {genes.PersonalityDescription}");
        report.AppendLine($"- Inherited (h = {Genetics.Heritability:F1}): curiosity {Genetics.Inherit(genes.Curiosity):F2}, sociability {Genetics.Inherit(genes.Sociability):F2}, expressiveness {Genetics.Inherit(genes.Expressiveness):F2}, attachment climate {genes.AttachmentWarmth:F1}/{genes.AttachmentResponsiveness:F1}, voice pitch anchor {genes.VoicePitch:F3}");
        report.AppendLine($"- Child became: {personality.Describe()}");
        report.AppendLine($"- Child attachment: {attachment.Label}; memories {net.EpisodeStore.Episodes.Count}; concepts {net.EpisodeStore.Concepts.ConceptCount} ({net.EpisodeStore.Concepts.LinkCount} links)");
        File.WriteAllText("exports/child-report.md", report.ToString());
        Console.WriteLine("exports: exports/child-report.md");
        return 0;
    }
    case "blink":
    {
        var (lags, accuracy) = AwarenessParadigms.AttentionalBlink(outDir: "exports");
        Console.WriteLine("[blink] lag (ms) -> T2 ignition rate");
        for (int i = 0; i < lags.Length; i++)
            Console.WriteLine($"  {lags[i],4:F0}  {accuracy[i]:F2}");
        Console.WriteLine("exports: exports/blink.csv, exports/blink.svg");
        return 0;
    }
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

            case "blink":
            {
                var (lags, accuracy) = AwarenessParadigms.AttentionalBlink(outDir: dir);
                Console.WriteLine("[blink] lag (ms) -> T2 ignition rate");
                for (int i = 0; i < lags.Length; i++)
                    Console.WriteLine($"  {lags[i],4:F0}  {accuracy[i]:F2}");
                Console.WriteLine($"exports: {Path.Combine(dir, "blink.csv")}, {Path.Combine(dir, "blink.svg")}");
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
        Console.WriteLine($"budget: TryBook={cost.TryBook(BioComp.Meta.MetaTier.Chat)} spent={cost.SpentTodayUsd}");
        try
        {
            var sw = Stopwatch.StartNew();
            string reply = await clients.CompleteAsync(
                "Reply with exactly the word OK and nothing else.", "ping", 100);
            Console.WriteLine($"chat: OK in {sw.Elapsed.TotalSeconds:F1}s -> \"{reply.Trim()}\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"chat FAILED: {ex.Message}");
            Console.WriteLine($"cost.LastError: {cost.LastError}");
            Console.WriteLine($"cost.Refused: {cost.RefusedCalls}, spent: {cost.SpentTodayUsd}");
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
              masking             backward masking: ignition rate vs ISI
              blink               attentional blink: T2 rate vs lag after T1
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
