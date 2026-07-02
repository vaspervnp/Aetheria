using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>
/// "Spark" — the player character. Owns the full platforming state machine:
/// acceleration/friction, variable-height jumps with coyote time and jump
/// buffering, double jump, dash, wall slide/climb/jump and matter phasing.
///
/// The update loop is a pure function of (InputState, TileMap, dt) so it is
/// exercised directly by unit tests and the head-less smoke harness — it never
/// touches Raylib.
/// </summary>
public sealed class Player : Entity
{
    private const float PhaseDrain = 34f;   // energy/sec while phasing
    private const float PhaseMinEnergy = 6f;

    public readonly AbilitySet Abilities = new();
    public readonly Arsenal Weapons = new();

    // ---- survivability ------------------------------------------------------
    public int Health { get; private set; } = GameConfig.MaxHealth;
    public int MaxHealth => GameConfig.MaxHealth;
    public float Energy { get; private set; } = GameConfig.MaxEnergy;
    public bool Alive => Health > 0;

    // ---- contact / state flags (refreshed every Update) --------------------
    public bool OnGround { get; private set; }
    public bool AtCeiling { get; private set; }
    public bool OnWallLeft { get; private set; }
    public bool OnWallRight { get; private set; }
    public bool WallSliding { get; private set; }
    public bool Dashing => _dashTimer > 0f;
    public bool Phasing { get; private set; }
    public bool Invulnerable => _invulnTimer > 0f || Dashing;

    // ---- one-shot event flags (true for the frame they happen) -------------
    public bool JustJumped { get; private set; }
    public bool JustDoubleJumped { get; private set; }
    public bool JustWallJumped { get; private set; }
    public bool JustDashed { get; private set; }
    public bool JustLanded { get; private set; }
    public bool JustHurt { get; private set; }
    public bool JustDied { get; private set; }
    public bool JustFired { get; private set; }
    public bool JustBladed { get; private set; }

    // ---- combat intent (consumed by CombatSystem) --------------------------
    public bool WantsFire { get; private set; }
    public WeaponType FireWeapon { get; private set; }
    public Vector2 FireOrigin { get; private set; }
    public int FireDir { get; private set; } = 1;

    /// <summary>Active melee (Plasma Blade) hitbox this frame, or null.</summary>
    public Aabb? MeleeHitbox { get; private set; }
    public bool Blading => _bladeTimer > 0f;

    // ---- internal timers ----------------------------------------------------
    private float _coyote;
    private float _jumpBuffer;
    private bool _hasDoubleJumped;
    private float _dashTimer;
    private float _dashCooldown;
    private int _dashDir = 1;
    private float _wallJumpLock;
    private float _invulnTimer;
    private float _pulseCooldown;
    private float _bladeTimer;
    private bool _wasOnGround;

    public float DashCooldownFraction =>
        GameConfig.DashCooldown <= 0 ? 0 : Math.Clamp(_dashCooldown / GameConfig.DashCooldown, 0f, 1f);

    public Player(Vector2 spawn) : base(spawn, 10f, 14f) { }

    /// <summary>Full reset (new life): restore health/energy and clear timers.</summary>
    public void Respawn(Vector2 spawn)
    {
        Position = new Vector2(spawn.X - Width * 0.5f, spawn.Y - Height * 0.5f);
        Velocity = Vector2.Zero;
        Health = GameConfig.MaxHealth;
        Energy = GameConfig.MaxEnergy;
        _coyote = _jumpBuffer = _dashTimer = _dashCooldown = 0f;
        _wallJumpLock = _invulnTimer = _pulseCooldown = _bladeTimer = 0f;
        _hasDoubleJumped = false;
        Phasing = false;
        MeleeHitbox = null;
        Active = true;
    }

    /// <summary>Reposition without a full reset (room transition): keep health/abilities.</summary>
    public void PlaceAt(Vector2 spawn, bool keepVelocity = false)
    {
        Position = new Vector2(spawn.X - Width * 0.5f, spawn.Y - Height * 0.5f);
        if (!keepVelocity) Velocity = Vector2.Zero;
        _dashTimer = 0f;
        Phasing = false;
    }

