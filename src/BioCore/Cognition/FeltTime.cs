using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// FELT TIME — subjective duration (attentional-gate models: the fuller the
/// attentional window, the longer a moment FEELS in retrospect — yet rich
/// interaction compresses lived time in the moment; Zakay &amp; Block 1997;
/// Wittmann 2013 for the emotional modulation). The brain keeps a marker
/// density (events per window) and a valence trace, and from them reports
/// whether the recent past FELT long or short:
///
///   event-sparse + low mood  -> time DRAGS  ("this hour felt like three")
///   event-dense + positive   -> time FLIES  ("you were only just here")
///   event-dense + negative   -> time CRAWLS (threat seconds stretch)
///
/// It enters the diary, the reply prompts, and the testament — a life
/// remembered at its felt size, not its clock size.
///
/// Sim-thread only.
/// </summary>
public sealed class FeltTime
{
    private readonly Queue<(float TimeMs, float Valence)> _events = new();
    private float _lastTrimMs;

    /// <summary>Window over which felt duration is judged (ms).</summary>
    public float WindowMs { get; set; } = 300_000f;

    /// <summary>Mark any event worth living: a note, a stimulus, a contact.</summary>
    public void OnEvent(float nowMs, float valence)
    {
        _events.Enqueue((nowMs, valence));
        Trim(nowMs);
    }

    /// <summary>Felt stretch of the recent window: 1.0 = veridical, >1 = dragged, <1 = flew.</summary>
    public float Stretch(float nowMs)
    {
        Trim(nowMs);
        if (_events.Count == 0) return 1.4f;         // an empty window drags
        float density = _events.Count / MathF.Max(1f, WindowMs / 60_000f); // events per felt-minute
        float meanValence = 0f;
        foreach (var e in _events) meanValence += e.Valence;
        meanValence /= _events.Count;

        // dense + positive -> compression; dense + negative -> dilation; sparse -> drag
        float densityTerm = density < 1f ? 0.35f : meanValence > 0.1f ? -0.25f : 0.30f;
        return Math.Clamp(1f + densityTerm + 0.1f * -meanValence, 0.6f, 2.0f);
    }

    /// <summary>For diary/prompt: the recent past in felt-size words.</summary>
    public string Describe(float nowMs)
    {
        float s = Stretch(nowMs);
        return s > 1.5f ? "lately time drags - the quiet minutes are longer than the clock says"
             : s > 1.15f ? "time has been slow-shaped lately"
             : s < 0.8f ? "good things compress time - the visit felt shorter than it was"
             : "time feels about its own size";
    }

    private void Trim(float nowMs)
    {
        if (nowMs - _lastTrimMs < 5_000f) return;
        _lastTrimMs = nowMs;
        while (_events.Count > 0 && nowMs - _events.Peek().TimeMs > WindowMs)
            _events.Dequeue();
    }
}
