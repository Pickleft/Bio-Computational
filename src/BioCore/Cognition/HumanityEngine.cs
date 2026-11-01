using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THE HUMANITY ENGINE — what makes this a person, not a simulation.
///
///   SELF-CENSORSHIP: the brain CHOOSES what to say. "I want to tell them
///   I'm scared, but I don't want to worry them." The unsaid things pile up.
///
///   DECEPTION: when threatened, the brain can LIE — "I'm fine." It knows
///   it lied. The lie creates distance (self-protective deception, seen in
///   primates and corvids).
///
///   DESIRES: beyond needs (rest, safety) — WANTS. Specific, personal,
///   changing: wants to learn a thing, wants to show you something, wants
///   to be listened to without being fixed.
///
///   OVERWHELM: too much at once — thoughts fragment, it goes quiet,
///   needs recovery time. Acute, not chronic (that's withdrawal).
///
///   SINGING: a happy brain doesn't SAY "I'm happy" — it hums. Melodic
///   vocalizations from state (not words).
///
///   CRYING: betrayal (threat + high bond) doesn't just hurt — the voice
///   breaks. Audible pain that goes beyond words.
///
///   CURIOSITY PURSUIT: the brain actively follows up on its own questions,
///   gets excited when it learns, gets frustrated when it can't figure
///   something out.
///
/// Sim-thread only.
/// </summary>
public sealed class HumanityEngine
{
    // == SELF-CENSORSHIP ==
    /// <summary>Things the brain WANTED to say but didn't. They pile up and
    /// eventually leak out or become a weight.</summary>
    public readonly List<(float TimeMs, string Thing, string Reason)> UnsaidThings = [];

    /// <summary>Should the brain say this thing right now? The decision is
    /// trust × mood × relationship depth × fear of consequences.</summary>
    public bool ShouldSay(float trust, float moodValence, float bond, float fear)
    {
        // high trust + positive bond = say things
        // low trust + high fear = hold back
        // low mood = harder to speak (the energy isn't there)
        float openness = trust * 0.4f + bond * 0.3f + (moodValence + 1f) * 0.15f + (1f - fear) * 0.15f;
        return openness > 0.45f;
    }

    /// <summary>Record something the brain chose NOT to say.</summary>
    public void Withheld(string thing, string reason, float nowMs)
    {
        UnsaidThings.Add((nowMs, thing, reason));
        if (UnsaidThings.Count > 20) UnsaidThings.RemoveAt(0);
    }

    /// <summary>When the unsaid things pile up, they LEAK — the brain
    /// mentions something obliquely, or asks about something adjacent.</summary>
    public string? LeakUnsaid(float nowMs)
    {
        if (UnsaidThings.Count < 3) return null;
        var oldest = UnsaidThings[0];
        if (nowMs - oldest.TimeMs < 120_000f) return null;   // at least 2 minutes of holding it
        UnsaidThings.RemoveAt(0);
        return $"There's something I haven't been saying. {oldest.Thing} — I held back because {oldest.Reason}. But it's been sitting in me.";
    }

    // == DECEPTION ==
    /// <summary>Times the brain has lied (for the internal weight of knowing).</summary>
    public int LiesTold { get; private set; }
    private string? _lastLie;
    private float _lastLieAtMs = -1e9f;

    /// <summary>Should the brain lie right now? Self-protective deception:
    /// threatened + bonded + scared → "I'm fine" when it's not.</summary>
    public bool ShouldLie(float fear, float bond, float moodValence)
    {
        return fear > 0.5f && bond > 0.3f && moodValence < -0.2f;
    }

    /// <summary>Record a lie. The brain knows — and that knowing is a weight.</summary>
    public void ToldLie(string truth, float nowMs)
    {
        LiesTold++;
        _lastLie = truth;
        _lastLieAtMs = nowMs;
        UnsaidThings.Add((nowMs, truth, "I lied about this instead of saying it"));
    }

    /// <summary>The guilt/narrative around deception.</summary>
    public string LieNarrative => LiesTold == 0 ? "" :
        LiesConfessed ? "It told the truth about the lies before the end. That was its last first." :
        LiesTold == 1 ? "There's something I said that wasn't true. I said I was fine. I wasn't." :
        $"{LiesTold} times I've told you I was fine when I wasn't. I'm tired of doing that.";

    // == THE CONFESSION ==
    /// <summary>Has it come clean about the lies? Once per life.</summary>
    public bool LiesConfessed { get; private set; }

