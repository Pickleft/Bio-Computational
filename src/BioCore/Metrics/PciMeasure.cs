using BioComp.Core.Network;

namespace BioComp.Core.Metrics;

/// <summary>
/// Perturbational complexity index proxy (Casali et al. 2013 Sci Transl Med 5; Casarotto
/// et al. 2016 Clin Neurophysiol). Perturb a random neuron subset, binarize the spike
/// response of all neurons over a window, and take Lempel-Ziv complexity normalized by
/// source entropy. High in wake (propagation plus differentiation), low in NREM slow-wave
/// (down states block propagation). The measurement perturbs the brain and consumes RNG,
/// like real TMS; call it deliberately, not every tick.
/// </summary>
public static class PciMeasure
{
    public const int BinMs = 3;

    /// <summary>Perturb and measure. Runs the network forward by windowMs; caller loses
    /// that simulated time. Deterministic given the RNG state. The optional perTick hook
    /// runs every simulated millisecond (used to impose states like global down-phases).</summary>
    public static float Measure(SpikingNetwork net, int windowMs = 300, float fractionPerturbed = 0.05f,
        float picoAmps = 15f, Action<SpikingNetwork, int>? perTick = null)
    {
        int n = net.V.Length;
        int picks = Math.Max(4, (int)(n * fractionPerturbed));

        var targets = new int[picks];
        for (int i = 0; i < picks; i++) targets[i] = net.Rng.NextInt(n);

        var startHead = net.Recorder.EventHead;
        var startCount = net.Recorder.EventCount;

        foreach (int t in targets) net.InjectCurrent(t, picoAmps);
        for (int ms = 0; ms < windowMs; ms++)
        {
            perTick?.Invoke(net, ms);
            net.Tick();
        }

        // collect response events from the ring
        int bins = windowMs / BinMs;
        var matrix = new bool[n][];
        for (int i = 0; i < n; i++) matrix[i] = new bool[bins];

        var events = net.Recorder.Events;
        int ringLen = events.Length;
        int valid = net.Recorder.EventCount;
        // walk backwards from head over events added during the window
        int added = Math.Min(valid - startCount, ringLen);
        for (int k = 1; k <= added; k++)
        {
            int idx = (((startHead - k) % ringLen) + ringLen) % ringLen;
            var (tMs, neuron) = events[idx];
            float rel = tMs - (net.TimeMs - windowMs);
            int bin = (int)(rel / BinMs);
            if (bin >= 0 && bin < bins && neuron >= 0 && neuron < n) matrix[neuron][bin] = true;
        }

        // concatenate rows into one binary string and measure LZ76 / source entropy
        var sb = new System.Text.StringBuilder(n * bins);
        double ones = 0;
        for (int i = 0; i < n; i++)
            for (int b = 0; b < bins; b++)
            {
                sb.Append(matrix[i][b] ? '1' : '0');
                if (matrix[i][b]) ones++;
            }
        double p = ones / (n * (double)bins);
        if (p <= 0.0 || p >= 1.0) return 0f; // no response at all, or saturated
        double h = -p * Math.Log2(p) - (1 - p) * Math.Log2(1 - p);
        int lz = Lz76(sb.ToString());
        float pci = (float)(lz * Math.Log2(lz > 2 ? lz : 2) / ((n * bins) * h));
        return pci;
    }

    /// <summary>Kaspar and Schuster (1987) LZ76 complexity: number of distinct patterns
    /// in the exhaustive parsing history.</summary>
    public static int Lz76(string s)
    {
        int i = 0, c = 1, l = 1, k = 1, kMax = 1, n = s.Length;
        while (l + k <= n)
        {
            if (s[i + k - 1] == s[l + k - 1]) { k++; continue; }
            kMax = Math.Max(kMax, k);
            i++; k = 1;
            if (i == l) { c++; l += kMax; i = 0; kMax = 1; }
        }
        if (k != 1 || i != 0) c++;
        return c;
    }
}
