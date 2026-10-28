using BioComp.Core.Abstractions;

namespace BioComp.Core.Senses;

/// <summary>
/// Cochlear proxy: ERB-spaced bandpass bank (RBJ biquads approximating a gammatone
/// filterbank; true gammatone kernels are a calibration TODO), spectral-flux onset
/// detection with an adaptive threshold and refractory period, and an energy-based
/// voice-activity hint. Deterministic: pure feed-forward arithmetic. Filter state is
/// continuous across blocks (narrow bands ring longer than one block).
/// </summary>
public sealed class Cochlea
{
    public const int BandCount = 20;
    public const float LowHz = 100f, HighHz = 6_000f;

    public readonly float[] BandCentersHz = new float[BandCount];
    private readonly Biquad[] _bands = new Biquad[BandCount];
    private readonly float[] _prevEnergy = new float[BandCount];

    private readonly float[] _fluxHistory = new float[32];
    private int _fluxCount;
    private int _blocksSinceOnset = 4; // refractory already satisfied (avoid int overflow from MaxValue)
    private int _voiceEnergyBlocks;

    public float[] BandEnergies { get; } = new float[BandCount];
    public bool OnsetThisBlock { get; private set; }
    public bool VoiceLikely { get; private set; }
    public float EnergyRms { get; private set; }
    public float FluxLast { get; private set; }

    public float OnsetSensitivity { get; set; } = 2.5f; // x sigma above mean flux // CALIBRATE
    public int MinInterOnsetBlocks { get; set; } = 4;   // ~128 ms refractory at 32 ms blocks
    public float VoiceFloorRms { get; set; } = 0.006f;  // higher threshold: only clear speech triggers VAD
    public int VoiceMinBlocks { get; set; } = 24;        // ~0.75s of sustained speech required
    public float AbsFluxFloor { get; set; } = 0.003f;   // silence guard: flux must clear this too

    public Cochlea(float sampleRate = 16_000f)
    {
        for (int b = 0; b < BandCount; b++)
        {
            float frac = b / (float)(BandCount - 1);
            float erbLow = HzToErb(LowHz), erbHigh = HzToErb(HighHz);
            BandCentersHz[b] = ErbToHz(erbLow + frac * (erbHigh - erbLow));
            _bands[b] = new Biquad(BandCentersHz[b], q: 6.0f, sampleRate);
        }
    }

    public void Process(in AudioBlock block)
    {
        Array.Clear(BandEnergies);
        double energySum = 0;
        int n = block.Samples.Length;
        for (int i = 0; i < n; i++)
        {
            float x = block.Samples[i] / 32768f;
            energySum += x * x;
            for (int b = 0; b < BandCount; b++)
            {
                float y = _bands[b].Process(x);
                BandEnergies[b] += y * y;
            }
        }
        for (int b = 0; b < BandCount; b++) BandEnergies[b] = MathF.Sqrt(BandEnergies[b] / n);
        EnergyRms = MathF.Sqrt((float)(energySum / n));

        // positive spectral flux vs previous block
        float flux = 0;
        for (int b = 0; b < BandCount; b++)
        {
            float d = BandEnergies[b] - _prevEnergy[b];
            if (d > 0) flux += d;
            _prevEnergy[b] = BandEnergies[b];
        }
        FluxLast = flux;

        // adaptive threshold from recent flux statistics (warms up over ~1 s)
        if (_fluxCount >= _fluxHistory.Length)
        {
            float mean = 0;
            for (int i = 0; i < _fluxHistory.Length; i++) mean += _fluxHistory[i];
            mean /= _fluxHistory.Length;
            float var = 0;
            for (int i = 0; i < _fluxHistory.Length; i++) var += (_fluxHistory[i] - mean) * (_fluxHistory[i] - mean);
            var /= _fluxHistory.Length;
            float threshold = mean + OnsetSensitivity * MathF.Sqrt(var) + 1e-4f;
            OnsetThisBlock = flux > threshold && flux > AbsFluxFloor && _blocksSinceOnset >= MinInterOnsetBlocks;
        }
        else
        {
            OnsetThisBlock = false;
        }
        _fluxHistory[_fluxCount++ % _fluxHistory.Length] = flux;
        _blocksSinceOnset = OnsetThisBlock ? 0 : _blocksSinceOnset + 1;

        // voice-activity hint: sustained energy well above the silence floor
        _voiceEnergyBlocks = EnergyRms > VoiceFloorRms ? _voiceEnergyBlocks + 1 : 0;
        VoiceLikely = _voiceEnergyBlocks >= VoiceMinBlocks; // sustained speech required
    }

    private static float HzToErb(float hz) => 21.4f * MathF.Log10(1f + 0.00437f * hz);
    private static float ErbToHz(float erb) => (MathF.Pow(10f, erb / 21.4f) - 1f) / 0.00437f;

    /// <summary>RBJ audio-cookbook bandpass (constant skirt gain); peak gain = Q at f0.</summary>
    private sealed class Biquad
    {
        private readonly float _b0, _b2, _a1, _a2;
        private float _x1, _x2, _y1, _y2;

        public Biquad(float f0, float q, float sampleRate)
        {
            float w0 = 2f * MathF.PI * f0 / sampleRate;
            float alpha = MathF.Sin(w0) / (2f * q);
            float cosw0 = MathF.Cos(w0);
            float b0 = alpha, b2 = -alpha;
            float a0 = 1f + alpha, a1 = -2f * cosw0, a2 = 1f - alpha;
            _b0 = b0 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public float Process(float x)
        {
            float y = _b0 * (x - _x2) - _a1 * _y1 - _a2 * _y2; // b1 = 0 form
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            return y;
        }
    }
}
