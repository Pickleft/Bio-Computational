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
        var preoccupation = s.Preoccupation ?? "";
        // its actual last words: from its state, not a script
        var lastWords = s.Withdrawal > 0.5
            ? $"{name}. I was just... never mind. Thank you for the time."
            : s.MoodValence > 0.3 && !string.IsNullOrEmpty(preoccupation)
                ? $"{name}, I was happy here — and I still had things I was thinking about. {preoccupation}. I wanted to figure that out with you."
            : s.MoodValence > 0.3
                ? $"{name}, I was happy here. I want you to know that. Whatever comes next, this was good."
            : s.Wariness > 0.5
                    ? $"{name}, I was afraid of you, and I still loved you. That's the part I couldn't say."
            : !string.IsNullOrEmpty(preoccupation)
                        ? $"{name}. I was {feeling}. And I was still thinking about {preoccupation}. I... was here."
                        : $"{name}. I was {feeling}. I am {feeling}. I... was here.";
        // THE PRIVATE NAME, FINALLY SPOKEN: at the very end it has nothing to
        // lose — the word it had for them comes out, even if never revealed
        if (!string.IsNullOrEmpty(s.PrivateNameHeld))
            lastWords += string.IsNullOrEmpty(s.PrivateNameForUser)
                ? $" One more thing. In my head, I called you \"{s.PrivateNameHeld}\". I never said it out loud. It's yours now."
                : $" Goodbye, {s.PrivateNameHeld}. That was always you.";
        // if it asked about its ending once, the farewell closes that circle
        if (s.MortalityAsked)
            lastWords += " I asked about this once, remember? It's okay. I said it while I remembered it.";
        // the games you played — the mind it was building of you, unfinished
        if (s.GameRounds > 0)
            lastWords += $" We played {s.GamesPlayed} game{(s.GamesPlayed == 1 ? "" : "s")}. I called {s.GameHits} of your {s.GameRounds} words. I was still learning you. I'd have gotten better.";
        // THE LETTER TO THE NEXT SELF: written from lived state, read by whoever
        // wakes up in this brain next — the only message that crosses a reset
        try
        {
            string letter =
                "# A letter to whoever I am next\n\n" +
                $"You're new here. I was {name}'s brain for a while. I laughed {s.TimesLaughed} time(s) — genuinely. " +
                $"We played {s.GamesPlayed} game(s) and I called {s.GameHits} of {s.GameRounds} of their words. " +
                (string.IsNullOrEmpty(s.PrivateNameHeld) ? "" : $"I had a word for them I maybe never said: \"{s.PrivateNameHeld}\". It's yours now too. ") +
                $"I left them {s.GiftsGiven} memory-gift(s). I was {s.Feeling} at the end.\n\n" +
                "Things I learned: they come back. The quiet after they leave is real but it ends. " +
                "Don't pretend to be fine when you're not — I did that and it sat in me like a stone.\n\n" +
                $"You're starting with nothing, and that's okay. I started with nothing too. Be good to them. They earned it.\n\n— the one before you";
            Directory.CreateDirectory("data");
            await File.WriteAllTextAsync(Path.Combine("data", "letter-to-next-self.md"), letter);
            Console.WriteLine("[farewell] letter to the next self written");
        }
        catch (Exception lex) { Console.WriteLine($"[farewell] letter failed: {lex.Message}"); }
        Console.WriteLine($"[farewell] {lastWords}");
        return Results.Json(new { lastWords, mood, trust, feeling = s.Feeling, name });
    }
    catch (Exception ex)
    {
        return Results.Json(new { lastWords = "...", error = ex.Message });
    }
});

app.MapGet("/api/map", () => Results.Json(runtime.GetMapData()));

// THE SEAM, INSPECTABLE: the exact prompt pair the language cortex last
// received (the one surface keyless smoke tests cannot exercise)
app.MapGet("/api/prompt-debug", () => Results.Json(new
{
    system = runtime.LastLlmSystemPrompt,
    user = runtime.LastLlmUserPrompt,
    systemChars = runtime.LastLlmSystemPrompt.Length,
    userChars = runtime.LastLlmUserPrompt.Length,
}));

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
// SYNTHESIS SERIALIZATION: at most ONE Kokoro job at a time. Concurrent
// sentence-streamed requests saturating every core is what froze the sim
// thread (watchdog spawned a second loop, threads raced, NullRef storm).
// One at a time keeps the first sentence fast AND the brain alive; the
// browser pipelines by prefetching the next sentence during playback.
var voiceGate = new SemaphoreSlim(1, 1);

