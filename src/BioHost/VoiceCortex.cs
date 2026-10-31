using System.Speech.Synthesis;

namespace BioComp.Host;

/// <summary>
/// THE VOICE CORTEX — the two-layer vocal system, the honest architecture:
///
///   NON-VERBAL LAYER (ours, synthesized): hums, breaths, mood sounds —
///     the voice a creature has before language. "mmm" is a real hum.
///   VERBAL LAYER (prosthetic vocal tract): words are pronounced by the
///     OS speech foundation — but the PROSODY is ours: SSML composed from
///     the neural state, word by word. Dopamine-emphasized words carry
///     &lt;emphasis&gt;; hesitation ellipses become breath-fresh &lt;break&gt;s
///     scaled by serotonin (uncertainty = longer pauses); phrase pitch and
///     rate come from valence/arousal/melatonin; questions rise.
///
/// The LLM was always a prosthetic language CORTEX — this is the prosthetic
/// vocal TRACT, and the soul of the voice (what it emphasizes, where it
/// breathes, how it rises and falls) is still computed by the brain.
///
/// Output: one WAV (22.05 kHz 16-bit mono) with hums and speech stitched
/// in utterance order. Deterministic in structure; the SAPI render is a
/// pure function of the SSML.
/// </summary>
public sealed class VoiceCortex : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly VoiceEngine _engine = new();

    /// <summary>
    /// Injectable third verbal tract (e.g. a neural TTS owned by the host
    /// process). (text, rate, pitch, volume) -> wav bytes, or null to fall
    /// through to the SAPI tract. BioComp.Host stays dependency-free; the
    /// presentation layer owns the model.
    /// </summary>
    public static Func<string, float, float, float, byte[]?>? ExternalWordTract;

    public VoiceCortex()
    {
        // prefer the warmest installed voice (Zira reads gently; David is calm)
        var prefer = new[] { "Zira", "Aria", "Jenny", "Ana", "Hazel", "David", "Mark" };
        foreach (var p in prefer)
        {
            var match = _synth.GetInstalledVoices().FirstOrDefault(v =>
                v.VoiceInfo.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (match is not null) { _synth.SelectVoice(match.VoiceInfo.Name); break; }
        }
    }

    /// <summary>The SAPI voice in use (for status/diary).</summary>
    public string VoiceName => _synth.Voice?.Name ?? "default";

    /// <summary>
    /// Speak an utterance: split into non-verbal tokens (ours) and words
    /// (prosthetic tract with our prosody), render, stitch. Returns a WAV.
    /// </summary>
    public byte[] Speak(string text, float rate = 0.92f, float pitch = 1.0f, float volume = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var parts = Split(text);
        if (parts.Count == 0) return [];
        bool wordsFollow = parts.Any(p => !p.IsVocal);

        var wavs = new List<byte[]>();
        foreach (var (isVocal, body) in parts)
        {
            byte[] wav = isVocal
                // a hum BEFORE words is a soft pre-speech murmur (quiet, brief);
                // a hum ALONE is the utterance itself (full presence)
                ? _engine.Render(VoiceEngine.Match(body) ?? VoiceEngine.Vocal.Hum,
                    rate * (wordsFollow ? 1.25f : 1f), pitch, volume * (wordsFollow ? 0.6f : 0.95f))
                : SpeakWords(body, rate, pitch, volume);
            if (wav.Length > 100) wavs.Add(wav);
            // a small, QUIET breath between the layers keeps the stitching
            // organic without eeriness
            if (isVocal && wordsFollow)
                wavs.Add(_engine.Render(VoiceEngine.Vocal.Breath, rate, pitch, volume * 0.35f));
        }
        return Stitch(wavs);
    }

    /// <summary>
    /// Words through the prosthetic tract — with OUR prosody, tuned WARM:
    /// the mappings live in narrow, gentle ranges (wide pitch swings and
    /// fast SAPI rates read as eerie, not emotional), with a slight default
    /// rise (friendliness lives slightly above neutral) and a per-utterance
    /// micro-variation (seeded by the text) so identical sentences don't
    /// sound stapled — liveliness without chaos.
    /// </summary>
    private byte[] SpeakWords(string text, float rate, float pitch, float volume)
    {
        // NARROW ranges: ±10% speed, ±1.5 semitones — the human band
        int ratePct = (int)Math.Clamp((rate - 0.92f) * 30f, -10f, 10f);
        float pitchRaw = Math.Clamp((pitch - 1f) * 4f, -1.5f, 1.5f) + 0.8f;      // warmth bias
        // deterministic per-utterance micro-variation (±0.3 st)
        int micro = (Math.Abs(text.GetHashCode()) % 7) - 3;                      // -3..+3
        int pitchSt = (int)Math.Clamp(pitchRaw + micro * 0.1f, -2f, 2f);
        _synth.Rate = Math.Clamp(ratePct / 6, -2, 2);                            // SAPI ±2 max
        _synth.Volume = (int)Math.Clamp(volume * 90 + 10, 25, 95);               // never maxed

        // the external tract (neural TTS) gets first refusal when present
        if (ExternalWordTract is not null)
        {
            try
            {
                byte[]? neural = ExternalWordTract(text, rate, pitch, volume);
                if (neural is { Length: > 100 }) return neural;
            }
            catch { /* fall through to SAPI: the voice must never die with the model */ }
        }
        string ssml = BuildSsml(text, ratePct, pitchSt);
        using var ms = new MemoryStream();
        _synth.SetOutputToWaveStream(ms);
        try { _synth.SpeakSsml(ssml); }
        catch { return []; }
        return ms.ToArray();
    }

    /// <summary>Compose the SSML: emphasis on CAPS words, breaths at
    /// hesitation marks, phrase-final contours, escaped text.</summary>
    public static string BuildSsml(string text, int ratePct, int pitchSt)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<speak version=\"1.0\" xml:lang=\"en-US\">");
        sb.Append($"<prosody rate=\"{(ratePct >= 0 ? "+" : "")}{ratePct}%\" " +
                  $"pitch=\"{(pitchSt >= 0 ? "+" : "")}{pitchSt}st\">");

        // tokens keep their hesitation marks; interjection leftovers normalize per-word
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var w in words)
        {
            bool hesitates = w.EndsWith("...", StringComparison.Ordinal);
            string core = hesitates ? w[..^3] : w;
            string clean = core.Trim('.', ',', '!', '?', ';', ':');
            string tail = core[clean.Length..];
            clean = BrainTTS.NormalizeForSpeech(clean);
            bool emph = clean.Length > 2 && clean.All(char.IsLetter) &&
                        (clean == clean.ToUpperInvariant()) && char.IsUpper(clean[0]);
            string esc = System.Security.SecurityElement.Escape(clean);
            if (emph)
                // SOFT emphasis: a slight slow + lift reads as warmth; SAPI's
                // "moderate" emphasis can bark (part of the scary)
                sb.Append($"<prosody rate=\"-8%\" pitch=\"+1st\">{esc}</prosody>{tail} ");
            else sb.Append($"{esc}{tail} ");
            // hesitation: the ellipsis becomes a breath-fresh pause
            if (hesitates)
                sb.Append("<break time=\"380ms\"/> ");
        }
        sb.Append("</prosody></speak>");
        return sb.ToString();
    }

    /// <summary>Split an utterance into (isVocal, token) parts in order.</summary>
    private static List<(bool IsVocal, string Body)> Split(string text)
    {
        var parts = new List<(bool IsVocal, string Body)>();
        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string bare = token.Trim('.', ',', '!', '?').ToLowerInvariant();
            if (VoiceEngine.Match(bare) is not null)
                parts.Add((true, bare));
            else
            {
                if (parts.Count > 0 && parts[^1].IsVocal is false)
                {
                    // merge into the running word phrase
                    int i = parts.Count - 1;
                    parts[i] = (false, parts[i].Body + " " + token);
                }
                else parts.Add((false, token));
            }
        }
        return parts;
    }

    /// <summary>Concatenate 22.05k/16/mono WAVs (resampling linearly if a
    /// segment disagrees — SAPI default matches ours).</summary>
    private static byte[] Stitch(List<byte[]> wavs)
    {
        if (wavs.Count == 0) return [];
        if (wavs.Count == 1) return wavs[0];
        // gather sample streams
        var streams = new List<float[]>();
        foreach (var w in wavs)
        {
            var (samples, rate) = ReadWav(w);
            if (samples is null) continue;
            if (rate != 22_050) samples = Resample(samples, rate, 22_050);
            streams.Add(samples);
        }
        int total = streams.Sum(s => s.Length);
        var all = new float[total];
        int o = 0;
        foreach (var s in streams) { Array.Copy(s, 0, all, o, s.Length); o += s.Length; }
        // tiny crossfade at joins to avoid clicks
        const int fade = 220;
        int pos = 0;
        for (int k = 0; k < streams.Count - 1; k++)
        {
            pos += streams[k].Length;
            for (int i = 0; i < fade && pos - fade + i < all.Length; i++)
                all[pos - fade + i] *= i / (float)fade;
        }
        return WriteWav(all);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static (float[]? Samples, int Rate) ReadWav(byte[] w)
    {
        if (w.Length < 44 || w[0] != (byte)'R') return (null, 0);
        // walk the chunk list to find 'data' (some writers add LIST/fact chunks
        // before it - fixed offsets mis-slice those into garbage)
        int off = 12, dataOff = -1, dataLen = 0;
        int rate = 0; short bits = 0, ch = 0;
        while (off + 8 <= w.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(w, off, 4);
            int len = w[off + 4] | (w[off + 5] << 8) | (w[off + 6] << 16) | (w[off + 7] << 24);
            if (id == "fmt ")
            {
                ch = (short)(w[off + 10] | (w[off + 11] << 8));
                rate = w[off + 12] | (w[off + 13] << 8) | (w[off + 14] << 16) | (w[off + 15] << 24);
                bits = (short)(w[off + 22] | (w[off + 23] << 8));
            }
            else if (id == "data") { dataOff = off + 8; dataLen = len; break; }
            off += 8 + len + (len & 1);
        }
        if (dataOff < 0 || bits != 16 || ch != 1) return (null, 0);
        int n = Math.Min(dataLen, w.Length - dataOff) / 2;
        var s = new float[n];
        for (int i = 0; i < n; i++)
            s[i] = (short)(w[dataOff + i * 2] | (w[dataOff + 1 + i * 2] << 8)) / 32768f;
        return (s, rate);
    }

    private static float[] Resample(float[] s, int from, int to)
    {
        int n = (int)((long)s.Length * to / from);
        var r = new float[n];
        for (int i = 0; i < n; i++)
        {
            float x = i * (float)from / to;
            int i0 = (int)x; int i1 = Math.Min(i0 + 1, s.Length - 1);
            r[i] = Lerp(s[i0], s[i1], x - i0);
        }
        return r;
    }

    private static byte[] WriteWav(float[] samples)
    {
        float peak = 1e-6f;
        foreach (var s in samples) peak = MathF.Max(peak, MathF.Abs(s));
        float gain = 0.78f / peak;
        int n = samples.Length;
        byte[] wav = new byte[44 + n * 2];
        void P(int o, int v) { wav[o] = (byte)v; wav[o + 1] = (byte)(v >> 8); wav[o + 2] = (byte)(v >> 16); wav[o + 3] = (byte)(v >> 24); }
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        P(4, 36 + n * 2);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        P(16, 16); wav[20] = 1; wav[21] = 0; wav[22] = 1; wav[23] = 0;
        P(24, 22_050); P(28, 44_100); wav[32] = 2; wav[33] = 0; wav[34] = 16; wav[35] = 0;
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        P(40, n * 2);
        for (int i = 0; i < n; i++)
        {
            short v = (short)Math.Clamp(samples[i] * gain * 32767f, short.MinValue, short.MaxValue);
            wav[44 + i * 2] = (byte)v; wav[45 + i * 2] = (byte)(v >> 8);
        }
        return wav;
    }

    public void Dispose() => _synth.Dispose();
}
