namespace Aetheria.Engine.Core;

/// <summary>
/// Small, fast, fully deterministic pseudo-random generator (xorshift128+ style
/// seeded from splitmix64). Deterministic output for a given seed is essential
/// so procedural generation can be asserted in unit tests.
/// </summary>
public sealed class Rng
{
    private ulong _s0;
    private ulong _s1;

    public Rng(ulong seed)
    {
        // splitmix64 to derive the initial state so even seed 0 is well mixed.
        _s0 = SplitMix(ref seed);
        _s1 = SplitMix(ref seed);
        if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
    }

    private static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Raw 64-bit value (xorshift128+).</summary>
    public ulong NextULong()
    {
        ulong s1 = _s0;
        ulong s0 = _s1;
        _s0 = s0;
        s1 ^= s1 << 23;
        _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
        return _s1 + s0;
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (float)NextDouble();

    /// <summary>Uniform float in [min, max).</summary>
    public float Range(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        uint span = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextULong() % span);
    }

    /// <summary>True with the given probability (0..1).</summary>
    public bool Chance(float probability) => NextFloat() < probability;

    /// <summary>+1 or -1.</summary>
    public int Sign() => (NextULong() & 1) == 0 ? 1 : -1;
}
