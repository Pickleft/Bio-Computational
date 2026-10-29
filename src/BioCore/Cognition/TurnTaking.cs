namespace BioComp.Core.Cognition;

/// <summary>
/// Conversational turn-taking (Levinson and Torreira 2016): partners in
/// conversation learn each other's rhythm and project when the other's turn
/// will end, taking the floor in the gap rather than overlapping. Here the
/// brain holds its spontaneous vocalizations shortly after the partner speaks
/// (they may not be finished - interrupting is a social error), and notices
/// when an ESTABLISHED pattern breaks: a partner who usually talks often has
/// gone quiet for several typical gaps. Sim-thread only.
/// </summary>
public sealed class TurnTaking
{
    /// <summary>How long after the partner's utterance the brain holds its turn.</summary>
    public float HoldSec { get; set; } = 2.5f;          // CALIBRATE
    /// <summary>Utterances within this window mean an engaged conversation.</summary>
    public float EngagementWindowSec { get; set; } = 600f;
    /// <summary>Minimum engaged utterances before a pattern exists to break.</summary>
    public int EngagementMin { get; set; } = 3;

    private readonly List<float> _utteranceTimes = [];
    private float _lastUtteranceSec = -1e9f;
    private bool _breakLatched;

    public int UtterancesHeard => _utteranceTimes.Count;
    public bool EngagedRecently(float nowSec) =>
        _utteranceTimes.Count(t => nowSec - t < EngagementWindowSec) >= EngagementMin;
    /// <summary>Median observed gap between the partner's utterances (engaged periods).</summary>
    public float TypicalGapSec(float nowSec)
    {
        var recent = _utteranceTimes.Where(t => nowSec - t < EngagementWindowSec).OrderBy(t => t).ToList();
        if (recent.Count < 2) return 30f;
        var gaps = new List<float>();
        for (int i = 1; i < recent.Count; i++) gaps.Add(recent[i] - recent[i - 1]);
        gaps.Sort();
        return gaps[gaps.Count / 2];
    }

    public void OnUserUtterance(float timeSec)
    {
        _utteranceTimes.Add(timeSec);
        while (_utteranceTimes.Count > 0 && timeSec - _utteranceTimes[0] > EngagementWindowSec * 2)
            _utteranceTimes.RemoveAt(0);
        _lastUtteranceSec = timeSec;
        _breakLatched = false;
    }

    /// <summary>False while the partner may still be holding the floor. In an
    /// engaged conversation the hold is much longer: while you and it are
    /// talking, it WAITS properly instead of barging into every pause.</summary>
    public float HoldSecEngaged { get; set; } = 7f;   // CALIBRATE

    public bool ShouldSpeakNow(float nowSec) =>
        nowSec - _lastUtteranceSec > (EngagedRecently(nowSec) ? HoldSecEngaged : HoldSec);

    /// <summary>
    /// True once per break: the partner was engaged in a rhythm and has now
    /// been silent for several typical gaps. Latches until they speak again.
    /// </summary>
    public bool PatternBroken(float nowSec)
    {
        if (_breakLatched || !EngagedRecently(nowSec)) return false;
        float silence = nowSec - _lastUtteranceSec;
        if (silence > MathF.Max(20f, 3f * TypicalGapSec(nowSec)))
        {
            _breakLatched = true;
            return true;
        }
        return false;
    }
}
