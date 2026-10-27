namespace BioComp.Core.Metrics;

/// <summary>
/// Neuronal avalanche analysis on 4 ms binned population spike counts
/// (Beggs &amp; Plenz 2003 J Neurosci 23:11167). Branching sigma is the ratio of
/// descendant to ancestor activity over consecutive active bins; sigma near 1 means
/// the network operates near criticality.
/// </summary>
public sealed class AvalancheMetrics
{
    public const int BinMs = 4;

    private readonly List<int> _bins = [];
    private int _currentBinCount;

    /// <summary>Call once per network tick with the tick's spike count.</summary>
    public void OnTick(long tickIndex, int spikeCount)
    {
        _currentBinCount += spikeCount;
        if ((tickIndex + 1) % BinMs == 0)
        {
            _bins.Add(_currentBinCount);
            _currentBinCount = 0;
            if (_bins.Count > 100_000) _bins.RemoveRange(0, 50_000); // bounded memory
        }
    }

    public IReadOnlyList<int> Bins => _bins;

    /// <summary>sigma = sum(next) / sum(current) over consecutive positive bins.</summary>
    public float BranchingRatio()
    {
        double ancestors = 0, descendants = 0;
        for (int i = 0; i + 1 < _bins.Count; i++)
        {
            if (_bins[i] > 0 && _bins[i + 1] > 0)
            {
                ancestors += _bins[i];
                descendants += _bins[i + 1];
            }
        }
        return ancestors > 0 ? (float)(descendants / ancestors) : 0f;
    }

    /// <summary>Avalanche sizes: total spikes in each run of consecutive active bins.</summary>
    public List<int> AvalancheSizes()
    {
        var sizes = new List<int>();
        int run = 0;
        foreach (int b in _bins)
        {
            if (b > 0) run += b;
            else if (run > 0) { sizes.Add(run); run = 0; }
        }
        if (run > 0) sizes.Add(run);
        return sizes;
    }
}
