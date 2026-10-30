using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Host;

/// <summary>
/// THE NEURAL VOICE — speech prosody generated from actual neurotransmitter
/// levels. The voice IS the brain's state: when dopamine is high, the voice
/// brightens (more resonance, upward inflection, faster). When serotonin
/// drops, it flattens (monotone, trailing). When noradrenaline spikes, it
/// tightens (staccato, urgent). This is not pitch/rate adjustment — the very
/// TEXT is transformed to carry emotional prosody markers that the TTS engine
/// interprets naturally.
///
/// How it works:
/// 1. The brain's 7 neurotransmitter levels + valence/arousal are read
/// 2. They drive a prosody transformer that modifies the TEXT before TTS:
///    - High DA → exclamation, emphasis caps, shorter sentences
///    - Low 5-HT → ellipses (trailing thoughts), filler words ("um", "I guess")
///    - High NA → urgent pacing, staccato periods, intensity
///    - High OXT → warm fillers ("you know", gentle phrasing)
///    - High ACh → precise articulation, measured pacing
///    - High Mel → sleepy drawl, long pauses, trailing...
///    - Low Hist → quiet, distant phrasing
/// 3. The transformed text + prosody parameters are sent to the browser
/// 4. The browser TTS renders it — the emotional state is IN the speech
///    because it's in the words and their rhythm, not just pitch/rate
/// </summary>
public static class NeuralVoice
{
    /// <summary>
    /// Transform plain text into emotionally-colored speech text based on
    /// the brain's current neurotransmitter state. The output uses natural
    /// prosody markers (punctuation, emphasis, fillers, pauses) that TTS
    /// engines interpret as emotional variation.
    /// </summary>
    public static (string Text, float Rate, float Pitch, float Volume) Compose(
        string text, SpikingNetwork net, float valence, float arousal)
    {
        float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        float sht = net.Modulators.Get(NeuromodulatorKind.Serotonin);
        float na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        float oxt = net.Modulators.Get(NeuromodulatorKind.Oxytocin);
        float ach = net.Modulators.Get(NeuromodulatorKind.Acetylcholine);
        float mel = net.Modulators.Get(NeuromodulatorKind.Melatonin);
        float hist = net.Modulators.Get(NeuromodulatorKind.Histamine);

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count == 0) return (text, 0.92f, 1f, 1f);

        // === PROSODY PARAMETERS (drive the TTS voice settings) ===
        // Base rate: slightly slow for warmth; arousal speeds it up
        float rate = 0.90f + arousal * 0.15f - mel * 0.10f;
        // Pitch: dopamine brightens (up), melatonin deepens (down)
        float pitch = 1.0f + (da - 0.3f) * 0.15f - mel * 0.20f + valence * 0.05f;
        // Volume: histamine = presence; low histamine = fading
        float volume = 0.85f + hist * 0.15f;

        // === TEXT TRANSFORMATION ===

        // 1. MELATONIN (sleepy): add trailing pauses and drawl
        if (mel > 0.4f)
        {
            words = AddSleepyProsody(words);
        }

        // 2. LOW SEROTONIN (uncertain): add fillers and hedging
        if (sht < 0.35f)
        {
            words = AddUncertainty(words, valence);
        }

        // 3. HIGH DOPAMINE (excited/rewarded): add emphasis and energy
        if (da > 0.55f)
        {
            words = AddExcitement(words, valence);
        }

        // 4. HIGH NORADRENALINE (alert/stressed): add urgency
        if (na > 0.5f && valence < 0)
        {
            words = AddUrgency(words);
        }

        // 5. HIGH OXYTOCIN (bonding/warm): add warmth markers
        if (oxt > 0.3f && valence > 0.1f)
        {
            words = AddWarmth(words);
        }

        // 6. HIGH ACH (focused): tighten pacing
        if (ach > 0.5f)
        {
            rate += 0.03f; // slightly crisper
        }

        string result = string.Join(" ", words);
        return (result,
            Math.Clamp(rate, 0.75f, 1.2f),
            Math.Clamp(pitch, 0.8f, 1.2f),
            Math.Clamp(volume, 0.6f, 1.0f));
    }

    /// <summary>Sleepy prosody: trailing ellipses, longer pauses</summary>
    private static List<string> AddSleepyProsody(List<string> words)
    {
        var result = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            result.Add(words[i]);
            // add pause after every 4-5 words when sleepy
            if (i % 4 == 3 && i < words.Count - 1)
                result.Add("...");
        }
        // trailing off at the end
        if (result.Count > 0 && !result[^1].EndsWith("..."))
            result[^1] = result[^1].TrimEnd('.', '!') + "...";
        return result;
    }

    /// <summary>Uncertainty: hedging fillers, tentative phrasing</summary>
    private static List<string> AddUncertainty(List<string> words, float valence)
    {
        var result = new List<string>();
        var rnd = words.Count; // deterministic
        for (int i = 0; i < words.Count; i++)
        {
            // occasional filler
            if (i == 1 && rnd % 3 == 0) result.Add(valence < 0 ? "I guess..." : "maybe...");
            if (i == 3 && rnd % 4 == 0) result.Add("um,");
            result.Add(words[i]);
        }
        return result;
    }

    /// <summary>Excitement: emphasis on key words, exclamation</summary>
    private static List<string> AddExcitement(List<string> words, float valence)
    {
        var result = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            // capitalize emotionally positive words for emphasis
            if (valence > 0.2f && w.Length > 3 &&
                (w.Contains("good", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("nice", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("love", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("happy", StringComparison.OrdinalIgnoreCase) ||
                 w.Contains("great", StringComparison.OrdinalIgnoreCase)))
                w = w.ToUpperInvariant();
            result.Add(w);
        }
        // exclamation if positive
        if (valence > 0.3f && result.Count > 0)
        {
            string last = result[^1].TrimEnd('.', '!');
            result[^1] = last + "!";
        }
        return result;
    }

    /// <summary>Urgency: staccato pacing, short emphatic phrases</summary>
    private static List<string> AddUrgency(List<string> words)
    {
        var result = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            result.Add(words[i]);
            // period after every 3 words = staccato urgency
            if (i % 3 == 2 && i < words.Count - 1)
            {
                string last = result[^1].TrimEnd(',', '.');
                result[^1] = last + ".";
            }
        }
        return result;
    }

    /// <summary>Warmth: gentle fillers, soft phrasing</summary>
    private static List<string> AddWarmth(List<string> words)
    {
        var result = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            result.Add(words[i]);
            // occasional warm filler
            if (i == 2 && words.Count > 4 && result.Count % 5 == 0)
                result.Insert(i, "you know,");
        }
        return result;
    }
}
