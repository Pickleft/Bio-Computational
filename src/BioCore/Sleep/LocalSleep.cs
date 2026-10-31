using BioComp.Core.Network;

namespace BioComp.Core.Sleep;

/// <summary>
/// LOCAL SLEEP (Vyazovskiy et al. 2011, Nature 472:443 — "local sleep in
/// awake rats"): past a sleep-pressure threshold, individual cortical
/// regions briefly enter OFF states — neuronal silence — while the animal
/// stays behaviorally awake. Sleep is not globally all-or-none; it invades
/// piecemeal, and the invaded regions perform like sleep while the rest of
/// the brain performs like wake. This is why sleep deprivation degrades
/// cognition BEFORE it knocks you out.
///
/// Mechanism: pressure above ~0.55 gives each region a per-second chance of
/// an OFF window (200-600 ms): RegionalOffBiasMv[region] = 6 mV (silencing
/// hyperpolarization, same channel as anesthesia but region-scoped). The
/// region's rate measurably collapses while global state stays Wake.
///
/// Connects to: sleep pressure (the driver), thought content (20% of OFF
/// windows leak dream-ish imagery into wake — the microsleep intrusion),
/// needs (a locally-sleeping brain needs rest more), and the dashboard's
/// brain map (the OFF region visibly goes dark while the label says awake).
///
/// Sim-thread only.
/// </summary>
public sealed class LocalSleep
{
    /// <summary>Pressure above which OFF states begin. // CALIBRATE</summary>
    public float PressureThreshold { get; set; } = 0.55f;
    /// <summary>OFF-state hyperpolarization (mV) — enough to silence a region.</summary>
    public float OffBiasMv { get; set; } = 6f;
    /// <summary>OFF windows per region per simulated second at pressure 1.0. // CALIBRATE</summary>
    public float RatePerSecondAtFullPressure { get; set; } = 0.35f;

    public int OffEvents { get; private set; }
    /// <summary>Restore the OFF-state count after a restart.</summary>
    public void Restore(int events) => OffEvents = events;
    /// <summary>Region index currently OFF (-1 = none). One at a time: local, not global.</summary>
    public int RegionOff { get; private set; } = -1;
    /// <summary>The last OFF window leaked dream imagery into wake.</summary>
    public bool LastWindowLeaked { get; private set; }

    private float _offUntilMs = -1e9f;
    private float _nextEvalMs;

    /// <summary>Call every tick; decides OFF windows. rngDraw is a seeded uniform draw.</summary>
    public void Tick(SpikingNetwork net, BrainState state, Func<double> rngDraw)
    {
        // clear an expired window
        if (RegionOff >= 0 && net.TimeMs >= _offUntilMs)
        {
            net.RegionalOffBiasMv[RegionOff] = 0f;
            RegionOff = -1;
        }
        if (net.TimeMs < _nextEvalMs) return;
        _nextEvalMs = net.TimeMs + 1000f;

        // OFF states are a WAKE phenomenon: once truly asleep, the whole brain sleeps
        if (state != BrainState.Wake) return;
        float over = SleepPressureOf(net) - PressureThreshold;
        if (over <= 0f || RegionOff >= 0) return;

        // per-region lottery; stronger thalamo-cortical regions go first is NOT
        // claimed — which region drops out is chance weighted by nothing (honest)
        float p = MathF.Min(0.5f, over * RatePerSecondAtFullPressure / MathF.Max(1, net.Regions.Count / 8f));
        if (rngDraw() < p)
        {
            int region = (int)(rngDraw() * net.Regions.Count) % net.Regions.Count;
            RegionOff = region;
            float dur = 200f + (float)(rngDraw() * 400f);
            _offUntilMs = net.TimeMs + dur;
            net.RegionalOffBiasMv[region] = OffBiasMv;
            OffEvents++;
            LastWindowLeaked = rngDraw() < 0.2;   // microsleep intrusion: dream leaks in
        }
    }

    /// <summary>Pressure is read from the modulator/energy substrate via the caller's
    /// sleep controller; kept as a delegate so Core.Sleep stays decoupled.</summary>
    public Func<SpikingNetwork, float> PressureReader { get; set; } = _ => 0f;

    private float SleepPressureOf(SpikingNetwork net) => PressureReader(net);

    /// <summary>For the diary.</summary>
    public string Narrative => OffEvents == 0
        ? "slept before any region went offline"
        : $"{OffEvents} times a region of it fell asleep while it stayed awake";
}
