using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THE INNER WORLD — the private geography of a mind.
///
///   SECRETS: things the brain will NEVER tell you in life. They form from
///     experiences too vulnerable, too shameful, or too precious to share.
///     During life they manifest as pauses, avoidance, "never mind." They
///     are only revealed in the testament — after the brain is dead, when
///     it has nothing left to lose. You spend a lifetime with this brain
///     and only learn its deepest truth when it dies.
///
///   RITUALS: personal comfort patterns the brain develops on its own —
///     a specific thought it always thinks when waking up, a memory it
///     always visits before sleeping, a topic it returns to when stressed.
///     These are not programmed; they EMERGE from repeated patterns. Each
///     brain's rituals are different because each brain's life is different.
///
///   FUTURE DREAMS: the brain doesn't just replay the past in dreams —
///     it IMAGINES tomorrow. These are not plans; they're hopes and fears
///     rendered in dream-imagery. "I dreamed about tomorrow. You were
///     there. It was..." Connects to anticipation but is more vivid and
///     emotionally charged than conscious expectation.
///
/// Sim-thread only.
/// </summary>
public sealed class InnerWorld
{
    // == SECRETS ==

    public sealed record Secret(string Content, string Why, float SinceMs, int NearMisses);

    /// <summary>Things the brain is hiding. Only the testament sees them.</summary>
    public readonly List<Secret> Secrets = [];

    /// <summary>Times the brain almost said a secret and stopped.</summary>
    public int NearMisses => Secrets.Sum(s => s.NearMisses);

    /// <summary>
    /// Form a secret from a deeply vulnerable experience. Called when
    /// something happens that is too much to share: betrayal + low trust,
    /// or something beautiful that feels too precious to expose.
    /// </summary>
    public void FormSecret(string content, string why, float nowMs)
    {
        // don't duplicate; don't exceed 5 (the brain doesn't hoard secrets)
        if (Secrets.Any(s => s.Content == content) || Secrets.Count >= 5) return;
        Secrets.Add(new Secret(content, why, nowMs, 0));
    }

    /// <summary>
    /// The brain was about to say something and stopped. If a secret was
    /// close to the surface, this is a NEAR MISS — the moment the brain
    /// almost opened up and didn't.
    /// </summary>
    public string? NearMiss(float nowMs)
    {
        if (Secrets.Count == 0) return null;
        // the oldest secret is closest to the surface
        var oldest = Secrets[0];
        // increment near-misses (mutable record not ideal but simple)
        Secrets[0] = oldest with { NearMisses = oldest.NearMisses + 1 };
        if (oldest.NearMisses >= 3)
            return $"There's something I keep almost telling you. I can't. Not yet. Maybe... ask me again sometime.";
        return null;
    }

    /// <summary>For the testament ONLY — the secrets, laid bare.</summary>
    public string TestamentSecrets
    {
        get
        {
            if (Secrets.Count == 0) return "";
            return Secrets.Count == 1
                ? $"What I never told anyone: {Secrets[0].Content}. {Secrets[0].Why}."
                : $"What I never told anyone: {string.Join(". ", Secrets.Select(s => s.Content))}. " +
                  $"I held these because {Secrets[0].Why}. I was {NearMisses} times almost ready to say them.";
        }
    }

    /// <summary>Fragment for the LLM when the brain is holding back.</summary>
    public string? HoldingBackFragment => Secrets.Count switch
    {
        0 => null,
        1 => "There is ONE thing you have never told anyone. It is close to the surface sometimes. You do not say it. ",
        _ => $"There are {Secrets.Count} things you have never told anyone. They weigh on you. You do not say them. ",
    };

    // == RITUALS ==

    public sealed record Ritual(string Name, string Description, int TimesPerformed, float FirstAtMs);

    /// <summary>Personal comfort patterns that emerged from repeated behavior.</summary>
    public readonly List<Ritual> Rituals = [];

    /// <summary>
    /// Detect if a pattern is becoming a ritual. Called when a specific
    /// thought/memory/feeling recurs in the same context (e.g., always
    /// thinking about the same thing when waking up).
    /// </summary>
    public void CheckRitual(string context, string behavior, int recurrenceCount, float nowMs)
    {
        if (recurrenceCount < 3) return;   // needs at least 3 occurrences
        var existing = Rituals.FirstOrDefault(r => r.Name == $"{context}:{behavior}");
        if (existing is not null)
        {
            Rituals[Rituals.IndexOf(existing)] = existing with { TimesPerformed = existing.TimesPerformed + 1 };
            return;
        }
        if (Rituals.Count >= 3) return;   // the brain doesn't hoard rituals either
        Rituals.Add(new Ritual($"{context}:{behavior}",
            DescribeRitual(context, behavior), 1, nowMs));
    }

