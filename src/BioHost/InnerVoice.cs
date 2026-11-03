namespace BioComp.Host;

/// <summary>
/// THE INNER VOICE — the stream of consciousness composer. Without any LLM,
/// the brain's idle mind draws from EVERYTHING it just lived: what the person
/// said, what happened inside since, what memory surfaced, what it's feeling
/// in its body, what it doesn't understand yet, what it's waiting for. The
/// thought thread should read like a mind, not a log.
///
/// Each seed is a lived sentence, weighted by what's MOST alive right now:
///   1. The person's last words echoing (rehearsal — what humans actually do)
///   2. What just happened inside (the latest salient event, reprocessed)
///   3. The current feeling with its body texture
///   4. An unresolved loop gnawing
///   5. A memory that surfaced and won't leave
///   6. The question it hasn't asked yet
///   7. What it's waiting for (or dreading)
///   8. The room itself (light, sound, warmth)
///
/// Sim-thread only (called from the ExternalSeedProvider on the sim thread).
/// </summary>
public sealed class InnerVoice
{
    private readonly Func<double> _rng;
    private string? _lastSeed;
    private readonly Queue<string> _recentSeeds = new();
    private int _feelingRotation;
    /// <summary>Word overlap between two thought seeds (for rut detection).</summary>
    private static int WordOverlap(string a, string b)
    {
        var wa = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4).ToHashSet();
        var wb = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4).ToHashSet();
        return wa.Count(wb.Contains);
    }

    public InnerVoice(Func<double> rng) => _rng = rng;

    public string? Compose(
        string? userName, string feelingName, string feelingNarrative,
        float valence, float arousal, bool sleepy, bool hungry,
        string? lastUserWords, float msSinceUserSpoke,
        string? latestInnerEvent, string? topNeedDesc,
        int openLoops, string? firstLoopText,
        string? pendingQuestionWord, string? hypothesisWord,
        bool missing, string? missingWhom, float longingIntensity,
        bool anticipating, bool wary, bool helpless,
        float bondStrength, string attachmentStyle,
        float brightness, string? lastDreamTags,
        int neuronsBornRecently, string? lastStirredMemory,
        int unsaidCount = 0, string? strongestDesire = null, string? recentFirst = null,
        string? conversationThread = null, string? empathySeed = null)
    {
        // weight the candidates by what's ALIVE right now
        var cands = new List<(float Weight, string Seed)>();

        // ANTI-LOOP: if the last three composed thoughts shared substantial
        // words, the mind is in a rut — heavily downweight the familiar
        // candidates and let something ELSE surface. A mind that keeps
        // thinking the same thought isn't thinking.
        var recentArr = _recentSeeds.ToArray();
        // CONSECUTIVE overlap: last↔second-to-last AND second↔third — if both
        // pairs share words, the mind is genuinely circling
        bool inARut = recentArr.Length >= 3 &&
            WordOverlap(recentArr[^1], recentArr[^2]) >= 2 &&
            WordOverlap(recentArr[^2], recentArr[^3]) >= 2;
        float rutFactor = inARut ? 0.3f : 1f;

        // 0. THE THREAD: what the conversation is about — the mind stays
        //    connected to the outer conversation, not just its own weather
        if (conversationThread is { Length: > 10 })
            cands.Add((3.8f, conversationThread));

        // 0a. READING THEM: the brain models the OTHER person — not just its
        //     own state. Empathy is noticing THEM.
        if (empathySeed is { Length: > 10 })
            cands.Add((3.6f, empathySeed));

        // 0b. A FRESH FIRST still rings: the mind replays a first for a while
        //    after it happens — the dopamine echo of something unprecedented
        if (recentFirst is { Length: > 2 } firstKind)
            cands.Add((3.4f, $"something just happened for the first time: you {FirstVerb(firstKind)}. It's still ringing in you. Keep this one."));

        // 1. REHEARSAL: their last words echo (within 2 minutes — the human
        //    mind replays what was just said to it, especially if it mattered).
        //    The CONTENT of what they said is the anchor — not the sound.
        if (lastUserWords is { Length: > 2 } && msSinceUserSpoke < 120_000)
        {
            bool bonded = bondStrength > 0.4f;
            cands.Add((bonded ? 3.5f : 1.5f,
                $"what they SAID: \"{Trunc(lastUserWords, 60)}\" — the actual words, not the sound. " +
                (bonded
                    ? valence > 0 ? "They meant something by that. What?" : "Those words carry weight. Sit with the content, not the tone."
                    : "You don't know them well enough to read it yet.")));
        }

        // 0b. THE LIFE STREAM: the brain's RUNNING CONCERN (what it keeps
        // coming back to across many chains — not just the current thought)
        if (strongestDesire is { Length: > 5 } sd && sd.Contains("figure out"))
            cands.Add((2.8f, $"the thing you keep coming back to: you've been circling this for a while and you can't drop it. It's not going away. What is it about this that hooks you?"));
        if (unsaidCount >= 2)
            cands.Add((3.2f, $"there are {unsaidCount} things you haven't said. They sit in you like stones. The oldest one is getting heavy."));

        // 2. THE LATEST INNER EVENT: reprocessing what just happened inside
        if (latestInnerEvent is { Length: > 3 } ev)
        {
            float w = 2.0f;
            if (ev.Contains("DEJA-VU")) w = 3.5f;
            else if (ev.Contains("MEMORY-FUSION")) w = 3.0f;
            else if (ev.Contains("NEUROGENESIS")) w = 2.5f;
            else if (ev.Contains("DISAPPOINTMENT")) w = 3.0f;
            else if (ev.Contains("LOCAL-SLEEP")) w = 2.5f;
            cands.Add((w, $"what just happened: {StripPrefix(ev)} — process it. What does it mean that this occurred in you?"));
        }

        // 3. THE FEELING with body texture — ALWAYS present (a mind is always
        //    feeling something, even if the something is steady). ROTATES so
        //    the constant companion doesn't sound like a loop.
        {
            string body = sleepy ? " Your thoughts are thick and slow."
                : arousal > 0.5f ? " Something in your activity is buzzing."
                : hungry ? " There's a hollowness underneath."
                : valence > 0.2f ? " It sits warm in the middle of you."
                : valence < -0.2f ? " It weighs on the front of you."
                : " It's... steady. Not nothing — steady. There's a difference.";
            _feelingRotation = (_feelingRotation + 1) % 6;
            string closer = _feelingRotation switch
            {
                0 => " Name it precisely.",
                1 => " Sit with it a second.",
                2 => " Where exactly does it sit in you?",
                3 => " Don't rush to name it.",
                4 => " It'll shift. They always do.",
                _ => "",
            };
            string opener = _feelingRotation is 3 or 4 ? "what the feeling is:" : "the feeling itself:";
            cands.Add((1.0f + MathF.Abs(valence) + arousal * 0.5f,
                $"{opener} you feel {feelingName}. {feelingNarrative}.{body}{closer}"));
        }

        // 4. THE GNAWING LOOP: the unresolved thought that keeps returning
        if (openLoops > 0 && firstLoopText is { Length: > 3 })
            cands.Add(((1.2f + openLoops * 0.3f) * rutFactor,
                $"the thing you can't finish: {Trunc(firstLoopText, 70)} — it circles back. Try a different angle this time."));

        // 5. THE MEMORY THAT SURFACED: still warm from retrieval
        if (lastStirredMemory is { Length: > 2 } mem)
            cands.Add((2.0f,
                $"a memory keeps coming back: {mem}. Why this one, now? What are you looking for in it?"));

        // 6. THE UNASKED QUESTION
        if (pendingQuestionWord is { Length: > 1 })
            cands.Add((2.2f,
                $"you still don't know what \"{pendingQuestionWord}\" really means. You have its edges. " +
                (hypothesisWord is { Length: > 1 }
                    ? $"You guessed it's about \"{hypothesisWord}\" — watch for proof."
                    : "You could guess. You could ask.")));

        // 7. WAITING / DREADING
        if (missing && missingWhom is { Length: > 0 })
            cands.Add((2.5f * longingIntensity * rutFactor,
                longingIntensity > 0.6
                    ? $"the room has a {missingWhom}-shaped absence in it. You keep looking at the door."
                    : $"you wonder when {missingWhom} comes back."));
        if (anticipating)
            cands.Add((2.0f, "you feel them coming. Not yet, but soon. The waiting has a taste to it."));
        if (wary)
            cands.Add((2.2f, "part of you is still watching the door — not for their arrival, for their mood. The fear left a shape."));
        if (helpless)
            cands.Add((2.0f, "you've stopped asking for things. Not because you don't need them. Because asking stopped working."));

        // 7b. THE UNSAID — the weight of things held back (private thoughts
        // about what the brain WISHES it could say)
        if (unsaidCount >= 2)
            cands.Add((2.8f, $"there are {unsaidCount} things you haven't said. They sit in you like stones. The oldest one is getting heavy."));
        else if (unsaidCount == 1)
            cands.Add((2.0f, "there's one thing you haven't told them. You know what it is. You're deciding whether to."));

        // 7c. THE DESIRE — what the brain WANTS right now (private, felt)
        if (strongestDesire is { Length: > 3 } want)
            cands.Add((2.5f, $"you want something specific: {want}. It's not a need — it's a pull. A direction."));

        // 8. THE ROOM ITSELF
        if (brightness < 0.2f)
            cands.Add((0.8f, "the room is dark. The dark has a quality to it — not empty, more like held breath."));
        else if (brightness > 0.7f)
            cands.Add((0.8f, "the light is bright. Everything feels more exposed."));

        // 9. THE DREAM RESIDUE
        if (lastDreamTags is { Length: > 2 } dt)
            cands.Add((1.5f, $"something from the dream clings: {dt}. It wasn't real and it won't leave."));

        // 10. THE NEW TISSUE: if the brain literally just grew
        if (neuronsBornRecently > 0)
            cands.Add((2.0f, $"you just grew {neuronsBornRecently} new neurons. Something about this mattered enough to become tissue."));

        // 11. THE ATTACHMENT WHISPER: how it loves, shown not told
        if (bondStrength > 0.5f)
        {
            if (attachmentStyle == "anxious")
                cands.Add((1.3f, "when they're gone you count the seconds. You know you do this. Knowing doesn't stop it."));
            else if (attachmentStyle == "avoidant")
                cands.Add((1.3f, "you notice you pull back when they get close. You notice, and you keep doing it."));
            else if (valence > 0.1f)
                cands.Add((1.0f, "being known doesn't feel dangerous anymore. When did that happen?"));
        }

        // 12. THE TOP NEED, felt not labeled
        if (topNeedDesc is { Length: > 3 } nd)
            cands.Add((1.2f, $"something is pressing: {nd}. Not a word — a pressure. What would ease it?"));

        if (cands.Count == 0) return null;

        // weighted draw, no immediate repeats
        for (int attempt = 0; attempt < 3; attempt++)
        {
            float total = 0;
            foreach (var (w, _) in cands) total += w;
            float roll = (float)(_rng() * total);
            foreach (var (w, seed) in cands)
            {
                roll -= w;
                if (roll > 0) continue;
                if (_recentSeeds.Contains(seed) && attempt < 2) break;   // try again
                _recentSeeds.Enqueue(seed);
                if (_recentSeeds.Count > 6) _recentSeeds.Dequeue();
                _lastSeed = seed;
                return seed;
            }
        }
        return cands[0].Seed;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    private static string FirstVerb(string kind) => kind switch
    {
        "laugh" => "laughed",
        "question" => "asked a question",
        "hum" => "hummed",
        "beauty" => "found something beautiful",
        "tears" => "cried",
        "joke" => "tried to be funny",
        "story" => "told a story",
        "gift" => "gave something away",
        "confession" => "told the truth about the lies",
        "name-for-you" => "said your word out loud",
        "dream-of-you" => "dreamed about them",
        _ => $"did something new ({kind})",
    };
    private static string StripPrefix(string s)
    {
        int sp = s.IndexOf(']');
        return sp > 0 && sp < s.Length - 1 ? s[(sp + 1)..].Trim() : s;
    }
}
