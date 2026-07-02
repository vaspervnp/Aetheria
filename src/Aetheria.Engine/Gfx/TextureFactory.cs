using Raylib_cs;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Gfx;

/// <summary>
/// Builds every visual asset procedurally at load time (after the GL context
/// exists): a full 16x16 tile set AND a nebula backdrop for each biome, so the
/// Rust Vents, Crystal Conduits and Mainframe each render with their own colours
/// and tile shapes. No image files are ever read.
/// </summary>
public sealed class TextureFactory : IDisposable
{
    private const int TS = GameConfig.TileSize;
    public int SolidVariants { get; } = 4;

    private readonly Dictionary<Biome, Dictionary<TileType, Texture2D[]>> _tiles = new();
    private readonly Dictionary<Biome, Texture2D> _backgrounds = new();
    private readonly List<Texture2D> _owned = new();

    public TextureFactory(uint seed)
    {
        foreach (var biome in Biomes.All)
        {
            var style = BiomePalette.For(biome);
            uint bs = seed + (uint)((int)biome + 1) * 1013904223u;
            var set = new Dictionary<TileType, Texture2D[]>
            {
                [TileType.Solid] = BuildSet(style, TileType.Solid, SolidVariants, bs + 1),
                [TileType.Phase] = BuildSet(style, TileType.Phase, 2, bs + 2),
                [TileType.Hazard] = BuildSet(style, TileType.Hazard, 2, bs + 3),
                [TileType.OneWay] = BuildSet(style, TileType.OneWay, 1, bs + 4),
                [TileType.Cracked] = BuildSet(style, TileType.Cracked, 2, bs + 5),
                [TileType.DoorRed] = BuildSet(style, TileType.DoorRed, 1, bs + 6),
                [TileType.DoorBlast] = BuildSet(style, TileType.DoorBlast, 1, bs + 7),
            };
            _tiles[biome] = set;
            var bg = BuildBackground(style, GameConfig.VirtualWidth, GameConfig.VirtualHeight, bs + 99);
            _backgrounds[biome] = bg;
            _owned.Add(bg);
        }
    }

    public Texture2D Tile(Biome biome, TileType type, int variant)
    {
        var set = _tiles[biome];
        if (!set.TryGetValue(type, out var arr) || arr.Length == 0) arr = set[TileType.Solid];
        int i = ((variant % arr.Length) + arr.Length) % arr.Length;
        return arr[i];
    }

    public Texture2D Background(Biome biome) => _backgrounds[biome];

