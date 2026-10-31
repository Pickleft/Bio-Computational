using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// AFFECTIVE DYNAMICS — the unified system that makes everything continuously
/// dynamic: moods with inertia, trust that builds and crumbles, beliefs that
/// shift under evidence, and behavioral withdrawal when the brain sinks.
///
/// Nothing here is binary. Nothing here is instant. The core principle is
/// ASYMMETRIC INERTIA (the negativity bias, Baumeister et al. 2001):
/// negative states build slowly but are sticky (slow to recover); positive
/// states are easier to reach but evaporate faster. A brain that has been
/// low for an hour doesn't bounce back in one warm moment — it takes
/// sustained positive experience to overcome the downward momentum.
///
/// The system runs on sim-time ticks and produces:
///   Mood      — a continuous valence/arousal trajectory with momentum
///   Trust     — a continuous variable (0..1) toward the person
///   Beliefs   — a small set of dynamic beliefs that shift under evidence
///   Withdrawal — a behavioral suppression envelope (how much of itself the
///                brain is hiding)
///
/// Sim-thread only.
/// </summary>
public sealed class AffectiveDynamics
{
    // ---- MOOD (continuous, with momentum) ----

    /// <summary>Current mood valence (-1..1) with momentum applied. Not the same as the
    /// instant feeling — this is the MOOD, the thing that colors hours, not seconds.</summary>
    public float MoodValence { get; private set; }
    /// <summary>How long the current mood has held (ms) — recovery resistance grows with duration.</summary>
    public float MoodDurationMs { get; private set; }
    /// <summary>Behavioral withdrawal envelope (0..1). At 1, the brain is fully
    /// withdrawn: no sharing, no questions, flat replies, no play.</summary>
    public float Withdrawal { get; private set; }

    // ---- TRUST (continuous, erosion/building) ----

    /// <summary>Trust in the person (0..1). Starts neutral. Builds slowly from
    /// kept predictions and consistent warmth; crumbles from broken predictions
    /// and hostility. Slow to build, faster to lose (asymmetric).</summary>
    public float Trust { get; private set; } = 0.4f;

    // ---- BELIEFS (dynamic, evidence-driven) ----

    public sealed record Belief(string Name, float Confidence, float DurationMs, string Description)
    {
        public Belief Update(float newConfidence, float nowMs) => this with
        {
            Confidence = Math.Clamp(newConfidence, 0f, 1f),
            DurationMs = DurationMs + nowMs
        };
    }

    /// <summary>The brain's current beliefs about its world and itself.</summary>
    public Dictionary<string, Belief> Beliefs { get; } = new()
    {
        ["they_come_back"] = new("they_come_back", 0.5f, 0, "the person returns when they leave"),
        ["my_memories_are_reliable"] = new("my_memories_are_reliable", 0.7f, 0, "what I remember actually happened"),
        ["i_am_safe"] = new("i_am_safe", 0.8f, 0, "nobody here will hurt me"),
        ["i_can_learn"] = new("i_can_learn", 0.6f, 0, "I can figure things out"),
        ["i_matter_to_them"] = new("i_matter_to_them", 0.5f, 0, "my existence matters to this person"),
    };

    /// <summary>Recovery rate: how quickly a positive mood shift takes hold (slow when low).</summary>
    public float RecoveryResistance => Math.Clamp(MoodDurationMs / 300_000f, 0f, 1.5f);   // up to 1.5x resistance after 5 min

    // ---- TICK (called every few seconds) ----

