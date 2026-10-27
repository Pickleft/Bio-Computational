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

        // Ambient episodic encoding while awake.
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

        net.Tick(); // the idle loop owns time
    }

    /// <summary>Encode an externally tagged event (person entered, sound, reward...).</summary>
    public Episode? OnEvent(SpikingNetwork net, string[] tags, float valence, float arousal = 0.8f)
    {
        if (arousal >= 0.5f) Sleep.ForceWake(net);
        return net.EpisodeStore.Encode(net, "CA3", tags, valence);
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
