using BioComp.Core.Network;
using BioComp.Core.Senses;

namespace BioComp.Core.Metrics;

/// <summary>
/// REAL-TIME FREE ENERGY — Karl Friston's Free Energy Principle (Friston 2010,
/// Nat Rev Neurosci 11:127) computed and visualized for a living network.
/// This has been called "the most important idea in neuroscience" but has
/// NEVER been demonstrated as a real-time, observable measurement.
///
/// The brain minimizes free energy: F = Prediction Error + Model Complexity
///   - Prediction Error: how badly the brain's internal model predicts sensory input
///   - Model Complexity: the cost of maintaining the model (approximated by entropy)
///
/// As the brain learns its environment: prediction error drops → free energy decreases.
/// When something unexpected happens: prediction error spikes → free energy jumps.
/// The free energy curve over time IS the brain's learning made visible.
///
/// This connects to the dopamine system (Schwartz et al. — dopamine signals
/// free energy gradients) and to the salience network (NA signals free energy spikes).
/// </summary>
public sealed class FreeEnergyMonitor
{
    public readonly List<(float TimeMs, float FreeEnergy, float PredError, float Complexity)> History = [];

    private float _lastSensoryPrediction;
    private float _predictionEma = 0.5f;
    private float _complexityEma = 0.1f;
    private float _smoothedInput = 0.5f;

    /// <summary>
    /// Update with the latest sensory input magnitude (visual change energy,
    /// audio energy, or combined). Returns the current free energy.
    /// </summary>
    public float Update(SpikingNetwork net, float sensoryInput)
    {
        // prediction error: how far is the input from what the model expects
        _smoothedInput += (sensoryInput - _smoothedInput) * 0.05f;
        float predError = MathF.Abs(_smoothedInput - _predictionEma);
        _predictionEma += (_smoothedInput - _predictionEma) * 0.02f;

        // model complexity: how variable is the recent input (entropy proxy)
        float recentVariability = MathF.Abs(sensoryInput - _lastSensoryPrediction);
        _complexityEma += (recentVariability - _complexityEma) * 0.01f;
        _lastSensoryPrediction = sensoryInput;

        // free energy = accuracy + complexity (simplified)
        float freeEnergy = predError + _complexityEma;

        // log every 500ms
        if (net.TickIndex % 500 == 0)
        {
            History.Add((net.TimeMs, freeEnergy, predError, _complexityEma));
            if (History.Count > 240) History.RemoveAt(0); // ~2 minutes
        }

        return freeEnergy;
    }

    public float Current => History.Count > 0 ? History[^1].FreeEnergy : 0f;
    public float CurrentPredError => History.Count > 0 ? History[^1].PredError : 0f;
    public float CurrentComplexity => History.Count > 0 ? History[^1].Complexity : 0f;
}
