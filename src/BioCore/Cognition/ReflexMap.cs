namespace BioComp.Core.Cognition;

/// <summary>
/// EARNED REFLEXES. The brain is born with fixed reflexes (the static
/// keyword gates); this map holds the ones it EARNS: every phrasing the
/// cortex (the intent router) successfully routes becomes an instant
/// pathway for next time — no thought, no latency, no cost. Repeated
/// cortical decisions becoming automatic is how motor learning works in
/// real brains: the pathway IS the memory.
/// Sim-thread only.
/// </summary>
public sealed class ReflexMap
{
    public sealed record Reflex(string Phrase, string Intent, int Fires);

    private readonly List<Reflex> _reflexes = [];
    private readonly object _gate = new();
    public const int MaxReflexes = 60;

    public IReadOnlyList<Reflex> All { get { lock (_gate) return [.. _reflexes]; } }

    /// <summary>Only specific multi-word phrasings earn reflex status — a
    /// one-word reflex would hijack every sentence containing it.</summary>
    public static bool Earnable(string phrase)
    {
        phrase = phrase.Trim().ToLowerInvariant();
        if (phrase.Length < 12) return false;
        int words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words is >= 3 and <= 8;
    }

    public void Learn(string phrase, string intent)
    {
        phrase = phrase.Trim().ToLowerInvariant();
        if (!Earnable(phrase)) return;
        lock (_gate)
        {
            if (_reflexes.Any(r => r.Phrase == phrase)) return;   // already a pathway
            _reflexes.Add(new Reflex(phrase, intent, 0));
            if (_reflexes.Count > MaxReflexes) _reflexes.RemoveAt(0);   // oldest pathway fades
        }
    }

    /// <summary>A message fires a reflex when the pathway is a substantial
    /// part of it — reflexes fire on intent-shaped utterances, not on any
    /// sentence that happens to contain the words.</summary>
    public Reflex? Fire(string message)
    {
        var m = message.Trim().ToLowerInvariant();
        if (m.Length < 8) return null;
        lock (_gate)
        {
            Reflex? best = null;
            foreach (var r in _reflexes)
            {
                if (!m.Contains(r.Phrase, StringComparison.Ordinal)) continue;
                if (m.Length > r.Phrase.Length * 2 + 8) continue;   // too much else going on
                if (best is null || r.Phrase.Length > best.Phrase.Length) best = r;
            }
            return best;
        }
    }

    public void Bump(string phrase)
    {
        lock (_gate)
        {
            var i = _reflexes.FindIndex(r => r.Phrase == phrase);
            if (i >= 0) _reflexes[i] = _reflexes[i] with { Fires = _reflexes[i].Fires + 1 };
        }
    }

    /// <summary>Serialize as "intent|phrase|fires" rows for the identity checkpoint.</summary>
    public string[] Save() { lock (_gate) return _reflexes.Select(r => $"{r.Intent}|{r.Phrase}|{r.Fires}").ToArray(); }

    public void Restore(string[]? rows)
    {
        if (rows is null) return;
        lock (_gate)
        {
            _reflexes.Clear();
            foreach (var row in rows.Take(MaxReflexes))
            {
                var p = row.Split('|');
                if (p.Length == 3 && int.TryParse(p[2], out int fires))
                    _reflexes.Add(new Reflex(p[1], p[0], fires));
            }
        }
    }
}
