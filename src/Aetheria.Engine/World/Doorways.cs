using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.World;

/// <summary>Cardinal directions. North = up (gy-1), South = down (gy+1).</summary>
public enum Direction { North, East, South, West }

/// <summary>
/// Fixed-screen dimensions and standardized door-band geometry. Because every
/// screen is the same size and doors sit at the same band on each edge, a room's
/// East door automatically lines up with its East-neighbour's West door — so
/// transitions and player placement are computed, never hand-wired.
/// </summary>
public static class Doorways
{
    private const int T = GameConfig.TileSize;

    public const int RoomW = 40;      // tiles
    public const int RoomH = 22;      // tiles
    public const int FloorRow = 19;   // solid floor rows 19..21
    public const int DoorLen = 3;     // door opening length (tiles)

    // door bands (top-left of the 3-tile opening)
    public const int EwDoorRow = FloorRow - 3;      // E/W opening rows 16..18 (walk in at floor level)
    public const int NsDoorCol = RoomW / 2 - 2;     // N/S opening cols 18..20

    public static float PixelW => RoomW * T;
    public static float PixelH => RoomH * T;

    public static Direction Opposite(Direction d) => d switch
    {
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        Direction.East => Direction.West,
        _ => Direction.East,
    };

    public static (int dx, int dy) Delta(Direction d) => d switch
    {
        Direction.North => (0, -1),
        Direction.South => (0, 1),
        Direction.East => (1, 0),
        _ => (-1, 0),
    };

    /// <summary>The tile column/row of the door opening's start on the given edge.</summary>
    public static IEnumerable<(int x, int y)> OpeningTiles(Direction d)
    {
        switch (d)
        {
            case Direction.West:
                for (int r = 0; r < DoorLen; r++) yield return (0, EwDoorRow + r);
                break;
            case Direction.East:
                for (int r = 0; r < DoorLen; r++) yield return (RoomW - 1, EwDoorRow + r);
                break;
            case Direction.North:
                for (int c = 0; c < DoorLen; c++) yield return (NsDoorCol + c, 0);
                break;
            case Direction.South:
                for (int c = 0; c < DoorLen; c++) yield return (NsDoorCol + c, RoomH - 1);
                break;
        }
    }

    /// <summary>World-space zone whose overlap with the player fires the transition.</summary>
    public static Aabb TriggerZone(Direction d) => d switch
    {
        Direction.West => new Aabb(-0.5f * T, EwDoorRow * T, 2f * T, DoorLen * T),
        Direction.East => new Aabb((RoomW - 1.5f) * T, EwDoorRow * T, 2f * T, DoorLen * T),
        Direction.North => new Aabb(NsDoorCol * T, -0.5f * T, DoorLen * T, 2f * T),
        _ => new Aabb(NsDoorCol * T, (RoomH - 1.5f) * T, DoorLen * T, 2f * T),
    };

    /// <summary>Player-centre placement when ARRIVING through the door on this edge.</summary>
    public static Vector2 EntryPosition(Direction arriveEdge) => arriveEdge switch
    {
        Direction.West => new Vector2(3.5f * T, (FloorRow - 2) * T),
        Direction.East => new Vector2((RoomW - 3.5f) * T, (FloorRow - 2) * T),
        Direction.North => new Vector2((NsDoorCol + 1.5f) * T, 2.5f * T),          // fell in from above
        _ => new Vector2((NsDoorCol - 1.5f) * T, (FloorRow - 2) * T),             // came up from below
    };

    /// <summary>The tile column where a vertical climb route to the North door should sit.</summary>
    public static int NorthClimbCol => NsDoorCol + 1;
    public static int SouthGapColStart => NsDoorCol;
}
