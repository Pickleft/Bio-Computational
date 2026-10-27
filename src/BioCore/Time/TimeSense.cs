using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Time;

/// <summary>
/// Subjective time. A pacemaker integrates simulated milliseconds at a rate modulated
/// by dopamine and noradrenaline (Treisman 1963 internal clock; Meck 1996 - dopamine
/// speeds interval timing), and a population of log-spaced "time cells" decays after
/// each salient event, providing a temporal-context code for now (Howard &amp; Kahana
/// 2002; Kraus et al. 2013). Feelings consume this: boredom, impatience and
/// anticipation are computed over subjective, not wall-clock, intervals.
/// </summary>
public sealed class TimeSense
{
    public const int CellCount = 48;
    public const float MinTauMs = 500f, MaxTauMs = 1_800_000f; // 0.5 s .. 30 min

    public readonly float[] Cells = new float[CellCount];
    private readonly float[] _tauMs = new float[CellCount];

    /// <summary>Integrated subjective time in ms (advances faster under arousal).</summary>
    public double SubjectiveMs { get; private set; }

    /// <summary>Current pacemaker rate (1 = real time).</summary>
    public float ClockRate { get; private set; } = 1f;

    /// <summary>Subjective ms since the last salient event (boredom/impatience input).</summary>
    public double SubjectiveSinceEventMs { get; private set; }

    public TimeSense()
    {
        for (int i = 0; i < CellCount; i++)
        {
            float t = MathF.Exp(MathF.Log(MinTauMs) + (MathF.Log(MaxTauMs) - MathF.Log(MinTauMs)) * i / (CellCount - 1));
            _tauMs[i] = t;
            Cells[i] = 0f;
        }
    }

    public void Tick(SpikingNetwork net)
    {
        float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        float na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        ClockRate = Math.Clamp(1f + 0.6f * (da - NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Dopamine))
                                  + 0.6f * (na - NeuromodulatorSystem.BaselineOf(NeuromodulatorKind.Noradrenaline)), 0.25f, 3f);
        SubjectiveMs += ClockRate;
        SubjectiveSinceEventMs += ClockRate;
        for (int i = 0; i < CellCount; i++)
            Cells[i] *= MathF.Exp(-ClockRate / _tauMs[i]);
    }

    /// <summary>A salient event resets the temporal context.</summary>
    public void OnEvent()
    {
        SubjectiveSinceEventMs = 0;
        Array.Fill(Cells, 1f);
    }

    /// <summary>Snapshot of the current temporal context (for episode encoding).</summary>
    public float[] Context() => (float[])Cells.Clone();
}
