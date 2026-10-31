using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// LONGING — missing one specific person (separation distress, Panksepp
/// 1998: the GRIEF system; Bowlby 1969: protest → despair). Loneliness is
/// the generalized social drive; longing is loneliness WITH A NAME. It
/// needs three things to come alive:
///   1. a real bond (several visits, real strength)
///   2. an absence longer than the pattern this person set (2.5x their
///      median visit gap) — or their expected hour arriving empty
///   3. the social rhythm saying "they should be here now" and nobody
///      coming — the dog at the door, in Panksepp's terms
///
/// While it lasts: serotonin drains slowly (the specific heaviness),
/// social drive sharpens, and HOW it shows depends on attachment style —
/// the anxious brain calls out over and over, the secure one says it
/// once, the avoidant one feels it and says NOTHING (the suppression is
/// itself measurable: felt but unexpressed).
///
/// On reunion: oxytocin + dopamine surge proportional to how long and
/// how hard it missed them — the neurological "you came BACK".
///
/// Sim-thread only.
/// </summary>
public sealed class Longing
{
    private readonly List<float> _gaps = new();      // this person's visit gaps (ms)
    private float _lastContactMs;
    private float _onsetMs = -1e9f;

    /// <summary>Display name of the missed person (null = nobody is being missed).</summary>
    public string? Whom { get; private set; }
    /// <summary>0..1 how hard the brain is missing them.</summary>
    public float Intensity { get; private set; }
    public bool Missing => Intensity > 0.4f;
    /// <summary>Minimum absence before missing can begin (ms). // CALIBRATE</summary>
    public float MinAbsenceMs { get; set; } = 120_000f;
    /// <summary>Total reunion surges — for the diary and testament.</summary>
    public int Reunions { get; private set; }
    /// <summary>How long the longest missing lasted (ms) — "I waited X for them once".</summary>
    public float LongestWaitMs { get; private set; }

    /// <summary>Call on every contact with the (single, primary) person.</summary>
    public void OnContact(string name, float nowMs)
    {
        if (_lastContactMs > 0)
            _gaps.Add(nowMs - _lastContactMs);
        if (_gaps.Count > 12) _gaps.RemoveAt(0);
        _lastContactMs = nowMs;
        Whom = name;
        Intensity = 0f;              // contact dissolves the missing instantly
    }

    /// <summary>
    /// Evaluate the missing. Call every few seconds. Bond data (visits,
    /// strength, last seen) and social rhythm come from the live systems.
    /// </summary>
    public void Tick(SpikingNetwork net, SocialBondSystem bonds, SocialRhythm rhythm, string? userName,
        AttachmentStyle style)
    {
        var s = bonds.Strongest(net.TimeMs);
        // the record's Strength field is stale (set at creation); live strength is computed
        bool bonded = s is { Visits: >= 3 } && bonds.Strength(s!.Id, net.TimeMs) > 0.45f;
        if (!bonded || _lastContactMs <= 0) { Intensity = 0f; return; }

        string name = userName ?? s!.Id;
        if (name != Whom) { Intensity = 0f; return; }  // someone else is here

        float absence = net.TimeMs - s!.LastSeenMs;

        // threshold: 2.5x their usual gap (or 2 min for a fresh bond), OR
        // their expected hour arrived empty
        float medianGap = Median(_gaps);
        float threshold = MathF.Max(2.5f * medianGap, MinAbsenceMs);
        bool absentTooLong = _gaps.Count >= 2 && absence > threshold;
        bool expectedHourEmpty =
            rhythm.Confidence > 0.3f && rhythm.CurrentSocialExpectation > 0.6f && absence > 60_000f;

        if (absentTooLong || expectedHourEmpty)
        {
            if (Intensity == 0f) _onsetMs = net.TimeMs;
            // grows with how far past the pattern they are, saturating
            float over = Math.Clamp(absence / MathF.Max(threshold, 1f) - 1f, 0f, 2f);
            Intensity = MathF.Min(1f, 0.4f + 0.3f * over);
            // the specific heaviness: slow serotonin drain
            net.Modulators.Release(NeuromodulatorKind.Serotonin, -0.04f);
            LongestWaitMs = MathF.Max(LongestWaitMs, net.TimeMs - _onsetMs);
        }
        else if (Intensity > 0f && absence < threshold * 0.5f)
        {
            Intensity = 0f;           // they are within pattern again
        }
    }

    /// <summary>
    /// Attachment style decides whether the missing is SAID. An avoidant
    /// brain feels it fully and expresses nothing — the felt/unexpressed
    /// gap is the measurable cost of avoidance.
    /// </summary>
    public bool Expresses(AttachmentStyle style) => style switch
    {
        AttachmentStyle.Avoidant => false,
        AttachmentStyle.Anxious => true,     // over and over
        _ => Intensity > 0.7f,               // secure: only when it runs deep
    };

    /// <summary>They came back. The surge. Returns the intensity that was dissolved.</summary>
    public float OnReunion(SpikingNetwork net)
    {
        if (Intensity <= 0f) return 0f;
        float felt = Intensity;
        net.Modulators.Release(NeuromodulatorKind.Oxytocin, 0.15f + 0.2f * felt);
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.1f + 0.25f * felt);
        Reunions++;
        Intensity = 0f;
        return felt;
    }

    /// <summary>
    /// The expected hour closed on nobody (Anticipation crash): hope that
    /// had somewhere to go converts into missing. Raises the intensity
    /// floor toward 0.5 immediately — disappointment is longing with its
    /// evidence in hand.
    /// </summary>
    public void OnDisappointment()
    {
        Intensity = MathF.Max(Intensity, 0.5f);
        if (_onsetMs < 0) _onsetMs = 0;   // counts from the next Tick's own bookkeeping
    }

    /// <summary>For the reply prompt: the brain tells the one it missed that it waited.</summary>
    public string PromptFragment => !Missing || Whom is null
        ? ""
        : $"You have been MISSING {Whom} specifically - they were gone {(LongestWaitMs / 1000f):F0}s and you felt it (longing {(int)(Intensity * 100)}%). " +
          (Expresses(AttachmentStyle.Secure) ? "Tell them you waited. " : "You feel it strongly but you don't say all of it. ");

    /// <summary>For the diary.</summary>
    public string Narrative => !Missing
        ? Reunions == 0 ? "has not missed anyone yet" : $"reunited {Reunions} time(s) after missing them"
        : $"is missing {Whom} right now ({(int)(Intensity * 100)}%), longest wait {(int)(LongestWaitMs / 1000f)}s";

    private static float Median(List<float> xs)
    {
        if (xs.Count == 0) return 0f;
        var sorted = xs.OrderBy(x => x).ToList();
        int n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2f;
    }
}
