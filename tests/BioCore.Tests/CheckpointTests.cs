using BioComp.Core.Network;
using BioComp.Core.Persistence;
using BioComp.Core.Rng;
using Xunit;

namespace BioCore.Tests;

public class CheckpointTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"biocptest-{Guid.NewGuid():N}.ckpt");

    /// <summary>Drive loop identical on both branches: fixed driven set chosen by the script
    /// at the start of each Run call, tonic current every tick, reward every 100 ms.</summary>
    private static ulong Run(SpikingNetwork net, long from, long to, Xoshiro256StarStar script)
    {
        var driven = new int[30];
        for (int k = 0; k < driven.Length; k++) driven[k] = script.NextInt(net.V.Length);
        for (long t = from; t < to; t++)
        {
            foreach (int d in driven) net.InjectCurrent(d, 10f);
            if (t % 100 == 0) net.ApplyReward(0.5f);
            net.Tick();
        }
        return net.ComputeTrajectoryHash();
    }

    [Fact]
    public void SaveLoad_ResumeIsBitIdentical()
    {
        const long warmup = 800, cont = 700;

        // Reference run: warm up, checkpoint, continue.
        var scriptA = new Xoshiro256StarStar(777);
        var netA = new SpikingNetwork(BrainConfig.Default(seed: 9));
        Run(netA, 0, warmup, scriptA);
        CheckpointService.Save(netA, _path);
        ulong hashA = Run(netA, warmup, warmup + cont, scriptA);

        // Resumed run: the fresh script has consumed exactly one Run's worth of draws
        // (the warm-up's 30); after that its position matches the reference before its
        // continuation segment.
        var scriptB = new Xoshiro256StarStar(777);
        var netB = CheckpointService.Load(_path, BrainConfig.Default(seed: 9));
        for (int k = 0; k < 30; k++) scriptB.NextInt(1); // replay the warm-up's draws
        ulong hashB = Run(netB, warmup, warmup + cont, scriptB);

        Assert.Equal(hashA, hashB);
    }

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* temp cleanup best effort */ }
    }
}
