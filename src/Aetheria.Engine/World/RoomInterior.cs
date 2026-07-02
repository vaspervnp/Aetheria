using Aetheria.Engine.Core;

namespace Aetheria.Engine.World;

/// <summary>
/// Builds the collision interior of a fixed-size screen for a given set of door
/// edges. Routes are guaranteed traversable by construction: E/W doors sit at
/// floor level, North doors are reached by a base-jumpable platform ladder, and
/// South doors are a gap in the floor. All routes anchor to the floor, so every
/// door is reachable from every other door.
/// </summary>
public static class RoomInterior
{
    private const int W = Doorways.RoomW;
    private const int H = Doorways.RoomH;
    private const int Floor = Doorways.FloorRow;
    private const int Col = Doorways.NsDoorCol;
    private const int Len = Doorways.DoorLen;

    public static TileMap Build(IReadOnlyCollection<Direction> doors, Biome biome, uint seed)
    {
        var map = new TileMap(W, H, GameConfig.TileSize);
        map.Border(TileType.Solid);
        map.Fill(0, Floor, W - 1, H - 1, TileType.Solid);   // solid floor + base

        bool north = doors.Contains(Direction.North);
        bool south = doors.Contains(Direction.South);
        bool east = doors.Contains(Direction.East);
        bool west = doors.Contains(Direction.West);

        // carve every door opening (World fills locked ones with a barrier later)
        foreach (var d in doors)
            foreach (var (x, y) in Doorways.OpeningTiles(d))
                map.Set(x, y, TileType.Empty);

        // E/W approach pockets so the entry ledge is clear
        if (west) map.Fill(1, Doorways.EwDoorRow, 3, Floor - 1, TileType.Empty);
        if (east) map.Fill(W - 4, Doorways.EwDoorRow, W - 2, Floor - 1, TileType.Empty);

        if (south) BuildSouthGap(map);
        if (north) BuildNorthClimb(map, seed);

        DecorateInterior(map, biome, seed, north, south);
        return map;
    }

    private static void BuildSouthGap(TileMap map)
    {
        // open the floor beneath the south opening so Spark can drop/rise through
        map.Fill(Col, Floor, Col + Len - 1, H - 1, TileType.Empty);
    }

    private static void BuildNorthClimb(TileMap map, uint seed)
    {
        // a platform directly under the top opening…
        map.Fill(Col - 1, 3, Col + Len, 3, TileType.Solid);
        // …then a zig-zag ladder of platforms down to the floor (each step ≤3 up / ≤4 across)
        int climbCol = Doorways.NorthClimbCol;
        int side = 1;
        for (int row = 6; row <= Floor - 1; row += 3)
        {
            int cx = climbCol + side * 2;
            map.Fill(Math.Max(1, cx - 1), row, Math.Min(W - 2, cx + 1), row, TileType.Solid);
            side = -side;
        }
    }

    private static void DecorateInterior(TileMap map, Biome biome, uint seed, bool north, bool south)
    {
        var rng = new Rng(seed ^ 0xBEEF);
        // a couple of floating ledges for interest, kept clear of the N/S corridor
        int ledges = rng.Range(1, 4);
        for (int i = 0; i < ledges; i++)
        {
            int len = rng.Range(3, 6);
            int y = rng.Range(8, Floor - 2);
            int x = rng.Range(3, W - 4 - len);
            if (Overlaps(x, x + len, Col - 2, Col + Len + 1) && (north || south)) continue; // don't block the shaft
            map.Fill(x, y, x + len, y, TileType.OneWay);
        }

        // Rust Vents get an occasional hazard vent on the floor, kept to a safe
        // band well clear of the spawn column, all door approaches and the S gap,
        // and narrow enough to hop across.
        if (biome == Biome.RustVents && rng.Chance(0.5f))
        {
            int hx = rng.Range(24, 33);
            map.Fill(hx, Floor, hx + 1, Floor, TileType.Hazard);
        }
    }

    private static bool Overlaps(int a0, int a1, int b0, int b1) => a0 <= b1 && a1 >= b0;

    /// <summary>A safe standing column on the floor for spawning pickups/enemies (avoids door gaps).</summary>
    public static int SafeFloorColumn(Rng rng, bool south)
    {
        for (int tries = 0; tries < 12; tries++)
        {
            int c = rng.Range(4, W - 5);
            if (south && Overlaps(c, c, Col - 1, Col + Len)) continue;
            return c;
        }
        return 5;
    }
}
