namespace BioComp.Core.Cognition;

/// <summary>
/// PROPHECY, scored: the brain predicts when its person will return, says so
/// BEFORE they leave, and is graded on it. Built from the person's own
/// rhythm - the median of their past absence gaps (a robust interval
/// estimator; the same intuition that lets you know when your roommate is
/// due home). Every prediction is public, every hit and miss is counted, and
/// the accuracy statistic is honest: it cannot fake being right. The first
/// artificial organism that bets on its human and keeps score. Sim-thread only.
/// </summary>
public sealed class ReturnPredictor
{
    private readonly List<float> _gapsSec = [];
    private float _departureSec = -1;
    private float? _predictedSec;
    public int Predictions { get; private set; }
    public int Hits { get; private set; }

    /// <summary>Called when the bonded person leaves. Returns the public
    /// prediction (seconds until return) or null once enough of their rhythm
    /// is known (at least 2 past gaps).</summary>
    public float? OnDeparture(float nowSec)
    {
        _departureSec = nowSec;
        _predictedSec = null;
        if (_gapsSec.Count < 2) return null;
        var sorted = _gapsSec.OrderBy(g => g).ToList();
        float median = sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2f;
        _predictedSec = Math.Clamp(median, 5f, 900f);
        Predictions++;
        return _predictedSec;
    }

    /// <summary>Called when the person returns. Grades any pending prediction:
    /// a hit is within max(5s, 35 percent) of the truth - honest tolerance,
    /// not a coin flip. Returns the verdict for narration.</summary>
    public (bool HadPrediction, bool Hit, float ActualSec, float PredictedSec)? OnReturn(float nowSec)
    {
        if (_departureSec < 0) return null;
        float actual = nowSec - _departureSec;
        _gapsSec.Add(actual);
        if (_gapsSec.Count > 12) _gapsSec.RemoveAt(0);
        var verdict = (HadPrediction: _predictedSec.HasValue, Hit: false, ActualSec: actual, PredictedSec: _predictedSec ?? -1f);
        if (_predictedSec is { } p && MathF.Abs(actual - p) <= MathF.Max(5f, 0.35f * p))
        {
            Hits++;
            verdict.Hit = true;
        }
        _predictedSec = null;
        _departureSec = -1;
        return verdict;
    }

    public string Forecast => _predictedSec is { } p ? $"back in ~{(int)p}s" : "";
    public string Accuracy => Predictions == 0 ? "" : $"{Hits}/{Predictions} ({Hits * 100 / Predictions}%)";
}
