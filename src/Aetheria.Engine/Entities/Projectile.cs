using System.Numerics;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>A small moving hitbox: Spark's energy pulse, or an enemy's shot.</summary>
public sealed class Projectile
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Life;
    public int Damage;
    public bool FromPlayer;
    public bool Dead;
    public float Age;
    public bool BreaksWalls;   // Scatter pellets shatter cracked walls

    public Projectile(Vector2 pos, Vector2 vel, float radius, float life, int damage, bool fromPlayer)
    {
        Position = pos;
        Velocity = vel;
        Radius = radius;
        Life = life;
        Damage = damage;
        FromPlayer = fromPlayer;
    }

    public Aabb Bounds => new(Position.X - Radius, Position.Y - Radius, Radius * 2f, Radius * 2f);

    public void Update(TileMap map, float dt)
    {
        Age += dt;
        Position += Velocity * dt;
        Life -= dt;
        if (Life <= 0f) Dead = true;
        // despawn on hitting solid geometry; Scatter pellets shatter cracked walls
        if (map.OverlapsSolid(new Aabb(Position.X - 1, Position.Y - 1, 2, 2)))
        {
            if (BreaksWalls)
            {
                int tx = map.WorldToTileX(Position.X), ty = map.WorldToTileY(Position.Y);
                if (map.Get(tx, ty) == TileType.Cracked) map.Set(tx, ty, TileType.Empty);
            }
            Dead = true;
        }
    }
}
