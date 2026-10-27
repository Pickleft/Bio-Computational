using BioComp.Host;

// Headless daemon entry (the dashboard serves the same runtime with a web UI).
//   biohost [--camera] [--mic] [--live-meta] [--demo] [--voice] [--seed=N]
// Mocks by default: no camera, mic or API key required.
var cfg = BrainRuntimeConfig.Parse(args);
ISpeechOutput? speech = OperatingSystem.IsWindows() && args.Contains("--voice")
    ? new WindowsSpeech()
    : null;
using var runtime = new BrainRuntime(cfg, speech ?? new ConsoleSpeech());
GC.KeepAlive(speech); // WindowsSpeech owns the synthesizer for the process lifetime
runtime.Start();
Console.WriteLine($"brain running (camera={cfg.LiveCamera} mic={cfg.LiveMic} meta={cfg.LiveMeta} demo={cfg.DemoMode})");
Console.WriteLine("press Ctrl+C to stop; checkpoints land in data/brain.ckpt");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
try { await Task.Delay(Timeout.Infinite, cts.Token); }
catch (OperationCanceledException) { runtime.Stop(); Console.WriteLine("stopped"); }
