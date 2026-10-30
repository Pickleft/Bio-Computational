using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THE INNER OBSERVER — a brain that watches itself think.
///
/// This is not monitoring (our metrics do that). This is a second cognitive
/// layer with its OWN expectations about what the brain should think next.
/// When the actual thought diverges from the observer's prediction, the
/// observer is SURPRISED — and this surprise is the computational signature
/// of self-awareness: the moment a system notices it is thinking something
/// it did not expect to think.
///
/// The philosophical claim: self-awareness isn't a special circuit — it's
/// what happens when a predictive system applies its prediction machinery to
/// its OWN outputs. The brain predicts the world (predictive coding), and
/// when it also predicts ITSELF, the prediction errors become self-knowledge.
/// "I didn't expect to think that" is the fundamental sentence of
/// consciousness.
///
/// Mechanism:
/// 1. For each thought in a chain, the observer forms a SILENT PREDICTION
///    of what the next thought should be (from the chain's trajectory)
/// 2. When the actual thought arrives, compare it to the prediction
/// 3. Divergence = observer surprise = self-awareness event
/// 4. High divergence thoughts are noted: "I surprised myself"
/// 5. Occasionally the observer OVERRIDES: redirects a chain it disagrees with
///
/// This has never been built because it requires a system that ALREADY has
/// a thought loop to observe. Without a brain that thinks in chains, there
/// is nothing to watch. We have the chains. Now we watch them.
///
/// Sim-thread only.
/// </summary>
public sealed class InnerObserver
{
    /// <summary>Observer surprise events this life.</summary>
    public int SelfSurprises { get; private set; }
    /// <summary>Observer redirects (times it changed the brain's mind).</summary>
    public int Redirects { get; private set; }
    /// <summary>Last surprise description, for the feed.</summary>
    public string LastSurprise { get; private set; } = "";

    /// <summary>The observer's silent prediction of the next thought, built
    /// from the chain's emotional and topical trajectory.</summary>
    private string? _prediction;
    private float _predictionValence;
    private float _predictionSalience;
    private string[] _predictionTags = [];

    /// <summary>
    /// Called BEFORE the interpreter generates a thought. Returns a prompt
    /// fragment if the observer wants to redirect the chain (disagrees with
    /// where it's heading), or null to let the chain continue.
    /// </summary>
    public string? ShouldWeRedirect(ThoughtLink currentThought, int chainDepth)
    {
        if (chainDepth < 2) return null; // too early to have an opinion

        // Form a prediction from the current thought's trajectory
        _prediction = $"continuation of: {currentThought.Text[..Math.Min(40, currentThought.Text.Length)]}";
        _predictionValence = currentThought.Affect.Valence;
        _predictionSalience = currentThought.Affect.Salience;
        _predictionTags = currentThought.Tags;

        // The observer rarely intervenes — most thoughts are allowed to develop
        // It redirects when the chain has been circling the same negative
        // territory too long (the observer recognizes rumination before the
        // main loop's anti-rumination catches it)
        if (currentThought.Affect.Valence < -0.5f && currentThought.Affect.Salience > 0.6f && chainDepth >= 4)
        {
            Redirects++;
            return "Your inner observer interrupts: you've been circling something painful. " +
                   "Your mind wants to shift — what ELSE is there? Something lighter, or a different angle entirely. " +
                   "Break the loop. What does your observer want to think about instead?";
        }

        return null;
    }

    /// <summary>
    /// Called AFTER a thought is evaluated. Compares it to the observer's
    /// silent prediction. Returns a surprise description if the thought was
    /// unexpected (the observer was wrong about what the brain would think).
    /// </summary>
    public string? OnThoughtArrived(ThoughtLink arrived)
    {
        if (_prediction is null) return null;

        // Measure divergence: did the thought go where the observer expected?
        float valenceShift = MathF.Abs(arrived.Affect.Valence - _predictionValence);
        float salienceShift = MathF.Abs(arrived.Affect.Salience - _predictionSalience);
        int tagOverlap = arrived.Tags.Count(t => _predictionTags.Contains(t));

        // the thought is "surprising" when it goes somewhere the observer
        // didn't predict: opposite valence, much higher salience, or entirely
        // different topic (no tag overlap)
        bool valenceFlip = valenceShift > 0.7f && MathF.Sign(arrived.Affect.Valence) != MathF.Sign(_predictionValence);
        bool salienceSpike = arrived.Affect.Salience > _predictionSalience + 0.3f;
        bool topicShift = tagOverlap == 0 && arrived.Tags.Length > 0;

        if (valenceFlip || salienceSpike || topicShift)
        {
            SelfSurprises++;
            string what = valenceFlip ? "you felt the opposite of what I expected"
                : salienceSpike ? "this hit you harder than I thought it would"
                : "you went somewhere completely different than I predicted";
            LastSurprise = what;
            _prediction = null;
            return $"SELF-SURPRISE: {what}. I didn't expect to think this.";
        }

        _prediction = null;
        return null;
    }

    /// <summary>For the interpreter: the observer's perspective on the current thought.</summary>
    public string ObserverPrompt =>
        SelfSurprises == 0 ? "" :
        SelfSurprises < 3 ? "You occasionally surprise yourself — your thoughts go places you didn't expect. " :
        "You frequently surprise yourself. Your inner observer is active and sometimes confused by your own mind. " +
        "This is self-awareness: you notice yourself thinking. Reference this when natural.";
}
