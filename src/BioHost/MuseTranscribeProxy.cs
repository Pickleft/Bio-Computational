using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BioComp.Host;

/// <summary>
/// Real-time speech transcription proxy: bridges browser WebSocket ↔ Meta's
/// Muse Voice real-time ASR WebSocket (muse-voice-transcribe-1.0).
///
/// HANDSHAKE FORMAT (from https://dev.meta.ai/docs/speech-to-text):
///   - Auth is in "authorization.accessToken" as "Bearer <key>" (NOT api_key)
///   - audioEncoding is "PCM_16KHZ" or "PCM_24KHZ" (NOT "PCM_S16LE")
///   - model is "muse-voice-transcribe-1.0" (NOT "muse-voice")
///   - The handshake has NO "type" field
///   - Server replies with {"sessionId": "..."} (no type field)
///   - All subsequent server frames DO have a "type" field
///
/// AUDIO: raw signed 16-bit LE mono PCM in BINARY frames, paced at real time.
/// End with {"type":"endStream"}. Server closes with code 1000.
/// </summary>
public sealed class MuseTranscribeProxy : IDisposable
{
    private ClientWebSocket? _meta;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _meta?.State == WebSocketState.Open;
    public string? SessionId { get; private set; }

    /// <summary>
    /// Connect to Meta's real-time ASR endpoint and send the handshake.
    /// Format: authorization.accessToken (Bearer), audioEncoding (PCM_16KHZ),
    /// model (muse-voice-transcribe-1.0), mode (ENDPOINTING).
    /// </summary>
    public async Task<bool> ConnectAsync(string apiKey, string model = "muse-voice-transcribe-1.0", CancellationToken ct = default)
    {
        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _meta = new ClientWebSocket();
            _meta.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            await _meta.ConnectAsync(new Uri("wss://api.meta.ai/v1/asr/realtime"), _cts.Token);

            // HANDSHAKE — exact format from Meta's docs:
            // auth in "authorization.accessToken" as "Bearer <key>"
            // audioEncoding uses Meta's naming (PCM_16KHZ, not PCM_S16LE)
            var handshake = JsonSerializer.Serialize(new
            {
                authorization = new { accessToken = $"Bearer {apiKey}" },
                audioEncoding = "PCM_16KHZ",
                model = model,
                mode = "ENDPOINTING",
                partialMode = "CUMULATIVE",
                emitAudioProgress = false,
            });
            var hsBytes = Encoding.UTF8.GetBytes(handshake);
            await _meta.SendAsync(new ArraySegment<byte>(hsBytes), WebSocketMessageType.Text, true, _cts.Token);

            // read the ack: {"sessionId": "..."} (no type field on this one)
            var ackBuf = new byte[4096];
            var ackResult = await _meta.ReceiveAsync(new ArraySegment<byte>(ackBuf), _cts.Token);
            if (ackResult.MessageType == WebSocketMessageType.Close) return false;
            var ackJson = Encoding.UTF8.GetString(ackBuf, 0, ackResult.Count);
            try
            {
                using var doc = JsonDocument.Parse(ackJson);
                if (doc.RootElement.TryGetProperty("sessionId", out var sid))
                    SessionId = sid.GetString();
                // check for error in the ack
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    Console.WriteLine($"[transcribe-proxy] handshake error: {err}");
                    return false;
                }
            }
            catch { }

            Console.WriteLine($"[transcribe-proxy] connected, session {SessionId}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[transcribe-proxy] connect failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send a chunk of raw 16-bit PCM audio to Meta (binary frames).
    /// Browser sends 16kHz mono; we relay as-is.
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
    /// Signal end of audio stream. Server responds with final transcript then closes.
    /// </summary>
    public async Task EndStreamAsync(CancellationToken ct = default)
    {
        if (_meta?.State != WebSocketState.Open) return;
        try
        {
            var endBytes = Encoding.UTF8.GetBytes("""{"type":"endStream"}""");
            await _meta.SendAsync(new ArraySegment<byte>(endBytes), WebSocketMessageType.Text, true, ct);
        }
        catch { }
    }

    /// <summary>
    /// Read the next transcript event from Meta. Returns null on disconnect.
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