    public void Heal(int amount) => Health = Math.Min(MaxHealth, Health + amount);
    public void RefillEnergy() => Energy = GameConfig.MaxEnergy;

    /// <summary>Externally mark the player grounded (e.g. standing on a push block).</summary>
    public void MarkGrounded()
    {
        OnGround = true;
        _coyote = GameConfig.CoyoteTime;
        _hasDoubleJumped = false;
    }

    public void Update(InputState input, TileMap map, float dt)
    {
        ClearEventFlags();
        TickTimers(dt);

        if (!Alive) { return; }

        UpdateDash(input, dt);
        UpdatePhasing(input, dt);
        UpdateHorizontal(input, dt);
        UpdateJump(input, dt);
        UpdateGravity(input, dt);

        // Integrate position against the tilemap (axis-separated, sub-stepped).
        OnGround = false;
        AtCeiling = false;
        MoveX(map, Velocity.X * dt);
        MoveY(map, Velocity.Y * dt, input.Down);

        UpdateContacts(map, input);
        UpdateHazards(map);
        UpdateEnergyAndCombat(input, dt);

        // landing detection for SFX/particles
        if (OnGround && !_wasOnGround) JustLanded = true;
        _wasOnGround = OnGround;
        if (OnGround)
        {
            _coyote = GameConfig.CoyoteTime;
            _hasDoubleJumped = false;
        }
        if (input.MoveX > 0.01f) Facing = 1;
        else if (input.MoveX < -0.01f) Facing = -1;

        MeleeHitbox = _bladeTimer > 0f ? BladeBox() : null;
    }

    private Aabb BladeBox()
    {
        const float bw = 22f, bh = 18f;
        float bx = Facing > 0 ? Position.X + Width - 2f : Position.X - bw + 2f;
        return new Aabb(bx, Center.Y - bh * 0.5f, bw, bh);
    }

    private void ClearEventFlags()
    {
        JustJumped = JustDoubleJumped = JustWallJumped = JustDashed = false;
        JustLanded = JustHurt = JustDied = WantsFire = JustFired = JustBladed = false;
    }

    private void TickTimers(float dt)
    {
        if (_coyote > 0) _coyote -= dt;
        if (_jumpBuffer > 0) _jumpBuffer -= dt;
        if (_dashTimer > 0) _dashTimer -= dt;
        if (_dashCooldown > 0) _dashCooldown -= dt;
        if (_wallJumpLock > 0) _wallJumpLock -= dt;
        if (_invulnTimer > 0) _invulnTimer -= dt;
        if (_pulseCooldown > 0) _pulseCooldown -= dt;
        if (_bladeTimer > 0) _bladeTimer -= dt;
    }

    private void UpdateDash(InputState input, float dt)
    {
        if (input.DashPressed && Abilities.Has(AbilityType.Dash) && _dashCooldown <= 0 && !Dashing)
        {
            _dashDir = input.MoveX > 0.1f ? 1 : input.MoveX < -0.1f ? -1 : Facing;
            Facing = _dashDir;
            _dashTimer = GameConfig.DashTime;
            _dashCooldown = GameConfig.DashCooldown;
            JustDashed = true;
        }

        if (Dashing)
        {
            Velocity = new Vector2(_dashDir * GameConfig.DashSpeed, 0f);
        }
    }

    private void UpdatePhasing(InputState input, float dt)
    {
        bool wants = input.PhaseHeld && Abilities.Has(AbilityType.Phase) && Energy > PhaseMinEnergy;
        Phasing = wants;
        if (Phasing) Energy = Math.Max(0f, Energy - PhaseDrain * dt);
    }

