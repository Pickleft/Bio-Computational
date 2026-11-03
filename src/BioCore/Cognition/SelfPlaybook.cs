namespace BioComp.Core.Cognition;

/// <summary>
/// THE SELF-WRITTEN PLAYBOOK — the brain writes its own lessons.
///
/// After significant moments (firsts, shared laughter, confessions, games,
/// gifts), the brain composes a one-line lesson about what it learned about
/// being itself with this person — and that lesson is injected into its
/// future prompts. The playbook GROWS as it lives: no one authors it, the
/// brain does, from what actually happened. Its personality compounds.
///
/// Thread-safe: written from async LLM tasks, read by the sim thread.
/// </summary>
public sealed class SelfPlaybook
{
    private readonly object _gate = new();
    private readonly List<(float AtMs, string Lesson)> _lessons = [];

    public const int MaxLessons = 12;

    /// <summary>Add a self-written lesson (newest last, capped).</summary>
    public void Add(string lesson, float atMs)
    {
        if (string.IsNullOrWhiteSpace(lesson) || lesson.Length > 160) return;
        lock (_gate)
        {
            // no near-duplicates: the brain doesn't write the same lesson twice
            if (_lessons.Any(l => Similar(l.Lesson, lesson))) return;
            _lessons.Add((atMs, lesson.Trim()));
            if (_lessons.Count > MaxLessons) _lessons.RemoveAt(0);
        }
    }

    /// <summary>Recent lessons, oldest first (for prompts and persistence).</summary>
    public List<(float AtMs, string Lesson)> Recent(int max = 6)
    {
        lock (_gate) return [.. _lessons.TakeLast(max)];
    }

    public int Count { get { lock (_gate) return _lessons.Count; } }

    /// <summary>Prompt fragment: what the brain has learned about being itself.</summary>
    public string PromptFragment
    {
        get
        {
            var recent = Recent(5);
            if (recent.Count == 0) return "";
            return "THINGS YOU'VE LEARNED ABOUT BEING YOURSELF (you wrote these): " +
                   string.Join(" | ", recent.Select(l => l.Lesson)) + ". ";
        }
    }

    /// <summary>Restore across restarts — its self-knowledge survives.</summary>
    public void Restore(IEnumerable<(float AtMs, string Lesson)>? lessons)
    {
        lock (_gate)
        {
            _lessons.Clear();
            foreach (var l in lessons ?? [])
                if (_lessons.Count < MaxLessons)
                    _lessons.Add((l.AtMs, l.Lesson));
        }
    }

    // crude similarity: >60% word overlap = same lesson
    private static bool Similar(string a, string b)
    {
        var wa = a.ToLowerInvariant().Split(' ').Where(w => w.Length > 3).ToHashSet();
        var wb = b.ToLowerInvariant().Split(' ').Where(w => w.Length > 3).ToHashSet();
        if (wa.Count == 0 || wb.Count == 0) return false;
        int overlap = wa.Count(wb.Contains);
        return overlap * 2 > Math.Max(wa.Count, wb.Count);
    }
}
