using BioComp.Core.Network;
using BioComp.Core.Synapses;
using Xunit;

namespace BioCore.Tests;

public class GraphTests
{
    private static (SpikingNetwork Net, SynapseGraph G) Build()
    {
        var net = new SpikingNetwork(BrainConfig.Default(scale: 1));
        return (net, net.Syn);
    }

    [Fact]
    public void Csr_InvariantsHold()
    {
        var (_, g) = Build();
        Assert.Equal(0, g.RowPtr[0]);
        for (int i = 0; i < g.NeuronCount; i++)
        {
            Assert.True(g.RowPtr[i] <= g.RowPtr[i + 1], $"rowPtr not monotonic at {i}");
            for (int s = g.RowPtr[i]; s < g.RowPtr[i + 1]; s++)
            {
                Assert.Equal(i, g.PreIdx[s]);
                Assert.InRange(g.PostIdx[s], 0, g.NeuronCount - 1);
                Assert.InRange(g.DelayMs[s], (byte)1, (byte)SynapseGraph.MaxDelayMs);
                Assert.InRange(g.TmR[s], 0f, 1.0001f);
            }
        }
        Assert.Equal(g.SynapseCount, g.RowPtr[g.NeuronCount]);
        Assert.True(g.SynapseCount > 0);
    }

    [Fact]
    public void Dale_AllSynapsesRespectLaw()
    {
        var (net, g) = Build();
        for (int s = 0; s < g.SynapseCount; s++)
        {
            bool preInhibitory = net.Inhibitory[g.PreIdx[s]];
            if (preInhibitory)
            {
                Assert.True(g.WeightQ15[s] < 0, $"inhibitory pre has positive weight at synapse {s}");
                Assert.Equal(ReceptorKind.Inhibitory,
                    g.Receptors[s] & ReceptorKind.Inhibitory);
                Assert.Equal(ReceptorKind.None, g.Receptors[s] & ReceptorKind.Excitatory);
            }
            else
            {
                Assert.True(g.WeightQ15[s] > 0, $"excitatory pre has non-positive weight at synapse {s}");
                Assert.Equal(ReceptorKind.Excitatory,
                    g.Receptors[s] & ReceptorKind.Excitatory);
                Assert.Equal(ReceptorKind.None, g.Receptors[s] & ReceptorKind.Inhibitory);
            }
        }
    }

    [Fact]
    public void InhibitoryFraction_IsAboutTwentyPercent()
    {
        var (net, _) = Build();
        int inhibitory = 0;
        for (int i = 0; i < net.Inhibitory.Length; i++) inhibitory += net.Inhibitory[i] ? 1 : 0;
        double frac = (double)inhibitory / net.Inhibitory.Length;
        Assert.InRange(frac, 0.15, 0.25);
    }
}
