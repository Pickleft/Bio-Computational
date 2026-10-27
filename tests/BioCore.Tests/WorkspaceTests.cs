using BioComp.Core.Cognition;
using BioComp.Core.Metrics;
using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;
using BioComp.Core.Sleep;
using Xunit;

namespace BioCore.Tests;

public class WorkspaceTests
{
    [Fact]
    public void Lz76_OrdersSequencesByComplexity()
    {
        // Kaspar-Schuster reference example: c("1001111011000010") = 6
        Assert.Equal(6, PciMeasure.Lz76("1001111011000010"));
        // constant sequences are least complex; random-ish ones most
        int constant = PciMeasure.Lz76("0000000000000000");
        int alternating = PciMeasure.Lz76("0101010101010101");
        int varied = PciMeasure.Lz76("0110100110010110");
        Assert.True(constant <= alternating);
        Assert.True(alternating <= varied);
    }

    [Fact]
    public void Pci_DownStateSuppressesComplexity()
    {
        // Perturbation during a global down phase (NREM-like, imposed via the per-tick
        // hook) propagates poorly; the same perturbation in the normal state differentiates.
        float Measure(bool downState, ulong seed)
        {
            var net = new SpikingNetwork(BrainConfig.Default(seed: seed));
            return PciMeasure.Measure(net, fractionPerturbed: 0.03f, picoAmps: 10f, perTick: downState
                ? (n, _) => { for (int i = 0; i < n.V.Length; i++) n.InjectCurrent(i, -15f); }
                : null);
        }
        float awake = Measure(false, 3);
        float down = Measure(true, 4);
        Assert.True(awake > down,
            $"awake PCI ({awake:F3}) should exceed down-state ({down:F3})");
    }

    [Fact]
    public void StrongVisualSalience_Ignites_WithRefractory()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 71));
        var idle = new IdleController(net);

        // quiet first: no ignition
        for (int t = 0; t < 2_000; t++) idle.Tick(net);
        Assert.Equal(0, idle.Workspace.IgnitionCount);

        // a strong, sustained visual event
        idle.VisionSalience = 1.5f;
        int ignitionsAtFirst = 0;
        for (int t = 0; t < 5_000; t++)
        {
            idle.VisionSalience = 1.5f; // keep submitting (Tick decays it slightly)
            idle.Tick(net);
            ignitionsAtFirst = Math.Max(ignitionsAtFirst, idle.Workspace.IgnitionCount);
        }
        Assert.True(idle.Workspace.IgnitionCount >= 1, "sustained salience never ignited");
        Assert.Equal(WorkspaceModule.Vision, idle.Workspace.LastIgnition.Winner);
        Assert.Contains(idle.Workspace.IgnitionLog, e => e.Winner == WorkspaceModule.Vision);

        // refractory + decaying salience: ignitions stay bounded over 5 s
        Assert.True(idle.Workspace.IgnitionCount < 20,
            $"refractory not respected ({idle.Workspace.IgnitionCount} ignitions in 5 s)");
    }

    [Fact]
    public void Feelings_TrackNeuromodulatorsAndBody()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 81));
        var idle = new IdleController(net);
        for (long t = 0; t < 500; t++) idle.Tick(net);
        float baselineValence = idle.Feelings.Last.Valence;

        // dopamine burst: positive valence, high arousal -> curious or calm-positive
        for (int i = 0; i < 30; i++) net.Modulators.Release(NeuromodulatorKind.Dopamine, 0.5f);
        idle.VisionSalience = 1.2f; // something interesting happening
        for (long t = 0; t < 500; t++) idle.Tick(net);
        Assert.True(idle.Feelings.Last.Valence > baselineValence,
            "dopamine burst should lift valence");
        Assert.Equal("curious", idle.Feelings.Last.Name);

        // dead battery: negative valence
        idle = new IdleController(new SpikingNetwork(BrainConfig.Default(seed: 81)))
        {
            Body = new BioComp.Core.Abstractions.InteroceptionSample(12, false, 2, 30f, 6f),
        };
        var net2 = new SpikingNetwork(BrainConfig.Default(seed: 82));
        for (long t = 0; t < 300; t++) idle.Tick(net2);
        Assert.True(idle.Feelings.Last.Valence < 0f, "battery deficit should sink valence");
    }

    [Fact]
    public void Actions_RespectSleepQuietHoursAndLearnFromFeedback()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 91));
        var idle = new IdleController(net);
        idle.Sleep.DayLengthMs = 20_000;

        // asleep: no actions
        long t = 0;
        for (; t < 20_000 && idle.Sleep.State == BrainState.Wake; t++) idle.Tick(net);
        if (idle.Sleep.State is BrainState.NREM or BrainState.REM)
            Assert.Null(idle.Actions.Choose(net, idle.Sleep, idle.Workspace, idle.Feelings.Last, 0.5f, 0.5f));

        // awake, quiet hours, no ignition: speaking impossible
        idle.Sleep.ForceWake(net);
        idle.Actions.QuietHoursActive = true;
        var choice = idle.Actions.Choose(net, idle.Sleep, idle.Workspace, idle.Feelings.Last, 0.2f, 0.9f);
        Assert.NotEqual(BrainAction.Speak, choice);

        // with ignition and social drive and no quiet hours, speak becomes possible
        idle.Actions.QuietHoursActive = false;
        for (int i = 0; i < 600; i++) { idle.VisionSalience = 2f; idle.Tick(net); } // let it ignite
        Assert.True(idle.Workspace.IgnitionCount > 0, "sustained salience never ignited");
        var speakPossible = false;
        for (int attempt = 0; attempt < 200 && !speakPossible; attempt++)
            speakPossible = idle.Actions.Choose(net, idle.Sleep, idle.Workspace, idle.Feelings.Last, 0.2f, 2.0f) == BrainAction.Speak;
        Assert.True(speakPossible, "speak never chosen despite ignition + strong social drive");

        // negative social feedback suppresses speaking value
        float qBefore = idle.Actions.Q[(int)BrainAction.Speak];
        idle.Actions.OnOutcome(net, BrainAction.Speak, welcomed: false);
        Assert.True(idle.Actions.Q[(int)BrainAction.Speak] < qBefore, "feedback did not lower Q(speak)");
    }

    [Fact]
    public void SelfModel_TagsOwnOutputs()
    {
        var self = new SelfModel();
        Assert.False(self.IsOwnVoice(100f));
        self.VoiceOutput(500f);
        Assert.True(self.IsOwnVoice(400f));
        Assert.False(self.IsOwnVoice(600f));
        Assert.Equal(1, self.SelfTaggedAudioBlocks);
    }

    [Fact]
    public void AnswerWhatHappened_UsesRealEpisodes()
    {
        var net = new SpikingNetwork(BrainConfig.Default(seed: 101));
        var idle = new IdleController(net);
        var ca3 = net.Regions.First(r => r.Name == "CA3");
        for (long t = 0; t < 400; t++) net.Tick();
        for (int burst = 0; burst < 6; burst++)
        {
            foreach (int n in Enumerable.Range(ca3.Start, 32)) net.InjectCurrent(n, 25f);
            for (int t = 0; t < 20; t++) net.Tick();
        }
        Assert.NotNull(net.EpisodeStore.Encode(net, "CA3", ["visitor"], 0.8f, minMeanRateHz: 0.005f));

        string answer = idle.AnswerWhatHappened(net);
        Assert.Contains("visitor", answer);
    }
}
