using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// Play, in the ethological sense (Burghardt 2005's five criteria, simplified
/// to what is measurable here): spontaneous, self-directed, varied activity
/// that appears when the organism is safe, well-fed, secure and curious - and
/// disappears under threat or social distress. Developmentally, secure
/// attachment enables exploratory play; anxiety suppresses it. Play GROWS
/// expressiveness (it is practice for the expressive apparatus), so a brain
/// raised safe becomes visibly livelier than one raised cold. Sim-thread only.
/// </summary>
public sealed class PlaySystem
{
    /// <summary>Minimum interval between play bursts (ms).</summary>
    public float CooldownMs { get; set; } = 12_000f;

    public int PlayBursts { get; private set; }
    public bool Playing { get; private set; }
    public float Vigor { get; private set; }

    private float _lastPlayMs = -1e9f;

    /// <summary>
    /// Whether the conditions for play hold right now: awake, safe (low
    /// noradrenaline), rewarded (dopamine above baseline), curious, and either
    /// securely attached or too young to have learned distance.
    /// </summary>
    public static bool ConditionsHold(SpikingNetwork net, IdleController idle,
        AttachmentStyle style, float curiosityTrait) =>
        idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake
        && net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Noradrenaline) < 0.55f
        && net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine) > 0.45f
        && curiosityTrait > 0.05f
        && style is AttachmentStyle.Secure or AttachmentStyle.Forming;

    /// <summary>Try to start a play burst. Returns vigor 0..1, or 0 when not playing.</summary>
    public float TryPlay(SpikingNetwork net, IdleController idle, AttachmentStyle style,
        float curiosityTrait, float expressivenessTrait, Func<double> rng)
    {
        if (net.TimeMs - _lastPlayMs < CooldownMs) { Playing = false; return 0f; }
        if (!ConditionsHold(net, idle, style, curiosityTrait)) { Playing = false; return 0f; }
        if (rng() > 0.35) return 0f;
        _lastPlayMs = net.TimeMs;
        Playing = true;
        PlayBursts++;
        Vigor = Math.Clamp(0.4f + 0.6f * MathF.Max(0f, expressivenessTrait)
            + 0.3f * (net.Modulators.Get(BioComp.Core.Neuromodulators.NeuromodulatorKind.Dopamine) - 0.45f), 0f, 1f);
        return Vigor;
    }
}