    private Texture2D[] BuildSet(BiomeStyle style, TileType type, int count, uint seed)
    {
        var arr = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = BuildTile(style, type, seed + (uint)(i * 7919));
            _owned.Add(arr[i]);
        }
        return arr;
    }

    private static Texture2D Upload(Image img)
    {
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.UnloadImage(img);
        return tex;
    }

    private Texture2D BuildTile(BiomeStyle style, TileType type, uint seed)
    {
        var noise = new Noise(seed);
        Image img = Raylib.GenImageColor(TS, TS, new Color(0, 0, 0, 0));
        for (int y = 0; y < TS; y++)
            for (int x = 0; x < TS; x++)
                Raylib.ImageDrawPixel(ref img, x, y, PixelFor(style, type, x, y, noise, seed));
        return Upload(img);
    }

    private static Color PixelFor(BiomeStyle s, TileType type, int x, int y, Noise noise, uint seed)
    {
        float n = noise.Fractal(x * 0.18f, y * 0.18f, 3);
        bool edge = x == 0 || y == 0 || x == TS - 1 || y == TS - 1;
        bool topLeft = x == 0 || y == 0;
        bool corner = (x is 2 or 13) && (y is 2 or 13);

        switch (type)
        {
            case TileType.Solid:
                return SolidPixel(s, x, y, n, seed, topLeft, edge, corner);

            case TileType.Cracked:
            {
                var baseCol = SolidPixel(s, x, y, n, seed, topLeft, edge, corner);
                // jagged crack: two branching dark seams
                int cx = 6 + ((int)(seed % 4));
                if (Math.Abs((x) - (cx + (y % 4))) <= 0 || Math.Abs((y) - (cx + (x % 5))) <= 0)
                    baseCol = Palette.Lerp(baseCol, new Color(0, 0, 0, 255), 0.7f);
                return baseCol;
            }
            case TileType.Phase:
            {
                var c = Palette.Lerp(s.Dark, s.Accent, n * 0.7f);
                if (((x + y) % 4) == 0) c = Palette.Lerp(c, s.Light, 0.5f);
                if (edge) c = Palette.Lerp(c, s.Accent, 0.3f);
                return new Color((int)c.R, (int)c.G, (int)c.B, 205);
            }
            case TileType.Hazard:
            {
                float upward = 1f - y / (float)TS;
                float e = upward * (0.5f + 0.6f * n);
                var c = Palette.Lerp(s.HazardDim, s.Hazard, Math.Clamp(e, 0f, 1f));
                if (y > TS - 3) c = s.Dark;
                return c;
            }
            case TileType.OneWay:
            {
                if (y >= 4) return new Color(0, 0, 0, 0);
                var c = Palette.Lerp(s.Mid, s.Light, n);
                if (y == 0) c = s.Accent;
                return c;
            }
            case TileType.DoorRed:
            {
                // red energy field with a metal frame
                if (edge) return s.Light;
                var red = new Color(255, 54, 64, 255);
                var dark = new Color(70, 12, 16, 255);
                bool bar = ((x + (int)(seed % 3)) % 4) < 2;
                return Palette.Lerp(dark, red, bar ? 0.85f : 0.3f + 0.4f * n);
            }
            case TileType.DoorBlast:
            {
                // heavy plated blast door with a central seam and bolts
                var c = Palette.Lerp(s.Mid, s.Light, n * 0.6f + 0.2f);
                if (x == TS / 2 || x == TS / 2 - 1) c = s.Dark;              // vertical seam
                if (corner) c = s.Accent;                                    // bolts
                if (edge) c = s.Dark;
                return c;
            }
            default:
                return s.Mid;
        }
    }

    private static Color SolidPixel(BiomeStyle s, int x, int y, float n, uint seed, bool topLeft, bool edge, bool corner)
    {
        var baseCol = Palette.Lerp(s.Dark, s.Mid, n);
        if (topLeft) baseCol = s.Light;
        if (x == TS - 1 || y == TS - 1) baseCol = s.Dark;

        switch (s.Shape)
        {
            case TileShape.Riveted:
                if (corner) baseCol = s.Accent;                              // rivets
                else if (corner && !edge) baseCol = s.AccentDim;
                break;
            case TileShape.Faceted:
            {
                int diag = (x + y) % 8;
                if (diag < 2) baseCol = Palette.Lerp(baseCol, s.Light, 0.5f);
                if (diag == 4) baseCol = Palette.Lerp(baseCol, s.Accent, 0.35f);
                break;
            }
            case TileShape.Circuit:
            {
                if ((x % 5 == 2 || y % 5 == 2) && !edge) baseCol = Palette.Lerp(baseCol, s.AccentDim, 0.8f);
                if (x % 5 == 2 && y % 5 == 2) baseCol = s.Accent;            // node
                break;
            }
        }
        return baseCol;
    }

    private Texture2D BuildBackground(BiomeStyle s, int w, int h, uint seed)
    {
        var neb = new Noise(seed);
        var star = new Noise(seed + 5);
        Image img = Raylib.GenImageColor(w, h, s.Bg0);
        for (int y = 0; y < h; y++)
        {
            float vy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                var baseCol = Palette.Lerp(s.Bg0, s.Bg1, vy * 0.7f);
                float cloud = neb.Fractal(x * 0.012f, y * 0.012f, 5);
                float density = Math.Clamp((cloud - 0.45f) * 1.7f, 0f, 1f);
                var c = Palette.Lerp(baseCol, s.Bg1, density * 0.8f);
                c = Palette.Lerp(c, s.BgGlow, Math.Clamp((cloud - 0.72f) * 2f, 0f, 0.5f));
                if (star.Value(x * 1.7f, y * 1.7f) > 0.996f)
                    c = new Color(210, 225, 240, 255);
                Raylib.ImageDrawPixel(ref img, x, y, c);
            }
        }
        return Upload(img);
    }

    public void Dispose()
    {
        foreach (var t in _owned) Raylib.UnloadTexture(t);
        _owned.Clear();
        _tiles.Clear();
        _backgrounds.Clear();
    }
}
