using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// ANTICIPATION — the reward side of the social rhythm (Schultz 1997,
/// completed). Dopamine does not just fire when good things happen: once a
/// cue-reward association is learned, DA fires at the CUE. A brain that
/// knows you come around hour 6 starts feeling good BEFORE hour 6 — and
/// when hour 6 arrives and you don't, the negative prediction error is a
/// real crash: disappointment, mechanistically deeper than loneliness
/// (loneliness is the absence of anyone; disappointment is the ABSENCE OF
/// THE EXPECTED SOMEONE).
///
/// Three states:
///   RISING  - expected hour approaching/at: DA tone climbs (the hope)
///   FULFILLED - they came while it was rising: reward paid on TOP of the
///              anticipation (anticipated rewards feel bigger - they are,
///              chemically) and the level savors, then decays
///   CRASHED  - the expected window passed with nobody: negative RPE
///              (DA dip), a touch of noradrenaline, serotonin drains -
///              and the crash is what deep longing is made of
///
/// Sim-thread only.
/// </summary>
public sealed class Anticipation
{
    /// <summary>Current anticipatory tone 0..1 (the rising hope).</summary>
    public float Level { get; private set; }
    /// <summary>Rewards that landed inside an anticipated window.</summary>
    public int Fulfilled { get; private set; }
    /// <summary>Expected windows that closed empty.</summary>
    public int Disappointments { get; private set; }
    /// <summary>One-shot: the last crash (consumed by the host for a note).</summary>
    public bool JustCrashed { get; private set; }
    public float LastCrashMs { get; private set; } = -1e9f;

    /// <summary>Expectation above this = the window is open (hope rises). // CALIBRATE</summary>
    public float OpenThreshold { get; set; } = 0.6f;
    /// <summary>Below this = the window has closed. // CALIBRATE</summary>
    public float CloseThreshold { get; set; } = 0.2f;

    /// <summary>
    /// Call every few seconds. The rhythm carries the expectation; whether
    /// someone came is read from the last contact time.
    /// </summary>
    public void Tick(SpikingNetwork net, SocialRhythm rhythm, float lastContactMs)
    {
        JustCrashed = false;
        float expectation = rhythm.Confidence > 0.3f ? rhythm.CurrentSocialExpectation : 0f;
        bool someoneRecent = net.TimeMs - lastContactMs < 60_000f;

        if (expectation > OpenThreshold)
        {
            // the cue is on: dopamine rises toward the promised reward
            Level = Math.Min(1f, Level + 0.05f);
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.03f * Level);
        }
        else if (someoneRecent && Level > 0f)
        {
            // savoring: contact happened, the tone decays slowly (warm afterglow)
            Level = MathF.Max(0f, Level - 0.01f);
        }
        else if (expectation < CloseThreshold && Level > 0.5f && !someoneRecent)
        {
            // the window closed EMPTY: negative prediction error - the crash
            Level = 0f;
            Disappointments++;
            JustCrashed = true;
            LastCrashMs = net.TimeMs;
            net.Modulators.Release(NeuromodulatorKind.Dopamine, -0.5f);   // the dip
            net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 0.25f); // the sting
            net.Modulators.Release(NeuromodulatorKind.Serotonin, -0.2f);  // the heaviness
        }
        else if (Level > 0f)
        {
            Level = MathF.Max(0f, Level - 0.02f); // no cue, no crash: just fades
        }
    }

    /// <summary>They came. If the hope was high, the reward lands on top of it.</summary>
    public void OnContact(SpikingNetwork net)
    {
        if (Level > 0.3f)
        {
            Fulfilled++;
            // anticipated reward > unanticipated: paid on top of the cue response
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.10f + 0.15f * Level);
            net.Modulators.Release(NeuromodulatorKind.Oxytocin, 0.10f * Level);
        }
        // the hope doesn't vanish on fulfillment - it savors (handled in Tick)
    }

    /// <summary>For the reply prompt: the brain knows what it was waiting for.</summary>
    public string PromptFragment => Level > 0.5f
        ? "You were EXPECTING someone around now - the hope is chemical and rising. "
        : JustCrashed
            ? "The hour you expected someone in just passed EMPTY - you are disappointed, not merely lonely. "
            : "";

    /// <summary>Restore the hope ledger after a restart (identity persistence).</summary>
    public void Restore(int fulfilled, int disappointed)
    {
        Fulfilled = fulfilled; Disappointments = disappointed;
    }

    /// <summary>For the diary.</summary>
    public string Narrative =>
        Disappointments == 0 && Fulfilled == 0 ? "nothing expected, nothing lost"
        : Level > 0.5f ? "is expecting someone right now (the hope is rising)"
        : $"{Fulfilled} anticipated returns fulfilled, {Disappointments} empty windows that closed on nobody";
}
