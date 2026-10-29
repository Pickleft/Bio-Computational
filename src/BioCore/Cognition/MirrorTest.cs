using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

/// <summary>
/// The mirror test, computational form (Gallup 1970; contingency criterion
/// after Rochat 2003): can the system tell its OWN output from the world's?
/// Training: the agent "vocalizes" - a fixed motor pattern is injected while
/// the efference copy marks it self-produced (SelfModel; Sperry 1950 corollary
/// discharge). Repeated self-production wears the pathway deep (Hebbian
/// potentiation through STDP - nothing is hard-coded). Test: replay the OWN
/// pattern unannounced versus a matched NOVEL one, measuring the response
/// DOWNSTREAM of the sensory region (where transmission, not injection,
/// decides). Ratio = response(own) / response(novel) above 1 means the brain
/// responds preferentially to its own signature - the computational own-name
/// effect (Moray 1959; self-reference effect, Rogers et al. 1977). The mirror
/// is passed.
/// Sim-thread only; deterministic for a given seed.
/// </summary>
public static class MirrorTest
{
    public sealed record Result(float SelfResponse, float NovelResponse, float Ratio, bool Passed);

    /// <summary>One run: train on the own-pattern, then probe self vs novel.</summary>
    public static Result Run(SpikingNetwork net, IdleController idle,
        int trainReps = 30, int patternNeurons = 20, float picoAmps = 24f)
    {
        var a1 = net.Regions.First(r => r.Name == "A1");
        // measure DOWNSTREAM (A1 -> Thal): the injected current drives A1
        // directly no matter what was learned, but the SYNAPTIC response in
        // the target reflects transmission - and transmission learns
        var thal = net.Regions.First(r => r.Name == "Thal");
        var probe = Enumerable.Range(0, 48).Select(i => thal.Start + i * (thal.Count / 48)).ToArray();
        // deterministic motor signature: evenly spaced A1 neurons (seed-independent,
        // so the novel pattern can be matched in intensity)
        var own = Enumerable.Range(0, patternNeurons)
            .Select(i => a1.Start + i * (a1.Count / patternNeurons)).ToArray();
        var novel = Enumerable.Range(0, patternNeurons)
            .Select(i => a1.Start + (i * 7 + 3) % a1.Count).Distinct().ToArray();

        float Burst(int[] pattern, int windowMs = 250)
        {
            var counts = net.Recorder.Counts;
            ulong before = 0;
            foreach (var n in probe) before += counts[n];
            foreach (var n in pattern) net.InjectCurrent(n, picoAmps);
            for (int t = 0; t < windowMs; t++) net.Tick();
            ulong after = 0;
            foreach (var n in probe) after += counts[n];
            return (after - before) / (float)probe.Length;
        }

        // training: the agent vocalizes its signature again and again; the
        // efference copy marks each production as self-generated
        for (int rep = 0; rep < trainReps; rep++)
        {
            idle.Self.VoiceOutput(net.TimeMs + 400f);
            foreach (var n in own) net.InjectCurrent(n, picoAmps);
            for (int t = 0; t < 150; t++) net.Tick();
            for (int t = 0; t < 100; t++) net.Tick(); // inter-production rest
        }
        // a little noradrenaline clearance so test responses are comparable
        net.Modulators.Step();

        // test: unannounced replay of the own signature vs a matched novel one
        float self = Burst(own);
        for (int t = 0; t < 300; t++) net.Tick();
        float novelResp = Burst(novel);

        float ratio = novelResp > 0 ? self / novelResp : float.MaxValue;
        return new Result(self, novelResp, ratio, ratio > 1.1f);
    }
}
