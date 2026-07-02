using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class GameFlagsTests
{
    [Fact]
    public void SetGetAndEvent()
    {
        var f = new GameFlags();
        string? changed = null;
        f.Changed += (k, _) => changed = k;
        Assert.False(f.IsSet("a"));
        f.SetFlag("a");
        Assert.True(f.IsSet("a"));
        Assert.Equal("a", changed);
        f.Increment("count");
        f.Increment("count");
        Assert.Equal(2, f.Get("count"));
    }

    [Fact]
    public void LoadFromReplacesState()
    {
        var f = new GameFlags();
        f.SetFlag("old");
        f.LoadFrom(new Dictionary<string, int> { ["new"] = 3 });
        Assert.False(f.IsSet("old"));
        Assert.Equal(3, f.Get("new"));
    }
}

public class PuzzleTests
{
    private const float Dt = 1f / 60f;
    private const int T = GameConfig.TileSize;

    private static TileMap FlatRoom()
    {
        var m = new TileMap(Doorways.RoomW, Doorways.RoomH, T);
        m.Border(TileType.Solid);
        m.Fill(0, Doorways.FloorRow, m.Width - 1, m.Height - 1, TileType.Solid);
        return m;
    }

    private static Room MakeRoom(TileMap m) => new()
    {
        GridX = 0, GridY = 0, Biome = Biome.RustVents, Map = m, Seed = 1,
    };

    [Fact]
    public void ShootableSwitchSetsItsFlag()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        room.Switches.Add(new PuzzleSwitch { Tile = new GridPoint(10, 15), Kind = SwitchKind.Shootable, Flag = "door1" });
        var flags = new GameFlags();
        var player = new Player(new Vector2(5 * T, 17 * T));
        var blocks = new List<PushBlock>();

        // a player projectile passing through the switch
        var c = room.Switches[0].WorldCenter(T);
        var projectiles = new List<Projectile> { new(c, new Vector2(100, 0), 4, 1f, 1, fromPlayer: true) };

        PuzzleSystem.Step(room, player, InputState.None, projectiles, blocks, flags, null, m, Dt);

        Assert.True(room.Switches[0].Active);
        Assert.True(flags.IsSet("door1"));
    }

    [Fact]
    public void MeleeSwitchNeedsTheBladeNotAShot()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        room.Switches.Add(new PuzzleSwitch { Tile = new GridPoint(10, 15), Kind = SwitchKind.Melee, Flag = "m1" });
        var flags = new GameFlags();
        var player = new Player(new Vector2(5 * T, 17 * T));
        var c = room.Switches[0].WorldCenter(T);

        // a shot does not trigger a melee switch
        var shot = new List<Projectile> { new(c, new Vector2(100, 0), 4, 1f, 1, fromPlayer: true) };
        PuzzleSystem.Step(room, player, InputState.None, shot, new(), flags, null, m, Dt);
        Assert.False(flags.IsSet("m1"));

        // a melee hitbox over it does
        var melee = new Aabb(c.X - 8, c.Y - 8, 16, 16);
        PuzzleSystem.Step(room, player, InputState.None, new(), new(), flags, melee, m, Dt);
        Assert.True(flags.IsSet("m1"));
    }

    [Fact]
    public void SequenceSolvedOnlyInCorrectOrder()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        room.Sequence = new SequencePuzzle { Flag = "seq", Count = 3, TimeLimit = 6f };
        for (int i = 0; i < 3; i++)
            room.Switches.Add(new PuzzleSwitch { Tile = new GridPoint(8 + i * 4, 15), Kind = SwitchKind.Shootable, SequenceIndex = i });
        var flags = new GameFlags();
        var player = new Player(new Vector2(5 * T, 17 * T));

        void Hit(int idx)
        {
            var sw = room.Switches.First(s => s.SequenceIndex == idx);
            var proj = new List<Projectile> { new(sw.WorldCenter(T), new Vector2(60, 0), 4, 1f, 1, true) };
            PuzzleSystem.Step(room, player, InputState.None, proj, new(), flags, null, m, Dt);
        }

        // wrong order resets
        Hit(0); Hit(2);
        Assert.False(flags.IsSet("seq"));
        Assert.Equal(0, room.Sequence.Progress);

        // correct order solves
        Hit(0); Hit(1); Hit(2);
        Assert.True(room.Sequence.Solved);
        Assert.True(flags.IsSet("seq"));
    }

    [Fact]
    public void SequenceTimesOut()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        room.Sequence = new SequencePuzzle { Flag = "seq", Count = 3, TimeLimit = 0.1f };
        for (int i = 0; i < 3; i++)
            room.Switches.Add(new PuzzleSwitch { Tile = new GridPoint(8 + i * 4, 15), Kind = SwitchKind.Shootable, SequenceIndex = i });
        var flags = new GameFlags();
        var player = new Player(new Vector2(5 * T, 17 * T));

        var sw0 = room.Switches.First(s => s.SequenceIndex == 0);
        PuzzleSystem.Step(room, player, InputState.None,
            new List<Projectile> { new(sw0.WorldCenter(T), new Vector2(60, 0), 4, 1f, 1, true) }, new(), flags, null, m, Dt);
        Assert.Equal(1, room.Sequence.Progress);

        for (int i = 0; i < 20; i++) // let the timer expire
            PuzzleSystem.Step(room, player, InputState.None, new(), new(), flags, null, m, Dt);
        Assert.Equal(0, room.Sequence.Progress);
        Assert.False(flags.IsSet("seq"));
    }

    [Fact]
    public void PressurePlateLatchesFlagWhileBlockRestsOnIt()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        int plateCol = 12;
        room.Plates.Add(new PressurePlate { Tile = new GridPoint(plateCol, Doorways.FloorRow - 1), Flag = "plate" });
        var flags = new GameFlags();
        var player = new Player(new Vector2(5 * T, 17 * T));

        // block starting above the plate falls onto it
        var block = new PushBlock(new Vector2((plateCol + 0.5f) * T, (Doorways.FloorRow - 4) * T));
        var blocks = new List<PushBlock> { block };

        bool everPressed = false;
        for (int i = 0; i < 120; i++)
        {
            PuzzleSystem.Step(room, player, InputState.None, new(), blocks, flags, null, m, Dt);
            everPressed |= flags.IsSet("plate");
        }
        Assert.True(everPressed, "plate never latched under the block");
    }

    [Fact]
    public void PlayerCanPushABlockAlongTheFloor()
    {
        var m = FlatRoom();
        var room = MakeRoom(m);
        var flags = new GameFlags();

        var block = new PushBlock(new Vector2(14 * T, (Doorways.FloorRow - 1) * T));
        var blocks = new List<PushBlock> { block };
        float startX = block.Position.X;

        // player to the left of the block, pushing right
        var player = new Player(new Vector2(12 * T, (Doorways.FloorRow - 1) * T));
        for (int i = 0; i < 90; i++)
        {
            player.Update(InputState.Move(1f), m, Dt);
            PuzzleSystem.Step(room, player, InputState.Move(1f), new(), blocks, flags, null, m, Dt);
        }
        Assert.True(block.Position.X > startX + T, "block was not pushed");
    }
}
