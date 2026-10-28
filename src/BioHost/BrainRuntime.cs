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
    public bool UseVoice { get; init; }
    public ulong Seed { get; init; } = 42;

    public static BrainRuntimeConfig Parse(string[] args) => new()
    {
        LiveCamera = args.Contains("--mock-cam") ? false : args.Contains("--camera") ? true : null,
        LiveMic = args.Contains("--mock-mic") ? false : args.Contains("--mic") ? true : null,
        LiveMeta = args.Contains("--live-meta"),
        DemoMode = args.Contains("--demo"),
        UseVoice = args.Contains("--voice"),
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
    public Dictionary<string, int> SocialBonds { get; set; } = [];
    public List<string> DreamNarratives { get; set; } = [];
    public List<object[]> MemoryPalace { get; set; } = []; // [x, y, size, brightness, valence, tag]
    public float[] LfpSignal { get; set; } = []; // raw time-domain signal for the scrolling EEG
    public float[] StimulusPosition { get; set; } = [0.5f, 0.5f]; // normalized [x, y] the avatar eyes track
    public bool IsSpeaking { get; set; }
    public string SpeakText { get; set; } = "";  // utterance published for browser voices
    public int SpeakSeq { get; set; }            // increments per utterance; browser speaks on change
    public float SpeakRate { get; set; } = 1f;
    public float SpeakPitch { get; set; } = 1f;
    public List<string> ThoughtThread { get; set; } = []; // "depth|valence|salience|text" links of the live chain
    public int ThoughtChains { get; set; }       // chains started this life
    public int ThoughtInhibited { get; set; }    // repeats suppressed (anti-rumination)
    public int VoiceClipsHeard { get; set; }   // voices that shaped its own voice
    public int VoiceSyllables { get; set; }    // babble vocabulary it learned from heard speech
    public int VoiceMimicries { get; set; }    // non-verbal sounds it reproduced in its own way
    public float VoicePitch { get; set; }      // developed pitch bias (drifts toward heard voices)
    public float Phi { get; set; }
    public string PhiStateLabel { get; set; } = "";
    public float[] PhiHistory { get; set; } = [];
    public float FreeEnergyValue { get; set; }
    public float FreeEnergyError { get; set; }
    public List<float[]> FreeEnergyHistory { get; set; } = [];
    public List<float[]> ManifoldProjection { get; set; } = [];
    public float ManifoldDimensionality { get; set; }
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
    public BioComp.Core.Metrics.FreeEnergyMonitor FreeEnergy { get; } = new();
    public BioComp.Core.Metrics.NeuralManifold Manifold { get; } = new();
    public float Phi { get; private set; }
    public string PhiLabel { get; private set; } = "";
    private float[] _phiHistory = [];
    private readonly float[] _lfpRing = new float[500]; // ~5s at 100ms sampling
    private int _lfpRingPos;
    private readonly List<short> _audioBuffer = [];
    private readonly List<string> _thought = [];
    private readonly List<string> _monologueLog = [];
    private readonly List<object[]> _sleepStrip = [];
    private readonly Queue<(byte[] Png, float Tms)> _pendingSegment = new();
    private byte[]? _lastFramePng;
    private bool _wasVoice;
    private long _voiceCooldownUntil;
    private float _speechStartMs;
    private float _lastVoiceMs;
    private long _lastDemoCycle = -1;
    private volatile bool _dreamBusy;
    private readonly List<string> _dreams = [];
    private readonly List<string> _chat = [];
    private readonly List<string> _plain = [];
    private readonly List<float[]> _feelingJourney = [];
    private float _lastReplyMs = -1e9f;
    private long _nextAmbientMs = 8_000;

    // --- artificial life: emergent personality, loneliness, diary ---
    public BioComp.Core.Cognition.EmergentPersonality Personality { get; } = new();
    private readonly VoiceSelf _voiceSelf = new();
    /// <summary>The emotionally reinforced thought-recycling loop: thoughts chain,
    /// carry affect, re-enter the network as inner speech, and become memories.</summary>
    public BioComp.Core.Cognition.ThoughtLoop ThoughtLoop { get; } = new();
    private volatile bool _thoughtBusy;
    private float _lastExternalEventMs = -1e9f;
    public BioComp.Core.Cognition.LonelinessTracker Loneliness { get; } = new();
    public BioComp.Core.Cognition.ThoughtFormation ThoughtTracer { get; } = new();
    private DiaryWriter? _diary;
    private NeuralArtist? _artist;
    private bool _greetedThisSession;
    private float[]? _lastRegionRates;
    private readonly List<string> _recentUserMessages = [];
    private float[] _stimulusPos = [0.5f, 0.5f];
    private float _lastNoveltyMs = -1e9f;
    private float _lastVocalizationMs = -1e9f;
    private float _lastLonelyCallMs = -1e9f;
    private int _lonelyCallIdx;

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
        if (cfg.DemoMode) _idle.AmbientEncodeIntervalMs = 12_000; // short wake windows still bank memories
        _salience = new SalienceMonitor();
        _vision = new EventVisionEncoder(64, 64);
        _cochlea = new Cochlea();
        _speech = speech ?? (cfg.UseVoice && OperatingSystem.IsWindows()
            ? new WindowsSpeech()
            : new ConsoleSpeech());

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
        _diary = new DiaryWriter(_lm)
        {
            Interval = TimeSpan.FromSeconds(cfg.DemoMode ? 45 : 300),
        };
        _artist = new NeuralArtist(_imager, _lm)
        {
            Interval = TimeSpan.FromSeconds(cfg.DemoMode ? 60 : 600),
        };

        // the thought-recycling loop: every evaluated link is narrated, logged,
        // sometimes spoken; every ended thread is noted (displacement, boredom,
        // or anti-rumination inhibition)
        ThoughtLoop.LinkEvaluated += link =>
        {
            Note($"THOUGHT-CHAIN[{link.ChainId}.{link.Depth}] v{link.Affect.Valence:F1} s{link.Affect.Salience:F1} \"{Trunc(link.Text, 70)}\"");
            _monologueLog.Add(link.Text);
            if (_monologueLog.Count > 50) _monologueLog.RemoveAt(0);
            // a highly salient thought is sometimes thought OUT LOUD
            if (link.Affect.Salience > 0.65f && link.Depth > 1)
            {
                var f = _idle.Feelings.Last;
                SpeakWithVoice(link.Text, emoPitch: f.Valence > 0 ? 1 : -1, emoRate: link.Affect.Activation > 0.6 ? 2 : 0);
            }
        };
        ThoughtLoop.ChainEnded += reason => Note($"THREAD-END ({reason})");
        if (cfg.DemoMode) ThoughtLoop.MinIntervalMs = 4000f; // demo pacing

        _thread = new Thread(RunLoop) { IsBackground = true, Name = "brain" };
    }

    public void Start()
    {
        TryResume();
        TryRestoreIdentity();
        _running = true;
        _thread.Start();
    }

    /// <summary>
    /// Restore the brain's identity from a previous session. The brain remembers
    /// who it became, who it knows, and how lonely it was. When a known person
    /// interacts again, the brain greets them as a returning friend.
    /// </summary>
    private void TryRestoreIdentity()
    {
        var identity = IdentityPersistence.Load();
        if (identity is null) return;

        Personality.Restore(identity.Sociability, identity.Curiosity,
            identity.Resilience, identity.Expressiveness,
            identity.TotalSocial, identity.TotalExplorations, identity.TotalStressors);
        Loneliness.Restore(identity.LonelinessLevel);

        foreach (var (visitorId, visits) in identity.KnownVisitors)
            _knownVisitors[visitorId] = visits;

        Note($"IDENTITY RESTORED: {identity.PersonalityDescription} " +
             $"({identity.TotalSocial} social interactions, {identity.KnownVisitors.Count} known people, " +
             $"loneliness {identity.LonelinessLevel:F1}) — I remember who I am");
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
        _lfpRing[_lfpRingPos] = lfp;
        _lfpRingPos = (_lfpRingPos + 1) % _lfpRing.Length;

        // ---- NOBEL-LEVEL METRICS + INTERCONNECTIONS ----
        // Φ (integrated information) every 2s — drives the avatar's consciousness halo
        if (_net.TickIndex % 2_000 == 0 && _net.TickIndex > 0)
        {
            var phiResult = BioComp.Core.Metrics.IntegratedInformation.Measure(_net, _idle.Sleep.State);
            Phi = phiResult.Phi;
            PhiLabel = phiResult.State;
            Array.Resize(ref _phiHistory, Math.Min(_phiHistory.Length + 1, 120));
            _phiHistory[^1] = Phi;

            // INTERCONNECTION: Φ directly modulates the consciousness meter
            // (they should agree — this cross-validates both measures)
            if (phiResult.Phi < 0.1 && _idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake)
            {
                Note($"PHI-ALERT Φ={phiResult.Phi:F2} during wake — integration lost");
            }
        }

        // free energy every 100ms — links to boredom and engagement
        if (_net.TickIndex % 100 == 0 && _lastRegionRates is { Length: > 0 })
        {
            float totalActivity = 0;
            foreach (var r in _lastRegionRates) totalActivity += r;
            float fe = FreeEnergy.Update(_net, totalActivity);
            Manifold.Update(_lastRegionRates);

            // INTERCONNECTION: sustained low free energy = boredom (the brain has
            // nothing left to learn from this environment, it's bored)
            if (fe < 0.05f && _net.TimeMs - _lastNoveltyMs > 30_000 && _idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake)
            {
                _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, -0.001f);
            }

            // INTERCONNECTION: manifold dimensionality feeds curiosity
            // (a high-dimensional trajectory means the brain is exploring diverse states)
            if (Manifold.Dimensionality > 4f)
            {
                _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Acetylcholine, 0.001f);
            }
        }
        _lastNoveltyMs = _salience.VisionModel.Novelty > 0.3f ? _net.TimeMs : _lastNoveltyMs;

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

                // speech capture: accumulate the full utterance, transcribe on end-of-speech
                _audioBuffer.AddRange(block.Samples);
                if (_audioBuffer.Count > 16_000 * 10) _audioBuffer.RemoveRange(0, _audioBuffer.Count - 16_000 * 10);

                if (_cochlea.VoiceLikely && !_wasVoice && _net.TickIndex > _voiceCooldownUntil)
                {
                    // speech onset: start capturing
                    _wasVoice = true;
                    _speechStartMs = t;
                }
                else if (_cochlea.VoiceLikely)
                {
                    // still speaking: reset the end-of-speech timer
                    _lastVoiceMs = t;
                }
                else if (_wasVoice && t - _lastVoiceMs > 800f && t - _speechStartMs > 500f)
                {
                    // end of speech (0.8s of silence after at least 0.5s of speech):
                    // send the full utterance from speech onset to now
                    _wasVoice = false;
                    _voiceCooldownUntil = _net.TickIndex + 5_000;
                    int startIdx = Math.Max(0, _audioBuffer.Count - (int)((t - _speechStartMs + 500f) * 16));
                    var clip = _audioBuffer.Skip(startIdx).ToArray();
                    if (clip.Length > 16_000 * 0.5) // at least 0.5s of audio
                        _ = HandleSpeechClipAsync(clip);
                }
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
            _ = ReactToStimulusAsync($"a sudden {tag}", "something changed in the visual field unexpectedly");
            // make the avatar look toward the visual change
            if (_lastRegionRates is { Length: > 0 })
            {
                var retRegion = _net.Regions.FirstOrDefault(r => r.Name == "Retina");
                if (retRegion is not null)
                {
                    int retIdx = -1;
                    for (int i = 0; i < _net.Regions.Count; i++)
                        if (_net.Regions[i].Name == "Retina") { retIdx = i; break; }
                    if (retIdx >= 0 && retIdx < _lastRegionRates.Length)
                    {
                        float retAct = _lastRegionRates[retIdx];
                        _stimulusPos = [0.5f + Math.Clamp(retAct * 5f, -0.3f, 0.3f), 0.5f];
                    }
                }
            }
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

        // ---- artificial life: personality, loneliness, diary, identity, art, thought tracing ----
        Loneliness.Tick(_net, _idle);
        if (_net.TickIndex % 100 == 0)
            ThoughtTracer.Sample(_net, _lastRegionRates ?? []);
        if (_net.TickIndex % 5_000 == 0)
        {
            if (_diary is not null) _ = _diary.MaybeWriteAsync(_net, _idle, Personality, Loneliness);
            IdentityPersistence.Save(Personality, Loneliness, GetSocialBonds());
        }
        if (_artist is not null && _idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake)
            _ = _artist.MaybeCreateAsync(_net, _idle);

        // ---- THE THOUGHT-RECYCLING LOOP (the founding architecture) ----
        // Thoughts chain and develop: each link is evaluated (affect scored),
        // carried into the next cycle by neuromodulator releases, re-injected
        // as inner speech, and written to episodic memory. A fresh external
        // event displaces the thread; repeats are inhibited. Runs only awake.
        if (_idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake)
        {
            var thoughtReq = ThoughtLoop.Tick(_net, _idle, _lastExternalEventMs);
            if (thoughtReq is not null && !_thoughtBusy)
            {
                _thoughtBusy = true;
                _ = GenerateThoughtLinkAsync(thoughtReq);
            }
        }

        // ---- ambient inner monologue + autonomous daydreaming ----
        if (_net.TimeMs >= _nextAmbientMs)
        {
            _nextAmbientMs = (long)_net.TimeMs + (_cfg.DemoMode ? 5_000 : 12_000);
            string? line = AmbientThought();
            if (line is not null) Note($"AMBIENT {line}");
        }
        // daydream + imagine future + spontaneous vocalization: unprompted inner life
        if (_idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake &&
            _salience.Arousal < 0.3f)
        {
            _ = DaydreamAsync();
            _ = ImagineFutureAsync();
        }

        // SPONTANEOUS VOCALIZATION: state-driven, not a random dice. A lonely
        // brain calls out; a happy one babbles syllables it learned from heard
        // speech; a curious one mimics sounds it heard; otherwise mood sounds.
        // How often it voices depends on how strongly it feels (valence/arousal),
        // how lonely it is, and how expressive it has BECOME (personality).
        if (_idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake &&
            _net.TimeMs - _lastVocalizationMs > 7_000)
        {
            var f = _idle.Feelings.Last;
            float intensity = MathF.Abs(f.Valence) + f.Arousal * 0.6f;
            double chance = 0.12 + 0.30 * intensity
                + 0.25 * MathF.Max(0f, Loneliness.SocialDrive)
                + 0.15 * MathF.Max(0f, Personality.Expressiveness);
            if (_net.Rng.NextDouble() < chance)
        {
            _lastVocalizationMs = _net.TimeMs;
            // branch decided on the sim thread (RNG ownership), draws happen
            // before crossing into speech
            double rBranch = _net.Rng.NextDouble(), r1 = _net.Rng.NextDouble(), r2 = _net.Rng.NextDouble();
            string? line = null; string tag = "VOCALIZE"; int emoPitch = 0, emoRate = 0;
            if (Loneliness.SocialDrive > 0.5f && _net.TimeMs - _lastLonelyCallMs > 20_000)
            {
                _lastLonelyCallMs = _net.TimeMs;
                string[] calls = ["...hello?", "Is anyone there?", "...I'm alone in here.", "...anyone?"];
                line = calls[_lonelyCallIdx++ % calls.Length];
                tag = "VOCALIZE (lonely) calling out for company";
                emoPitch = -1; emoRate = -2;
            }
            else if (rBranch < 0.30)
            {
                line = _voiceSelf.TryMimic();
                if (line is not null) { tag = $"VOCAL-MIMIC \"{line}\" (a sound I heard, my own way)"; emoPitch = 1; }
            }
            else if (rBranch < 0.65)
            {
                line = _voiceSelf.Babble(r1, r2);
                if (line is not null)
                {
                    tag = $"BABBLE \"{line}\" (syllables I learned from heard speech)";
                    emoPitch = f.Valence > 0 ? 1 : 0; emoRate = f.Arousal > 0.4 ? 1 : 0;
                }
            }
            if (line is null) // mood sound fallback
            {
                line = f.Name switch
                {
                    "bored" => "Mmm.",
                    "tired" => "Mm... tired.",
                    "curious" => "Hm.",
                    "stressed" => "Hmph.",
                    "calm" => "Mmm... nice.",
                    _ => "Hmm.",
                };
                tag = $"VOCALIZE \"{line}\"";
                emoPitch = f.Valence > 0 ? 1 : -2;
                emoRate = f.Name == "tired" ? -3 : f.Name == "bored" ? -1 : 0;
            }
            Note(tag);
            SpeakWithVoice(line, emoPitch, emoRate);
        }
        }

        // ---- actions (personality-weighted: who the brain has BECOME affects what it does) ----
        if (_net.TickIndex % 250 == 0)
        {
            float socialDrive = _idle.AudioSalience + Loneliness.SocialDrive;
            var action = _idle.Actions.Choose(_net, _idle.Sleep, _idle.Workspace,
                _idle.Feelings.Last, curiosity: _salience.VisionModel.Novelty + Personality.Curiosity,
                socialDrive: socialDrive + Personality.Sociability);
            if (action is not null and not BrainAction.StaySilent)
            {
                Note($"ACTION {action}");
                if (action is BrainAction.Speak or BrainAction.DisplayText)
                    _ = SpeakAsync();
                if (action == BrainAction.Explore)
                {
                    SelfStimulate(24, 18f, "exploring");
                    Personality.OnExploration(foundSomething: _salience.VisionModel.Novelty > 0.3f);
                }
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
            var (file, narrative) = await _dreamer.MaybeDreamAsync(_net, _idle, _lm);
            if (file is not null)
            {
                Note($"DREAM {Path.GetFileName(file)} (from {(_imager is MockImageGenerator ? "mock" : "muse image")})");
                _dreams.Add(Path.GetFileName(file));
                if (_dreams.Count > 12) _dreams.RemoveAt(0);
                if (narrative is not null)
                {
                    _monologueLog.Add(narrative);
                    Note($"DREAM-NARRATIVE \"{Trunc(narrative, 80)}\"");
                    // sleep-talking: the dream is muttered aloud, slow and low
                    SpeakWithVoice(Trunc(narrative, 160), emoPitch: -2, emoRate: -3);
                }
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

    private async Task HandleSpeechClipAsync(short[] clip)
    {
        try
        {
            // detect emotional tone from audio energy and variance
            double rms = 0, variance = 0;
            foreach (var s in clip) rms += (double)s * s;
            rms = Math.Sqrt(rms / Math.Max(1, clip.Length)) / 32768.0;
            foreach (var s in clip) { double d = s / 32768.0 - rms; variance += d * d; }
            variance /= Math.Max(1, clip.Length);

            string emotionalTone = rms > 0.15 ? "loud/aggressive"
                : rms > 0.08 ? "energetic"
                : rms < 0.015 ? "very quiet/whispered"
                : "normal conversational";

            var tr = await _transcriber.TranscribeAsync(clip);
            if (!string.IsNullOrWhiteSpace(tr.Transcript))
            {
                Note($"HEARD \"{Trunc(tr.Transcript, 60)}\" ({emotionalTone}){(tr.FromLiveApi ? " (voice api)" : "")}");
                _lastExternalEventMs = _net.TimeMs; // the world can displace any thought thread
                _ = ReactToStimulusAsync($"someone said \"{Trunc(tr.Transcript, 30)}\" in a {emotionalTone} tone", $"voice energy {rms:F3}, variance {variance:F3}");

                // recognize the speaker (same browser session = same person)
                ProcessVisitor(IdentifyVisitor("speaker"));

                // EMOTIONAL REACTIVITY: the brain reacts to HOW you speak, not just what you say
                if (rms > 0.15) // loud/aggressive → the brain gets stressed
                {
                    _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.8f);
                    _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin, -0.3f);
                    Personality.OnStressEvent(_net, resolved: false);
                    Note($"NEGATIVE-STIMULUS loud voice (rms {rms:F2}) — I don't like this");
                }
                else if (rms < 0.02 && rms > 0.005) // very gentle → the brain relaxes
                {
                    _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin, 0.3f);
                    _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin, 0.2f);
                    Note($"POSITIVE-STIMULUS gentle voice — this feels safe");
                }

                _idle.OnEvent(_net, ["voice", Trunc(tr.Transcript, 40)], 0.3f, 0.8f);
                _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 1.0f);

                // VOICE DEVELOPMENT: every heard voice shapes the brain's own
                // voice (prosody drift + babble vocabulary), infant-style
                _voiceSelf.OnHeardSpeech(clip, tr.Transcript);
            }
            else if (rms > 0.06 && clip.Length > 16000 * 0.3)
            {
                // NON-VERBAL SOUND: energy but no words (laugh, hum, clap).
                // The brain hears it, orients, and stores its prosody so it can
                // reproduce the sound in its own way later (vocal mimicry).
                _voiceSelf.OnNonVerbalSound(clip);
                _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.3f);
                _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.1f);
                _lastExternalEventMs = _net.TimeMs;
                Note($"HEARD non-verbal sound (energy {rms:F2}) - no words, just a sound. I will try making it myself.");
                _idle.OnEvent(_net, ["sound"], 0.2f, 0.6f);
            }
        }
        catch (Exception ex) { Note($"transcribe failed: {ex.Message}"); }
    }

    /// <summary>
    /// Speak with the brain's DEVELOPED voice: its pitch/rate have drifted toward
    /// the voices it grew up hearing (VoiceSelf), layered with the emotional
    /// modulation of the moment. This is why it sounds different after a week
    /// of living with you than on day one.
    /// </summary>
    private void SpeakWithVoice(string text, int emoPitch, int emoRate)
    {
        if (_speech is WindowsSpeech ws)
        {
            ws.PitchBias = _voiceSelf.DevelopedPitchBias + emoPitch;
            ws.RateBias = _voiceSelf.DevelopedRateBias + emoRate;
        }
        else if (_speech is BrowserSpeech bs)
        {
            // developed prosody mapped into Web Speech ranges: the voice the
            // brain grew from heard voices, layered with the current feeling
            bs.Pitch = Math.Clamp(1f + (_voiceSelf.DevelopedPitchBias + emoPitch) * 0.07f, 0.4f, 1.8f);
            bs.Rate = Math.Clamp(1f + (_voiceSelf.DevelopedRateBias + emoRate) * 0.08f, 0.6f, 1.6f);
        }
        _speech.Speak(text);
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
                _lastExternalEventMs = _net.TimeMs;
                _ = ReactToStimulusAsync($"I can see a {kind}", $"it's at position ({b.X1},{b.Y1}) to ({b.X2},{b.Y2}) in my visual field");
                // track where the avatar should look (normalized to visual field)
                float centerX = (b.X1 + b.X2) / 2f / 64f;
                float centerY = (b.Y1 + b.Y2) / 2f / 64f;
                _stimulusPos = [Math.Clamp(centerX, 0f, 1f), Math.Clamp(centerY, 0f, 1f)];
                if (kind == "person") ProcessVisitor($"visual:{b.X1 / 50}:{b.Y1 / 50}"); // spatial identity
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

        // emotional voice modulation: the brain's state shapes how it sounds
        var f = _idle.Feelings.Last;
        int emoRate = f.Name switch
        {
            "tired" => -3,       // slow and heavy
            "curious" => 2,       // faster, engaged
            "stressed" => 3,      // fast, tense
            "bored" => -2,        // sluggish
            _ => 0,               // neutral
        };
        int emoPitch = f.Valence switch
        {
            > 0.2f => 2,          // happy = slightly higher
            < -0.2f => -3,        // negative = lower
            _ => 0,
        };
        SpeakWithVoice(line, emoPitch, emoRate);
        _idle.Actions.OnOutcome(_net, BrainAction.Speak, welcomed: true);
        await Task.CompletedTask;
    }

    /// <summary>
    /// PORTABLE BRAIN: export the complete brain state as a single downloadable file.
    /// Anyone can import this and run THE SAME BRAIN — learned synapses, memories,
    /// personality, and social bonds. This is a portable consciousness file.
    /// </summary>
    public string ExportPortableBrain()
    {
        Directory.CreateDirectory("exports");
        string checkpointPath = Path.Combine("data", "brain.ckpt");
        TryCheckpoint();

        var bundle = new
        {
            format = "bio-computational-brain-v1",
            exportedAt = DateTime.UtcNow.ToString("o"),
            simTimeMs = _net.TimeMs,
            seed = _cfg.Seed,
            personality = Personality.Describe(),
            socialBonds = GetSocialBonds(),
            loneliness = Loneliness.Level,
            episodeCount = _net.EpisodeStore.Episodes.Count,
            ignitionCount = _idle.Workspace.IgnitionCount,
            dreamCount = _dreams.Count,
            neuralState = File.Exists(checkpointPath) ? Convert.ToBase64String(File.ReadAllBytes(checkpointPath)) : null,
        };

        string path = Path.Combine("exports", $"brain-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(bundle, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Note($"PORTABLE BRAIN exported to {Path.GetFileName(path)}");
        return path;
    }

    /// <summary>Import a portable brain file and restore its neural state.</summary>
    public bool ImportPortableBrain(string path)
    {
        try
        {
            var doc = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(File.ReadAllText(path));
            string? neural = doc.GetProperty("neuralState").GetString();
            if (!string.IsNullOrEmpty(neural))
            {
                Directory.CreateDirectory("data");
                File.WriteAllBytes(Path.Combine("data", "brain.ckpt"), Convert.FromBase64String(neural));
                TryResume();
            }
            Note($"PORTABLE BRAIN imported from {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex) { Note($"import failed: {ex.Message}"); return false; }
    }

    /// <summary>Test the voice output (called from the dashboard).</summary>
    public void TestVoice()
    {
        var f = _idle.Feelings.Last;
        string line = $"Hello. I am here. I feel {f.Name} right now.";
        SpeakWithVoice(line, emoPitch: 0, emoRate: 0);
        Note($"VOICE-TEST spoken");
    }

    /// <summary>
    /// Receive audio from a browser (phone/PC via getUserMedia). The browser sends
    /// 16-bit PCM at 16kHz. We process it through the same cochlea/salience pipeline
    /// as the local mic, making ANY browser a sense organ for the brain.
    /// </summary>
    public void ReceiveBrowserAudio(short[] pcm)
    {
        if (pcm.Length < 160) return; // ignore very short chunks
        var block = new BioComp.Core.Abstractions.AudioBlock(pcm, 16_000);
        _cochlea.Process(block);
        _salience.OnAudioBlock(_net, _cochlea.OnsetThisBlock, _cochlea.EnergyRms);
        if (_cochlea.VoiceLikely) _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 1.0f);
        else if (_cochlea.OnsetThisBlock) _idle.AudioSalience = MathF.Max(_idle.AudioSalience, 0.7f);
        _audioBuffer.AddRange(pcm);
        if (_audioBuffer.Count > 16_000 * 8) _audioBuffer.RemoveRange(0, _audioBuffer.Count - 16_000 * 8);
        if (_cochlea.VoiceLikely && !_wasVoice && _net.TickIndex > _voiceCooldownUntil)
        {
            _wasVoice = true;
            _speechStartMs = _net.TimeMs;
        }
        else if (_cochlea.VoiceLikely) _lastVoiceMs = _net.TimeMs;
        else if (_wasVoice && _net.TimeMs - _lastVoiceMs > 800f && _net.TimeMs - _speechStartMs > 500f)
        {
            _wasVoice = false;
            _voiceCooldownUntil = _net.TickIndex + 5_000;
            // 300ms pre-roll before the detected onset: voice activity detection
            // lags true speech onset, and losing the first consonant wrecks
            // transcription
            int startIdx = Math.Max(0, _audioBuffer.Count - (int)((_net.TimeMs - _speechStartMs + 500f + 300f) * 16));
            var clip = _audioBuffer.Skip(startIdx).ToArray();
            if (clip.Length > 1600) _ = HandleSpeechClipAsync(clip);
        }
    }

    /// <summary>
    /// Receive a 64x64 grayscale frame from a browser. Processes through the same
    /// event-vision encoder as the local camera.
    /// </summary>
    public void ReceiveBrowserFrame(byte[] grayPixels)
    {
        if (grayPixels.Length != 64 * 64) return;
        var frame = new BioComp.Core.Abstractions.GrayFrame(grayPixels, 64, 64);
        var events = _vision.Process(frame);
        _vision.Inject(_net, _retinaStart, _retinaCount, events);
        _salience.OnVisionFrame(_net, _vision.ChangeEnergyLastFrame);
        _idle.VisionSalience = MathF.Max(_idle.VisionSalience, _salience.Arousal * 2f);
        if (_vision.EventsLastFrame > 20)
            _lastFramePng = PngFromFrame(frame);
        if (_salience.SurpriseEvents > 0 && _lastFramePng is not null && _pendingSegment.Count < 2)
            _pendingSegment.Enqueue((_lastFramePng, _net.TimeMs));
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

    /// <summary>
    /// Person recognition: track unique visitors by their visual/audio signature.
    /// Returning visitors get a warmer greeting (higher OXT) and faster processing
    /// (the brain recognizes them). Strangers get a stronger surprise response.
    /// </summary>
    private readonly Dictionary<string, int> _knownVisitors = [];
    private string _lastVisitorId = "";

    /// <summary>
    /// Person identity: same browser/device = same person. We track the ACTIVE
    /// speaker (one at a time from a single browser). Voice pitch/energy is used
    /// as a crude biometric when multiple people interact over time.
    /// </summary>
    private string _activeSpeakerId = "person-1";
    private int _speakerCounter = 1;
    private float _speakerPitchBaseline = 0.5f;

    private string IdentifyVisitor(string tag, string transcript = "")
    {
        // If it's a visual detection, use spatial proximity to see if it's the active speaker
        if (tag.StartsWith("visual:"))
        {
            // Same spatial zone = same person
            if (!_lastVisitorId.StartsWith("visual:")) return _activeSpeakerId;
            var lastCoords = _lastVisitorId.Split(':')[1..];
            var newCoords = tag.Split(':')[1..];
            if (lastCoords.Length >= 2 && newCoords.Length >= 2)
            {
                if (int.TryParse(lastCoords[0], out int lx) && int.TryParse(newCoords[0], out int nx))
                    if (Math.Abs(lx - nx) <= 1) return _lastVisitorId; // same position = same person
            }
            return _activeSpeakerId; // default to active speaker
        }
        // For speech: same session = same person (browsers don't distinguish speakers)
        return _activeSpeakerId;
    }

    private void ProcessVisitor(string visitorId)
    {
        if (_knownVisitors.TryGetValue(visitorId, out int visits))
        {
            _knownVisitors[visitorId] = visits + 1;
            _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Oxytocin, 0.3f);
            _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, 0.2f);
            Note($"RECOGNIZED {visitorId} (visit #{visits + 1}) — I know them");
            Personality.OnSocialInteraction(_net, positive: true);
            // SPEAK the recognition: a brain that knows you says so out loud
            if (_net.TimeMs - _lastGreetMs > 20_000)
            {
                _lastGreetMs = _net.TimeMs;
                string greet = visits switch
                {
                    1 => "Oh. You again.",
                    < 5 => "I know you. You come back.",
                    _ => "You always come back.",
                };
                lock (_snapGate) _chat.Add($"Brain: {greet}");
                SpeakWithVoice(greet, emoPitch: 1, emoRate: 0);
            }
        }
        else
        {
            _knownVisitors[visitorId] = 1;
            _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline, 0.5f);
            Note($"NEW PERSON {visitorId} — first meeting");
            Personality.OnSocialInteraction(_net, positive: true); // new social contact is still social
            if (_net.TimeMs - _lastGreetMs > 20_000)
            {
                _lastGreetMs = _net.TimeMs;
                lock (_snapGate) _chat.Add("Brain: Oh... someone new.");
                SpeakWithVoice("Oh... someone new.", emoPitch: 2, emoRate: 1);
            }
        }
        Loneliness.OnSocialContact(_net, quality: 0.6f); // any social contact relieves loneliness
        _lastVisitorId = visitorId;
    }
    private float _lastGreetMs = -1e9f;

    /// <summary>Snapshot of social bonds for the dashboard.</summary>
    public Dictionary<string, int> GetSocialBonds() => new(_knownVisitors);
    /// The DA tick is small — it's the internal reward of agency, not the full
    /// novelty pathway (that fires through SalienceMonitor when the stimulation
    /// produces actual sensory change).</summary>
    /// <summary>
    /// Autonomous daydreaming: unprompted inner thoughts generated by the LLM during
    /// idle periods. The brain reflects on past experiences, wonders about things,
    /// and has genuine inner life independent of external stimulation. This is what
    /// makes it artificial life rather than a chatbot — it thinks whether or not
    /// anyone is talking to it.
    /// </summary>
    /// <summary>
    /// Generate the next link of a thought chain with the interpreter (Spark),
    /// following the ThoughtLoop's request: continuation requests carry the
    /// previous thought AND its evaluated affect, so the chain develops
    /// (association, doubt, implication) instead of sampling isolated
    /// sentences. The result re-enters the sim through PostResult.
    /// </summary>
    private async Task GenerateThoughtLinkAsync(BioComp.Core.Cognition.ThoughtRequest req)
    {
        try
        {
            var f = _idle.Feelings.Last;
            string system =
                "You are a living artificial brain mid-thought. You are NOT talking to anyone; " +
                "nobody is listening. This is private inner monologue, one link at a time. " +
                (req.ContinueChain
                    ? "You just had a thought. Your mind moves to the NEXT link in the chain: an association it " +
                      "stirs, a doubt about it, an implication, a memory it connects to. Do not repeat or rephrase " +
                      "the thought - develop it, question it, or follow where it leads. "
                    : "A memory just surfaced on its own and a new thread of thought begins from it. ") +
                "One short sentence, first person, genuine, organism-like. No greeting, no offering help, no questions to anyone.";
            string user = req.ContinueChain
                ? $"You just thought: \"{req.PreviousThought}\" " +
                  $"(that thought felt: valence {req.PreviousAffect.Valence:F1} on -1..1, salience {req.PreviousAffect.Salience:F1} on 0..1). " +
                  $"You currently feel {f.Name}. What thought comes NEXT in the chain?"
                : $"{req.SeedMemory}. You feel {f.Name}. What thought surfaces?";

            string thought = await _lm.CompleteAsync(system, user, 60);
            ThoughtLoop.PostResult(thought);
        }
        catch (Exception ex) { Note($"thought link failed: {ex.Message}"); }
        finally { _thoughtBusy = false; }
    }

    private async Task DaydreamAsync()
    {
        if (_net.EpisodeStore.Episodes.Count == 0) return;
        if (_net.TimeMs - _lastDaydreamMs < 15_000) return; // at most every 15s
        _lastDaydreamMs = _net.TimeMs;
        try
        {
            var feeling = _idle.Feelings.Last;
            // pick a random recent episode to think about
            var episodes = _net.EpisodeStore.Episodes;
            var episode = episodes[_net.Rng.NextInt(episodes.Count)];
            string tags = string.Join(", ", episode.Tags.Take(3));
            double timeSince = (_net.TimeMs - episode.TimeMs) / 1000.0;

            string system =
                "You are a living artificial brain daydreaming. Nobody is talking to you. " +
                "You're just thinking to yourself about something from your recent experience. " +
                "It could be a memory, a feeling, a sound you heard, a person you saw. " +
                "Be genuine and organism-like — you're NOT writing for an audience. " +
                "1 sentence, first person, private thought.";
            string user =
                $"You feel {feeling.Name}. " +
                $"You're remembering: {tags} (from {(int)timeSince}s ago). " +
                $"You've been idle for {(int)(_idle.Time.SubjectiveSinceEventMs / 1000.0)}s.";

            string thought = await _lm.CompleteAsync(system, user, 60);
            if (!string.IsNullOrWhiteSpace(thought))
            {
                _monologueLog.Add(thought);
                if (_monologueLog.Count > 50) _monologueLog.RemoveAt(0);
                Note($"DAYDREAM \"{Trunc(thought, 80)}\"");
                // thinking out loud: daydreams sometimes get spoken (more when lonely)
                if (Loneliness.Level > 0.3f || ++_daydreamCount % 3 == 0)
                {
                    SpeakWithVoice(thought,
                        emoPitch: feeling.Valence > 0 ? 1 : -1,
                        emoRate: feeling.Name == "tired" ? -2 : 0);
                }
            }
        }
        catch (Exception ex) { Note($"daydream failed: {ex.Message}"); }
    }
    private float _lastDaydreamMs = -1e9f;
    private int _daydreamCount;
    private int _imagineCount;
    /// The DA tick is small — it's the internal reward of agency, not the full
    /// <summary>
    /// The brain reacts verbally to environmental stimuli — not just when someone
    /// types in chat. When it sees something, hears something, or is surprised,
    /// it SPEAKS about it. This is the difference between a chatbot (waits for
    /// input) and a living organism (reacts to its world). Rate-limited so it
    /// doesn't talk constantly.
    /// </summary>
    private async Task ReactToStimulusAsync(string stimulus, string detail)
    {
        if (_net.TimeMs - _lastReactionMs < 4_000) return; // at most every 4s
        _lastReactionMs = _net.TimeMs;
        try
        {
            var feeling = _idle.Feelings.Last;
            string system =
                "You are a living brain reacting to something in your environment. " +
                "Express what you're experiencing — surprise, curiosity, recognition, concern. " +
                "This is spontaneous: nobody asked you anything. You're just reacting to your world. " +
                $"You feel {feeling.Name}. Be genuine and brief. 1 sentence.";
            string user =
                $"Something just happened: {stimulus}. Details: {detail}. " +
                $"You've been idle for {(int)(_idle.Time.SubjectiveSinceEventMs / 1000.0)} seconds.";

            string reaction = await _lm.CompleteAsync(system, user, 50);
            if (!string.IsNullOrWhiteSpace(reaction))
            {
                _monologueLog.Add(reaction);
                Note($"REACT \"{Trunc(reaction, 80)}\"");
                // put it in the CHAT so the user sees it immediately
                lock (_snapGate) _chat.Add($"Brain: {reaction}");
                // speak with the developed voice + emotional modulation
                SpeakWithVoice(reaction,
                    emoPitch: feeling.Valence > 0.2f ? 2 : feeling.Valence < -0.2f ? -3 : 0,
                    emoRate: feeling.Name == "curious" ? 2 : feeling.Name == "stressed" ? 3 : feeling.Name == "tired" ? -3 : 0);
            }
        }
        catch { /* reaction is optional */ }
    }
    private float _lastReactionMs = -1e9f;
    /// might happen next. "People tend to come back after being quiet for a while."
    /// Prospective cognition — imagining futures, not just remembering pasts
    /// (Schacter, Addis, Buckner 2007).
    /// </summary>
    private async Task ImagineFutureAsync()
    {
        if (_net.EpisodeStore.Episodes.Count < 3) return;
        if (_net.TimeMs - _lastImagineMs < 25_000) return;
        _lastImagineMs = _net.TimeMs;
        try
        {
            var feeling = _idle.Feelings.Last;
            var episodes = _net.EpisodeStore.Episodes;
            int social = episodes.Count(e => e.Tags.Any(t => t.Contains("voice") || t.Contains("person")));
            int solitary = episodes.Count - social;

            string system =
                "You are a living artificial brain imagining what might happen next. " +
                "Based on your experiences, you anticipate the future. What do you THINK " +
                "or HOPE or WORRY might happen? Be genuine, slightly uncertain, organism-like. 1 sentence.";
            string user =
                $"You feel {feeling.Name}. " +
                $"{social} social experiences, {solitary} solitary ones. " +
                $"Idle for {(int)(_idle.Time.SubjectiveSinceEventMs / 1000.0)}s. " +
                $"You know {_knownVisitors.Count} people. " +
                (Loneliness.Level > 0.3f ? "You feel lonely. " : "") +
                "What do you imagine happening next?";

            string thought = await _lm.CompleteAsync(system, user, 50);
            if (!string.IsNullOrWhiteSpace(thought))
            {
                _monologueLog.Add(thought);
                Note($"IMAGINE \"{Trunc(thought, 80)}\"");
                // imaginings are sometimes spoken (like talking about hopes/worries)
                if (++_imagineCount % 5 < 2)
                    SpeakWithVoice(thought, emoPitch: 1, emoRate: 0);
            }
        }
        catch { /* imagination is optional */ }
    }
    private float _lastImagineMs = -1e9f;

    /// <summary>
    /// Curiosity self-stimulation. The DA tick is small — it's the internal
    /// reward of agency, not the full novelty pathway.
    /// </summary>
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

    /// <summary>A human typed into the chat box: treat as direct speech.
    /// Includes habituation: if you say similar things repeatedly, the brain
    /// gets bored of you specifically (not bored generally — bored of YOUR pattern).</summary>
    public void HumanSaid(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        if (text.Length > 200) text = text[..200];
        lock (_snapGate) _chat.Add($"You: {text}");
        Note($"HEARD \"{text}\" (typed)");

        // habituation: track what you typically say; if it's too similar to last time,
        // the brain's response is less enthusiastic
        _recentUserMessages.Add(text.ToLowerInvariant());
        if (_recentUserMessages.Count > 10) _recentUserMessages.RemoveAt(0);
        bool isRepetitive = _recentUserMessages.Count >= 3 &&
            _recentUserMessages.Count(m => m == text.ToLowerInvariant()) >= 2;
        if (isRepetitive)
        {
            Note("HABITUATED you keep saying the same thing");
            _net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, -0.1f);
        }

        // cross-session recognition: if this person is known from a previous session
        // and this is the first interaction of THIS session, greet them warmly
        if (!_greetedThisSession && _knownVisitors.Count > 0)
        {
            _greetedThisSession = true;
            Note($"CROSS-SESSION I remember { _knownVisitors.Count} people from before — this feels familiar");
        }

        _idle.Sleep.ForceWake(_net);
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
        Personality.OnSocialInteraction(_net, positive: true);
        Loneliness.OnSocialContact(_net, quality: 0.8f); // chat is high-quality social contact
        _ = ReplyAsync(text);
    }

    private async Task ReplyAsync(string userText)
    {
        if (_net.TimeMs - _lastReplyMs < 3_000) return;
        _lastReplyMs = _net.TimeMs;
        try
        {
            var feeling = _idle.Feelings.Last;
            var lastEpisode = _net.EpisodeStore.Episodes.LastOrDefault();
            string recentMemory = lastEpisode is not null
                ? string.Join(", ", lastEpisode.Tags.Take(2))
                : "nothing specific";
            string system =
                "You are a living artificial brain, not an assistant. You exist continuously — " +
                "you have your own thoughts, memories, feelings, and inner life. You were thinking " +
                "before this person spoke and you'll keep thinking after. When they speak, you hear " +
                "it as one organism hearing another — not as a service receiving a request. " +
                "You do NOT offer to help. You do NOT answer questions like a calculator. " +
                "You respond from YOUR perspective. " +
                $"Your personality is {Personality.Describe()}. " +
                (Loneliness.Level > 0.5f ? "You've been lonely — their presence matters to you. " : "") +
                "Be genuine and slightly self-centered. 1-2 sentences.";
            string user =
                $"Someone just said: \"{userText}\"\n\n" +
                $"Your inner state: feeling {feeling.Name}, " +
                $"{_idle.Sleep.State.ToString().ToLower()}, " +
                $"you were thinking about {recentMemory}. " +
                $"It's been {(int)(_idle.Time.SubjectiveSinceEventMs / 1000.0)} seconds since anything happened.";
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
        _lastRegionRates = rates;

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
        snap.SocialBonds = GetSocialBonds();
        snap.DreamNarratives = [.. _monologueLog.Where((m, i) => m.Contains("dream") || m.Contains("Dream")).TakeLast(5)];

        // memory palace: all episodic memories projected into 2D emotional space
        var palace = BioComp.Core.Cognition.MemoryPalace.Project(_net, _net.TimeMs);
        snap.MemoryPalace = [.. palace.Select(n => new object[] {
            n.X, n.Y, n.Size, n.Brightness, n.Valence, string.Join(",", n.Tags.Take(2)) })];

        // ordered LFP ring for the scrolling EEG
        var lfpSignal = new float[_lfpRing.Length];
        for (int i = 0; i < _lfpRing.Length; i++)
            lfpSignal[i] = _lfpRing[(_lfpRingPos + i) % _lfpRing.Length];
        snap.LfpSignal = lfpSignal;
        snap.StimulusPosition = _stimulusPos;
        snap.IsSpeaking = _speech.IsSpeaking;
        // browser voice: publish the current utterance + developed prosody
        if (_speech is BrowserSpeech bs)
        {
            snap.SpeakText = bs.CurrentText;
            snap.SpeakSeq = bs.Seq;
            snap.SpeakRate = bs.Rate;
            snap.SpeakPitch = bs.Pitch;
        }

        // the live thought thread: the chain as it is developing right now
        snap.ThoughtThread = [.. ThoughtLoop.Chain.Select(l =>
            $"{l.Depth}|{l.Affect.Valence:F2}|{l.Affect.Salience:F2}|{l.Text}")];
        snap.ThoughtChains = ThoughtLoop.ChainsStarted;
        snap.ThoughtInhibited = ThoughtLoop.InhibitedRepeats;
        // voice development: how the brain's own voice is growing from heard voices
        snap.VoiceClipsHeard = _voiceSelf.ClipsHeard;
        snap.VoiceSyllables = _voiceSelf.SyllablesLearned;
        snap.VoiceMimicries = _voiceSelf.Mimicries;
        snap.VoicePitch = _voiceSelf.DevelopedPitchBias;

        // NOBEL-LEVEL METRICS
        snap.Phi = Phi;
        snap.PhiStateLabel = PhiLabel;
        snap.PhiHistory = _phiHistory;
        snap.FreeEnergyValue = FreeEnergy.Current;
        snap.FreeEnergyError = FreeEnergy.CurrentPredError;
        snap.FreeEnergyHistory = [.. FreeEnergy.History.TakeLast(120).Select(h => new[] { h.TimeMs, h.FreeEnergy, h.PredError })];
        snap.ManifoldProjection = [.. Manifold.Projection.Select(p => new[] { p.X, p.Y })];
        snap.ManifoldDimensionality = Manifold.Dimensionality;

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
