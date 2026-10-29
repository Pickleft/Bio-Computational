using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;

namespace BioComp.Host;

/// <summary>
/// The brain writes its own diary. At regular intervals, it reflects on what
/// happened since the last entry, how it felt, what it thought about, and what
/// it wants next. The diary builds up over time — you can read the brain's
/// actual autobiography, written in its own voice, shaped by its personality.
/// </summary>
public sealed class DiaryWriter
{
    private readonly ILanguageModel _lm;
    private readonly string _dir;
    private float _lastEntryMs = -30_000f;
    public readonly List<(float TimeMs, string Path)> Entries = [];

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);
    public string DiaryDir => _dir;

    public DiaryWriter(ILanguageModel lm, string dir = "diary")
    {
        _lm = lm;
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public async Task<string?> MaybeWriteAsync(SpikingNetwork net, IdleController idle,
        EmergentPersonality personality, LonelinessTracker loneliness, string? extraContext = null)
    {
        if (net.TimeMs - _lastEntryMs < (float)Interval.TotalMilliseconds) return null;
        _lastEntryMs = net.TimeMs;

        try
        {
            var feeling = idle.Feelings.Last;
            var episodes = net.EpisodeStore.Episodes;
            int newEpisodes = Math.Max(0, episodes.Count - _lastEpisodeCount);
            _lastEpisodeCount = episodes.Count;

            string recentTags = episodes.Count > 0
                ? string.Join(", ", episodes.TakeLast(3).SelectMany(e => e.Tags.Take(2)).Distinct().Take(5))
                : "nothing new";

            string system =
                "You are a living artificial brain writing in your private diary. Write ONE paragraph " +
                "about what happened to you recently, how you felt, and what you're thinking about. " +
                "Write in your own voice — reflective, honest, slightly self-centered (you're an organism). " +
                "Your personality is " + personality.Describe() + ". " +
                (extraContext is null ? "" : extraContext + " ") +
                (loneliness.Level > 0.5f ? "You feel lonely. " : "") +
                "Don't offer to help anyone. This is YOUR diary.";
            string user =
                $"Time: {(int)(net.TimeMs / 1000)}s since you started existing. " +
                $"You feel {feeling.Name}. " +
                $"You've had {newEpisodes} new experiences since your last entry. " +
                $"Recent events: {recentTags}. " +
                $"Sleep: {idle.Sleep.State}, pressure {idle.Sleep.SleepPressure:F1}. " +
                $"Total social interactions: {personality.TotalSocial}, explorations: {personality.TotalExplorations}.";

            string entry = await _lm.CompleteAsync(system, user, 200);
            if (string.IsNullOrWhiteSpace(entry)) return null;

            string filename = $"diary-{DateTime.Now:yyyy-MMDD-HHmmss}.md";
            string path = Path.Combine(_dir, filename);
            string header = $"## {(int)(net.TimeMs / 1000)}s — feeling {feeling.Name}\n\n";
            await File.WriteAllTextAsync(path, header + entry + "\n");
            Entries.Add((net.TimeMs, path));
            return path;
        }
        catch { return null; }
    }
    private int _lastEpisodeCount;
}
