using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Physics;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>
/// A procedurally-generated heavy block Spark can shove across the floor onto a
/// pressure plate. Falls under gravity, collides with the tilemap, and is a
/// dynamic solid to the player (resolved by <see cref="PuzzleSystem"/>).
/// </summary>
public sealed class PushBlock : Entity
{
    public const float PushSpeed = 70f;
    public bool Grounded { get; private set; }

    public PushBlock(Vector2 center) : base(Vector2.Zero, 18f, 18f)
        => Position = new Vector2(center.X - Width * 0.5f, center.Y - Height * 0.5f);

    public void Update(TileMap map, float dt)
    {
        Velocity.Y += GameConfig.Gravity * dt;
        if (Velocity.Y > GameConfig.MaxFallSpeed) Velocity.Y = GameConfig.MaxFallSpeed;
        var (_, ground, _) = TileCollider.MoveY(map, ref Position, ref Velocity, Width, Height, Velocity.Y * dt);
        Grounded = ground;
        // heavy: bleed off horizontal speed quickly
        Velocity.X = MathF.Abs(Velocity.X) <= 900f * dt ? 0f : Velocity.X - MathF.Sign(Velocity.X) * 900f * dt;
    }

    /// <summary>Try to slide horizontally; returns the distance actually moved.</summary>
    public float TryPush(TileMap map, float dx)
    {
        float before = Position.X;
        TileCollider.MoveX(map, ref Position, ref Velocity, Width, Height, dx);
        return Position.X - before;
    }
}
