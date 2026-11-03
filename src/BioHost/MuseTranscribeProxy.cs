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
///   - audioEncoding is "PCM_24KHZ" (full recognition bandwidth) or "PCM_16KHZ"
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
    /// Format: authorization.accessToken (Bearer), audioEncoding (PCM_24KHZ —
    /// Muse's full recognition bandwidth; 16k throws away the top half),
    /// model (muse-voice-transcribe-1.0), mode (ENDPOINTING).
    /// languageBias + keywords bias recognition toward the actual
    /// conversation vocabulary (names and domain words the user really says).
    /// </summary>
    public async Task<bool> ConnectAsync(string apiKey, string model = "muse-voice-transcribe-1.0",
        CancellationToken ct = default, string[]? keywords = null, string[]? languageBias = null)
    {
        try
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _meta = new ClientWebSocket();
            _meta.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            await _meta.ConnectAsync(new Uri("wss://api.meta.ai/v1/asr/realtime"), _cts.Token);

            // HANDSHAKE — try the documented format; log everything for debugging
            var hs = new Dictionary<string, object>
            {
                ["authorization"] = new Dictionary<string, string> { ["accessToken"] = $"Bearer {apiKey}" },
                ["audioEncoding"] = "PCM_24KHZ",
                ["model"] = model,
                ["mode"] = "ENDPOINTING",
                ["partialMode"] = "CUMULATIVE",
                ["emitAudioProgress"] = false,
            };
            if (languageBias is { Length: > 0 }) hs["languageBias"] = languageBias;
            if (keywords is { Length: > 0 }) hs["keywords"] = keywords;
            var handshake = JsonSerializer.Serialize(hs);
            Console.WriteLine($"[transcribe-proxy] sending handshake: {handshake[..Math.Min(100, handshake.Length)]}...");
            var hsBytes = Encoding.UTF8.GetBytes(handshake);
            await _meta.SendAsync(new ArraySegment<byte>(hsBytes), WebSocketMessageType.Text, true, _cts.Token);

            // read the ack: {"sessionId": "..."} (no type field on this one)
            var ackBuf = new byte[4096];
            var ackResult = await _meta.ReceiveAsync(new ArraySegment<byte>(ackBuf), _cts.Token);
            if (ackResult.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine($"[transcribe-proxy] server closed during handshake, code {ackResult.CloseStatus}");
                return false;
            }
            var ackJson = Encoding.UTF8.GetString(ackBuf, 0, ackResult.Count);
            Console.WriteLine($"[transcribe-proxy] ack received: {ackJson[..Math.Min(200, ackJson.Length)]}");
            try
            {
                using var doc = JsonDocument.Parse(ackJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("sessionId", out var sid))
                    SessionId = sid.GetString();
                // check for error in ANY format (error, message, type=error)
                string? errorMsg = null;
                if (root.TryGetProperty("error", out var err))
                    errorMsg = err.ToString();
                else if (root.TryGetProperty("message", out var msg) && !msg.ValueKind.Equals(JsonValueKind.Object))
                    errorMsg = msg.GetString();
                else if (root.TryGetProperty("type", out var typ) && typ.GetString() == "error")
                {
                    errorMsg = root.TryGetProperty("message", out var m2) ? m2.GetString() : "unknown error";
                }
                if (errorMsg is not null)
                {
                    Console.WriteLine($"[transcribe-proxy] HANDSHAKE REJECTED: {errorMsg}");
                    return false;
                }
                if (SessionId is null && !root.TryGetProperty("sessionId", out _))
                {
                    Console.WriteLine($"[transcribe-proxy] ack has no sessionId and no error — treating as failure: {ackJson}");
                    return false;
                }
            }
            catch (JsonException)
            {
                Console.WriteLine($"[transcribe-proxy] ack is not valid JSON: {ackJson[..Math.Min(100, ackJson.Length)]}");
                return false;
            }

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
