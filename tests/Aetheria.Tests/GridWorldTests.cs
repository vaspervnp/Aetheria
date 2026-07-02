using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

/// <summary>Proves 4-directional transitions on a hand-built 5-room cross.</summary>
public class GridTransitionTests
{
    private const float Dt = 1f / 60f;

    private static void TransitionsInDirection(Direction dir)
    {
        var world = MapGenerator.Cross();
        var p = new Player(world.StartSpawn);
        p.PlaceAt(world.StartSpawn);
        var startCell = world.CurrentCell;

        // stand the player inside the door's trigger zone
        var tz = Doorways.TriggerZone(dir);
        p.PlaceAt(new Vector2(tz.CenterX, tz.CenterY));
        world.Update(Dt, p);

        var (dx, dy) = Doorways.Delta(dir);
        var expected = new GridPoint(startCell.X + dx, startCell.Y + dy);
        Assert.True(world.JustTransitioned, $"no transition {dir}");
        Assert.Equal(expected, world.CurrentCell);

        // arrived at the opposite edge, clear of that edge's trigger (won't bounce back)
        var backTz = Doorways.TriggerZone(Doorways.Opposite(dir));
        Assert.False(p.Bounds.Intersects(backTz));
    }

    [Fact] public void North() => TransitionsInDirection(Direction.North);
    [Fact] public void South() => TransitionsInDirection(Direction.South);
    [Fact] public void East() => TransitionsInDirection(Direction.East);
    [Fact] public void West() => TransitionsInDirection(Direction.West);

    [Fact]
    public void LockedDoorBlocksTransitionUntilFlagSet()
    {
        // build a 2-room world with a Blast door between them
        var a = new GridPoint(0, 0);
        var b = new GridPoint(1, 0);
        var mapA = RoomInterior.Build(new HashSet<Direction> { Direction.East }, Biome.RustVents, 1);
        var mapB = RoomInterior.Build(new HashSet<Direction> { Direction.West }, Biome.RustVents, 2);
        var roomA = new Room { GridX = 0, GridY = 0, Biome = Biome.RustVents, Map = mapA, Seed = 1,
            DefaultSpawn = new Vector2(5.5f * GameConfig.TileSize, (Doorways.FloorRow - 2) * GameConfig.TileSize) };
        var roomB = new Room { GridX = 1, GridY = 0, Biome = Biome.RustVents, Map = mapB, Seed = 2 };
        roomA.Doors.Add(new Door { Edge = Direction.East, Kind = DoorKind.Blast, Flag = "gate" });
        roomB.Doors.Add(new Door { Edge = Direction.West, Kind = DoorKind.Blast, Flag = "gate" });
        var world = new World(new[] { roomA, roomB }, a);
        var p = new Player(world.StartSpawn);

        var tz = Doorways.TriggerZone(Direction.East);
        p.PlaceAt(new Vector2(tz.CenterX, tz.CenterY));
        world.Update(Dt, p);
        Assert.Equal(a, world.CurrentCell);   // still locked

        world.Flags.SetFlag("gate");
        p.PlaceAt(new Vector2(tz.CenterX, tz.CenterY));
        world.Update(Dt, p);
        Assert.Equal(b, world.CurrentCell);   // opened
    }
}

/// <summary>Invariants for the large procedural map.</summary>
public class MapGeneratorTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(20240601u)]
    [InlineData(777u)]
    public void GeneratesEnoughConnectedRoomsAcrossThreeBiomes(uint seed)
    {
        var world = MapGenerator.Generate(seed);
        Assert.True(world.Rooms.Count >= 60, $"only {world.Rooms.Count} rooms");

        // connectivity: BFS over doors reaches every room
        var start = world.StartCell;
        var seen = new HashSet<GridPoint> { start };
        var q = new Queue<GridPoint>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            var room = world.Rooms[c];
            foreach (var d in room.Doors)
            {
                var (dx, dy) = Doorways.Delta(d.Edge);
                var n = new GridPoint(c.X + dx, c.Y + dy);
                if (world.Rooms.ContainsKey(n) && seen.Add(n)) q.Enqueue(n);
            }
        }
        Assert.Equal(world.Rooms.Count, seen.Count);

        // three biomes present
        var biomes = world.Rooms.Values.Select(r => r.Biome).Distinct().ToList();
        Assert.Contains(Biome.RustVents, biomes);
        Assert.Contains(Biome.CrystalConduits, biomes);
        Assert.Contains(Biome.Mainframe, biomes);

        // exactly one Core, and it hosts the Warden
        var cores = world.Rooms.Values.Where(r => r.IsCore).ToList();
        Assert.Single(cores);
        Assert.Contains(cores[0].Enemies, e => e.Kind == EnemyKind.Warden);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(20240601u)]
    [InlineData(777u)]
    public void EveryRoomInteriorConnectsAllItsDoors(uint seed)
    {
        var world = MapGenerator.Generate(seed);
        foreach (var room in world.Rooms.Values)
            Assert.True(TileReachability.AllDoorsReachable(room),
                $"unreachable door in room ({room.GridX},{room.GridY}) {room.Biome}");
    }

    [Fact]
    public void DoorsAreSymmetricBetweenNeighbours()
    {
        var world = MapGenerator.Generate(20240601u);
        foreach (var room in world.Rooms.Values)
            foreach (var d in room.Doors)
            {
                var (dx, dy) = Doorways.Delta(d.Edge);
                var n = new GridPoint(room.GridX + dx, room.GridY + dy);
                Assert.True(world.Rooms.ContainsKey(n), "door leads nowhere");
                Assert.True(world.Rooms[n].HasDoor(Doorways.Opposite(d.Edge)), "neighbour has no matching door");
            }
    }

    [Fact]
    public void ProgressionAbilitiesArePlaced()
    {
        var world = MapGenerator.Generate(20240601u);
        var placed = world.Rooms.Values.SelectMany(r => r.Pickups).Select(p => p.Type).Distinct().ToList();
        Assert.Contains(Aetheria.Engine.Abilities.AbilityType.DoubleJump, placed);
        Assert.Contains(Aetheria.Engine.Abilities.AbilityType.Dash, placed);
        Assert.Contains(Aetheria.Engine.Abilities.AbilityType.Phase, placed);
    }
}
