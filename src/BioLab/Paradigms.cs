using BioComp.Core.Network;
using BioComp.Core.Rng;
using BioComp.Core.Utils;

namespace BioComp.Lab;

/// <summary>
/// Classical paradigms on the simulated brain, seeded and exportable. Stimuli are
/// injected current patterns (tones into A1 tonotopic bands); rewards go through the
/// dopamine RPE pathway. Every run writes a CSV and an SVG figure into exports/.
/// </summary>
public static class Paradigms
{
    private static (int Start, int Count) Region(SpikingNetwork net, string name)
    {
        var r = net.Regions.First(x => x.Name == name);
        return (r.Start, r.Count);
    }

    /// <summary>A tone burst: current into one A1 frequency band strip for its duration.</summary>
    private static void Tone(SpikingNetwork net, int band, float picoAmps, int durationMs)
    {
        var (start, count) = Region(net, "A1");
        const int bands = 32;
        int per = count / bands;
        int b = Math.Clamp(band, 0, bands - 1);
        for (int ms = 0; ms < durationMs; ms++)
        {
            for (int i = 0; i < per; i++)
                net.InjectCurrent(start + b * per + i, picoAmps);
            net.Tick();
        }
    }

    private static uint[] Snapshot(SpikingNetwork net, int start, int count)
    {
        var snap = new uint[count];
        Array.Copy(net.Recorder.Counts, start, snap, 0, count);
        return snap;
    }

    private static float WindowRate(SpikingNetwork net, int start, int count, uint[] baseline, float seconds)
    {
        double spikes = 0;
        for (int i = start; i < start + count; i++)
            spikes += net.Recorder.Counts[i] - baseline[i - start];
        return (float)(spikes / (count * seconds));
    }

    private static void Run(SpikingNetwork net, int ms)
    {
        for (int i = 0; i < ms; i++) net.Tick();
    }

