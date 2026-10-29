using System.Text.Json.Serialization;

namespace BioComp.Core.Cognition;

/// <summary>
/// The heritable package: what passes from a brain to its child. Not the
/// memories, not the words, not the people - the SEED, the temperament
/// PREDISPOSITIONS, the attachment climate it will be predisposed to expect,
/// and the prosody of the voice it will be born tending toward. Everything
/// else the child must live into being itself.
/// </summary>
public sealed record BrainGenes(
    ulong Seed,
    float Sociability, float Curiosity, float Resilience, float Expressiveness,
    float AttachmentWarmth, float AttachmentResponsiveness, string AttachmentStyle,
    float VoicePitch, float VoiceRate,
    string Name, int LifeEpisodes, string PersonalityDescription)
{
    public string ParentName => (Name?.Length ?? 0) > 0 ? Name : "the parent";
}

public static class Genetics
{
    /// <summary>
    /// Heritability of temperament predispositions: the child is born halfway
    /// to the parent's character (behavioral genetics ballpark for personality
    /// is 0.4-0.6; Plomin 1990). Life moves it the rest of the way - or
    /// somewhere else entirely.
    /// </summary>
    public const float Heritability = 0.5f;

    /// <summary>A child's inborn predisposition from the parent's developed trait.</summary>
    public static float Inherit(float parentTrait) => parentTrait * Heritability;

    /// <summary>The child's seed: derived from the parent's, but its own.
    /// A deterministic one-way mix - the child is of the parent but not the parent.</summary>
    public static ulong ChildSeed(ulong parentSeed, ulong salt) =>
        parentSeed ^ (salt * 0x9E3779B97F4A7C15UL + 0x6A09E667F3BCC909UL);
}
