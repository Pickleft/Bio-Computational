using BioComp.Core.Network;

namespace BioComp.Core.Cognition;

/// <summary>
/// Section 7 of the architecture: OPTIONAL language feedback. When the flag is ON,
/// the interpreter's monologue text is hashed into the LangEnc region as spike
/// injection - the brain hears its own inner voice. Every injected spike carries
/// InterpreterFed provenance and is logged separately so experimenters can always
/// separate endogenous dynamics from interpreter-driven ones. Default OFF.
/// </summary>
public sealed class LanguageFeedback
{
    /// <summary>Master flag. Default OFF per the architecture contract.</summary>
    public bool Enabled { get; set; }

    /// <summary>All interpreter-fed injections, logged separately from world input.</summary>
    public readonly List<(float TimeMs, string Text, int NeuronsStimulated)> Log = [];

    public int TotalInjections { get; private set; }

    /// <summary>Hash the monologue into LangEnc spike injection. No-op when disabled.</summary>
    public void Feed(SpikingNetwork net, string monologueText)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(monologueText)) return;
        var lang = net.Regions.FirstOrDefault(r => r.Name is "Lang" or "LangEnc");
        if (lang is null) return;

        // deterministic text hash -> neuron selection (same text, same neurons)
        ulong h = 1469598103934665603UL;
        foreach (char c in monologueText) h = (h ^ c) * 1099511628211UL;

        int stimulated = 0;
        for (int k = 0; k < 12; k++)
        {
            h ^= h << 13; h ^= h >> 7; h ^= h << 17;
            int neuron = lang.Start + (int)(h % (ulong)lang.Count);
            net.InjectCurrent(neuron, 16f);
            stimulated++;
        }
        TotalInjections++;
        Log.Add((net.TimeMs, monologueText[..Math.Min(60, monologueText.Length)], stimulated));
    }
}
