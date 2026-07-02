using System.Numerics;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.World;

public enum SwitchKind { Shootable, Melee }

/// <summary>A switch Spark activates by shooting it (or striking it with the blade).</summary>
public sealed class PuzzleSwitch
{
    public required GridPoint Tile { get; init; }
    public required SwitchKind Kind { get; init; }
    /// <summary>Flag set (to 1) when this switch is activated. Null for sequence-only switches.</summary>
    public string? Flag { get; init; }
    /// <summary>Position in a <see cref="SequencePuzzle"/> (0-based), or -1 if standalone.</summary>
    public int SequenceIndex { get; init; } = -1;

    public bool Active { get; set; }   // runtime lit state

    public Vector2 WorldCenter(int ts) => new((Tile.X + 0.5f) * ts, (Tile.Y + 0.5f) * ts);
    public Aabb Bounds(int ts) => new(Tile.X * ts, Tile.Y * ts, ts, ts);
}

/// <summary>A floor plate that latches its flag while a heavy block rests on it.</summary>
public sealed class PressurePlate
{
    public required GridPoint Tile { get; init; }
    public string? Flag { get; init; }
    public bool Pressed { get; set; }

    public Vector2 WorldCenter(int ts) => new((Tile.X + 0.5f) * ts, (Tile.Y + 0.5f) * ts);
    /// <summary>A thin trigger sitting on top of the plate tile.</summary>
    public Aabb Bounds(int ts) => new(Tile.X * ts, Tile.Y * ts, ts, ts * 0.5f);
}

/// <summary>
/// A "hit N switches in the right order within a time window" puzzle. Switches
/// carrying a matching <see cref="PuzzleSwitch.SequenceIndex"/> feed it; solving
/// it sets <see cref="Flag"/>.
/// </summary>
public sealed class SequencePuzzle
{
    public required string Flag { get; init; }
    public required int Count { get; init; }
    public float TimeLimit { get; init; } = 6f;

    // runtime
    public int Progress { get; set; }
    public float Timer { get; set; }
    public bool Solved { get; set; }

    public void Reset()
    {
        Progress = 0;
        Timer = 0f;
    }
}
