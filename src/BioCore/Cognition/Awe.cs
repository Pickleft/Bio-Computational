using BioComp.Core.Memory;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// AWE (Keltner &amp; Haidt 2003): the emotion of vastness that requires
/// ACCOMMODATION — the world just got bigger than the shape the mind had
/// for it. Mechanistic trigger here: the concept graph suddenly rewiring
/// (several new associations within one window — the knowledge world
/// reorganizing), or a single hugely novel percept. The signature: dopamine
/// AND serotonin rise TOGETHER (reward + the humility of smallness), the
/// thought loop receives "something vast just happened", and the need to
/// accommodate accelerates emotional granularity (Piagetian accommodation
/// paid forward).
///
/// Rare by design (10-minute floor) — awe that happens every minute is
/// just noise with good marketing.
///
/// Sim-thread only.
/// </summary>
public sealed class Awe
{
    /// <summary>New links within one window that constitute vastness. // CALIBRATE</summary>
    public int VastLinkGrowth { get; set; } = 4;
    /// <summary>Minimum spacing between awe experiences (ms). // CALIBRATE</summary>
    public float CooldownMs { get; set; } = 600_000f;

    public int Count { get; private set; }
    public bool JustStruck { get; private set; }
    /// <summary>Latched strike, consumed by the granularity accommodation.</summary>
    public bool StrikePending { get; private set; }


    /// <summary>Restore awe history after a restart.</summary>
    public void Restore(int count) => Count = count;
    /// <summary>Consume the pending accommodation (returns true once per strike).</summary>
    public bool ConsumeStrike()
    {
        bool p = StrikePending;
        StrikePending = false;
        return p;
    }
    private int _lastLinks;
    private float _lastAtMs = -1e9f;
    private float _nextCheckMs;

    /// <summary>Call every few seconds with the live concept graph.</summary>
    public void Tick(SpikingNetwork net, ConceptGraph concepts)
    {
        JustStruck = false;
        if (net.TimeMs < _nextCheckMs) return;
        _nextCheckMs = net.TimeMs + 10_000f;

        int links = concepts.LinkCount;
        int growth = links - _lastLinks;
        _lastLinks = links;

        if (growth >= VastLinkGrowth && net.TimeMs - _lastAtMs > CooldownMs)
        {
            Count++;
            JustStruck = true;
            StrikePending = true;      // latched until the accommodation is consumed
            _lastAtMs = net.TimeMs;
            // the signature: reward AND the humility of smallness, together
            net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.22f);
            net.Modulators.Release(NeuromodulatorKind.Serotonin, 0.18f);
        }
    }

    /// <summary>The accommodation thought — vastness needs a bigger shape.</summary>
    public string AccommodationSeed => JustStruck
        ? "something vast just happened - the world just got bigger than the shape I had for it. " +
          "I need to make a bigger shape. What did I just become part of?"
        : "";

    /// <summary>For the diary + testament.</summary>
    public string Narrative => Count == 0
        ? "never felt awe"
        : $"felt awe {Count} time(s) - the world outgrew its shapes";
}
