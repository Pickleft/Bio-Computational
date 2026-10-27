namespace BioComp.Host;

/// <summary>Speech output behind an interface: console for tests/headless, Windows TTS live.</summary>
public interface ISpeechOutput
{
    void Speak(string text);
    bool IsSpeaking { get; }
}

public sealed class ConsoleSpeech : ISpeechOutput
{
    public bool IsSpeaking => false;
    public void Speak(string text) => Console.WriteLine($"[spoken] {text}");
}

/// <summary>Windows SAPI voice via System.Speech with emotional modulation.
/// Pitch and rate shift with the brain's state: tired = slow and deep,
/// curious = faster and higher, calm = measured, stressed = fast and tense.
/// The voice IS the brain's emotional state made audible.</summary>
public sealed class WindowsSpeech : ISpeechOutput, IDisposable
{
    private readonly System.Speech.Synthesis.SpeechSynthesizer _synth = new();

    public float PitchBias { get; set; } = 0;  // -10 to +10
    public float RateBias { get; set; } = 0;   // -5 to +5

    public WindowsSpeech()
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.Rate = 0;
        _synth.Volume = 90;
        // select a natural-sounding voice if available
        var voice = _synth.GetInstalledVoices().FirstOrDefault(v =>
            v.VoiceInfo.Name.Contains("Zira") || v.VoiceInfo.Name.Contains("David") ||
            v.VoiceInfo.Name.Contains("Natural"))?.VoiceInfo;
        if (voice is not null) _synth.SelectVoice(voice.Name);
    }

    /// <summary>Speak with emotional modulation. Call SetEmotionalState before this.</summary>
    public bool IsSpeaking => _synth.State == System.Speech.Synthesis.SynthesizerState.Speaking;

    public void Speak(string text)
    {
        if (IsSpeaking) return;
        // apply emotional pitch/rate (Windows TTS uses SSML for pitch)
        int clampedPitch = Math.Clamp((int)PitchBias, -10, 10);
        int clampedRate = Math.Clamp((int)RateBias, -5, 5);
        _synth.Rate = clampedRate;
        if (clampedPitch != 0)
        {
            string pitchTag = clampedPitch > 0
                ? $"<pitch absmiddle=\"{clampedPitch * 10}\"/>"
                : $"<pitch absmiddle=\"{-clampedPitch * 10}\"/>";
            _synth.SpeakSsml($"<speak version=\"1.0\" xml:lang=\"en-US\">{pitchTag}{System.Security.SecurityElement.Escape(text)}</speak>");
        }
        else
        {
            _synth.SpeakAsync(text);
        }
    }

    public void Dispose() => _synth.Dispose();
}
