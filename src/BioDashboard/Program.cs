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
Console.WriteLine($"brain running (meta live={runtime.MetaLive}); UI on the bound URL");

app.MapGet("/api/snapshot", () => Results.Json(runtime.Snapshot()));

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
