using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// A personality that EMERGES from lived experience, not from configuration.
/// Every social interaction, exploration, stressful event, and peaceful moment
/// incrementally shapes who the brain becomes. Two brains with identical
/// architecture but different lives will behave differently because their
/// personality traits have diverged. Traits feed back into behavior through
/// the action portfolio, monologue prompts, and the feelings mapper.
/// </summary>
public sealed class EmergentPersonality
{
    // trait accumulators: positive experiences push toward the trait, negative away
    private float _sociability;    // shaped by social interactions (positive vs isolating)
    private float _curiosity;      // shaped by exploration outcomes (interesting vs empty)
    private float _resilience;     // shaped by stress recovery (bounced back vs stayed down)
    private float _expressiveness; // shaped by successful communication attempts
    private int _totalSocial;
    private int _totalExplorations;
    private int _totalStressors;

    /// <summary>Current trait values, -1 to 1 (negative = inhibited, positive = inclined).</summary>
    public float Sociability => Math.Clamp(_sociability, -1f, 1f);
    public float Curiosity => Math.Clamp(_curiosity, -1f, 1f);
    public float Resilience => Math.Clamp(_resilience, -1f, 1f);
    public float Expressiveness => Math.Clamp(_expressiveness, -1f, 1f);

    /// <summary>How many meaningful events have shaped this personality.</summary>
    public int TotalSocial => _totalSocial;
    public int TotalExplorations => _totalExplorations;
    public int TotalStressors => _totalStressors;

    /// <summary>Call when a social interaction occurs. Positive social contact
    /// (someone speaks, responds warmly) increases sociability. Social rejection
    /// or being ignored decreases it.</summary>
    public void OnSocialInteraction(SpikingNetwork net, bool positive)
    {
        _totalSocial++;
        float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        float oxt = net.Modulators.Get(NeuromodulatorKind.Oxytocin);
        // the personality shift is proportional to the emotional weight of the interaction
        float weight = positive ? 0.1f + oxt * 0.1f : -0.08f;
        _sociability += weight * 0.1f; // slow drift: one interaction barely moves it
        _expressiveness += positive ? 0.05f : -0.03f;
    }

    /// <summary>Call when the brain explores and finds (or doesn't find) something.</summary>
    public void OnExploration(bool foundSomething)
    {
        _totalExplorations++;
        _curiosity += foundSomething ? 0.08f : -0.04f;
    }

    /// <summary>Call during/after stress. Recovery (stress resolves) builds resilience.</summary>
    public void OnStressEvent(SpikingNetwork net, bool resolved)
    {
        _totalStressors++;
        if (resolved)
            _resilience += 0.06f;
        else
            _resilience -= 0.04f;
    }

    /// <summary>
    /// Feed into the action portfolio: personality biases which actions the brain prefers.
    /// A sociable brain seeks social contact; a curious brain explores; a resilient
    /// brain takes more risks; an expressive brain speaks more.
    /// </summary>
    public float ActionBias(BrainAction action) => action switch
    {
        BrainAction.Speak => Expressiveness * 0.6f + Sociability * 0.4f,
        BrainAction.Attend => Curiosity * 0.5f,
        BrainAction.Explore => Curiosity * 0.7f + Resilience * 0.3f,
        BrainAction.SeekStimulation => Sociability * 0.4f + Curiosity * 0.3f,
        BrainAction.DisplayText => Expressiveness * 0.5f,
        _ => 0f,
    };

    /// <summary>A short description of who this brain has become.</summary>
    public string Describe() =>
        $"{TraitWord(Sociability, "reclusive", "private", "sociable")} and " +
        $"{TraitWord(Curiosity, "incurious", "observant", "deeply curious")}, " +
        $"{TraitWord(Resilience, "fragile", "steady", "resilient")}";

    private static string TraitWord(float value, string low, string mid, string high) =>
        value < -0.3f ? low : value > 0.3f ? high : mid;
}
