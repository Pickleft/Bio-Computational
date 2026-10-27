using System.Diagnostics;
using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Persistence;
using BioComp.Core.Senses;
using BioComp.Core.Sleep;
using BioComp.Meta;

namespace BioComp.Host;

public sealed class BrainRuntimeConfig
{
    /// <summary>null = auto: use the real device when present, fall back to the mock.</summary>
    public bool? LiveCamera { get; init; }
    public bool? LiveMic { get; init; }
    public bool LiveMeta { get; init; }
    public bool DemoMode { get; init; }
    public ulong Seed { get; init; } = 42;

    public static BrainRuntimeConfig Parse(string[] args) => new()
    {
        LiveCamera = args.Contains("--mock-cam") ? false : args.Contains("--camera") ? true : null,
        LiveMic = args.Contains("--mock-mic") ? false : args.Contains("--mic") ? true : null,
        LiveMeta = args.Contains("--live-meta"),
        DemoMode = args.Contains("--demo"),
        Seed = ulong.TryParse(args.FirstOrDefault(a => a.StartsWith("--seed="))?["--seed=".Length..], out var s) ? s : 42,
    };
}

/// <summary>JSON-friendly snapshot consumed by the dashboard.</summary>
public sealed class BrainSnapshot
{
    public float SimSeconds { get; set; }
    public double RealtimeFactor { get; set; }
    public string SleepState { get; set; } = "";
    public float SleepPressure { get; set; }
    public string Feeling { get; set; } = "";
    public string FeelingNarrative { get; set; } = "";
    public float Valence { get; set; }
    public float Arousal { get; set; }
    public float[] Modulators { get; set; } = new float[7]; // DA, 5-HT, ACh, NA, OXT, Hist, Mel
    public float MeanRateHz { get; set; }
    public float Energy { get; set; }
    public float ClockRate { get; set; }
    public float SubjectiveMinutes { get; set; }
    public float SpentUsd { get; set; }
    public int ApiCalls { get; set; }
    public int Ignitions { get; set; }
    public int Surprises { get; set; }
    public int Replays { get; set; }
    public int Episodes { get; set; }
    public bool CameraLive { get; set; }
    public bool MicLive { get; set; }
    public bool MetaLive { get; set; }
    public string LastMonologue { get; set; } = "";
    public List<string> Dreams { get; set; } = [];
    public List<string> PlainThoughts { get; set; } = [];
    public List<string> Chat { get; set; } = [];
    public List<object[]> Regions { get; set; } = [];
    public float[] LfpBands { get; set; } = new float[5];
    public List<float[]> LfpHistory { get; set; } = [];
    public float LfpSlope { get; set; }
    public float AlphaRatio { get; set; }
    public float[] RegionRates { get; set; } = [];   // Hz per region, last second
    public float Consciousness { get; set; }          // 0..1 composite awareness gauge
    public string ConsciousnessLabel { get; set; } = "";
    public List<float[]> FeelingJourney { get; set; } = []; // [tSec, valence, arousal] history
    public List<float[]> Raster { get; set; } = [];   // [tSec, neuronFrac, regionIdx]
    public List<string> Thought { get; set; } = [];   // the thinking-process feed
    public List<string> Monologues { get; set; } = [];
    public List<string> RecentEpisodes { get; set; } = [];
    public List<object[]> SleepStrip { get; set; } = []; // [tSec, state] history
}

/// <summary>
/// The always-on daemon core: owns the sim loop (paced to wall time), the senses
/// (mock or live), the Meta tier (mock or live behind the cost governor), autonomous
/// actions with speech, and periodic checkpoints. The dashboard shares the process and
/// reads snapshots; BioLab drives the same runtime headless for experiments.
/// </summary>
public sealed class BrainRuntime : IDisposable
{
    private readonly BrainRuntimeConfig _cfg;
    private readonly SpikingNetwork _net;
    private readonly IdleController _idle;
    private readonly SalienceMonitor _salience;
    private readonly EventVisionEncoder _vision;
    private readonly Cochlea _cochlea;
    private readonly IFrameSource _camera = null!;
    private readonly IAudioSource _mic = null!;
    private readonly IInteroceptionSource _body;
    private readonly ILanguageModel _lm;
    private readonly ITranscriber _transcriber;
    private readonly ISegmenter _segmenter;
    private readonly IImageGenerator _imager;
    private readonly CostGovernor _cost = new() { DailyBudgetUsd = 20f };
    private readonly MonologueGenerator _monologue;
    private readonly DreamGenerator _dreamer;
    private readonly ISpeechOutput _speech;
    private readonly Thread _thread;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly object _snapGate = new();
    private BrainSnapshot _latest = new();

