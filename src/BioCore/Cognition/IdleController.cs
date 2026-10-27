using BioComp.Core.Memory;
using BioComp.Core.Metrics;
using BioComp.Core.Network;
using BioComp.Core.Replay;
using BioComp.Core.Sleep;

namespace BioComp.Core.Cognition;

/// <summary>
/// The idle-time orchestration loop: sleep regulation, episodic encoding, replay and
/// consolidation, DMN/task-positive anti-correlation tracking. The host daemon calls
/// Tick once per network tick; BioLab uses it headless for day simulations.
/// </summary>
public sealed class IdleController
{
    public SleepController Sleep { get; } = new();
    public ReplayScheduler Replay { get; } = new();
    public PopulationCorrelation DmnTaskPosCorrelation { get; } = new();
    public BioComp.Core.Time.TimeSense Time { get; } = new();

    public readonly List<string> EventLog = [];

    /// <summary>Simulated-time interval between ambient episode encodes while awake.</summary>
    public float AmbientEncodeIntervalMs { get; set; } = 30_000f;

    private float _nextAmbientEncodeMs;
    private long _nextCorrelationSample;
    private readonly uint[] _regionWindow;
    private readonly int _dmnIdx, _taskPosIdx;
    private BrainState _lastLogged;

    public IdleController(SpikingNetwork net)
    {
        _regionWindow = new uint[net.Regions.Count];
        _dmnIdx = IndexOf(net, "DMN");
        _taskPosIdx = IndexOf(net, "TaskPos");
        int ca3 = IndexOf(net, "CA3");
        _ca3Start = net.Regions[ca3].Start;
        _ca3Count = net.Regions[ca3].Count;
        _nextAmbientEncodeMs = net.TimeMs + AmbientEncodeIntervalMs;
        _lastLogged = Sleep.State;
    }

    /// <summary>External arousal in 0..1 (surprise input; from M4 the thalamic gate).
    /// Advances the network by one tick at the end of the loop.</summary>
    public void Tick(SpikingNetwork net, float externalArousal = 0f)
    {
        Sleep.Tick(net, externalArousal);
        Replay.Tick(net, Sleep.ReplayAllowed);

        if (Sleep.State != _lastLogged)
        {
            EventLog.Add($"t={net.TimeMs / 1000:F0}s state={Sleep.State} pressure={Sleep.SleepPressure:F2}");
            _lastLogged = Sleep.State;
        }

        // Ambient episodic encoding while awake, plus any pending event encode.
        if (_pendingEvent is { } pending && net.TimeMs >= pending.DueMs)
        {
            net.EpisodeStore.Encode(net, "CA3", pending.Tags, pending.Valence, minMeanRateHz: 0.005f);
            _pendingEvent = null;
        }
        if (net.TimeMs < _noveltyBurstUntil) NoveltyBurst(net);
        if (net.TimeMs >= _nextAmbientEncodeMs && Sleep.State == BrainState.Wake)
        {
            net.EpisodeStore.Encode(net, "CA3", ["ambient"], valence: 0f);
            _nextAmbientEncodeMs = net.TimeMs + AmbientEncodeIntervalMs;
        }

        // Region rate windows for the anti-correlation metric (every 100 ms).
        if (net.TickIndex >= _nextCorrelationSample)
        {
            var counts = net.Recorder.Counts;
            var dmn = net.Regions[_dmnIdx];
            var tp = net.Regions[_taskPosIdx];
            float dmnRate = (RegionTotal(counts, dmn) - _regionWindow[_dmnIdx]) / (float)dmn.Count;
            float tpRate = (RegionTotal(counts, tp) - _regionWindow[_taskPosIdx]) / (float)tp.Count;
            DmnTaskPosCorrelation.AddSample(dmnRate, tpRate);
            _regionWindow[_dmnIdx] = RegionTotal(counts, dmn);
            _regionWindow[_taskPosIdx] = RegionTotal(counts, tp);
            _nextCorrelationSample = net.TickIndex + 100;
        }

        Time.Tick(net); // subjective clock: dopamine and noradrenaline set the pace
        net.Tick(); // the idle loop owns time
    }

    /// <summary>
    /// Register an externally tagged event (person entered, sound, reward...). Wakes the
    /// brain immediately; the episode itself encodes ~250 ms later so sensory activity
    /// has time to propagate into CA3 (memory forms after experience, not at stimulus
    /// onset).
    /// </summary>
    public void OnEvent(SpikingNetwork net, string[] tags, float valence, float arousal = 0.8f)
    {
        if (arousal >= 0.5f)
        {
            Sleep.ForceWake(net);
            _noveltyBurstUntil = net.TimeMs + 60f; // novelty projection into CA3
        }
        Time.OnEvent();
        // the first deadline sticks: repeated arousal frames must not push the encode
        // out past the event they belong to
        _pendingEvent ??= (tags, valence, net.TimeMs + 250f);
    }

    private (string[] Tags, float Valence, float DueMs)? _pendingEvent;
    private float _noveltyBurstUntil = -1f;
    private readonly int _ca3Start, _ca3Count;

    private void NoveltyBurst(SpikingNetwork net)
    {
        // Locus-coeruleus/VTA-style novelty projection into CA3: salient events tag the
        // hippocampus directly, not only via the cortical pathway.
        for (int i = _ca3Start; i < _ca3Start + _ca3Count; i += 3)
            net.InjectCurrent(i, 10f);
    }

    private static uint RegionTotal(uint[] counts, RegionInfo r)
    {
        uint total = 0;
        for (int i = r.Start; i < r.Start + r.Count; i++) total += counts[i];
        return total;
    }

    private static int IndexOf(SpikingNetwork net, string name)
    {
        for (int i = 0; i < net.Regions.Count; i++)
            if (net.Regions[i].Name == name) return i;
        return 0; // fall back to the first region in minimal configs
    }
}
