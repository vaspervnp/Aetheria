using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.Physics;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>
/// A corrupted guardian algorithm. Three behaviours share one class:
///  - <b>Crawler</b>: gravity-bound ground patroller that turns at walls/ledges.
///  - <b>Floater</b>: drifts on sine waves through open air.
///  - <b>Sentinel</b>: tougher hovering turret that tracks Spark and fires.
/// </summary>
public sealed class Enemy : Entity
{
    public EnemyKind Kind { get; }
    public int Health { get; private set; }
    public int MaxHealth { get; }
    public int ContactDamage { get; } = GameConfig.ContactDamage;
    public bool AliveState => Health > 0;
    public bool JustHit { get; private set; }
    public bool JustKilled { get; private set; }

    public Vector2 Home { get; }
    public float Range { get; set; } = 64f;

    private float _t;
    private float _fireTimer;
    private float _hitInvuln;
    private bool _grounded;

    private readonly float _speed;

    public Enemy(EnemyKind kind, Vector2 center, float range)
        : base(Vector2.Zero, SizeFor(kind).X, SizeFor(kind).Y)
    {
        Kind = kind;
        Home = center;
        Range = range;
        Position = new Vector2(center.X - Width * 0.5f, center.Y - Height * 0.5f);
        (MaxHealth, _speed) = kind switch
        {
            EnemyKind.Crawler => (2, 52f),
            EnemyKind.Floater => (2, 42f),
            EnemyKind.Sentinel => (6, 34f),
            _ => (2, 50f),
        };
        Health = MaxHealth;
        Facing = -1;
        _fireTimer = 1.2f;
    }

    private static Vector2 SizeFor(EnemyKind kind) => kind switch
    {
        EnemyKind.Crawler => new Vector2(14, 12),
        EnemyKind.Floater => new Vector2(13, 13),
        EnemyKind.Sentinel => new Vector2(20, 20),
        _ => new Vector2(14, 14),
    };

    public static Enemy FromSpawn(EnemySpawn spawn, int tileSize)
        => new(spawn.Kind, spawn.WorldCenter(tileSize), spawn.Range * tileSize);

    public bool TakeDamage(int amount, Vector2 knockback)
    {
        if (!AliveState || _hitInvuln > 0f) return false;
        Health -= amount;
        _hitInvuln = 0.14f;
        Velocity += knockback;
        JustHit = true;
        if (Health <= 0) { Health = 0; JustKilled = true; }
        return true;
    }

    public void Update(TileMap map, Player player, float dt, List<Projectile> projectiles)
    {
        JustHit = false;
        JustKilled = false;
        if (!AliveState) return;
        _t += dt;
        if (_hitInvuln > 0) _hitInvuln -= dt;

        switch (Kind)
        {
            case EnemyKind.Crawler: UpdateCrawler(map, dt); break;
            case EnemyKind.Floater: UpdateFloater(map, dt); break;
            case EnemyKind.Sentinel: UpdateSentinel(map, player, dt, projectiles); break;
        }
    }

    private void UpdateCrawler(TileMap map, float dt)
    {
        // gravity
        Velocity.Y += GameConfig.Gravity * dt;
        if (Velocity.Y > GameConfig.MaxFallSpeed) Velocity.Y = GameConfig.MaxFallSpeed;
        var (_, ground, _) = TileCollider.MoveY(map, ref Position, ref Velocity, Width, Height, Velocity.Y * dt);
        _grounded = ground;

        // patrol bounds
        if (Center.X < Home.X - Range) Facing = 1;
        else if (Center.X > Home.X + Range) Facing = -1;

        // don't walk off ledges or into walls
        if (_grounded)
        {
            float frontX = Facing > 0 ? Bounds.Right + 1f : Bounds.Left - 1f;
            int ftx = map.WorldToTileX(frontX);
            int fty = map.WorldToTileY(Bounds.Bottom + 1f);
            if (!map.IsSolidTile(ftx, fty)) Facing = -Facing; // ledge
        }

        Velocity.X = Facing * _speed;
        bool hitWall = TileCollider.MoveX(map, ref Position, ref Velocity, Width, Height, Velocity.X * dt);
        if (hitWall) Facing = -Facing;
    }

    private void UpdateFloater(TileMap map, float dt)
    {
        float prevX = Center.X;
        float nx = Home.X + MathF.Sin(_t * 1.6f) * Range;
        float ny = Home.Y + MathF.Sin(_t * 2.4f) * 10f;
        // keep clear of solids: clamp to map interior
        nx = Math.Clamp(nx, map.TileSize * 1.5f, map.PixelWidth - map.TileSize * 1.5f);
        ny = Math.Clamp(ny, map.TileSize * 1.5f, map.PixelHeight - map.TileSize * 1.5f);
        Position = new Vector2(nx - Width * 0.5f, ny - Height * 0.5f);
        Facing = nx >= prevX ? 1 : -1;
    }

    private void UpdateSentinel(TileMap map, Player player, float dt, List<Projectile> projectiles)
    {
        // hover, tracking the player's X within the patrol range
        float targetX = Math.Clamp(player.Center.X, Home.X - Range, Home.X + Range);
        float nx = Center.X + Math.Clamp(targetX - Center.X, -_speed * dt, _speed * dt);
        float ny = Home.Y + MathF.Sin(_t * 1.8f) * 8f;
        Position = new Vector2(nx - Width * 0.5f, ny - Height * 0.5f);
        Facing = player.Center.X >= Center.X ? 1 : -1;

        _fireTimer -= dt;
        float dist = Vector2.Distance(Center, player.Center);
        if (_fireTimer <= 0f && dist < 240f && player.Alive)
        {
            _fireTimer = 1.7f;
            Vector2 dir = player.Center - Center;
            if (dir.LengthSquared() > 0.01f) dir = Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Center, dir * 135f, 3.5f, 3.2f, 1, fromPlayer: false));
        }
    }
}
