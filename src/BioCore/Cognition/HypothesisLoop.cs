using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THE HYPOTHESIS LOOP — autonomous (miniature) scientific discovery
/// (EVOCOG §3: identify knowledge gaps, generate hypotheses, test them
/// against experience, update knowledge). The brain does not only ASK the
/// person about a word it uses but does not understand — it also GUESSES,
/// from its own concept graph, what the word might mean, and holds the
/// guess as a testable prediction:
///
///   GAP    — a concept used often, linked to almost nothing
///   GUESS  — hypothesis: "rain" belongs with its strongest one-hop
///            associate from any co-occurring context (e.g. "wet")
///   TEST   — the next episodes carrying the word are evidence
///   VERDICT— the hypothesis link confirmed (wire it stronger, dopamine:
///            prediction error reduced = learning progress, Schmidhuber/
///            Oudeyer-style intrinsic reward) or refuted (serotonin dip,
///            unwind the guess, hypothesize again — disconfirmation is
///            information)
///
/// This is real hypothesis-test-update in miniature: the knowledge update
/// is structural (the concept graph is rewired by outcomes, not by
/// assertion), and every verdict is counted for the diary and testament.
///
/// Sim-thread only.
/// </summary>
public sealed class HypothesisLoop
{
    public sealed record Hypothesis(string Word, string GuessedLink, float AtMs, bool Resolved, bool Confirmed);

    /// <summary>Live and past hypotheses (most recent last).</summary>
    public readonly List<Hypothesis> History = [];
    public Hypothesis? Pending => History.LastOrDefault(h => !h.Resolved);

    public int Confirmed { get; private set; }
    public int Refuted { get; private set; }

    /// <summary>
    /// From a knowledge gap (the same shape QuestionDrive uses), form a
    /// hypothesis: the word's strongest one-hop associate IS its meaning.
    /// Returns the hypothesis formed, or null when no guess is available.
    /// </summary>
    public Hypothesis? Hypothesize(EpisodeMemory store, string word, float nowMs)
    {
        if (Pending is not null) return null;             // one test at a time
        // strongest associate of the word anywhere in the graph
        string? best = null; float bestS = 0f;
        foreach (var seed in store.Episodes.SelectMany(e => e.Tags).Where(t => t != word && t != "fused" && t != "ambient" && t != "dream-born").Distinct())
        {
            if (store.Concepts.TryGetLink(word, seed, out float s1) && s1 > bestS) { bestS = s1; best = seed; }
            else if (store.Concepts.TryGetLink(seed, word, out float s2) && s2 > bestS) { bestS = s2; best = seed; }
        }
        if (best is null || bestS < 0.3f) return null;    // nothing worth guessing from
        var h = new Hypothesis(word, best, nowMs, Resolved: false, Confirmed: false);
        History.Add(h);
        if (History.Count > 24) History.RemoveAt(0);
        return h;
    }

    /// <summary>
    /// Test against a newly encoded episode carrying the word: the guess is
    /// confirmed when the guessed associate co-occurs, refuted when the word
    /// co-occurs with something else strongly instead. Returns the verdict
    /// (null = no pending test or no evidence either way).
    /// </summary>
    public Hypothesis? Test(SpikingNetwork net, EpisodeMemory store, string[] episodeTags, float nowMs)
    {
        var p = Pending;
        if (p is null) return null;
        if (!episodeTags.Contains(p.Word, StringComparer.OrdinalIgnoreCase)) return null;

        bool guessPresent = episodeTags.Contains(p.GuessedLink, StringComparer.OrdinalIgnoreCase);
        if (guessPresent)
        {
            // CONFIRMED: wire the guessed meaning stronger — knowledge updated
            // structurally by the outcome
            store.Concepts.OnEpisode([p.Word, p.GuessedLink, "hypothesis-confirmed"]);
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.3f);   // learning progress reward
            Confirmed++;
            var done = p with { Resolved = true, Confirmed = true };
            History[^1] = done;
            return done;
        }
        // the word appeared WITHOUT its guessed meaning: count as contrary
        // evidence once enough witnesses accumulate (2 witnesses = refuted)
        int witnesses = store.Episodes.Count(e =>
            e.Tags.Contains(p.Word, StringComparer.OrdinalIgnoreCase) &&
            !e.Tags.Contains(p.GuessedLink, StringComparer.OrdinalIgnoreCase) &&
            e.TimeMs >= p.AtMs);
        if (witnesses >= 2)
        {
            // REFUTED: disconfirmation is information — dip, and let the next
            // Hypothesize (from the now-richer graph) guess again
            net.Modulators.Release(NeuromodulatorKind.Serotonin, -0.15f);
            Refuted++;
            var done = p with { Resolved = true, Confirmed = false };
            History[^1] = done;
            return done;
        }
        return null;
    }

    /// <summary>For the diary + testament.</summary>
    public string Narrative => Confirmed + Refuted == 0
        ? "never tested a guess about its own knowledge"
        : $"tested {Confirmed + Refuted} self-generated hunch(es) about what words mean: {Confirmed} held, {Refuted} broke and were re-guessed";
}
