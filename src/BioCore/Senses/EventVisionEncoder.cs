using BioComp.Core.Abstractions;
using BioComp.Core.Network;

namespace BioComp.Core.Senses;

/// <summary>One retinotopic visual event: pixel position and polarity.</summary>
public readonly record struct VisionEvent(int X, int Y, sbyte Polarity);

/// <summary>
/// Event-camera style vision: spikes only on temporal contrast. Each pixel keeps an
/// adaptive threshold that habituates to its own change statistics, so a static room
/// produces almost no events and motion produces bursts (predictive processing: only
/// prediction errors propagate). Events map retinotopically onto a target neuron range
/// (fovea center weighting by default).
/// </summary>
public sealed class EventVisionEncoder
{
    private readonly int _w, _h;
    private byte[] _prev;
    private float[] _baseline;     // EMA of |delta| per pixel (habituation)
    private bool _hasPrev;

    public float Margin { get; set; } = 14f;      // CALIBRATE
    public float BaselineUp { get; set; } = 0.02f;
    public float BaselineDown { get; set; } = 0.10f;
    public float EventCurrentPicoAmps { get; set; } = 22f;

    public int EventsLastFrame { get; private set; }
    public float ChangeEnergyLastFrame { get; private set; }

    public EventVisionEncoder(int width, int height)
    {
        _w = width; _h = height;
        _prev = new byte[width * height];
        _baseline = new float[width * height];
    }

    /// <summary>Process a frame; returns events for this frame.</summary>
    public List<VisionEvent> Process(in GrayFrame frame)
    {
        var events = new List<VisionEvent>();
        if (frame.Width != _w || frame.Height != _h)
            throw new ArgumentException("frame size changed after construction");
        EventsLastFrame = 0;
        ChangeEnergyLastFrame = 0f;

        if (!_hasPrev)
        {
            // first frame: no temporal contrast exists yet, so no change energy either
            // (reporting the raw frame delta would poison downstream error statistics)
            Array.Copy(frame.Pixels, _prev, _prev.Length);
            _hasPrev = true;
            return events;
        }

        for (int i = 0; i < _prev.Length; i++)
        {
            int delta = frame.Pixels[i] - _prev[i];
            float absDelta = Math.Abs(delta);
            ChangeEnergyLastFrame += absDelta;
            if (_hasPrev && absDelta > _baseline[i] + Margin)
            {
                sbyte pol = delta > 0 ? (sbyte)1 : (sbyte)-1;
                events.Add(new VisionEvent(i % _w, i / _w, pol));
            }
            // asymmetric habituation: thresholds relax slowly, tighten fast
            float alpha = absDelta > _baseline[i] ? BaselineDown : BaselineUp;
            _baseline[i] += alpha * (absDelta - _baseline[i]);
            _prev[i] = frame.Pixels[i];
        }
        _hasPrev = true;
        EventsLastFrame = events.Count;
        return events;
    }

    /// <summary>Inject events into a retinotopic neuron strip (fovea-weighted).</summary>
    public void Inject(SpikingNetwork net, int neuronStart, int neuronCount, List<VisionEvent> events)
    {
        foreach (var e in events)
        {
            int idx = neuronStart + (e.Y * _w + e.X) % neuronCount;
            net.InjectCurrent(idx, e.Polarity * EventCurrentPicoAmps);
        }
    }
}