    private void UpdateHorizontal(InputState input, float dt)
    {
        if (Dashing) return;               // dash controls velocity itself
        if (_wallJumpLock > 0) return;     // preserve the push-off from a wall jump

        float target = input.MoveX * GameConfig.MaxRunSpeed;
        float accel = OnGround ? GameConfig.GroundAccel : GameConfig.AirAccel;
        float friction = OnGround ? GameConfig.GroundFriction : GameConfig.AirFriction;

        if (MathF.Abs(input.MoveX) > 0.01f)
        {
            Velocity.X = MoveToward(Velocity.X, target, accel * dt);
        }
        else
        {
            Velocity.X = MoveToward(Velocity.X, 0f, friction * dt);
        }
    }

    private void UpdateJump(InputState input, float dt)
    {
        if (input.JumpPressed) _jumpBuffer = GameConfig.JumpBufferTime;

        bool canWall = Abilities.Has(AbilityType.WallClimb) && !OnGround && (OnWallLeft || OnWallRight);

        if (_jumpBuffer > 0)
        {
            if (OnGround || _coyote > 0)
            {
                Velocity.Y = -GameConfig.JumpSpeed;
                _coyote = 0;
                _jumpBuffer = 0;
                _hasDoubleJumped = false;
                JustJumped = true;
            }
            else if (canWall)
            {
                int wallDir = OnWallLeft ? -1 : 1;    // wall is on this side
                Velocity.Y = -GameConfig.WallJumpY;
                Velocity.X = -wallDir * GameConfig.WallJumpX;
                Facing = -wallDir;
                _wallJumpLock = GameConfig.WallJumpLockTime;
                _jumpBuffer = 0;
                _hasDoubleJumped = false;
                JustWallJumped = true;
            }
            else if (Abilities.Has(AbilityType.DoubleJump) && !_hasDoubleJumped && !Dashing)
            {
                Velocity.Y = -GameConfig.DoubleJumpSpeed;
                _hasDoubleJumped = true;
                _jumpBuffer = 0;
                JustDoubleJumped = true;
            }
        }

        // Variable jump height: releasing early cuts the rise.
        if (input.JumpReleased && Velocity.Y < 0f)
            Velocity.Y *= GameConfig.JumpCutMultiplier;
    }

    private void UpdateGravity(InputState input, float dt)
    {
        if (Dashing) return;

        bool climbing = Abilities.Has(AbilityType.WallClimb) && (OnWallLeft || OnWallRight) && !OnGround
                        && ((OnWallLeft && input.MoveX < -0.1f) || (OnWallRight && input.MoveX > 0.1f));

        if (climbing && input.Up)
        {
            Velocity.Y = -GameConfig.WallClimbSpeed;   // scale the wall
            return;
        }

        Velocity.Y += GameConfig.Gravity * dt;
        if (Velocity.Y > GameConfig.MaxFallSpeed) Velocity.Y = GameConfig.MaxFallSpeed;

        // Wall slide: cap downward speed when clinging to a wall.
        if (climbing && Velocity.Y > GameConfig.WallSlideSpeed)
            Velocity.Y = GameConfig.WallSlideSpeed;
    }

    private void UpdateContacts(TileMap map, InputState input)
    {
        // Thin side probes, inset vertically so floor/ceiling corners don't count.
        OnWallLeft = map.OverlapsSolid(new Aabb(Position.X - 1.5f, Position.Y + 2f, 1.5f, Height - 4f), Phasing);
        OnWallRight = map.OverlapsSolid(new Aabb(Position.X + Width, Position.Y + 2f, 1.5f, Height - 4f), Phasing);

        WallSliding = Abilities.Has(AbilityType.WallClimb) && !OnGround && Velocity.Y > 0f &&
                      ((OnWallLeft && input.MoveX < -0.1f) || (OnWallRight && input.MoveX > 0.1f));
    }

    private void UpdateHazards(TileMap map)
    {
        if (map.OverlapsHazard(Bounds))
        {
            int away = Center.X < map.PixelWidth * 0.5f ? 1 : -1;
            TakeDamage(GameConfig.HazardDamage, new Vector2(away, -1f));
        }
    }

