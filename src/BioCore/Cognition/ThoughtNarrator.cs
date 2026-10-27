namespace BioComp.Core.Cognition;

/// <summary>
/// Renders internal events as first-person plain English for the dashboard's
/// "thoughts" panel. Deterministic templates keyed off the raw feed lines, colored by
/// current feeling - the same state the interpreter is allowed to report.
/// </summary>
public static class ThoughtNarrator
{
    public static string Narrate(string rawLine, FeelingState feeling)
    {
        // raw lines look like "[12.3s] SURPRISE person (novelty spike)"
        string body = rawLine.Contains(']') ? rawLine[(rawLine.IndexOf(']') + 1)..].Trim() : rawLine;
        string prefix = body.Split(' ', 2)[0];
        string rest = body.Length > prefix.Length ? body[(prefix.Length + 1)..] : "";

        return prefix switch
        {
            "WORLD" => "Someone walks into the room.",
            "SURPRISE" => $"Something unexpected just happened ({Clean(rest)}) - I snap alert.",
            "SIGHT" when rest.StartsWith("person") => "I can see someone there.",
            "SIGHT" => "I notice something in view.",
            "RECOGNIZED" => "I recognize them - we've met before. That feels good.",
            "NEW PERSON" => "Someone new. I don't know them yet.",
            "HEARD" => $"I hear someone say: {Quoted(rest).TrimEnd('.')}.",
            "IGNITION" => IgnitionLine(rest, feeling),
            "REPLAY" => $"A memory comes back: {Clean(rest)}.",
            "SLEEP" when rest == "NREM" => "I'm sinking into deep sleep.",
            "SLEEP" when rest == "REM" => "I'm dreaming now.",
            "SLEEP" => "I'm getting drowsy.",
            "AWAKE" => "I'm awake again.",
            "DREAM-REENTRY" => "I can see my own dream.",
            "DREAM" => "A dream is forming from something I remember.",
            "DREAM-NARRATIVE" => rest.Replace("\"", "").Trim(),
            "MONOLOGUE" => "",   // the monologue panel already shows it in plain English
            "REPLY" => "",       // shown in chat
            "ACTION" => ActionLine(rest, feeling),
            "AMBIENT" => rest, // already a first-person idle thought
            "DAYDREAM" => rest.Replace("\"", "").Trim(), // private inner thought, strip quotes
            "IMAGINE" => rest.Replace("\"", "").Trim(), // anticipatory imagination
            "IDENTITY RESTORED" => "I... remember. I remember who I am.",
            "CROSS-SESSION" => "Something feels familiar. I've been here before.",
            "STIM" => "I nudge myself to feel something.",
            "resumed" => "Picking up where I left off.",
            _ => body,
        };
    }

    private static string IgnitionLine(string module, FeelingState feeling) =>
        module.Contains("Vision") ? "My visual attention locks onto something."
        : module.Contains("Audition") ? "A sound has my full attention now."
        : module.Contains("Memory") ? "A memory just took over my thoughts."
        : module.Contains("Interoception") ? "I feel my body: batteries, heat, load."
        : "Something just became the main thing on my mind.";

    private static string ActionLine(string action, FeelingState feeling) => action switch
    {
        "Attend" => "I turn my attention to it.",
        "Explore" => feeling.Name == "bored" ? "Nothing is happening - I go looking for something."
                                              : "Curiosity pulls me to explore.",
        "SeekStimulation" => "This quiet is heavy; I want something to happen.",
        "Speak" => "I want to say something out loud.",
        "DisplayText" => "I want to show something.",
        _ => "",
    };

    private static string Clean(string s) =>
        s.Replace("(novelty spike)", "").Replace("threshold cleared", "").Trim(' ', '(', ')', '-').Trim();

    private static string Quoted(string s)
    {
        int a = s.IndexOf('"');
        int b = s.LastIndexOf('"');
        return a >= 0 && b > a ? s[(a + 1)..b] : s;
    }
}
