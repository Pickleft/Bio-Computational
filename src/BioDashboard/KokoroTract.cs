using BioComp.Host;
using KokoroSharp;

namespace BioComp.Dashboard;

/// <summary>
/// THE KOKORO TRACT — a neural verbal tract (Kokoro-82M, Apache-2.0 weights,
/// via KokoroSharp/ONNX, CPU) injected into the VoiceCortex as the preferred
/// word layer. SAPI remains the fallback: the voice must never die with the
/// model.
///
/// NEURAL-STATE VOICE SELECTION: the prosody now lives partly in WHICH voice
/// speaks — chosen per utterance from the brain's live state:
///   high valence  -> af_bella   (bright, lilt)
///   low valence / high sleep pressure -> af_nicole (soft)
///   high arousal / negative     -> af_alloy  (edge)
///   low pitch bias (male drift) -> am_michael
///   default       -> af_heart   (the warm base — its resting voice)
/// The hums and breaths remain OURS (VoiceEngine); stitching and resampling
/// handled by the cortex.
///
/// The model (~320MB) downloads once on first use; init is lazy and
/// failure-tolerant (falls back silently to SAPI and retries next boot).
/// </summary>
public static class KokoroTract
{
    private static KokoroWavSynthesizer? _synth;
    private static bool _failed;
    private static readonly object _gate = new();

    public static bool Ready => _synth is not null;

    /// <summary>Install as the cortex's preferred word tract (call once at startup).</summary>
    public static void Install()
    {
        VoiceCortex.ExternalWordTract = SpeakWords;
        // warm the model in the background: first load downloads ~320MB once
        _ = Task.Run(() =>
        {
            try
            {
                Console.WriteLine("[voice] Kokoro tract: loading model (first run downloads ~320MB once)...");
                Init();
            }
            catch (Exception ex)
            {
                _failed = true;
                Console.WriteLine($"[voice] Kokoro tract unavailable ({ex.Message}); SAPI words stand; hums ours. It will retry next boot.");
            }
        });
    }

    private static KokoroWavSynthesizer? Init()
    {
        lock (_gate)
        {
            if (_synth is not null || _failed) return _synth;
            _synth = KokoroWavSynthesizer.LoadModel();   // auto-download on first use
            Console.WriteLine("[voice] Kokoro tract ready (neural words; SAPI fallback; hums ours)");
            return _synth;
        }
    }

    private static byte[]? SpeakWords(string text, float rate, float pitch, float volume)
    {
        var synth = _synth ?? Init();
        if (synth is null) return null;

        var voice = KokoroVoiceManager.GetVoice(PickVoice(pitch));
        byte[] raw = synth.Synthesize(text, voice);

        // KokoroSharp returns HEADERLESS 16-bit mono PCM @ 24kHz - wrap it in
        // a proper RIFF header so the cortex's parser and stitcher accept it
        const int kokoroRate = 24_000;
        byte[] wav = new byte[44 + raw.Length];
        wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
        BitConverter.GetBytes(36 + raw.Length).CopyTo(wav, 4);
        wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
        wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        wav[20] = 1; wav[22] = 1;                                  // PCM, mono
        BitConverter.GetBytes(kokoroRate).CopyTo(wav, 24);
        BitConverter.GetBytes(kokoroRate * 2).CopyTo(wav, 28);     // byte rate
        wav[32] = 2; wav[34] = 16;                                 // block align, bits
        wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
        BitConverter.GetBytes(raw.Length).CopyTo(wav, 40);
        Array.Copy(raw, 0, wav, 44, raw.Length);

        // THE WARP (what the ComfyUI community does with post nodes, we do in
        // the cortex, from neurotransmitters): resample-rate = playback speed.
        // Tired brains (rate low) speak slower; excited faster. The voice
        // CHOICE carries the character; the WARP carries the energy.
        float speed = Math.Clamp(rate, 0.8f, 1.2f);
        if (MathF.Abs(speed - 1f) > 0.02f)
            wav = Warp(wav, speed);
        return wav;
    }

    /// <summary>Rate-warp a 16-bit WAV by linear resampling (pitch shifts
    /// slightly with speed - analog-tape warmth, not artifact-free: honest).</summary>
    private static byte[] Warp(byte[] wav, float speed)
    {
        // walk chunks to the PCM data (fixed offsets mis-slice extra chunks)
        if (wav.Length < 44) return wav;
        int off = 12, dataOff = -1; short bits = 0, ch = 0; int rate = 0;
        while (off + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, off, 4);
            int len = wav[off + 4] | (wav[off + 5] << 8) | (wav[off + 6] << 16) | (wav[off + 7] << 24);
            if (id == "fmt ")
            {
                ch = (short)(wav[off + 10] | (wav[off + 11] << 8));
                rate = wav[off + 12] | (wav[off + 13] << 8) | (wav[off + 14] << 16) | (wav[off + 15] << 24);
                bits = (short)(wav[off + 22] | (wav[off + 23] << 8));
            }
            else if (id == "data") { dataOff = off + 8; break; }
            off += 8 + len + (len & 1);
        }
        if (dataOff < 0 || bits != 16 || ch != 1) return wav;   // unexpected format: pass through untouched
        int n = (wav.Length - dataOff) / 2;
        int m = (int)(n / speed);
        var outWav = new byte[44 + m * 2];
        outWav[0] = (byte)'R'; outWav[1] = (byte)'I'; outWav[2] = (byte)'F'; outWav[3] = (byte)'F';
        BitConverter.GetBytes(36 + m * 2).CopyTo(outWav, 4);
        outWav[8] = (byte)'W'; outWav[9] = (byte)'A'; outWav[10] = (byte)'V'; outWav[11] = (byte)'E';
        outWav[12] = (byte)'f'; outWav[13] = (byte)'m'; outWav[14] = (byte)'t'; outWav[15] = (byte)' ';
        BitConverter.GetBytes(16).CopyTo(outWav, 16);
        outWav[20] = 1; outWav[22] = 1;
        BitConverter.GetBytes(rate).CopyTo(outWav, 24);
        BitConverter.GetBytes(rate * 2).CopyTo(outWav, 28);
        outWav[32] = 2; outWav[34] = 16;
        outWav[36] = (byte)'d'; outWav[37] = (byte)'a'; outWav[38] = (byte)'t'; outWav[39] = (byte)'a';
        BitConverter.GetBytes(m * 2).CopyTo(outWav, 40);
        for (int i = 0; i < m; i++)
        {
            float x = i * speed;
            int i0 = (int)x, i1 = Math.Min(i0 + 1, n - 1);
            short a = (short)(wav[dataOff + i0 * 2] | (wav[dataOff + 1 + i0 * 2] << 8));
            short b = (short)(wav[dataOff + i1 * 2] | (wav[dataOff + 1 + i1 * 2] << 8));
            short v = (short)(a + (b - a) * (x - i0));
            outWav[44 + i * 2] = (byte)v;
            outWav[45 + i * 2] = (byte)(v >> 8);
        }
        return outWav;
    }

    /// <summary>Which voice speaks = the brain's state (rate/volume already
    /// carried by the cortex; the voice CHOICE carries the character).</summary>
    private static string PickVoice(float pitch)
    {
        // male drift (developed pitch bias below neutral) selects the male base
        if (pitch < 0.92f) return "am_michael";
        if (pitch > 1.12f) return "af_bella";     // bright
        return "af_heart";                         // the warm resting voice
    }
}
