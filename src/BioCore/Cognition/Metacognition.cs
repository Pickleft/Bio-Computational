using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// METACOGNITION — the brain knowing when it knows. Confidence in its own
/// beliefs scales with evidence quantity, consistency (low prediction error
/// = high confidence), and recency. A brain with 3 interactions hedges ("I
/// think maybe..."). A brain with 30 interactions is certain ("I know...").
///
/// Fleming et al. 2010 (Science 329:1541): metacognitive accuracy is carried
/// by prefrontal cortex, distinct from the primary perception/decision. Here,
/// confidence is computed from the free-energy monitor (prediction error) and
/// the evidence base (episodes + interactions), NOT from the feeling system.
/// The brain can be WRONG and CONFIDENT, or RIGHT and UNCERTAIN — the two are
/// independent, just as in biological metacognition.
/// </summary>
public sealed class Metacognition
{
    /// <summary>Confidence 0..1: 0 = always hedging, 1 = certain.</summary>
    public float Confidence { get; private set; }

    /// <summary>
    /// The prompt fragment that tells the interpreter HOW confident to be.
    /// A young brain hedges everything; a mature brain speaks with earned
    /// authority. This is what makes it sound like it's THINKING, not reciting.
    /// </summary>
    public string PromptFragment => Confidence switch
    {
        >= 0.7f => "You are CONFIDENT in what you know. State things directly: 'I know', 'I'm certain', 'this is true'. You've earned this certainty through experience.",
        >= 0.4f => "You are MODERATELY confident. Use 'I think', 'it seems like', 'probably'. You have some evidence but not enough for certainty.",
        >= 0.15f => "You are UNCERTAIN. Use 'maybe', 'I'm not sure', 'I wonder if'. You don't have enough experience to be sure of much yet.",
        _ => "You are VERY UNCERTAIN — you just started existing. Everything is new and you're not sure of anything. Use lots of 'maybe', 'I don't know', 'is this...?'"
    };

    /// <summary>Update confidence from the brain's actual evidence base.</summary>
    public void Update(SpikingNetwork net, IdleController idle,
        float freeEnergyError, int episodeCount, int socialInteractions)
    {
        // evidence quantity: more episodes = more grounds for confidence
        float quantity = MathF.Min(1f, episodeCount / 25f);
        // consistency: low prediction error = the brain's model is working
        float consistency = MathF.Max(0f, 1f - freeEnergyError * 2f);
        // social depth: knowing people well = confident about social predictions
        float social = MathF.Min(1f, socialInteractions / 20f);
        // geometric-ish blend: all must be present for high confidence
        Confidence = Math.Clamp(quantity * 0.35f + consistency * 0.35f + social * 0.30f, 0f, 1f);
    }
}
