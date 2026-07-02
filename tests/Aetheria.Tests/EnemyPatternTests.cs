using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;
using Xunit;

namespace Aetheria.Tests;

public class EnemyPatternTests
{
    private const float Dt = 1f / 60f;
    private const int Tile = GameConfig.TileSize;

    private static TileMap FlatRoom(int w = 60)
    {
        var m = new TileMap(w, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(0, 22, m.Width - 1, m.Height - 1, TileType.Solid);
        return m;
    }

    [Fact]
    public void ChargerStaysOnGroundWithinTheMap()
    {
        var m = FlatRoom();
        var e = new Enemy(EnemyKind.Charger, new Vector2(30 * Tile, 21 * Tile), 6 * Tile);
        var player = new Player(new Vector2(45 * Tile, 21 * Tile)); // far away → mostly patrols
        var projs = new List<Projectile>();
        for (int i = 0; i < 1500; i++)
        {
            e.Update(m, player, Dt, projs);
            Assert.True(e.Bounds.Bottom <= 22 * Tile + 1f, $"charger left the floor at frame {i}");
            Assert.InRange(e.Center.X, Tile, (m.Width - 1) * Tile);
        }
    }

    [Fact]
    public void ChargerChargesWhenPlayerIsInLine()
    {
        var m = FlatRoom();
        var e = new Enemy(EnemyKind.Charger, new Vector2(30 * Tile, 21 * Tile), 6 * Tile);
        var player = new Player(new Vector2(34 * Tile, 21 * Tile)); // same level, in range
        var projs = new List<Projectile>();
        bool charged = false;
        for (int i = 0; i < 150 && !charged; i++)
        {
            e.Update(m, player, Dt, projs);
            charged = e.Charging;
        }
        Assert.True(charged, "charger never entered its charge state");
    }

    [Fact]
    public void ChargerDoesNotChargeOffALedge()
    {
        // floor only tiles 10..20; a charger that hunts must not run off the end.
        var m = new TileMap(40, 24, Tile);
        m.Border(TileType.Solid);
        m.Fill(10, 22, 20, 23, TileType.Solid);
        var e = new Enemy(EnemyKind.Charger, new Vector2(15 * Tile, 21 * Tile), 20 * Tile);
        var player = new Player(new Vector2(19 * Tile, 21 * Tile));
        var projs = new List<Projectile>();
        for (int i = 0; i < 1200; i++)
        {
            e.Update(m, player, Dt, projs);
            Assert.InRange(e.Center.X, 10 * Tile - 2f, 21 * Tile + 2f);
        }
    }

    [Fact]
    public void WardenFiresAProjectileFan()
    {
        var m = FlatRoom();
        var e = new Enemy(EnemyKind.Warden, new Vector2(30 * Tile, 10 * Tile), 3 * Tile);
        var player = new Player(new Vector2(30 * Tile, 13 * Tile));
        var projs = new List<Projectile>();
        for (int i = 0; i < 200; i++) e.Update(m, player, Dt, projs);
        // at least one 3-shot fan of enemy projectiles
        Assert.True(projs.Count(p => !p.FromPlayer) >= 3, "warden never fired a fan");
    }

    [Fact]
    public void WardenIsToughAndEnragesBelowHalf()
    {
        var e = new Enemy(EnemyKind.Warden, new Vector2(0, 0), 32);
        Assert.True(e.MaxHealth >= 15);
        Assert.False(e.Enraged);

        // whittle it below half (respecting hit-invulnerability between blows)
        int guard = 0;
        while (e.HealthFraction > 0.49f && e.AliveState && guard++ < 500)
        {
            e.TakeDamage(1, Vector2.Zero);
            e.Update(FlatRoom(), new Player(new Vector2(0, 0)), Dt, new List<Projectile>());
        }
        Assert.True(e.Enraged);
        Assert.True(e.AliveState);
    }
}
