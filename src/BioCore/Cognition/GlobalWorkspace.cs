using BioComp.Core.Network;
using BioComp.Core.Neuromodulators;

namespace BioComp.Core.Cognition;

public enum WorkspaceModule : byte { Vision = 0, Audition, Memory, Interoception, Self, Language }

/// <summary>
/// Global workspace (Dehaene and Changeux 2011 Neuron 70): modules submit salience;
/// leaky integrators with cross inhibition compete; when the winner clears the
/// threshold with a margin, an ignition broadcasts (noradrenaline and acetylcholine
/// release, refractory period) and the content becomes reportable. The interpreter
/// rule downstream: only ignited content may be verbalized.
/// </summary>
public sealed class GlobalWorkspace
{
    public const int ModuleCount = 6;

    public readonly float[] Salience = new float[ModuleCount];        // submitted this tick
    private readonly float[] _integrated = new float[ModuleCount];

    public float IgnitionThreshold { get; set; } = 1.0f;   // CALIBRATE
    public float WinnerMargin { get; set; } = 0.3f;
    public float CrossInhibition { get; set; } = 0.25f;
    public float IntegrationTauMs { get; set; } = 100f;
    public int RefractoryMs { get; set; } = 300;

    /// <summary>
    /// Attention persistence: how much slower the winning module's salience decays
    /// vs the losers. High engagement = the brain LOCKS ON to the stimulus source
    /// and keeps processing it (top-down PFC attention). Default 0 = no persistence.
    /// </summary>
    public float AttentionPersistence { get; set; } = 0.5f;

    public int IgnitionCount { get; private set; }
    public WorkspaceModule? Broadcasting { get; private set; }
    public (float TimeMs, WorkspaceModule Winner, float Salience, string Tag) LastIgnition { get; private set; }
    public readonly List<(float TimeMs, WorkspaceModule Winner, string Tag)> IgnitionLog = [];

    private float _broadcastUntil = -1f;
    private long _refractoryUntil = long.MinValue;

    /// <summary>Submit a salience value for a module (replaces the previous value).</summary>
    public void Submit(WorkspaceModule module, float value) => Salience[(int)module] = value;

    /// <summary>Advance competition by one tick. Call before net.Tick each ms.</summary>
    public void Tick(SpikingNetwork net, string visionTag = "visual", string audioTag = "sound")
    {
        float decay = MathF.Exp(-1f / IntegrationTauMs);
        float sum = 0;
        for (int i = 0; i < ModuleCount; i++) sum += _integrated[i];

        // find the current leader for attention persistence
        int leader = 0;
        for (int i = 1; i < ModuleCount; i++)
            if (_integrated[i] > _integrated[leader]) leader = i;

        for (int i = 0; i < ModuleCount; i++)
        {
            float drive = Salience[i] - CrossInhibition * (sum - _integrated[i]);

            // attention persistence: the leader integrates more slowly toward zero
            // (top-down attention holds the focus), challengers decay normally
            float effectiveDecay = (i == leader && _integrated[i] > 0.3f)
                ? MathF.Exp(-1f / (IntegrationTauMs * (1f + AttentionPersistence * 3f)))
                : decay;

            _integrated[i] += (drive - _integrated[i]) * (1f - effectiveDecay);
        }

        Broadcasting = net.TimeMs < _broadcastUntil ? Broadcasting : null;

        if (net.TickIndex < _refractoryUntil) return;

        int winner = 0;
        for (int i = 1; i < ModuleCount; i++)
            if (_integrated[i] > _integrated[winner]) winner = i;
        float second = 0;
        for (int i = 0; i < ModuleCount; i++)
            if (i != winner) second = MathF.Max(second, _integrated[i]);

        if (_integrated[winner] >= IgnitionThreshold &&
            _integrated[winner] - second >= WinnerMargin)
        {
            var module = (WorkspaceModule)winner;
            string tag = module switch
            {
                WorkspaceModule.Vision => visionTag,
                WorkspaceModule.Audition => audioTag,
                WorkspaceModule.Memory => "replay",
                WorkspaceModule.Interoception => "body",
                WorkspaceModule.Self => "self",
                _ => "language",
            };
            IgnitionCount++;
            LastIgnition = (net.TimeMs, module, _integrated[winner], tag);
            IgnitionLog.Add((net.TimeMs, module, tag));
            Broadcasting = module;
            _broadcastUntil = net.TimeMs + 150f;
            _refractoryUntil = net.TickIndex + RefractoryMs;
            // ignition consumes the workspace: integrated saliences reset so the next
            // competitor starts from zero (Dehaene and Changeux 2011; this is what
            // creates the attentional blink - a second stimulus arriving during the
            // refractory must integrate from scratch against a decaying input)
            Array.Clear(_integrated);
            // broadcast: neuromodulatory gain up across the board
            net.Modulators.Release(NeuromodulatorKind.Noradrenaline, 0.3f);
            net.Modulators.Release(NeuromodulatorKind.Acetylcholine, 0.5f);
        }
    }
}
