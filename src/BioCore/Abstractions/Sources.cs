namespace BioComp.Core.Abstractions;

/// <summary>A grayscale video frame from any source (webcam, file, mock).</summary>
public readonly record struct GrayFrame(byte[] Pixels, int Width, int Height);

/// <summary>Frame source abstraction. Mocks are deterministic so tests and the keyless
/// demo reproduce exactly; the real webcam lives in BioComp.Senses.</summary>
public interface IFrameSource : IDisposable
{
    /// <summary>Frames arrive at the source cadence; returns false when no new frame yet.</summary>
    bool TryReadNext(out GrayFrame frame);
}

/// <summary>Monaural PCM audio block.</summary>
public readonly record struct AudioBlock(short[] Samples, int SampleRate);

public interface IAudioSource : IDisposable
{
    bool TryReadNext(out AudioBlock block);
}

/// <summary>Machine body state.</summary>
public readonly record struct InteroceptionSample(
    byte BatteryPercent, bool OnAcPower, byte BatteryFlag,
    float CpuLoadPercent, float MemoryAvailableGb);

public interface IInteroceptionSource
{
    InteroceptionSample Read();
}

/// <summary>
/// Deterministic mock camera: a static room with faint sensor noise; a bright "person"
/// enters after <paramref name="personAtMs"/> (or when TriggerPerson is called), walks
/// to center over 1.5 s, then stands for the rest of PersonDurationMs. Frames are 64x64,
/// emitted on a 66 ms cadence driven by the consumer's Advance() calls (sim time).
/// </summary>
public sealed class MockFrameSource : IFrameSource
{
    public const int Width = 64, Height = 64, FrameIntervalMs = 66;
    private const long PersonDurationMs = 30_000;

    private readonly ulong _seed;
    private double _personAtMs;
    private long _lastEmittedMs = -FrameIntervalMs;

    public MockFrameSource(ulong seed, double personAtMs)
    {
        _seed = seed;
        _personAtMs = personAtMs;
    }

    public long ClockMs { get; private set; }

    /// <summary>Advance mock wall time when the consumer has no frames to read.</summary>
    public void Advance(long ms) => ClockMs += ms;

    /// <summary>Make the person enter right now (used by tests once the brain sleeps).</summary>
    public void TriggerPerson()
    {
        if (_personAtMs > ClockMs) _personAtMs = ClockMs;
    }

    public bool TryReadNext(out GrayFrame frame)
    {
        if (ClockMs - _lastEmittedMs < FrameIntervalMs) { frame = default; return false; }
        _lastEmittedMs += FrameIntervalMs;

        var pixels = new byte[Width * Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            // static room gradient with a deterministic noise floor
            int x = i % Width, y = i / Width;
            byte room = (byte)(90 + (x * 30 / Width) + (y * 20 / Height));
            ulong h = SplitMix((ulong)i ^ _seed);
            int noise = (int)((h >> 40) % 5) - 2;
            pixels[i] = (byte)Math.Clamp(room + noise, 0, 255);
        }

        double t = ClockMs;
        if (t >= _personAtMs && t < _personAtMs + PersonDurationMs)
        {
            double entryT = Math.Min((t - _personAtMs) / 1_500.0, 1.0); // walk in, then stand
            int originX = (int)(entryT * (Width - 26));
            for (int py = 16; py < 56; py++)
                for (int px = 0; px < 20; px++)
                {
                    int x = originX + px;
                    if (x < 0 || x >= Width) continue;
                    pixels[py * Width + x] = (byte)(200 + (px % 7)); // bright figure on dark room
                }
        }
        frame = new GrayFrame(pixels, Width, Height);
        return true;
    }

    private static ulong SplitMix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public void Dispose() { }
}

/// <summary>
/// Deterministic mock microphone: silence with a noise floor, then a voice-like
/// amplitude-modulated two-tone signal during a window. Blocks of 512 samples at
/// 16 kHz, emitted on a 32 ms cadence driven by the consumer's Advance() calls.
/// </summary>
public sealed class MockAudioSource : IAudioSource
{
    public const int SampleRate = 16_000, BlockSize = 512;
    public const int BlockIntervalMs = 32;

    private readonly ulong _seed;
    private readonly double _voiceAtMs, _voiceDurationMs;
    private long _lastEmittedMs = -BlockIntervalMs;
    private int _samplePos;

    public MockAudioSource(ulong seed, double voiceAtMs, double voiceDurationMs = 2_500)
    {
        _seed = seed;
        _voiceAtMs = voiceAtMs;
        _voiceDurationMs = voiceDurationMs;
    }

    public long ClockMs { get; private set; }

    public void Advance(long ms) => ClockMs += ms;

    public bool TryReadNext(out AudioBlock block)
    {
        if (ClockMs - _lastEmittedMs < BlockIntervalMs) { block = default; return false; }
        _lastEmittedMs += BlockIntervalMs;

        double tMs = (double)_samplePos / SampleRate * 1000.0;
        bool voice = tMs >= _voiceAtMs && tMs < _voiceAtMs + _voiceDurationMs;
        var samples = new short[BlockSize];
        for (int i = 0; i < BlockSize; i++)
        {
            int n = _samplePos + i;
            double t = n / (double)SampleRate;
            double value = 3_000.0 * Math.Sin(2 * Math.PI * 300 * t)         // voice-ish fundamental
                         + 1_500.0 * Math.Sin(2 * Math.PI * 2_200 * t)       // formant shimmer
                         + 600.0 * (1.0 + Math.Sin(2 * Math.PI * 4.5 * t));  // syllable envelope, always positive
            if (!voice) value = 0.0;
            ulong h = SplitMix((ulong)n ^ _seed);
            value += (int)((h >> 48) % 40) - 20;                             // noise floor (int math!)
            samples[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
        _samplePos += BlockSize;
        block = new AudioBlock(samples, SampleRate);
        return true;
    }

    private static ulong SplitMix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public void Dispose() { }
}

/// <summary>Deterministic mock body: slow battery drain, load pulses.</summary>
public sealed class MockInteroceptionSource : IInteroceptionSource
{
    private long _ticks;
    public InteroceptionSample Read()
    {
        _ticks++;
        float phase = _ticks / 600f; // 10-minute cycle at 1 Hz polling
        return new InteroceptionSample(
            BatteryPercent: (byte)(80 - (int)(phase * 2) % 30),
            OnAcPower: false,
            BatteryFlag: 1,
            CpuLoadPercent: 25 + 10 * MathF.Sin(phase),
            MemoryAvailableGb: 4.0f);
    }
}
