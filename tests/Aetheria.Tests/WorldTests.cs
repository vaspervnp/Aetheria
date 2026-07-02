using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class WorldTests
{
    private const float Dt = 1f / 60f;

    private static void AssertStandable(TileMap m, GridPoint p, string ctx)
    {
        Assert.True(m.IsSolidTile(p.X, p.Y), $"{ctx}: no floor under waypoint {p.X},{p.Y}");
        Assert.False(m.IsSolidTile(p.X, p.Y - 1), $"{ctx}: no headroom at waypoint {p.X},{p.Y}");
    }

    [Fact]
    public void WorldHasSixConnectedRooms()
    {
        var w = WorldBuilder.Build();
        Assert.Equal(6, w.Rooms.Count);
        for (int i = 0; i < 6; i++) Assert.True(w.Rooms.ContainsKey(i));
        Assert.True(w.Rooms[WorldBuilder.CoreRoomId].IsCore);
        Assert.NotNull(w.Rooms[WorldBuilder.CoreRoomId].CoreCenter);
        Assert.Equal(WorldBuilder.StartRoomId, w.CurrentRoomId);
    }

    [Fact]
    public void DoorsWireUpBidirectionally()
    {
        var w = WorldBuilder.Build();
        for (int i = 0; i < 5; i++)
        {
            var east = w.Rooms[i].DoorOn(Direction.East);
            Assert.NotNull(east);
            Assert.Equal(i + 1, east!.TargetRoomId);
            Assert.Equal(Direction.West, east.TargetEdge);

            var west = w.Rooms[i + 1].DoorOn(Direction.West);
            Assert.NotNull(west);
            Assert.Equal(i, west!.TargetRoomId);
            Assert.Equal(Direction.East, west.TargetEdge);
        }
    }

    [Fact]
    public void EveryCriticalPathWaypointIsStandableAndWalksAreFloored()
    {
        var w = WorldBuilder.Build();
        foreach (var room in w.Rooms.Values)
        {
            foreach (var edge in room.CriticalPath)
            {
                if (edge.Walk)
                {
                    Assert.Equal(edge.From.Y, edge.To.Y); // walks must be level
                    int a = Math.Min(edge.From.X, edge.To.X);
                    int b = Math.Max(edge.From.X, edge.To.X);
                    for (int x = a; x <= b; x++)
                    {
                        Assert.True(room.Map.IsSolidTile(x, edge.From.Y),
                            $"{room.Name}: floor gap during walk at {x},{edge.From.Y}");
                        Assert.False(room.Map.IsSolidTile(x, edge.From.Y - 1),
                            $"{room.Name}: obstruction during walk at {x},{edge.From.Y - 1}");
                    }
                }
                else
                {
                    AssertStandable(room.Map, edge.From, room.Name);
                    AssertStandable(room.Map, edge.To, room.Name);
                }
            }
        }
    }

    [Fact]
    public void EverySpawnAndEntryIsClearOfSolids()
    {
        var w = WorldBuilder.Build();
        foreach (var room in w.Rooms.Values)
        {
            // default spawn body should not be embedded in a wall
            var spawnBox = Aabb.FromCenter(room.DefaultSpawn, 10, 14);
            Assert.False(room.Map.OverlapsSolid(spawnBox), $"{room.Name}: default spawn in solid");
            foreach (var door in room.Doors)
            {
                var entryBox = Aabb.FromCenter(door.EntrySpawn, 10, 14);
                Assert.False(room.Map.OverlapsSolid(entryBox), $"{room.Name}: door entry in solid");
            }
        }
    }

    [Fact]
    public void GatesGateAndProgressionIsSolvable()
    {
        var w = WorldBuilder.Build();
        var acc = new AbilitySet();

        // room 0 is walkable from the very start
        Assert.True(Reachability.PathClear(w.Rooms[0], acc));

        var order = new (int room, AbilityType ability)[]
        {
            (1, AbilityType.DoubleJump),
            (2, AbilityType.Dash),
            (3, AbilityType.WallClimb),
            (4, AbilityType.Phase),
        };

        foreach (var (roomId, ability) in order)
        {
            var room = w.Rooms[roomId];
            var gate = room.CriticalPath.First(e => e.Requires == ability);

            // the gate must NOT be passable before we have the ability
            Assert.False(Reachability.CanTraverse(gate, acc),
                $"{room.Name}: gate passable without {ability}");

            // but everything else in the room must already be reachable (to grab the pickup)
            foreach (var edge in room.CriticalPath)
                if (edge.Requires != ability)
                    Assert.True(Reachability.CanTraverse(edge, acc),
                        $"{room.Name}: non-gate edge blocked before {ability}");

            acc.Unlock(ability);
            Assert.True(Reachability.CanTraverse(gate, acc),
                $"{room.Name}: gate still blocked with {ability}");
            Assert.True(Reachability.PathClear(room, acc),
                $"{room.Name}: full path not clear with {ability}");
        }

        // with everything unlocked, the Core room is trivially walkable
        Assert.True(Reachability.PathClear(w.Rooms[WorldBuilder.CoreRoomId], acc));
        Assert.True(acc.HasAll);
    }

    [Fact]
    public void EnteringDoorTriggerTransitionsRooms()
    {
        var w = WorldBuilder.Build();
        var p = new Player(w.StartSpawn);
        var door = w.Current.DoorOn(Direction.East)!;
        p.PlaceAt(new Vector2(door.TriggerZone.CenterX, door.TriggerZone.CenterY));

        w.Update(Dt, p);

        Assert.Equal(1, w.CurrentRoomId);
        Assert.True(w.JustTransitioned);
        // player should now be near room 1's west entry, not back in the trigger
        var entry = w.Current.DoorOn(Direction.West)!.EntrySpawn;
        Assert.True(Vector2.Distance(p.Center, entry) < 20f);
    }

    [Fact]
    public void PickupUnlocksAbility()
    {
        var w = WorldBuilder.Build();
        var p = new Player(w.StartSpawn);
        AbilityType? unlocked = null;
        w.AbilityUnlocked += a => unlocked = a;

        // transition into room 1
        var door = w.Current.DoorOn(Direction.East)!;
        p.PlaceAt(new Vector2(door.TriggerZone.CenterX, door.TriggerZone.CenterY));
        w.Update(Dt, p);
        Assert.Equal(1, w.CurrentRoomId);

        var pickup = w.Current.Pickups[0];
        p.PlaceAt(pickup.WorldCenter(16));
        w.Update(Dt, p);

        Assert.True(pickup.Taken);
        Assert.True(p.Abilities.Has(AbilityType.DoubleJump));
        Assert.Equal(AbilityType.DoubleJump, unlocked);
    }

    [Fact]
    public void ChainingDoorsReachesTheCore()
    {
        var w = WorldBuilder.Build();
        var p = new Player(w.StartSpawn);

        for (int r = 0; r < 5; r++)
        {
            for (int k = 0; k < 25; k++) w.Update(Dt, p); // let transition lock expire
            var east = w.Current.DoorOn(Direction.East)!;
            p.PlaceAt(new Vector2(east.TriggerZone.CenterX, east.TriggerZone.CenterY));
            w.Update(Dt, p);
        }

        Assert.Equal(WorldBuilder.CoreRoomId, w.CurrentRoomId);
        Assert.True(w.Current.IsCore);

        for (int k = 0; k < 25; k++) w.Update(Dt, p);
        p.PlaceAt(w.Current.CoreCenter!.Value);
        w.Update(Dt, p);
        Assert.True(w.ReachedCore);
    }

    [Fact]
    public void ResetRestoresStartRoomAndPickups()
    {
        var w = WorldBuilder.Build();
        var p = new Player(w.StartSpawn);
        var door = w.Current.DoorOn(Direction.East)!;
        p.PlaceAt(new Vector2(door.TriggerZone.CenterX, door.TriggerZone.CenterY));
        w.Update(Dt, p);
        w.Current.Pickups.ForEach(pk => pk.Taken = true);

        w.Reset();

        Assert.Equal(WorldBuilder.StartRoomId, w.CurrentRoomId);
        Assert.False(w.ReachedCore);
        Assert.All(w.Rooms[1].Pickups, pk => Assert.False(pk.Taken));
    }
}
