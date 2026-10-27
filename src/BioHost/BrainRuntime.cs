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
    public bool LiveCamera { get; init; }
    public bool LiveMic { get; init; }
    public bool LiveMeta { get; init; }
    public bool DemoMode { get; init; }
    public ulong Seed { get; init; } = 42;
    public static BrainRuntimeConfig Parse(string[] args) => new()
    {
        LiveCamera = args.Contains("--camera"),
        LiveMic = args.Contains("--mic"),
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
    public float[] Modulators { get; set; } = new float[5]; // DA, 5-HT, ACh, NA, OXT
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
    private readonly IFrameSource _camera;
    private readonly IAudioSource _mic;
    private readonly IInteroceptionSource _body;
    private readonly ILanguageModel _lm;
    private readonly ITranscriber _transcriber;
    private readonly ISegmenter _segmenter;
    private readonly IImageGenerator _imager;
    private readonly CostGovernor _cost = new() { DailyBudgetUsd = 20f };
    private readonly MonologueGenerator _monologue;
    private readonly ISpeechOutput _speech;
    private readonly Thread _thread;
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly object _snapGate = new();
    private BrainSnapshot _latest = new();

    private readonly MockFrameSource? _mockCamera;
    private readonly MockAudioSource? _mockMic;
    private readonly int _retinaStart, _retinaCount, _a1Start, _a1Count;
    private readonly List<short> _audioBuffer = [];
    private readonly List<string> _thought = [];
    private readonly List<string> _monologueLog = [];
    private readonly List<object[]> _sleepStrip = [];
    private readonly Queue<(byte[] Png, float Tms)> _pendingSegment = new();
    private byte[]? _lastFramePng;
    private bool _wasVoice;
    private long _voiceCooldownUntil;
    private long _lastDemoCycle = -1;
    private int _lastIgnition, _lastSurprise, _lastReplayCount;
    private BrainState _lastSleepState;
    private double _simSecondsAtSnapshot;
    private volatile bool _running;

    public bool MetaLive { get; private set; }

    public BrainRuntime(BrainRuntimeConfig cfg, ISpeechOutput? speech = null)
    {
        _cfg = cfg;
        var brainCfg = BrainConfig.Default(seed: cfg.Seed);
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

        // senses: live where available, deterministic mocks otherwise
        if (cfg.LiveCamera)
        {
            try { _camera = new BioComp.Senses.OpenCvCameraSource(); }
            catch (Exception ex) { Note($"camera unavailable ({ex.Message}); using mock"); _camera = _mockCamera = new MockFrameSource(62, personAtMs: 100_000); }
        }
        else _camera = _mockCamera = new MockFrameSource(62, personAtMs: 100_000);

        if (cfg.LiveMic)
        {
            try { _mic = new BioComp.Senses.NAudioMicSource(); }
            catch (Exception ex) { Note($"microphone unavailable ({ex.Message}); using mock"); _mic = _mockMic = new MockAudioSource(63, voiceAtMs: 100_000); }
        }
        else _mic = _mockMic = new MockAudioSource(63, voiceAtMs: 100_000);

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
        while (_running)
        {
            double targetSimMs = _wall.Elapsed.TotalMilliseconds;
            // pace: simulate up to wall time, capped catch-up
            double limit = targetSimMs + 50;
            while (_net.TimeMs < Math.Min(targetSimMs, limit))
                StepOnce();

            BuildSnapshot();
            if (_net.TimeMs >= nextCheckpoint)
            {
                CheckpointService.Save(_net, Path.Combine("data", "brain.ckpt"));
                nextCheckpoint = (long)_net.TimeMs + 300_000;
            }
            Thread.Sleep(15);
        }
        CheckpointService.Save(_net, Path.Combine("data", "brain.ckpt"));
    }

    /// <summary>One simulated millisecond: senses in, cognition, actions out.</summary>
    private void StepOnce()
    {
        float t = _net.TimeMs;

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
            }
        }

        // ---- segmentation (async results enter as tagged events) ----
        if (_pendingSegment.TryDequeue(out var seg))
            _ = SegmentAsync(seg.Png, seg.Tms);

        _simSecondsAtSnapshot = _net.TimeMs / 1000.0;
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

    private void Note(string line)
    {
        lock (_snapGate)
        {
            _thought.Add($"[{_net.TimeMs / 1000:F1}s] {line}");
            if (_thought.Count > 60) _thought.RemoveAt(0);
        }
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
            CameraLive = _mockCamera is null,
            MicLive = _mockMic is null,
            MetaLive = MetaLive,
            LastMonologue = _monologueLog.Count > 0 ? _monologueLog[^1] : "",
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

        // raster: last ~600 spikes mapped to [tSec, neuronFrac, region]
        var rec = _net.Recorder;
        int ringLen = rec.Events.Length;
        int n = Math.Min(600, rec.EventCount);
        var raster = new List<float[]>(n);
        for (int k = 1; k <= n; k++)
        {
            int idx = (((rec.EventHead - k) % ringLen) + ringLen) % ringLen;
            var (tMs, neuron) = rec.Events[idx];
            raster.Add([tMs / 1000f, neuron / (float)_net.V.Length, _net.RegionOf[neuron]]);
        }
        snap.Raster = raster;

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
