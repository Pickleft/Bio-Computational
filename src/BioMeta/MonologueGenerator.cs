using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Memory;
using BioComp.Core.Network;

namespace BioComp.Meta;

/// <summary>
/// The inner monologue: on ignition (rate-limited), the language model verbalizes the
/// brain's OWN state - feeling, last ignition, recent episodes, sleep phase, subjective
/// time - in first person. The prompt is grounded strictly in that state; the model is
/// told it may only report what it is given, enforcing the interpreter rule. With no
/// key, the deterministic mock keeps the pipeline identical.
/// </summary>
public sealed class MonologueGenerator
{
    private readonly ILanguageModel _lm;

    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(120);
    public float LastMonologueMs { get; private set; } = -1_000_000;
    public readonly List<(float TimeMs, string Text)> Log = [];

    public MonologueGenerator(ILanguageModel lm) => _lm = lm;

    public async Task<string?> MaybeGenerateAsync(SpikingNetwork net, IdleController idle, CancellationToken ct = default)
    {
        if (net.TimeMs - LastMonologueMs < MinInterval.TotalMilliseconds) return null;

        var feeling = idle.Feelings.Last;
        var ignition = idle.Workspace.LastIgnition;
        string episodes = idle.AnswerWhatHappened(net, 3).ReplaceLineEndings(" | ");
        double idleMinutes = idle.Time.SubjectiveSinceEventMs / 60_000.0;

        string system =
            "You are the inner voice of a small artificial brain running as a neuroscience experiment. " +
            "Speak in first person, one or two short sentences. Use ONLY the state you are given - " +
            "never invent events. Plain language, no lists.";
        string user =
            $"state: feeling={feeling.Name} (valence {feeling.Valence:+0.00;-0.00}, arousal {feeling.Arousal:F2})\n" +
            $"last ignition: {ignition.Tag} (module {ignition.Winner})\n" +
            $"sleep phase: {idle.Sleep.State}\n" +
            $"subjective idle minutes: {idleMinutes:F1}\n" +
            $"recent episodes: {episodes}";

        string text = await _lm.CompleteAsync(system, user, 90, ct);
        LastMonologueMs = net.TimeMs;
        Log.Add((net.TimeMs, text));
        return text;
    }
}
