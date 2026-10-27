using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Memory;
using BioComp.Core.Network;

namespace BioComp.Meta;

/// <summary>
/// REM dreams: during REM, the most emotionally salient recent episode is rendered
/// into imagery through the image tier (Muse Image when live, deterministic mock
/// keyless). The prompt is a deterministic function of episode content and current
/// feeling, so the same memories dream the same way across runs; the returned pixels
/// are whatever the generator produces (the API has no seed parameter).
/// </summary>
public sealed class DreamGenerator
{
    private readonly IImageGenerator _imager;
    private float _lastDreamMs = -1e9f;

    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(60);
    public string DreamsDir { get; set; } = "dreams";
    public readonly List<(float TimeMs, string File, string Prompt)> Log = [];

    public DreamGenerator(IImageGenerator imager) => _imager = imager;

    public static string BuildPrompt(Episode episode, FeelingState feeling) =>
        $"A hazy, luminous dream image evoking: {string.Join(", ", episode.Tags)}. " +
        $"Mood: {feeling.Name} (valence {feeling.Valence:+0.0;-0.0}). " +
        "Soft bioluminescent neural texture, drifting, half-remembered, no text.";

    /// <summary>Generate one dream if the interval allows and memory has content.</summary>
    public async Task<string?> MaybeDreamAsync(SpikingNetwork net, IdleController idle)
    {
        if (net.TimeMs - _lastDreamMs < MinInterval.TotalMilliseconds) return null;
        var episodes = net.EpisodeStore.Episodes;
        if (episodes.Count == 0) return null;

        // emotional salience first, then recency: the brain dreams what mattered
        var episode = episodes.OrderByDescending(e => MathF.Abs(e.Valence))
                              .ThenByDescending(e => e.TimeMs).First();

        string prompt = BuildPrompt(episode, idle.Feelings.Last);
        byte[] png = await _imager.GenerateAsync(prompt, "1024x1024");
        Directory.CreateDirectory(DreamsDir);
        string file = Path.Combine(DreamsDir, $"dream-{(long)(net.TimeMs / 1000)}s-ep{episode.Id}.png");
        await File.WriteAllBytesAsync(file, png);
        _lastDreamMs = net.TimeMs;
        Log.Add((net.TimeMs, file, prompt));
        return file;
    }
}
