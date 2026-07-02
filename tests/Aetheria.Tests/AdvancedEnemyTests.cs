using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class AdvancedEnemyTests
{
    private const float Dt = 1f / 60f;
    private const int T = GameConfig.TileSize;

    private static TileMap OpenRoom(int w = 40, int h = 24)
    {
        var m = new TileMap(w, h, T);
        m.Border(TileType.Solid);
        m.Fill(0, h - 2, w - 1, h - 1, TileType.Solid);
        return m;
    }

    [Fact]
    public void HoverTurretFiresAtNearbyPlayer()
    {
        var m = OpenRoom();
        var turret = new Enemy(EnemyKind.HoverTurret, new Vector2(20 * T, 8 * T), 2 * T);
        var player = new Player(new Vector2(24 * T, 8 * T)); // in range, clear line of sight
        var projs = new List<Projectile>();
        bool fired = false;
        for (int i = 0; i < 200 && !fired; i++)
        {
            turret.Update(m, player, Dt, projs);
            fired = projs.Exists(p => !p.FromPlayer);
        }
        Assert.True(fired, "turret never fired");
    }

    [Fact]
    public void HoverTurretLeadsAMovingTarget()
    {
        var m = OpenRoom();
        var turret = new Enemy(EnemyKind.HoverTurret, new Vector2(20 * T, 8 * T), 2 * T);
        // player moving right fast, directly across from the turret
        var player = new Player(new Vector2(26 * T, 8 * T));
        player.Velocity = new Vector2(180, 0);
        var projs = new List<Projectile>();
        Projectile? shot = null;
        for (int i = 0; i < 200 && shot == null; i++)
        {
            turret.Update(m, player, Dt, projs);
            shot = projs.FirstOrDefault(p => !p.FromPlayer);
            player.Velocity = new Vector2(180, 0); // keep it moving (Update isn't driving it here)
        }
        Assert.NotNull(shot);
        // predictive aim → the shot has a rightward (lead) component toward where the player is going
        Assert.True(shot!.Velocity.X > 5f, "shot did not lead the moving player");
    }

    [Fact]
    public void StalkerDroneChasesAndClosesDistance()
    {
        var m = OpenRoom(48, 24);
        var drone = new Enemy(EnemyKind.StalkerDrone, new Vector2(6 * T, 8 * T), 4 * T);
        var player = new Player(new Vector2(40 * T, 8 * T));
        var projs = new List<Projectile>();
        float startDist = Vector2.Distance(drone.Center, player.Center);
        for (int i = 0; i < 240; i++)
        {
            drone.Update(m, player, Dt, projs);
            Assert.InRange(drone.Center.X, 0f, m.PixelWidth);   // stays in bounds
            Assert.InRange(drone.Center.Y, 0f, m.PixelHeight);
        }
        Assert.True(Vector2.Distance(drone.Center, player.Center) < startDist - 5 * T, "drone did not close in");
    }

    [Fact]
    public void StalkerDroneRoutesAroundAWall()
    {
        var m = OpenRoom(48, 24);
        // a tall wall between drone (left) and player (right), with a gap at the top
        m.Fill(24, 4, 24, 21, TileType.Solid);
        var drone = new Enemy(EnemyKind.StalkerDrone, new Vector2(10 * T, 18 * T), 4 * T);
        var player = new Player(new Vector2(38 * T, 18 * T));
        var projs = new List<Projectile>();
        for (int i = 0; i < 600; i++) drone.Update(m, player, Dt, projs);
        // it should have found its way to the player's side of the wall
        Assert.True(drone.Center.X > 24 * T, "drone never got around the wall");
    }

    [Fact]
    public void ArmoredCrawlerBlocksFrontalHitsButNotRearHits()
    {
        var m = OpenRoom();
        var player = new Player(new Vector2(2 * T, 2 * T));
        var e = new Enemy(EnemyKind.ArmoredCrawler, new Vector2(20 * T, 20 * T), 4 * T);
        e.Facing = 1; // faces right
        Assert.True(e.ArmoredFront);

        // frontal shot (coming from the right, moving left) is deflected
        var front = new Projectile(new Vector2(e.Center.X + 5, e.Center.Y), new Vector2(-200, 0), 4, 1f, 3, fromPlayer: true);
        var projectiles = new List<Projectile> { front };
        int hp = e.Health;
        CombatSystem.Step(player, new List<Enemy> { e }, projectiles, m, Dt);
        Assert.Equal(hp, e.Health);

        // rear shot (from behind, on its left) damages it
        var rear = new Projectile(new Vector2(e.Center.X - 5, e.Center.Y), new Vector2(200, 0), 4, 1f, 3, fromPlayer: true);
        var projectiles2 = new List<Projectile> { rear };
        CombatSystem.Step(player, new List<Enemy> { e }, projectiles2, m, Dt);
        Assert.True(e.Health < hp, "rear hit should damage the armored crawler");
    }
}
