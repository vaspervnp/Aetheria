namespace Aetheria.Engine.Maths;

/// <summary>
/// Deterministic 2D value noise with fractal (fBm) accumulation. Seeded and
/// hash-based so it needs no allocation table and produces identical output for
/// a given seed on every platform — important for reproducible procedural art
/// and for testable bounds.
/// </summary>
public sealed class Noise
{
    private readonly uint _seed;

    public Noise(uint seed) => _seed = seed == 0 ? 0x1D2C3B4Au : seed;

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Hash an integer lattice point to a float in [0,1).</summary>
    private float Hash(int x, int y)
    {
        unchecked
        {
            uint h = _seed;
            h ^= (uint)x * 0x9E3779B1u;
            h ^= (uint)y * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000; // 24-bit mantissa -> [0,1)
        }
    }

    /// <summary>Value noise sampled at (x, y). Output in [0, 1].</summary>
    public float Value(float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float fx = x - x0;
        float fy = y - y0;

        float v00 = Hash(x0, y0);
        float v10 = Hash(x0 + 1, y0);
        float v01 = Hash(x0, y0 + 1);
        float v11 = Hash(x0 + 1, y0 + 1);

        float ux = Fade(fx);
        float uy = Fade(fy);
        return Lerp(Lerp(v00, v10, ux), Lerp(v01, v11, ux), uy);
    }

    /// <summary>
    /// Fractal Brownian motion: sums <paramref name="octaves"/> layers of value
    /// noise at increasing frequency and decreasing amplitude. Normalized to
    /// [0, 1].
    /// </summary>
    public float Fractal(float x, float y, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
    {
        if (octaves < 1) octaves = 1;
        float sum = 0f;
        float amp = 1f;
        float freq = 1f;
        float norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * Value(x * freq, y * freq);
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    /// <summary>Signed fractal noise in [-1, 1].</summary>
    public float FractalSigned(float x, float y, int octaves = 4)
        => Fractal(x, y, octaves) * 2f - 1f;
}
