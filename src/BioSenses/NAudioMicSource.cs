using System.Collections.Concurrent;
using BioComp.Core.Abstractions;
using NAudio.Wave;

namespace BioComp.Senses;

/// <summary>
/// Real microphone behind IAudioSource: 16 kHz mono 16-bit, buffered internally,
/// handed out in 512-sample blocks (32 ms). Nothing is ever written to disk.
/// </summary>
public sealed class NAudioMicSource : IAudioSource
{
    public const int SampleRate = 16_000, BlockSize = 512;

    private readonly WaveIn _waveIn;
    private readonly ConcurrentQueue<short[]> _blocks = new();
    private readonly List<short> _staging = new(BlockSize * 2);
    private bool _disposed;

    public NAudioMicSource(int deviceIndex = 0)
    {
        _waveIn = new WaveIn { DeviceNumber = deviceIndex, WaveFormat = new WaveFormat(SampleRate, 16, 1), BufferMilliseconds = 50 };
        _waveIn.DataAvailable += (_, e) =>
        {
            for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
                _staging.Add(BitConverter.ToInt16(e.Buffer, i));
            while (_staging.Count >= BlockSize)
            {
                var block = new short[BlockSize];
                for (int i = 0; i < BlockSize; i++) block[i] = _staging[i];
                _staging.RemoveRange(0, BlockSize);
                _blocks.Enqueue(block);
            }
        };
        _waveIn.StartRecording();
    }

    public bool TryReadNext(out AudioBlock block)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_blocks.TryDequeue(out var samples))
        {
            block = new AudioBlock(samples, SampleRate);
            return true;
        }
        block = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _waveIn.StopRecording();
        _waveIn.Dispose();
    }
}
