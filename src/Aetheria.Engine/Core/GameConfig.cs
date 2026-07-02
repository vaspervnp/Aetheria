namespace Aetheria.Engine.Core;

/// <summary>
/// Central tunables for the whole game. All physics values are expressed in
/// pixels and seconds so they are frame-rate independent (everything is scaled
/// by delta time). Kept in one place so the feel of the game can be tweaked
/// without hunting through the code base.
/// </summary>
public static class GameConfig
{
    // ---- Rendering / world scale -------------------------------------------
    public const int TileSize = 16;
    public const int VirtualWidth = 480;   // logical render width  (30 tiles)
    public const int VirtualHeight = 270;  // logical render height (~17 tiles)
    public const int WindowWidth = 1280;
    public const int WindowHeight = 720;
    public const string Title = "Aetheria: The Bio-Mechanical Abyss";

    // ---- Gravity / falling --------------------------------------------------
    public const float Gravity = 1600f;         // px/s^2
    public const float MaxFallSpeed = 720f;      // px/s
    public const float WallSlideSpeed = 90f;     // px/s (max downward on wall)

    // ---- Horizontal movement ------------------------------------------------
    public const float MaxRunSpeed = 190f;       // px/s
    public const float GroundAccel = 2100f;      // px/s^2
    public const float GroundFriction = 2600f;   // px/s^2
    public const float AirAccel = 1500f;         // px/s^2
    public const float AirFriction = 700f;       // px/s^2

    // ---- Jumping ------------------------------------------------------------
    public const float JumpSpeed = 470f;         // initial upward speed
    public const float DoubleJumpSpeed = 430f;
    public const float JumpCutMultiplier = 0.45f; // velocity kept on early release
    public const float CoyoteTime = 0.10f;        // grace after leaving ledge
    public const float JumpBufferTime = 0.12f;    // grace before landing

    // ---- Wall jump ----------------------------------------------------------
    public const float WallJumpX = 250f;
    public const float WallJumpY = 430f;
    public const float WallJumpLockTime = 0.14f;  // horizontal control lock
    public const float WallClimbSpeed = 92f;      // upward climb with ability

    // ---- Dash ---------------------------------------------------------------
    public const float DashSpeed = 500f;
    public const float DashTime = 0.18f;
    public const float DashCooldown = 0.45f;

    // ---- Combat / survivability --------------------------------------------
    public const int MaxHealth = 5;
    public const float MaxEnergy = 100f;
    public const float EnergyRegen = 22f;         // per second
    public const float PulseCost = 18f;
    public const float PulseCooldown = 0.28f;
    public const int PulseDamage = 2;
    public const int DashDamage = 1;
    public const float InvulnTime = 1.0f;         // after taking damage
    public const int ContactDamage = 1;
    public const int HazardDamage = 1;
    public const float Knockback = 240f;
}
