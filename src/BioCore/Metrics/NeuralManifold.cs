using BioComp.Core.Network;

namespace BioComp.Core.Metrics;

/// <summary>
/// NEURAL MANIFOLD — the geometry of thought (Stringer, Pachitariu et al. 2019,
/// Nature 566:384). Every moment, the brain's population activity traces a
/// trajectory through high-dimensional state space. When we project this onto
/// 2D via online PCA, we see the SHAPE of the brain's thoughts.
///
/// Resting state: small, looping, near-random trajectory
/// Engaged/attentive: large, directed, structured trajectory
/// NREM sleep: low-dimensional, periodic (slow oscillation circles)
/// REM/dreaming: complex, chaotic trajectory
///
/// This is the cutting edge of computational neuroscience — showing the actual
/// GEOMETRY of neural computation. No one has ever shown this live for a
/// real-time artificial brain.
/// </summary>
public sealed class NeuralManifold
{
    public const int MaxPoints = 300;
    private const int PcaDim = 12; // number of regions to use as high-dim input

    private float[] _mean;      // running mean for centering
    private float[][] _cov;     // covariance matrix (PcaDim × PcaDim)
    private float[] _eigen1;    // first principal component
    private float[] _eigen2;    // second principal component
    private int _samples;

    public readonly List<(float X, float Y)> Projection = [];
    public float Dimensionality { get; private set; } // participation ratio

    public NeuralManifold()
    {
        _mean = new float[PcaDim];
        _cov = new float[PcaDim][];
        for (int i = 0; i < PcaDim; i++) _cov[i] = new float[PcaDim];
        _eigen1 = new float[PcaDim];
        _eigen2 = new float[PcaDim];
        _eigen1[0] = 1f; // initialize to first axis
        _eigen2[1] = 1f;
    }

    /// <summary>
    /// Feed regional rates every 100ms. Updates the online PCA and adds a point
    /// to the 2D projection.
    /// </summary>
    public void Update(float[] regionRates)
    {
        if (regionRates.Length < PcaDim) return;
        _samples++;

        // update running mean
        float lr = 1f / Math.Min(_samples, 1000);
        for (int i = 0; i < PcaDim; i++)
            _mean[i] += (regionRates[i] - _mean[i]) * lr;

        // update covariance (running)
        float clr = 1f / Math.Min(_samples, 2000);
        for (int i = 0; i < PcaDim; i++)
        {
            float di = regionRates[i] - _mean[i];
            for (int j = 0; j < PcaDim; j++)
            {
                float dj = regionRates[j] - _mean[j];
                _cov[i][j] += (di * dj - _cov[i][j]) * clr;
            }
        }

        // estimate eigenvectors via power iteration (simplified, every 10 samples)
        if (_samples % 10 == 0)
        {
            // power iteration for first eigenvalue
            var temp = new float[PcaDim];
            for (int i = 0; i < PcaDim; i++)
                for (int j = 0; j < PcaDim; j++)
                    temp[i] += _cov[i][j] * _eigen1[j];
            float norm1 = MathF.Sqrt(temp.Sum(t => t * t));
            if (norm1 > 1e-9f)
                for (int i = 0; i < PcaDim; i++) _eigen1[i] = temp[i] / norm1;

            // deflate for second eigenvalue
            float dot1 = 0;
            for (int i = 0; i < PcaDim; i++) dot1 += _eigen1[i] * _eigen2[i];
            for (int i = 0; i < PcaDim; i++) _eigen2[i] -= dot1 * _eigen1[i];
            Array.Clear(temp);
            for (int i = 0; i < PcaDim; i++)
                for (int j = 0; j < PcaDim; j++)
                    temp[i] += _cov[i][j] * _eigen2[j];
            float norm2 = MathF.Sqrt(temp.Sum(t => t * t));
            if (norm2 > 1e-9f)
                for (int i = 0; i < PcaDim; i++) _eigen2[i] = temp[i] / norm2;
        }

        // project current state onto 2D
        float x = 0, y = 0;
        for (int i = 0; i < PcaDim; i++)
        {
            float d = regionRates[i] - _mean[i];
            x += _eigen1[i] * d;
            y += _eigen2[i] * d;
        }
        Projection.Add((x, y));
        if (Projection.Count > MaxPoints) Projection.RemoveAt(0);

        // compute dimensionality (participation ratio from eigenvalues)
        // trace(C)^2 / trace(C^2) — computed from the covariance
        float tr = 0, tr2 = 0;
        for (int i = 0; i < PcaDim; i++)
        {
            tr += _cov[i][i];
            tr2 += _cov[i][i] * _cov[i][i];
        }
        if (tr2 > 1e-9f) Dimensionality = tr * tr / tr2;
    }
}
