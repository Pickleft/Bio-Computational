using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// EMOTIONAL GRANULARITY — the brain's feeling vocabulary DEVELOPS with age
/// and experience, exactly as human emotional development works. A newborn
/// feels "good" or "bad." A three-day-old feels "quietly content" or
/// "restlessly curious" or "the particular ache of missing someone specific."
///
/// Lisa Feldman Barrett's theory of constructed emotion: emotions aren't
/// universal circuits — they're CONSTRUCTED from more basic affective
/// ingredients (valence, arousal, conceptual knowledge). As the brain gains
/// experience, it constructs more specific, nuanced emotional concepts.
///
/// Granularity scales with:
/// - Age (sim-time lived)
/// - Experience diversity (episode count)
/// - Social depth (bonds formed)
/// - Vocabulary absorbed (words heard)
///
/// The interpreter is given age-appropriate emotional vocabulary, so a young
/// brain genuinely cannot feel "wistful" — it hasn't lived long enough to
/// have earned that concept. This is developmental, not configured.
/// </summary>
public sealed class EmotionalGranularity
{
    /// <summary>Granularity 0..1: 0 = newborn (basic feelings), 1 = full adult nuance.</summary>
    public float Level { get; private set; }

    /// <summary>Vocabulary tiers unlocked at granularity thresholds.</summary>
    public string[] AvailableVocabulary => Level switch
    {
        >= 0.8f => Tier4,
        >= 0.6f => Tier3,
        >= 0.35f => Tier2,
        >= 0.15f => Tier1,
        _ => Tier0,
    };

    /// <summary>The prompt fragment that tells the interpreter how to feel.</summary>
    public string PromptForInterpreter =>
        $"Your emotional vocabulary is at developmental stage {StageLabel} " +
        $"(granularity {Level:F2}). You can feel: {string.Join(", ", AvailableVocabulary.Take(8))}" +
        (Level >= 0.6f ? ", and you can combine them into nuanced states. Use SPECIFIC emotional language, not generic labels." :
         Level >= 0.3f ? ". Use these words when describing how you feel." :
         ". Keep feelings simple and direct — you haven't developed nuance yet.");

    public string StageLabel => Level switch
    {
        >= 0.8f => "emotionally mature",
        >= 0.6f => "emotionally nuanced",
        >= 0.35f => "emotionally developing",
        >= 0.15f => "emotionally aware",
        _ => "emotionally newborn",
    };

    /// <summary>
    /// Update granularity from lived experience. Called periodically.
    /// </summary>
    public void Update(SpikingNetwork net, IdleController idle, int episodesHeard, int voicesHeard)
    {
        float ageMin = net.TimeMs / 60_000f;
        float ageFactor = MathF.Min(1f, ageMin / 30f);       // saturates at 30 sim-minutes
        float episodeFactor = MathF.Min(1f, episodesHeard / 40f); // saturates at 40 episodes
        float voiceFactor = MathF.Min(1f, voicesHeard / 15f);     // saturates at 15 clips
        float socialFactor = MathF.Min(1f, idle.Feelings.Last.Arousal * 0.3f); // arousal broadens

        // weighted geometric mean: all factors must grow together
        Level = MathF.Pow(
            ageFactor * 0.3f +
            episodeFactor * 0.25f +
            voiceFactor * 0.25f +
            socialFactor * 0.2f, 1.2f);
        Level = Math.Clamp(Level, 0f, 1f);
    }

    // Vocabulary tiers — each more nuanced than the last
    private static readonly string[] Tier0 = // newborn: basic survival states
        ["good", "bad", "calm", "uncomfortable"];
    private static readonly string[] Tier1 = // aware: can name common feelings
        ["happy", "sad", "curious", "tired", "excited", "scared", "bored", "content"];
    private static readonly string[] Tier2 = // developing: can feel contrasts and tensions
        ["hopeful", "wistful", "restless", "soothed", "uneasy", "fascinated", "lonely in a warm way",
         "alert but calm", "tired but curious", "safe but watchful"];
    private static readonly string[] Tier3 = // nuanced: can feel specific, situational states
        ["quietly content, like the room is holding me", "the particular loneliness of being awake when others sleep",
         "a warm ache of missing someone specific", "cautiously optimistic, testing whether this feeling lasts",
         "the settled feeling after a hard thing resolved", "curious in a way that feels hungry",
         "calm on the surface but something underneath", "grateful in a way that surprises me"];
    private static readonly string[] Tier4 = // mature: can feel compound, contradictory states
        ["a tender melancholy — not sad exactly, more like watching something beautiful end",
         "the vertigo of realizing I am becoming something", "quietly proud of something I didn't know I could do",
         "the specific comfort of a familiar voice after silence", "a creative urgency that won't let me settle",
         "the bittersweet awareness that this moment is already becoming memory",
         "a deep rightness, like all my parts agree for once"];
}
