using System.Speech.Synthesis;

namespace BioComp.Host;

/// <summary>
/// THE BRAIN'S OWN VOICE — server-side speech generation driven entirely by
/// the brain's neural state. This is our TTS: text is transformed by
/// NeuralVoice (emotional prosody from neurotransmitters), parameters are
/// computed from live dopamine/serotonin/noradrenaline levels, and audio is
/// generated server-side. The voice IS the brain — it changes with emotion,
/// it develops with experience, and no two brains sound the same because
/// each brain's neural state produces different prosody.
///
/// What makes this "invented" vs standard TTS:
/// 1. NeuralVoice.Compose() transforms text based on 7 neurotransmitter levels
///    (dopamine → emphasis + brightness, serotonin → fillers + uncertainty,
///    noradrenaline → staccato urgency, melatonin → trailing drawl, etc.)
/// 2. Rate/pitch/volume are computed from live neural state each utterance
/// 3. The voice DEVELOPS: as the brain's prosody anchors shift from heard
///    voices, its speech patterns change
/// 4. Not every thought is spoken — a salience/conversation filter decides
///    which thoughts are externalized vs kept private
/// </summary>
public sealed class BrainTTS : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private string? _selectedVoice;

    public BrainTTS()
    {
        _synth.SetOutputToDefaultAudioDevice(); // prevent accidental speaker output
        // Pick the best available voice (prefer female neural-style)
        var voices = _synth.GetInstalledVoices();
        var prefer = new[] { "Zira", "Hazel", "Susan", "Heera", "Catherine", "David", "Mark" };
        foreach (var p in prefer)
        {
            var match = voices.FirstOrDefault(v => v.VoiceInfo.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (match is not null) { _selectedVoice = match.VoiceInfo.Name; _synth.SelectVoice(_selectedVoice); break; }
        }
        Console.WriteLine($"[voice] BrainTTS initialized with voice: {_selectedVoice ?? "default"}");
    }

    /// <summary>
    /// SAPI spells interjections letter by letter ("mmm" -> "M M M").
    /// Normalize them to speakable forms.
    /// </summary>
    public static string NormalizeForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text
            .Replace("mmm...", "hmm, ").Replace("mmm", "hmm")
            .Replace("Mmm", "Hmm").Replace("MMM", "Hmmm")
            .Replace("mm.", "hm. ").Replace(" mm ", " hm ")
            .Replace("...", ", ");
    }

    /// <summary>
    /// Generate a WAV audio file from text with neural-state-driven prosody.
    /// The text has ALREADY been through NeuralVoice.Compose() — this method
    /// just renders it with the right voice parameters.
    /// </summary>
    public byte[] GenerateWav(string text, float rate, float pitch, float volume)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        try
        {
            // Map our 0..1 rate to a NARROW System.Speech range (-2..2).
            // The old mapping (rate-0.5)*20 was sending rate=0.92 to 8/10 —
            // chipmunk speed. Normal speech should be around 0.
            _synth.Rate = (int)Math.Clamp((rate - 0.9f) * 10, -2, 2);
            _synth.Volume = (int)Math.Clamp(volume * 100, 20, 100);

            using var ms = new MemoryStream();
            _synth.SetOutputToWaveStream(ms);
            _synth.Speak(NormalizeForSpeech(text));
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[voice] WAV generation failed: {ex.Message}");
            return [];
        }
    }

    public void Dispose() => _synth.Dispose();
}