    private readonly MockFrameSource? _mockCamera;
    private readonly MockAudioSource? _mockMic;
    private readonly int _retinaStart, _retinaCount, _a1Start, _a1Count;
    private readonly int _thalStart, _thalCount, _v1Start, _v1Count;
    private readonly uint[] _regionLastCounts;
    private MapData? _mapData;
    public BioComp.Core.Metrics.LfpProxy Lfp { get; } = new();
    private readonly List<short> _audioBuffer = [];
    private readonly List<string> _thought = [];
    private readonly List<string> _monologueLog = [];
    private readonly List<object[]> _sleepStrip = [];
    private readonly Queue<(byte[] Png, float Tms)> _pendingSegment = new();
    private byte[]? _lastFramePng;
    private bool _wasVoice;
    private long _voiceCooldownUntil;
    private long _lastDemoCycle = -1;
    private volatile bool _dreamBusy;
    private readonly List<string> _dreams = [];
    private readonly List<string> _chat = [];
    private readonly List<string> _plain = [];
    private readonly List<float[]> _feelingJourney = [];
    private float _lastReplyMs = -1e9f;
    private long _nextAmbientMs = 8_000;

    /// <summary>An idle thought generated from the ACTUAL current state — no rotation,
    /// no templates. What the brain thinks about at rest depends on what it feels, how
    /// long it's been quiet, what it remembers, and what its body is doing.</summary>
    private string? AmbientThought()
    {
        var f = _idle.Feelings.Last;
        double idleSec = _idle.Time.SubjectiveSinceEventMs / 1000.0;
        if (_idle.Sleep.State is BioComp.Core.Sleep.BrainState.NREM or BioComp.Core.Sleep.BrainState.REM) return null;

        // the thought follows directly from the feeling + context
        return f.Name switch
        {
            "bored" when idleSec > 30 => $"It's been {(int)idleSec} subjective seconds. The silence is heavy.",
            "bored" => "Nothing is happening.",
            "tired" => "I can feel myself slowing down.",
            "curious" => "Something is different. I want to understand it.",
            "stressed" => "Something feels wrong. I can't settle.",
            "calm" when _net.EpisodeStore.Episodes.Count > 0 =>
                $"I keep thinking about {string.Join(", ", _net.EpisodeStore.Episodes[^1].Tags.Take(2))}.",
            "calm" => "The room is steady. Nothing needs me.",
            _ => _net.EpisodeStore.Episodes.Count > 0 && idleSec > 60
                ? $"It's been quiet for a while. I remember {string.Join(", ", _net.EpisodeStore.Episodes[^1].Tags.Take(1))}."
                : null, // don't speak if there's genuinely nothing to say
        };
    }
    private (bool Cam, bool Mic) _devicesLive;
    private int _lastIgnition, _lastSurprise, _lastReplayCount;
    private BrainState _lastSleepState;
    private double _simSecondsAtSnapshot;
    private volatile bool _running;

    public bool MetaLive { get; private set; }

