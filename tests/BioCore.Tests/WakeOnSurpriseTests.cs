using BioComp.Core.Abstractions;
using BioComp.Core.Cognition;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Senses;
using BioComp.Core.Sleep;
using Xunit;

namespace BioCore.Tests;

public class WakeOnSurpriseTests
{
    [Fact]
    public void SleepingBrain_WakesWhenAPersonEnters_NoCommand()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 61));
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 30_000; // night arrives within ~19 s
        var source = new MockFrameSource(seed: 62, personAtMs: 100_000); // person held back
        var encoder = new EventVisionEncoder(64, 64);
        var salience = new SalienceMonitor();
        var retina = net.Regions.First(r => r.Name == "Retina");

        void PumpFrame()
        {
            if (source.TryReadNext(out var frame))
            {
                var events = encoder.Process(frame);
                encoder.Inject(net, retina.Start, retina.Count, events);
                salience.OnVisionFrame(net, encoder.ChangeEnergyLastFrame);
                if (salience.SurpriseEvents > 0 && salience.ExternalArousal > 0.5f)
                    idle.OnEvent(net, [salience.SurpriseLog[^1].Tag], valence: 0.2f,
                        arousal: salience.ExternalArousal);
            }
        }

        // fall asleep in the quiet room
        while (idle.Sleep.State == BrainState.Wake && net.TimeMs < 25_000)
        {
            source.Advance(1);
            PumpFrame();
            idle.Tick(net, salience.ExternalArousal);
        }
        Assert.NotEqual(BrainState.Wake, idle.Sleep.State);

        float naBefore = net.Modulators.Get(NeuromodulatorKind.Noradrenaline);
        source.TriggerPerson();

        bool woke = false;
        float startMs = net.TimeMs;
        while (net.TimeMs - startMs < 5_000)
        {
            source.Advance(1);
            PumpFrame();
            idle.Tick(net, salience.ExternalArousal);
            if (idle.Sleep.State == BrainState.Wake && salience.SurpriseEvents > 0) { woke = true; break; }
        }

        Assert.True(woke, "the brain did not wake on the person entering");
        // let the delayed episode encode (250 ms after the event) flush
        for (int i = 0; i < 1_500; i++)
        {
            source.Advance(1);
            PumpFrame();
            idle.Tick(net, salience.ExternalArousal);
        }
        Assert.True(net.Modulators.Get(NeuromodulatorKind.Noradrenaline) > naBefore,
            "noradrenaline should rise with the surprise");
        Assert.Contains(net.EpisodeStore.Episodes, e => e.Tags.Contains("person"));
    }
}
