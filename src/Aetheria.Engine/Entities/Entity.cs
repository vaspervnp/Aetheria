using System.Numerics;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.Entities;

/// <summary>
/// Base for everything that lives in the world: a position (top-left of the
/// collision box), a velocity, a size, and a facing direction. Pure simulation
/// state — no rendering.
/// </summary>
public abstract class Entity
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Width;
    public float Height;
    public int Facing = 1;      // +1 right, -1 left
    public bool Active = true;

    protected Entity(Vector2 position, float width, float height)
    {
        Position = position;
        Width = width;
        Height = height;
    }

    public Aabb Bounds => new(Position.X, Position.Y, Width, Height);
    public Vector2 Center => new(Position.X + Width * 0.5f, Position.Y + Height * 0.5f);

    public void CenterOn(Vector2 point)
    {
        Position = new Vector2(point.X - Width * 0.5f, point.Y - Height * 0.5f);
    }
}
