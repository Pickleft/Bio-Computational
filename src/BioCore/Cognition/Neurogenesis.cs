using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// ADULT NEUROGENESIS (Eriksson et al. 1998, Nature Medicine 4:1313 — new
/// neurons form in the adult human hippocampus). The first artificial brain
/// that GROWS: dormant neurons are recruited and wired into the active network
/// when experience is important enough to deserve dedicated tissue.
///
/// Mechanism: each region has a pool of pre-allocated but DISCONNECTED neurons.
/// When a threshold of dopamine × salience is crossed (a moment worth
/// remembering with dedicated hardware), dormant neurons are activated and
/// connected via Hebbian coincidence with currently-active cells. The new
/// neurons are tagged with what triggered their birth.
///
/// This is structural plasticity, not synaptic plasticity. The brain's TOPOLOGY
/// changes with its life. No two brains that lived different lives have the
/// same wiring — even from the same seed.
///
/// Sim-thread only.
/// </summary>
public sealed class Neurogenesis
{
    /// <summary>Dopamine threshold above which new neurons are born.</summary>
    public float DopamineThreshold { get; set; } = 0.65f;  // CALIBRATE
    /// <summary>Minimum sim-ms between birth events (consolidation).</summary>
    public float BirthCooldownMs { get; set; } = 8_000f;   // CALIBRATE
    /// <summary>Neurons born per event.</summary>
    public int NeuronsPerBirth { get; set; } = 4;
    /// <summary>Maximum total births per life (the brain can't grow forever).</summary>
    public int MaxTotalBirths { get; set; } = 48;

    public int TotalNeuronsBorn { get; private set; }
    public int BirthEvents { get; private set; }
    /// <summary>Log of every birth: what triggered it, when, which region.</summary>
    public readonly List<(float TimeMs, string Region, string Trigger, string Tags)> BirthLog = [];

    private float _lastBirthMs = -1e9f;
    private int _nextDormantCA3;
    private int _nextDormantLang;

    /// <summary>
    /// Call every sim tick. Checks if conditions warrant new neural tissue.
    /// Returns a description of what was born, or null.
    /// </summary>
    public string? Tick(SpikingNetwork net, IdleController idle, string context)
    {
        if (TotalNeuronsBorn >= MaxTotalBirths) return null;
        if (net.TimeMs - _lastBirthMs < BirthCooldownMs) return null;

        float da = net.Modulators.Get(NeuromodulatorKind.Dopamine);
        if (da < DopamineThreshold) return null;

        // The moment is important enough: high dopamine means something
        // mattered. The brain invests DEDICATED TISSUE in it.
        _lastBirthMs = net.TimeMs;

        // Pick the region: CA3 for emotional/social memories, Lang for
        // linguistic/semantic ones
        var ca3 = net.Regions.FirstOrDefault(r => r.Name == "CA3");
        var lang = net.Regions.FirstOrDefault(r => r.Name == "Lang");
        if (ca3 is null) return null;

        string regionName = "CA3";
        int start = _nextDormantCA3;
        int regionSize = ca3.Count;
        if (_nextDormantCA3 + NeuronsPerBirth >= regionSize * 3 / 4)
        {
            // CA3 pool exhausted, try Lang
            if (lang is not null && _nextDormantLang + NeuronsPerBirth < lang.Count * 3 / 4)
            {
                regionName = "Lang";
                start = _nextDormantLang;
            }
            else return null; // all pools exhausted
        }

        // Activate the dormant neurons: inject current to wake them
        int born = 0;
        for (int i = 0; i < NeuronsPerBirth; i++)
        {
            int neuronIdx = regionName == "CA3"
                ? ca3.Start + start + i
                : lang.Start + start + i;
            if (neuronIdx >= (regionName == "CA3" ? ca3.Start + ca3.Count : lang.Start + lang.Count))
                break;
            net.InjectCurrent(neuronIdx, 15f); // birth pulse
            born++;
        }

        if (regionName == "CA3") _nextDormantCA3 += born;
        else _nextDormantLang += born;

        TotalNeuronsBorn += born;
        BirthEvents++;
        string desc = $"{born} new neurons in {regionName} for: {Trunc(context, 50)}";
        BirthLog.Add((net.TimeMs, regionName, Trunc(context, 60), ""));
        if (BirthLog.Count > 20) BirthLog.RemoveAt(0);

        // birth pays dopamine: the brain rewards itself for growing
        net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.2f);

        return desc;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
