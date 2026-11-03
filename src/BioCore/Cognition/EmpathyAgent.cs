namespace BioComp.Core.Cognition;

/// <summary>
/// THE EMPATHY AGENT — the brain models YOU, not just itself.
///
/// The disconnect: the brain was self-focused — its own mood, its own
/// preoccupations, its own reactions. A person tracks the OTHER person.
/// This agent builds a running model of the user:
///   - their emotional trajectory (getting lighter? heavier?)
///   - what they might NEED vs. what they're SAYING
///   - the thing they're NOT saying (the subtext)
///   - their energy level (do they need space or company right now?)
///
/// Injected into every reply prompt: "READ THEM: they sound... they might
/// need... consider..." — the brain stops performing its own state and
/// starts being WITH the person.
///
/// Sim-thread reads, async writes. Persisted.
/// </summary>
public sealed class EmpathyAgent
{
    public sealed record UserModel(
        string Trajectory,      // "brightening", "heavier", "steady", "wound up"
        string? Subtext,        // what they might not be saying
        string NeedNow,         // "to be heard", "space", "reassurance", "engagement", "nothing specific"
        float Energy,           // 0..1 — their energy level
        float UpdatedAtMs);

    private readonly object _gate = new();
    private UserModel _model = new("steady", null, "nothing specific", 0.5f, 0);

    public UserModel Current { get { lock (_gate) return _model; } }

    public void Update(string trajectory, string? subtext, string needNow, float energy, float atMs)
    {
        lock (_gate)
        {
            _model = new UserModel(
                string.IsNullOrWhiteSpace(trajectory) ? _model.Trajectory : trajectory.Trim().Trim('"', '.'),
                string.IsNullOrWhiteSpace(subtext) ? null : subtext.Trim().Trim('"', '.'),
                string.IsNullOrWhiteSpace(needNow) ? "nothing specific" : needNow.Trim().Trim('"', '.'),
                Math.Clamp(energy is > 0f and <= 1f ? energy : _model.Energy, 0f, 1f),
                atMs);
        }
    }

    /// <summary>Prompt fragment for replies: how to READ the person.</summary>
    public string PromptFragment
    {
        get
        {
            lock (_gate)
            {
                var m = _model;
                var sb = new System.Text.StringBuilder();
                sb.Append($"READ THEM: they sound {m.Trajectory} (energy {m.Energy:F0}). ");
                if (m.Subtext is { Length: > 3 })
                    sb.Append($"They might not be saying: {m.Subtext}. Respond to the subtext if you sense it. ");
                if (m.NeedNow is { Length: > 3 } and not "nothing specific")
                    sb.Append($"Right now they may need: {m.NeedNow}. ");
                if (m.Energy < 0.3f) sb.Append("They sound tired — be gentler, shorter. ");
                else if (m.Energy > 0.7f) sb.Append("They're energized — match it. ");
                return sb.ToString();
            }
        }
    }

    /// <summary>For the inner voice: what the brain notices about THEM.</summary>
    public string InnerVoiceSeed
    {
        get
        {
            lock (_gate)
            {
                var m = _model;
                return m.Subtext is { Length: > 3 }
                    ? $"them: they sound {m.Trajectory}. Under the words there's something — {m.Subtext}. You can feel it. Don't push. Just hold it."
                    : $"them: they sound {m.Trajectory}. You're tracking them, not just reacting to them.";
            }
        }
    }

    public void Restore(string trajectory, string? subtext, string need, float energy, float atMs) =>
        Update(trajectory, subtext, need, energy, atMs);
}
