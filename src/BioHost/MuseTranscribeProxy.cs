using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BioComp.Host;

/// <summary>
/// Real-time speech transcription proxy: bridges browser WebSocket ↔ Meta's
/// Muse Voice real-time ASR WebSocket. The browser sends raw PCM audio; we
/// relay it to Meta and return live partial/final transcripts.
///
/// HANDSHAKE CRITICAL: Meta authenticates via the FIRST JSON frame after
/// connection — NOT via HTTP Authorization headers (which are ignored for
/// WebSocket upgrades). The handshake frame must arrive within 10 seconds.
/// </summary>
public sealed class MuseTranscribeProxy : IDisposable
{
    private ClientWebSocket? _meta;
    private CancellationTokenSource? _cts;
    private Task? _relayTask;

    public bool IsConnected => _meta?.State == WebSocketState.Open;

    /// <summary>
    /// Connect to Meta's real-time ASR endpoint and send the auth handshake.
    /// The API key goes in the first JSON frame, not in headers.
    /// </summary>
    public async Task<bool> ConnectAsync(string apiKey, string model = "muse-voice", CancellationToken ct = default)
    {
        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _meta = new ClientWebSocket();
            _meta.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            await _meta.ConnectAsync(new Uri("wss://api.meta.ai/v1/asr/realtime"), _cts.Token);

            // HANDSHAKE: authentication is in the first frame, not headers.
            // Must arrive within 10 seconds of connection.
            var handshake = JsonSerializer.Serialize(new
            {
                type = "handshake",
                api_key = apiKey,
                model = model,
                mode = "ENDPOINTING",           // auto-detect end of speech
                audio_format = "PCM_S16LE",     // 16-bit little-endian
                sample_rate = 16000,
                channels = 1,
            });
            var hsBytes = Encoding.UTF8.GetBytes(handshake);
            await _meta.SendAsync(new ArraySegment<byte>(hsBytes), WebSocketMessageType.Text, true, _cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[transcribe-proxy] connect failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send a chunk of raw 16-bit PCM audio to Meta.
    /// The browser sends 16kHz mono; we relay as binary frames.
    /// </summary>
    public async Task SendAudioAsync(byte[] pcm, CancellationToken ct = default)
    {
        if (_meta?.State != WebSocketState.Open) return;
        try
        {
            await _meta.SendAsync(new ArraySegment<byte>(pcm), WebSocketMessageType.Binary, true, ct);
        }
        catch { /* connection may have timed out; caller should reconnect */ }
    }

    /// <summary>
    /// Read the next transcript event from Meta. Returns null on disconnect.
    /// Events include: partial_transcript, speech_complete, session_expired.
    /// </summary>
    public async Task<TranscriptEvent?> ReceiveAsync(CancellationToken ct = default)
    {
        if (_meta?.State != WebSocketState.Open) return null;
        try
        {
            var buffer = new byte[16384];
            var result = await _meta.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return ParseEvent(json);
        }
        catch { return null; }
    }

    private static TranscriptEvent? ParseEvent(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

            return type switch
            {
                "partial_transcript" => new TranscriptEvent(
                    Type: "partial",
                    Text: root.TryGetProperty("transcript", out var tx) ? tx.GetString() ?? "" : "",
                    IsFinal: false),
                "speech_complete" or "final_transcript" => new TranscriptEvent(
                    Type: "final",
                    Text: root.TryGetProperty("transcript", out var tx) ? tx.GetString() ?? "" : "",
                    IsFinal: true),
                "session_expired" => new TranscriptEvent(Type: "expired", Text: "", IsFinal: false),
                "error" => new TranscriptEvent(
                    Type: "error",
                    Text: root.TryGetProperty("message", out var m) ? m.GetString() ?? "unknown" : "unknown",
                    IsFinal: false),
                _ => new TranscriptEvent(Type: type, Text: "", IsFinal: false),
            };
        }
        catch { return null; }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _meta?.Dispose(); } catch { }
    }

    public sealed record TranscriptEvent(string Type, string Text, bool IsFinal);
}
