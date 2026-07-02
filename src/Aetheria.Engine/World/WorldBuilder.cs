using Aetheria.Engine.Abilities;

namespace Aetheria.Engine.World;

/// <summary>
/// Assembles the whole Abyss: six interconnected rooms forming an ability-gated
/// progression (Double Jump → Dash → Wall-Climb → Phase → Core). Collision
/// geometry is authored for guaranteed traversability and validated by the
/// reachability test-suite; visual detail is layered on procedurally at render
/// time from each room's <see cref="Room.Seed"/>.
/// </summary>
public static class WorldBuilder
{
    public const int StartRoomId = 0;
    public const int CoreRoomId = 6;

    public static World Build(uint seed = 1337)
    {
        var rooms = new[]
        {
            DormantConduit(seed + 0),
            FloodedGallery(seed + 1),
            FracturedAscent(seed + 2),
            ConduitShafts(seed + 3),
            PhaseVault(seed + 4),
            SunderedSpan(seed + 5),
            TheCore(seed + 6),
        };
        return new World(rooms, StartRoomId);
    }

    // Room 0 — gentle intro, flat floor, walk east.
    private static Room DormantConduit(uint seed)
    {
        var b = new RoomBuilder(0, "Dormant Conduit", 40, 26, seed, 22);
        b.Spawn(4, 22);
        b.EastDoor(19, 1);
        b.Platform(10, 13, 17);
        b.Platform(22, 26, 15);
        b.Enemy(16, 21, EnemyKind.Crawler, 5);
        b.Enemy(28, 21, EnemyKind.Crawler, 6);
        b.Walk(36, 22);
        return b.Build();
    }

    // Room 1 — Double Jump. Grab it low, then double-jump onto raised ground.
    private static Room FloodedGallery(uint seed)
    {
        var b = new RoomBuilder(1, "Flooded Gallery", 48, 26, seed, 22);
        b.WestDoor(19, 0);
        b.Spawn(3, 22);
        b.Platform(6, 9, 19);
        b.Pickup(7, 19, AbilityType.DoubleJump);
        b.Block(31, 17, 47, 21);            // raised east ground, top at row 17
        b.EastDoor(14, 2);                  // door sits on the high ground
        b.Enemy(18, 21, EnemyKind.Crawler, 5);
        b.Enemy(24, 21, EnemyKind.Crawler, 6);
        b.Platform(14, 17, 15);
        // critical path
        b.Waypoint(7, 19);                  // hop up for the ability
        b.Waypoint(7, 22);                  // drop back
        b.Walk(30, 22);                     // to the foot of the ledge
        b.Waypoint(32, 17, AbilityType.DoubleJump); // double-jump up (5 tiles)
        b.Walk(44, 17);                     // to the east door
        return b.Build();
    }

    // Room 2 — Dash. Grab it, then dash a wide hazard gap.
    private static Room FracturedAscent(uint seed)
    {
        var b = new RoomBuilder(2, "Fractured Ascent", 52, 26, seed, 22);
        b.WestDoor(19, 1);
        b.Spawn(3, 22);
        b.Platform(7, 10, 19);
        b.Pickup(8, 19, AbilityType.Dash);
        b.Pit(30, 37, 3);                   // 8-wide chasm with a hazard floor
        b.Block(31, 16, 36, 17);            // low ceiling: no big jump arc over the gap
        b.EastDoor(19, 3);
        b.Enemy(20, 21, EnemyKind.Crawler, 5);
        b.Enemy(45, 21, EnemyKind.Crawler, 5);
        b.Enemy(34, 11, EnemyKind.Floater, 4);
        // critical path
        b.Waypoint(8, 19);
        b.Waypoint(8, 22);
        b.Walk(29, 22);                     // west lip of the chasm
        b.Waypoint(38, 22, AbilityType.Dash); // dash across (9 tiles)
        b.Walk(50, 22);
        return b.Build();
    }

