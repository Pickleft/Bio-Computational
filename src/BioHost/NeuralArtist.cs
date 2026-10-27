using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Host;

/// <summary>
/// The brain's neural activity becomes generative art. The actual spike patterns,
/// neurotransmitter levels, and regional firing rates are translated into a Muse
/// Image prompt that captures the brain's CURRENT internal state as a visual.
/// A calm brain generates smooth, flowing patterns. An anxious brain generates
/// jagged, chaotic ones. A curious brain generates branching, exploratory forms.
/// This is not "draw me a picture" — it's "here is what my mind looks like right now."
/// </summary>
public sealed class NeuralArtist
{
    private readonly IImageGenerator _imager;
    private readonly ILanguageModel _lm;
    private float _lastArtMs = -60_000f;
    public readonly List<(float TimeMs, string Path, string StateDescription)> Gallery = [];

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(60);
    public string ArtDir { get; set; } = "art";

    public NeuralArtist(IImageGenerator imager, ILanguageModel lm)
    {
        _imager = imager;
        _lm = lm;
        Directory.CreateDirectory(ArtDir);
    }

    public async Task<string?> MaybeCreateAsync(SpikingNetwork net, IdleController idle)
    {
        if (net.TimeMs - _lastArtMs < (float)Interval.TotalMilliseconds) return null;
        _lastArtMs = net.TimeMs;

        try
        {
            // read the brain's current neural state
            var m = net.Modulators;
            float da = m.Get(NeuromodulatorKind.Dopamine);
            float sht = m.Get(NeuromodulatorKind.Serotonin);
            float na = m.Get(NeuromodulatorKind.Noradrenaline);
            float ach = m.Get(NeuromodulatorKind.Acetylcholine);
            float rate = net.Recorder.MeanRateHz(MathF.Max(1000, net.TimeMs));

            // translate neural state to visual descriptors
            string energy = rate > 2 ? "intense, electric" : rate > 0.5 ? "active, flowing" : "still, minimal";
            string mood = da > 0.4 ? "warm, golden, ascending" : da < 0.2 ? "cool, blue, descending" : "balanced, neutral";
            string complexity = ach > 0.5 ? "intricate, detailed, layered" : "simple, clean, spacious";
            string stability = sht > 0.6 ? "orderly, geometric, structured" : "organic, flowing, irregular";
            string urgency = na > 0.4 ? "sharp, urgent, high-contrast" : "soft, gentle, low-contrast";

            // generate a description of what the brain's state looks like
            string stateDesc = $"{energy} {mood} {complexity} {stability} {urgency}";

            // ask the LLM to interpret the neural state as visual art direction
            string prompt =
                $"Abstract neural art: {stateDesc}. " +
                $"This is a real-time portrait of a living brain's neural state — " +
                "spiking activity translated to visual form. " +
                "No text, no faces, pure abstract form capturing the feeling of this neural state.";

            byte[] png = await _imager.GenerateAsync(prompt, "1024x1024");
            string file = Path.Combine(ArtDir, $"neural-{(long)(net.TimeMs / 1000)}s.png");
            await File.WriteAllBytesAsync(file, png);
            Gallery.Add((net.TimeMs, file, stateDesc));
            return file;
        }
        catch { return null; }
    }
}
