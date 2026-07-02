using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class ArsenalTests
{
    [Fact]
    public void StartsWithBlasterOnly()
    {
        var a = new Arsenal();
        Assert.Equal(WeaponType.Blaster, a.Current);
        Assert.True(a.Has(WeaponType.Blaster));
        Assert.False(a.Has(WeaponType.Scatter));
        Assert.Equal(1, a.Count);
    }

    [Fact]
    public void UnlockAndCycle()
    {
        var a = new Arsenal();
        Assert.True(a.Unlock(WeaponType.Scatter));
        Assert.True(a.Unlock(WeaponType.Blade));
        Assert.False(a.Unlock(WeaponType.Blade)); // idempotent
        a.Select(WeaponType.Blaster);
        a.Cycle(); Assert.Equal(WeaponType.Scatter, a.Current);
        a.Cycle(); Assert.Equal(WeaponType.Blade, a.Current);
        a.Cycle(); Assert.Equal(WeaponType.Blaster, a.Current); // wraps
    }
}

public class WeaponTests
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

    private static Player OnFloor()
    {
        var p = new Player(new Vector2(5 * T, (Doorways.FloorRow - 2) * T));
        return p;
    }

    [Fact]
    public void BlasterFiresOneProjectile()
    {
        var m = FlatRoom();
        var p = OnFloor();
        for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt); // face + settle
        p.Update(new InputState(moveX: 1f, attackPressed: true), m, Dt);

        var projectiles = new List<Projectile>();
        CombatSystem.Step(p, new(), projectiles, m, Dt);
        Assert.Single(projectiles);
        Assert.True(projectiles[0].FromPlayer);
        Assert.True(projectiles[0].Velocity.X > 0);
    }

    [Fact]
    public void ScatterFiresASpreadThatBreaksWalls()
    {
        var m = FlatRoom();
        var p = OnFloor();
        p.Weapons.Unlock(WeaponType.Scatter);
        p.Weapons.Select(WeaponType.Scatter);
        for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt);
        p.Update(new InputState(moveX: 1f, attackPressed: true), m, Dt);

        var projectiles = new List<Projectile>();
        CombatSystem.Step(p, new(), projectiles, m, Dt);
        Assert.True(projectiles.Count >= GameConfig.ScatterPellets - 1);
        Assert.All(projectiles, pr => Assert.True(pr.BreaksWalls));
    }

    [Fact]
    public void ScatterShattersACrackedWall()
    {
        var m = FlatRoom();
        var p = OnFloor();
        for (int i = 0; i < 10; i++) p.Update(InputState.Move(1f), m, Dt); // settle & face right
        // a cracked block just in front, at the fire height
        int row = (int)(p.Center.Y / T);
        m.Set(9, row, TileType.Cracked);
        Assert.True(m.IsSolidTile(9, row));

        p.Weapons.Unlock(WeaponType.Scatter);
        p.Weapons.Select(WeaponType.Scatter);
        p.Update(new InputState(moveX: 0f, attackPressed: true), m, Dt);

        var projectiles = new List<Projectile>();
        for (int i = 0; i < 20; i++)
            CombatSystem.Step(p, new(), projectiles, m, Dt);

        Assert.Equal(TileType.Empty, m.Get(9, row));
    }

    [Fact]
    public void BladeDeflectsEnemyProjectiles()
    {
        var m = FlatRoom();
        var p = OnFloor();
        for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt); // face right
        p.Weapons.Unlock(WeaponType.Blade);
        p.Weapons.Select(WeaponType.Blade);
        p.Update(new InputState(moveX: 1f, attackPressed: true), m, Dt);
        Assert.True(p.Blading);
        Assert.NotNull(p.MeleeHitbox);

        var blade = p.MeleeHitbox!.Value;
        var enemyShot = new Projectile(new Vector2(blade.CenterX, blade.CenterY),
            new Vector2(-200, 0), 4, 2f, 1, fromPlayer: false);
        var projectiles = new List<Projectile> { enemyShot };

        CombatSystem.Step(p, new(), projectiles, m, Dt);

        Assert.True(enemyShot.FromPlayer);       // deflected — now hurts enemies
        Assert.True(enemyShot.Velocity.X > 0);   // sent back the way Spark faces
    }

    [Fact]
    public void BladeDamagesEnemyInTheArc()
    {
        var m = FlatRoom();
        var p = OnFloor();
        for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt);
        p.Weapons.Unlock(WeaponType.Blade);
        p.Weapons.Select(WeaponType.Blade);
        p.Update(new InputState(moveX: 1f, attackPressed: true), m, Dt);

        var blade = p.MeleeHitbox!.Value;
        var enemy = new Enemy(EnemyKind.Crawler, new Vector2(blade.CenterX, blade.CenterY), 16);
        var enemies = new List<Enemy> { enemy };
        int before = enemy.Health;

        CombatSystem.Step(p, enemies, new(), m, Dt);
        Assert.True(enemy.Health < before || !enemies.Contains(enemy));
    }

    [Fact]
    public void BladeTripsMeleeSwitch()
    {
        var m = FlatRoom();
        var p = OnFloor();
        for (int i = 0; i < 5; i++) p.Update(InputState.Move(1f), m, Dt);
        p.Weapons.Unlock(WeaponType.Blade);
        p.Weapons.Select(WeaponType.Blade);
        p.Update(new InputState(moveX: 1f, attackPressed: true), m, Dt);

        var blade = p.MeleeHitbox!.Value;
        int col = (int)(blade.CenterX / T), row = (int)(blade.CenterY / T);
        var room = new Room { GridX = 0, GridY = 0, Biome = Biome.RustVents, Map = m, Seed = 1 };
        room.Switches.Add(new PuzzleSwitch { Tile = new GridPoint(col, row), Kind = SwitchKind.Melee, Flag = "m" });
        var flags = new GameFlags();

        PuzzleSystem.Step(room, p, InputState.None, new(), new(), flags, p.MeleeHitbox, m, Dt);
        Assert.True(flags.IsSet("m"));
    }
}
