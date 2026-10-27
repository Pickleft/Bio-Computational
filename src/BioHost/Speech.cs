namespace BioComp.Host;

/// <summary>Speech output behind an interface: console for tests/headless, Windows TTS live.</summary>
public interface ISpeechOutput
{
    void Speak(string text);
}

public sealed class ConsoleSpeech : ISpeechOutput
{
    public void Speak(string text) => Console.WriteLine($"[spoken] {text}");
}

/// <summary>Windows SAPI voice via System.Speech. Fire-and-forget so the loop never blocks.</summary>
public sealed class WindowsSpeech : ISpeechOutput, IDisposable
{
    private readonly System.Speech.Synthesis.SpeechSynthesizer _synth = new();

    public WindowsSpeech()
    {
        _synth.SetOutputToDefaultAudioDevice();
        _synth.Rate = 0;
        _synth.Volume = 90;
    }

    public void Speak(string text)
    {
        if (_synth.State == System.Speech.Synthesis.SynthesizerState.Speaking) return; // no overlap
        _synth.SpeakAsync(text);
    }

    public void Dispose() => _synth.Dispose();
}
