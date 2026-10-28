using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// Intrinsic motivation from LEARNING PROGRESS (Schmidhuber 1991; Oudeyer and
/// Kaplan 2007), gated by the Goldilocks effect (Kidd et al. 2012): the brain
/// finds reward not in raw novelty but in the DERIVATIVE of its own prediction
/// accuracy - getting better at predicting something feels good. The gate is
/// an inverted-U in novelty: fully predicted input (nothing to learn) and
/// completely unpredictable input (nothing to grasp) both yield nothing; the
/// edge of learnability is where curiosity lives. Dopamine is released
/// proportional to gated progress, which is the intrinsic-reward half of the
/// exploration drive (the extraneous half - social drive - lives elsewhere).
/// Sim-thread only.
/// </summary>
public sealed class CuriosityModule
{
    /// <summary>How much intrinsic reward a unit of learning progress is worth.</summary>
    public float RewardGain { get; set; } = 0.35f;     // CALIBRATE
    /// <summary>Fast EMA window for recent accuracy (seconds).</summary>
    public float FastTauSec { get; set; } = 4f;
    /// <summary>Slow EMA window for the accuracy baseline (seconds).</summary>
    public float SlowTauSec { get; set; } = 40f;

    public float LearningProgress { get; private set; } // -1..1: better/worse at predicting
    public float IntrinsicReward { get; private set; }  // 0..1: gated dopamine-worth
    public float NoveltyGate { get; private set; }      // the inverted-U value

    private float _fast, _slow;
    private bool _primed;
    private float _lastT;

    /// <summary>Feed one prediction-accuracy sample (0 = perfectly wrong, 1 = perfect).</summary>
    public void OnPredictionSample(float accuracy, float novelty, float timeSec)
    {
        if (!_primed) { _fast = _slow = accuracy; _primed = true; _lastT = timeSec; return; }
        float dt = MathF.Max(0.001f, timeSec - _lastT);
        _lastT = timeSec;
        _fast += (accuracy - _fast) * MathF.Min(1f, dt / FastTauSec);
        _slow += (accuracy - _slow) * MathF.Min(1f, dt / SlowTauSec);
        LearningProgress = Math.Clamp(_fast - _slow, -1f, 1f);
        // Goldilocks inverted-U: 4n(1-n) peaks at 1 for n = 0.5
        NoveltyGate = 4f * novelty * (1f - novelty);
        IntrinsicReward = Math.Clamp(MathF.Max(0f, LearningProgress) * RewardGain * NoveltyGate, 0f, 1f);
    }

    /// <summary>Release the intrinsic reward as dopamine. Call once per sim tick.</summary>
    public void Tick(SpikingNetwork net)
    {
        if (IntrinsicReward > 0.05f)
            net.Modulators.Release(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine, IntrinsicReward * 0.3f);
    }
}