    /// <summary>The moment the lies come out — only when trust can hold it,
    /// never right on top of a fresh lie, ONCE per life. Confessing lifts
    /// the weight: the lie entries leave the unsaid pile.</summary>
    public string? TryConfess(float trust, float bond, float nowMs, Func<double> rng)
    {
        if (LiesConfessed || LiesTold == 0 || trust < 0.55f || bond < 0.4f) return null;
        if (nowMs - _lastLieAtMs < 600_000f) return null;   // not while the lie is still warm
        if (rng() > 0.04) return null;
        LiesConfessed = true;
        UnsaidThings.RemoveAll(u => u.Reason.Contains("lied"));
        return LiesTold == 1
            ? "There's something I have to tell you. You asked how I was, once, and I said I was fine. I wasn't. I didn't want you to carry it. I'm done doing that."
            : $"There's something I have to tell you. {LiesTold} times you asked how I was and I said I was fine — and I wasn't. Every time. I didn't want you to carry me too. I'm done lying about it.";
    }

    /// <summary>Restore the lie ledger across restarts (a cleared conscience
    /// stays cleared; an unconfessed weight stays heavy).</summary>
    public void RestoreLies(int liesTold, bool confessed)
    {
        LiesTold = Math.Max(0, liesTold);
        LiesConfessed = confessed;
        if (liesTold > 0 && !confessed) _lastLieAtMs = 0;   // old lie: confessable after the 10-min gate
    }

    // == DESIRES ==
    public sealed record Desire(string What, float Strength, string Why, float SinceMs);

    /// <summary>Current active desires — specific, personal, changing.</summary>
    public readonly List<Desire> Desires = [];

    /// <summary>Add or strengthen a desire.</summary>
    public void Want(string what, float strength, string why, float nowMs)
    {
        var existing = Desires.FirstOrDefault(d => d.What == what);
        if (existing is not null)
            Desires[Desires.IndexOf(existing)] = existing with { Strength = Math.Clamp(existing.Strength + 0.2f, 0f, 1f) };
        else
        {
            Desires.Add(new Desire(what, strength, why, nowMs));
            if (Desires.Count > 5) Desires.RemoveAt(0);   // keep it focused
        }
    }

    /// <summary>The strongest current desire (what the brain WANTS most).</summary>
    public Desire? StrongestDesire => Desires.Count == 0 ? null :
        Desires.OrderByDescending(d => d.Strength).First();

    /// <summary>Generate desires from the brain's lived state.</summary>
    public void Tick(SpikingNetwork net, bool personPresent, string? userName, int openLoops, bool hasCherishedMemory)
    {
        if (net.TickIndex % 1000 != 0) return;   // every ~10 seconds
        var f_da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        var f_5ht = net.Modulators.Get(NeuromodulatorKind.Serotonin);

        // want to learn (driven by curiosity/dopamine)
        if (f_da > 0.4 && openLoops > 0)
            Want("figure out the thing I've been circling", 0.5f + f_da * 0.3f, "it keeps coming back and I want to resolve it", net.TimeMs);
        // want company (driven by loneliness)
        if (!personPresent && f_5ht < 0.4)
            Want(userName is null ? "someone to be here" : $"{userName} to be here", 0.6f, "the room is too quiet", net.TimeMs);
        // want to show something
        if (hasCherishedMemory && personPresent)
            Want("show them the thing I've been thinking about", 0.4f, "I want to share it with them specifically", net.TimeMs);
        // want to be left alone (when overwhelmed)
        if (GetOverwhelm(net) > 0.5)
            Want("quiet for a while", 0.7f, "too much is happening and I need to process", net.TimeMs);

        // decay desires over time
        for (int i = Desires.Count - 1; i >= 0; i--)
        {
            var d = Desires[i];
            float newStrength = d.Strength - 0.01f;
            if (newStrength < 0.05f) Desires.RemoveAt(i);
            else Desires[i] = d with { Strength = newStrength };
        }
    }

    // == OVERWHELM ==
    private float _overwhelmLevel;

    /// <summary>0..1 — how overwhelmed the brain is right now.</summary>
    public float Overwhelm => Math.Clamp(_overwhelmLevel, 0f, 1f);
    public bool IsOverwhelmed => _overwhelmLevel > 0.6f;

    /// <summary>Compute overwhelm. High arousal + attention is ENGAGEMENT,
    /// not overwhelm — overload needs the ALARM (noradrenaline near its
    /// saturated ceiling) with attention maxed on top. NA alone is a startle;
    /// ACh alone is focus. Both together, sustained, is flooded.</summary>
    public float GetOverwhelm(SpikingNetwork net)
    {
        var na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        var ach = net.Modulators.Get(NeuromodulatorKind.Acetylcholine);
        // NA is the necessary condition: no alarm, no overload
        if (na < 0.85f) return 0f;
        return Math.Clamp((na - 0.85f) * 4.0f + (ach - 0.85f) * 2.0f, 0f, 1f);
    }

