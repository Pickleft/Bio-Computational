using System.Text;

namespace BioComp.Host;

/// <summary>
/// The brain's own voice, which DEVELOPS from the voices it hears - the way an
/// infant's babble drifts toward its household's speech. Every measurement is a
/// real acoustic feature of the incoming audio (RMS energy, zero-crossing rate
/// as a pitch proxy, syllable rate); nothing is canned.
/// Three things grow here:
/// 1. Prosody anchoring - its pitch/rate drift toward the average heard voice.
/// 2. A babble vocabulary - syllables extracted from heard speech, so after
///    hearing us talk it starts producing syllable strings of its own.
/// 3. Vocal mimicry - non-verbal sounds (laughs, hums) are stored as prosody
///    patterns and later reproduced in its own way.
/// </summary>
public sealed class VoiceSelf
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _syllables = [];
    private readonly List<(float Energy, float Zcr, float Dur)> _heardSounds = [];

    /// Running averages of heard voices (exponential, consolidating with age).</summary>
    public float HeardPitchZcr { get; private set; }
    public float HeardEnergy { get; private set; }
    public float HeardRate { get; private set; }
    public int ClipsHeard { get; private set; }

    public int SyllablesLearned { get { lock (_gate) return _syllables.Count; } }
    public int Mimicries { get; private set; }
    public int SoundsHeard { get { lock (_gate) return _heardSounds.Count; } }

    /// <summary>Feed a transcribed speech clip: learn its prosody and syllables.</summary>
    public void OnHeardSpeech(short[] clip, string transcript)
    {
        float zcr = ZeroCrossingRate(clip);
        float rms = Rms(clip);
        float sylRate = SyllableCount(transcript) / MathF.Max(0.5f, clip.Length / 16000f);
        ClipsHeard++;
        // learning rate falls as it ages: early impressions move the voice most
        float a = MathF.Min(0.35f, 2f / (2f + ClipsHeard));
        HeardPitchZcr += a * (zcr - HeardPitchZcr);
        HeardEnergy += a * (rms - HeardEnergy);
        HeardRate += a * (Math.Clamp(sylRate, 0.5f, 6f) - HeardRate);
        lock (_gate)
        {
            foreach (var s in ExtractSyllables(transcript))
            {
                _syllables[s] = _syllables.TryGetValue(s, out int n) ? n + 1 : 1;
                if (_syllables.Count > 400)
                    _syllables.Remove(_syllables.OrderBy(kv => kv.Value).First().Key);
            }
        }
    }

    /// <summary>Feed a non-verbal sound (energy but no transcript) for later mimicry.</summary>
    public void OnNonVerbalSound(short[] clip)
    {
        lock (_gate)
        {
            _heardSounds.Add((Rms(clip), ZeroCrossingRate(clip), clip.Length / 16000f));
            if (_heardSounds.Count > 20) _heardSounds.RemoveAt(0);
        }
    }

    /// <summary>
    /// Babble: chain 2-4 of the syllables it has heard most often. Returns null
    /// before it has learned enough speech (caller falls back to mood sounds).
    /// r1/r2 are random draws supplied by the caller (sim thread owns the RNG).
    /// </summary>
    public string? Babble(double r1, double r2)
    {
        string[] top;
        lock (_gate) top = [.. _syllables.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key)];
        if (top.Length < 3) return null;
        int n = 2 + (int)(r1 * 3);
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(top[(int)(r2 * top.Length)]);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reproduce the most recent heard non-verbal sound in its own way.
    /// Voiced hums (low zero-crossings) become "mmm", bright sounds become
    /// "haa"; repetition scales with how long the heard sound lasted.
    /// </summary>
    public string? TryMimic()
    {
        (float Energy, float Zcr, float Dur) s;
        lock (_gate) { if (_heardSounds.Count == 0) return null; s = _heardSounds[^1]; }
        Mimicries++;
        return s.Zcr > 0.12f
            ? (s.Dur > 0.6f ? "haa haa haa" : "haa!")
            : (s.Dur > 0.8f ? "mmm, mmm, mmm" : "mmm!");
    }

    /// <summary>
    /// Its own voice parameters, drifting toward the voices it grew up hearing.
    /// Typical speech zero-crossing rate sits near 0.10; a brighter-voiced
    /// household pulls its pitch up, a deeper one pulls it down.
    /// </summary>
    public float DevelopedPitchBias => ClipsHeard == 0 ? 0
        : Math.Clamp((HeardPitchZcr - 0.10f) * 30f, -4f, 5f);
    public float DevelopedRateBias => ClipsHeard == 0 ? 0
        : Math.Clamp((HeardRate - 2.5f) * 1.2f, -2f, 2f);

    private static float Rms(short[] clip)
    {
        double s = 0;
        foreach (var v in clip) s += (double)v * v;
        return (float)(Math.Sqrt(s / Math.Max(1, clip.Length)) / 32768.0);
    }

    private static float ZeroCrossingRate(short[] clip)
    {
        int crossings = 0;
        for (int i = 1; i < clip.Length; i++)
            if ((clip[i - 1] < 0) != (clip[i] < 0)) crossings++;
        return crossings / (float)clip.Length;
    }

    /// <summary>
    /// Acoustic profile of a clip for prosody-based affect estimation: energy,
    /// pitch proxy (zero-crossing rate) and their variabilities across thirds
    /// of the clip (rising/falling contours matter, not just levels).
    /// </summary>
    public static (float Rms, float Zcr, float ZcrVariability, float RmsVariability) AcousticProfile(short[] clip)
    {
        int third = Math.Max(1, clip.Length / 3);
        float r0 = Rms(clip[..third]), r1 = Rms(clip[third..(2 * third)]), r2 = Rms(clip[(2 * third)..]);
        float z0 = ZeroCrossingRate(clip[..third]), z1 = ZeroCrossingRate(clip[third..(2 * third)]), z2 = ZeroCrossingRate(clip[(2 * third)..]);
        float rm = (r0 + r1 + r2) / 3f, zm = (z0 + z1 + z2) / 3f;
        float rVar = (MathF.Abs(r0 - rm) + MathF.Abs(r1 - rm) + MathF.Abs(r2 - rm)) / 3f;
        float zVar = (MathF.Abs(z0 - zm) + MathF.Abs(z1 - zm) + MathF.Abs(z2 - zm)) / 3f;
        return (rm, zm, zVar, rVar);
    }

    private static int SyllableCount(string t)
    {
        int c = 0; bool vowel = false;
        foreach (var ch in t.ToLowerInvariant())
        {
            bool v = "aeiouy".Contains(ch);
            if (v && !vowel) c++;
            vowel = v;
        }
        return Math.Max(1, c);
    }

    /// Crude onset-rime split at vowel nuclei: "hello" -> "he" + "llo".
    private static IEnumerable<string> ExtractSyllables(string t)
    {
        foreach (var w in t.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (w.Length < 2 || w.Length > 15) continue;
            int start = 0; bool inVowel = false;
            for (int i = 0; i < w.Length - 1; i++)
            {
                bool v = "aeiouy".Contains(w[i]);
                if (!inVowel && v && i - start >= 1) { yield return w[start..(i + 1)]; start = i + 1; }
                inVowel = v;
            }
            if (w.Length - start >= 1) yield return w[start..];
        }
    }
}
