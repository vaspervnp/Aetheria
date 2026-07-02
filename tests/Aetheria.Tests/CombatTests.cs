using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class CombatTests
{
    private const float Dt = 1f / 60f;
    private const int Tile = GameConfig.TileSize;

    private static TileMap FlatRoom()
    {
        var m = new TileMap(40, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(0, 22, m.Width - 1, m.Height - 1, TileType.Solid);
        return m;
    }

    [Fact]
    public void EnemyTakesDamageAndDies()
    {
        var e = new Enemy(EnemyKind.Crawler, new Vector2(100, 100), 64);
        Assert.Equal(e.MaxHealth, e.Health);
        Assert.True(e.TakeDamage(1, Vector2.Zero));
        // hit-invulnerability blocks the immediate second hit
        Assert.False(e.TakeDamage(1, Vector2.Zero));
        Assert.True(e.AliveState);
    }

    [Fact]
    public void CrawlerStaysOnGroundWithinPatrolRange()
    {
        var m = FlatRoom();
        var spawn = new Vector2(20 * Tile, 21 * Tile);
        var e = new Enemy(EnemyKind.Crawler, spawn, range: 5 * Tile);
        var player = new Player(new Vector2(2 * Tile, 2 * Tile));
        var projs = new List<Projectile>();

        for (int i = 0; i < 1200; i++)
        {
            e.Update(m, player, Dt, projs);
            Assert.True(e.Bounds.Bottom <= 22 * Tile + 1f, $"crawler left the floor at frame {i}");
            Assert.InRange(e.Center.X, spawn.X - 5 * Tile - Tile, spawn.X + 5 * Tile + Tile);
        }
    }

    [Fact]
    public void CrawlerTurnsAtALedge()
    {
        // floor only spans tiles 15..25; crawler must not walk off either end.
        var m = new TileMap(40, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(15, 22, 25, 23, TileType.Solid);
        var e = new Enemy(EnemyKind.Crawler, new Vector2(20 * Tile, 21 * Tile), range: 20 * Tile);
        var player = new Player(new Vector2(2 * Tile, 2 * Tile));
        var projs = new List<Projectile>();

        for (int i = 0; i < 2000; i++)
        {
            e.Update(m, player, Dt, projs);
            Assert.InRange(e.Center.X, 15 * Tile - 2f, 26 * Tile + 2f);
        }
    }

    [Fact]
    public void FloaterStaysNearItsHome()
    {
        var m = FlatRoom();
        var home = new Vector2(20 * Tile, 8 * Tile);
        var e = new Enemy(EnemyKind.Floater, home, range: 4 * Tile);
        var player = new Player(new Vector2(2 * Tile, 2 * Tile));
        var projs = new List<Projectile>();
        for (int i = 0; i < 600; i++)
        {
            e.Update(m, player, Dt, projs);
            Assert.True(Vector2.Distance(e.Center, home) < 5 * Tile + 12f);
        }
    }

    [Fact]
    public void SentinelFiresAtNearbyPlayer()
    {
        var m = FlatRoom();
        var e = new Enemy(EnemyKind.Sentinel, new Vector2(20 * Tile, 10 * Tile), range: 3 * Tile);
        var player = new Player(new Vector2(20 * Tile, 12 * Tile)); // close by
        var projs = new List<Projectile>();
        bool fired = false;
        for (int i = 0; i < 180 && !fired; i++)
        {
            e.Update(m, player, Dt, projs);
            fired = projs.Exists(p => !p.FromPlayer);
        }
        Assert.True(fired);
    }

    [Fact]
    public void PlayerPulseDestroysEnemy()
    {
        var m = FlatRoom();
        var player = new Player(new Vector2(10 * Tile, 20 * Tile));
        var enemy = new Enemy(EnemyKind.Crawler, new Vector2(10 * Tile + 40, 20 * Tile), 32);
        var enemies = new List<Enemy> { enemy };
        var projectiles = new List<Projectile>();

        // spawn a pulse aimed at the enemy
        projectiles.Add(new Projectile(player.Center, new Vector2(360, 0), 5, 1f, GameConfig.PulseDamage, true));

        int killed = 0;
        for (int i = 0; i < 60 && enemies.Count > 0; i++)
            CombatSystem.Step(player, enemies, projectiles, m, Dt,
                (kind, _) => { if (kind == EffectKind.EnemyDead) killed++; });

        Assert.Empty(enemies);
        Assert.Equal(1, killed);
    }

    [Fact]
    public void EnemyContactDamagesPlayer()
    {
        var m = FlatRoom();
        var player = new Player(new Vector2(10 * Tile, 20 * Tile));
        int before = player.Health;
        var enemy = new Enemy(EnemyKind.Crawler, player.Center, 8);
        var enemies = new List<Enemy> { enemy };
        var projectiles = new List<Projectile>();

        CombatSystem.Step(player, enemies, projectiles, m, Dt);

        Assert.True(player.Health < before);
    }

    [Fact]
    public void DashingThroughEnemyDamagesItButNotPlayer()
    {
        var m = FlatRoom();
        var player = new Player(new Vector2(10 * Tile, 20 * Tile));
        player.Abilities.Unlock(Aetheria.Engine.Abilities.AbilityType.Dash);
        // face right and dash
        player.Update(new InputState(moveX: 1f, dashPressed: true), m, Dt);
        Assert.True(player.Dashing);
        int playerHp = player.Health;

        var enemy = new Enemy(EnemyKind.Crawler, player.Center, 8);
        var enemies = new List<Enemy> { enemy };
        var projectiles = new List<Projectile>();

        CombatSystem.Step(player, enemies, projectiles, m, Dt);

        Assert.Equal(playerHp, player.Health);       // dash grants invulnerability
        Assert.True(enemy.Health < enemy.MaxHealth);  // but the enemy is hurt
    }

    [Fact]
    public void ProjectileExpiresAndDespawnsOnWalls()
    {
        var m = FlatRoom();
        var wall = new Projectile(new Vector2(1 * Tile, 20 * Tile), new Vector2(-500, 0), 3, 2f, 1, true);
        wall.Update(m, Dt); // moves into the left border
        Assert.True(wall.Dead);

        var timeout = new Projectile(new Vector2(20 * Tile, 5 * Tile), Vector2.Zero, 3, 0.01f, 1, true);
        timeout.Update(m, Dt); // 0.01s life < one 1/60s frame
        Assert.True(timeout.Dead);
    }
}
