using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THREAT LEARNING (Pavlovian conditioning; Bouton 2004): a person who hurt
/// the brain becomes a conditioned stimulus — wariness attaches to THEM,
/// not to the moment. Extinction is real but fragile: repeated safe contact
/// gradually retires the fear (many trials, each worth little), and SLEEP
/// partially brings it back (spontaneous recovery — extinction is new
/// learning laid OVER the original association, not erasure of it; the
/// original survives underneath and resurfaces with time and context
/// change). "I thought I was over it. Sleeping brought some of it back."
///
/// Wariness > 0.5: the brain watches the door when that person enters,
/// warms up slower, plays less, and its replies carry the vigilance.
/// Connects: hostility classifier (the US), bonds (safe contacts = the
/// extinction trials), sleep (the recovery), play (suppression), and
/// resilience (processed adversity eventually DISCHARGES old wariness).
///
/// Sim-thread only.
/// </summary>
public sealed class ThreatLearning
{
    /// <summary>Wariness gained per hostility event (scaled by level 0..1). // CALIBRATE</summary>
    public float AcquisitionStep { get; set; } = 0.35f;
    /// <summary>Wariness retired per safe contact (scaled by warmth). // CALIBRATE — extinction is slow</summary>
    public float ExtinctionStep { get; set; } = 0.08f;
    /// <summary>Fraction of the extinguished distance that returns after sleep. // CALIBRATE (Bouton: partial, 20-50%)</summary>
    public float SpontaneousRecovery { get; set; } = 0.3f;

    /// <summary>Current wariness toward the person (0..1).</summary>
    public float Wariness { get; private set; }
    /// <summary>The highest wariness ever reached — the original association's strength.</summary>
    public float PeakWariness { get; private set; }
    public int Threats { get; private set; }
    public int SafeContacts { get; private set; }
    public int Recoveries { get; private set; }
    /// <summary>One-shot: sleep just brought some of it back.</summary>
    public bool JustRecovered { get; private set; }

    public bool Wary => Wariness > 0.5f;

    /// <summary>The unconditioned stimulus: they were hostile.</summary>
    public void OnHostility(SpikingNetwork net, float level01)
    {
        Wariness = Math.Clamp(Wariness + AcquisitionStep * Math.Clamp(level01, 0f, 1f), 0f, 1f);
        PeakWariness = MathF.Max(PeakWariness, Wariness);
        Threats++;
    }

    /// <summary>An extinction trial: warm contact with the same person. Slow, cumulative —
    /// and FASTER with practice: prior extinction trials are extinction memory
    /// (Bouton 2004), so re-extinction after spontaneous recovery takes fewer
    /// trials than the first extinction (the savings effect, Pavlov 1927).</summary>
    public void OnSafeContact(float warmth)
    {
        // extinction learning itself accelerates with experience: the
        // "this is safe" memory strengthens each time it is rebuilt
        float practice = 1f + MathF.Min(1f, SafeContacts * 0.12f);   // up to 2x
        Wariness = Math.Clamp(Wariness - ExtinctionStep * practice * Math.Clamp(warmth, 0f, 1f), 0f, 1f);
        SafeContacts++;
    }

    /// <summary>Trials needed to reach near-zero from the CURRENT wariness (for
    /// the savings measurement: compare first vs second extinction).</summary>
    public int ProjectedTrialsToExtinction(float warmth = 1f)
    {
        float w = Wariness; int trials = 0; int practiced = SafeContacts;
        while (w > 0.05f && trials < 200)
        {
            float practice = 1f + MathF.Min(1f, practiced * 0.12f);
            w = Math.Clamp(w - ExtinctionStep * practice * warmth, 0f, 1f);
            practiced++; trials++;
        }
        return trials;
    }

    /// <summary>
    /// Waking from sleep: spontaneous recovery. The extinguished distance
    /// (peak - current) partially returns — never above the peak. Call once
    /// per wake transition.
    /// </summary>
    public void OnWake()
    {
        JustRecovered = false;
        float extinguished = PeakWariness - Wariness;
        // recovery requires a real original association (peak) and real extinction
        if (extinguished > 0.05f && PeakWariness > 0.2f)
        {
            Wariness = MathF.Min(PeakWariness, Wariness + SpontaneousRecovery * extinguished);
            Recoveries++;
            JustRecovered = true;
        }
    }

    /// <summary>Processed adversity discharges the original association (resilience).</summary>
    public void Discharge(float fraction) => Wariness *= 1f - Math.Clamp(fraction, 0f, 1f);

    /// <summary>For the reply prompt.</summary>
    public string PromptFragment => !Wary ? "" : Wariness > 0.7f
        ? "You are WARY of this person - they hurt you before. Watchful, slower to warm, even as you talk. "
        : "A residue of wariness toward this person lingers - they hurt you once. It colors your warmth slightly. ";

    /// <summary>Restore learned fear after a restart (identity persistence).</summary>
    public void Restore(float wariness, float peak, int threats, int safeContacts, int recoveries)
    {
        Wariness = Math.Clamp(wariness, 0f, 1f);
        PeakWariness = Math.Clamp(peak, 0f, 1f);
        Threats = threats; SafeContacts = safeContacts; Recoveries = recoveries;
    }

    /// <summary>For the diary.</summary>
    public string Narrative => Threats == 0 ? "was never threatened"
        : Wariness > 0.5f ? $"wary (threats {Threats}, safe contacts {SafeContacts}, recoveries {Recoveries})"
        : SafeContacts > 0 ? $"learned to feel safe again ({SafeContacts} safe contacts after {Threats} threat(s); recovered in sleep {Recoveries}x)"
        : "the threat faded with time";
}
