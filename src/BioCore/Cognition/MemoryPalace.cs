using BioComp.Core.Memory;
using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// The brain's Memory Palace: every episodic memory mapped onto a 2D emotional
/// space. X axis = valence (painful left, positive right). Y axis = arousal
/// (calm bottom, exciting top). Size = emotional significance (absolute valence).
/// Color = recency (bright = just happened, dim = fading). The brain can navigate
/// its own memory space: "What was the most positive thing that happened to me?"
/// or "Show me everything that made me anxious."
/// This is the method of loci, but emergent — the brain didn't choose to organize
/// memories this way, the emotional architecture naturally arranges them.
/// </summary>
public static class MemoryPalace
{
    public readonly record struct MemoryNode(
        int EpisodeId, float X, float Y, float Size, float Brightness, string[] Tags, float Valence);

    /// <summary>Project all episodes into the 2D emotional space.</summary>
    public static List<MemoryNode> Project(SpikingNetwork net, float currentTimeMs)
    {
        var nodes = new List<MemoryNode>();
        foreach (var ep in net.EpisodeStore.Episodes)
        {
            float age = (currentTimeMs - ep.TimeMs) / 1000f;
            float brightness = MathF.Exp(-age / 300f); // memories dim over ~5 minutes

            nodes.Add(new MemoryNode(
                EpisodeId: ep.Id,
                X: Math.Clamp(ep.Valence, -1f, 1f),           // valence → horizontal position
                Y: Math.Clamp(ep.DopamineContext - 0.3f, -0.5f, 1f), // DA context → vertical (arousal proxy)
                Size: 3f + MathF.Abs(ep.Valence) * 8f,          // bigger = more emotionally significant
                Brightness: Math.Max(0.15f, brightness),        // dimmer = older
                Tags: [.. ep.Tags.Take(3)],
                Valence: ep.Valence));
        }
        return nodes;
    }

    /// <summary>The brain's most emotionally significant memory (for diary reflection).</summary>
    public static MemoryNode? MostSignificant(List<MemoryNode> nodes) =>
        nodes.OrderByDescending(n => MathF.Abs(n.Valence) * n.Brightness).FirstOrDefault();

    /// <summary>Find memories that are emotionally similar to the current state
    /// (the brain naturally recalls similar past experiences).</summary>
    public static List<MemoryNode> ResonantMemories(List<MemoryNode> nodes, float currentValence, float currentArousal, int max = 3)
    {
        return nodes
            .OrderByDescending(n => MathF.Abs(n.X - currentValence) + MathF.Abs(n.Y - currentArousal) < 0.5f ? n.Brightness : 0f)
            .Take(max)
            .ToList();
    }
}
