using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;
using Aetheria.Engine.Persistence;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class SaveTests
{
    [Fact]
    public void CaptureThenApplyRestoresAbilitiesRoomAndPickups()
    {
        var world = WorldBuilder.Build();
        var player = new Player(world.StartSpawn);
        player.Abilities.Unlock(AbilityType.DoubleJump);
        player.Abilities.Unlock(AbilityType.Dash);
        world.DebugEnter(3, player);

        var data = SaveGame.Capture(world, player, deaths: 4);
        Assert.Equal(3, data.RoomId);
        Assert.Equal(4, data.Deaths);
        Assert.Contains(AbilityType.DoubleJump, data.Abilities);
        Assert.Contains(AbilityType.Dash, data.Abilities);

        // apply onto a fresh run
        var world2 = WorldBuilder.Build();
        var player2 = new Player(world2.StartSpawn);
        SaveGame.Apply(data, world2, player2);

        Assert.True(player2.Abilities.Has(AbilityType.DoubleJump));
        Assert.True(player2.Abilities.Has(AbilityType.Dash));
        Assert.False(player2.Abilities.Has(AbilityType.WallClimb));
        Assert.Equal(3, world2.CurrentRoomId);
        // the double-jump & dash pickups should be marked collected
        Assert.True(world2.Rooms[1].Pickups.Single().Taken);
        Assert.True(world2.Rooms[2].Pickups.Single().Taken);
        Assert.False(world2.Rooms[3].Pickups.Single().Taken); // wall-climb not owned
    }

    [Fact]
    public void SaveStoreRoundTripsThroughAFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetheria_test_" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            var data = new SaveData
            {
                RoomId = 5,
                Deaths = 7,
                Abilities = { AbilityType.DoubleJump, AbilityType.WallClimb, AbilityType.Phase },
            };
            SaveStore.Save(data, path);
            Assert.True(SaveStore.Exists(path));

            var loaded = SaveStore.Load(path);
            Assert.NotNull(loaded);
            Assert.Equal(5, loaded!.RoomId);
            Assert.Equal(7, loaded.Deaths);
            Assert.Equal(3, loaded.Abilities.Count);
            Assert.Contains(AbilityType.Phase, loaded.Abilities);

            SaveStore.Delete(path);
            Assert.False(SaveStore.Exists(path));
        }
        finally
        {
            SaveStore.Delete(path);
        }
    }

    [Fact]
    public void LoadingMissingFileReturnsNull()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetheria_missing_" + Guid.NewGuid().ToString("N") + ".dat");
        Assert.Null(SaveStore.Load(path));
    }

    [Fact]
    public void LoadingGarbageDoesNotThrow()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetheria_garbage_" + Guid.NewGuid().ToString("N") + ".dat");
        try
        {
            File.WriteAllText(path, "not a real save\n@@@@\nroom=notanumber\n");
            var data = SaveStore.Load(path); // should not throw
            Assert.NotNull(data);
            Assert.Equal(0, data!.RoomId); // unpar‑seable room stays default
        }
        finally { SaveStore.Delete(path); }
    }
}
