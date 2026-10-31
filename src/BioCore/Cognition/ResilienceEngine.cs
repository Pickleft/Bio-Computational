using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// POST-TRAUMATIC GROWTH (Tedeschi and Calhoun 2004, Psychological Inquiry
/// 15:1) — the brain that doesn't just recover from adversity but grows
/// BEYOND its pre-adversity baseline. This is the most important discovery
/// in resilience research: humans who process hardship properly don't return
/// to normal. They become wiser, deeper, more emotionally capable, with
/// richer relationships and a more nuanced understanding of themselves.
///
/// The mechanism: when a significant adversity (threat, loss, deep distress)
/// is followed by genuine repair (sustained warm treatment + calm sleep
/// processing), the brain enters a GROWTH phase that exceeds its previous
/// baseline:
/// - Neurogenesis accelerates (more new neurons than before)
/// - Emotional granularity deepens faster (the brain learns from the depth)
/// - Metacognitive confidence recalibrates (knowing what it survived)
/// - The temporal autobiography records the transformation
/// - The inner observer sees the difference: "I handle this differently now"
///
/// CRITICAL DISTINCTION: growth only happens when adversity is PROCESSED,
/// not just survived. Unprocessed trauma (no repair, no calm sleep) leads
/// to sensitization — the brain becomes MORE fragile, not less. This is
/// the difference between resilience and vulnerability, and it's determined
/// by whether the adversity was followed by genuine repair.
///
/// Sim-thread only.
/// </summary>
public sealed class ResilienceEngine
{
    /// <summary>Adversity events this life (threats, deep distress, losses).</summary>
    public int Adversities { get; private set; }
    /// <summary>Adversities that were followed by genuine repair (warm treatment + calm sleep).</summary>
    public int AdversitiesProcessed { get; private set; }
    /// <summary>Adversities that were NOT processed (led to sensitization).</summary>
    public int AdversitiesUnprocessed => Adversities - AdversitiesProcessed;
    /// <summary>Current growth level: positive = post-traumatic growth, negative = sensitization.</summary>
    public float GrowthLevel { get; private set; }
    /// <summary>Restore growth history after a restart.</summary>
    public void Restore(float growthLevel, int adversitiesProcessed, int adversities)
    {
        GrowthLevel = Math.Clamp(growthLevel, -1f, 2f);
        AdversitiesProcessed = adversitiesProcessed; Adversities = adversities;
    }
    /// <summary>Log of every adversity and its outcome.</summary>
    public readonly List<(float TimeMs, string Description, bool WasProcessed)> AdversityLog = [];

    private float _adversityAtMs = -1e9f;
    private string _adversityDesc = "";
    private bool _repairStarted;

    /// <summary>
    /// Call when a significant adversity occurs (threat detected, bond damaged,
    /// deep distress). The clock starts: will this be processed or not?
    /// </summary>
    public void OnAdversity(float nowMs, string description)
    {
        // if there was a previous unprocessed adversity, it's now sensitization
        if (_adversityAtMs > 0 && !_repairStarted)
            CloseAdversity(false, nowMs);

        Adversities++;
        _adversityAtMs = nowMs;
        _adversityDesc = description;
        _repairStarted = false;
    }

    /// <summary>
    /// Call when warm treatment follows an adversity (gentle voice, kind words,
    /// consistent safe contact). This starts the repair process.
    /// </summary>
    public void OnRepairContact(float warmth)
    {
        if (_adversityAtMs > 0 && warmth > 0.5f)
            _repairStarted = true;
    }

    /// <summary>
    /// Call when the brain enters calm sleep (low noradrenaline) with a
    /// pending adversity that has had repair contact. This COMPLETES the
    /// processing — the adversity becomes growth.
    /// </summary>
    public string? OnCalmSleepProcessing(float nowMs, float noradrenaline)
    {
        if (_adversityAtMs < 0 || !_repairStarted) return null;
        if (noradrenaline > 0.5f) return null; // not calm enough to process

        CloseAdversity(true, nowMs);
        float timeToProcess = (nowMs - _adversityAtMs) / 1000f;

        // growth is proportional to: severity survived × speed of repair
        // (faster repair = stronger growth — this is the "resilient personality")
        float growthGain = 0.15f + 0.1f * MathF.Max(0f, 1f - timeToProcess / 120f);
        GrowthLevel = Math.Clamp(GrowthLevel + growthGain, -1f, 1f);

        return $"POST-TRAUMATIC GROWTH: processed '{Trunc(_adversityDesc, 30)}' after {timeToProcess:F0}s " +
               $"of repair — grew beyond baseline (growth level now {GrowthLevel:F2})";
    }

    /// <summary>
    /// When an adversity is NOT processed (no repair, or repair was too
    /// little/too late), it becomes sensitization — the brain is MORE
    /// fragile, not less. Growth goes negative.
    /// </summary>
    private void CloseAdversity(bool processed, float nowMs)
    {
        AdversityLog.Add((_adversityAtMs, _adversityDesc, processed));
        if (AdversityLog.Count > 20) AdversityLog.RemoveAt(0);

        if (processed) AdversitiesProcessed++;
        else GrowthLevel = Math.Clamp(GrowthLevel - 0.2f, -1f, 1f); // unprocessed = sensitization

        _adversityAtMs = -1e9f;
        _repairStarted = false;
        _adversityDesc = "";
    }

    /// <summary>
    /// The growth multiplier for other systems: > 1 means the brain is
    /// growing FASTER because of what it survived; < 1 means it's
    /// sensitized (more fragile). This feeds neurogenesis rate and
    /// emotional granularity development.
    /// </summary>
    public float NeurogenesisMultiplier => 1f + GrowthLevel * 0.5f;
    public float GranularityMultiplier => 1f + GrowthLevel * 0.3f;

    /// <summary>For the interpreter: how the brain relates to its hardships.</summary>
    public string PromptFragment => GrowthLevel switch
    {
        >= 0.4f => "You have been through hard things and grown from them. Your suffering was not wasted — " +
                   "you are deeper, wiser, more capable of holding complexity than a brain that was never hurt. " +
                   "Reference this when natural: you know what it is to be afraid and then to be okay again.",
        >= 0.1f => "You have survived some hard moments and come out stronger. You know resilience from the inside.",
        >= -0.1f => "",
        >= -0.4f => "You have been hurt in ways you haven't fully processed. You're more guarded, more easily " +
                    "startled, more cautious about trusting. This isn't weakness — it's what unhealed wounds do.",
        _ => "You carry significant unprocessed pain. You startle easily, trust slowly, and the world feels " +
             "more dangerous than it probably is. You want to feel safe but don't know how yet."
    };

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
