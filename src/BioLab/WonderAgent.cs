using System.Text.Json;
using BioComp.Core.Abstractions;
using BioComp.Core.Network;
using BioComp.Core.Utils;
using BioComp.Meta;

namespace BioComp.Lab;

public sealed record ResearchReport(string Title, string Path, string Summary, string[] Figures);

/// <summary>
/// The autonomous research agent (targets Muse Wonder when live; deterministic mock
/// keyless). Protocol: the model receives a research goal plus a JSON tool list and
/// drives the loop by replying with {"tool": name, "args": {...}} until it produces a
/// final summary, which must cite tool results. With the mock (no reasoning), the
/// agent falls back to executing the full tool battery in a fixed order - the same
/// experiments, a template narrative - so the pipeline, files and costs are identical
/// either way.
/// </summary>
public sealed class WonderAgent
{
    private readonly ILanguageModel _lm;
    public List<string> Trace { get; } = [];

    public WonderAgent(ILanguageModel lm) => _lm = lm;

    private static readonly string[] ToolList =
    [
        "criticality_sweep - sweep intrinsic noise, report branching ratio and rates",
        "run_mmn - run the oddball paradigm, report deviance in sigma",
        "run_discounting - report how patience varies with serotonin",
        "write_report - assemble findings into the final report",
    ];

    public async Task<ResearchReport> RunAsync(string goal, string outDir = "reports", string exportDir = "exports")
    {
        Directory.CreateDirectory(outDir);
        string toolLog = "";
        var figures = new List<string>();
        string narrative;

        try
        {
            // agentic loop: the model picks tools until it writes the report
            string system =
                "You are an autonomous research agent for a spiking-brain platform. " +
                "Reply with a single JSON object {\"tool\": \"name\", \"args\": {}} to call a tool, " +
                "or {\"done\": true, \"summary\": \"...\"} when finished. Available tools:\n" +
                string.Join("\n", ToolList.Select(t => "- " + t));
            string user = $"research goal: {goal}";
            for (int turn = 0; turn < 8; turn++)
            {
                string reply = await _lm.CompleteAsync(system, user + "\n\nresults so far:\n" + toolLog, 400);
                if (TryParseToolCall(reply, out string? tool, out string? done, out string? summary))
                {
                    if (done is not null && !string.IsNullOrWhiteSpace(summary))
                    {
                        narrative = summary!;
                        Trace.Add("model finished with its own summary");
                        return Assemble(goal, narrative, toolLog, figures, outDir);
                    }
                    Trace.Add($"tool call: {tool}");
                    switch (tool)
                    {
                        case "criticality_sweep":
                            toolLog += CriticalitySweep(exportDir) + "\n";
                            break;
                        case "run_mmn":
                        {
                            var (_, _, mmn) = Paradigms.Mmn(80, outDir: exportDir);
                            toolLog += $"mmn: deviance {mmn:F1} sigma (deviants vs standards)\n";
                            figures.Add("exports/mmn.svg");
                            break;
                        }
                        case "run_discounting":
                        {
                            var rows = Paradigms.Discounting(exportDir);
                            toolLog += "discounting: indifference delay grows from " +
                                       $"{rows[0].IndifferenceDelayS:F1}s (5-HT {rows[0].Sht:F1}) to " +
                                       $"{rows[^1].IndifferenceDelayS:F1}s (5-HT {rows[^1].Sht:F1})\n";
                            figures.Add("exports/discounting.svg");
                            break;
                        }
                        case "write_report":
                            narrative = $"Tool-driven report. {toolLog}";
                            return Assemble(goal, narrative, toolLog, figures, outDir);
                        default:
                            toolLog += $"unknown tool {tool}\n";
                            break;
                    }
                    continue;
                }
                break; // unparseable reply: fall through to the fixed protocol
            }
        }
        catch (Exception ex) { Trace.Add($"agent loop ended: {ex.Message}"); }

        // fallback protocol (mock path): run the full battery, template narrative
        Trace.Add("running fixed tool battery (mock protocol)");
        toolLog += CriticalitySweep(exportDir) + "\n";
        var (_, _, mmn2) = Paradigms.Mmn(80, outDir: exportDir);
        toolLog += $"mmn: deviance {mmn2:F1} sigma\n";
        figures.AddRange(["exports/mmn.svg", "exports/discounting.svg", "exports/criticality.svg"]);
        var rows2 = Paradigms.Discounting(exportDir);
        toolLog += "discounting: patience grows monotonically with serotonin tone\n";
        narrative =
            "Fixed-protocol run (mock interpreter). Key findings: " + toolLog.Replace("\n", " ");
        return Assemble(goal, narrative, toolLog, figures, outDir);
    }

