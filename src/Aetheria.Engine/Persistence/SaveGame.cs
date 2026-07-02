using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Persistence;

/// <summary>A serializable snapshot of run progress (the checkpoint).</summary>
public sealed class SaveData
{
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int Deaths { get; set; }
    public List<AbilityType> Abilities { get; set; } = new();
    public Dictionary<string, int> Flags { get; set; } = new();

    public bool HasAbility(AbilityType a) => Abilities.Contains(a);
    public GridPoint Cell => new(CellX, CellY);
}

/// <summary>
/// Captures and restores run state (abilities, world flags, and the last room
/// cell reached). Pure and rendering-free so it is fully unit-testable; file I/O
/// lives in <see cref="SaveStore"/>.
/// </summary>
public static class SaveGame
{
    public static SaveData Capture(World.World world, Player player, int deaths)
        => new()
        {
            CellX = world.CurrentCell.X,
            CellY = world.CurrentCell.Y,
            Deaths = deaths,
            Abilities = player.Abilities.Unlocked.OrderBy(a => (int)a).ToList(),
            Flags = world.Flags.NonZero.ToDictionary(kv => kv.Key, kv => kv.Value),
        };

    /// <summary>
    /// Apply a save onto a freshly-built world + player: re-grant abilities and
    /// flags, mark matching pickups collected, and warp to the saved cell.
    /// </summary>
    public static void Apply(SaveData data, World.World world, Player player)
    {
        foreach (var a in data.Abilities) player.Abilities.Unlock(a);
        foreach (var room in world.Rooms.Values)
            foreach (var pickup in room.Pickups)
                if (data.Abilities.Contains(pickup.Type)) pickup.Taken = true;
        world.Flags.LoadFrom(data.Flags);
        world.DebugEnter(data.Cell, player);
    }
}
