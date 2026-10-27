using BioComp.Core.Abstractions;
using OpenCvSharp;

namespace BioComp.Senses;

/// <summary>
/// Real webcam behind IFrameSource: grayscale, resized to 64x64 for the event-vision
/// encoder. Non-blocking: TryReadNext returns false until a new frame is available.
/// </summary>
public sealed class OpenCvCameraSource : IFrameSource
{
    private readonly VideoCapture _capture;
    private readonly Mat _gray = new();
    private readonly Mat _small = new(64, 64, MatType.CV_8UC1);
    private bool _disposed;

    public OpenCvCameraSource(int deviceIndex = 0)
    {
        _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
        _capture.FrameWidth = 320;
        _capture.FrameHeight = 240;
        if (!_capture.IsOpened())
            throw new InvalidOperationException($"camera {deviceIndex} could not be opened");
    }

    public bool TryReadNext(out GrayFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_capture.Grab()) { frame = default; return false; }
        using var raw = new Mat();
        if (!_capture.Retrieve(raw)) { frame = default; return false; }
        Cv2.CvtColor(raw, _gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Resize(_gray, _small, _small.Size());
        var pixels = new byte[64 * 64];
        System.Runtime.InteropServices.Marshal.Copy(_small.Data, pixels, 0, pixels.Length);
        frame = new GrayFrame(pixels, 64, 64);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _capture.Release();
        _capture.Dispose();
        _gray.Dispose();
        _small.Dispose();
    }
}