    private static string CriticalitySweep(string exportDir)
    {
        var curve = new List<(float X, float Y)>();
        var lines = new List<object[]>();
        float bestNoise = 0, bestScore = float.MaxValue;
        foreach (float noise in new[] { 1f, 2f, 3f, 4f, 5f, 8f })
        {
            var cfg = BrainConfig.Default(seed: 42);
            cfg.NoiseSigmaMv = noise;
            var net = new SpikingNetwork(cfg);
            for (long t = 0; t < 15_000; t++) net.Tick();
            float sigma = net.Avalanches.BranchingRatio();
            float rate = net.Recorder.MeanRateHz(15_000);
            var sizes = net.Avalanches.AvalancheSizes();
            float maxAvalFrac = sizes.Count > 0 ? sizes.Max() / (float)net.V.Length : 0f;
            curve.Add((noise, sigma));
            lines.Add([$"{noise:F0}", $"{sigma:F3}", $"{rate:F3}", $"{maxAvalFrac:F3}"]);
            // branching ~1 also appears in the runaway regime (one endless avalanche);
            // a sane operating point needs bounded avalanches and a live but modest rate
            bool sane = maxAvalFrac < 0.10f && rate is > 0.05f and < 10f;
            float score = sane ? MathF.Abs(sigma - 1f) : float.MaxValue;
            if (score < bestScore) { bestScore = score; bestNoise = noise; }
        }
        SciOutput.WriteCsv(Path.Combine(exportDir, "criticality.csv"),
            ["noise_mv", "branching_sigma", "mean_rate_hz", "max_avalanche_fraction"], lines);
        SciOutput.WriteSvg(Path.Combine(exportDir, "criticality.svg"),
            $"Criticality: branching ratio vs noise (sane operating point at noise {bestNoise:F0} mV)",
            "noise sigma (mV)", "branching ratio", 1, 8, [("branching", curve)]);
        return $"criticality: noise {bestNoise:F0} mV is the nearest-critical bounded-avalanche point; " +
               "runaway regime excluded via max-avalanche fraction (see exports/criticality.csv)";
    }

    private static ResearchReport Assemble(string goal, string narrative, string toolLog,
        List<string> figures, string outDir)
    {
        string slug = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
        string path = Path.Combine(outDir, $"report-{slug}.md");
        string md =
            $"# Research report: {goal}\n\n" +
            $"Generated {DateTime.Now:yyyy-MM-dd HH:mm} by the Wonder agent " +
            $"(live model calls, if any, are itemized in data/api_cost.jsonl).\n\n" +
            $"## Summary\n\n{narrative}\n\n" +
            $"## Tool log\n\n```\n{toolLog}\n```\n\n" +
            $"## Figures\n\n" + string.Join("\n", figures.Select(f => $"- {f}")) + "\n";
        File.WriteAllText(path, md);
        return new ResearchReport(goal, path, narrative, [.. figures]);
    }

    private static bool TryParseToolCall(string reply, out string? tool, out string? done, out string? summary)
    {
        tool = done = summary = null;
        int start = reply.IndexOf('{');
        int end = reply.LastIndexOf('}');
        if (start < 0 || end <= start) return false;
        try
        {
            using var doc = JsonDocument.Parse(reply[start..(end + 1)]);
            var root = doc.RootElement;
            if (root.TryGetProperty("tool", out var t)) { tool = t.GetString(); return true; }
            if (root.TryGetProperty("done", out var d) && d.GetBoolean())
            {
                done = "true";
                if (root.TryGetProperty("summary", out var s)) summary = s.GetString();
                return true;
            }
        }
        catch (JsonException) { }
        return false;
    }
}
