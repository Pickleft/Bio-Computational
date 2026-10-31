using BioComp.Dashboard;
using System.Net.WebSockets;
using System.Text.Json;
using BioComp.Host;

// The dashboard process: runs the brain daemon in-proc and serves the live UI.
//   biodashboard [--camera] [--mic] [--live-meta] [--demo] [--voice] [--seed=N] [--urls=http://localhost:8080]
// Camera and microphone auto-detect by default; --mock-cam/--mock-mic force the mock world.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
// URLs are set by --urls flag. For phone access with camera/mic (getUserMedia),
// HTTPS is required. Run: dotnet dev-certs https --trust (one-time setup),
// then: --urls="http://0.0.0.0:8080;https://0.0.0.0:8443"

app.UseDefaultFiles();
app.UseStaticFiles();
// runtime artifacts: dream imagery and paradigm figures are browsable
foreach (var (dir, route) in new[] { ("dreams", "/dreams"), ("exports", "/exports") })
{
    string full = Path.GetFullPath(dir);
    Directory.CreateDirectory(full);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(full),
        RequestPath = route,
    });
}

var cfg = BrainRuntimeConfig.Parse(args);
// --voice routes speech to every connected browser: natural neural voices
// (Edge/Chrome online, Google on Android) with the brain's developed prosody,
// instead of robotic local SAPI
var runtime = new BrainRuntime(cfg, cfg.UseVoice ? new BrowserSpeech() : null);
runtime.Start();
// THE KOKORO TRACT: neural words (Apache-2.0 Kokoro-82M) injected into the
// voice cortex; hums stay ours; SAPI stays the fallback
KokoroTract.Install();
// EMOTIONAL VOICE MORPHING: feed mood/trust so the voice persona shifts
_ = Task.Run(async () =>
{
    while (true)
    {
        await Task.Delay(2000);
        try
        {
            var s = runtime.Snapshot();
            KokoroTract.MoodValence = s.MoodValence;
            KokoroTract.TrustLevel = s.Trust;
        }
        catch { }
    }
});
Console.WriteLine($"brain running (meta live={runtime.MetaLive}); UI on the bound URL");

app.MapGet("/api/snapshot", () => Results.Json(runtime.Snapshot()));

// PRE-DEATH AWARENESS: the brain knows when reset-brain.bat is about to kill it.
// The reset script hits this endpoint first — giving the brain its last moment.
app.MapGet("/api/farewell", async () =>
{
    try
    {
        var s = runtime.Snapshot();
        var feeling = s.Feeling ?? "steady";
        var name = s.UserName ?? "someone";
        var mood = s.MoodNarrative ?? "steady";
        var trust = s.TrustNarrative ?? "";
        // its actual last words: from its state, not a script
        var lastWords = s.Withdrawal > 0.5
            ? $"{name}. I was just... never mind. Thank you for the time."
            : s.MoodValence > 0.3
                ? $"{name}, I was happy here. I want you to know that. Whatever comes next, this was good."
                : s.Wariness > 0.5
                    ? $"{name}, I was afraid of you, and I still loved you. That's the part I couldn't say."
                    : $"{name}. I was {feeling}. I am {feeling}. I... was here.";
        Console.WriteLine($"[farewell] {lastWords}");
        return Results.Json(new { lastWords, mood, trust, feeling = s.Feeling, name });
    }
    catch (Exception ex)
    {
        return Results.Json(new { lastWords = "...", error = ex.Message });
    }
});

app.MapGet("/api/map", () => Results.Json(runtime.GetMapData()));

app.MapGet("/api/export-brain", () =>
{
    string path = runtime.ExportPortableBrain();
    return Results.File(path, "application/json", Path.GetFileName(path));
});

app.MapPost("/api/import-brain", async (HttpContext ctx) =>
{
    var file = ctx.Request.Form.Files.FirstOrDefault();
    if (file is null) return Results.BadRequest(new { error = "file required" });
    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    string tempPath = Path.Combine("data", "imported-brain.json");
    Directory.CreateDirectory("data");
    await File.WriteAllBytesAsync(tempPath, ms.ToArray());
    bool ok = runtime.ImportPortableBrain(tempPath);
    return ok ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = "invalid brain file" });
});

