namespace Aetheria.Engine.World;

/// <summary>
/// A conservative platformer flood-fill over a room's <b>standable</b> tiles
/// (empty tile with solid/one-way directly below). Two standable tiles connect
/// if reachable by a bounded jump/fall/walk. Used to prove every door approach
/// in a generated room is reachable from the room's floor — i.e. the interior
/// is traversable — regardless of which door combination it has.
/// </summary>
public static class TileReachability
{
    public const int JumpUp = 4;     // tiles of rise per jump
    public const int JumpAcross = 5; // horizontal tiles per hop

    public static bool IsStandable(TileMap map, int x, int y)
    {
        if (!map.InBounds(x, y)) return false;
        if (map.Get(x, y) != TileType.Empty) return false;
        var below = map.Get(x, y + 1);
        return below == TileType.Solid || below == TileType.OneWay
            || below == TileType.Cracked || below == TileType.DoorRed || below == TileType.DoorBlast
            || !map.InBounds(x, y + 1); // floor at bottom edge
    }

    public static HashSet<(int x, int y)> ReachableFrom(TileMap map, (int x, int y) start)
    {
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        if (IsStandable(map, start.x, start.y)) { seen.Add(start); stack.Push(start); }

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            for (int dx = -JumpAcross; dx <= JumpAcross; dx++)
            {
                for (int dy = -JumpUp; dy <= JumpUp + 6; dy++)  // allow deep falls
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || seen.Contains((nx, ny))) continue;
                    if (!IsStandable(map, nx, ny)) continue;
                    int up = y - ny;                    // >0 => target higher
                    if (up > 0 && (up > JumpUp || Math.Abs(dx) > JumpAcross)) continue;
                    if (up <= 0 && Math.Abs(dx) > JumpAcross) continue;
                    seen.Add((nx, ny));
                    stack.Push((nx, ny));
                }
            }
        }
        return seen;
    }

    /// <summary>The standable tile just inside a door on the given edge.</summary>
    public static (int x, int y) ApproachTile(Direction edge) => edge switch
    {
        Direction.West => (2, Doorways.FloorRow - 1),
        Direction.East => (Doorways.RoomW - 3, Doorways.FloorRow - 1),
        Direction.North => (Doorways.NsDoorCol + 1, 2),                 // atop the climb platform
        _ => (Doorways.NsDoorCol - 1, Doorways.FloorRow - 1),           // floor beside the south gap
    };

    /// <summary>The first standable tile along the floor row (robust to hazard vents).</summary>
    public static (int x, int y) FloorSeed(TileMap map)
    {
        int y = Doorways.FloorRow - 1;
        for (int x = 1; x < map.Width - 1; x++)
            if (IsStandable(map, x, y)) return (x, y);
        return (5, y);
    }

    /// <summary>Are all of a room's door approaches reachable from its floor?</summary>
    public static bool AllDoorsReachable(Room room)
    {
        var reach = ReachableFrom(room.Map, FloorSeed(room.Map));
        foreach (var door in room.Doors)
            if (!reach.Contains(ApproachTile(door.Edge)))
                return false;
        return true;
    }
}
