namespace BioComp.Core.Cognition;

/// <summary>
/// A STRUCTURED THOUGHT - the user's founding idea, made real: thoughts are
/// not bare text but mental objects with typed parts. CONCLUSION is what the
/// thought actually claims (and the only part that chains forward).
/// FEELING is the felt-sense of the thought, in its own words. MATH is its
/// own arithmetic - a simple two-number expression it computes itself, and
/// getting a valid computation right pays a small salience bonus (precision
/// feels good; this is the smallest possible bridge between thought and the
/// curiosity reward). MEMORY names the memories the thought leans on, and
/// each successful recall of them strengthens those traces (spacing effect).
/// Parsing is tolerant: any missing part degrades gracefully; a completely
/// unstructured reply still works as a plain thought.
/// </summary>
public sealed record StructuredThought(
    string Conclusion, string Feeling, string? MathExpression, float? MathResult,
    string[] MemoryTags)
{
    public static StructuredThought FromPlain(string text) => new(text, "", null, null, []);

    /// <summary>Tolerant parse of the four-line thought format.</summary>
    public static StructuredThought Parse(string reply)
    {
        string conclusion = "", feeling = ""; string? math = null; var mems = new List<string>();
        foreach (var raw in reply.Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                conclusion = line["CONCLUSION:".Length..].Trim();
            else if (line.StartsWith("FEELING:", StringComparison.OrdinalIgnoreCase))
                feeling = line["FEELING:".Length..].Trim();
            else if (line.StartsWith("MATH:", StringComparison.OrdinalIgnoreCase))
                math = line["MATH:".Length..].Trim();
            else if (line.StartsWith("MEMORY:", StringComparison.OrdinalIgnoreCase))
                mems.AddRange(line["MEMORY:".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 1));
        }
        if (conclusion.Length == 0)
            conclusion = reply.Trim(); // unstructured reply: whole thing is the thought
        float? result = math is null ? null : EvalSimple(math);
        return new StructuredThought(conclusion, feeling, math, result, [.. mems.Take(3)]);
    }

    /// <summary>
    /// Its own arithmetic: one binary expression "a + b", "a - b", "a * b",
    /// "a / b" with plain numbers. Returns null for anything else - a brain
    /// that cannot compute something says so rather than inventing it.
    /// </summary>
    public static float? EvalSimple(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;
        foreach (var op in new[] { '+', '-', '*', '/' })
        {
            int i = expr.IndexOf(op);
            if (i > 0 && i < expr.Length - 1)
            {
                var left = expr[..i].Trim(); var right = expr[(i + 1)..].Trim();
                // strip trailing junk after the second number (units, words)
                right = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? right;
                if (float.TryParse(left, out float a) && float.TryParse(right, out float b))
                {
                    if (op == '/' && MathF.Abs(b) < 1e-6f) return null; // no invented division by zero
                    return op switch { '+' => a + b, '-' => a - b, '*' => a * b, _ => a / b };
                }
            }
        }
        return null;
    }
}
