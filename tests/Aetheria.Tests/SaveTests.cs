using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;
using Aetheria.Engine.Persistence;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class SaveTests
{
    [Fact]
    public void CaptureThenApplyRestoresAbilitiesFlagsAndCell()
    {
        var world = MapGenerator.Generate(4242u);
        var player = new Player(world.StartSpawn);
        player.Abilities.Unlock(AbilityType.DoubleJump);
        player.Abilities.Unlock(AbilityType.Dash);
        world.Flags.SetFlag("door_alpha");
        world.Flags.Set("seq_gamma", 2);
        var targetCell = world.Rooms.Keys.First(k => k.X != 0 || k.Y != 0);
        world.DebugEnter(targetCell, player);

        var data = SaveGame.Capture(world, player, deaths: 4);
        Assert.Equal(targetCell.X, data.CellX);
        Assert.Equal(targetCell.Y, data.CellY);
        Assert.Equal(4, data.Deaths);
        Assert.Contains(AbilityType.DoubleJump, data.Abilities);
        Assert.Equal(1, data.Flags["door_alpha"]);
        Assert.Equal(2, data.Flags["seq_gamma"]);

        var world2 = MapGenerator.Generate(4242u);
        var player2 = new Player(world2.StartSpawn);
        SaveGame.Apply(data, world2, player2);

        Assert.True(player2.Abilities.Has(AbilityType.DoubleJump));
        Assert.True(player2.Abilities.Has(AbilityType.Dash));
        Assert.False(player2.Abilities.Has(AbilityType.Phase));
        Assert.Equal(targetCell, world2.CurrentCell);
        Assert.True(world2.Flags.IsSet("door_alpha"));
        Assert.Equal(2, world2.Flags.Get("seq_gamma"));

        // owned-ability pickups are marked collected
        foreach (var room in world2.Rooms.Values)
            foreach (var pk in room.Pickups)
                if (pk.Type is AbilityType.DoubleJump or AbilityType.Dash)
                    Assert.True(pk.Taken);
    }

    [Fact]
    public void SaveStoreRoundTripsThroughAFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetheria_test_" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            var data = new SaveData
            {
                CellX = 3, CellY = -2, Deaths = 7,
                Abilities = { AbilityType.DoubleJump, AbilityType.WallClimb },
                Flags = { ["blast_1"] = 1, ["plate_2"] = 1 },
            };
            SaveStore.Save(data, path);
            Assert.True(SaveStore.Exists(path));

            var loaded = SaveStore.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded!.CellX);
            Assert.Equal(-2, loaded.CellY);
            Assert.Equal(7, loaded.Deaths);
            Assert.Equal(2, loaded.Abilities.Count);
            Assert.Equal(1, loaded.Flags["blast_1"]);
            Assert.Equal(1, loaded.Flags["plate_2"]);
        }
        finally { SaveStore.Delete(path); }
    }

    [Fact]
    public void LoadingMissingFileReturnsNull()
        => Assert.Null(SaveStore.Load(Path.Combine(Path.GetTempPath(), "aetheria_missing_" + Guid.NewGuid().ToString("N") + ".dat")));

    [Fact]
    public void LoadingGarbageDoesNotThrow()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetheria_garbage_" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            File.WriteAllText(path, "not a real save\n@@@@\ncellx=notanumber\n");
            var data = SaveStore.Load(path);
            Assert.NotNull(data);
            Assert.Equal(0, data!.CellX);
        }
        finally { SaveStore.Delete(path); }
    }
}
