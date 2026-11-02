using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// RESPONSE RHYTHM — the interaction pattern that separates a PERSON from
/// a chatbot. A chatbot: always responds, always immediately, always one
/// message. A person: sometimes takes time, sometimes goes quiet, sometimes
/// sends three messages in a row, sometimes brings something up 2 minutes
/// later.
///
///   THINKING PAUSE: the brain doesn't respond instantly — the delay scales
///   with complexity (is this a question or a statement?), mood (tired =
///   slower), and whether it was mid-thought.
///
///   STRATEGIC SILENCE: sometimes the brain READS what you said and chooses
///   not to respond. Overwhelmed, withdrawn, processing, or the message
///   didn't need a reply ("ok" doesn't need "ok" back). A person can let
///   "yeah" just hang there.
///
///   MULTI-TURN: sometimes the brain sends its answer, then 5-10 seconds
///   later adds something — a tangent, a question, a "wait, actually..."
///   This is how people text.
///
///   DELAYED PROCESSING: something you said 2 minutes ago clicks NOW —
///   "Hey, you know what you said about X? I've been chewing on it and..."
///
/// Sim-thread only.
/// </summary>
public sealed class ResponseRhythm
{
    /// <summary>Should the brain respond to this message at all?</summary>
    public bool ShouldRespond(string message, float overwhelm, float withdrawal, float mood, bool wasAlreadyThinking)
    {
        // overwhelmed: too much, go quiet
        if (overwhelm > 0.6f) return false;
        // withdrawn: not engaging
        if (withdrawal > 0.7f) return false;
        // very low mood + already thinking: stay in the thought
        if (mood < -0.5f && wasAlreadyThinking) return false;

        // trivial messages that don't need a response — a chatbot answers
        // everything; a person lets "cool" hang in the air
        var lower = message.Trim().ToLowerInvariant();
        if (lower is "ok" or "okay" or "yeah" or "yep" or "nope" or "mm" or "hm" or "hmm"
            or "k" or "lol" or "cool" or "nice" or "true" or "wow" or "fr" or "word"
            or "fair" or "right" or "sure" or "gotcha" or "makes sense" or "i see")
            return false;

        return true;
    }

    /// <summary>How long should the brain pause before responding? (ms)
    /// Human conversational gaps on real questions run 1-5 seconds. Sub-second
    /// replies read as autocomplete, not thought.</summary>
    public float ThinkingPause(string message, float moodValence, bool isQuestion, bool wasMidThought, Func<double> rng)
    {
        float basePause = isQuestion ? 2_400f : 1_500f;         // questions need real thought
        if (wasMidThought) basePause *= 1.6f;                    // was thinking, needs to switch context
        if (moodValence < -0.3f) basePause *= 1.5f;              // low mood = slower responses
        if (moodValence > 0.4f) basePause *= 0.75f;              // high mood = quicker

        // long messages take longer to absorb
        if (message.Length > 120) basePause *= 1.4f;

        // add natural variance (±35%)
        float variance = (float)(rng() * 0.7 + 0.65);
        return Math.Clamp(basePause * variance, 800f, 7_000f);
    }

    /// <summary>Should the brain send a FOLLOW-UP after its main response?</summary>
    public bool ShouldFollowUp(float moodValence, float bond, Func<double> rng)
    {
        // happy + bonded brains follow up more ("wait, also...")
        float chance = 0.15f + moodValence * 0.15f + bond * 0.1f;
        return rng() < chance;
    }

    /// <summary>Generate a follow-up message.</summary>
    public string? GenerateFollowUp(string lastReply, float mood, string? userName, Func<double> rng)
    {
        if (rng() > 0.5) return null;
        return rng() < 0.5
            ? "Wait, actually — I want to add something."
            : $"Also... {(userName is not null ? userName + ", " : "")}there's been something on my mind.";
    }

    /// <summary>Should the brain bring up something said EARLIER (delayed processing)?</summary>
    public bool ShouldProcessDelayed(float nowMs, string? lastUserMessage, float lastUserMessageAtMs, Func<double> rng)
    {
        if (lastUserMessage is null || lastUserMessage.Length < 10) return false;
        float sinceMs = nowMs - lastUserMessageAtMs;
        // 30-120 seconds after they said it: the thought has been simmering
        return sinceMs is > 30_000f and < 120_000f && rng() < 0.2;
    }
}
