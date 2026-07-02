using Raylib_cs;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Gfx;

/// <summary>
/// Builds every visual asset procedurally at load time (after the GL context
/// exists): a small variant set of 16x16 bio-mechanical tile textures per tile
/// type, plus a noise nebula backdrop. No image files are ever read.
/// </summary>
public sealed class TextureFactory : IDisposable
{
    private const int TS = GameConfig.TileSize;
    public int SolidVariants { get; } = 4;

    private readonly Dictionary<TileType, Texture2D[]> _tiles = new();
    private readonly List<Texture2D> _owned = new();
    public Texture2D Background { get; private set; }

    public TextureFactory(uint seed)
    {
        _tiles[TileType.Solid] = BuildSet(TileType.Solid, SolidVariants, seed + 1);
        _tiles[TileType.Phase] = BuildSet(TileType.Phase, 2, seed + 2);
        _tiles[TileType.Hazard] = BuildSet(TileType.Hazard, 2, seed + 3);
        _tiles[TileType.OneWay] = BuildSet(TileType.OneWay, 1, seed + 4);
        Background = BuildBackground(GameConfig.VirtualWidth, GameConfig.VirtualHeight, seed + 99);
        _owned.Add(Background);
    }

    public Texture2D Tile(TileType type, int variant)
    {
        if (!_tiles.TryGetValue(type, out var arr) || arr.Length == 0)
            arr = _tiles[TileType.Solid];
        int i = ((variant % arr.Length) + arr.Length) % arr.Length;
        return arr[i];
    }

    private Texture2D[] BuildSet(TileType type, int count, uint seed)
    {
        var arr = new Texture2D[count];
        for (int i = 0; i < count; i++)
        {
            arr[i] = BuildTile(type, seed + (uint)(i * 7919));
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

    private Texture2D BuildTile(TileType type, uint seed)
    {
        var noise = new Noise(seed);
        Image img = Raylib.GenImageColor(TS, TS, new Color(0, 0, 0, 0));
        for (int y = 0; y < TS; y++)
            for (int x = 0; x < TS; x++)
                Raylib.ImageDrawPixel(ref img, x, y, PixelFor(type, x, y, noise, seed));
        return Upload(img);
    }

    private static Color PixelFor(TileType type, int x, int y, Noise noise, uint seed)
    {
        float n = noise.Fractal(x * 0.18f, y * 0.18f, 3);
        bool edge = x == 0 || y == 0 || x == TS - 1 || y == TS - 1;
        bool topLeft = x == 0 || y == 0;

        switch (type)
        {
            case TileType.Solid:
            {
                var baseCol = Palette.Lerp(Palette.MetalDark, Palette.MetalMid, n);
                if (topLeft) baseCol = Palette.MetalLight;                 // bevel highlight
                if (x == TS - 1 || y == TS - 1) baseCol = Palette.MetalDark;
                // circuit vein: a faint line seeded per tile
                int lane = (int)(seed % 12) + 2;
                if ((x == lane || y == (lane % (TS - 2)) + 1) && !edge)
                    baseCol = Palette.Lerp(baseCol, Palette.CircuitDim, 0.8f);
                if (x == lane && y == (lane % (TS - 2)) + 1)
                    baseCol = Palette.Circuit;                              // a glowing node
                return baseCol;
            }
            case TileType.Phase:
            {
                var c = Palette.Lerp(Palette.MetalDark, Palette.Phase, n * 0.8f);
                if (((x + y) % 4) == 0) c = Palette.Lerp(c, Palette.PhaseGlow, 0.5f); // lattice
                if (edge) c = Palette.Lerp(c, Palette.PhaseGlow, 0.3f);
                return new Color((int)c.R, (int)c.G, (int)c.B, 205);       // ghostly
            }
            case TileType.Hazard:
            {
                // glowing energy welling up from the bottom
                float upward = 1f - y / (float)TS;
                float e = upward * (0.5f + 0.6f * n);
                var c = Palette.Lerp(Palette.HazardDim, Palette.Hazard, Math.Clamp(e, 0f, 1f));
                if (y > TS - 3) c = Palette.MetalDark;                      // rooted base
                return c;
            }
            case TileType.OneWay:
            {
                if (y >= 4) return new Color(0, 0, 0, 0);                   // see-through underside
                var c = Palette.Lerp(Palette.MetalMid, Palette.MetalLight, n);
                if (y == 0) c = Palette.Circuit;
                return c;
            }
            default:
                return Palette.MetalMid;
        }
    }

    private Texture2D BuildBackground(int w, int h, uint seed)
    {
        var neb = new Noise(seed);
        var star = new Noise(seed + 5);
        Image img = Raylib.GenImageColor(w, h, Palette.Void);
        for (int y = 0; y < h; y++)
        {
            float vy = y / (float)h;
            for (int x = 0; x < w; x++)
            {
                // deep vertical gradient
                var baseCol = Palette.Lerp(Palette.Void, new Color(12, 16, 30, 255), vy);

                // drifting nebula clouds
                float cloud = neb.Fractal(x * 0.012f, y * 0.012f, 5);
                float tone = neb.Value(x * 0.004f + 10f, y * 0.004f);
                var nebCol = Palette.Lerp(Palette.Nebula1, Palette.Nebula2, tone);
                float density = Math.Clamp((cloud - 0.45f) * 1.7f, 0f, 1f);
                var c = Palette.Lerp(baseCol, nebCol, density * 0.75f);
                // faint inner glow highlight
                c = Palette.Lerp(c, Palette.NebulaGlow, Math.Clamp((cloud - 0.72f) * 2f, 0f, 0.5f));

                // sparse stars
                if (star.Value(x * 1.7f, y * 1.7f) > 0.996f)
                    c = new Color(200, 220, 240, 255);

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
    }
}
