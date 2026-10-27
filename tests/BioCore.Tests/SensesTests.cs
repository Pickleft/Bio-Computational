using BioComp.Core.Abstractions;
using BioComp.Core.Senses;
using Xunit;

namespace BioCore.Tests;

public class SensesTests
{
    [Fact]
    public void EventVision_StaticRoom_ProducesAlmostNoEvents()
    {
        var source = new MockFrameSource(seed: 1, personAtMs: 100_000); // person never arrives
        var encoder = new EventVisionEncoder(64, 64);
        int totalEvents = 0, frames = 0;
        while (source.ClockMs < 5_000)
        {
            if (source.TryReadNext(out var frame))
            {
                totalEvents += encoder.Process(frame).Count;
                frames++;
            }
            else source.Advance(10);
        }
        Assert.True(frames > 60, $"mock camera starved ({frames} frames)");
        Assert.InRange((float)totalEvents / frames, 0f, 0.5f); // habituated to sensor noise
    }

    [Fact]
    public void EventVision_PersonEntering_BurstsThenHabituates()
    {
        var source = new MockFrameSource(seed: 2, personAtMs: 2_000);
        var encoder = new EventVisionEncoder(64, 64);
        var eventsPerFrame = new List<int>();
        while (source.ClockMs < 8_000)
        {
            if (source.TryReadNext(out var frame))
                eventsPerFrame.Add(encoder.Process(frame).Count);
            else source.Advance(10);
        }

        int arrivalIdx = (int)(2_000 / MockFrameSource.FrameIntervalMs);
        int duringFirstSecond = eventsPerFrame.Skip(arrivalIdx).Take(15).Sum();
        int lateAfterHabituation = eventsPerFrame.Skip(arrivalIdx + 60).Take(15).Sum();

        Assert.True(duringFirstSecond > 200, $"no event burst on arrival ({duringFirstSecond})");
        Assert.True(lateAfterHabituation < duringFirstSecond / 4,
            "vision did not habituate to the moving person");
    }

    [Fact]
    public void Cochlea_Silence_NoOnsets()
    {
        var source = new MockAudioSource(seed: 3, voiceAtMs: 100_000);
        var cochlea = new Cochlea();
        int onsets = 0;
        for (int i = 0; i < 200; i++) // ~6.4 s of silence
        {
            source.Advance(MockAudioSource.BlockIntervalMs);
            if (source.TryReadNext(out var block)) cochlea.Process(block);
            if (cochlea.OnsetThisBlock) onsets++;
        }
        Assert.True(onsets <= 1, $"onsets in silence: {onsets}");
        Assert.False(cochlea.VoiceLikely);
    }

    [Fact]
    public void Cochlea_Voice_DetectedOnsetAndVad()
    {
        var source = new MockAudioSource(seed: 4, voiceAtMs: 2_000, voiceDurationMs: 3_000);
        var cochlea = new Cochlea();
        int onsets = 0;
        bool voiceSeen = false;
        for (int i = 0; i < 250; i++) // ~8 s
        {
            source.Advance(MockAudioSource.BlockIntervalMs);
            if (source.TryReadNext(out var block)) cochlea.Process(block);
            if (cochlea.OnsetThisBlock) onsets++;
            voiceSeen |= cochlea.VoiceLikely;
        }
        Assert.True(onsets >= 1, "voice onset not detected");
        Assert.True(voiceSeen, "voice activity never flagged");
    }

    [Fact]
    public void Cochlea_TonePeaksInTheRightBand()
    {
        // 1 kHz sine: the band whose center is nearest 1 kHz should dominate.
        const float toneHz = 1_000f;
        var cochlea = new Cochlea();
        int nearest = 0;
        for (int b = 1; b < Cochlea.BandCount; b++)
            if (MathF.Abs(cochlea.BandCentersHz[b] - toneHz) < MathF.Abs(cochlea.BandCentersHz[nearest] - toneHz))
                nearest = b;

        var samples = new short[4096];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (short)(8_000 * MathF.Sin(2f * MathF.PI * toneHz * i / 16_000f));
        for (int rep = 0; rep < 20; rep++) // let filters ring up
            cochlea.Process(new AudioBlock(samples, 16_000));

        float max = cochlea.BandEnergies.Max();
        Assert.True(cochlea.BandEnergies[nearest] > max * 0.6f,
            $"tone energy misplaced: band {nearest} = {cochlea.BandEnergies[nearest]:F4}, max = {max:F4}");
    }

    [Fact]
    public void PredictiveModel_Habituates_ThenSurprisesAgain()
    {
        var m = new PredictiveModel();
        for (int i = 0; i < 60_000; i++) m.Observe(1.0f); // a minute of constancy
        Assert.True(m.Novelty < 0.1f);

        float surprise = 0f;
        for (int i = 0; i < 2_000; i++) surprise = MathF.Max(surprise, m.Observe(5.0f));
        Assert.True(surprise > 3f, "step change should surprise");
        Assert.True(m.Novelty > 0.5f, "novelty trace should rise");
    }
}
