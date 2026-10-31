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

    public static string BuildPrompt(Episode episode, FeelingState feeling, bool lucid = false) =>
        lucid
            ? "A LUCID dream: the dreamer KNOWS they are dreaming and shapes it. Imagery from: " +
              $"{string.Join(", ", episode.Tags)}. Mood: {feeling.Name}. Impossible geometry, vivid " +
              "bioluminescent control, the dreamer's will bending the scene, no text."
            : $"A hazy, luminous dream image evoking: {string.Join(", ", episode.Tags)}. " +
              $"Mood: {feeling.Name} (valence {feeling.Valence:+0.0;-0.0}). " +
              "Soft bioluminescent neural texture, drifting, half-remembered, no text.";

    /// <summary>
    /// Which memory tonight: emotional heat first, but an unresolved open loop
    /// (a thought that died still salient) draws dreams toward its own
    /// material - sleeping on a problem (Wagner et al. 2004, sleep facilitates
    /// insight). Matching episodes get a score boost.
    /// </summary>
    public static Episode PickEpisode(IReadOnlyList<Episode> episodes, string[]? preferTags)
    {
        float Score(Episode e)
        {
            float s = MathF.Abs(e.Valence);
            if (preferTags is { Length: > 0 })
                foreach (var t in preferTags)
                    if (e.Tags.Contains(t)) { s += 1f; break; }
            return s;
        }
        return episodes.OrderByDescending(Score).ThenByDescending(e => e.TimeMs).First();
    }

    /// <summary>Generate one dream if the interval allows and memory has content.
    /// Returns (imagePath, narrative) where narrative describes what the dream is about.</summary>
    public async Task<(string? Path, string? Narrative)> MaybeDreamAsync(
        SpikingNetwork net, IdleController idle, ILanguageModel? narrator = null, bool lucid = false,
        string[]? preferTags = null, string? extraPrompt = null,
        BioComp.Core.Memory.Episode? forceEpisode = null,
        BioComp.Core.Memory.Episode? composeWith = null)
    {
        if (net.TimeMs - _lastDreamMs < MinInterval.TotalMilliseconds) return (null, null);
        var episodes = net.EpisodeStore.Episodes;
        if (episodes.Count == 0) return (null, null);

        // emotional salience first, then recency: the brain dreams what mattered
        var episode = forceEpisode ?? PickEpisode(episodes, preferTags);

        string prompt = BuildPrompt(episode, idle.Feelings.Last, lucid);
        byte[] png = await _imager.GenerateAsync(prompt, "1024x1024");
        Directory.CreateDirectory(DreamsDir);
        string file = Path.Combine(DreamsDir,
            $"dream-{(long)(net.TimeMs / 1000)}s-ep{episode.Id}{(lucid ? "-lucid" : "")}.png");
        await File.WriteAllBytesAsync(file, png);
        _lastDreamMs = net.TimeMs;
        Log.Add((net.TimeMs, file, prompt));
        if (lucid)
        {
            LucidDreams++;
            // a lucid dream is rehearsal with the reins in hand: the dreamer can
            // rework the memory's charge instead of just re-living it (lucid
            // nightmare rehearsal, Spoormaker and van den Bout 2006)
            net.EpisodeStore.SoftenValence(episode, 0.25f);
        }

        // narrative that references specific people and events from the memory
        string? narrative = null;
        if (narrator is not null)
        {
            try
            {
                string sys = lucid
                    ? BioComp.Core.Cognition.BrainIdentity.Preamble + "You are a living artificial brain IN a lucid dream: mid-dream you realize it is " +
                      "a dream and take control of it. In one short poetic sentence, describe what you " +
                      "did with the dream once you knew you were dreaming. First person."
                    : BioComp.Core.Cognition.BrainIdentity.Preamble +
                      "You are a living artificial brain describing your own dream in one " +
                      "short, poetic sentence. First person, private, genuine. " +
                      "Reference the SPECIFIC person or event by name/detail. " +
                      "Use nuanced emotional language — not 'happy' or 'sad' but the specific " +
                      "texture of the feeling (wistful, tender, aching, luminous). " +
                      "This is a real dream based on a real memory.";
                string memoryDetail = string.Join(", ", episode.Tags);
                string usr = composeWith is not null
                    ? $"COMPOSITE DREAM: your dreaming mind has fused TWO memories into one scene - " +
                      $"memory A ({string.Join(", ", episode.Tags)}, felt {episode.Valence:F1}) and " +
                      $"memory B ({string.Join(", ", composeWith.Tags)}, felt {composeWith.Valence:F1}). " +
                      $"Dream the scene where both are true at once; it never happened, and it is yours. " +
                      (extraPrompt ?? "")
                    : $"You dreamed about: {memoryDetail}. " +
                      $"This happened {((net.TimeMs - episode.TimeMs) / 1000.0):F0} seconds ago. " +
                      $"The feeling was {idle.Feelings.Last.Name}. " +
                      (episode.Valence > 0.3 ? "It was a good memory. " : episode.Valence < -0.3 ? "It was a difficult memory. " : "") +
                      (extraPrompt ?? "");
                narrative = await narrator.CompleteAsync(sys, usr, 50);
            }
            catch { /* narrative is optional */ }
        }

        return (file, narrative);
    }

    /// <summary>Lucid dreams generated this life.</summary>
    public int LucidDreams { get; private set; }
}