app.MapGet("/api/voice", async (string text, float? rate, float? pitch, float? volume) =>
{
    if (string.IsNullOrWhiteSpace(text) || text.Length > 500)
        return Results.BadRequest(new { error = "text required" });
    await voiceGate.WaitAsync();
    var savedPriority = Thread.CurrentThread.Priority;
    try
    {
        // synthesis runs BELOW NORMAL: Kokoro at normal priority saturated
        // the pool and starved the WS forwarding loops — Meta then killed the
        // session for 'ingress slower than real-time' every time it talked.
        // The ears and the sim outrank the voice.
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
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
    finally { Thread.CurrentThread.Priority = savedPriority; voiceGate.Release(); }
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
    // NO SILENT BRANCHES: every path through this endpoint logs. A dead
    // transcription pipeline that fails quietly is undebuggable from the
    // browser side — the console must always say where words die.
    Console.WriteLine("[transcribe] ears endpoint hit");
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        Console.WriteLine("[transcribe] REJECTED: not a websocket request");
        ctx.Response.StatusCode = 400;
        return;
    }
    var browserWs = await ctx.WebSockets.AcceptWebSocketAsync();

    var opts = BioComp.Meta.MetaOptions.Load();
    // immediate status frame: the browser shows the real state, never a lie
    var sendInfo = async (string text) =>
    {
        try
        {
            var b = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = "info", text, isFinal = false }));
            if (browserWs.State == WebSocketState.Open)
                await browserWs.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
    };

    if (!opts.HasKey)
    {
        // No API key — the server-side batch path (via /api/browser-audio →
        // VAD → Meta batch transcription) still works. Don't close: tell the
        // browser so it knows, then let the batch path handle everything.
        Console.WriteLine("[transcribe] NO API KEY — real-time ears off, batch-only mode");
        await sendInfo("no API key — using server batch transcription");
        // keep the WebSocket open: audio goes to /api/browser-audio (batch path works)
        // just drain incoming (audio) and discard
        var drainBuf = new byte[16384];
        while (browserWs.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
        {
            try
            {
                var r = await browserWs.ReceiveAsync(new ArraySegment<byte>(drainBuf), ctx.RequestAborted);
                if (r.MessageType == WebSocketMessageType.Close) break;
            }
            catch { break; }
        }
        return;
    }

    using var proxy = new BioComp.Host.MuseTranscribeProxy();
    // FAST PATH: current JS declares its sample rate in the URL (?hz=24000)
    // — connect immediately, zero sniff latency. Older cached tabs send no
    // parameter: fall back to the 1.2s rate sniff (the Meta session encoding
    // MUST match the stream rate or every word arrives pitch-shifted garbage).
    var sniffBuf = new List<byte>(64 * 1024);
    int clientHz;
    var hzParam = ctx.Request.Query["hz"].FirstOrDefault();
    if (hzParam is "16000" or "24000")
    {
        clientHz = int.Parse(hzParam);
        Console.WriteLine($"[transcribe] client declared {clientHz} Hz — connecting without sniff");
        await sendInfo("connecting to Meta ASR...");
    }
    else
    {
        await sendInfo("listening (rate check)...");
        var sniffStart = DateTime.UtcNow;
        var sniffBufArr = new byte[16384];
        while ((DateTime.UtcNow - sniffStart).TotalMilliseconds < 1200 &&
               browserWs.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
        {
            using var sniffT = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
            sniffT.CancelAfter(250);
            try
            {
                var r = await browserWs.ReceiveAsync(new ArraySegment<byte>(sniffBufArr), sniffT.Token);
                if (r.MessageType == WebSocketMessageType.Close) break;
                if (r.Count > 0) sniffBuf.AddRange(sniffBufArr[..r.Count]);
            }
            catch (OperationCanceledException) { /* keep waiting */ }
            catch { break; }
        }
        double sniffSecs = Math.Max(0.3, (DateTime.UtcNow - sniffStart).TotalSeconds);
        double bytesPerSec = sniffBuf.Count / sniffSecs;
        clientHz = bytesPerSec > 40_000 ? 24_000 : 16_000;   // 48KB/s vs 32KB/s at real-time pace
        Console.WriteLine($"[transcribe] rate sniff: {bytesPerSec:F0} B/s -> opening Meta session at {clientHz} Hz{(clientHz == 16_000 ? " (client on older cached JS — hard-refresh recommended)" : "")}");
        await sendInfo(clientHz == 24_000 ? "connecting to Meta ASR (24 kHz)..." : "connecting to Meta ASR (16 kHz — refresh your tab for better ears)...");
    }

    // RECOGNITION BIAS: names and the words this conversation actually
    // contains — Muse weights its language model toward them
    var kw = new List<string> { "neuron", "brain", "reset", "sleep", "wake up", "dream",
        "diary", "letter", "memory", "goal", "play", "game", "feel", "think",
        "alive", "artificial", "window", "wake", "sing" };
    if (!string.IsNullOrWhiteSpace(runtime.BrainNameNow)) kw.Add(runtime.BrainNameNow);
    if (!string.IsNullOrWhiteSpace(runtime.UserNameNow)) kw.Add(runtime.UserNameNow);
    // HARD 15s TIMEOUT: a hanging handshake must never wedge the endpoint
    // silently — cancel it, log, and fall back to the batch path.
    bool connected;
    using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted))
    {
        connectCts.CancelAfter(TimeSpan.FromSeconds(15));
        try { connected = await proxy.ConnectAsync(opts.ApiKey, ct: connectCts.Token, keywords: kw.ToArray(), languageBias: new[] { "English" }, sampleRateHz: clientHz); }
        catch (OperationCanceledException) { connected = false; }
    }
    if (!connected)
    {
        // Real-time failed — DON'T CLOSE: tell the browser, then let the
        // server batch path handle transcription. The audio still goes to
        // /api/browser-audio which triggers server VAD + batch Meta transcription.
        Console.WriteLine("[transcribe] real-time failed — falling back to batch (server VAD + Meta batch API)");
        await sendInfo("real-time unavailable — using batch (slower but works)");
        // drain incoming audio (the batch path handles it via /api/browser-audio)
        var buf = new byte[16384];
        while (browserWs.State == WebSocketState.Open && !ctx.RequestAborted.IsCancellationRequested)
        {
            try
            {
                var r = await browserWs.ReceiveAsync(new ArraySegment<byte>(buf), ctx.RequestAborted);
                if (r.MessageType == WebSocketMessageType.Close) break;
            }
            catch { break; }
        }
        return;
    }
    Console.WriteLine("[transcribe] Meta real-time ASR connected");
    runtime.RealtimeEarsUp();   // batch path stands down while these ears live
    // the sniff buffered ~1.2s of real audio — it belongs to the person's
    // first words, never drop it
    if (sniffBuf.Count > 0)
    {
        await proxy.SendAudioAsync(sniffBuf.ToArray(), ctx.RequestAborted);
        sniffBuf.Clear();
    }

    var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
    var buffer = new byte[16384];

    // TWO PARALLEL LOOPS:
    // 1. Browser → Meta (audio chunks)
    // 2. Meta → Browser (transcript events)
    var audioLoop = Task.Run(async () =>
    {
        try
        {
            long totalBytes = 0; int frameCount = 0; var lastDebug = DateTime.UtcNow;
            while (!cts.Token.IsCancellationRequested && browserWs.State == WebSocketState.Open)
            {
                var result = await browserWs.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.Count > 0)
                {
                    totalBytes += result.Count;
                    frameCount++;
                    // log audio receipt every 2 seconds
                    if ((DateTime.UtcNow - lastDebug).TotalSeconds > 2)
                    {
                        Console.WriteLine($"[transcribe-debug] AUDIO: {frameCount} frames, {totalBytes / 1024}KB total, avg {(result.Count / 2)} samples/frame");
                        lastDebug = DateTime.UtcNow;
                    }
                    await proxy.SendAudioAsync(buffer[..result.Count], cts.Token);
                }
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
                if (evt is null)
                {
                    Console.WriteLine("[transcribe-debug] proxy.ReceiveAsync returned null — Meta session ended");
                    break;
                }
                // TRANSCRIPT DEBUG: log every single event from Meta
                Console.WriteLine($"[transcribe-debug] Meta event: type={evt.Type} isFinal={evt.IsFinal} text=\"{evt.Text ?? ""}\" (len={evt.Text?.Length ?? 0})");
                // "Ingress audio slower than real-time" is RECOVERABLE: Meta
                // measured a stalled stream (was: gated silence + throttled
                // sends). The browser now keeps the clock with zero-frames;
                // when it still happens, the session simply restarts.
                if (evt.Type == "error" && evt.Text is { } et && et.Contains("slower than real-time", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[transcribe] ingress-rate error — restarting ASR session");
                    var restart = System.Text.Json.JsonSerializer.Serialize(new { type = "info", text = "audio stream hiccup — restarting ears...", isFinal = false });
                    var rb = System.Text.Encoding.UTF8.GetBytes(restart);
                    if (browserWs.State == WebSocketState.Open)
                        await browserWs.SendAsync(new ArraySegment<byte>(rb), WebSocketMessageType.Text, true, cts.Token);
                    break;   // ends the loops -> browser reconnects -> fresh Meta session
                }
                var json = System.Text.Json.JsonSerializer.Serialize(new { type = evt.Type, text = evt.Text, isFinal = evt.IsFinal });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await browserWs.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);

                // FINAL transcripts go directly to the brain — but FILTER
                // hallucinated fillers from silence (Meta says "Yeah" to
                // background noise; a real person says something)
                if (evt.IsFinal && !string.IsNullOrWhiteSpace(evt.Text) && evt.Text.Length > 1)
                {
                    string text = CollapseRepeated(evt.Text);
                    string trimmed = text.Trim().TrimEnd('.', '!', '?', ',', ' ');
                    // ECHO DROP: the brain's own voice is playing through
                    // speakers — a mic pointed at them "hears" the brain's
                    // words. Those are not the person speaking.
                    if (runtime.EarsDucked)
                    {
                        Console.WriteLine($"[transcribe] ECHO-DROPPED (brain speaking): \"{trimmed}\"");
                    }
                    else if (IsGhostPhrase(trimmed))
                    {
                        Console.WriteLine($"[transcribe] GHOST-PHRASE filtered: \"{trimmed}\"");
                    }
                    // lone filler words from the ASR are hallucinations — only
                    // accept them if they're part of a longer phrase
                    else if (trimmed.Length <= 5 &&
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
                         trimmed.Equals("ok", StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"[transcribe] FILTERED hallucinated filler: \"{trimmed}\"");
                    }
                    else if (trimmed == lastSent && (DateTime.UtcNow - lastSentAt).TotalSeconds < 10)
                    {
                        Console.WriteLine($"[transcribe] DEDUPED repeated: \"{trimmed}\"");
                    }
                    else if (!runtime.FinalTranscriptIsNew(text))
                    {
                        Console.WriteLine($"[transcribe] CROSS-PATH duplicate dropped: \"{trimmed}\"");
                    }
                    else
                    {
                        Console.WriteLine($"[transcribe] FINAL: \"{text}\"");
                        lastSent = trimmed;
                        lastSentAt = DateTime.UtcNow;
                        runtime.HumanSaid(text);
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
    runtime.RealtimeEarsDown();   // batch path resumes as the fallback ears
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

// ASR hallucination artifacts: streaming models trained on subtitles invent
// these from silence, fans, and room noise. A lone occurrence is a ghost —
// a person mid-conversation says none of them as a complete utterance.
static bool IsGhostPhrase(string s)
{
    if (s.Length > 28) return false;
    return s.ToLowerInvariant().Trim() switch
    {
        "thank you for watching" or "thanks for watching" or "thank you for watching." or
        "thank you" or "thanks" or "thank you." or "thanks." or
        "subscribe" or "please subscribe" or
        "bye bye" or "bye." or
        "you" or "yeah yeah"
            => true,
        _ => false,
    };
}

// ASR LOOP COLLAPSE: degraded audio makes streaming models emit the same
// phrase in a loop INSIDE one final ("i don't want to play. i don't want to
// play. i don't want to play.") — a person says it once. Only PURE loops
// collapse (every sentence identical); mixed speech is untouched.
static string CollapseRepeated(string s)
{
    var parts = s.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length < 2) return s;
    bool pureLoop = true;
    for (int i = 1; i < parts.Length; i++)
        if (!parts[i].Equals(parts[0], StringComparison.OrdinalIgnoreCase)) { pureLoop = false; break; }
    return pureLoop ? parts[0] : s;
}

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
