using System;

namespace VoxEngine.Utils;

/// <summary>
/// A fast, period-32 Xorshift RNG that is fully deterministic across .NET versions.
/// </summary>
public class DeterministicRandom
{
    private uint _state;

    public DeterministicRandom(int seed)
    {
        // Seed must be non-zero for Xorshift
        _state = seed == 0 ? 0xDEADC0DEu : (uint)seed;
    }

    /// <summary>
    /// Generates the next random unsigned integer.
    /// </summary>
    public uint Next()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return x;
    }

    public int Next(int max) => (int)(Next() % (uint)max);
    public double NextDouble() => (double)Next() / uint.MaxValue;
}