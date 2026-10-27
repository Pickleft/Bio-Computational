using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Senses;
using BioComp.Core.Utils;

namespace BioComp.Lab;

/// <summary>
/// Awareness paradigms (Breitmeyer and Ogmen 2006; Raymond, Shapiro and Arnell 1992):
/// both rely on the workspace refractory - a second stimulus arriving within the
/// refractory window after a first fails to ignite, exactly as conscious access
/// fails in humans.
/// </summary>
public static class AwarenessParadigms
{
    private static (int Start, int Count) Region(SpikingNetwork net, string name)
    {
        var r = net.Regions.First(x => x.Name == name);
        return (r.Start, r.Count);
    }

    private static void Flash(SpikingNetwork net, IdleController idle, int strip, float picoAmps, int durationMs, float salience)
    {
        var (start, count) = Region(net, "Retina");
        int per = count / 8;
        for (int ms = 0; ms < durationMs; ms++)
        {
            for (int i = 0; i < per; i++)
                net.InjectCurrent(start + strip * per + i, picoAmps);
            idle.Tick(net);
        }
        // the salience monitor would report this in the live pipeline; here we
        // submit it directly so the workspace can compete on it
        idle.VisionSalience = MathF.Max(idle.VisionSalience, salience);
    }

    /// <summary>Run the idle loop (not just net ticks) so the workspace integrates.</summary>
    private static void RunIdle(IdleController idle, SpikingNetwork net, int ms)
    {
        for (int i = 0; i < ms; i++) idle.Tick(net);
    }

    private static void Run(SpikingNetwork net, int ms)
    {
        for (int i = 0; i < ms; i++) net.Tick();
    }

    // ----------------------------------------------------------------------------------
    // Backward masking: a brief target flash is followed by a mask at varying ISI.
    // Report accuracy = ignition rate for the target as a function of ISI.
    // ----------------------------------------------------------------------------------
    public static (float[] Isis, float[] Accuracy) BackwardMasking(
        int trialsPerIsi = 8, ulong seed = 42, string outDir = "exports")
    {
        float[] isis = [0f, 17f, 33f, 50f, 83f, 150f];
        var accuracy = new float[isis.Length];
        var rows = new List<object[]>();

        for (int isiIdx = 0; isiIdx < isis.Length; isiIdx++)
        {
            float isi = isis[isiIdx];
            int ignited = 0;
            for (int trial = 0; trial < trialsPerIsi; trial++)
            {
                var net = new SpikingNetwork(BrainConfig.Default(seed: seed + (ulong)trial));
                var idle = new IdleController(net);
                for (long t = 0; t < 1_000; t++) idle.Tick(net); // settle

                int before = idle.Workspace.IgnitionCount;
                Flash(net, idle, 2, 20f, 16, 1.15f);   // target: marginal salience
                RunIdle(idle, net, (int)isi);           // ISI
                Flash(net, idle, 5, 20f, 50, 0.6f);    // mask: below threshold alone
                RunIdle(idle, net, 600);                // let the workspace decide
                if (idle.Workspace.IgnitionCount > before) ignited++;
            }
            accuracy[isiIdx] = (float)ignited / trialsPerIsi;
            rows.Add([$"{isi:F0}", trialsPerIsi, ignited, $"{accuracy[isiIdx]:F2}"]);
        }

        SciOutput.WriteCsv(Path.Combine(outDir, "masking.csv"),
            ["isi_ms", "trials", "ignited", "accuracy"], rows);
        SciOutput.WriteSvg(Path.Combine(outDir, "masking.svg"),
            "Backward masking: target ignition rate vs ISI (shorter ISI = masked)",
            "ISI (ms)", "ignition rate", 0, 150,
            [("accuracy", isis.Zip(accuracy, (i, a) => (i, a)).ToList())]);
        return (isis, accuracy);
    }

    // ----------------------------------------------------------------------------------
    // Attentional blink: T1 at a strong flash, T2 at varying lag after T1's ignition.
    // T2 is marginal (salience just above threshold): it must integrate fast enough
    // to ignite before its own salience decays below threshold. When T1's refractory
    // (300 ms) blocks the workspace, T2's salience decays during the block,
    // causing the classic dip at short lags.
    // ----------------------------------------------------------------------------------
    public static (float[] Lags, float[] Accuracy) AttentionalBlink(
        int trialsPerLag = 10, ulong seed = 42, string outDir = "exports")
    {
        float[] lags = [50f, 100f, 150f, 200f, 300f, 450f, 700f];
        var accuracy = new float[lags.Length];
        var rows = new List<object[]>();

        for (int lagIdx = 0; lagIdx < lags.Length; lagIdx++)
        {
            float lag = lags[lagIdx];
            int ignited = 0;
            for (int trial = 0; trial < trialsPerLag; trial++)
            {
                var net = new SpikingNetwork(BrainConfig.Default(seed: seed + (ulong)trial));
                var idle = new IdleController(net);
                for (long t = 0; t < 1_000; t++) idle.Tick(net); // settle

                // T1: strong salience, will definitely ignite
                Flash(net, idle, 4, 20f, 20, 2.5f);
                RunIdle(idle, net, 200);                           // T1 ignites during this

                // burn off T1's residual salience (0.1%/ms decay is slow)
                RunIdle(idle, net, Math.Max(0, (int)lag - 200));

                // clear any residual so T2 starts fresh; only T2's own salience counts
                idle.VisionSalience = 0;

                // T2: marginal — only ignites if the workspace is not refractory
                int logBefore = idle.Workspace.IgnitionLog.Count;
                Flash(net, idle, 6, 20f, 20, 1.25f);
                RunIdle(idle, net, 800);
                if (idle.Workspace.IgnitionLog.Count > logBefore &&
                    idle.Workspace.IgnitionLog.Skip(logBefore).Any(e =>
                        e.Winner == BioComp.Core.Cognition.WorkspaceModule.Vision))
                    ignited++;
            }
            accuracy[lagIdx] = (float)ignited / trialsPerLag;
            rows.Add([$"{lag:F0}", trialsPerLag, ignited, $"{accuracy[lagIdx]:F2}"]);
        }

        SciOutput.WriteCsv(Path.Combine(outDir, "blink.csv"),
            ["lag_ms", "trials", "t2_ignited", "accuracy"], rows);
        SciOutput.WriteSvg(Path.Combine(outDir, "blink.svg"),
            "Attentional blink: T2 ignition rate vs lag after T1 ignition",
            "lag (ms)", "T2 ignition rate", 0, 700,
            [("accuracy", lags.Zip(accuracy, (l, a) => (l, a)).ToList())]);
        return (lags, accuracy);
    }
}
