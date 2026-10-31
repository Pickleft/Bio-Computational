using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// SOCIAL RHYTHM — the brain learns WHEN its person is typically present and
/// adjusts its receptivity accordingly. Like a dog that waits by the door at
/// the time its owner usually comes home, the brain builds an expectation
/// of social rhythm from experience:
/// - Tracks WHEN interactions happen (time of subjective day)
/// - Builds a rhythm map: which "hours" are socially active vs quiet
/// - Before expected social time: arousal and attention rise (anticipation)
/// - During expected social time: if nobody comes, loneliness hits harder
/// - During unexpected social time: surprise (but welcome surprise)
///
/// This connects to: sleep (the brain gets drowsy during expected-quiet
/// times), curiosity (more exploratory during expected-quiet), loneliness
/// (sharper during expected-social times), and the needs hierarchy (company
/// need is more urgent during expected social periods).
///
/// Sim-thread only.
/// </summary>
public sealed class SocialRhythm
{
    /// <summary>Subjective-hour buckets: 24 bins tracking interaction density.</summary>
    private readonly float[] _hourlyInteraction = new float[24];
    private readonly float[] _hourlySamples = new float[24];
    private int _lastHour = -1;

    /// <summary>How confident the brain is about its rhythm map (0..1).</summary>
    public float Confidence { get; private set; }
    /// <summary>The subjective hour with peak interaction density (0..23).</summary>
    public int PeakHour { get; private set; }
    /// <summary>How socially active the current hour is (0..1 normalized).</summary>
    public float CurrentSocialExpectation { get; private set; }
    /// <summary>Subjective-hour length. Config knob (tests shrink it); not neural state.</summary>
    public float HourMs { get; set; } = 3_600_000f;

    /// <summary>
    /// Call when a social interaction happens. Records the subjective hour.
    /// </summary>
    public void OnInteraction(SpikingNetwork net)
    {
        int hour = (int)((net.TimeMs / HourMs) % 24f); // subjective hours
        if (hour != _lastHour)
        {
            _lastHour = hour;
            // decay all hours slightly (rhythm adapts over time)
            for (int i = 0; i < 24; i++) _hourlyInteraction[i] *= 0.99f;
        }
        _hourlyInteraction[hour] += 1f;
        _hourlySamples[hour] += 1f;

        // confidence grows with total samples
        float total = 0;
        foreach (var s in _hourlySamples) total += s;
        Confidence = MathF.Min(1f, total / 30f);

        // find peak hour
        int peak = 0; float peakVal = 0;
        for (int i = 0; i < 24; i++)
            if (_hourlyInteraction[i] > peakVal) { peakVal = _hourlyInteraction[i]; peak = i; }
        PeakHour = peak;

        // current expectation (normalized)
        float max = MathF.Max(0.001f, peakVal);
        CurrentSocialExpectation = _hourlyInteraction[hour] / max;
    }

    /// <summary>
    /// The rhythm's effect on the brain's state. Called every few seconds.
    /// During expected social times: arousal and attention rise (anticipation).
    /// During expected quiet times: drowsiness increases (it's nap time).
    /// </summary>
    public void Tick(SpikingNetwork net)
    {
        if (Confidence < 0.3f) return; // not enough rhythm data yet
        int hour = (int)((net.TimeMs / HourMs) % 24f);
        float expectation = _hourlyInteraction[hour] / MathF.Max(0.001f, _hourlyInteraction[PeakHour]);
        CurrentSocialExpectation = expectation; // live: reflects the CURRENT hour, not the last-contact hour

        if (expectation > 0.6f)
        {
            // Expected social time: the brain perks up, anticipating company
            net.Modulators.Release(NeuromodulatorKind.Acetylcholine, 0.15f);
            net.Modulators.Release(NeuromodulatorKind.Histamine, 0.05f);
        }
        else if (expectation < 0.2f)
        {
            // Expected quiet time: drowsiness is OK here
            net.Modulators.Release(NeuromodulatorKind.Melatonin, 0.05f);
        }
    }

    /// <summary>The learned interaction bins (for persistence).</summary>
    public float[] SaveBins() => (float[])_hourlyInteraction.Clone();
    /// <summary>The per-bin sample counts (for persistence).</summary>
    public float[] SaveSamples() => (float[])_hourlySamples.Clone();

    /// <summary>Restore the learned rhythm after a restart (identity persistence).</summary>
    public void Restore(float[] hourlyInteraction, float[] hourlySamples)
    {
        if (hourlyInteraction is not { Length: 24 } || hourlySamples is not { Length: 24 }) return;
        for (int i = 0; i < 24; i++) { _hourlyInteraction[i] = hourlyInteraction[i]; _hourlySamples[i] = hourlySamples[i]; }
        RecomputeConfidence();
    }

    private void RecomputeConfidence()
    {
        float total = 0;
        foreach (var s in _hourlySamples) total += s;
        Confidence = MathF.Min(1f, total / 30f);
        int peak = 0; float peakVal = 0;
        for (int i = 0; i < 24; i++)
            if (_hourlyInteraction[i] > peakVal) { peakVal = _hourlyInteraction[i]; peak = i; }
        PeakHour = peak;
    }

    /// <summary>
    /// For the diary and mirror: a description of the brain's social rhythm.
    /// </summary>
    public string Narrative =>
        Confidence < 0.3f ? "still learning when people come"
        : $"expecting people around subjective hour {PeakHour}, " +
          (CurrentSocialExpectation > 0.6f ? "they should be here now"
          : CurrentSocialExpectation < 0.2f ? "this is usually quiet time"
          : "occasionally someone comes at this time");
}