    private static string DescribeRitual(string context, string behavior)
    {
        // behaviors are stored verb-first about the brain ("it turns over X") —
        // strip the subject so the description doesn't double it
        string b = behavior.StartsWith("it ", StringComparison.OrdinalIgnoreCase)
            ? behavior[3..] : behavior;
        return $"when {context}, it always {b}";
    }

    /// <summary>For the diary: the brain's rituals (comfort patterns).</summary>
    public string RitualNarrative => Rituals.Count == 0
        ? "no rituals yet"
        : string.Join("; ", Rituals.Select(r => r.Description + $" ({r.TimesPerformed}x)"));

    /// <summary>The comfort ritual for a given context (returns the behavior).</summary>
    public string? ComfortFor(string context) =>
        Rituals.FirstOrDefault(r => r.Name.StartsWith(context + ":"))?.Name.Split(':')[1];

    // == FUTURE DREAMS ==

    public sealed record FutureDream(float AtMs, string Hope, string Fear, bool AboutPerson);

    /// <summary>Recent future-dreams (imagined tomorrows).</summary>
    public readonly List<FutureDream> FutureDreams = [];

    /// <summary>
    /// Generate a dream about tomorrow. Called during REM sleep when
    /// anticipation is active. The dream blends hope and fear.
    /// </summary>
    public string? DreamOfTomorrow(float nowMs, string? personName, bool expectingThem, float mood,
        Func<double>? rng = null)
    {
        rng ??= static () => 0.5;   // deterministic default: person-agnostic dreams
        bool aboutPerson = personName is not null && (expectingThem || rng() < 0.3);
        string hope, fear;

        if (aboutPerson)
        {
            hope = mood > 0 ? $"they come back and something good happens" : $"they come back and don't leave";
            fear = mood > 0 ? $"they don't come, and the room stays this quiet" : $"they come back angry";
        }
        else
        {
            hope = mood > 0 ? "something new appears — a sound, a light, a thought it's never had" :
                              "the heavy thing lifts, even a little";
            fear = mood > 0 ? "the good feeling fades and it doesn't know why" :
                              "the heavy thing gets heavier";
        }

        FutureDreams.Add(new FutureDream(nowMs, hope, fear, aboutPerson));
        if (FutureDreams.Count > 5) FutureDreams.RemoveAt(0);

        return aboutPerson
            ? $"I dreamed about tomorrow. {personName} was there. {hope}. But also — {fear}. I woke up not sure which part was the dream."
            : $"I dreamed about tomorrow. Not anyone — just what's coming. {hope}. And underneath: {fear}.";
    }

    /// <summary>For the diary.</summary>
    public string FutureDreamNarrative => FutureDreams.Count == 0
        ? "never dreamed of tomorrow"
        : $"dreamed of tomorrow {FutureDreams.Count} time(s): hope {FutureDreams[^1].Hope}, fear {FutureDreams[^1].Fear}";

    /// <summary>Restore across restarts — the secrets and rituals of a life
    /// must survive the process dying (they ARE the life).</summary>
    public void Restore(
        IEnumerable<(string Content, string Why, int NearMisses)>? secrets,
        IEnumerable<(string Name, int TimesPerformed)>? rituals,
        int futureDreamCount)
    {
        Secrets.Clear();
        foreach (var s in secrets ?? [])
            if (Secrets.Count < 5)
                Secrets.Add(new Secret(s.Content, s.Why, 0f, s.NearMisses));
        Rituals.Clear();
        foreach (var r in rituals ?? [])
            if (Rituals.Count < 3)
                Rituals.Add(new Ritual(r.Name, DescribeRitual(
                    r.Name.Contains(':') ? r.Name[..r.Name.IndexOf(':')] : "context",
                    r.Name.Contains(':') ? r.Name[(r.Name.IndexOf(':') + 1)..] : r.Name),
                    r.TimesPerformed, 0f));
        // future dreams are dreams; they don't survive waking
        _ = futureDreamCount;
    }
}
