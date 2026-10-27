using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BioComp.Core.Abstractions;

namespace BioComp.Meta;

/// <summary>Plain-HTTP clients for the Meta Model API (OpenAI-compatible chat plus the
/// SAM, voice and image endpoints). No SDK dependency; every call goes through the
/// CostGovernor. Schemas pinned from dev.meta.ai/docs (Oct 27 2026).</summary>
public sealed class MetaClients : ILanguageModel, ISegmenter, ITranscriber, IImageGenerator
{
    private readonly MetaOptions _opts;
    private readonly CostGovernor _cost;
    private readonly HttpClient _http;

    public MetaClients(MetaOptions opts, CostGovernor cost, HttpClient? http = null)
    {
        _opts = opts;
        _cost = cost;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        if (_http.DefaultRequestHeaders.Authorization is null && opts.HasKey)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey);
    }

    // ---- chat (Spark 1.3; also used against Baseten for Spark Open) ----------------

    public async Task<string> CompleteAsync(string system, string user, int maxTokens = 120, CancellationToken ct = default)
    {
        string? text = await _cost.RunAsync(MetaTier.Chat, async () =>
        {
            using var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/chat/completions", new
            {
                model = _opts.SparkModel,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                max_tokens = maxTokens,
            }, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message")
                .GetProperty("content").GetString();
        }, t => !string.IsNullOrWhiteSpace(t));
        return text ?? throw new InvalidOperationException("chat call failed or budget refused");
    }

    // ---- SAM 3.1 segmentation -------------------------------------------------------

    private static readonly Regex BoxToken = new(
        @"<\|box;x1=(\d+);y1=(\d+);x2=(\d+);y2=(\d+);w=(\d+);h=(\d+)(?:;c=([\d.]+))?\|>",
        RegexOptions.Compiled);

    public async Task<SegmentationResult> SegmentAsync(byte[] imagePngOrJpeg, string concept, CancellationToken ct = default)
    {
        return await _cost.RunAsync(MetaTier.Segment, async () =>
        {
            bool jpeg = imagePngOrJpeg.Length > 2 && imagePngOrJpeg[0] == 0xFF;
            string mime = jpeg ? "image/jpeg" : "image/png";
            string dataUrl = $"data:{mime};base64,{Convert.ToBase64String(imagePngOrJpeg)}";
            using var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/responses", new
            {
                model = _opts.SamModel,
                stream = false,
                input = new object[]
                {
                    new
                    {
                        type = "message",
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = concept },
                            new { type = "input_image", image_url = dataUrl },
                        },
                    },
                },
            }, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            // non-streaming: assembled text lives in output_text (or output[].content)
            string raw = "";
            if (doc.RootElement.TryGetProperty("output_text", out var ot))
                raw = ot.GetString() ?? "";
            else if (doc.RootElement.TryGetProperty("output", out var output))
                foreach (var item in output.EnumerateArray())
                    if (item.TryGetProperty("content", out var content))
                        foreach (var part in content.EnumerateArray())
                            if (part.TryGetProperty("text", out var t))
                                raw += t.GetString();
            var boxes = BoxToken.Matches(raw)
                .Select(m => new SegmentBox(
                    int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                    int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value),
                    m.Groups[7].Success ? float.Parse(m.Groups[7].Value) : 1f))
                .ToArray();
            return new SegmentationResult(boxes, raw, FromLiveApi: true);
        }, r => r.FromLiveApi);
    }

    // ---- Muse Voice Transcribe (file mode) -------------------------------------------

    public async Task<TranscriptionResult> TranscribeAsync(short[] pcm16k, CancellationToken ct = default)
    {
        return await _cost.RunAsync(MetaTier.Voice, async () =>
        {
            byte[] wav = WavWriter.ToWav(pcm16k, 16_000);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(
                JsonSerializer.Serialize(new { mode = "DIARIZATION", model = _opts.VoiceModel, audioEncoding = "WAV" }),
                Encoding.UTF8, "application/json"), "request");
            var audio = new ByteArrayContent(wav);
            audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(audio, "audio", "clip.wav");
            using var resp = await _http.PostAsync($"{_opts.BaseUrl}/asr/transcribe", form, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            string transcript = doc.RootElement.TryGetProperty("transcript", out var tr) ? tr.GetString() ?? "" : "";
            var turns = new List<SpeechTurn>();
            if (doc.RootElement.TryGetProperty("turns", out var turnArr))
                foreach (var t in turnArr.EnumerateArray())
                    turns.Add(new SpeechTurn(
                        t.TryGetProperty("startMs", out var s) ? s.GetSingle() : 0,
                        t.TryGetProperty("endMs", out var e) ? e.GetSingle() : 0,
                        t.TryGetProperty("transcript", out var tx) ? tx.GetString() ?? "" : "",
                        t.TryGetProperty("speaker", out var sp) ? sp.GetString() ?? "" : ""));
            return new TranscriptionResult(transcript, turns.ToArray(), FromLiveApi: true);
        }, r => r.FromLiveApi);
    }

    // ---- Muse Image -------------------------------------------------------------------

    public async Task<byte[]> GenerateAsync(string prompt, string size = "1024x1024", CancellationToken ct = default)
    {
        byte[]? png = await _cost.RunAsync(MetaTier.Image, async () =>
        {
            using var resp = await _http.PostAsJsonAsync($"{_opts.BaseUrl}/images/generations", new
            {
                model = _opts.ImageModel,
                prompt,
                n = 1,
                size,
                response_format = "b64_json",
                output_format = "png",
            }, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            string b64 = doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString() ?? "";
            return Convert.FromBase64String(b64);
        }, b => b is { Length: > 100 });
        return png ?? throw new InvalidOperationException("image call failed or budget refused");
    }
}

/// <summary>Minimal RIFF/WAVE writer: mono 16-bit PCM. No external audio dependency.</summary>
public static class WavWriter
{
    public static byte[] ToWav(short[] samples, int sampleRate)
    {
        int dataLen = samples.Length * 2;
        var wav = new byte[44 + dataLen];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(36 + dataLen).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wav, 8);
        BitConverter.GetBytes(16).CopyTo(wav, 16);          // fmt chunk size
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);    // PCM
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);    // mono
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28); // byte rate
        BitConverter.GetBytes((short)2).CopyTo(wav, 32);    // block align
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);   // bits
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(dataLen).CopyTo(wav, 40);
        Buffer.BlockCopy(samples, 0, wav, 44, dataLen);
        return wav;
    }
}
