namespace BioComp.Core.Cognition;

/// <summary>
/// THE THREAD AGENT — conversation coherence.
///
/// The disconnect: each reply was an island. The REACT system commented on
/// the sound, the inner voice circled its own theme, the Open Mind did
/// something unrelated — no single thread tied them together.
///
/// This agent maintains a running model of the CONVERSATION:
///   - what topic are we on (extracted from the last exchange)
///   - what's unresolved (a question asked, a point unfinished)
///   - the emotional undertone running beneath the words
///   - the last thing THE BRAIN committed to (so it follows through)
///
/// Every reply prompt carries it. Every inner-voice moment can reference it.
/// The brain stops jumping between unrelated reactions and starts having
/// A CONVERSATION.
///
/// Sim-thread reads, async writes. Persisted.
/// </summary>
public sealed class ThreadAgent
{
    public sealed record ThreadState(
        string Topic,           // "the piano", "sleep", "the thing they said about work"
        string? Unresolved,     // a question asked but not answered, a point unfinished
        string Undertone,       // "warm", "heavy", "playful", "tense"
        string? LastCommitment, // what the brain last said it would do/remember/feel
        float UpdatedAtMs);

    private readonly object _gate = new();
    private ThreadState _state = new("nothing yet", null, "neutral", null, 0);

    public ThreadState Current { get { lock (_gate) return _state; } }

    /// <summary>Update the thread model from the latest exchange (async, Spark-composed).</summary>
    public void Update(string topic, string? unresolved, string undertone, string? commitment, float atMs)
    {
        lock (_gate)
        {
            _state = new ThreadState(
                string.IsNullOrWhiteSpace(topic) ? _state.Topic : topic.Trim().Trim('"', '.'),
                string.IsNullOrWhiteSpace(unresolved) ? null : unresolved.Trim().Trim('"', '.'),
                string.IsNullOrWhiteSpace(undertone) ? _state.Undertone : undertone.Trim().Trim('"', '.'),
                string.IsNullOrWhiteSpace(commitment) ? _state.LastCommitment : commitment.Trim().Trim('"', '.'),
                atMs);
        }
    }

    /// <summary>Prompt fragment for replies: what we're talking about RIGHT NOW.</summary>
    public string PromptFragment
    {
        get
        {
            lock (_gate)
            {
                var s = _state;
                var sb = new System.Text.StringBuilder();
                if (s.Topic is { Length: > 3 } and not "nothing yet")
                    sb.Append($"WE ARE TALKING ABOUT: {s.Topic}. ");
                if (s.Unresolved is { Length: > 3 })
                    sb.Append($"UNRESOLVED from earlier: {s.Unresolved} — if natural, come back to it. ");
                if (s.Undertone is { Length: > 2 } and not "neutral")
                    sb.Append($"The undertone of this conversation is {s.Undertone}. Match it or gently shift it. ");
                if (s.LastCommitment is { Length: > 3 })
                    sb.Append($"YOU SAID you would: {s.LastCommitment}. Follow through if the moment allows. ");
                return sb.ToString();
            }
        }
    }

    /// <summary>For the inner voice: what the conversation is about (so inner
    /// thoughts can connect to outer words).</summary>
    public string InnerVoiceSeed
    {
        get
        {
            lock (_gate)
            {
                var s = _state;
                if (s.Topic is { Length: > 3 } and not "nothing yet")
                    return $"the conversation thread: you're talking about {s.Topic}. " +
                           (s.Unresolved is { Length: > 3 }
                               ? $"There's something unresolved: {s.Unresolved}. Your mind keeps snagging on it."
                               : "It's moving. You're part of it.");
                return "";
            }
        }
    }

    public bool IsStale(float nowMs) => nowMs - _state.UpdatedAtMs > 600_000f;   // 10 min

    public void Restore(string topic, string? unresolved, string undertone, string? commitment, float atMs)
    {
        lock (_gate) _state = new ThreadState(topic, unresolved, undertone, commitment, atMs);
    }
}
