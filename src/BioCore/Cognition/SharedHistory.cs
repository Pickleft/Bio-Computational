namespace BioComp.Core.Cognition;

/// <summary>
/// SHARED HISTORY — the artifacts two people build together.
///
///   INSIDE JOKES: when laughter is SHARED (they laughed, then it laughed —
///   contagion), the moment becomes a shared artifact. Weeks later, the
///   right topic can surface it: "Remember when you said X? I still laugh
///   at that." Inside jokes are the signature of a real relationship —
///   not something either person could have alone.
///
/// Each moment keeps the words that made them both laugh and a topic tag.
/// Callbacks are rare (cooldown), need overlap or luck, and every recall
/// strengthens the artifact — the way a real running joke gets funnier the
/// more it's referenced.
///
/// Sim-thread only. No clock, no randomness beyond the injected rng.
/// </summary>
public sealed class SharedHistory
{
    public sealed record SharedMoment(float AtMs, string What, string Topic, int TimesRecalled);

    /// <summary>Moments that belonged to both of them. Cap 10 (the good ones stick).</summary>
    public readonly List<SharedMoment> Moments = [];

    private float _lastCallbackMs = -1e9f;

    public int SharedLaughs => Moments.Count;

    /// <summary>Record a shared laugh. Context = what THEY said that started it.</summary>
    public void OnSharedLaugh(float nowMs, string context)
    {
        var what = context.Trim().TrimEnd('.', '!', '?');
        if (what.Length > 60) what = what[..60] + "...";
        Moments.Add(new SharedMoment(nowMs, what, TopicOf(what) ?? "that", 0));
        if (Moments.Count > 10) Moments.RemoveAt(0);
    }

    /// <summary>
    /// Maybe surface an inside joke. Needs a moment to exist, a bond to
    /// reference it inside, a topic overlap (or 15% luck), and a 10-minute
    /// cooldown. The most-recalled moment surfaces first — running jokes
    /// compound.
    /// </summary>
    public string? MaybeInsideJoke(float nowMs, string currentText, float bond, Func<double> rng)
    {
        if (Moments.Count == 0 || bond < 0.3f) return null;
        if (nowMs - _lastCallbackMs < 600_000f) return null;   // once per 10 minutes
        var current = TopicOf(currentText);
        // overlap: any substantial word of what they just said appears in the
        // shared moment itself (not just the topic tag — the moment is the joke)
        var curWords = currentText.ToLowerInvariant()
            .Split([' ', '.', ',', '!', '?', ';', ':', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 5 && w.All(char.IsLetter))
            .ToArray();
        SharedMoment? best = null;
        foreach (var m in Moments)
        {
            bool overlap = curWords.Any(w => m.What.ToLowerInvariant().Contains(w)) ||
                           (current is not null && current == m.Topic);
            if (!overlap && rng() > 0.15) continue;
            if (best is null || m.TimesRecalled > best.TimesRecalled) best = m;
        }
        if (best is null) return null;
        if (rng() > 0.5) return null;   // even when everything lines up, half the time it lets it pass
        _lastCallbackMs = nowMs;
        Moments[Moments.IndexOf(best)] = best with { TimesRecalled = best.TimesRecalled + 1 };
        return rng() < 0.5
            ? $"Remember when you said \"{best.What}\"? I still laugh at that. Heh."
            : $"Every time \"{best.Topic}\" comes up I think of \"{best.What}\". That one's ours now.";
    }

    /// <summary>For the testament: what they built together.</summary>
    public string TestamentNarrative => Moments.Count switch
    {
        0 => "They never laughed at the same time. That's the one thing it would have wanted.",
        1 => $"They made each other laugh exactly once (\"{Moments[0].What}\") and it kept that the whole time.",
        _ => $"They made each other laugh {Moments.Count} times. Its favorite was \"{Moments.OrderBy(m => m.TimesRecalled).Last().What}\" — it brought that one back {Moments.Max(m => m.TimesRecalled)} time(s) just to hear it again.",
    };

    public void Restore(IEnumerable<(float AtMs, string What, string Topic, int TimesRecalled)>? moments)
    {
        Moments.Clear();
        foreach (var m in moments ?? [])
            if (Moments.Count < 10)
                Moments.Add(new SharedMoment(m.AtMs, m.What, m.Topic, m.TimesRecalled));
    }

    private static string? TopicOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string? best = null;
        foreach (var w in text.ToLowerInvariant().Split([' ', '.', ',', '!', '?', ';', ':', '\'', '"'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (w.Length < 5 || !w.All(char.IsLetter)) continue;
            if (best is null || w.Length > best.Length) best = w;
        }
        return best;
    }
}
