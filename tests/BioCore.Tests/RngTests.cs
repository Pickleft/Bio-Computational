using BioComp.Core.Rng;
using Xunit;

namespace BioCore.Tests;

public class RngTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new Xoshiro256StarStar(42);
        var b = new Xoshiro256StarStar(42);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var a = new Xoshiro256StarStar(42);
        var b = new Xoshiro256StarStar(43);
        bool anyDifferent = false;
        for (int i = 0; i < 100; i++) anyDifferent |= a.NextUInt64() != b.NextUInt64();
        Assert.True(anyDifferent);
    }

    [Fact]
    public void StateSaveRestore_ResumesExactSequence()
    {
        var a = new Xoshiro256StarStar(7);
        for (int i = 0; i < 100; i++) a.NextUInt64(); // advance
        var state = a.GetState();

        var b = new Xoshiro256StarStar(999); // different trajectory...
        b.SetState(state);                    // ...resumed from a's state
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var rng = new Xoshiro256StarStar(1);
        for (int i = 0; i < 100_000; i++)
        {
            double d = rng.NextDouble();
            Assert.True(d >= 0.0 && d < 1.0);
        }
    }
}
