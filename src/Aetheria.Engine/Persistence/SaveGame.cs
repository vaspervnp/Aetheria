using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;

namespace Aetheria.Engine.Persistence;

/// <summary>A serializable snapshot of run progress (the checkpoint).</summary>
public sealed class SaveData
{
    public int RoomId { get; set; }
    public int Deaths { get; set; }
    public List<AbilityType> Abilities { get; set; } = new();

    public bool HasAbility(AbilityType a) => Abilities.Contains(a);
}

/// <summary>
/// Captures and restores run state (unlocked abilities + last room reached).
/// Pure and rendering-free so it is fully unit-testable; file I/O lives in
/// <see cref="SaveStore"/>.
/// </summary>
public static class SaveGame
{
    public static SaveData Capture(World.World world, Player player, int deaths)
        => new()
        {
            RoomId = world.CurrentRoomId,
            Deaths = deaths,
            Abilities = player.Abilities.Unlocked.OrderBy(a => (int)a).ToList(),
        };

    /// <summary>
    /// Apply a save onto a freshly-built world + player: re-grant abilities, mark
    /// the matching pickups collected, and warp to the saved room.
    /// </summary>
    public static void Apply(SaveData data, World.World world, Player player)
    {
        foreach (var a in data.Abilities) player.Abilities.Unlock(a);
        foreach (var room in world.Rooms.Values)
            foreach (var pickup in room.Pickups)
                if (data.Abilities.Contains(pickup.Type)) pickup.Taken = true;
        world.DebugEnter(data.RoomId, player);
    }
}
