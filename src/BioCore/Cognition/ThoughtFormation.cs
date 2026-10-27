using System.Text.Json;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// Tracks which brain regions activated in what ORDER for each significant thought.
/// This is the neuroscience visualization nobody has ever shown live: you literally
/// watch a thought FORM — CA3 retrieves a memory, PFC integrates it with context,
/// the workspace ignites, language areas activate, and the thought becomes words.
/// Each thought has a propagation path through the brain that we capture and display.
/// </summary>
public sealed class ThoughtFormation
{
    public readonly record struct RegionActivation(
        float TimeMs, string Region, float Rate, string Phase);

    public readonly record struct ThoughtTrace(
        float StartTimeMs, string Result, List<RegionActivation> Path);

    public readonly List<ThoughtTrace> Traces = [];
    private ThoughtTrace? _current;
    private readonly Dictionary<string, float> _lastRates = [];

    /// <summary>Call every 100ms with current region rates.</summary>
    public void Sample(SpikingNetwork net, float[] regionRates)
    {
        // detect significant activation in any region (>2x baseline)
        for (int r = 0; r < regionRates.Length && r < net.Regions.Count; r++)
        {
            string name = net.Regions[r].Name;
            float rate = regionRates[r];
            if (!_lastRates.TryGetValue(name, out float last)) last = 0;

            if (rate > 0.5f && last < 0.2f) // region just became significantly active
            {
                var path = _current?.Path.ToList() ?? [];
                string phase = name switch
                {
                    "CA3" or "CA1" => "memory retrieval",
                    "Retina" or "A1" => "sensory input",
                    "Thal" => "relay",
                    "V1" => "visual processing",
                    "PFC" => "integration",
                    "DMN" => "self-reference",
                    "LangEnc" => "language formation",
                    "Motor" => "action preparation",
                    _ => "processing",
                };
                path.Add(new RegionActivation(net.TimeMs, name, rate, phase));
                _current = new ThoughtTrace(_current?.StartTimeMs ?? net.TimeMs, "", path);
            }
            _lastRates[name] = rate;
        }

        // complete a trace when activity settles and there's a meaningful path
        if (_current is { Path.Count: > 2 } and { } trace && net.TimeMs - trace.StartTimeMs > 500)
        {
            // determine the result from the last activated region
            string result = trace.Path[^1].Region switch
            {
                "LangEnc" => "verbalized",
                "Motor" => "acted",
                "DMN" => "self-reflected",
                _ => "processed",
            };
            Traces.Add(trace with { Result = result });
            if (Traces.Count > 20) Traces.RemoveAt(0);
            _current = null;
        }
    }

    /// <summary>Latest complete thought trace for the dashboard.</summary>
    public ThoughtTrace? Latest => Traces.Count > 0 ? Traces[^1] : null;
}

/// <summary>
/// Internal dialogue: the brain argues with itself. Two competing streams —
/// the emotional/limbic voice (fast, reactive, feeling-driven) and the
/// cortical/rational voice (slow, deliberative, context-aware). This is what
/// consciousness IS in Global Workspace Theory: competing coalitions. Nobody
/// has ever shown this live in a demo.
/// </summary>
public sealed class InternalDialogue
{
    private readonly Func<Task<string>> _generateAsync;

    public readonly List<(float TimeMs, string Limbic, string Cortical)> Exchanges = [];

    public InternalDialogue(Func<Task<string>> generateAsync)
    {
        _generateAsync = generateAsync;
    }

    /// <summary>
    /// Generate one internal exchange: the limbic system reacts emotionally,
    /// then the cortical system contextualizes. Both are from the SAME brain.
    /// </summary>
    public async Task<(string Limbic, string Cortical)?> GenerateExchangeAsync(
        SpikingNetwork net, IdleController idle, string trigger)
    {
        try
        {
            float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
            float sht = net.Modulators.Get(NeuromodulatorKind.Serotonin);
            float na = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);

            // limbic: raw emotional reaction, no filter
            string limbicPrompt =
                $"You are the LIMBIC (emotional) part of a living brain. React instantly and rawly to: {trigger}. " +
                $"Your neurochemistry: DA={da:F2} (reward), 5-HT={sht:F2} (mood), NA={na:F2} (alertness). " +
                "Express the PURE FEELING before any thinking. 1 short sentence, impulsive.";
            string limbic = await _generateAsync();

            // cortical: measured, contextual, rational
            string corticalPrompt =
                $"You are the CORTICAL (rational) part of the same brain. Your limbic system just said: \"{limbic}\". " +
                $"Context: you feel {idle.Feelings.Last.Name}, you've been idle {(int)(idle.Time.SubjectiveSinceEventMs / 1000)}s. " +
                "Respond with measured perspective. 1 short sentence, thoughtful.";
            string cortical = await _generateAsync();

            Exchanges.Add((net.TimeMs, limbic, cortical));
            if (Exchanges.Count > 20) Exchanges.RemoveAt(0);
            return (limbic, cortical);
        }
        catch { return null; }
    }
}