    // Room 3 — Wall-Climb. Grab it, then scale a sheer cliff to the high door.
    private static Room ConduitShafts(uint seed)
    {
        var b = new RoomBuilder(3, "Conduit Shafts", 44, 26, seed, 22);
        b.WestDoor(19, 2);
        b.Spawn(3, 22);
        b.Platform(7, 10, 19);
        b.Pickup(8, 19, AbilityType.WallClimb);
        b.Block(30, 13, 43, 21);            // sheer cliff mass, top at row 13
        b.EastDoor(10, 4);                  // door on the cliff top
        b.Enemy(18, 21, EnemyKind.Crawler, 5);
        b.Enemy(24, 21, EnemyKind.Crawler, 6);
        b.Enemy(37, 12, EnemyKind.Sentinel, 5);
        // critical path
        b.Waypoint(8, 19);
        b.Waypoint(8, 22);
        b.Walk(28, 22);                     // foot of the cliff
        b.Waypoint(35, 13, AbilityType.WallClimb); // climb (9 tiles)
        b.Walk(40, 13);
        return b.Build();
    }

    // Room 4 — Phase. Grab it, then phase through the sealed matter wall.
    private static Room PhaseVault(uint seed)
    {
        var b = new RoomBuilder(4, "Phase Vault", 44, 26, seed, 22);
        b.WestDoor(19, 3);
        b.Spawn(3, 22);
        b.Platform(7, 10, 19);
        b.Pickup(8, 19, AbilityType.Phase);
        b.Block(28, 1, 29, 21, TileType.Phase);  // full-height phase seal
        b.EastDoor(19, 5);
        b.Enemy(16, 21, EnemyKind.Crawler, 5);
        b.Enemy(36, 21, EnemyKind.Sentinel, 6);
        // critical path
        b.Waypoint(8, 19);
        b.Waypoint(8, 22);
        b.Walk(27, 22);
        b.Waypoint(31, 22, AbilityType.Phase);   // slip through the seal
        b.Walk(40, 22);
        return b.Build();
    }

    // Room 5 — Sundered Span. A combined-ability gauntlet (no new gate) guarded
    // by the new charger pattern; the additional zone before the Core.
    private static Room SunderedSpan(uint seed)
    {
        var b = new RoomBuilder(5, "Sundered Span", 52, 26, seed, 22);
        b.WestDoor(19, 4);
        b.Spawn(3, 22);
        b.Pit(12, 16, 3);                    // a chasm to hop/dash
        b.Block(30, 17, 51, 21);             // raised east ground (double-jump up)
        b.EastDoor(14, 6);                   // door on the high ground
        b.Enemy(8, 21, EnemyKind.Charger, 5);
        b.Enemy(24, 21, EnemyKind.Charger, 6);
        b.Enemy(20, 12, EnemyKind.Floater, 4);
        // critical path (all abilities owned by now)
        b.Walk(11, 22);                      // to the west lip of the chasm
        b.Waypoint(17, 22);                  // cross it
        b.Walk(29, 22);                      // to the foot of the raised ground
        b.Waypoint(31, 17);                  // double-jump up (5 tiles)
        b.Walk(44, 17);                      // to the east door
        return b.Build();
    }

    // Room 6 — The Core, guarded by the Warden boss. Defeat it, then reach the
    // Core to win the game.
    private static Room TheCore(uint seed)
    {
        var b = new RoomBuilder(6, "The Core", 46, 26, seed, 22, isCore: true);
        b.WestDoor(19, 5);
        b.Spawn(3, 22);
        b.Platform(30, 39, 20);              // pedestal
        b.Core(34, 19);
        b.Enemy(32, 9, EnemyKind.Warden, 12);   // the boss
        b.Enemy(14, 21, EnemyKind.Charger, 6);
        b.Walk(28, 22);                      // walk to the pedestal
        b.Waypoint(34, 20);                  // step up onto it
        return b.Build();
    }
}