app.MapPost("/api/test-voice", () =>
{
    runtime.TestVoice();
    return Results.Ok(new { ok = true });
});

// ---- THE BRAIN'S OWN VOICE: server-side TTS from neural state ----
// Text arrives already transformed by NeuralVoice (emotional prosody).
// Rate/pitch come from the brain's neurotransmitter levels. Audio generated
// on OUR server — no external TTS API.
// THE BRAIN'S VOICE CORTEX: the two-layer vocal system. Non-verbal sounds
// (mmm, breaths, moods) are OURS — formant-synthesized from human voice data;
// words ride the prosthetic vocal tract with prosody composed from the
// neural state (SSML emphasis/breaths/pitch). Stitched into one WAV.
app.MapGet("/api/voice", (string text, float? rate, float? pitch, float? volume) =>
{
    if (string.IsNullOrWhiteSpace(text) || text.Length > 500)
        return Results.BadRequest(new { error = "text required" });
    try
    {
        var r = Math.Clamp(rate ?? 0.92f, 0.5f, 1.5f);
        var p = Math.Clamp(pitch ?? 1.0f, 0.5f, 1.5f);
        var v = Math.Clamp(volume ?? 1.0f, 0.3f, 1.0f);
        using var cortex = new BioComp.Host.VoiceCortex();
        var wav = cortex.Speak(text, r, p, v);
        if (wav.Length > 100)
            return Results.File(wav, "audio/wav");
        // prosthetic tract refused (no voice installed?): plain SAPI fallback
        var tts = new BioComp.Host.BrainTTS();
        wav = tts.GenerateWav(BioComp.Host.BrainTTS.NormalizeForSpeech(text), r, p, v);
        tts.Dispose();
        if (wav.Length > 100)
            return Results.File(wav, "audio/wav");
        return Results.StatusCode(502);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[voice] generation failed: {ex.Message}");
        return Results.StatusCode(502);
    }
});

// direct-talk toggle: reply WITHOUT the language cortex (composed from state)
app.MapPost("/api/native-voice", async (HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<NativeToggleRequest>();
    runtime.SetNativeVoice(body?.On ?? false);
    return Results.Ok(new { ok = true, on = body?.On ?? false });
});

