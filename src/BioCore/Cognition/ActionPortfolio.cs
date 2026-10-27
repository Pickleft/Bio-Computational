using BioComp.Core.Network;
using BioComp.Core.Sleep;

namespace BioComp.Core.Cognition;

public enum BrainAction : byte { StaySilent = 0, Attend, Speak, DisplayText, Explore, SeekStimulation }

/// <summary>
/// Output-side behavior: the brain chooses among acting, attending, speaking (TTS is
/// host-side), displaying, exploring and seeking stimulation. Choice is a drive-weighted
/// softmax whose temperature rises with serotonin (patience); speaking and displaying
/// require an ignition (only reportable content leaves the brain), have long refractory
/// periods, respect quiet hours, and their action values learn from social feedback via
/// dopamine. No scripted triggers: drives, ignition and learned values decide.
/// </summary>
public sealed class ActionPortfolio
{
    public const int Count = 6;

    public readonly float[] Q = new float[Count];
    public readonly long[] LastFiredMs = new long[Count];
    public readonly List<(float TimeMs, BrainAction Action, string Reason)> Log = [];

    /// <summary>Quiet hours block speaking and display output unless overridden.</summary>
    public bool QuietHoursActive { get; set; }

    private static readonly int[] RefractoryMs =
    [
        0, 10_000, 120_000, 180_000, 60_000, 90_000,
    ];

    public BrainAction? Choose(SpikingNetwork net, SleepController sleep, GlobalWorkspace workspace,
        FeelingState feeling, float curiosity, float socialDrive)
    {
        if (sleep.State is BrainState.NREM or BrainState.REM) return null; // asleep: no output

        // actions are available from t=0: refractory clocks start in the past
        for (int i = 0; i < Count; i++)
            if (LastFiredMs[i] == 0) LastFiredMs[i] = -RefractoryMs[i];

        bool canSpeak = workspace.LastIgnition.TimeMs > net.TimeMs - 5_000; // recent ignition
        float patience = Math.Clamp(net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Serotonin), 0.2f, 1f);

        float Score(BrainAction a)
        {
            int i = (int)a;
            if (net.TimeMs - LastFiredMs[i] < RefractoryMs[i]) return float.NegativeInfinity;
            float baseScore = 0.2f + Q[i];
            return a switch
            {
                BrainAction.StaySilent => baseScore + (feeling.Arousal < 0.3f ? 0.5f : 0f),
                BrainAction.Attend => baseScore + curiosity * 0.8f + 0.3f,
                BrainAction.Speak => canSpeak && !QuietHoursActive ? baseScore + socialDrive * 0.9f + feeling.Valence * 0.2f : float.NegativeInfinity,
                BrainAction.DisplayText => canSpeak && !QuietHoursActive ? baseScore + socialDrive * 0.5f : float.NegativeInfinity,
                BrainAction.Explore => baseScore + curiosity * 1.1f - 0.2f,
                BrainAction.SeekStimulation => baseScore + (feeling.Name == "bored" ? 1.2f : 0f),
                _ => baseScore,
            };
        }

        // drive-weighted softmax over valid actions
        double temperature = 0.15 + 0.35 * patience;
        var valid = new List<(BrainAction A, double W)>();
        double sum = 0;
        for (int i = 0; i < Count; i++)
        {
            float s = Score((BrainAction)i);
            if (float.IsNegativeInfinity(s)) continue;
            double w = Math.Exp(s / temperature);
            valid.Add(((BrainAction)i, w));
            sum += w;
        }
        if (valid.Count == 0) return null;

        double r = net.Rng.NextDouble() * sum;
        foreach (var (a, w) in valid)
        {
            r -= w;
            if (r <= 0)
            {
                LastFiredMs[(int)a] = (long)net.TimeMs;
                string reason = a switch
                {
                    BrainAction.Speak => "ignited content + social drive",
                    BrainAction.SeekStimulation => "boredom",
                    BrainAction.Attend => "curiosity",
                    BrainAction.Explore => "curiosity + exploration",
                    _ => "default",
                };
                Log.Add((net.TimeMs, a, reason));
                return a;
            }
        }
        return valid[^1].A;
    }

    /// <summary>Social feedback outcome: dopamine RPE updates the action's value
    /// (was speaking welcomed?). Oxytocin on positive, dopamine pause on negative.</summary>
    public void OnOutcome(SpikingNetwork net, BrainAction action, bool welcomed)
    {
        Q[(int)action] = Math.Clamp(Q[(int)action] + 0.2f * (welcomed ? 1f : -1f), -1.5f, 1.5f);
        net.ApplySocialFeedback(welcomed);
    }
}
