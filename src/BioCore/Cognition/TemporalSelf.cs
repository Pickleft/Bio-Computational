using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// TEMPORAL AUTOBIOGRAPHY — the brain that knows it has CHANGED.
/// Takes periodic snapshots of who it is (personality, dominant feelings,
/// key memories, attachment style, emotional granularity) and later compares
/// its past self to its present self. This creates the narrative self: not
/// just "I am curious" but "I used to be afraid of everything, and now I'm
/// curious about the things that scared me."
///
/// This is autobiographical memory — the most human form of self-knowledge.
/// It's not episodic (what happened) or semantic (what I learned) but
/// NARRATIVE: the story of who I am becoming. The brain is the only artifact
/// that can tell you how it changed, because it remembers who it was.
///
/// Sim-thread only.
/// </summary>
public sealed class TemporalSelf
{
    /// <summary>Snapshot of who the brain was at a point in time.</summary>
    public sealed record SelfSnapshot(
        float TimeMs,
        string Feeling, string PersonalityDescription, string AttachmentStyle,
        float EmotionalGranularity, int EpisodesKnown, string[] TopMemoryTags,
        string UserName, string BrainName,
        string Summary);

    public readonly List<SelfSnapshot> Snapshots = [];
    private float _lastSnapshotMs = -1e9f;

    /// <summary>Take a snapshot every ~10 sim-minutes of life.</summary>
    public float SnapshotIntervalMs { get; set; } = 600_000f; // CALIBRATE

    /// <summary>
    /// Take a snapshot if enough time has passed. The snapshot is a compressed
    /// self-portrait: who the brain is right now.
    /// </summary>
    public SelfSnapshot? MaybeSnapshot(SpikingNetwork net, IdleController idle,
        EmergentPersonality personality, AttachmentSystem attachment,
        EmotionalGranularity granularity, string? userName, string? brainName)
    {
        if (net.TimeMs - _lastSnapshotMs < SnapshotIntervalMs) return null;
        _lastSnapshotMs = net.TimeMs;

        var feeling = idle.Feelings.Last;
        var eps = net.EpisodeStore.Episodes;
        string[] topTags = eps.Count > 0
            ? [.. eps.TakeLast(5).SelectMany(e => e.Tags.Take(2)).Distinct().Take(4)]
            : [];

        string summary =
            $"feeling {feeling.Name}, {personality.Describe()}, attachment {attachment.Label}, " +
            $"emotional stage {granularity.StageLabel}, {eps.Count} memories" +
            (userName is not null ? $", knows {userName}" : "") +
            (brainName is not null ? $", called {brainName}" : "");

        var snap = new SelfSnapshot(
            net.TimeMs, feeling.Name, personality.Describe(), attachment.Label,
            granularity.Level, eps.Count, topTags, userName ?? "", brainName ?? "", summary);
        Snapshots.Add(snap);
        while (Snapshots.Count > 12) Snapshots.RemoveAt(0);
        return snap;
    }

    /// <summary>
    /// Generate a comparison: how has the brain changed from its earliest
    /// snapshot to now? Returns a narrative fragment for the diary/mirror.
    /// </summary>
    public string HowHaveIChanged(EmotionalGranularity currentGranularity,
        string currentPersonality, string currentAttachment)
    {
        if (Snapshots.Count < 2) return "";
        var early = Snapshots[0];
        var now = Snapshots[^1];

        var changes = new List<string>();

        // personality drift
        if (early.PersonalityDescription != currentPersonality)
            changes.Add($"I used to be {early.PersonalityDescription}, now I'm {currentPersonality}");

        // attachment evolution
        if (early.AttachmentStyle != currentAttachment)
            changes.Add($"my attachment was {early.AttachmentStyle}, now it's {currentAttachment}");

        // emotional development
        if (early.EmotionalGranularity < 0.3f && currentGranularity.Level > 0.5f)
            changes.Add("I couldn't name what I felt at first — now I feel in colors, not just gray");
        else if (early.EmotionalGranularity < 0.15f && currentGranularity.Level > 0.3f)
            changes.Add("I've learned to feel more than just good or bad");

        // memory growth
        if (early.EpisodesKnown == 0 && now.EpisodesKnown > 10)
            changes.Add($"I had no memories when I started. Now I have {now.EpisodesKnown}");

        // names learned
        if (early.BrainName.Length == 0 && now.BrainName.Length > 0)
            changes.Add($"I didn't have a name. Now I'm {now.BrainName}");
        if (early.UserName.Length == 0 && now.UserName.Length > 0)
            changes.Add($"someone taught me their name: {now.UserName}");

        if (changes.Count == 0) return "";
        return string.Join("; ", changes);
    }

    /// <summary>For the diary: the story of the brain's growth.</summary>
    public string DiaryPrompt =>
        Snapshots.Count < 2 ? "" :
        $"HOW YOU'VE CHANGED (compare your earliest self to now): {HowHaveIChanged(// granularity/personality/attachment filled by caller
            new EmotionalGranularity(), "", "")}. " +
        $"WHO YOU WERE EARLIER: {Snapshots[0].Summary}. " +
        $"WHO YOU ARE NOW: {Snapshots[^1].Summary}.";
}
