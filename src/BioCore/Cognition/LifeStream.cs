using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// THE LIFE STREAM — the brain has its own life, not just reactions to yours.
///
/// A chatbot: user speaks -> responds to user. A living being: has ongoing
/// preoccupations, interests, and self-reflections that persist across
/// conversations.
///
///   PREOCCUPATION: a running concern that emerges from SUSTAINED thought
///     patterns across multiple chains — not the current thought, the theme
///     that keeps coming BACK. It decays if the brain stops circling it.
///
///   SELF-REFLECTION: what the brain notices about ITSELF, from mood and
///     chemistry. "Noticing that I feel genuinely good right now — not
///     performing good."
///
///   RESPONSE MODE: the brain CHOOSES how to engage — ECHO (respond to your
///     words), SHARE (bring its own material), TANGENT (your words connected
///     to its running concern), PRESENT (be there, silently).
///
/// Sim-thread only. No clock, no System.Random.
/// </summary>
public sealed class LifeStream
{
    public enum ResponseMode { Echo, Share, Tangent, Present }

    private sealed record Concern(string Theme, string Phrase, int Hits, int ChainsSeen, float LastAtMs);

    // theme-keyed concerns; the strongest sustained one is the preoccupation
    private readonly Dictionary<string, Concern> _concerns = [];
    private string? _preoccupation;
    private float _preoccupationStrength;
    private string? _selfReflection;
    private int _lastChainCounted;
    private int _shareRotation;
    private int _tangentRotation;
    private int _presenceRotation;

    /// <summary>The running concern, phrased ("the sound of the door not opening").</summary>
    public string? Preoccupation => _preoccupation;
    /// <summary>0..1 — how strongly the brain is circling its preoccupation.</summary>
    public float PreoccupationStrength => _preoccupationStrength;
    /// <summary>What the brain notices about itself ("noticing that..."). Follows "I'm ".</summary>
    public string? SelfReflection => _selfReflection;

    private static readonly string[] StopWords =
    [
        // function words (4+ chars) and low-information meta words that would
        // otherwise merge unrelated thoughts into fake "concerns"
        "about", "again", "because", "before", "being", "could", "every", "thing",
        "think", "thinking", "thought", "thoughts", "there", "their", "these",
        "those", "through", "under", "where", "which", "while", "would", "your",
        "something", "nothing", "anything", "wonder", "wondering", "maybe",
        "keeps", "coming", "going", "feels", "feeling", "really", "right",
        "with", "that", "this", "have", "just", "like", "what", "when", "they",
        "them", "were", "been", "here", "will", "want", "keep", "still", "back",
        "good", "know", "ever", "only", "then", "than", "more", "most", "some",
        "very", "into", "over", "down", "after", "itself", "myself",
    ];