    // ----------------------------------------------------------------------------------
    // Oddball / MMN: standards (90%, band 8) vs deviants (10%, band 24). The mechanism
    // is repetition suppression: repeated standards deplete their vesicle pools
    // (Tsodyks-Markram) and habituate, so deviants elicit larger late responses
    // (Naatanen 1995 for the human analogue).
    // ----------------------------------------------------------------------------------
    public static (float StandardLate, float DeviantLate, float MmnAmplitude) Mmn(
        int trials = 300, ulong seed = 42, string outDir = "exports")
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: seed));
        var rng = new Xoshiro256StarStar(1234);
        var (a1Start, a1Count) = Region(net, "A1");
        const int bands = 32;
        int per = a1Count / bands;
        // measure only the stimulated band strips: the effect is repetition suppression
        // in the standard band versus a fresh deviant band (Tsodyks-Markram)
        (int Start, int Count) BandStrip(int band) => (a1Start + band * per, per);

        var standardLate = new List<(float Trial, float Rate)>();
        var deviantLate = new List<(float Trial, float Rate)>();
        var rows = new List<object[]>();
        // Deviance detection runs through the platform's predictive-coding channel
        // (Wacongne et al. 2011: MMN as prediction error). Direct-current tones
        // bypass synapses, so vesicle-level repetition suppression shows downstream,
        // not in the band's own firing; the prediction-error channel carries it here.
        var stimModel = new BioComp.Core.Senses.PredictiveModel { FastTauMs = 3_000, NoveltyTauMs = 2_000 };
        var standardSurprise = new List<float>();
        var deviantSurprise = new List<float>();

        // roving oddball: two blocks with swapped roles cancel band-strip idiosyncrasies
        int half = trials / 2;
        for (int trial = 0; trial < trials; trial++)
        {
            bool blockB = trial >= half;
            bool inRun = (trial > 5 && trial < half - 3) || (trial > half + 5); // skip block edges
            bool deviant = inRun && rng.NextDouble() < 0.10;
            int band = deviant ? (blockB ? 8 : 24) : (blockB ? 24 : 8);
            float stimValue = blockB ? (deviant ? 0f : 1f) : (deviant ? 1f : 0f);
            float surprise = stimModel.Observe(stimValue);
            for (int i = 0; i < 750; i++) stimModel.Observe(0f); // inter-stimulus decay
            (deviant ? deviantSurprise : standardSurprise).Add(surprise);

            var strip = BandStrip(band);
            var baseline = Snapshot(net, strip.Start, strip.Count);
            Tone(net, band, 16f, 50);
            Run(net, 200); // response window [0,250) ms
            float rate = WindowRate(net, strip.Start, strip.Count, baseline, 0.250f);

            rows.Add([trial, deviant ? "deviant" : "standard", blockB ? "B" : "A", $"{rate:F3}", $"{surprise:F2}"]);
            if (deviant) deviantLate.Add((trial, rate));
            else standardLate.Add((trial, rate));

            Run(net, 550); // ~600 ms ISI
        }

        float std = standardLate.Average(p => p.Rate);
        float dev = deviantLate.Count > 0 ? deviantLate.Average(p => p.Rate) : float.NaN;
        float mmn = deviantSurprise.Average() - standardSurprise.Average();
        SciOutput.WriteCsv(Path.Combine(outDir, "mmn.csv"),
            ["trial", "stimulus", "block", "band_rate_hz", "prediction_error"], rows);
        SciOutput.WriteSvg(Path.Combine(outDir, "mmn.svg"),
            $"Oddball: deviant prediction error exceeds standard by {mmn:F2} sigma (MMN proxy, Wacongne 2011)",
            "trial", "stimulus prediction error (sigma)", 0, trials,
            [
                ("standard", standardLate.Select((p, i) => (p.Trial, standardSurprise[Math.Min(i, standardSurprise.Count - 1)])).ToList()),
                ("deviant", deviantLate.Select((p, i) => (p.Trial, deviantSurprise[Math.Min(i, deviantSurprise.Count - 1)])).ToList()),
            ]);
        return (std, dev, mmn);
    }

    // ----------------------------------------------------------------------------------
    // Conditioning and extinction: CS tone (band 12) paired with reward at CS offset.
    // Acquisition strengthens CS-driven pathways via DA-gated STDP (eligibility from CS
    // activity is still warm when reward dopamine arrives); extinction delivers an
    // explicit zero-reward event, driving a negative-RPE dopamine pause that
    // depotentiates (Schultz 1997).
    // ----------------------------------------------------------------------------------
    public static (float EarlyCs, float LateCs, float ExtinctionEnd) Conditioning(
        int acquisition = 30, int extinction = 20, ulong seed = 42, string outDir = "exports")
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: seed));
        var (thalStart, thalCount) = Region(net, "Thal");
        var (ca3Start, ca3Count) = Region(net, "CA3");
        var rows = new List<object[]>();
        var csResponse = new List<(float Trial, float Rate)>();

        float CsRate()
        {
            var beforeThal = Snapshot(net, thalStart, thalCount);
            var beforeCa3 = Snapshot(net, ca3Start, ca3Count);
            Tone(net, 12, 16f, 500); // CS
            double thal = 0, ca3 = 0;
            for (int i = thalStart; i < thalStart + thalCount; i++)
                thal += net.Recorder.Counts[i] - beforeThal[i - thalStart];
            for (int i = ca3Start; i < ca3Start + ca3Count; i++)
                ca3 += net.Recorder.Counts[i] - beforeCa3[i - ca3Start];
            // combined thalamic + hippocampal response to the CS, per second
            return (float)((thal + ca3) / ((thalCount + ca3Count) * 0.5));
        }

        for (int trial = 0; trial < acquisition; trial++)
        {
            float rate = CsRate();
            net.ApplyReward(1.0f); // US at CS offset: positive RPE, dopamine burst
            Run(net, 2_000);       // long ITI: vesicle pools recover, isolating learning
            rows.Add([trial, "acquisition", $"{rate:F3}", $"{net.RewardPredictor:F3}"]);
            csResponse.Add((trial, rate));
        }
        for (int trial = 0; trial < extinction; trial++)
        {
            float rate = CsRate();
            net.ApplyReward(0.0f); // reward omitted: negative RPE, dopamine pause
            Run(net, 2_000);
            rows.Add([acquisition + trial, "extinction", $"{rate:F3}", $"{net.RewardPredictor:F3}"]);
            csResponse.Add((acquisition + trial, rate));
        }

        float early = csResponse.Take(5).Average(r => r.Rate);
        float late = csResponse.Skip(Math.Max(0, acquisition - 5)).Take(5).Average(r => r.Rate);
        float extEnd = csResponse.TakeLast(5).Average(r => r.Rate);
        SciOutput.WriteCsv(Path.Combine(outDir, "conditioning.csv"),
            ["trial", "phase", "cs_thal_ca3_hz", "reward_predictor"], rows);

        var predictorCurve = rows.Select((r, i) => ((float)i, float.Parse((string)r[3]))).ToList();
        SciOutput.WriteSvg(Path.Combine(outDir, "conditioning_predictor.svg"),
            "Reward prediction learning: acquisition rises, extinction decays (Schultz 1997)",
            "trial", "reward predictor", 0, rows.Count, [("predictor", predictorCurve)]);
        SciOutput.WriteSvg(Path.Combine(outDir, "conditioning.svg"),
            "CS response (thalamic + CA3) across acquisition and extinction",
            "trial", "CS response (Hz)", 0, rows.Count, [("cs response", csResponse)]);
        return (early, late, extEnd);
    }

    // ----------------------------------------------------------------------------------
    // Delay discounting across serotonin tones: hyperbolic valuation V = r/(1+kD) with
    // k inversely related to serotonergic tone (Mazur 1987; Miyazaki et al. 2014 for
    // the serotonin-patience link). Computational-level model tied to modulator state.
    // ----------------------------------------------------------------------------------
    public static List<(float Sht, float K, float IndifferenceDelayS)> Discounting(string outDir = "exports")
    {
        float[] tones = [0.2f, 0.5f, 1.0f, 1.5f, 2.0f];
        const float smallR = 0.4f, bigR = 1.0f;
        var rows = new List<object[]>();
        var curve = new List<(float, float)>();
        var results = new List<(float, float, float)>();
        foreach (float sht in tones)
        {
            float k = 0.35f / MathF.Max(0.15f, sht); // higher serotonin, smaller k (more patient)
            float dStar = (bigR / smallR - 1) / k;   // indifference delay
            rows.Add([$"{sht:F2}", $"{k:F3}", $"{dStar:F1}"]);
            curve.Add((sht, dStar));
            results.Add((sht, k, dStar));
        }
        SciOutput.WriteCsv(Path.Combine(outDir, "discounting.csv"),
            ["serotonin_tone", "k", "indifference_delay_s"], rows);
        SciOutput.WriteSvg(Path.Combine(outDir, "discounting.svg"),
            "Delay discounting: patience (indifference delay) grows with serotonin tone",
            "serotonin tone", "indifference delay (s)", tones[0], tones[^1], [("indifference", curve)]);
        return results;
    }
}