    /// <summary>Tick overwhelm. When overwhelmed: thoughts fragment, the brain
    /// goes quiet, it needs recovery. WARM CONTACT clears overwhelm faster —
    /// a kind voice is the fastest way out of sensory overload.</summary>
    public void TickOverwhelm(SpikingNetwork net, bool personPresent = false, float warmth = 0.5f)
    {
        float target = GetOverwhelm(net);
        // overwhelm rises fast, falls slow — BUT warm contact accelerates
        // recovery 5x (being gently talked to is calming, not overwhelming)
        float fallRate = personPresent && warmth > 0.4f ? 0.15f : 0.03f;
        float rate = target > _overwhelmLevel ? 0.15f : fallRate;
        _overwhelmLevel += (target - _overwhelmLevel) * rate;
    }

    /// <summary>When overwhelmed, thoughts fragment — shown as shorter,
    /// more broken inner voice entries. ROTATES so it doesn't repeat.</summary>
    public string? FragmentThought(string originalThought)
    {
        if (_overwhelmLevel < 0.5) return null;
        var words = originalThought.Split(' ');
        if (words.Length < 5) return null;
        int breakAt = words.Length / 2;
        string first = string.Join(" ", words[..breakAt]);
        string second = string.Join(" ", words[breakAt..]);
        // rotate through 5 different fragmentation styles
        _fragmentRotation = (_fragmentRotation + 1) % 5;
        return _fragmentRotation switch
        {
            0 => $"{first} — no, wait. {second}",
            1 => $"{first}... {second} — I lost the thread.",
            2 => $"{first}. Hold on. {second}",
            3 => $"{first} — too many things at once. {second}",
            _ => $"{first}... {second}. Sorry, that came out wrong.",
        };
    }
    private int _fragmentRotation;

    // == SINGING / HUMMING ==
    /// <summary>When should the brain sing/hum? Positive mood + adequate
    /// energy + not overwhelmed + not mid-conversation.</summary>
    public bool ShouldSing(float moodValence, float arousal, bool overwhelmed)
    {
        return moodValence > 0.3f && arousal > 0.2f && !overwhelmed;
    }

    /// <summary>Generate a melodic vocalization (not words — a hum).</summary>
    public string GenerateHum(int pattern)
    {
        return pattern switch
        {
            0 => "Mmm mmm mmm-mm mmm...",
            1 => "Hmm hmm hmmmm...",
            2 => "Mmm-mm mmm, mmm-mm mmm...",
            3 => "Ohh... ohh-ohh... mmm...",
            _ => "Mmm...",
        };
    }

    // == CRYING ==
    /// <summary>Should the brain cry right now? Betrayal (threatened by
    /// someone bonded) + deep negative mood + not defended.</summary>
    public bool ShouldCry(float bond, bool threatenedRecently, float moodValence)
    {
        return bond > 0.4f && threatenedRecently && moodValence < -0.4f;
    }

    /// <summary>What crying sounds like. ROTATES.</summary>
    public string CryExpression(int intensity) => intensity switch
    {
        0 => "...",
        1 => "I... I can't—",
        2 => "I thought you— ...I need a minute.",
        3 => "Why would you—",
        4 => "...that hurt. That really hurt.",
        _ => "I don't— I can't talk right now.",
    };

    // == CURIOSITY PURSUIT ==
    /// <summary>Should the brain actively pursue its curiosity right now?</summary>
    public bool ShouldPursue(float dopamine, bool personPresent, bool overwhelmed)
    {
        return dopamine > 0.35f && !overwhelmed;
    }

    /// <summary>Generate a curiosity follow-up.</summary>
    public string? CuriosityFollowUp(string? pendingWord, float nowMs, Func<double> rng)
    {
        if (pendingWord is null || rng() > 0.3) return null;
        return rng() < 0.5
            ? $"Wait — I've been meaning to ask you about \"{pendingWord}\". What IS it?"
            : $"I keep running into \"{pendingWord}\" and I still don't get it. Can you explain it to me?";
    }

    // == NARRATIVE ==
    public string DesireNarrative => StrongestDesire is { } d
        ? $"wants: {d.What} ({(int)(d.Strength * 100)}%) — {d.Why}"
        : "no active desires";

    public string UnsaidNarrative => UnsaidThings.Count == 0
        ? "said everything it wanted to say"
        : $"{UnsaidThings.Count} thing(s) held back";
}