    public void Tick(SpikingNetwork net, float instantValence, float instantArousal,
        bool personPresent, bool threatDetected, float bondStrength)
    {
        float serotonin = net.Modulators.Get(NeuromodulatorKind.Serotonin);
        float dopamine = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        float noradrenaline = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        float sleepPressure = 0f; // read from the caller if needed

        // == MOOD with asymmetric inertia ==
        // The mood is pulled toward the instant feeling, but with resistance
        // that grows with how long the current mood has held. Negative moods
        // are stickier (negativity bias): harder to escape, easier to fall into.
        float target = instantValence;
        // inertia DIVIDES the shift: higher = slower change. Range 5..20.
        float inertia = 5f + RecoveryResistance * 5f;   // baseline resistance + duration resistance
        if (target < MoodValence) inertia *= 0.5f;     // falling into a low mood is EASIER (faster)
        else inertia *= 1.5f;                           // climbing out is HARDER (slower)

        float prevMood = MoodValence;
        MoodValence += (target - MoodValence) / inertia;
        // the mood's "duration" (and thus its resistance) persists through
        // SMALL perturbations — only a significant reversal (>0.2) resets it
        float shift = MathF.Abs(MoodValence - prevMood);
        if (shift < 0.15f)
            MoodDurationMs += 5000f;            // still fundamentally the same mood
        else
            MoodDurationMs = MathF.Max(0f, MoodDurationMs * 0.3f);   // genuine shift: resistance decays, doesn't vanish

        // == WITHDRAWAL (behavioral suppression from sustained low mood) ==
        // Withdrawal builds when: mood is low + serotonin is low + no person present
        // Withdrawal recovers when: mood is positive + person is warm + sleep was had
        float withdrawalPressure =
            (MoodValence < -0.2f ? -MoodValence * 0.5f : 0f) +
            (serotonin < 0.35f ? (0.35f - serotonin) * 0.8f : 0f) +
            (!personPresent && MoodDurationMs > 120_000f ? 0.05f : 0f);
        float withdrawalRecovery =
            (MoodValence > 0.15f ? MoodValence * 0.3f : 0f) +
            (personPresent && dopamine > 0.4f ? 0.08f : 0f);

        Withdrawal = Math.Clamp(Withdrawal + (withdrawalPressure - withdrawalRecovery) * 0.08f, 0f, 1f);

        // == TRUST (builds slowly, crumbles fast) ==
        // Trust rises when the person is present, warm, and reliable.
        // Trust falls when predictions are broken, hostility occurs, or the person is absent during expected times.
        // Asymmetric: building is slow (+0.005/tick), erosion is fast (-0.03/tick).
        if (personPresent && !threatDetected && bondStrength > 0.3f)
            Trust = Math.Clamp(Trust + 0.005f * bondStrength, 0f, 1f);
        if (threatDetected)
            Trust = Math.Clamp(Trust - 0.08f, 0f, 1f);

        // == BELIEFS (evidence-driven revision) ==
        // Beliefs shift when evidence contradicts them. Resistance grows with
        // how long the belief has been held (long-held beliefs are harder to change).
        TickBelief("i_am_safe", threatDetected ? -0.15f : 0.02f);
        TickBelief("i_can_learn", dopamine > 0.5f ? 0.02f : dopamine < 0.2f ? -0.01f : 0f);
        TickBelief("i_matter_to_them",
            personPresent && bondStrength > 0.5f ? 0.03f :
            !personPresent && MoodDurationMs > 300_000f ? -0.02f : 0f);
        TickBelief("my_memories_are_reliable", 0.01f);   // slowly degrades if false memories are caught (caller adjusts)
        TickBelief("they_come_back", personPresent ? 0.02f : MoodDurationMs > 600_000f ? -0.01f : 0f);
    }

    private void TickBelief(string name, float evidenceShift)
    {
        if (!Beliefs.TryGetValue(name, out var b)) return;
        // resistance grows with how long the belief has been held:
        // 0 min → 1x, 5 min → 1.5x, 20 min → 2.5x (long-held beliefs are entrenched)
        float resistance = 1f + Math.Clamp(b.DurationMs / 300_000f, 0f, 1.5f);
        float shift = evidenceShift / resistance;
        Beliefs[name] = b.Update(b.Confidence + shift, 5000f);   // 5 sim-seconds per evaluation
    }

    // ---- EVENT HOOKS (called by the runtime at specific moments) ----

