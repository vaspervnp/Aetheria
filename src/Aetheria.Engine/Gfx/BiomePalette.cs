using Raylib_cs;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Gfx;

public enum TileShape { Riveted, Faceted, Circuit }

/// <summary>The colour + shape language for one biome's procedural tiles/backdrop.</summary>
public readonly struct BiomeStyle
{
    public required Color Dark { get; init; }
    public required Color Mid { get; init; }
    public required Color Light { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentDim { get; init; }
    public required Color Hazard { get; init; }
    public required Color HazardDim { get; init; }
    public required Color Bg0 { get; init; }
    public required Color Bg1 { get; init; }
    public required Color BgGlow { get; init; }
    public required TileShape Shape { get; init; }
}

public static class BiomePalette
{
    private static Color C(int r, int g, int b) => new(r, g, b, 255);

    public static BiomeStyle For(Biome b) => b switch
    {
        Biome.RustVents => new BiomeStyle
        {
            Dark = C(30, 20, 16), Mid = C(74, 44, 30), Light = C(122, 76, 46),
            Accent = C(255, 150, 60), AccentDim = C(120, 62, 24),
            Hazard = C(255, 96, 40), HazardDim = C(140, 44, 20),
            Bg0 = C(18, 10, 8), Bg1 = C(58, 26, 16), BgGlow = C(150, 66, 26),
            Shape = TileShape.Riveted,
        },
        Biome.CrystalConduits => new BiomeStyle
        {
            Dark = C(16, 28, 34), Mid = C(30, 68, 74), Light = C(72, 152, 150),
            Accent = C(120, 255, 235), AccentDim = C(40, 120, 120),
            Hazard = C(255, 80, 150), HazardDim = C(120, 30, 80),
            Bg0 = C(8, 18, 24), Bg1 = C(20, 52, 62), BgGlow = C(44, 130, 128),
            Shape = TileShape.Faceted,
        },
        _ => new BiomeStyle
        {
            Dark = C(18, 20, 30), Mid = C(36, 42, 60), Light = C(72, 86, 124),
            Accent = C(120, 255, 160), AccentDim = C(40, 100, 72),
            Hazard = C(255, 72, 92), HazardDim = C(120, 30, 40),
            Bg0 = C(8, 10, 18), Bg1 = C(20, 26, 44), BgGlow = C(44, 84, 116),
            Shape = TileShape.Circuit,
        },
    };
}
