using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.World;

public enum TileType : byte
{
    Empty = 0,
    Solid = 1,       // fully solid collision
    Phase = 2,       // solid unless the player is phasing
    Hazard = 3,      // non-solid but damages on contact
    OneWay = 4,      // solid only from above (jump-through platform)
    Cracked = 5,     // solid, but shattered by the Scatter-Shot
    DoorRed = 6,     // solid locked door (Red Energy) until its flag is set
    DoorBlast = 7,   // solid heavy blast door until its flag is set
}

/// <summary>
/// A dense grid of tiles for one room. Provides the collision queries the
/// physics resolver needs, plus world &lt;-&gt; tile coordinate conversion. Out of
/// bounds is treated as solid so entities can never leave the world except
/// through an explicit room transition.
/// </summary>
public sealed class TileMap
{
    public int Width { get; }
    public int Height { get; }
    public int TileSize { get; }

    private readonly TileType[] _tiles;

    public TileMap(int width, int height, int tileSize = GameConfig.TileSize)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        TileSize = tileSize;
        _tiles = new TileType[Width * Height];
    }

    public float PixelWidth => Width * TileSize;
    public float PixelHeight => Height * TileSize;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public TileType Get(int x, int y)
        => InBounds(x, y) ? _tiles[y * Width + x] : TileType.Solid;

    public void Set(int x, int y, TileType t)
    {
        if (InBounds(x, y)) _tiles[y * Width + x] = t;
    }

    /// <summary>Fill a rectangle (inclusive) with a tile type, clipped to bounds.</summary>
    public void Fill(int x0, int y0, int x1, int y1, TileType t)
    {
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
        x1 = Math.Min(Width - 1, x1); y1 = Math.Min(Height - 1, y1);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _tiles[y * Width + x] = t;
    }

    public void Border(TileType t)
    {
        Fill(0, 0, Width - 1, 0, t);
        Fill(0, Height - 1, Width - 1, Height - 1, t);
        Fill(0, 0, 0, Height - 1, t);
        Fill(Width - 1, 0, Width - 1, Height - 1, t);
    }

    // ---- coordinate conversion ---------------------------------------------
    public int WorldToTileX(float wx) => (int)MathF.Floor(wx / TileSize);
    public int WorldToTileY(float wy) => (int)MathF.Floor(wy / TileSize);
    public float TileToWorldX(int tx) => tx * TileSize;
    public float TileToWorldY(int ty) => ty * TileSize;

    public Aabb TileAabb(int tx, int ty)
        => new(tx * TileSize, ty * TileSize, TileSize, TileSize);

    // ---- collision queries --------------------------------------------------

    /// <summary>Is this tile solid to a body? Phase tiles are passable while phasing.</summary>
    public bool IsSolidTile(int x, int y, bool phasing = false)
    {
        TileType t = Get(x, y);
        return t switch
        {
            TileType.Solid => true,
            TileType.Cracked => true,
            TileType.DoorRed => true,
            TileType.DoorBlast => true,
            TileType.Phase => !phasing,
            TileType.OneWay => false, // handled specially by the resolver
            _ => false,
        };
    }

    public bool IsOneWay(int x, int y) => Get(x, y) == TileType.OneWay;
    public bool IsHazardTile(int x, int y) => Get(x, y) == TileType.Hazard;

    /// <summary>Does the box overlap any fully-solid tile? (ignores one-way).</summary>
    public bool OverlapsSolid(in Aabb box, bool phasing = false)
    {
        int minX = WorldToTileX(box.Left);
        int maxX = WorldToTileX(box.Right - 0.001f);
        int minY = WorldToTileY(box.Top);
        int maxY = WorldToTileY(box.Bottom - 0.001f);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (IsSolidTile(x, y, phasing))
                    return true;
        return false;
    }

    /// <summary>Does the box overlap a hazard tile?</summary>
    public bool OverlapsHazard(in Aabb box)
    {
        int minX = WorldToTileX(box.Left);
        int maxX = WorldToTileX(box.Right - 0.001f);
        int minY = WorldToTileY(box.Top);
        int maxY = WorldToTileY(box.Bottom - 0.001f);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                if (IsHazardTile(x, y))
                    return true;
        return false;
    }

    /// <summary>
    /// One-way platform support test: returns true if the box is resting on the
    /// top edge of a one-way tile while descending (used by the vertical
    /// resolver). <paramref name="prevBottom"/> is the box bottom before the move.
    /// </summary>
    public bool RestsOnOneWay(in Aabb box, float prevBottom)
    {
        int minX = WorldToTileX(box.Left);
        int maxX = WorldToTileX(box.Right - 0.001f);
        int footTile = WorldToTileY(box.Bottom - 0.001f);
        for (int x = minX; x <= maxX; x++)
        {
            if (!IsOneWay(x, footTile)) continue;
            float tileTop = footTile * TileSize;
            // Only collide if we were above the platform on the previous frame.
            if (prevBottom <= tileTop + 0.5f && box.Bottom >= tileTop)
                return true;
        }
        return false;
    }

    /// <summary>Count of non-empty tiles (used by generation invariants/tests).</summary>
    public int SolidCount()
    {
        int c = 0;
        for (int i = 0; i < _tiles.Length; i++)
            if (_tiles[i] != TileType.Empty) c++;
        return c;
    }
}