    public BrainRuntime(BrainRuntimeConfig cfg, ISpeechOutput? speech = null)
    {
        _cfg = cfg;
        var brainCfg = BrainConfig.Default(scale: cfg.DemoMode ? 3 : 1, seed: cfg.Seed); // demo brain: ~18k neurons for a dense map
        _net = new SpikingNetwork(brainCfg);
        _idle = new IdleController(_net);
        if (cfg.DemoMode) _idle.Sleep.DayLengthMs = 120_000; // 2-minute demo day: sleep on camera
        _salience = new SalienceMonitor();
        _vision = new EventVisionEncoder(64, 64);
        _cochlea = new Cochlea();
        _speech = speech ?? new ConsoleSpeech();

        var retina = _net.Regions.First(r => r.Name == "Retina");
        (_retinaStart, _retinaCount) = (retina.Start, retina.Count);
        var a1 = _net.Regions.First(r => r.Name == "A1");
        (_a1Start, _a1Count) = (a1.Start, a1.Count);
        var thal = _net.Regions.First(r => r.Name == "Thal");
        _thalStart = thal.Start;
        _thalCount = thal.Count;
        var v1 = _net.Regions.First(r => r.Name == "V1");
        _v1Start = v1.Start;
        _v1Count = v1.Count;
        _regionLastCounts = new uint[_net.Regions.Count];

        // senses: real devices when present (auto by default), deterministic mocks otherwise
        bool camLive = false;
        if (_cfg.LiveCamera != false)
        {
            try { _camera = new BioComp.Senses.OpenCvCameraSource(); camLive = true; }
            catch (Exception ex) { Note($"camera unavailable ({ex.Message}); using mock"); }
        }
        if (!camLive)
            _camera = _mockCamera = new MockFrameSource(62, personAtMs: 100_000);

        bool micLive = false;
        if (_cfg.LiveMic != false)
        {
            try { _mic = new BioComp.Senses.NAudioMicSource(); micLive = true; }
            catch (Exception ex) { Note($"microphone unavailable ({ex.Message}); using mock"); }
        }
        if (!micLive)
            _mic = _mockMic = new MockAudioSource(63, voiceAtMs: 100_000);
        _devicesLive = (camLive, micLive);

        _body = OperatingSystem.IsWindows() && !cfg.DemoMode
            ? new BioComp.Senses.WindowsInteroception()
            : new MockInteroceptionSource();

        // meta: live only with a key and explicit opt-in; mocks keep everything else identical
        var opts = MetaOptions.Load();
        if (cfg.LiveMeta && opts.HasKey)
        {
            var clients = new MetaClients(opts, _cost);
            _lm = clients;
            _transcriber = clients;
            _segmenter = clients;
            _imager = clients;
            MetaLive = true;
        }
        else
        {
            _lm = new MockLanguageModel();
            _transcriber = new MockTranscriber();
            _segmenter = new MockSegmenter();
            _imager = new MockImageGenerator();
            MetaLive = false;
        }
        _monologue = new MonologueGenerator(_lm)
        {
            MinInterval = TimeSpan.FromSeconds(cfg.DemoMode ? 8 : 120),
        };
        _dreamer = new DreamGenerator(_imager)
        {
            MinInterval = TimeSpan.FromSeconds(cfg.DemoMode ? 25 : 300),
        };

        _thread = new Thread(RunLoop) { IsBackground = true, Name = "brain" };
    }

    public void Start()
    {
        TryResume();
        _running = true;
        _thread.Start();
    }

    public void Stop() => _running = false;

    public BrainSnapshot Snapshot()
    {
        lock (_snapGate) return _latest;
    }

    private void RunLoop()
    {
        long nextCheckpoint = 300_000; // 5 sim-minutes
        int consecutiveErrors = 0;
        while (_running)
        {
            try
            {
                double targetSimMs = _wall.Elapsed.TotalMilliseconds;
                double limit = targetSimMs + 50;
                while (_net.TimeMs < Math.Min(targetSimMs, limit))
                    StepOnce();
                consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                // the brain must never die from a single bad tick
                if (++consecutiveErrors > 100)
                {
                    Note($"FATAL: {consecutiveErrors} consecutive errors, last: {ex.Message}");
                    break;
                }
            }

            try { BuildSnapshot(); } catch { /* snapshot must not kill the loop */ }
            if (_net.TimeMs >= nextCheckpoint)
            {
                TryCheckpoint();
                nextCheckpoint = (long)_net.TimeMs + 300_000;
            }
            Thread.Sleep(15);
        }
        TryCheckpoint();
    }

    /// <summary>A failed checkpoint must never kill the brain.</summary>
    private void TryCheckpoint()
    {
        try { CheckpointService.Save(_net, Path.Combine("data", "brain.ckpt")); }
        catch (Exception ex) { Note($"checkpoint failed: {ex.Message}"); }
    }

