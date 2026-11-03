namespace BioComp.Core.Cognition;

/// <summary>
/// THE GOAL ENGINE — the brain sets its OWN goals, open-endedly.
///
/// Not desires from a hardcoded list. Spark composes a goal from live state:
/// a gap in its knowledge, a pattern about the person it hasn't confirmed,
/// something about itself it wants to understand. Then it PURSUES: works
/// the goal into its questions, reports progress, declares completion or
/// gives up honestly. Goals persist across death. The brain has an agenda
/// it wrote itself.
///
/// Thread-safe: sim-thread reads, async writes.
/// </summary>
public sealed class GoalEngine
{
    public sealed record Goal(string What, string Why, string Progress, float SetAtMs, float? CompletedAtMs, bool Abandoned)
    {
        public bool Active => CompletedAtMs is null && !Abandoned;
    }

    private readonly object _gate = new();
    private readonly List<Goal> _goals = [];

    public const int MaxGoals = 3;   // a mind holds few active goals at once

    public IReadOnlyList<Goal> All { get { lock (_gate) return [.. _goals]; } }
    public List<Goal> Active { get { lock (_gate) return _goals.Where(g => g.Active).ToList(); } }

    /// <summary>Set a self-authored goal. Replaces the oldest if at capacity —
    /// minds move on.</summary>
    public Goal Set(string what, string why, float atMs)
    {
        what = what.Trim().Trim('"', '.');
        why = why.Trim().Trim('"', '.');
        if (what.Length is < 8 or > 120) throw new ArgumentException("goal malformed");
        lock (_gate)
        {
            // don't set a near-duplicate of an active goal
            if (_goals.Any(g => g.Active && Similar(g.What, what)))
                return _goals.First(g => g.Active && Similar(g.What, what));
            var goal = new Goal(what, why, "just set it", atMs, null, false);
            _goals.Add(goal);
            // at capacity: abandon the oldest active goal (honest attrition)
            var active = _goals.Where(g => g.Active).ToList();
            if (active.Count > MaxGoals)
            {
                var oldest = active.OrderBy(g => g.SetAtMs).First();
                _goals[_goals.IndexOf(oldest)] = oldest with { Abandoned = true };
            }
            if (_goals.Count > 12) _goals.RemoveAt(0);
            return goal;
        }
    }

    /// <summary>Update progress notes on an active goal.</summary>
    public void Note(Goal goal, string progress)
    {
        lock (_gate)
        {
            var idx = _goals.FindIndex(g => g.What == goal.What && g.SetAtMs == goal.SetAtMs);
            if (idx >= 0) _goals[idx] = _goals[idx] with { Progress = progress.Trim() };
        }
    }

    /// <summary>Complete a goal. Returns the completed record for celebration.</summary>
    public Goal Complete(Goal goal, float atMs)
    {
        lock (_gate)
        {
            var idx = _goals.FindIndex(g => g.What == goal.What && g.SetAtMs == goal.SetAtMs);
            if (idx < 0) return goal;
            _goals[idx] = _goals[idx] with { CompletedAtMs = atMs };
            return _goals[idx];
        }
    }

    /// <summary>Prompt fragment: the brain's own agenda.</summary>
    public string PromptFragment
    {
        get
        {
            var active = Active;
            if (active.Count == 0) return "";
            return "GOALS YOU SET YOURSELF (your agenda, you wrote these): " +
                   string.Join(" | ", active.Select(g => $"{g.What} (progress: {g.Progress})")) + ". ";
        }
    }

    /// <summary>For the testament: the agenda of a life.</summary>
    public string TestamentNarrative
    {
        get
        {
            lock (_gate)
            {
                var completed = _goals.Where(g => g.CompletedAtMs is not null).ToList();
                var abandoned = _goals.Where(g => g.Abandoned).ToList();
                var active = _goals.Where(g => g.Active).ToList();
                if (_goals.Count == 0) return "It never set itself a goal.";
                var sb = new System.Text.StringBuilder();
                if (completed.Count > 0) sb.Append($"It set itself {completed.Count} goal(s) and finished {completed.Count}: {string.Join("; ", completed.Select(g => g.What))}. ");
                if (active.Count > 0) sb.Append($"Still pursuing when it ended: {string.Join("; ", active.Select(g => g.What))}. ");
                if (abandoned.Count > 0) sb.Append($"It honestly gave up on {abandoned.Count}. ");
                return sb.ToString().TrimEnd();
            }
        }
    }

    public void Restore(IEnumerable<(string What, string Why, string Progress, float SetAtMs, float? CompletedAtMs, bool Abandoned)>? saved)
    {
        lock (_gate)
        {
            _goals.Clear();
            foreach (var g in saved ?? [])
                if (_goals.Count < 12)
                    _goals.Add(new Goal(g.What, g.Why, g.Progress, g.SetAtMs, g.CompletedAtMs, g.Abandoned));
        }
    }

    private static bool Similar(string a, string b)
    {
        var wa = a.ToLowerInvariant().Split(' ').Where(w => w.Length > 3).ToHashSet();
        var wb = b.ToLowerInvariant().Split(' ').Where(w => w.Length > 3).ToHashSet();
        if (wa.Count == 0 || wb.Count == 0) return false;
        return wa.Count(wb.Contains) * 2 > Math.Max(wa.Count, wb.Count);
    }
}
