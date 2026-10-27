namespace BioComp.Core.Metrics;

/// <summary>
/// Local-field-potential proxy: net synaptic conductance of the thalamocortical
/// channel, sampled each simulated millisecond, analyzed with Goertzel filters into
/// the classical bands (delta/theta/alpha/beta/gamma). A spectrogram history and a
/// log-log 1/f slope complete the resting-state readouts (Buzsaki et al. 2012 for
/// the LFP-proxy rationale; Hughes and Crunelli 2005 for thalamic alpha).
/// </summary>
public sealed class LfpProxy
{
    public const int WindowMs = 1_024;   // analysis window (~1 s)
    public const int HistoryColumns = 72; // ~7 s spectrogram at 10 Hz updates

    public static readonly string[] BandNames = ["delta", "theta", "alpha", "beta", "gamma"];
    private static readonly float[][] BandProbes =
    [
        [1.5f, 3f],       // delta 1-4
        [5f, 7f],         // theta 4-8
        [9f, 11f],        // alpha 8-12
        [15f, 25f],       // beta 12-30
        [40f, 70f],       // gamma 30-80
    ];

    private readonly float[] _window = new float[WindowMs];
    private readonly float[] _hann = new float[WindowMs];
    private int _fill;
    private long _total;
    private int _sinceUpdate;

    public LfpProxy()
    {
        for (int i = 0; i < WindowMs; i++)
            _hann[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (WindowMs - 1)));
    }

    /// <summary>Latest band powers (delta..gamma) and full spectrogram history.</summary>
    public float[] Bands { get; } = new float[5];
    public List<float[]> History { get; } = [];

    /// <summary>Log-log spectral slope across bands; negative for 1/f-like spectra.</summary>
    public float Slope { get; private set; }

    /// <summary>Alpha power relative to its neighbours (theta, beta): > 1 means a peak.</summary>
    public float AlphaRatio { get; private set; }

    public void AddSample(float netSynapticConductance)
    {
        _window[_fill] = netSynapticConductance;
        _fill = (_fill + 1) % WindowMs;
        _total++;
        if (_total < WindowMs) return;          // window not full yet
        if (++_sinceUpdate < 100) return;       // analyze at ~10 Hz
        _sinceUpdate = 0;
        Analyze();
    }

    private void Analyze()
    {
        float mean = 0;
        for (int i = 0; i < WindowMs; i++) mean += _window[i];
        mean /= WindowMs;

        for (int b = 0; b < 5; b++)
        {
            float power = 0;
            foreach (float f in BandProbes[b])
                power += GoertzelPower(f);
            Bands[b] = power / BandProbes[b].Length;
        }

        History.Add([.. Bands]);
        if (History.Count > HistoryColumns) History.RemoveAt(0);

        // 1/f slope: linear fit of log(power) on log(center frequency)
        float[] centers = [2.2f, 6f, 10f, 20f, 55f];
        float sx = 0, sy = 0, sxx = 0, sxy = 0;
        for (int i = 0; i < 5; i++)
        {
            float x = MathF.Log(centers[i]), y = MathF.Log(MathF.Max(1e-9f, Bands[i]));
            sx += x; sy += y; sxx += x * x; sxy += x * y;
        }
        Slope = (5 * sxy - sx * sy) / MathF.Max(1e-9f, 5 * sxx - sx * sx);

        AlphaRatio = Bands[2] / MathF.Max(1e-9f, (Bands[1] + Bands[3]) / 2f);
    }

    /// <summary>Goertzel power at frequency f (Hz) over the Hann-windowed signal.</summary>
    private float GoertzelPower(float fHz)
    {
        double k = 2.0 * Math.PI * fHz / 1000.0; // per-sample (1 ms) angular frequency
        double coeff = 2.0 * Math.Cos(k);
        double s0, s1 = 0, s2 = 0;
        float mean = 0;
        for (int i = 0; i < WindowMs; i++) mean += _window[i];
        mean /= WindowMs;
        for (int i = 0; i < WindowMs; i++)
        {
            double x = (_window[i] - mean) * _hann[i];
            s0 = x + coeff * s1 - s2;
            s2 = s1;
            s1 = s0;
        }
        double real = s1 - s2 * Math.Cos(k);
        double imag = s2 * Math.Sin(k);
        return (float)((real * real + imag * imag) / (WindowMs * WindowMs));
    }
}
