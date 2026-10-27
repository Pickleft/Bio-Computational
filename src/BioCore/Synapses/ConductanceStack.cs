namespace BioComp.Core.Synapses;

/// <summary>
/// Pooled per-neuron conductances for the four receptor kinds. Two-state exponential
/// cascade per receptor: rise state x follows spike quanta with τr, conductance g
/// follows x with τd - a dual-exponential postsynaptic response.
/// </summary>
public sealed class ConductanceStack
{
    public readonly float[] XAmpa, XNmda, XGabaa, XGabab;
    public readonly float[] GAmpa, GNmda, GGabaa, GGabab;

    // Precomputed 1 ms decay factors (updated per tick, quanta delivered before decay).
    private readonly float _xAmpaDecay, _xNmdaDecay, _xGabaaDecay, _xGababDecay;
    private readonly float _gAmpaDecay, _gNmdaDecay, _gGabaaDecay, _gGababDecay;

    public ConductanceStack(int n)
    {
        XAmpa = new float[n]; XNmda = new float[n]; XGabaa = new float[n]; XGabab = new float[n];
        GAmpa = new float[n]; GNmda = new float[n]; GGabaa = new float[n]; GGabab = new float[n];
        static float Decay(float tauMs) => MathF.Exp(-1f / tauMs);
        _xAmpaDecay = Decay(ReceptorKinetics.TauRiseAmpaMs);
        _xNmdaDecay = Decay(ReceptorKinetics.TauRiseNmdaMs);
        _xGabaaDecay = Decay(ReceptorKinetics.TauRiseGabaaMs);
        _xGababDecay = Decay(ReceptorKinetics.TauRiseGababMs);
        _gAmpaDecay = Decay(ReceptorKinetics.TauDecayAmpaMs);
        _gNmdaDecay = Decay(ReceptorKinetics.TauDecayNmdaMs);
        _gGabaaDecay = Decay(ReceptorKinetics.TauDecayGabaaMs);
        _gGababDecay = Decay(ReceptorKinetics.TauDecayGababMs);
    }

    public void Deliver(int post, ReceptorKind kind, float amount)
    {
        if ((kind & ReceptorKind.Ampa) != 0) XAmpa[post] += amount * ReceptorKinetics.AmpaFraction;
        if ((kind & ReceptorKind.Nmda) != 0) XNmda[post] += amount * ReceptorKinetics.NmdaFraction;
        if ((kind & ReceptorKind.Gabaa) != 0) XGabaa[post] += amount * ReceptorKinetics.GabaaFraction;
        if ((kind & ReceptorKind.Gabab) != 0) XGabab[post] += amount * ReceptorKinetics.GababFraction;
    }

    public void DecayAll()
    {
        Follow(XAmpa, GAmpa, _xAmpaDecay, _gAmpaDecay);
        Follow(XNmda, GNmda, _xNmdaDecay, _gNmdaDecay);
        Follow(XGabaa, GGabaa, _xGabaaDecay, _gGabaaDecay);
        Follow(XGabab, GGabab, _xGababDecay, _gGababDecay);

        static void Follow(float[] x, float[] g, float xDecay, float gDecay)
        {
            for (int i = 0; i < x.Length; i++)
            {
                g[i] = g[i] * gDecay + x[i] * (1f - gDecay);
                x[i] = x[i] * xDecay;
            }
        }
    }

    /// <summary>Total synaptic current into neuron i at membrane potential v.</summary>
    public float Current(int i, float v)
    {
        float iExc = GAmpa[i] * (v - ReceptorKinetics.RevExcitatoryMv)
                   + GNmda[i] * (v - ReceptorKinetics.RevExcitatoryMv) * ReceptorKinetics.NmdaMgBlock(v);
        float iInh = GGabaa[i] * (v - ReceptorKinetics.RevInhibitoryMv)
                   + GGabab[i] * (v - ReceptorKinetics.RevGababMv);
        return iExc + iInh;
    }
}