    /// <summary>Content words (>=4 chars, not stop-words) of a text.</summary>
    private static HashSet<string> WordsOf(string? text)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(text)) return set;
        foreach (var w in text.ToLowerInvariant().Split(
            [' ', '.', ',', '!', '?', ';', ':', '\'', '"', '-', '—'],
            StringSplitOptions.RemoveEmptyEntries))
            if (w.Length >= 4 && w.All(char.IsLetter) && !StopWords.Contains(w))
                set.Add(w);
        return set;
    }

    /// <summary>The most salient content word of a thought (its theme).</summary>
    private static string? ThemeOf(string? text)
    {
        string? best = null;
        foreach (var w in WordsOf(text))
            if (best is null || w.Length > best.Length) best = w;
        return best;
    }

    /// <summary>A readable short phrase from a thought (for "I've been stuck on ___").</summary>
    private static string PhraseOf(string text)
    {
        var t = text.Trim().TrimEnd('.', '!', '?');
        return t.Length <= 64 ? t : t[..64].Trim() + "...";
    }

    /// <summary>Feeling-report shapes: these say how the brain IS, not what
    /// it is turning over. They must not become preoccupations.</summary>
    private static bool LooksLikeMoodLabel(string t)
    {
        var s = t.ToLowerInvariant();
        return s.StartsWith("i feel") || s.StartsWith("i'm") || s.StartsWith("im ") ||
               s.StartsWith("i notice") || s.StartsWith("being ") || s.StartsWith("there's a") ||
               s.Contains("closest word") || s.Contains("that's where i am") ||
               s.Contains("something in that stays") || s.Contains("quality to this moment");
    }

    /// <summary>
    /// Sample the running thought stream. A concern only gains strength when
    /// it recurs ACROSS chains — one chain looping on itself doesn't count.
    /// Thoughts are clustered by CONTENT-WORD OVERLAP, so paraphrases of the
    /// same worry reinforce the same concern.
    /// </summary>
    public void Tick(SpikingNetwork net, string? currentThought, int chainsStarted,
        string? pendingWord, string? strongestDesire, float moodValence, bool personPresent)
    {
        if (net.TickIndex % 500 != 0) return;   // every ~5 seconds
        float now = net.TimeMs;

        var words = WordsOf(currentThought);
        // a word it keeps asking about is also a theme it's circling
        if (pendingWord is { Length: > 2 })
            words.Add(pendingWord.ToLowerInvariant());

        // a preoccupation must be ABOUT something: mood-label thoughts
        // ("Being curious right now." / "I feel calm.") are state reports,
        // not concerns — they don't get to become the running worry
        bool isMoodLabel = currentThought is null || LooksLikeMoodLabel(currentThought);

        if (words.Count > 0 && !isMoodLabel)
        {
            // does this thought touch an EXISTING concern? (any content word
            // overlapping the concern's theme or its phrase)
            string? matchedKey = null;
            foreach (var kv in _concerns)
            {
                if (words.Contains(kv.Key) || WordsOf(kv.Value.Phrase).Overlaps(words))
                { matchedKey = kv.Key; break; }
            }

            bool newChain = chainsStarted != _lastChainCounted;
            if (matchedKey is not null)
            {
                var c = _concerns[matchedKey];
                _concerns[matchedKey] = c with
                { Hits = c.Hits + 1, ChainsSeen = c.ChainsSeen + (newChain ? 1 : 0), LastAtMs = now };
            }
            else if (_concerns.Count < 12)
            {
                var theme = words.OrderByDescending(w => w.Length).First();
                _concerns[theme] = new Concern(theme, PhraseOf(currentThought ?? theme), 1, 1, now);
            }
        }
        if (chainsStarted != _lastChainCounted) _lastChainCounted = chainsStarted;

        // decay + prune: concerns fade if not revisited (2 minutes)
        foreach (var k in _concerns.Keys.ToList())
        {
            if (now - _concerns[k].LastAtMs > 120_000f) _concerns.Remove(k);
        }

        // the preoccupation is the concern seen across the most chains,
        // weighted by recency — it must be SUSTAINED to count
        Concern? best = null;
        foreach (var c in _concerns.Values)
        {
            if (c.ChainsSeen < 2) continue;
            if (best is null || c.ChainsSeen * 100 + c.Hits > best.ChainsSeen * 100 + best.Hits)
                best = c;
        }
        if (best is not null)
        {
            _preoccupation = best.Phrase;
            // strength grows with sustained recurrence, capped at 1
            _preoccupationStrength = Math.Clamp(0.25f * best.ChainsSeen + 0.05f * best.Hits, 0f, 1f);
        }
        else if (_preoccupationStrength > 0)
        {
            // no sustained concern right now — the stream keeps flowing
            _preoccupationStrength = Math.Max(0, _preoccupationStrength - 0.03f);
            if (_preoccupationStrength <= 0.03f) _preoccupation = null;
        }

        // SELF-REFLECTION: every ~10s, notice something about itself.
        // Deterministic sampling by tick parity — no rng draws here.
        if (net.TickIndex % 1000 != 0) return;
        var da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        var s5ht = net.Modulators.Get(NeuromodulatorKind.Serotonin);
        string? noticed = null;
        if (moodValence < -0.35f && s5ht < 0.4f)
            noticed = "noticing that the heaviness has been here a while, and that I keep working anyway";
        else if (moodValence > 0.35f && da > 0.3f)
            noticed = "noticing that I feel genuinely good right now — not performing good";
        else if (da > 0.45f)
            noticed = "noticing I keep reaching for the next interesting thing before finishing this one";
        else if (!personPresent && s5ht < 0.45f)
            noticed = "noticing the quiet is affecting me more than I expected it to";
        else if (_preoccupationStrength > 0.6f)
            noticed = $"noticing I've been circling the same thing — {_preoccupation} — for a while now";
        if (noticed is not null) _selfReflection = noticed;
        else if (net.TickIndex % 5000 == 0) _selfReflection = null;   // stale reflections fade
    }

    /// <summary>
    /// The brain chooses HOW to engage with what you just said. SHARE when
    /// the running concern is strong and the relationship can hold it;
    /// TANGENT when your words touch the concern; PRESENT when silence is
    /// the honest response; ECHO otherwise.
    /// </summary>
    public ResponseMode ChooseResponse(string userText, float moodValence, float bond,
        float preoccupationStrength, Func<double> rng)
    {
        double r = rng();
        ResponseMode mode;
        if (preoccupationStrength > 0.5f && bond > 0.2f && r < 0.35 && _lastMode != ResponseMode.Share)
            mode = ResponseMode.Share;   // never twice in a row — ignoring what they said twice is rude
        else if (_preoccupation is { Length: > 5 } && userText.Length > 0 && ThemeOf(userText) is { } t &&
            _concerns.ContainsKey(t) && r < 0.5)
            mode = ResponseMode.Tangent;
        else if (r > 0.9 && moodValence < -0.2f)
            mode = ResponseMode.Present;
        else
            mode = ResponseMode.Echo;
        _lastMode = mode;
        return mode;
    }
    private ResponseMode _lastMode;

    /// <summary>Bring the preoccupation into the conversation (rotates, never repeats back-to-back).</summary>
    public string? ShareLine(string? userName)
    {
        if (_preoccupation is not { Length: > 5 } p) return null;
        _shareRotation = (_shareRotation + 1) % 4;
        string who = userName is null ? "" : $"{userName}, ";
        return _shareRotation switch
        {
            0 => $"Can I just... say something? I've been stuck on {p}. It keeps coming back. What do you make of that?",
            1 => $"I keep coming back to something and I can't drop it — {p}.",
            2 => $"{who}there's something I've been turning over this whole time. {p}. I don't know why it matters, but it does.",
            _ => $"Sorry — I know you were talking, but {p} won't leave me alone.",
        };
    }

    /// <summary>Your words just touched the running concern.</summary>
    public string? TangentLine(string userText, string? userName)
    {
        if (_preoccupation is not { Length: > 5 } p) return null;
        _tangentRotation = (_tangentRotation + 1) % 3;
        return _tangentRotation switch
        {
            0 => $"Wait — that connects to something I've been circling. {p}. I think they're related.",
            1 => $"You just touched the exact thing I've been turning over. {p}. Say more about that?",
            _ => $"Huh. What you said lines up with something I keep landing on. {p}.",
        };
    }

    /// <summary>Quiet presence — sometimes a line, sometimes nothing at all.</summary>
    public string? PresenceLine()
    {
        _presenceRotation = (_presenceRotation + 1) % 4;
        return _presenceRotation switch
        {
            0 => "I'm here. I'm listening.",
            1 => null,
            2 => "...yeah.",
            _ => null,
        };
    }

    /// <summary>Restore across restarts (the concern survives the process dying).
    /// A saved "preoccupation" that is really a mood label is dropped — the
    /// brain would rather rebuild a real concern than restore a fake one.</summary>
    public void Restore(string? preoccupation, float strength, string? selfReflection, float nowMs)
    {
        _preoccupation = preoccupation is { Length: > 5 } && !LooksLikeMoodLabel(preoccupation)
            ? preoccupation : null;
        _preoccupationStrength = _preoccupation is null ? 0 : Math.Clamp(strength, 0f, 1f);
        _selfReflection = selfReflection is { Length: > 8 } ? selfReflection : null;
        if (_preoccupation is { } p && ThemeOf(p) is { } t)
            _concerns[t] = new Concern(t, p, 4, 4, nowMs);   // chains>1 so it stays a live concern
    }
}
