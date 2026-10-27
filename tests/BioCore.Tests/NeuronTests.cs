using BioComp.Core.Neurons;
using BioComp.Core.Network;
using Xunit;

namespace BioCore.Tests;

public class NeuronTests
{
    private static (SpikingNetwork Net, int RsIdx, int FsIdx) BuildNoiseless()
    {
        var quiet = BrainConfig.Default(scale: 1, seed: 1234);
        quiet.NoiseSigmaMv = 0f;
        var net = new SpikingNetwork(quiet);
        int rs = Array.IndexOf(net.Types, NeuronType.RegularSpiking);
        int fs = Array.IndexOf(net.Types, NeuronType.FastSpiking);
        return (net, rs, fs);
    }

    [Fact]
    public void DrivenRsNeuron_FiresTonicRange()
    {
        var (net, rs, _) = BuildNoiseless();
        for (int t = 0; t < 1000; t++)
        {
            net.InjectCurrent(rs, 10f);
            net.Tick();
        }
        float hz = net.Recorder.Counts[rs] / 1.0f; // 1000 ms window
        Assert.InRange(hz, 5f, 120f);
    }

    [Fact]
    public void FsNeuron_OutpacesRs_AtSameDrive()
    {
        var (net, rs, fs) = BuildNoiseless();
        for (int t = 0; t < 1000; t++)
        {
            net.InjectCurrent(rs, 10f);
            net.InjectCurrent(fs, 10f);
            net.Tick();
        }
        Assert.True(net.Recorder.Counts[fs] > net.Recorder.Counts[rs],
            $"FS {net.Recorder.Counts[fs]} Hz should exceed RS {net.Recorder.Counts[rs]} Hz");
    }

    [Fact]
    public void SilentNetwork_NoDriveNoNoise_StaysQuiet()
    {
        var cfg = BrainConfig.Default(seed: 5);
        cfg.NoiseSigmaMv = 0f;
        var net = new SpikingNetwork(cfg);
        // Recurrent weights exist, but with no input and no noise the quiescent state
        // should produce at most rare spontaneous spikes from initial-condition transients.
        for (int t = 0; t < 500; t++) net.Tick();
        Assert.True(net.Recorder.TotalSpikes < net.Recorder.Counts.Length * 2,
            $"expected near-silence, got {net.Recorder.TotalSpikes} spikes");
    }
}
