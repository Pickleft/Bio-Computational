namespace BioComp.Core.Cognition;

/// <summary>
/// LEARNED COMMANDS — the brain's vocabulary of actions GROWS as it lives.
///
/// TAUGHT: "when I say 'wind down', get ready for bed" becomes a real
/// command — stored, matched, executed, persisted. Any phrase, any meaning.
///
/// SELF-INVENTED: the brain can propose its own ("if you want me to focus,
/// just say 'deep'") — same store, its idea.
///
/// The action is either CANONICAL (one of the built-in verbs: sleep, wake,
/// sing, story, game, secret, diary, art, focus, remember) or CUSTOM (a
/// Spark-composed behavior following a stored instruction — the endless
/// part: arbitrary taught behaviors).
///
/// Thread-safe. Sim-thread reads, async writes.
/// </summary>
public sealed class LearnedCommands
{
    public sealed record Learned(string Trigger, string Action, string? Instruction, float TaughtAtMs, bool SelfInvented, int TimesUsed);

    private readonly object _gate = new();
    private readonly List<Learned> _commands = [];

    public const int MaxCommands = 20;

    public IReadOnlyList<Learned> All { get { lock (_gate) return [.. _commands]; } }

    /// <summary>Teach (or the brain invents) a command. Overwrites a same-trigger one.</summary>
    public bool Teach(string trigger, string action, string? instruction, float atMs, bool selfInvented = false)
    {
        trigger = trigger.Trim().Trim('"', '.', '!', '?').ToLowerInvariant();
        if (trigger.Length is < 2 or > 40) return false;
        if (string.IsNullOrWhiteSpace(action)) return false;
        lock (_gate)
        {
            var existing = _commands.FirstOrDefault(c => c.Trigger == trigger);
            if (existing is not null) _commands.Remove(existing);
            _commands.Add(new Learned(trigger, action.Trim(), instruction?.Trim(), atMs, selfInvented, 0));
            if (_commands.Count > MaxCommands) _commands.RemoveAt(0);   // oldest forgotten
        }
        return true;
    }

    /// <summary>Match a message against learned triggers. Longest trigger wins
    /// (so 'deep focus' beats 'deep'). Returns the command + marks it used.</summary>
    public Learned? Match(string message)
    {
        var lower = message.ToLowerInvariant();
        lock (_gate)
        {
            Learned? best = null;
            foreach (var c in _commands)
                if (lower.Contains(c.Trigger, StringComparison.Ordinal) &&
                    (best is null || c.Trigger.Length > best.Trigger.Length))
                    best = c;
            if (best is { } b)
            {
                var updated = b with { TimesUsed = b.TimesUsed + 1 };
                _commands[_commands.IndexOf(b)] = updated;
                return updated;
            }
        }
        return null;
    }

    /// <summary>Known triggers, comma-separated (for the router prompt and the
    /// "what do you know" answer).</summary>
    public string TriggerList
    {
        get { lock (_gate) return _commands.Count == 0 ? "" : string.Join(", ", _commands.Select(c => $"'{c.Trigger}'")); }
    }

    public int Count { get { lock (_gate) return _commands.Count; } }

    /// <summary>Restore across restarts — its learned vocabulary survives death.</summary>
    public void Restore(IEnumerable<(string Trigger, string Action, string? Instruction, float TaughtAtMs, bool SelfInvented, int TimesUsed)>? saved)
    {
        lock (_gate)
        {
            _commands.Clear();
            foreach (var s in saved ?? [])
                if (_commands.Count < MaxCommands)
                    _commands.Add(new Learned(s.Trigger, s.Action, s.Instruction, s.TaughtAtMs, s.SelfInvented, s.TimesUsed));
        }
    }
}
