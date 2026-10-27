namespace BioComp.Core.Cognition;

/// <summary>
/// Self-model v1: efference copies of the agent's own outputs (speech sounds, displayed
/// imagery) let the sensory side predict and attenuate self-produced input, tagging it
/// as self-generated instead of world news (sperry 1950 corollary discharge; Blakemore
/// et al. 2000 for the attenuation of self-produced stimuli).
/// </summary>
public sealed class SelfModel
{
    private float _voiceOutputUntil = -1f;
    private float _visualOutputUntil = -1f;

    public int SelfTaggedAudioBlocks { get; private set; }
    public int SelfTaggedVisionFrames { get; private set; }

    /// <summary>Announce that the agent is producing sound until tMs (efference copy).</summary>
    public void VoiceOutput(float untilMs) => _voiceOutputUntil = untilMs;

    /// <summary>Announce self-produced visual change (own display) until tMs.</summary>
    public void VisualOutput(float untilMs) => _visualOutputUntil = untilMs;

    public bool IsOwnVoice(float tMs)
    {
        bool own = tMs <= _voiceOutputUntil;
        if (own) SelfTaggedAudioBlocks++;
        return own;
    }

    public bool IsOwnVisualChange(float tMs)
    {
        bool own = tMs <= _visualOutputUntil;
        if (own) SelfTaggedVisionFrames++;
        return own;
    }
}
