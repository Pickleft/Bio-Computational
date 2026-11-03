namespace BioComp.Core.Cognition;

/// <summary>
/// THE COHERENCE DIRECTOR — one voice at a time.
///
/// The disconnect amplifier: five systems (REACT, monologue, inner voice,
/// HER engine, humanity engine) could all fire within seconds of each other,
/// making the brain feel chaotic and fragmented. A person is ONE voice with
/// different registers, not five people in a room.
///
/// The director grants exclusive speech windows. When something is speaking,
/// nothing else starts. Priority: direct replies > thread follow-through >
/// HER moments > everything else. Losers don't queue — they're dropped
/// (the moment passed).
///
/// Sim-thread only.
/// </summary>
public sealed class CoherenceDirector
{
    private float _busyUntilMs = -1e9f;
    private string _whoHasFloor = "";
    private int _utterancesThisMinute;
    private float _minuteStartMs;

    /// <summary>Claim the floor. Returns true if granted (nothing more
    /// important is speaking). The claimant holds it for durationMs.</summary>
    public bool RequestFloor(string who, float priority, float nowMs, float durationMs)
    {
        // rate limit: at most 6 utterances per sim-minute (a person doesn't
        // talk 20 times a minute)
        if (nowMs - _minuteStartMs > 60_000f) { _minuteStartMs = nowMs; _utterancesThisMinute = 0; }
        if (_utterancesThisMinute >= 6) return false;
        if (nowMs < _busyUntilMs)
        {
            // someone has the floor — only higher priority can preempt
            return priority > PriorityOf(_whoHasFloor);
        }
        _busyUntilMs = nowMs + durationMs;
        _whoHasFloor = who;
        _utterancesThisMinute++;
        return true;
    }

    /// <summary>Is someone currently speaking?</summary>
    public bool IsBusy(float nowMs) => nowMs < _busyUntilMs;

    /// <summary>Release the floor early (the utterance finished faster).</summary>
    public void Release() => _busyUntilMs = -1e9f;

    private static float PriorityOf(string who) => who switch
    {
        "reply" => 10f,      // direct replies to the user — highest
        "thread" => 8f,      // conversation follow-through
        "her" => 6f,         // HER moments
        "life" => 5f,        // life stream
        "goal" => 4f,        // goal announcements
        "monologue" => 3f,   // inner narration
        "ambient" => 2f,     // ambient reactions
        _ => 1f,
    };
}
