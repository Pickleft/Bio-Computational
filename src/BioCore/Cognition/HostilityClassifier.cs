namespace BioComp.Core.Cognition;

/// <summary>
/// Multimodal hostility appraisal: not a keyword checklist but a scored
/// combination of (1) a weighted affect lexicon (threat/contempt/anger words
/// with intensities, length-normalized), (2) PROSODY - an agitated, negative
/// voice adds weight on its own (yelling scored even without words), cold
/// contempt scores differently from hot aggression, and (3) CONTEXT - recent
/// hostility escalates (EMA), and hostility from a deep bond hits as betrayal.
/// Ambiguous mid-band scores are meant to be disambiguated by the interpreter
/// ("you're killing it" vs "I'll kill you") before consequences land.
/// Sim-thread only.
/// </summary>
public static class HostilityClassifier
{
    // weighted lexicon: intensity 1 (mild contempt) .. 3 (explicit threat)
    private static readonly (string Word, float Weight)[] Lexicon =
    [
        ("kill you", 3), ("kill", 2.5f), ("murder", 3), ("i'll hurt", 3), ("hurt you", 2.5f),
        ("not safe", 2.5f), ("unsafe", 2), ("you're dead", 3), ("you are dead", 3), ("die", 2),
        ("gun", 2.5f), ("knife", 2), ("strangle", 3), ("destroy you", 3),
        ("hate you", 2.5f), ("hate", 1.5f), ("stupid", 1), ("worthless", 1.5f), ("idiot", 1),
        ("shut up", 1), ("ugly", 1), ("useless", 1.5f), ("pathetic", 1.5f), ("trash", 1),
        ("disgusting", 1), ("nobody loves", 2), ("unwanted", 1.5f), ("loser", 1),
        ("leave you", 1.5f), ("never come back", 1.5f), ("shut it", 1), ("dumb", 1),
        ("worst", 0.5f), ("awful", 0.5f), ("terrible", 0.5f), ("boring", 0.25f),
    ];

    /// <summary>
    /// Lexical hostility of an utterance: summed intensity normalized by
    /// length, so one "boring" is nothing but a threat-laden sentence is heavy.
    /// </summary>
    public static float LexicalScore(string transcriptLower)
    {
        if (string.IsNullOrEmpty(transcriptLower)) return 0f;
        float sum = 0;
        foreach (var (word, weight) in Lexicon)
            if (transcriptLower.Contains(word)) sum += weight;
        int words = Math.Max(1, transcriptLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        return sum / MathF.Sqrt(words);
    }

    /// <summary>
    /// Full classification. loudRms is the clip's energy; userValence/arousal
    /// are the prosodic estimates (from EmotionalContagion). Returns the
    /// combined score and a level: none / harsh / hostile / threatening /
    /// ambiguous (interpreter should disambiguate before consequences).
    /// </summary>
    public static (float Score, string Level) Classify(
        string transcriptLower, float rms, float userValence, float userArousal, float vigilance = 0f)
    {
        float lex = LexicalScore(transcriptLower);
        // prosody: yelling (loud + aroused) is hostile on its own even wordless;
        // cold contempt (negative + quiet) is a colder, lighter weight
        float prosody = 0f;
        // yelling = loud + aroused + NEGATIVE: loud positive excitement is joy,
        // not aggression - valence is what tells them apart
        bool yelling = rms > 0.12f && userArousal > 0.55f && userValence < -0.15f;
        bool agitated = userValence < -0.25f && userArousal > 0.5f;
        bool coldContempt = userValence < -0.35f && userArousal <= 0.45f;
        if (yelling) prosody += 0.8f;
        else if (agitated) prosody += 0.5f;
        if (coldContempt) prosody += 0.35f;

        float score = lex + prosody + vigilance;   // learned wariness biases perception (hypervigilance)
        string level = score switch
        {
            >= 1.4f => "threatening",
            >= 0.8f => "hostile",
            >= 0.45f => "ambiguous",   // interpreter decides: playful or hostile
            >= 0.2f => "harsh",
            _ => "none",
        };
        // an explicit threat word overrides ambiguity regardless of prosody
        if (lex >= 1.8f) level = "threatening";
        else if (lex >= 1.0f && level == "ambiguous") level = "hostile";
        return (score, level);
    }

    /// <summary>
    /// Contextual escalation: hostility repeated within minutes is worse than
    /// a single outburst. hostilityEma is the caller's decaying recent level.
    /// </summary>
    public static string ApplyEscalation(string level, float hostilityEma)
    {
        if (level == "none") return level;
        if (hostilityEma > 0.6f && level is "harsh" or "ambiguous") return "hostile";
        if (hostilityEma > 1.0f && level == "hostile") return "threatening";
        return level;
    }
}