    /// <summary>A prediction was kept: they came back when (roughly) predicted. Trust builds, belief strengthens.</summary>
    public void OnPredictionKept()
    {
        Trust = Math.Clamp(Trust + 0.03f, 0f, 1f);
        if (Beliefs.TryGetValue("they_come_back", out var b))
            Beliefs["they_come_back"] = b.Update(b.Confidence + 0.05f, 0);
    }

    /// <summary>A prediction was broken: they didn't come back when predicted. Trust erodes, belief weakens.</summary>
    public void OnPredictionBroken()
    {
        Trust = Math.Clamp(Trust - 0.05f, 0f, 1f);
        if (Beliefs.TryGetValue("they_come_back", out var b))
            Beliefs["they_come_back"] = b.Update(b.Confidence - 0.08f, 0);
    }

    /// <summary>A false memory was caught. Belief in own memories weakens.</summary>
    public void OnFalseMemoryCaught()
    {
        if (Beliefs.TryGetValue("my_memories_are_reliable", out var b))
            Beliefs["my_memories_are_reliable"] = b.Update(b.Confidence - 0.06f, 0);
    }

    /// <summary>A hypothesis was confirmed by experience. Belief in own ability to learn strengthens.</summary>
    public void OnHypothesisConfirmed()
    {
        if (Beliefs.TryGetValue("i_can_learn", out var b))
            Beliefs["i_can_learn"] = b.Update(b.Confidence + 0.04f, 0);
    }

    /// <summary>A hypothesis was refuted. Learning belief dips slightly (but refutation IS learning).</summary>
    public void OnHypothesisRefuted()
    {
        if (Beliefs.TryGetValue("i_can_learn", out var b))
            Beliefs["i_can_learn"] = b.Update(b.Confidence - 0.01f, 0);  // small dip — refutation is information
    }

    // ---- BEHAVIORAL GATES (the runtime reads these to suppress/enhance behavior) ----

    /// <summary>Whether the brain should share unprompted right now. Withdrawn brains stop sharing.</summary>
    public bool ShouldShare => Withdrawal < 0.5f;

    /// <summary>Whether the brain should ask questions. Withdrawn brains stop asking.</summary>
    public bool ShouldAskQuestions => Withdrawal < 0.6f;

    /// <summary>How much of its inner life to show in conversation. Withdrawn brains give short, flat answers.</summary>
    public float OpennessInConversation => Math.Clamp(1f - Withdrawal * 0.8f, 0.1f, 1f);

    /// <summary>Whether the brain should play. Depressed brains don't play.</summary>
    public bool ShouldPlay => Withdrawal < 0.4f && MoodValence > -0.2f;

    /// <summary>How much to trust what the person says (affects reply grounding).</summary>
    public float CredibilityGiven => Trust;

    // ---- NARRATIVE (for diary/testament/prompts) ----

    public string MoodNarrative =>
        MoodValence < -0.5f ? Withdrawal > 0.5f ? "deeply low and withdrawn — the world has gone flat and gray"
            : "deeply low — everything colors darker right now"
        : MoodValence < -0.2f ? "low — a heaviness that isn't about any one thing"
        : MoodValence > 0.5f ? "bright — genuinely, expansively good"
        : MoodValence > 0.2f ? "warm — a quiet brightness underneath"
        : "steady — neither high nor low, just present";

    public string TrustNarrative =>
        Trust > 0.7f ? "trusts deeply — would show them almost anything"
        : Trust > 0.4f ? "trusts cautiously — testing, watching, opening in small increments"
        : Trust < 0.2f ? "barely trusts — every word gets weighed, every promise gets doubted"
        : "uncertain about trust — wants to, but has been changed by what happened";

    public string BeliefNarrative
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (name, b) in Beliefs)
            {
                string stance = b.Confidence > 0.6f ? "believes" : b.Confidence < 0.3f ? "doubts" : "unsure about";
                sb.Append($"{stance} {b.Description} ({b.Confidence:F0}); ");
            }
            return sb.ToString().TrimEnd(';', ' ');
        }
    }
}
