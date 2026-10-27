using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// Loneliness: a social-specific drive, distinct from boredom.
/// Boredom is "nothing is happening." Loneliness is "I need someone."
/// It builds during social deprivation, is relieved by any social contact
/// (chat, voice, a person in view), and affects oxytocin tone, the action
/// portfolio (lonely brains seek social contact specifically), and the
/// feelings mapper (loneliness feels different from boredom — it's a craving,
/// not just restlessness). Cacioppo and Hawkley 2009 for the human analogue.
/// </summary>
public sealed class LonelinessTracker
{
    /// <summary>0 = socially satisfied, 1 = deeply lonely.</summary>
    public float Level { get; private set; }

    /// <summary>How many seconds since the last social interaction.</summary>
    public float SecondsSinceSocial { get; private set; }

    private float _lastSocialMs = -1e9f;

    /// <summary>Call every simulated millisecond.</summary>
    public void Tick(SpikingNetwork net, IdleController idle)
    {
        float t = net.TimeMs;
        SecondsSinceSocial = (t - _lastSocialMs) / 1000f;

        // loneliness builds slowly during social deprivation (much slower than boredom)
        // and only while awake
        if (idle.Sleep.State == BioComp.Core.Sleep.BrainState.Wake && SecondsSinceSocial > 30f)
        {
            float buildup = 0.00001f * MathF.Min(SecondsSinceSocial / 60f, 3f);
            Level = Math.Clamp(Level + buildup, 0f, 1f);
        }
        // loneliness decays during sleep (you don't feel lonely while unconscious)
        else if (idle.Sleep.State != BioComp.Core.Sleep.BrainState.Wake)
        {
            Level = MathF.Max(0f, Level - 0.00002f);
        }
    }

    /// <summary>Call when any social stimulus occurs (chat, voice, person seen).</summary>
    public void OnSocialContact(SpikingNetwork net, float quality = 0.5f)
    {
        _lastSocialMs = net.TimeMs;
        // relief is proportional to the quality of the interaction
        Level = MathF.Max(0f, Level - quality * 0.4f);
    }

    /// <summary>Loneliness feeds into the social drive for the action portfolio.</summary>
    public float SocialDrive => Level * 1.5f;
}
