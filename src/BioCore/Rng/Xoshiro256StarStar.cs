using System.Numerics;

namespace BioComp.Core.Rng;

/// <summary>
/// xoshiro256** - the single deterministic random source for the simulation.
/// Owned by the sim thread; state is saveable/restorable for checkpoint/resume.
/// Blackman &amp; Vigna, Scrambled Linear Pseudorandom Number Generators, ACM TOMS 2021.
/// </summary>
public sealed class Xoshiro256StarStar
{
    private ulong _s0, _s1, _s2, _s3;

    public Xoshiro256StarStar(ulong seed)
    {
        // SplitMix64 seeding; all-zero state is forbidden in xoshiro and this avoids it.
        var z = seed;
        _s0 = SplitMix(ref z);
        _s1 = SplitMix(ref z);
        _s2 = SplitMix(ref z);
        _s3 = SplitMix(ref z);
        if ((_s0 | _s1 | _s2 | _s3) == 0UL) _s0 = 0x9E3779B97F4A7C15UL;
    }

    private static ulong SplitMix(ref ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        ulong x = z;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s0 ^= _s1;
        _s1 ^= _s2;
        _s2 ^= _s3;
        _s3 ^= _s0;
        _s0 ^= t;
        _s2 = BitOperations.RotateLeft(_s2, 45);
        return result;
    }

    /// <summary>Uniform double in [0,1) with 53 bits of entropy.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Uniform float in [0,1) with 24 bits of entropy.</summary>
    public float NextFloat() => (NextUInt64() >> 40) * (1f / (1 << 24));

    /// <summary>Uniform int in [0, maxExclusive). Tiny modulo-free bias is acceptable here.</summary>
    public int NextInt(int maxExclusive) => (int)(NextDouble() * maxExclusive);

    /// <summary>Standard-normal sample (Box-Muller); consumes exactly two uniforms per call.</summary>
    public double NextGaussian()
    {
        double u1 = 1.0 - NextDouble();
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public ulong[] GetState() => [_s0, _s1, _s2, _s3];

    public void SetState(ulong[] state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfLessThan(state.Length, 4);
        _s0 = state[0]; _s1 = state[1]; _s2 = state[2]; _s3 = state[3];
    }
}
