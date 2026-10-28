using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Replay;

/// <summary>
/// Sharp-wave-ripple-style compressed reactivation of stored episodes
/// (Wilson &amp; McNaughton 1994; Foster &amp; Wilson 2006). A replayed code is injected
/// into its region over a short window; a modest dopamine release during replay opens
/// the plasticity gate so co-active traces consolidate (Girardeau et al. 2009).
/// </summary>
public sealed class ReplayScheduler
{
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromMilliseconds(4_000);
    public float ReplayWindowMs { get; set; } = 150f;   // ~15x compression of a ~2 s episode
    public float InjectionPicoAmps { get; set; } = 25f;
    public float DopaminePerReplay { get; set; } = 0.25f;
    /// <summary>Fraction of emotional charge stripped per low-stress replay
    /// (Walker and van der Helm 2009). CALIBRATE.</summary>
    public float EmotionalSoftening { get; set; } = 0.12f;

    public int ReplayCount { get; private set; }
    public Episode? Current { get; private set; }
    public readonly List<(float TimeMs, int EpisodeId, string Tags)> Log = [];

    private float _windowStart = -1;
    private int _nextIdx;
    private long _cooldownUntil;

    /// <summary>Called every network tick; starts and advances replays when allowed.</summary>
    public void Tick(SpikingNetwork net, bool replayAllowed)
    {
        if (Current is not null)
        {
            // Advance: stagger neurons across the compressed window.
            float perNeuronMs = ReplayWindowMs / Current.CodeNeurons.Length;
            int target = (int)((net.TimeMs - _windowStart) / MathF.Max(0.5f, perNeuronMs)) + 1;
            while (_nextIdx < Math.Min(target, Current.CodeNeurons.Length))
            {
                net.InjectCurrent(Current.CodeNeurons[_nextIdx], InjectionPicoAmps);
                _nextIdx++;
            }
            if (net.TimeMs - _windowStart >= ReplayWindowMs)
            {
                Log.Add((net.TimeMs, Current.Id, string.Join(",", Current.Tags)));
                // replaying an emotionally salient memory re-evokes a fraction of its
                // original affect: positive memories tick DA, negative tick 5-HT down
                if (Current.Valence > 0.2f)
                    net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.2f * Current.Valence);
                else if (Current.Valence < -0.2f)
                    net.Modulators.Release(NeuromodulatorKind.Serotonin, -0.15f * Current.Valence);
                // RECONSOLIDATION (Walker and van der Helm 2009): the replayed trace
                // strengthens (consolidation) while its emotional charge softens toward
                // neutral - but only in low-stress sleep: high noradrenaline during
                // sleep preserves the charge (stressed sleep fails to heal)
                float na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
                var touched = net.EpisodeStore.Touch(Current);
                net.EpisodeStore.SoftenValence(touched, EmotionalSoftening * MathF.Max(0f, 1f - na / 1.2f));
                Current = null;
                _cooldownUntil = net.TickIndex + (long)Cooldown.TotalMilliseconds;
                ReplayCount++;
            }
            return;
        }

        if (!replayAllowed) return;
        if (net.TickIndex < _cooldownUntil) return;
        var episode = net.EpisodeStore.PickForReplay(net.Rng.NextInt);
        if (episode is null) return;
        Current = episode;
        _windowStart = net.TimeMs;
        _nextIdx = 0;
        net.Modulators.Release(NeuromodulatorKind.Dopamine, DopaminePerReplay);
    }
}
