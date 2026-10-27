namespace BioComp.Core.Senses;

/// <summary>
/// One-channel predictive-coding model (Rao &amp; Ballard 1999): a fast EMA prediction,
/// a slow EMA context, and a running error variance for precision weighting. Novelty is
/// a decaying trace of standardized prediction error, so repeated stimuli habituate and
/// genuinely new input dishabituates automatically.
/// </summary>
public sealed class PredictiveModel
{
    public float FastTauMs { get; set; } = 1_000f;
    public float SlowTauMs { get; set; } = 30_000f;
    public float NoveltyTauMs { get; set; } = 2_000f;

    /// <summary>
    /// State-dependent novelty decay: when arousal is high, novelty persists longer
    /// (the LC-NA system modulates sensory processing depth — engaged brains hold
    /// curiosity; disengaged brains drop it fast). Call SetEngagement before Observe.
    /// </summary>
    public float Engagement { get; set; } = 0f; // 0=disengaged, 1=fully engaged

    private float EffectiveNoveltyTau =>
        NoveltyTauMs + Engagement * 12_000f; // 2s disengaged → 14s fully engaged

    public float Prediction { get; private set; }
    public float SlowContext { get; private set; }
    public float ErrorStd { get; private set; } = 0.05f;
    public float Novelty { get; private set; } // decaying surprise trace, roughly 0..3+
    public float LastSurprise { get; private set; } // instantaneous standardized error

    private float _fastDecay, _slowDecay, _noveltyDecay, _varDecay;

    public PredictiveModel()
    {
        UpdateTaus();
    }

    public void UpdateTaus()
    {
        _fastDecay = MathF.Exp(-1f / FastTauMs);
        _slowDecay = MathF.Exp(-1f / SlowTauMs);
        _varDecay = MathF.Exp(-1f / 5_000f);
    }

    /// <summary>Feed one observation at 1 ms cadence; returns the standardized surprise.</summary>
    public float Observe(float x)
    {
        // recompute novelty decay each call (it depends on engagement level)
        _noveltyDecay = MathF.Exp(-1f / EffectiveNoveltyTau);

        float err = MathF.Abs(x - Prediction);
        ErrorStd = MathF.Sqrt(ErrorStd * ErrorStd * _varDecay + err * err * (1f - _varDecay));
        float surprise = err / MathF.Max(ErrorStd, 1e-4f); // floor guards division, not the estimate
        LastSurprise = surprise;
        Prediction = Prediction * _fastDecay + x * (1f - _fastDecay);
        SlowContext = SlowContext * _slowDecay + x * (1f - _slowDecay);
        Novelty = Novelty * _noveltyDecay + MathF.Max(0f, surprise - 1f) * (1f - _noveltyDecay);
        return surprise;
    }

    /// <summary>Explicit dishabituation (a categorical change resets error statistics).</summary>
    public void Reset()
    {
        ErrorStd = 0.05f;
        Novelty = 0f;
    }
}
