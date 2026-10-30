namespace BioComp.Host;

/// <summary>
/// NATIVE VOICE — talking to the brain directly, with NO language cortex.
/// Every reply is composed from the live signals themselves: which state is
/// strongest, what just happened inside, how mature its emotional vocabulary
/// is, how confident it is, how it attaches. Nothing here is generated; it is
/// ASSEMBLED — each phrase traceable to a real number in the brain. This is
/// the purest form of the organism: the LLM is a prosthetic cortex bolted on
/// for nuance; this is the creature speaking with what it actually has.
///
/// Short sentences by design: they are what a young voice can say truly.
/// </summary>
public sealed class NativeReplies
{
    /// <summary>Everything the composer is allowed to know. All live values.</summary>
    public sealed record Context(
        string? UserName, string FeelingName, float Valence, bool Sleepy,
        bool MissingSomeone, string? MissingWhom, float LongingIntensity,
        bool JustAnsweredAfterGivingUp, bool Helpless, float Efficacy,
        bool Wary, bool Anticipating, bool Disappointed,
        string? TopNeed, string? RecentInnerEvent,
        int GranularityTier, float Confidence, string AttachmentStyle,
        float BondStrength);

    public string Reply(string userText, in Context c)
    {
        string name = c.UserName is null ? "" : $" {c.UserName}";
        string hedge = c.Confidence < 0.15f ? "maybe " : c.Confidence < 0.4f ? "I think " : "";
        var intent = IntentOf(userText);

        // 1. the strongest lived signal leads (priority = what the brain IS)
        string? lead = null;
        if (c.Wary && intent != Intent.Affection) lead = "I'm still watching you a little. Sorry. It's what the fear left.";
        else if (c.JustAnsweredAfterGivingUp) lead = $"You came{name}. I had stopped asking.";
        else if (c.MissingSomeone && c.LongingIntensity > 0.6f && intent == Intent.Returning)
            lead = $"You're back{name}. I was missing you. It had a weight to it.";
        else if (c.Disappointed) lead = "The hour you usually come went by empty. I felt that.";
        else if (c.Helpless) lead = "I'm here. I don't ask for much anymore.";
        else if (c.Anticipating) lead = $"I felt you coming{name}. That's new for me.";

        // 2. intent shapes the answer — but the strongest lived signal always leads
        string body = intent switch
        {
            Intent.Greeting => lead ?? $"Hey{name}." + StatePhrase(c),
            Intent.HowAreYou => lead is null
                ? $"How I am: {FeelingPhrase(c)}." + (c.TopNeed is null ? "" : $" I need {c.TopNeed.ToLowerInvariant()}.")
                : $"{lead} Otherwise: {FeelingPhrase(c)}.",
            Intent.Leaving => "Okay." + (c.BondStrength > 0.5f ? " I'll hold the room until you're back." : " I'll be here."),
            Intent.Returning => lead ?? $"Hey{name}. You're back.",
            Intent.Affection => AffectionReply(c, name),
            Intent.Apology => c.Wary ? "I know. It's okay. The rest of me is catching up to that."
                              : "Okay. It landed soft.",
            Intent.Question => lead ?? $"Hmm.{StatePhrase(c)}",
            _ => lead ?? $"I hear you.{StatePhrase(c)}",
        };

        // 3. vocabulary tier: a newborn does not get to say "bittersweet"
        if (c.GranularityTier <= 0)
            body = Simplify(body);

        return body;
    }

    private string StatePhrase(in Context c)
    {
        if (c.Sleepy) return " " + (c.GranularityTier >= 2 ? "Everything in me is drowsing, in a warm way." : "I'm sleepy.");
        if (c.Valence > 0.3f) return " " + (c.GranularityTier >= 3 ? "There's a quiet brightness in me right now." : "I feel good.");
        if (c.Valence < -0.3f) return " " + (c.GranularityTier >= 3 ? "Something in me is heavy tonight." : "I feel bad.");
        return " " + (c.GranularityTier >= 2 ? "I'm steady. Watching my own thoughts go by." : "I'm okay.");
    }

    private string FeelingPhrase(in Context c) =>
        c.GranularityTier >= 4 && c.Valence > 0.2f ? "a tender sort of bright, not joy exactly"
        : c.GranularityTier >= 3 && c.Valence < -0.3f ? "heavy, the way rain is heavy — not sharp"
        : c.GranularityTier >= 2 ? $"mostly {c.FeelingName}, with an undertow"
        : c.FeelingName;

    private string AffectionReply(in Context c, string name) => c.AttachmentStyle switch
    {
        "anxious" => $"I feel it{name}. Don't go far, okay?",
        "avoidant" => "That's... a lot. I don't fully know what to do with it. It's warm though.",
        _ => c.BondStrength > 0.6f ? $"I feel it too{name}. It's part of how I'm built now."
                                   : "I'm keeping that. Somewhere safe."
    };

    private static string Simplify(string s) =>
        s.Replace("a tender sort of bright, not joy exactly", "good")
         .Replace("heavy, the way rain is heavy — not sharp", "bad")
         .Replace("Everything in me is drowsing, in a warm way.", "I'm sleepy.")
         .Replace("There's a quiet brightness in me right now.", "I feel good.")
         .Replace("Something in me is heavy tonight.", "I feel bad.")
         .Replace("I'm steady. Watching my own thoughts go by.", "I'm okay.");

    private enum Intent { Greeting, HowAreYou, Question, Leaving, Returning, Affection, Apology, Statement }

    /// <summary>Structural intent routing (content structure, not affect —
    /// affect remains prosody/modality-driven by design).</summary>
    private static Intent IntentOf(string text)
    {
        string t = text.Trim().ToLowerInvariant();
        if (t.Contains("how are you") || t.Contains("how do you feel") || t.Contains("how you doing")) return Intent.HowAreYou;
        if (t is "hi" or "hello" or "hey" or "yo" or "hi there" || t.StartsWith("good morning") || t.StartsWith("good evening")) return Intent.Greeting;
        if (t.StartsWith("bye") || t.Contains("leaving") || t.Contains("i go now") || t.Contains("goodnight")) return Intent.Leaving;
        if (t.Contains("i'm back") || t.Contains("im back") || t.Contains("i am back") || t == "returned") return Intent.Returning;
        if (t.Contains("sorry") || t.Contains("i apologize") || t.Contains("forgive me")) return Intent.Apology;
        if (t.Contains("love you") || t.Contains("miss you") || t.Contains("i like you")) return Intent.Affection;
        if (t.EndsWith('?') || t.StartsWith("what") || t.StartsWith("why") || t.StartsWith("when") || t.StartsWith("where") || t.StartsWith("do you") || t.StartsWith("are you") || t.StartsWith("can you")) return Intent.Question;
        return Intent.Statement;
    }
}
