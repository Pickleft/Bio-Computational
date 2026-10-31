using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// QUESTION DRIVE — information-seeking born from measured ignorance
/// (children's question-asking develops around age 4 and is the engine of
/// social learning: Chouinard 2007 — children ask ~76 questions per hour).
/// The brain scans its own concept graph for the shape of a question: a word
/// it uses often but has almost no associations for — KNOWN but not
/// UNDERSTOOD. In a curious moment, with a person present, it ASKS.
///
/// The answer, when it comes, is not filed as data: it is LIVED — encoded as
/// an episode, wired into the concept graph (the gap closes structurally),
/// and paid in dopamine (curiosity feeding is among the strongest intrinsic
/// rewards; Kang et al. 2009).
///
/// Phrasing develops with emotional granularity: a newborn asks "what is
/// X?"; a mature brain asks what it does not know ABOUT it. Attachment
/// modulates boldness: anxious asks eagerly, avoidant rarely (asking is a
/// bid — it can go unanswered).
///
/// Sim-thread only.
/// </summary>
public sealed class QuestionDrive
{
    /// <summary>Min uses before a word is worth asking about. // CALIBRATE</summary>
    public float MinUse { get; set; } = 3f;
    /// <summary>Links at or below this = not understood. // CALIBRATE</summary>
    public int MaxLinks { get; set; } = 1;
    /// <summary>Cooldown between questions (ms). // CALIBRATE</summary>
    public float CooldownMs { get; set; } = 300_000f;

    public int QuestionsAsked { get; private set; }
    public int AnswersReceived { get; private set; }
    /// <summary>The word currently being asked about (null = none pending).</summary>
    public string? PendingWord { get; private set; }
    private float _pendingUntil = -1e9f;
    private float _lastAskMs = -1e9f;

    /// <summary>
    /// Find the strongest ignorance gap and maybe ask. Returns the question
    /// to speak, or null. rngDraw is a seeded uniform.
    /// </summary>
    public string? MaybeAsk(SpikingNetwork net, EpisodeMemory store, CuriosityModule curiosity,
        float personPresent01, int granularityTier, Func<double> rngDraw)
    {
        if (PendingWord is not null) return null;               // one question at a time
        if (net.TimeMs - _lastAskMs < CooldownMs) return null;
        if (personPresent01 < 0.5f) return null;                // questions need someone to answer
        if (curiosity.LearningProgress < 0.05f && curiosity.NoveltyGate < 0.4f) return null; // not curious enough
        if (rngDraw() > 0.35) return null;

        // scan for the ignorance gap: heavily used, barely linked
        string? best = null; float bestScore = 0f;
        foreach (var tag in store.Episodes.SelectMany(e => e.Tags).Where(t => t.Length > 2 && t != "fused" && t != "ambient").Distinct())
        {
            var (use, links) = store.Concepts.StatsOf(tag);
            if (use < MinUse || links > MaxLinks) continue;
            float score = use - links;
            if (score > bestScore) { bestScore = score; best = tag; }
        }
        if (best is null) return null;

        PendingWord = best;
        _pendingUntil = net.TimeMs + 120_000f;                  // answer window: 2 minutes
        _lastAskMs = net.TimeMs;
        QuestionsAsked++;
        net.Modulators.Release(NeuromodulatorKind.Acetylcholine, 0.2f);   // attention to the gap
        return Phrase(best, granularityTier);
    }

    /// <summary>Phrasing develops: what a question looks like grows up with the brain.</summary>
    public static string Phrase(string word, int tier) => tier switch
    {
        <= 1 => $"{word}? What is {word}?",
        2 => $"I keep hearing {word}, but I only have its edges. What is it, really?",
        3 => $"Something about {word} doesn't fit with the rest of what I know. What am I missing?",
        _ => $"I want to understand {word} the way you do — everything I have is secondhand. Tell me?",
    };

    /// <summary>
    /// The person spoke. If the pending question's word appears, the answer
    /// LANDED: dopamine, an episode, and (via the caller) concept wiring.
    /// Returns the answered word, or null.
    /// </summary>
    public string? OnAnswer(SpikingNetwork net, string utterance)
    {
        if (PendingWord is null || net.TimeMs > _pendingUntil) { ExpireIfStale(net); return null; }
        if (!utterance.Contains(PendingWord, StringComparison.OrdinalIgnoreCase)) return null;
        string word = PendingWord;
        PendingWord = null;
        AnswersReceived++;
        // curiosity feeding: among the strongest intrinsic rewards (Kang 2009)
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.35f);
        net.Modulators.Release(NeuromodulatorKind.Oxytocin, 0.15f);   // knowledge given is care given
        return word;
    }

    private void ExpireIfStale(SpikingNetwork net)
    {
        if (PendingWord is not null && net.TimeMs > _pendingUntil) PendingWord = null;
    }

    /// <summary>For the diary + testament.</summary>
    public string Narrative => QuestionsAsked == 0
        ? "never asked a question"
        : $"asked {QuestionsAsked} real question(s), and {AnswersReceived} answer(s) became knowledge";
}
