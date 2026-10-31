namespace BioComp.Host;

/// <summary>
/// THE NON-VERBAL VOCAL LAYER — the sounds that are truly ours. A creature's
/// voice underneath its words: hums ("mmm..."), breaths, moods, the sounds
/// a brain makes before it has language. Synthesized from human voice data:
/// a glottal pulse train with real vocal-fold imperfections (cycle jitter
/// ~1.2%, amplitude shimmer, breathiness), shaped by a cascade of three
/// Klatt formant resonators at Peterson &amp; Barney (1952) vowel means with
/// coarticulation glides.
///
/// Words are NOT rendered here — word sounds belong to the prosthetic vocal
/// tract (VoiceCortex routes them to SSML speech). This layer does what
/// synthesis is genuinely good at: continuous, pitch-contoured, wordless
/// vocalization. "mmm" is a real hum. So is everything it feels.
/// </summary>
public sealed class VoiceEngine
{
    private const int Fs = 22_050;

    /// <summary>A non-verbal vocalization kind.</summary>
    public enum Vocal : byte
    {
        Hum,        // mmm — contentment, recognition
        HmmQuestion,// rising hum — wondering
        Breath,     // soft exhale — tired / relieved
        Ahh,        // open vowel — realization, warmth
        Ohh,        // low wonder / sadness
        Hee,        // small laugh-bright chirp
        Whimper,    // small falling distress
    }

