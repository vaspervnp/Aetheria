using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.World;

public enum Direction { North, East, South, West }

public enum EnemyKind { Crawler, Floater, Sentinel, Charger, Warden }

public readonly record struct GridPoint(int X, int Y);

/// <summary>A connection to another room, triggered when Spark enters the zone.</summary>
public sealed class Door
{
    public required Direction Edge { get; init; }
    public required int TargetRoomId { get; init; }
    public required Direction TargetEdge { get; init; }
    /// <summary>World-space zone whose overlap with the player fires the transition.</summary>
    public required Aabb TriggerZone { get; init; }
    /// <summary>World-space player-center position when arriving through this door.</summary>
    public required Vector2 EntrySpawn { get; init; }
}

public sealed class AbilityPickup
{
    public required GridPoint Tile { get; init; }
    public required AbilityType Type { get; init; }
    public bool Taken { get; set; }

    public Vector2 WorldCenter(int tileSize)
        => new((Tile.X + 0.5f) * tileSize, (Tile.Y + 0.5f) * tileSize);
}

public sealed class EnemySpawn
{
    public required GridPoint Tile { get; init; }
    public required EnemyKind Kind { get; init; }
    /// <summary>Half-width (in tiles) of a patrol/roam range around the spawn.</summary>
    public int Range { get; init; } = 4;

    public Vector2 WorldCenter(int tileSize)
        => new((Tile.X + 0.5f) * tileSize, (Tile.Y + 0.5f) * tileSize);
}

/// <summary>
/// One edge of the intended "critical path" through a room, optionally requiring
/// an ability. <paramref name="Walk"/> marks a traversal along continuous solid
/// floor (always passable). Used by <see cref="Reachability"/> and the test-suite
/// to prove the world is solvable and that gates actually gate.
/// </summary>
public readonly record struct PathEdge(GridPoint From, GridPoint To, AbilityType? Requires, bool Walk = false);

/// <summary>A single explorable room: geometry + entities + progression metadata.</summary>
public sealed class Room
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required TileMap Map { get; init; }
    /// <summary>Default player-center spawn (game start / checkpoint), world space.</summary>
    public required Vector2 DefaultSpawn { get; init; }
    public required uint Seed { get; init; }
    public bool IsCore { get; init; }
    /// <summary>World-space centre of the Core (victory trigger). Set on the core room.</summary>
    public Vector2? CoreCenter { get; set; }

    public List<Door> Doors { get; } = new();
    public List<AbilityPickup> Pickups { get; } = new();
    public List<EnemySpawn> Enemies { get; } = new();
    public List<PathEdge> CriticalPath { get; } = new();

    public Door? DoorOn(Direction edge) => Doors.FirstOrDefault(d => d.Edge == edge);
}
