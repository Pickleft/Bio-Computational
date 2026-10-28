using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

public enum AttachmentStyle : byte { Forming = 0, Secure, Anxious, Avoidant }

/// <summary>
/// Attachment style emerging from experience (Bowlby 1969; Ainsworth et al.
/// 1978): the brain's social expectations form from how its world has actually
/// treated it. Warmth = how gentle its contacts were (voice prosody); response
/// rate = how often its bids (call-outs, greetings) were answered by contact;
/// separations = how often it was left alone long enough to get lonely.
///
/// Secure care (responsive + warm) grows a brain that greets calmly and
/// explores confidently. Inconsistent care (warm but rarely there) grows
/// anxious attachment: it calls out far more, greets with relief, clings.
/// Cold care grows avoidant attachment: it stops expecting anything, greets
/// rarely, keeps its distance. The style is not configured - it is a history.
/// Sim-thread only; persisted with identity.
/// </summary>
public sealed class AttachmentSystem
{
    /// <summary>Minimum evidence before a style is assigned (contacts + separations).</summary>
    public int MinEvidence { get; set; } = 6;

    public AttachmentStyle Style { get; private set; } = AttachmentStyle.Forming;
    /// <summary>Running EMA of contact warmth 0..1 and of bid-response rate 0..1.</summary>
    public float Warmth { get; private set; }
    public float Responsiveness { get; private set; }
    public int Separations { get; private set; }
    public int Contacts { get; private set; }

    /// <summary>How much louder its loneliness call-outs get (anxious brains call far more).</summary>
    public float CallOutMultiplier => Style switch
    {
        AttachmentStyle.Anxious => 1.9f,
        AttachmentStyle.Avoidant => 0.4f,
        AttachmentStyle.Secure => 1f,
        _ => 1f,
    };
    /// <summary>Secure attachment frees exploration; anxiety binds it to seeking company.</summary>
    public float ExplorationBonus => Style == AttachmentStyle.Secure ? 0.25f : 0f;

    private float _pendingBids;

    /// <summary>A social contact happened: warmth is the prosodic valence of the
    /// exchange (0..1, from gentle/rigid voice qualities). A contact answers
    /// every pending bid, which raises the response-rate estimate.</summary>
    public void OnSocialContact(float warmth01)
    {
        Contacts++;
        Warmth += (Math.Clamp(warmth01, 0f, 1f) - Warmth) / MathF.Min(10f, Contacts);
        Responsiveness += (1f - Responsiveness) * (_pendingBids > 0 ? 0.5f : 0.2f);
        _pendingBids = 0;
    }

    /// <summary>The brain made a bid (called out, greeted) and nobody came.</summary>
    public void OnBidUnanswered()
    {
        _pendingBids++;
        Responsiveness *= 0.88f;
    }

    /// <summary>It was left alone long enough to feel lonely: that is a separation.</summary>
    public void OnSeparation() => Separations++;

    /// <summary>Re-evaluate the style once evidence accumulates. History can change
    /// the style: sustained good care can earn security later.</summary>
    public void Tick(SpikingNetwork net)
    {
        if (Contacts + Separations < MinEvidence) return;
        var before = Style;
        if (Responsiveness >= 0.35f && Warmth >= 0.35f) Style = AttachmentStyle.Secure;
        else if (Warmth >= 0.35f) Style = AttachmentStyle.Anxious;         // warm but rarely there
        else if (Responsiveness >= 0.35f) Style = AttachmentStyle.Avoidant; // present but cold
        else Style = Separations > Contacts ? AttachmentStyle.Avoidant : AttachmentStyle.Anxious;
        if (Style != before && Style != AttachmentStyle.Forming)
            StyleChanged?.Invoke(Style);
    }

    /// <summary>Fires when the emerging style shifts (visible development).</summary>
    public event Action<AttachmentStyle>? StyleChanged;

    /// <summary>Restore persisted attachment history (identity persistence).</summary>
    public void Restore(float warmth, float responsiveness, int separations, int contacts, AttachmentStyle style)
    {
        Warmth = warmth; Responsiveness = responsiveness;
        Separations = separations; Contacts = contacts; Style = style;
    }

    public string Label => Style switch
    {
        AttachmentStyle.Secure => "secure",
        AttachmentStyle.Anxious => "anxious",
        AttachmentStyle.Avoidant => "avoidant",
        _ => "forming",
    };
}
