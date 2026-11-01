using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// CONVERSATION MEMORY — the brain remembers what was said in this
/// conversation and BUILDS on it. Without this, each response is an island.
/// With it, the brain says things like:
///
///   CALLBACK: "You mentioned X earlier — I've been thinking about that."
///   CONTRADICTION: "Wait, you said Y before, but now you're saying Z."
///   BUILDING: "Following from what you said about X..."
///   BOREDOM: "We've been talking about this for a while. What else?"
///   TOPIC PREFERENCE: the brain gets MORE ENGAGED with topics that paid
///     dopamine (measured per topic) and DRIFS AWAY from ones that didn't.
///
/// This is what makes a conversation feel CONTINUOUS rather than a series
/// of independent exchanges.
///
/// Sim-thread only.
/// </summary>
public sealed class ConversationMemory
{
    public sealed record Exchange(
        float TimeMs, string Who, string Text, string[] Topics, float Valence,
        float DopamineAt, bool BrainResponded);

    /// <summary>All exchanges in this conversation (user + brain).</summary>
    public readonly List<Exchange> History = [];

    /// <summary>Topic engagement: how much dopamine each topic has paid.</summary>
    private readonly Dictionary<string, float> _topicEngagement = [];

    /// <summary>How many times each topic has been discussed (for boredom).</summary>
    private readonly Dictionary<string, int> _topicCount = [];

    public int UserMessageCount => History.Count(e => e.Who == "user");
    public int BrainResponseCount => History.Count(e => e.Who == "brain");

    /// <summary>Record a user message.</summary>
    public void OnUserMessage(string text, float nowMs, float dopamine, float valence)
    {
        var topics = ExtractTopics(text);
        History.Add(new Exchange(nowMs, "user", text, topics, valence, dopamine, false));
        if (History.Count > 30) History.RemoveAt(0);
        foreach (var topic in topics)
        {
            _topicCount[topic] = _topicCount.GetValueOrDefault(topic) + 1;
            // engagement grows when dopamine fires while this topic is active
            if (dopamine > 0.3)
                _topicEngagement[topic] = _topicEngagement.GetValueOrDefault(topic) + dopamine * 0.2f;
        }
    }

    /// <summary>Record a brain response.</summary>
    public void OnBrainResponse(string text, float nowMs, float valence)
    {
        var topics = ExtractTopics(text);
        History.Add(new Exchange(nowMs, "brain", text, topics, valence, 0f, true));
        if (History.Count > 30) History.RemoveAt(0);
    }

    /// <summary>Extract meaningful topics from text (words >4 chars, not stop-words).</summary>
    private static string[] ExtractTopics(string text)
    {
        var stop = new HashSet<string> { "that", "this", "with", "have", "just", "like", "what", "when", "your", "they", "them", "were", "been", "here", "there", "about", "would", "could", "right", "think", "really", "maybe", "going", "because" };
        return text.ToLowerInvariant().Split([' ', '.', ',', '!', '?', ';', ':', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4 && !stop.Contains(w) && w.All(char.IsLetter))
            .Distinct()
            .Take(5)
            .ToArray();
    }

    /// <summary>Find a TOPIC from earlier in the conversation that connects to the current message.</summary>
    public string? FindCallback(string currentText, float nowMs)
    {
        var currentTopics = ExtractTopics(currentText);
        var earlierUser = History.Where(e => e.Who == "user" && nowMs - e.TimeMs > 30_000).ToList();
        foreach (var ex in earlierUser)
        {
            var overlap = ex.Topics.Intersect(currentTopics).ToArray();
            if (overlap.Length > 0)
                return $"You mentioned {string.Join("/", overlap.Take(2))} earlier — I kept thinking about that.";
            // also check if the brain's PREVIOUS response touched the same topic
            var brainOnSame = History.Where(e => e.Who == "brain" && e.Topics.Intersect(currentTopics).Any()).Any();
            if (brainOnSame && nowMs - ex.TimeMs > 60_000)
                return $"We talked about {string.Join("/", ex.Topics.Take(1))} a while back. It's still with me.";
        }
        return null;
    }

    /// <summary>Detect a contradiction: the user said X before but is now saying the opposite.</summary>
    public string? DetectContradiction(string currentText, float nowMs)
    {
        var currentTopics = ExtractTopics(currentText);
        var earlier = History.Where(e => e.Who == "user" && nowMs - e.TimeMs > 20_000 && nowMs - e.TimeMs < 300_000);
        foreach (var ex in earlier)
        {
            var overlap = ex.Topics.Intersect(currentTopics).ToArray();
            if (overlap.Length > 0)
            {
                // check if valence flipped on the same topic
                if (History.Count > 0)
                {
                    var lastExchange = History[^1];
                    bool currentNeg = currentText.Contains("not") || currentText.Contains("don't") || currentText.Contains("never") || currentText.Contains("don't want");
                    bool earlierPos = !ex.Text.Contains("not") && !ex.Text.Contains("don't") && !ex.Text.Contains("never");
                    if (currentNeg && earlierPos && overlap.Length > 0)
                        return $"Wait — earlier you said \"{Trunc(ex.Text, 30)}\" about {overlap[0]}. Now you're saying the opposite. Which is it?";
                }
            }
        }
        return null;
    }

    /// <summary>The brain's most engaging topic (what it LIKES talking about).</summary>
    public string? FavoriteTopic
    {
        get
        {
            string? best = null; float bestVal = 0;
            foreach (var (topic, engagement) in _topicEngagement)
                if (engagement > bestVal) { bestVal = engagement; best = topic; }
            return bestVal > 0.3f ? best : null;
        }
    }

    /// <summary>Is the brain getting BORED of the current topic? (repeated >3 times)</summary>
    public bool IsBoredOf(string topic) => _topicCount.GetValueOrDefault(topic) > 3;

    /// <summary>How engaged the brain is with this topic (0..1).</summary>
    public float EngagementWith(string topic) => Math.Clamp(_topicEngagement.GetValueOrDefault(topic), 0f, 1f);

    /// <summary>Persist topic preferences (the brain's favorite topics outlive
    /// any single conversation).</summary>
    public IReadOnlyDictionary<string, float> SaveEngagement() => _topicEngagement;

    /// <summary>Persist topic repetition counts (boredom has a memory too).</summary>
    public IReadOnlyDictionary<string, int> SaveCounts() => _topicCount;

    /// <summary>Restore topic engagement + counts learned in past lives.</summary>
    public void RestoreEngagement(IEnumerable<KeyValuePair<string, float>>? engagement,
        IEnumerable<KeyValuePair<string, int>>? counts)
    {
        foreach (var kv in engagement ?? [])
            _topicEngagement[kv.Key] = kv.Value;
        foreach (var kv in counts ?? [])
            _topicCount[kv.Key] = kv.Value;
    }

    /// <summary>Fragment for the LLM prompt: what the brain remembers from this conversation.</summary>
    public string PromptFragment
    {
        get
        {
            var recent = History.TakeLast(6).ToList();
            if (recent.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("CONVERSATION SO FAR: ");
            foreach (var e in recent)
                sb.Append($"{(e.Who == "user" ? "They" : "You")} said \"{Trunc(e.Text, 40)}\". ");
            if (FavoriteTopic is { } fav)
                sb.Append($"You've been MOST engaged when discussing {fav}. ");
            return sb.ToString();
        }
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "...";
}
