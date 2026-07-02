using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

/// <summary>
/// These tests drive the *real* Player.Update simulation (the same code the game
/// loop runs) against hand-built tile maps, asserting emergent platforming
/// behaviour. dt is fixed at 1/60 for determinism.
/// </summary>
public class PlayerTests
{
    private const float Dt = 1f / 60f;
    private const int Tile = GameConfig.TileSize;

    /// <summary>40x24 room, solid border + a 2-tile-thick floor at the bottom.</summary>
    private static TileMap FlatRoom(int floorTopRow = 22)
    {
        var m = new TileMap(40, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(0, floorTopRow, m.Width - 1, m.Height - 1, TileType.Solid);
        return m;
    }

    private static Player SpawnOnFloor(TileMap m, int tileX = 6, int floorTopRow = 22)
    {
        // Center the player a couple of tiles above the floor and let it settle.
        var spawn = new Vector2(tileX * Tile + Tile * 0.5f, (floorTopRow - 2) * Tile);
        var p = new Player(spawn);
        Settle(p, m, 40);
        return p;
    }

    private static void Settle(Player p, TileMap m, int frames, InputState input = default)
    {
        for (int i = 0; i < frames; i++) p.Update(input, m, Dt);
    }

    [Fact]
    public void PlayerFallsAndLandsOnFloor()
    {
        var m = FlatRoom();
        var p = new Player(new Vector2(6 * Tile, 4 * Tile));
        Settle(p, m, 120);
        Assert.True(p.OnGround);
        Assert.True(MathF.Abs(p.Velocity.Y) < 1f);
        // rests exactly on top of the floor row (row 22 -> top edge y = 352)
        Assert.Equal(22 * Tile, p.Bounds.Bottom, 1);
    }

    [Fact]
    public void PlayerNeverTunnelsThroughFloor()
    {
        var m = FlatRoom();
        var p = new Player(new Vector2(6 * Tile, 2 * Tile));
        for (int i = 0; i < 600; i++)
        {
            p.Update(InputState.None, m, Dt);
            Assert.True(p.Bounds.Bottom <= 22 * Tile + 0.5f, $"fell through at frame {i}");
        }
    }

    [Fact]
    public void JumpReachesExpectedApex()
    {
        var m = FlatRoom();
        var p = SpawnOnFloor(m);
        float startY = p.Position.Y;

        float minY = startY;
        // press jump then keep holding through the rise
        p.Update(new InputState(jumpPressed: true, jumpHeld: true), m, Dt);
        minY = MathF.Min(minY, p.Position.Y);
        for (int i = 0; i < 60; i++)
        {
            p.Update(new InputState(jumpHeld: true), m, Dt);
            minY = MathF.Min(minY, p.Position.Y);
        }

        float rise = startY - minY;
        float analytic = GameConfig.JumpSpeed * GameConfig.JumpSpeed / (2f * GameConfig.Gravity);
        Assert.InRange(rise, analytic - 12f, analytic + 12f);
    }

    [Fact]
    public void ReleasingJumpEarlyCutsHeight()
    {
        var m = FlatRoom();

        float FullJump()
        {
            var p = SpawnOnFloor(m);
            float startY = p.Position.Y, minY = startY;
            p.Update(new InputState(jumpPressed: true, jumpHeld: true), m, Dt);
            for (int i = 0; i < 60; i++)
            {
                p.Update(new InputState(jumpHeld: true), m, Dt);
                minY = MathF.Min(minY, p.Position.Y);
            }
            return startY - minY;
        }

        float ShortJump()
        {
            var p = SpawnOnFloor(m);
            float startY = p.Position.Y, minY = startY;
            p.Update(new InputState(jumpPressed: true, jumpHeld: true), m, Dt);
            p.Update(new InputState(jumpHeld: true), m, Dt);
            p.Update(new InputState(jumpReleased: true), m, Dt); // let go early
            for (int i = 0; i < 60; i++)
            {
                p.Update(InputState.None, m, Dt);
                minY = MathF.Min(minY, p.Position.Y);
            }
            return startY - minY;
        }

        Assert.True(ShortJump() < FullJump() - 8f);
    }

    [Fact]
    public void RunningReachesMaxSpeed()
    {
        var m = FlatRoom();
        var p = SpawnOnFloor(m);
        for (int i = 0; i < 40; i++) p.Update(InputState.Move(1f), m, Dt);
        Assert.InRange(p.Velocity.X, GameConfig.MaxRunSpeed - 5f, GameConfig.MaxRunSpeed + 0.01f);
        Assert.Equal(1, p.Facing);
    }

    [Fact]
    public void WallBlocksHorizontalMovement()
    {
        var m = FlatRoom();
        // vertical wall at tile column 10
        m.Fill(10, 0, 10, 21, TileType.Solid);
        var p = SpawnOnFloor(m, tileX: 6);
        for (int i = 0; i < 120; i++) p.Update(InputState.Move(1f), m, Dt);
        // must be stopped left of the wall (wall left edge = 160)
        Assert.True(p.Bounds.Right <= 10 * Tile + 0.01f);
        Assert.True(p.OnWallRight);
    }

    [Fact]
    public void DoubleJumpRequiresAbility()
    {
        var m = FlatRoom();

        float SecondJumpVy(bool withAbility)
        {
            var p = SpawnOnFloor(m);
            if (withAbility) p.Abilities.Unlock(AbilityType.DoubleJump);
            p.Update(new InputState(jumpPressed: true, jumpHeld: true), m, Dt);
            // rise, then wait until we're past the apex and clearly falling
            for (int i = 0; i < 40 && p.Velocity.Y < 60f; i++)
                p.Update(new InputState(jumpHeld: true), m, Dt);
            // still airborne and descending; trigger a second jump
            p.Update(new InputState(jumpPressed: true, jumpHeld: true), m, Dt);
            return p.Velocity.Y;
        }

        Assert.True(SecondJumpVy(true) < -300f);   // strong upward kick
        Assert.True(SecondJumpVy(false) > -50f);   // no double jump: still falling
    }

    [Fact]
    public void DashRequiresAbilityAndMovesFar()
    {
        var m = FlatRoom();

        float DashDistance(bool withAbility)
        {
            var p = SpawnOnFloor(m);
            for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt); // face right
            float startX = p.Position.X;
            if (withAbility) p.Abilities.Unlock(AbilityType.Dash);
            p.Update(new InputState(moveX: 1f, dashPressed: true), m, Dt);
            for (int i = 0; i < 12; i++) p.Update(InputState.Move(1f), m, Dt);
            return p.Position.X - startX;
        }

        Assert.True(DashDistance(true) > 45f);
        Assert.True(DashDistance(false) < DashDistance(true) - 20f);
    }

