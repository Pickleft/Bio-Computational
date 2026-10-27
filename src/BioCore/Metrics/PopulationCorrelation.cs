namespace BioComp.Core.Metrics;

/// <summary>
/// Windowed Pearson correlation between two population rate series (e.g. DMN vs
/// task-positive; anti-correlation at rest is a resting-state signature, Fox et al. 2005).
/// </summary>
public sealed class PopulationCorrelation
{
    private readonly float[] _a = new float[WindowCount];
    private readonly float[] _b = new float[WindowCount];
    private int _count;

    public const int WindowCount = 100; // windows of accumulation

    public void AddSample(float a, float b)
    {
        _a[_count % WindowCount] = a;
        _b[_count % WindowCount] = b;
        _count++;
    }

    public float Pearson()
    {
        int n = Math.Min(_count, WindowCount);
        if (n < 10) return 0f;
        float meanA = 0, meanB = 0;
        for (int i = 0; i < n; i++) { meanA += _a[i]; meanB += _b[i]; }
        meanA /= n; meanB /= n;
        float cov = 0, varA = 0, varB = 0;
        for (int i = 0; i < n; i++)
        {
            float da = _a[i] - meanA, db = _b[i] - meanB;
            cov += da * db; varA += da * da; varB += db * db;
        }
        float denom = MathF.Sqrt(varA * varB);
        return denom > 1e-9f ? cov / denom : 0f;
    }
}