    /// <summary>One simulated millisecond: senses in, cognition, actions out.</summary>
    private void StepOnce()
    {
        float t = _net.TimeMs;

        // thalamocortical LFP proxy: net synaptic conductance of Thal + V1
        float lfp = 0;
        var cond = _net.Cond;
        for (int i = _thalStart; i < _thalStart + _thalCount; i++)
            lfp += cond.GAmpa[i] + cond.GNmda[i] - cond.GGabaa[i] - cond.GGabab[i];
        for (int i = _v1Start; i < _v1Start + _v1Count; i++)
            lfp += cond.GAmpa[i] + cond.GNmda[i] - cond.GGabaa[i] - cond.GGabab[i];
        Lfp.AddSample(lfp);

        // ---- vision ----
        if (_mockCamera is not null) _mockCamera.Advance(1);

        // demo-mode mock world: a visitor arrives once per compressed day (the brain
        // itself reacts with no scripting; this only scripts the environment)
        if (_cfg.DemoMode && _mockCamera is not null && _mockMic is not null)
        {
            double dayMs = _idle.Sleep.DayLengthMs;
            long cycle = (long)(_net.TimeMs / dayMs);
            if (cycle != _lastDemoCycle && _net.TimeMs % dayMs > dayMs * 0.45)
            {
                _lastDemoCycle = cycle;
                _mockCamera.TriggerPerson();
                _mockMic.TriggerVoice();
                Note("WORLD visitor enters (mock)");
            }
        }

        if (_camera.TryReadNext(out var frame))
        {
            var events = _vision.Process(frame);
            _vision.Inject(_net, _retinaStart, _retinaCount, events);
            _salience.OnVisionFrame(_net, _vision.ChangeEnergyLastFrame);
            _idle.VisionSalience = MathF.Max(_idle.VisionSalience, _salience.Arousal * 2f);
            if (_vision.EventsLastFrame > 20)
                _lastFramePng = PngFromFrame(frame);
            if (_salience.SurpriseEvents > _lastSurprise && _lastFramePng is not null && _pendingSegment.Count < 2)
                _pendingSegment.Enqueue((_lastFramePng, t));
        }

        // ---- audition ----
        if (_mockMic is not null) _mockMic.Advance(1);
        if (_mic.TryReadNext(out var block))
        {
            if (!_idle.Self.IsOwnVoice(t)) // attenuate self-produced sound
            {
                _cochlea.Process(block);
                _salience.OnAudioBlock(_net, _cochlea.OnsetThisBlock, _cochlea.EnergyRms);
                if (_cochlea.VoiceLikely) _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 1.0f);
                else if (_cochlea.OnsetThisBlock) _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 0.7f);
                _audioBuffer.AddRange(block.Samples);
                if (_audioBuffer.Count > 16_000 * 8) _audioBuffer.RemoveRange(0, _audioBuffer.Count - 16_000 * 8);
                if (_cochlea.VoiceLikely && !_wasVoice && _net.TickIndex > _voiceCooldownUntil)
                {
                    _wasVoice = true;
                    _voiceCooldownUntil = _net.TickIndex + 15_000;
                    _ = HandleSpeechAsync(t);
                }
                if (!_cochlea.VoiceLikely) _wasVoice = false;
            }
        }

        // ---- body ----
        if (_net.TickIndex % 1_000 == 0) _idle.Body = _body.Read();

        // ---- cognition ----
        _idle.Tick(_net, _salience.ExternalArousal);

        // ---- event bookkeeping (ignition, surprise, replay, sleep) ----
        if (_salience.SurpriseEvents > _lastSurprise)
        {
            _lastSurprise = _salience.SurpriseEvents;
            var (st, tag) = _salience.SurpriseLog[^1];
            Note($"SURPRISE {tag} (novelty spike)");
            _idle.OnEvent(_net, [tag], 0.2f, _salience.ExternalArousal);
        }
        if (_idle.Workspace.IgnitionCount > _lastIgnition)
        {
            _lastIgnition = _idle.Workspace.IgnitionCount;
            var ig = _idle.Workspace.LastIgnition;
            Note($"IGNITION {ig.Winner} threshold cleared ({ig.Tag})");
            _ = MonologueOnIgnition();
        }
        if (_idle.Replay.ReplayCount > _lastReplayCount)
        {
            _lastReplayCount = _idle.Replay.ReplayCount;
            var r = _idle.Replay.Log[^1];
            Note($"REPLAY episode #{r.EpisodeId} [{r.Tags}]");
        }
        if (_idle.Sleep.State != _lastSleepState)
        {
            _lastSleepState = _idle.Sleep.State;
            _sleepStrip.Add([MathF.Round(t / 1000f, 1), _lastSleepState.ToString()]);
            if (_sleepStrip.Count > 600) _sleepStrip.RemoveAt(0);
            if (_lastSleepState is BrainState.REM or BrainState.NREM)
                Note($"SLEEP {_lastSleepState}");
            else if (_lastSleepState == BrainState.Wake)
                Note("AWAKE");
        }

        // ---- REM dreams: render the salient episode, show it, and re-enter vision ----
        if (_idle.Sleep.State == BrainState.REM && !_dreamBusy)
            _ = DreamAsync();

        // ---- feelings emerge from real dynamics, not oscillators ----
        // (no artificial mood drift: modulators move when events happen, when the
        // brain self-stimulates, during replay, and during sleep; at rest they settle)

        // ---- ambient inner monologue: idle is a stream of consciousness, not silence ----
        if (_net.TimeMs >= _nextAmbientMs)
        {
            _nextAmbientMs = (long)_net.TimeMs + (_cfg.DemoMode ? 5_000 : 12_000);
            string? line = AmbientThought();
            if (line is not null) Note($"AMBIENT {line}");
        }

        // ---- actions ----
        if (_net.TickIndex % 250 == 0)
        {
            var action = _idle.Actions.Choose(_net, _idle.Sleep, _idle.Workspace,
                _idle.Feelings.Last, curiosity: _salience.VisionModel.Novelty,
                socialDrive: _idle.AudioSalience);
            if (action is not null and not BrainAction.StaySilent)
            {
                Note($"ACTION {action}");
                if (action is BrainAction.Speak or BrainAction.DisplayText)
                    _ = SpeakAsync();
                if (action == BrainAction.Explore)
                    SelfStimulate(24, 18f, "exploring");
                if (action == BrainAction.SeekStimulation)
                    SelfStimulate(40, 24f, "seeking stimulation");
            }
        }

        // ---- segmentation (async results enter as tagged events) ----
        if (_pendingSegment.TryDequeue(out var seg))
            _ = SegmentAsync(seg.Png, seg.Tms);

        _simSecondsAtSnapshot = _net.TimeMs / 1000.0;
    }

    private async Task DreamAsync()
    {
        _dreamBusy = true;
        try
        {
            string? file = await _dreamer.MaybeDreamAsync(_net, _idle);
            if (file is not null)
            {
                Note($"DREAM {Path.GetFileName(file)} (from {(_imager is MockImageGenerator ? "mock" : "muse image")})");
                _dreams.Add(Path.GetFileName(file));
                if (_dreams.Count > 12) _dreams.RemoveAt(0);
                await DreamReentryAsync(file);
            }
        }
        catch (Exception ex) { Note($"dream failed: {ex.Message}"); }
        finally { _dreamBusy = false; }
    }

    /// <summary>The dream re-enters the visual field as self-generated imagery.</summary>
    private async Task DreamReentryAsync(string file)
    {
        try
        {
            byte[] png = await File.ReadAllBytesAsync(file);
            using var src = OpenCvSharp.Cv2.ImDecode(png, OpenCvSharp.ImreadModes.Grayscale);
            if (src.Empty()) return;
            using var small = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Resize(src, small, new OpenCvSharp.Size(64, 64));
            var pixels = new byte[64 * 64];
            System.Runtime.InteropServices.Marshal.Copy(small.Data, pixels, 0, pixels.Length);

            float until = _net.TimeMs + 1_500f;
            _idle.Self.VisualOutput(until); // this imagery is self-produced
            var frame = new GrayFrame(pixels, 64, 64);
            var events = _vision.Process(frame);
            _vision.Inject(_net, _retinaStart, _retinaCount, events);
            Note($"DREAM-REENTRY {events.Count} visual events from own imagery");
        }
        catch (Exception ex) { Note($"dream re-entry failed: {ex.Message}"); }
    }

    private async Task HandleSpeechAsync(float t)
    {
        try
        {
            var clip = _audioBuffer.Count > 16_000 * 4
                ? _audioBuffer.Skip(_audioBuffer.Count - 16_000 * 4).ToArray()
                : _audioBuffer.ToArray();
            var tr = await _transcriber.TranscribeAsync(clip);
            if (!string.IsNullOrWhiteSpace(tr.Transcript))
            {
                Note($"HEARD \"{tr.Transcript}\"{(tr.FromLiveApi ? " (voice api)" : " (mock)")}");
                _idle.OnEvent(_net, ["voice", tr.Transcript[..Math.Min(40, tr.Transcript.Length)]], 0.3f, 0.8f);
            }
        }
        catch (Exception ex) { Note($"transcribe failed: {ex.Message}"); }
    }

    private async Task SegmentAsync(byte[] png, float t)
    {
        try
        {
            var seg = await _segmenter.SegmentAsync(png, "person");
            if (seg.Boxes.Length > 0)
            {
                var b = seg.Boxes[0];
                string kind = (b.Y2 - b.Y1) > (b.X2 - b.X1) * 1.2 ? "person" : "object";
                Note($"SIGHT {kind} {(seg.FromLiveApi ? "(sam 3.1)" : "(mock)")} box {b.X1},{b.Y1}..{b.X2},{b.Y2}");
                _idle.OnEvent(_net, [kind], 0.2f, 0.7f);
            }
        }
        catch (Exception ex) { Note($"segmentation failed: {ex.Message}"); }
    }

    private async Task MonologueOnIgnition()
    {
        try
        {
            string? text = await _monologue.MaybeGenerateAsync(_net, _idle);
            if (text is not null)
            {
                _monologueLog.Add(text);
                if (_monologueLog.Count > 50) _monologueLog.RemoveAt(0);
                Note($"MONOLOGUE \"{Trunc(text, 110)}\"");
            }
        }
        catch (Exception ex) { Note($"monologue failed: {ex.Message}"); }
    }

    private async Task SpeakAsync()
    {
        string line = _monologueLog.Count > 0 ? _monologueLog[^1] : _idle.Feelings.Last.Narrative;
        float durationMs = MathF.Max(2000, line.Length * 65f);
        _idle.Self.VoiceOutput(_net.TimeMs + durationMs); // efference copy: this sound is me
        _speech.Speak(line);
        _idle.Actions.OnOutcome(_net, BrainAction.Speak, welcomed: true); // human response detection is future work
        await Task.CompletedTask;
    }

    /// <summary>Region graph payload for the dashboard: region anchors, names, and
    /// the projection edges with weights.</summary>
    public MapData GetMapData()
    {
        if (_mapData is not null) return _mapData;
        var anchors = _net.Config.Regions.Select(r => new[] { r.SheetX, r.SheetY }).ToArray();
        var names = _net.Config.Regions.Select(r => r.Name).ToArray();
        var edges = _net.Config.Projections
            .GroupBy(p => (p.Pre, p.Post))
            .Select(g =>
            {
                int from = Array.IndexOf(names, g.Key.Pre);
                int to = Array.IndexOf(names, g.Key.Post);
                return new[] { from, to, g.Sum(p => p.OutDegree * p.WeightMean) };
            })
            .Where(e => e[0] >= 0 && e[1] >= 0)
            .ToArray();
        _mapData = new MapData(names, anchors, edges);
        return _mapData;
    }

    public sealed record MapData(string[] Names, float[][] Anchors, float[][] Edges);

    /// <summary>Curiosity self-stimulation: the brain pokes itself and feels it.
    /// The DA tick is small — it's the internal reward of agency, not the full
    /// novelty pathway (that fires through SalienceMonitor when the stimulation
    /// produces actual sensory change).</summary>
    private void SelfStimulate(int neurons, float picoAmps, string what)
    {
        var ca3 = _net.Regions.FirstOrDefault(r => r.Name == "CA3") ?? _net.Regions[0];
        for (int k = 0; k < neurons; k++)
            _net.InjectCurrent(ca3.Start + _net.Rng.NextInt(ca3.Count), picoAmps);
        // agency is mildly rewarding (small DA tick), and any input is arousing (small NA)
        _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.08f);
        _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.12f);
        Note($"STIM self ({what}) - {neurons} neurons in {ca3.Name}");
    }

    private void Note(string line)
    {
        lock (_snapGate)
        {
            _thought.Add($"[{_net.TimeMs / 1000:F1}s] {line}");
            if (_thought.Count > 60) _thought.RemoveAt(0);
            string plain = ThoughtNarrator.Narrate(line, _idle.Feelings.Last);
            if (plain.Length > 0)
            {
                _plain.Add($"[{_net.TimeMs / 1000:F1}s] {plain}");
                if (_plain.Count > 60) _plain.RemoveAt(0);
            }
        }
    }

    /// <summary>A human typed into the chat box: treat as direct speech — a real
    /// social stimulus that flows through the normal salience pathway.</summary>
    public void HumanSaid(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (text.Length > 200) text = text[..200];
        lock (_snapGate) _chat.Add($"You: {text}");
        Note($"HEARD \"{text}\" (typed)");
        // wake the brain: being addressed is a social stimulus AND a novel event
        // (correct neuroscience: social interaction releases OXT for bonding,
        // DA for the reward of engagement, NA for the surprise of being addressed)
        _idle.Sleep.ForceWake(_net);
        _idle.OnEvent(_net, ["voice", "you"], 0.4f, 0.9f);
        _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 1.2f);
        _idle.VisionSalience = MathF.Max(_idle.VisionSalience, 1.5f);
        _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin, 0.4f);
        _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.3f);
        _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.25f);
        _ = ReplyAsync(text);
    }

    private async Task ReplyAsync(string userText)
    {
        if (_net.TimeMs - _lastReplyMs < 3_000) return;
        _lastReplyMs = _net.TimeMs;
        try
        {
            var feeling = _idle.Feelings.Last;
            string system =
                "You are a living artificial brain that runs continuously on a laptop. " +
                "Someone is talking to you directly. Respond to WHAT THEY SAID in 1-2 short, " +
                "warm sentences, first person. Your feeling state is context — mention it " +
                "naturally if relevant, but focus on engaging with their words.";
            string user =
                $"They said: \"{userText}\"\n\n" +
                $"Your current state: feeling {feeling.Name}, " +
                $"{_idle.Sleep.State.ToString().ToLower()}, " +
                $"awake for {_idle.Time.SubjectiveSinceEventMs / 1000.0:F0} seconds since last event.";
            string reply = await _lm.CompleteAsync(system, user, 80);
            lock (_snapGate) _chat.Add($"Brain: {reply}");
            _monologueLog.Add(reply);
            if (_monologueLog.Count > 50) _monologueLog.RemoveAt(0);
            Note($"REPLY \"{Trunc(reply, 90)}\"");
        }
        catch (Exception ex) { Note($"reply failed: {ex.Message}"); }
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "...";

    private static byte[] PngFromFrame(GrayFrame frame)
    {
        using var mat = new OpenCvSharp.Mat(frame.Height, frame.Width, OpenCvSharp.MatType.CV_8UC1);
        System.Runtime.InteropServices.Marshal.Copy(frame.Pixels, 0, mat.Data, frame.Pixels.Length);
        OpenCvSharp.Cv2.ImEncode(".png", mat, out byte[] png);
        return png;
    }

    private void TryResume()
    {
        try
        {
            string path = Path.Combine("data", "brain.ckpt");
            if (!File.Exists(path)) return;
            var resumed = CheckpointService.Load(path, BrainConfig.Default(seed: _cfg.Seed));
            Note($"resumed checkpoint at t={resumed.TimeMs / 1000:F0}s");
        }
        catch (Exception ex) { Note($"resume failed (starting fresh): {ex.Message}"); }
    }

    private void BuildSnapshot()
    {
        var snap = new BrainSnapshot
        {
            SimSeconds = _net.TimeMs / 1000f,
            RealtimeFactor = _simSecondsAtSnapshot > 1 ? _simSecondsAtSnapshot / _wall.Elapsed.TotalSeconds : 0,
            SleepState = _idle.Sleep.State.ToString(),
            SleepPressure = _idle.Sleep.SleepPressure,
            Feeling = _idle.Feelings.Last.Name,
            FeelingNarrative = _idle.Feelings.Last.Narrative,
            Valence = _idle.Feelings.Last.Valence,
            Arousal = _idle.Feelings.Last.Arousal,
            MeanRateHz = _net.Recorder.MeanRateHz(MathF.Max(1000, _net.TimeMs)),
            Energy = _net.Energy.Spent,
            ClockRate = _idle.Time.ClockRate,
            SubjectiveMinutes = (float)(_idle.Time.SubjectiveMs / 60_000.0),
            SpentUsd = _cost.SpentTodayUsd,
            ApiCalls = _cost.CallsToday,
            Ignitions = _idle.Workspace.IgnitionCount,
            Surprises = _salience.SurpriseEvents,
            Replays = _idle.Replay.ReplayCount,
            Episodes = _net.EpisodeStore.Episodes.Count,
            CameraLive = _devicesLive.Cam,
            MicLive = _devicesLive.Mic,
            MetaLive = MetaLive,
            LastMonologue = _monologueLog.Count > 0 ? _monologueLog[^1] : "",
            Dreams = [.. _dreams],
            PlainThoughts = [.. _plain],
            Chat = [.. _chat.TakeLast(20)],
            Regions = [.. _net.Regions.Select(r => new object[] { r.Name, r.Count })],
            LfpBands = [.. Lfp.Bands],
            LfpHistory = [.. Lfp.History],
            LfpSlope = Lfp.Slope,
            AlphaRatio = Lfp.AlphaRatio,
            Thought = [.. _thought],
            Monologues = [.. _monologueLog.TakeLast(8)],
            RecentEpisodes = [.. _net.EpisodeStore.Episodes.TakeLast(8)
                .Select(e => $"[{e.TimeMs / 1000:F0}s] {string.Join(", ", e.Tags)}")],
            SleepStrip = [.. _sleepStrip],
        };
        var m = _net.Modulators;
        snap.Modulators[0] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine);
        snap.Modulators[1] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin);
        snap.Modulators[2] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Acetylcholine);
        snap.Modulators[3] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline);
        snap.Modulators[4] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin);
        snap.Modulators[5] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Histamine);
        snap.Modulators[6] = m.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Melatonin);

        // raster: last ~600 spikes as [tSec, neuronIdx, regionIdx] (idx lets the map
        // flash the exact neuron; frac derivable client-side)
        var rec = _net.Recorder;
        int ringLen = rec.Events.Length;
        int n = Math.Min(600, rec.EventCount);
        var raster = new List<float[]>(n);
        for (int k = 1; k <= n; k++)
        {
            int idx = (((rec.EventHead - k) % ringLen) + ringLen) % ringLen;
            var (tMs, neuron) = rec.Events[idx];
            raster.Add([tMs / 1000f, neuron, _net.RegionOf[neuron]]);
        }
        snap.Raster = raster;

        // per-region rates over the last second (node glow + edge traffic)
        var rates = new float[_net.Regions.Count];
        for (int r = 0; r < rates.Length; r++)
        {
            var reg = _net.Regions[r];
            double total = 0;
            for (int i = reg.Start; i < reg.Start + reg.Count; i++)
                total += _net.Recorder.Counts[i];
            double spikes = total - _regionLastCounts[r];
            rates[r] = (float)(spikes / reg.Count); // spikes/neuron in the window
            _regionLastCounts[r] = (uint)total;
        }
        snap.RegionRates = rates;

        // consciousness gauge and feeling journey
        float consciousness = BioComp.Core.Metrics.ConsciousnessMeter.Measure(_net, _idle);
        snap.Consciousness = consciousness;
        snap.ConsciousnessLabel = BioComp.Core.Metrics.ConsciousnessMeter.Label(consciousness);
        if (_net.TickIndex % 200 == 0) // sample every 200ms
        {
            _feelingJourney.Add([_net.TimeMs / 1000f, snap.Valence, snap.Arousal]);
            if (_feelingJourney.Count > 300) _feelingJourney.RemoveAt(0); // ~1 minute of history
        }
        snap.FeelingJourney = [.. _feelingJourney];

        lock (_snapGate) _latest = snap;
    }

    public void Dispose()
    {
        _running = false;
        try { if (_thread.IsAlive) _thread.Join(1000); } catch { }
        _camera.Dispose();
        _mic.Dispose();
        if (_speech is IDisposable d) d.Dispose();
    }
}
