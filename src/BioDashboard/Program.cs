using System.Net.WebSockets;
using System.Text.Json;
using BioComp.Host;

// The dashboard process: runs the brain daemon in-proc and serves the live UI.
//   biodashboard [--camera] [--mic] [--live-meta] [--demo] [--voice] [--seed=N] [--urls=http://localhost:8080]
// Camera and microphone auto-detect by default; --mock-cam/--mock-mic force the mock world.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

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

var runtime = new BrainRuntime(BrainRuntimeConfig.Parse(args));
runtime.Start();
Console.WriteLine($"brain running (meta live={runtime.MetaLive}); UI on the bound URL");

app.MapGet("/api/snapshot", () => Results.Json(runtime.Snapshot()));

app.MapGet("/api/map", () => Results.Json(runtime.GetMapData()));

app.MapPost("/api/poke", async (HttpContext ctx) =>
{
    var body = await ctx.Request.ReadFromJsonAsync<PokeRequest>();
    if (body is null) return Results.BadRequest(new { error = "x,y required" });
    runtime.Poke(body.X, body.Y, body.Strength is > 0 and <= 3 ? body.Strength.Value : 1f);
    return Results.Ok(new { ok = true });
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
            await ws.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true,
                context.RequestAborted);
        }
        catch (WebSocketException) { break; }
        await Task.Delay(100, context.RequestAborted); // 10 Hz
    }
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
record PokeRequest(float X, float Y, float? Strength);
