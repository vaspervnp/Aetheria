using System.Numerics;
using Aetheria.Engine.Abilities;

namespace Aetheria.Engine.World;

public enum EnemyKind
{
    Crawler, Floater, Sentinel, Charger, Warden,
    HoverTurret, StalkerDrone, ArmoredCrawler,
}

public readonly record struct GridPoint(int X, int Y);

public enum DoorKind { Open, RedEnergy, Blast, AbilityGate }

/// <summary>
/// A connection on one edge of a room. The target is simply the grid neighbour
/// in <see cref="Edge"/>'s direction (computed by <see cref="World"/>), so no
/// explicit target id is stored. Locked doors fill their opening with a solid
/// barrier tile until their gate condition is met.
/// </summary>
public sealed class Door
{
    public required Direction Edge { get; init; }
    public DoorKind Kind { get; init; } = DoorKind.Open;
    /// <summary>Flag that opens a RedEnergy/Blast door.</summary>
    public string? Flag { get; init; }
    /// <summary>Ability that opens an AbilityGate door.</summary>
    public AbilityType? Requires { get; init; }

    public bool IsLocked(GameFlags flags, AbilitySet abilities) => Kind switch
    {
        DoorKind.Open => false,
        DoorKind.AbilityGate => Requires is { } a && !abilities.Has(a),
        _ => Flag is { } f && !flags.IsSet(f),
    };

    public TileType BarrierTile => Kind switch
    {
        DoorKind.RedEnergy => TileType.DoorRed,
        DoorKind.Blast => TileType.DoorBlast,
        _ => TileType.Solid,
    };
}

public sealed class AbilityPickup
{
    public required GridPoint Tile { get; init; }
    public required AbilityType Type { get; init; }
    public bool Taken { get; set; }

    public Vector2 WorldCenter(int tileSize)
        => new((Tile.X + 0.5f) * tileSize, (Tile.Y + 0.5f) * tileSize);
}

public sealed class WeaponPickup
{
    public required GridPoint Tile { get; init; }
    public required WeaponType Type { get; init; }
    public bool Taken { get; set; }

    public Vector2 WorldCenter(int tileSize)
        => new((Tile.X + 0.5f) * tileSize, (Tile.Y + 0.5f) * tileSize);
}

public sealed class EnemySpawn
{
    public required GridPoint Tile { get; init; }
    public required EnemyKind Kind { get; init; }
    public int Range { get; init; } = 4;

    public Vector2 WorldCenter(int tileSize)
        => new((Tile.X + 0.5f) * tileSize, (Tile.Y + 0.5f) * tileSize);
}

/// <summary>
/// One fixed-size screen at grid cell (<see cref="GridX"/>, <see cref="GridY"/>):
/// tile geometry, its biome, its doors, and the entities/puzzles it hosts.
/// </summary>
public sealed class Room
{
    public required int GridX { get; init; }
    public required int GridY { get; init; }
    public required Biome Biome { get; init; }
    public required TileMap Map { get; init; }
    public required uint Seed { get; init; }
    public string Name { get; set; } = "";

    /// <summary>Player-centre spawn used when this is the run's start room.</summary>
    public Vector2 DefaultSpawn { get; set; }

    public bool IsCore { get; set; }
    public Vector2? CoreCenter { get; set; }

    public List<Door> Doors { get; } = new();
    public List<AbilityPickup> Pickups { get; } = new();
    public List<WeaponPickup> WeaponPickups { get; } = new();
    public List<EnemySpawn> Enemies { get; } = new();
    public List<PuzzleSwitch> Switches { get; } = new();
    public List<PressurePlate> Plates { get; } = new();
    public List<GridPoint> BlockSpawns { get; } = new();
    public SequencePuzzle? Sequence { get; set; }

    public GridPoint Cell => new(GridX, GridY);

    public Door? DoorOn(Direction edge) => Doors.FirstOrDefault(d => d.Edge == edge);
    public bool HasDoor(Direction edge) => Doors.Exists(d => d.Edge == edge);
}
