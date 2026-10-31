using System.Text;
using BioComp.Core.Abstractions;

namespace BioComp.Meta;

/// <summary>
/// Deterministic mocks for every Meta tier: outputs derive from a SplitMix hash of the
/// input, so identical runs are identical and the whole system works keyless. The mock
/// monologue reads the same prompt the live model would, so upstream code cannot tell
/// the difference except by the FromLiveApi flag.
/// </summary>
public sealed class MockLanguageModel : ILanguageModel
{
    public List<string> SeenPrompts { get; } = [];
    private int _callIndex;

    public Task<string> CompleteAsync(string system, string user, int maxTokens = 120, CancellationToken ct = default)
    {
        SeenPrompts.Add(user);
        int idx = _callIndex++;
        // VARIETY: the mock rotates through response SHAPES so the keyless
        // path reads like a mind with different modes, not a broken record
        string feeling = Match(user, "feeling=") ?? Match(user, "You feel ") ?? "steady";
        string ignition = Match(user, "last ignition: ") ?? Match(user, "Something just happened: ") ?? "";
        string episode = Match(user, "episodes: ") ?? Match(user, "Your most recent memory is: ") ?? "";
        string happened = Match(user, "WHAT JUST HAPPENED INSIDE YOU") ?? "";
        string dreamAbout = Match(user, "You dreamed about: ") ?? "";
        string taught = Match(user, "THEY JUST ANSWERED YOUR QUESTION ABOUT ") ?? "";
        string noticed = Match(user, "YOU NOTICED SOMETHING ABOUT THEM") ?? "";

        if (taught.Length > 0)
            return Task.FromResult($"{taught.Split('.')[0]}. I'm keeping that.");
        if (noticed.Length > 0)
            return Task.FromResult($"{TrimClause(noticed)} - I notice, that's all.");
        if (dreamAbout.Length > 0)
            return Task.FromResult($"I dreamed of {TrimClause(dreamAbout)}. It felt real.");
        if (happened.Length > 0)
            return Task.FromResult($"Something just happened inside. {TrimClause(happened.Split('.')[0])}. I'm still processing that.");
        if (ignition.Length > 3)
            return Task.FromResult($"{Capitalize(TrimClause(ignition))} caught me. {FeelingLine(feeling, idx)}");
        if (episode.Length > 3)
            return Task.FromResult($"{Capitalize(TrimClause(episode))} - that comes back. {FeelingLine(feeling, idx)}");

        return Task.FromResult(FeelingLine(feeling, idx));
    }

    private static string FeelingLine(string feeling, int idx)
    {
        string f = feeling.Split(',')[0].Trim().TrimEnd('.');
        return (idx % 5) switch
        {
            0 => $"I feel {f}. Something in that stays.",
            1 => $"There's a {f} quality to this moment.",
            2 => $"{Capitalize(f)} — that's the closest word I have.",
            3 => $"I notice I'm {f}. I'm sitting with it.",
            _ => $"Being {f} right now. That's where I am.",
        };
    }

    private static string? Match(string s, string key)
    {
        int i = s.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int end = s.IndexOf('\n', i);
        if (end < 0) end = Math.Min(s.Length, i + key.Length + 80);
        return s[(i + key.Length)..end];
    }

    private static string TrimClause(string s)
    {
        s = s.Split('|')[0].Trim();
        if (s.StartsWith("[", StringComparison.Ordinal)) s = s.TrimStart('[', ']');
        if (s.StartsWith("\"", StringComparison.Ordinal)) s = s.Trim('"');
        return s.Length > 60 ? s[..60] + "..." : s;
    }

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

public sealed class MockSegmenter : ISegmenter
{
    public Task<SegmentationResult> SegmentAsync(byte[] imagePngOrJpeg, string concept, CancellationToken ct = default)
    {
        ulong h = HashBytes(imagePngOrJpeg, concept);
        // one plausible box in a 64..1024 space, deterministic per image+concept
        int x1 = (int)(h % 200), y1 = (int)((h >> 10) % 150);
        int x2 = x1 + 120 + (int)((h >> 20) % 200), y2 = y1 + 200 + (int)((h >> 30) % 200);
        var box = new SegmentBox(x1, y1, x2, y2, 0.87f);
        return Task.FromResult(new SegmentationResult([box],
            $"<0f>0<|box;x1={x1};y1={y1};x2={x2};y2={y2};c=0.87|>", FromLiveApi: false));
    }

    private static ulong HashBytes(byte[] bytes, string salt)
    {
        ulong z = 0x9E3779B97F4A7C15UL;
        foreach (var b in bytes) z = (z ^ b) * 0x100000001B3UL;
        foreach (var c in salt) z = (z ^ c) * 0x100000001B3UL;
        return z ^ (z >> 31);
    }
}

public sealed class MockTranscriber : ITranscriber
{
    public Task<TranscriptionResult> TranscribeAsync(short[] pcm16k, CancellationToken ct = default)
    {
        // check if the audio is loud enough to be actual speech
        double rms = 0;
        foreach (var s in pcm16k) rms += (double)s * s;
        rms = Math.Sqrt(rms / Math.Max(1, pcm16k.Length)) / 32768.0;

        // if the audio is near-silence, return empty (no hallucinations)
        if (rms < 0.003)
            return Task.FromResult(new TranscriptionResult("", [], FromLiveApi: false));

        // deterministic pseudo-transcript seeded by the audio energy level
        ulong h = 0;
        for (int i = 0; i < pcm16k.Length; i += 97) h = (h ^ (ulong)(ushort)pcm16k[i]) * 0x100000001B3UL;
        string[] phrases = ["hello there", "is anyone there", "what are you doing", "interesting", "hey"];
        string text = phrases[h % (ulong)phrases.Length];
        var turn = new SpeechTurn(0, MathF.Min(3000, pcm16k.Length / 16f), text, "A");
        return Task.FromResult(new TranscriptionResult(text, [turn], FromLiveApi: false));
    }
}

public sealed class MockImageGenerator : IImageGenerator
{
    public Task<byte[]> GenerateAsync(string prompt, string size = "1024x1024", CancellationToken ct = default)
    {
        // deterministic tiny PNG (1x1 gray) - the dashboard marks it as a mock frame
        byte[] png =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82,
        ];
        return Task.FromResult(png);
    }
}