    /// <summary>Render a vocalization as a WAV (22.05 kHz 16-bit mono).</summary>
    public byte[] Render(Vocal kind, float rate = 0.92f, float pitch = 1.0f, float volume = 1.0f)
    {
        float f0Base = Math.Clamp(170f * Math.Clamp(pitch, 0.6f, 1.4f), 85f, 280f);
        float durMs = DurationOf(kind) / Math.Clamp(rate, 0.6f, 1.4f);
        int n = (int)(durMs * Fs / 1000f);
        if (n < 64) n = 64;

        var rng = new Random(0x5EED + (int)kind);   // deterministic per kind
        var samples = new float[n];

        // formant targets per kind (P&B-derived): start -> end (a contour)
        var (f1s, f2s, f3s, f1e, f2e, f3e) = FormantsOf(kind);
        float f0Start = f0Base * PitchContourStart(kind);
        float f0End = f0Base * PitchContourEnd(kind);
        float breath = kind == Vocal.Breath ? 0.30f : 0.08f;

        float y1a = 0, y1b = 0, y2a = 0, y2b = 0, y3a = 0, y3b = 0;
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            // contour interpolation + a gentle 4.5 Hz vibrato (natural voice tremor)
            float vib = 1f + 0.006f * MathF.Sin(2f * MathF.PI * 4.5f * t * durMs / 1000f);
            float f0 = Lerp(f0Start, f0End, t) * vib;
            float f1 = Lerp(f1s, f1e, t);
            float f2 = Lerp(f2s, f2e, t);
            float f3 = Lerp(f3s, f3e, t);

            // envelope: slow soft attack, slower release (a voice, not a click)
            float env = Math.Min(t / 0.25f, 1f) * Math.Min((1f - t) / 0.35f, 1f);
            env = MathF.Max(0f, env);

            // glottal source with jitter + shimmer + breathiness
            float jitter = 1f + ((float)rng.NextDouble() - 0.5f) * 0.024f;
            float shimmer = 1f + ((float)rng.NextDouble() - 0.5f) * 0.10f;
            double period = Fs / (f0 * jitter);
            phase += 1.0 / period;
            if (phase >= 1.0) phase -= 1.0;
            double p = phase;
            float pulse = p < 0.4 ? (float)(0.5 - 0.5 * Math.Cos(Math.PI * p / 0.4))
                       : p < 0.6 ? (float)(0.5 + 0.5 * Math.Cos(Math.PI * (p - 0.4) / 0.2))
                       : 0f;
            float excitation = (pulse - 0.25f) * shimmer
                               + (((float)rng.NextDouble() - 0.5f) * breath);

            float s1 = Resonate(ref y1a, ref y1b, excitation, f1, 80f);
            float s2 = Resonate(ref y2a, ref y2b, s1, f2, 95f);
            float s3 = Resonate(ref y3a, ref y3b, s2, f3, 130f);
            samples[i] = s3 * 2.5f * env;
        }
        return Wav(samples, Math.Clamp(volume, 0.3f, 1f));
    }

    private static float DurationOf(Vocal k) => k switch
    {
        Vocal.Hum => 520f, Vocal.HmmQuestion => 420f, Vocal.Breath => 480f,
        Vocal.Ahh => 460f, Vocal.Ohh => 520f, Vocal.Hee => 240f, Vocal.Whimper => 380f,
        _ => 400f,
    };

    // (F1s,F2s,F3s, F1e,F2e,F3e) — hums live low, open vowels open up
    private static (float, float, float, float, float, float) FormantsOf(Vocal k) => k switch
    {
        Vocal.Hum => (250, 1100, 2200, 250, 1000, 2100),
        Vocal.HmmQuestion => (250, 1100, 2200, 300, 1400, 2300),
        Vocal.Breath => (450, 1200, 2400, 380, 950, 2300),
        Vocal.Ahh => (450, 1100, 2400, 730, 1090, 2440),     // opens toward "ah"
        Vocal.Ohh => (570, 840, 2410, 430, 800, 2350),       // closes, darkens
        Vocal.Hee => (400, 1900, 2500, 300, 2200, 2800),     // brightens
        Vocal.Whimper => (500, 1300, 2500, 380, 1000, 2400), // falls
        _ => (500, 1500, 2500, 500, 1500, 2500),
    };

    private static float PitchContourStart(Vocal k) => k switch
    {
        Vocal.HmmQuestion => 0.92f, Vocal.Ohh => 0.95f, Vocal.Whimper => 1.02f, _ => 1.0f,
    };
    private static float PitchContourEnd(Vocal k) => k switch
    {
        Vocal.Hum => 0.90f, Vocal.HmmQuestion => 1.14f, Vocal.Breath => 0.85f,
        Vocal.Ahh => 0.94f, Vocal.Ohh => 0.86f, Vocal.Hee => 1.1f, Vocal.Whimper => 0.84f,
        _ => 0.95f,
    };

    /// <summary>Map an interjection token to a vocalization (null = not ours).</summary>
    public static Vocal? Match(string token) => token.ToLowerInvariant() switch
    {
        "mmm" or "mm" or "hmm" or "hm" or "m" => Vocal.Hum,
        "hmm?" or "hm?" or "hmph" => Vocal.HmmQuestion,
        "ahh" or "ah" => Vocal.Ahh,
        "ohh" or "oh" or "ooh" => Vocal.Ohh,
        "hee" or "hee-hee" or "hoo" or "hehe" => Vocal.Hee,
        "*breath*" or "..." => Vocal.Breath,
        _ => null,
    };

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Resonate(ref float y1, ref float y2, float x, float freq, float bw)
    {
        float c = -MathF.Exp(-2f * MathF.PI * bw / Fs);
        float b = 2f * MathF.Exp(-MathF.PI * bw / Fs) * MathF.Cos(2f * MathF.PI * freq / Fs);
        float a = 1f - b - c;
        float y = a * x + b * y1 + c * y2;
        y2 = y1; y1 = y;
        return y;
    }

    private static byte[] Wav(float[] samples, float volume)
    {
        float peak = 1e-6f;
        foreach (var s in samples) peak = MathF.Max(peak, MathF.Abs(s));
        float gain = 0.6f * volume / peak;
        int n = samples.Length;
        byte[] wav = new byte[44 + n * 2];
        void P(int o, int v) { wav[o] = (byte)v; wav[o + 1] = (byte)(v >> 8); wav[o + 2] = (byte)(v >> 16); wav[o + 3] = (byte)(v >> 24); }
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        P(4, 36 + n * 2);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        P(16, 16); wav[20] = 1; wav[21] = 0; wav[22] = 1; wav[23] = 0;
        P(24, Fs); P(28, Fs * 2); wav[32] = 2; wav[33] = 0; wav[34] = 16; wav[35] = 0;
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        P(40, n * 2);
        for (int i = 0; i < n; i++)
        {
            short v = (short)Math.Clamp(samples[i] * gain * 32767f, short.MinValue, short.MaxValue);
            wav[44 + i * 2] = (byte)v; wav[45 + i * 2] = (byte)(v >> 8);
        }
        return wav;
    }
}
