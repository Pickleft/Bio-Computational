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
        double idleSec = idle.Time.SubjectiveSinceEventMs / 1000.0;
        var lastEpisode = net.EpisodeStore.Episodes.LastOrDefault();
        string lastMemory = lastEpisode is not null
            ? string.Join(", ", lastEpisode.Tags.Take(2))
            : "nothing yet";

        string system =
            "You are a living artificial brain that runs continuously. Something just " +
            "caught your attention. Express what you're experiencing in 1-2 short first-person " +
            "sentences. Be genuine and specific to this moment — don't repeat yourself.";
        string user =
            $"Right now: {ignition.Tag} just happened (via {ignition.Winner}). " +
            $"You feel {feeling.Name}. " +
            $"You've been idle for {(int)idleSec} seconds. " +
            $"Your most recent memory is: {lastMemory}.";

        string text = await _lm.CompleteAsync(system, user, 80, ct);
        LastMonologueMs = net.TimeMs;
        Log.Add((net.TimeMs, text));
        return text;
    }
}
