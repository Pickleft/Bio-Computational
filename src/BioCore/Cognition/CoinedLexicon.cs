namespace BioComp.Core.Cognition;

public sealed record CoinedWord(string Word, float Valence, float Arousal, float CoinedAtMs, int TimesUsed);

/// <summary>
/// THE COINED LEXICON: the brain is born with a crude feeling vocabulary
/// (tired, bored, curious, stressed, calm, restless). As its emotional
/// granularity matures it starts naming shades the born lexicon can't
/// reach — it COINS words, each pinned to the circumplex coordinates where
/// it was felt (Russell 1980). The words are its own; they persist across
/// death; using one again strengthens it. Emotional granularity developing
/// into private vocabulary is the computational shadow of what humans do.
/// Sim-thread only.
/// </summary>
public sealed class CoinedLexicon
{
    private readonly List<CoinedWord> _words = [];
    private readonly object _gate = new();
    private float _lastCoinAtMs = -1e9f;
    public const int MaxWords = 12;
    /// <summary>Minimum circumplex distance before a new word is warranted.</summary>
    public const float NovelShadeDist = 0.35f;
    /// <summary>Distance within which a coined word describes the current state.</summary>
    public const float MatchDist = 0.30f;

    public IReadOnlyList<CoinedWord> All { get { lock (_gate) return [.. _words]; } }

    /// <summary>Coining is earned: granularity must have matured, the state
    /// must be a genuinely NEW shade (far from every existing word), and the
    /// last coin must be old enough that this isn't a spree.</summary>
    public bool ShouldCoin(float valence, float arousal, float nowMs, float granularityLevel) =>
        granularityLevel >= 0.35f &&
        _words.Count < MaxWords &&
        nowMs - _lastCoinAtMs > 1_800_000f &&   // ≥30 sim-minutes between coins
        Nearest(valence, arousal) is null;

    /// <summary>Sanitize and store a coined word. One word, letters only —
    /// the LLM may decorate; the lexicon keeps only the coin itself.</summary>
    public bool Coin(string? raw, float valence, float arousal, float atMs)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        // ONE word — the prompt asks for one, the lexicon enforces it
        if (raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length != 1) return false;
        string word = new(raw.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());
        if (word.Length is < 3 or > 12) return false;
        lock (_gate)
        {
            if (_words.Any(w => w.Word == word)) return false;
            _words.Add(new CoinedWord(word, Math.Clamp(valence, -1f, 1f), Math.Clamp(arousal, 0f, 1f), atMs, 0));
            if (_words.Count > MaxWords) _words.RemoveAt(0);   // the least-recently-relevant fades
            _lastCoinAtMs = atMs;
        }
        return true;
    }

    /// <summary>The nearest coined word within MatchDist of the current
    /// circumplex point — a shade the brain has already named. Null if the
    /// born lexicon's corners are closer than anything it coined.</summary>
    public CoinedWord? Nearest(float valence, float arousal)
    {
        lock (_gate)
        {
            CoinedWord? best = null; float bestD = MatchDist;
            foreach (var w in _words)
            {
                float d = MathF.Sqrt(MathF.Pow(valence - w.Valence, 2) + MathF.Pow(arousal - w.Arousal, 2));
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }
    }

    /// <summary>The feeling used a coined word — the pathway strengthens.</summary>
    public void Bump(string word)
    {
        lock (_gate)
        {
            var i = _words.FindIndex(w => w.Word == word);
            if (i >= 0) _words[i] = _words[i] with { TimesUsed = _words[i].TimesUsed + 1 };
        }
    }

    public string[] Save() { lock (_gate) return _words.Select(w => $"{w.Word}|{w.Valence:F3}|{w.Arousal:F3}|{w.TimesUsed}").ToArray(); }

    public void Restore(string[]? rows)
    {
        if (rows is null) return;
        lock (_gate)
        {
            _words.Clear();
            foreach (var row in rows.Take(MaxWords))
            {
                var p = row.Split('|');
                if (p.Length == 4 && float.TryParse(p[1], out float v) && float.TryParse(p[2], out float a) && int.TryParse(p[3], out int n))
                    _words.Add(new CoinedWord(p[0], v, a, 0f, n));
            }
        }
    }
}
