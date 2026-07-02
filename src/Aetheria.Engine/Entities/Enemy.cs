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
    private int _mode;          // charger state: 0 patrol, 1 windup, 2 charge, 3 recover
    private float _modeTimer;
    private float _repathTimer;
    private GridPoint? _pathNext;

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
            EnemyKind.Charger => (3, 60f),
            EnemyKind.Warden => (22, 30f),
            EnemyKind.HoverTurret => (3, 22f),
            EnemyKind.StalkerDrone => (2, 152f),
            EnemyKind.ArmoredCrawler => (4, 42f),
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
        EnemyKind.Charger => new Vector2(16, 14),
        EnemyKind.Warden => new Vector2(30, 30),
        EnemyKind.HoverTurret => new Vector2(18, 16),
        EnemyKind.StalkerDrone => new Vector2(14, 14),
        EnemyKind.ArmoredCrawler => new Vector2(18, 14),
        _ => new Vector2(14, 14),
    };

    public bool IsBoss => Kind == EnemyKind.Warden;
    public bool Charging => Kind == EnemyKind.Charger && _mode == 2;
    public bool WindingUp => Kind == EnemyKind.Charger && _mode == 1;
    public bool Enraged => Kind == EnemyKind.Warden && Health * 2 <= MaxHealth;
    public float HealthFraction => MaxHealth <= 0 ? 0f : Health / (float)MaxHealth;

    /// <summary>Armored Crawlers ignore attacks that land on their facing (front) side.</summary>
    public bool ArmoredFront => Kind == EnemyKind.ArmoredCrawler;
    public bool IsFrontalHit(float attackerX) => Math.Sign(attackerX - Center.X) == Facing;

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
            case EnemyKind.Charger: UpdateCharger(map, player, dt); break;
            case EnemyKind.Warden: UpdateWarden(map, player, dt, projectiles); break;
            case EnemyKind.ArmoredCrawler: UpdateCrawler(map, dt); break;
            case EnemyKind.HoverTurret: UpdateHoverTurret(map, player, dt, projectiles); break;
            case EnemyKind.StalkerDrone: UpdateStalkerDrone(map, player, dt); break;
        }
    }

    private void UpdateHoverTurret(TileMap map, Player player, float dt, List<Projectile> projectiles)
    {
        // hovers in place with a gentle bob, aims where the player is heading
        float ny = Home.Y + MathF.Sin(_t * 1.6f) * 6f;
        Position = new Vector2(Home.X - Width * 0.5f, ny - Height * 0.5f);
        Facing = player.Center.X >= Center.X ? 1 : -1;

        _fireTimer -= dt;
        float dist = Vector2.Distance(Center, player.Center);
        if (_fireTimer <= 0f && dist < 320f && player.Alive && LineOfSight(map, Center, player.Center))
        {
            _fireTimer = 1.5f;
            const float projSpeed = 155f;
            float lead = dist / projSpeed;                       // predictive aim
            Vector2 aim = player.Center + player.Velocity * lead;
            Vector2 dir = aim - Center;
            dir = dir.LengthSquared() < 0.01f ? new Vector2(Facing, 0f) : Vector2.Normalize(dir);
            projectiles.Add(new Projectile(Center, dir * projSpeed, 3.5f, 3f, 1, fromPlayer: false));
        }
    }

    private void UpdateStalkerDrone(TileMap map, Player player, float dt)
    {
        _repathTimer -= dt;
        var myTile = new GridPoint(map.WorldToTileX(Center.X), map.WorldToTileY(Center.Y));
        var goal = new GridPoint(map.WorldToTileX(player.Center.X), map.WorldToTileY(player.Center.Y));
        if (_repathTimer <= 0f)
        {
            _pathNext = Pathfinder.NextStep(map, myTile, goal);
            _repathTimer = 0.1f;
        }

        Vector2 aim = _pathNext is { } n
            ? new Vector2((n.X + 0.5f) * map.TileSize, (n.Y + 0.5f) * map.TileSize)
            : player.Center;
        Vector2 dir = aim - Center;
        dir = dir.LengthSquared() < 1f ? Vector2.Zero : Vector2.Normalize(dir);
        Velocity = dir * _speed;
        Facing = Velocity.X >= 0 ? 1 : -1;

        TileCollider.MoveX(map, ref Position, ref Velocity, Width, Height, Velocity.X * dt);
        TileCollider.MoveY(map, ref Position, ref Velocity, Width, Height, Velocity.Y * dt);
    }

    private static bool LineOfSight(TileMap map, Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Math.Max(1, (int)(dist / (map.TileSize * 0.5f)));
        for (int i = 1; i < steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
            if (map.IsSolidTile(map.WorldToTileX(p.X), map.WorldToTileY(p.Y))) return false;
        }
        return true;
    }

    private void UpdateCharger(TileMap map, Player player, float dt)
    {
        Velocity.Y += GameConfig.Gravity * dt;
        if (Velocity.Y > GameConfig.MaxFallSpeed) Velocity.Y = GameConfig.MaxFallSpeed;
        var (_, ground, _) = TileCollider.MoveY(map, ref Position, ref Velocity, Width, Height, Velocity.Y * dt);
        _grounded = ground;

        switch (_mode)
        {
            case 0: // patrol
                if (Center.X < Home.X - Range) Facing = 1;
                else if (Center.X > Home.X + Range) Facing = -1;
                TurnAtLedge(map);
                Velocity.X = Facing * _speed;
                float dx = player.Center.X - Center.X;
                float dy = MathF.Abs(player.Center.Y - Center.Y);
                if (player.Alive && _grounded && MathF.Abs(dx) < Range * 1.5f && dy < 1.6f * map.TileSize)
                {
                    Facing = dx >= 0 ? 1 : -1;
                    _mode = 1; _modeTimer = 0.45f; Velocity.X = 0f;   // wind up (telegraph)
                }
                break;
            case 1: // windup
                Velocity.X = 0f;
                _modeTimer -= dt;
                if (_modeTimer <= 0f) { _mode = 2; _modeTimer = 0.8f; }
                break;
            case 2: // charge
                Velocity.X = Facing * 255f;
                _modeTimer -= dt;
                if (_modeTimer <= 0f || WouldFallAhead(map)) { _mode = 3; _modeTimer = 0.5f; Velocity.X = 0f; }
                break;
            case 3: // recover
                Velocity.X = MoveToward(Velocity.X, 0f, 500f * dt);
                _modeTimer -= dt;
                if (_modeTimer <= 0f) _mode = 0;
                break;
        }

        bool hitWall = TileCollider.MoveX(map, ref Position, ref Velocity, Width, Height, Velocity.X * dt);
        if (hitWall)
        {
            if (_mode == 2) { _mode = 3; _modeTimer = 0.5f; }
            else Facing = -Facing;
        }
    }

    private void UpdateWarden(TileMap map, Player player, float dt, List<Projectile> projectiles)
    {
        float targetX = Math.Clamp(player.Center.X, Home.X - Range, Home.X + Range);
        float nx = Center.X + Math.Clamp(targetX - Center.X, -_speed * dt, _speed * dt);
        float ny = Home.Y + MathF.Sin(_t * 1.4f) * 10f;
        Position = new Vector2(nx - Width * 0.5f, ny - Height * 0.5f);
        Facing = player.Center.X >= Center.X ? 1 : -1;

        _fireTimer -= dt;
        if (_fireTimer <= 0f && player.Alive && Vector2.Distance(Center, player.Center) < 460f)
        {
            _fireTimer = Enraged ? 1.05f : 1.9f;              // enrage below half health
            FireFan(player, projectiles, Enraged ? 5 : 3, Enraged ? 34f : 22f, 130f);
        }
    }

    private void FireFan(Player player, List<Projectile> projectiles, int count, float spreadDeg, float speed)
    {
        Vector2 dir = player.Center - Center;
        dir = dir.LengthSquared() < 0.01f ? new Vector2(Facing, 0f) : Vector2.Normalize(dir);
        float baseAng = MathF.Atan2(dir.Y, dir.X);
        float spread = spreadDeg * MathF.PI / 180f;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float ang = baseAng + (t - 0.5f) * spread;
            var v = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * speed;
            projectiles.Add(new Projectile(Center, v, 4f, 3.6f, 1, fromPlayer: false));
        }
    }

    private void TurnAtLedge(TileMap map)
    {
        if (!_grounded) return;
        float frontX = Facing > 0 ? Bounds.Right + 1f : Bounds.Left - 1f;
        int ftx = map.WorldToTileX(frontX);
        int fty = map.WorldToTileY(Bounds.Bottom + 1f);
        if (!map.IsSolidTile(ftx, fty)) Facing = -Facing;
    }

    private bool WouldFallAhead(TileMap map)
    {
        if (!_grounded) return false;
        float frontX = Facing > 0 ? Bounds.Right + 2f : Bounds.Left - 2f;
        int ftx = map.WorldToTileX(frontX);
        int fty = map.WorldToTileY(Bounds.Bottom + 1f);
        return !map.IsSolidTile(ftx, fty);
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
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