    [Fact]
    public void CoyoteTimeAllowsJumpJustAfterLeavingLedge()
    {
        // Floor only spans the left half; player runs off the right edge.
        var m = new TileMap(40, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(0, 22, 15, 23, TileType.Solid);       // ledge ends at tile 15
        var spawn = new Vector2(10 * Tile, 20 * Tile);
        var p = new Player(spawn);
        Settle(p, m, 40);
        Assert.True(p.OnGround);

        bool jumped = false;
        for (int i = 0; i < 200; i++)
        {
            bool wasGround = p.OnGround;
            p.Update(InputState.Move(1f), m, Dt);
            if (wasGround && !p.OnGround)
            {
                // first airborne frame after walking off: jump should still fire
                p.Update(new InputState(moveX: 1f, jumpPressed: true, jumpHeld: true), m, Dt);
                jumped = p.Velocity.Y < -200f;
                break;
            }
        }
        Assert.True(jumped);
    }

    [Fact]
    public void HazardDamagesPlayer()
    {
        var m = FlatRoom();
        m.Set(6, 21, TileType.Hazard); // a spike right where the player stands
        var p = new Player(new Vector2(6 * Tile + 3, 19 * Tile));
        int before = p.Health;            // full health, not yet exposed
        Settle(p, m, 25);                 // fall onto the spike
        Assert.True(p.Health < before);
    }

    [Fact]
    public void PhaseAbilityPassesThroughPhaseWalls()
    {
        int DistanceThroughPhaseWall(bool phasing)
        {
            var m = FlatRoom();
            m.Fill(10, 0, 10, 21, TileType.Phase); // a phase wall
            var p = SpawnOnFloor(m, tileX: 6);
            p.Abilities.Unlock(AbilityType.Phase);
            for (int i = 0; i < 120; i++)
                p.Update(new InputState(moveX: 1f, phaseHeld: phasing), m, Dt);
            return (int)p.Position.X;
        }

        int blocked = DistanceThroughPhaseWall(false);
        int through = DistanceThroughPhaseWall(true);
        Assert.True(blocked <= 10 * Tile);        // stopped at the wall
        Assert.True(through > 11 * Tile);          // slipped past it
    }

    [Fact]
    public void TakingLethalDamageKillsPlayer()
    {
        var m = FlatRoom();
        var p = SpawnOnFloor(m);
        for (int i = 0; i < GameConfig.MaxHealth; i++)
        {
            // wait out invulnerability then hit again
            p.TakeDamage(1, new Vector2(1, -1));
            for (int f = 0; f < 70; f++) p.Update(InputState.None, m, Dt);
        }
        Assert.False(p.Alive);
        Assert.Equal(0, p.Health);
    }
}
