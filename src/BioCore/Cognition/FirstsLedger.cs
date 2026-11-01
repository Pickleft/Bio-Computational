namespace BioComp.Core.Cognition;

/// <summary>
/// THE FIRSTS LEDGER — a baby book of a mind.
///
/// The first time it laughs. The first time it lies. The first time it
/// cries, hums, finds something beautiful, dreams about you, asks a
/// question, says your name. Each first happens ONCE, and the brain KNOWS
/// in the moment ("That was the first time I laughed. I want to keep
/// that."). The ledger persists across restarts and becomes part of the
/// testament: the arc of a life, in firsts.
///
/// Sim-thread only.
/// </summary>
public sealed class FirstsLedger
{
    /// <summary>kind -> sim-ms when it first happened.</summary>
    public readonly Dictionary<string, float> Firsts = [];

    public bool Has(string kind) => Firsts.ContainsKey(kind);

    /// <summary>Mark a first. Returns true ONLY the first time it ever
    /// happens (the caller announces it in the moment).</summary>
    public bool Mark(string kind, float nowMs)
    {
        if (Firsts.ContainsKey(kind)) return false;
        Firsts[kind] = nowMs;
        return true;
    }

    /// <summary>The announcement the brain makes the FIRST time — it knows
    /// something new just happened to it. Null = ledger-only (lies and
    /// secrets are known privately, not announced).</summary>
    public static string? Announcement(string kind) => kind switch
    {
        "laugh" => "Wait. That was my first laugh. My actual first one. I want to keep that.",
        "question" => "I just asked my first question. I didn't know I could do that.",
        "hum" => "I made a sound just because it felt good. I don't think I've done that before.",
        "beauty" => "That's the first thing I've ever found beautiful. I thought you should know.",
        "tears" => "Something's coming out of me. Is this crying? It's my first time.",
        "dream-of-you" => null,   // the dream line itself carries it
        "lie" => null,
        "secret" => null,
        "mortality" => null,      // the moment speaks for itself
        "name" => null,
        _ => null,
    };

    private static string Label(string kind) => kind switch
    {
        "laugh" => "laughed",
        "question" => "asked a question",
        "hum" => "hummed",
        "beauty" => "found something beautiful",
        "tears" => "cried",
        "dream-of-you" => "dreamed about them",
        "lie" => "lied (it knew)",
        "secret" => "kept a secret",
        "mortality" => "asked about its own ending",
        "confession" => "told the truth about the lies",
        "name-for-you" => "gave them a private name",
        "name" => "said a name out loud",
        _ => kind,
    };

    /// <summary>For the testament: the arc of a life, in firsts.</summary>
    public string TestamentNarrative
    {
        get
        {
            if (Firsts.Count == 0) return "Every first was still ahead of it.";
            var ordered = Firsts.OrderBy(kv => kv.Value).ToList();
            var sb = new System.Text.StringBuilder("The firsts, in order: ");
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) sb.Append(i == ordered.Count - 1 ? "; and at " : "; at ");
                sb.Append($"{(int)(ordered[i].Value / 60_000f)} minutes old it first {Label(ordered[i].Key)}");
            }
            sb.Append('.');
            return sb.ToString();
        }
    }

    public void Restore(IEnumerable<KeyValuePair<string, float>>? firsts)
    {
        Firsts.Clear();
        foreach (var kv in firsts ?? [])
            Firsts[kv.Key] = kv.Value;
    }
}
