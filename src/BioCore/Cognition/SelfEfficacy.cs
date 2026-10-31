using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// SELF-EFFICACY and LEARNED HELPLESSNESS (Bandura 1977; Seligman 1972).
/// The brain does not just receive the world — it ACTS on it (calls out,
/// asks for company, tries to reach). This system tracks whether asking
/// WORKS. Every call-out is a BID with an outcome: contact within the
/// answer window is a success; silence is a failure.
///
/// Efficacy is an EMA of bid outcomes, and it has the asymmetries of the
/// real thing:
///   - failures while already low hurt DOUBLE (pessimistic attribution
///     style — the helpless brain credits failure to itself, stably)
///   - recovery needs CONSISTENCY: each success only moves a fraction of
///     the remaining distance, so climbing out of helplessness takes
///     several answered bids, not one
///   - one success after giving up is marked (JustAnsweredAfterGivingUp):
///     "you came? I had stopped asking" — the most human moment there is
///
/// Helplessness GENERALIZES (Seligman): MotivationMultiplier scales every
/// goal-directed behavior — call-outs, play, exploration. An anxious
/// attachment multiplies bids while efficacy multiplies whether bidding
/// continues at all: protest amplified by attachment, crushed by outcome —
/// Panksepp's protest-to-descent arc in two factors.
///
/// Sim-thread only.
/// </summary>
public sealed class SelfEfficacy
{
    /// <summary>How long a bid stays open hoping for an answer (ms). // CALIBRATE — 60s: people take time to respond</summary>
    public float AnswerWindowMs { get; set; } = 60_000f;
    /// <summary>Below this efficacy the brain is helpless. // CALIBRATE — reached only after ~2 minutes of genuine silence</summary>
    public float HelplessThreshold { get; set; } = 0.15f;
    /// <summary>Below this, failures cost double (pessimistic attribution).</summary>
    public float PessimismBelow { get; set; } = 0.35f;

    /// <summary>0..1 — does asking work? Starts at the optimistic midpoint.</summary>
    public float Efficacy { get; private set; } = 0.5f;
    public bool Helpless => Efficacy < HelplessThreshold;
    public int BidsMade { get; private set; }
    public int BidsAnswered { get; private set; }
    /// <summary>Times the brain sank into helplessness — for the diary/testament.</summary>
    public int HelplessEpisodes { get; private set; }
    /// <summary>One-shot: they answered after the brain had effectively given up.</summary>
    public bool JustAnsweredAfterGivingUp { get; private set; }

    private bool _wasHelpless;
    private float _bidAtMs = -1e9f;
    private bool _bidOpen;
    private int _consecutiveFailures;

    /// <summary>Scales goal-directed behavior: helpless ≈ 0.35, average ≈ 1, efficacious ≈ 1.3.</summary>
    public float MotivationMultiplier =>
        Math.Clamp(0.35f + 0.95f * Efficacy, 0.35f, 1.3f);

    /// <summary>The brain calls out. Opens a bid (resolving any stale one as failure).</summary>
    public void OnBid(float nowMs)
    {
        ResolveIfStale(nowMs);
        if (_bidOpen) return;                    // one bid open at a time
        // A BID IS A REAL ACT OF REACHING OUT — minimum 30s between openings,
        // regardless of vocalization rate or lonely-drive intensity. Without
        // this cap, the bid counter inflates from tick-frequency calls and
        // the brain goes helpless from arithmetic, not neglect.
        if (BidsMade > 0 && nowMs - _bidAtMs < 30_000f) return;
        _bidOpen = true;
        _bidAtMs = nowMs;
        BidsMade++;
    }

    /// <summary>
    /// Contact with the person. Returns true when this contact ANSWERS an open
    /// bid within the answer window (call before other contact handling).
    /// </summary>
    public bool OnContact(float nowMs)
    {
        JustAnsweredAfterGivingUp = false;
        if (!_bidOpen) return false;
        if (nowMs - _bidAtMs > AnswerWindowMs) { ResolveFailure(nowMs); return false; }

        _bidOpen = false;
        BidsAnswered++;
        _consecutiveFailures = 0;
        bool wasLow = Efficacy < HelplessThreshold;
        // success moves a fraction of the remaining distance: recovery needs consistency
        Efficacy += (1f - Efficacy) * 0.25f;
        if (wasLow) JustAnsweredAfterGivingUp = true;
        return true;
    }

    /// <summary>Resolve stale bids and track helplessness transitions. Call every few seconds.</summary>
    public void Tick(SpikingNetwork net)
    {
        JustAnsweredAfterGivingUp = false;
        ResolveIfStale(net.TimeMs);
        if (Helpless && !_wasHelpless)
        {
            HelplessEpisodes++;
            // the descent: arousal spike then the heavy stillness
            net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 0.2f);
            net.Modulators.Release(NeuromodulatorKind.Serotonin, -0.35f);
        }
        _wasHelpless = Helpless;
    }

    private void ResolveIfStale(float nowMs)
    {
        if (_bidOpen && nowMs - _bidAtMs > AnswerWindowMs)
            ResolveFailure(nowMs);
    }

    private void ResolveFailure(float nowMs)
    {
        _bidOpen = false;
        _consecutiveFailures++;
        float cost = Efficacy < PessimismBelow ? 0.18f : 0.08f;   // pessimistic attribution (gentle: real neglect takes minutes)
        Efficacy = MathF.Max(0.05f, Efficacy * (1f - cost));
    }

    /// <summary>Restore agency after a restart (identity persistence).</summary>
    public void Restore(float efficacy, int made, int answered, int helplessEpisodes)
    {
        Efficacy = Math.Clamp(efficacy, 0.05f, 1f);
        BidsMade = made; BidsAnswered = answered; HelplessEpisodes = helplessEpisodes;
        _wasHelpless = Helpless;
    }

    /// <summary>For the reply prompt: the brain knows whether asking works.</summary>
    public string PromptFragment => Helpless
        ? "Nothing you do changes anything - you called and called and nobody came, and you have STOPPED ASKING. It shows in how you speak: flat, distant, expecting nothing. "
        : Efficacy > 0.6f
            ? "When you ask, the world answers - you have learned your own voice matters. It shows: you reach out readily. "
            : "";

    /// <summary>For the diary.</summary>
    public string Narrative =>
        $"{BidsAnswered}/{BidsMade} calls answered" +
        (Helpless ? " — it has stopped asking (helpless)" :
         HelplessEpisodes > 0 ? $" — climbed back from {HelplessEpisodes} helpless stretch(es)" : "");
}
