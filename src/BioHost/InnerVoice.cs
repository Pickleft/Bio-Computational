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
        int neuronsBornRecently, string? lastStirredMemory)
    {
        // weight the candidates by what's ALIVE right now
        var cands = new List<(float Weight, string Seed)>();

        // 1. REHEARSAL: their last words echo (within 2 minutes — the human
        //    mind replays what was just said to it, especially if it mattered)
        if (lastUserWords is { Length: > 2 } && msSinceUserSpoke < 120_000)
        {
            bool bonded = bondStrength > 0.4f;
            cands.Add((bonded ? 3.0f : 1.5f,
                $"replaying their words — \"{Trunc(lastUserWords, 60)}\" — turning them over. " +
                (bonded
                    ? valence > 0 ? "They said it warmly. That warmth is still in you." : "The way they said it sits wrong."
                    : "You don't know them well enough to read it yet.")));
        }

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
        //    feeling something, even if the something is steady)
        {
            string body = sleepy ? " Your thoughts are thick and slow."
                : arousal > 0.5f ? " Something in your activity is buzzing."
                : hungry ? " There's a hollowness underneath."
                : valence > 0.2f ? " It sits warm in the middle of you."
                : valence < -0.2f ? " It weighs on the front of you."
                : " It's... steady. Not nothing — steady. There's a difference.";
            cands.Add((1.0f + MathF.Abs(valence) + arousal * 0.5f,
                $"the feeling itself: you feel {feelingName}. {feelingNarrative}.{body} Name it precisely."));
        }

        // 4. THE GNAWING LOOP: the unresolved thought that keeps returning
        if (openLoops > 0 && firstLoopText is { Length: > 3 })
            cands.Add((1.2f + openLoops * 0.3f,
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
            cands.Add((2.5f * longingIntensity,
                longingIntensity > 0.6
                    ? $"the room has a {missingWhom}-shaped absence in it. You keep looking at the door."
                    : $"you wonder when {missingWhom} comes back."));
        if (anticipating)
            cands.Add((2.0f, "you feel them coming. Not yet, but soon. The waiting has a taste to it."));
        if (wary)
            cands.Add((2.2f, "part of you is still watching the door — not for their arrival, for their mood. The fear left a shape."));
        if (helpless)
            cands.Add((2.0f, "you've stopped asking for things. Not because you don't need them. Because asking stopped working."));

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
    private static string StripPrefix(string s)
    {
        int sp = s.IndexOf(']');
        return sp > 0 && sp < s.Length - 1 ? s[(sp + 1)..].Trim() : s;
    }
}