    private void UpdateEnergyAndCombat(InputState input, float dt)
    {
        if (!Phasing)
            Energy = Math.Min(GameConfig.MaxEnergy, Energy + GameConfig.EnergyRegen * dt);

        if (input.SwitchWeapon) Weapons.Cycle();

        if (!input.AttackPressed || _pulseCooldown > 0f) return;

        var w = Weapons.Current;
        float cost = w switch
        {
            WeaponType.Blaster => GameConfig.BlasterCost,
            WeaponType.Scatter => GameConfig.ScatterCost,
            _ => GameConfig.BladeCost,
        };
        if (Energy < cost) return;
        Energy -= cost;
        FireDir = Facing;
        FireOrigin = new Vector2(Center.X + Facing * (Width * 0.5f + 3f), Center.Y);

        switch (w)
        {
            case WeaponType.Blaster:
                _pulseCooldown = GameConfig.BlasterCooldown;
                WantsFire = true; FireWeapon = WeaponType.Blaster; JustFired = true;
                break;
            case WeaponType.Scatter:
                _pulseCooldown = GameConfig.ScatterCooldown;
                WantsFire = true; FireWeapon = WeaponType.Scatter; JustFired = true;
                break;
            case WeaponType.Blade:
                _pulseCooldown = GameConfig.BladeCooldown;
                _bladeTimer = GameConfig.BladeTime;
                JustBladed = true;
                break;
        }
    }

    /// <summary>Apply damage. Returns true if it landed (not invulnerable/dead).</summary>
    public bool TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (!Alive || Invulnerable) return false;
        Health -= amount;
        _invulnTimer = GameConfig.InvulnTime;
        if (knockbackDir != Vector2.Zero)
        {
            var k = Vector2.Normalize(knockbackDir);
            Velocity = new Vector2(k.X * GameConfig.Knockback, -MathF.Abs(k.Y) * GameConfig.Knockback * 0.7f - 60f);
        }
        JustHurt = true;
        if (Health <= 0)
        {
            Health = 0;
            JustDied = true;
        }
        return true;
    }

    // ---- collision integration ---------------------------------------------

    private void MoveX(TileMap map, float dx)
    {
        if (dx == 0f) return;
        float remaining = dx;
        float step = map.TileSize * 0.9f;
        while (MathF.Abs(remaining) > 0.0001f)
        {
            float move = Math.Clamp(remaining, -step, step);
            Position.X += move;
            Aabb b = Bounds;
            if (map.OverlapsSolid(b, Phasing))
            {
                if (move > 0)
                {
                    int tileX = map.WorldToTileX(b.Right - 0.001f);
                    Position.X = tileX * map.TileSize - Width;
                }
                else
                {
                    int tileX = map.WorldToTileX(b.Left);
                    Position.X = (tileX + 1) * map.TileSize;
                }
                Velocity.X = 0f;
                return;
            }
            remaining -= move;
        }
    }

    private void MoveY(TileMap map, float dy, bool dropThrough)
    {
        if (dy == 0f) return;
        float remaining = dy;
        float step = map.TileSize * 0.9f;
        while (MathF.Abs(remaining) > 0.0001f)
        {
            float move = Math.Clamp(remaining, -step, step);
            float prevBottom = Position.Y + Height;
            Position.Y += move;
            Aabb b = Bounds;

            bool hitSolid = map.OverlapsSolid(b, Phasing);
            bool hitOneWay = move > 0 && !hitSolid && !dropThrough && map.RestsOnOneWay(b, prevBottom);

            if (hitSolid || hitOneWay)
            {
                if (move > 0)
                {
                    int tileY = map.WorldToTileY(b.Bottom - 0.001f);
                    Position.Y = tileY * map.TileSize - Height;
                    OnGround = true;
                }
                else
                {
                    int tileY = map.WorldToTileY(b.Top);
                    Position.Y = (tileY + 1) * map.TileSize;
                    AtCeiling = true;
                }
                Velocity.Y = 0f;
                return;
            }
            remaining -= move;
        }
    }

    private static float MoveToward(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta) return target;
        return current + MathF.Sign(target - current) * maxDelta;
    }
}