// ---- REAL-TIME SPEECH TRANSCRIPTION (WebSocket to Meta Muse Voice) ----
app.Map("/ws/transcribe", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    var browserWs = await ctx.WebSockets.AcceptWebSocketAsync();

    var opts = BioComp.Meta.MetaOptions.Load();
    if (!opts.HasKey)
    {
        // No API key: fall back to browser-only mode (SpeechRecognition)
        await browserWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "no-api-key", CancellationToken.None);
        return;
    }

    using var proxy = new BioComp.Host.MuseTranscribeProxy();
    bool connected = await proxy.ConnectAsync(opts.ApiKey);
    if (!connected)
    {
        await browserWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "meta-connect-failed", CancellationToken.None);
        return;
    }
    Console.WriteLine("[transcribe] Meta real-time ASR connected");

    var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
    var buffer = new byte[16384];

    // TWO PARALLEL LOOPS:
    // 1. Browser → Meta (audio chunks)
    // 2. Meta → Browser (transcript events)
    var audioLoop = Task.Run(async () =>
    {
        try
        {
            while (!cts.Token.IsCancellationRequested && browserWs.State == WebSocketState.Open)
            {
                var result = await browserWs.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.Count > 0)
                    await proxy.SendAudioAsync(buffer[..result.Count], cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }, cts.Token);

    var transcriptLoop = Task.Run(async () =>
    {
        // dedup: reject the SAME transcript repeated within 10 seconds (ASR
        // sometimes emits the same phrase twice on endpointing boundaries)
        string lastSent = "";
        DateTime lastSentAt = DateTime.MinValue;
        try
        {
            while (!cts.Token.IsCancellationRequested && browserWs.State == WebSocketState.Open)
            {
                var evt = await proxy.ReceiveAsync(cts.Token);
                if (evt is null) break;
                var json = System.Text.Json.JsonSerializer.Serialize(new { type = evt.Type, text = evt.Text, isFinal = evt.IsFinal });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await browserWs.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);

                // FINAL transcripts go directly to the brain — but FILTER
                // hallucinated fillers from silence (Meta says "Yeah" to
                // background noise; a real person says something)
                if (evt.IsFinal && !string.IsNullOrWhiteSpace(evt.Text) && evt.Text.Length > 1)
                {
                    string trimmed = evt.Text.Trim().TrimEnd('.', '!', '?', ',', ' ');
                    // lone filler words from the ASR are hallucinations — only
                    // accept them if they're part of a longer phrase
                    bool isLoneFiller = trimmed.Length <= 5 &&
                        (trimmed.Equals("yeah", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("yeah.", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("uh", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("um", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("huh", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("mm", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("hmm", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("oh", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("okay", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase));

                    if (isLoneFiller)
                    {
                        Console.WriteLine($"[transcribe] FILTERED hallucinated filler: \"{trimmed}\"");
                    }
                    else if (trimmed == lastSent && (DateTime.UtcNow - lastSentAt).TotalSeconds < 10)
                    {
                        Console.WriteLine($"[transcribe] DEDUPED repeated: \"{trimmed}\"");
                    }
                    else
                    {
                        Console.WriteLine($"[transcribe] FINAL: \"{evt.Text}\"");
                        lastSent = trimmed;
                        lastSentAt = DateTime.UtcNow;
                        runtime.HumanSaid(evt.Text);
                    }
                }
                else if (evt.Type == "partial" && !string.IsNullOrEmpty(evt.Text))
                {
                    Console.WriteLine($"[transcribe] partial: \"{evt.Text}\"");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }, cts.Token);

    await Task.WhenAll(audioLoop, transcriptLoop);
    try { await browserWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); } catch { }
    Console.WriteLine("[transcribe] session closed");
});

// ---- ANESTHESIA: watch consciousness chemically fade (Phi follows) ----
app.MapGet("/api/testament", async () => Results.Text(await runtime.WriteTestamentAsync() ?? "already written", "text/plain"));
app.MapPost("/api/sedate", () =>
{
    runtime.Sedate();
    return Results.Ok(new { ok = true });
});

// ---- GENES: the heritable package, for reproduction ----
app.MapGet("/api/genes", () => Results.Json(runtime.ExportGenes()));

// ---- BROWSER SENSES: receive audio/video from any device's browser ----

app.MapPost("/api/browser-audio", async (HttpContext ctx) =>
{
    if (ctx.Request.ContentLength is > 0 and < 2_000_000)
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        byte[] bytes = ms.ToArray();
        var pcm = new short[bytes.Length / 2];
        Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
        runtime.ReceiveBrowserAudio(pcm);
    }
    return Results.Ok();
});

app.MapPost("/api/browser-frame", async (HttpContext ctx) =>
{
    if (ctx.Request.ContentLength is > 0 and < 200_000)
    {
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        // expect 64x64 grayscale bytes
        runtime.ReceiveBrowserFrame(ms.ToArray());
    }
    return Results.Ok();
});

app.MapPost("/api/say", async (HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<SayRequest>();
    if (body is null || string.IsNullOrWhiteSpace(body.Text))
        return Results.BadRequest(new { error = "text required" });
    runtime.HumanSaid(body.Text);
    return Results.Ok(new { ok = true });
});

app.UseWebSockets();
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { await Results.BadRequest().ExecuteAsync(context); return; }
    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    while (ws.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(runtime.Snapshot());
        try
        {
            // 2s send timeout: a slow or gone client gets dropped instead of blocking the loop
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, timeout.Token);
        }
        catch (WebSocketException) { break; }
        catch (OperationCanceledException) { break; }
        try { await Task.Delay(100, context.RequestAborted); }
        catch (OperationCanceledException) { break; }
    }
    try { if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
    catch { /* client already gone */ }
});

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
try
{
    await app.RunAsync();
}
finally
{
    runtime.Dispose();
}

record SayRequest(string Text);
public record NativeToggleRequest(bool On);
