using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Sleep;

public enum BrainState : byte { Wake, Drowsy, NREM, REM }

/// <summary>
/// Two-process sleep/wake regulation (Borbely 1982 Hum Neurobiol 1:195): a homeostatic
/// sleep pressure S driven by metabolic spending, and a circadian process C. State
/// effects: NREM injects ~0.8 Hz slow oscillations and allows replay with consolidation;
/// REM raises acetylcholine and allows vivid replay (dreams hook, M5+). External arousal
/// (surprise) forces wake. The day length is configurable so a full day can be simulated
/// compressed (`biolab day`).
/// </summary>
public sealed class SleepController
{
    public BrainState State { get; private set; } = BrainState.Wake;
    public float SleepPressure { get; private set; } // 0..1
    public float Circadian { get; private set; }     // 0..1

    /// <summary>Simulated day length; default real 24 h, tests use minutes.</summary>
    public double DayLengthMs { get; set; } = 86_400_000.0;
    public double CircadianPeakHour { get; set; } = 14.0;

    public bool ReplayAllowed => State is BrainState.Drowsy or BrainState.NREM or BrainState.REM;

    public readonly List<(float TimeMs, BrainState State)> Transitions = [];

    private double _stateClockMs;          // time in current NREM/REM cycle phase
    private bool _remPhase;
    private double _nextSlowWaveMs;
    private (float phaseStart, float duration, float amplitude) _slowWave;

    public void Tick(SpikingNetwork net, float externalArousal)
    {
        // Circadian from the simulated clock.
        double hour = (net.TimeMs % DayLengthMs) / DayLengthMs * 24.0;
        Circadian = 0.5f + 0.5f * MathF.Sin((float)(2.0 * Math.PI * (hour - CircadianPeakHour) / 24.0));

        // Homeostat: pressure builds with wake metabolism, drains in sleep.
        // Rates are biological-time; scale them so a compressed simulated day still
        // walks the full wake-sleep cycle.
        double pressureScale = 86_400_000.0 / DayLengthMs;
        float energyRate = Math.Clamp((float)(net.Energy.Spent / MathF.Max(1f, net.TimeMs)), 0f, 0.01f);
        float dPressure = State switch
        {
            BrainState.Wake or BrainState.Drowsy => 0.02f / 60_000f * (1f + 10f * energyRate),
            BrainState.NREM => -0.12f / 60_000f,
            BrainState.REM => -0.03f / 60_000f,
            _ => 0f,
        } * (float)pressureScale;
        SleepPressure = Math.Clamp(SleepPressure + dPressure, 0f, 1f);

        // Arousal (surprise) wakes the brain and holds it awake while sustained.
        if (externalArousal > 0.5f)
        {
            if (State != BrainState.Wake) SetState(net, BrainState.Wake);
            DriveWakeSleepModulators(net);
            return; // homeostatic transitions are suspended under arousal; pressure still accrued above
        }

        // Transitions with hysteresis.
        switch (State)
        {
            case BrainState.Wake when SleepPressure > 0.55f && Circadian < 0.45f:
                SetState(net, BrainState.Drowsy);
                break;
            case BrainState.Drowsy when SleepPressure > 0.65f:
                SetState(net, BrainState.NREM);
                break;
            case BrainState.Drowsy when SleepPressure < 0.5f:
                SetState(net, BrainState.Wake);
                break;
            case BrainState.NREM or BrainState.REM when SleepPressure < 0.20f && Circadian > 0.35f:
                SetState(net, BrainState.Wake);
                break;
            case BrainState.NREM or BrainState.REM:
                // 90-minute ultradian cycle, 70% NREM / 30% REM.
                _stateClockMs += 1;
                double cycle = 90 * 60_000 * (DayLengthMs / 86_400_000.0);
                bool wantRem = (_stateClockMs % cycle) > cycle * 0.70;
                if (wantRem != _remPhase)
                {
                    _remPhase = wantRem;
                    SetState(net, wantRem ? BrainState.REM : BrainState.NREM);
                }
                break;
        }

        DriveWakeSleepModulators(net);
        ApplyStateEffects(net);
    }

    public void ForceWake(SpikingNetwork net) => SetState(net, BrainState.Wake);

    /// <summary>
    /// Wake/sleep neuromodulator drive: histamine is the wake flip-flop
    /// (high in wake, collapses in NREM — Haas et al. 2008); melatonin follows
    /// the circadian oscillator (rises at biological night — Borbely 1982).
    /// </summary>
    private void DriveWakeSleepModulators(SpikingNetwork net)
    {
        var m = net.Modulators;
        // histamine: wake = high, NREM = near-zero, REM = low
        float histTarget = State switch
        {
            BrainState.Wake => 0.60f,
            BrainState.Drowsy => 0.35f,
            BrainState.REM => 0.10f,
            _ => 0.02f, // NREM
        };
        float histCurrent = m.Get(NeuromodulatorKind.Histamine);
        if (histCurrent < histTarget - 0.05f)
            m.Release(NeuromodulatorKind.Histamine, 0.1f);
        else if (histCurrent > histTarget + 0.05f)
            m.Release(NeuromodulatorKind.Histamine, -0.1f);

        // melatonin: inverse of circadian (high when circadian is low = biological night)
        float melTarget = (1f - Circadian) * 0.8f;
        float melCurrent = m.Get(NeuromodulatorKind.Melatonin);
        if (melCurrent < melTarget - 0.05f)
            m.Release(NeuromodulatorKind.Melatonin, 0.05f);
        else if (melCurrent > melTarget + 0.05f)
            m.Release(NeuromodulatorKind.Melatonin, -0.05f);
    }

    private void SetState(SpikingNetwork net, BrainState next)
    {
        if (State == next) return;
        State = next;
        _stateClockMs = 0;
        Transitions.Add((net.TimeMs, next));
        if (next == BrainState.REM)
            net.Modulators.Release(NeuromodulatorKind.Acetylcholine, 1.2f);
        if (next == BrainState.NREM)
            net.Modulators.Release(NeuromodulatorKind.Serotonin, 0.4f);
    }

    /// <summary>NREM slow oscillation: periodic down-then-up current waves across the network.</summary>
    private void ApplyStateEffects(SpikingNetwork net)
    {
        if (State != BrainState.NREM) return;
        if (net.TimeMs >= _nextSlowWaveMs && _slowWave.duration <= 0)
        {
            _slowWave = (net.TimeMs, 420f, 0.9f + 0.2f * (float)net.Rng.NextDouble());
        }
        if (_slowWave.duration > 0)
        {
            float t = net.TimeMs - _slowWave.phaseStart;
            if (t < 220f)
                InjectAll(net, -6f * _slowWave.amplitude);   // down state: hyperpolarizing
            else
                InjectAll(net, 10f * _slowWave.amplitude);    // up state: depolarizing burst
            if (t >= _slowWave.duration)
            {
                _slowWave = default;
                _nextSlowWaveMs = net.TimeMs + 800f + 400f * (float)net.Rng.NextDouble();
            }
        }
    }

    private static void InjectAll(SpikingNetwork net, float picoAmps)
    {
        for (int i = 0; i < net.V.Length; i++) net.InjectCurrent(i, picoAmps);
    }
}
