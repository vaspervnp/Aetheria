using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

/// <summary>
/// Proves each ability gate is actually beatable by the REAL player controller
/// physics (not just the conservative reachability model): places Spark at the
/// foot of a gate with the required ability and drives scripted input until it
/// reaches the far side. Catches real mechanical regressions in movement.
/// </summary>
public class GateTraversalTests
{
    private const float Dt = 1f / 60f;
    private const int T = GameConfig.TileSize;

    private static Player PlaceAtGateFoot(GridPoint from, params AbilityType[] abilities)
    {
        var p = new Player(Vector2.Zero);
        foreach (var a in abilities) p.Abilities.Unlock(a);
        p.PlaceAt(new Vector2((from.X + 0.5f) * T, from.Y * T - 7f));
        return p;
    }

    private static PathEdge Gate(Room room, AbilityType ability)
        => room.CriticalPath.First(e => e.Requires == ability);

    private static bool Drive(Player p, TileMap map, Func<int, InputState> script,
                              Func<Player, bool> success, int maxFrames)
    {
        for (int i = 0; i < 10; i++) p.Update(InputState.None, map, Dt); // settle onto the ledge
        for (int i = 0; i < maxFrames; i++)
        {
            p.Update(script(i), map, Dt);
            if (success(p)) return true;
            if (!p.Alive) return false;
        }
        return false;
    }

    /// <summary>Landed, on the ground, at or above the target row, near/past the target column.</summary>
    private static Func<Player, bool> ReachedTop(GridPoint to) =>
        p => p.OnGround && p.Bounds.Bottom <= (to.Y + 0.75f) * T && p.Center.X >= (to.X - 1) * T;

    [Fact]
    public void DoubleJumpGateIsBeatable()
    {
        var room = WorldBuilder.Build().Rooms[1];
        var gate = Gate(room, AbilityType.DoubleJump);
        var p = PlaceAtGateFoot(gate.From, AbilityType.DoubleJump);

        InputState Script(int i)
        {
            int c = i % 55;
            bool jump1 = c == 2;
            bool jump2 = c == 16;                 // second (double) jump near apex
            return new InputState(moveX: 1f,
                jumpPressed: jump1 || jump2,
                jumpHeld: c is >= 2 and <= 12 or (>= 16 and <= 22));
        }

        Assert.True(Drive(p, room.Map, Script, ReachedTop(gate.To), 300),
            "could not double-jump onto the high ground");
    }

    [Fact]
    public void WallClimbGateIsBeatable()
    {
        var room = WorldBuilder.Build().Rooms[3];
        var gate = Gate(room, AbilityType.WallClimb);
        var p = PlaceAtGateFoot(gate.From, AbilityType.WallClimb, AbilityType.DoubleJump, AbilityType.Dash);

        // hold right (into the cliff) + up (climb); one initial hop to reach the wall
        InputState Script(int i) => new(moveX: 1f, up: true,
            jumpPressed: i == 2, jumpHeld: i is >= 2 and <= 8);

        Assert.True(Drive(p, room.Map, Script, ReachedTop(gate.To), 700),
            "could not wall-climb the cliff to the high door");
    }

    [Fact]
    public void DashGateIsBeatable()
    {
        var room = WorldBuilder.Build().Rooms[2];
        var gate = Gate(room, AbilityType.Dash);
        // start with a short run-up west of the lip, like a real approach
        var p = new Player(Vector2.Zero);
        p.Abilities.Unlock(AbilityType.Dash);
        p.Abilities.Unlock(AbilityType.DoubleJump);
        p.PlaceAt(new Vector2((gate.From.X - 4 + 0.5f) * T, gate.From.Y * T - 7f));

        // run to the lip, hop, then dash across the chasm
        InputState Script(int i) => new(moveX: 1f,
            jumpPressed: i == 13,
            jumpHeld: i is >= 13 and <= 19,
            dashPressed: i == 16);

        // success: safely on the floor at/past the far lip
        bool Success(Player pl) => pl.OnGround && pl.Center.X >= gate.To.X * T
                                   && pl.Bounds.Bottom <= (gate.To.Y + 0.75f) * T;
        Assert.True(Drive(p, room.Map, Script, Success, 200),
            "could not dash across the chasm");
    }

    [Fact]
    public void PhaseGateIsBeatable()
    {
        var room = WorldBuilder.Build().Rooms[4];
        var gate = Gate(room, AbilityType.Phase);
        var p = PlaceAtGateFoot(gate.From, AbilityType.Phase);

        InputState Script(int i) => new(moveX: 1f, phaseHeld: true);

        bool Success(Player pl) => pl.Center.X >= gate.To.X * T;
        Assert.True(Drive(p, room.Map, Script, Success, 200),
            "could not phase through the sealed wall");
    }
}
