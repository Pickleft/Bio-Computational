namespace BioComp.Core.Abstractions;

/// <summary>Chat-style language model (Muse Spark via cloud; any OpenAI-compatible
/// endpoint; deterministic mock for keyless runs). Models never drive neural dynamics:
/// they verbalize state the dynamics produced.</summary>
public interface ILanguageModel
{
    Task<string> CompleteAsync(string system, string user, int maxTokens = 120, CancellationToken ct = default);
}

public readonly record struct SegmentBox(int X1, int Y1, int X2, int Y2, float Confidence);

public readonly record struct SegmentationResult(SegmentBox[] Boxes, string RawOutput, bool FromLiveApi);

/// <summary>Promptable segmentation (SAM 3.1): one concept, one image, boxes back.</summary>
public interface ISegmenter
{
    Task<SegmentationResult> SegmentAsync(byte[] imagePngOrJpeg, string concept, CancellationToken ct = default);
}

public readonly record struct SpeechTurn(float StartMs, float EndMs, string Transcript, string Speaker);

public readonly record struct TranscriptionResult(string Transcript, SpeechTurn[] Turns, bool FromLiveApi);

/// <summary>Speech to text with per-turn speaker labels (Muse Voice Transcribe).</summary>
public interface ITranscriber
{
    /// <summary>Audio: mono 16-bit PCM at 16 kHz.</summary>
    Task<TranscriptionResult> TranscribeAsync(short[] pcm16k, CancellationToken ct = default);
}

/// <summary>Image generation (Muse Image): dreams and mental imagery. The prompt is
/// deterministic (derived from episode state); returned pixels are not seedable.</summary>
public interface IImageGenerator
{
    Task<byte[]> GenerateAsync(string prompt, string size = "1024x1024", CancellationToken ct = default);
}
