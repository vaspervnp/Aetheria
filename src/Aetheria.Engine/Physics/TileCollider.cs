using System.Numerics;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Physics;

/// <summary>Contact flags produced by a collision-resolved move.</summary>
public struct MoveResult
{
    public bool HitX;
    public bool HitY;
    public bool OnGround;
    public bool AtCeiling;
}

/// <summary>
/// Simple axis-separated, sub-stepped tile collision for non-player entities
/// (enemies, debris). No phasing or one-way support — just solid walls/floors.
/// </summary>
public static class TileCollider
{
    public static bool MoveX(TileMap map, ref Vector2 pos, ref Vector2 vel, float w, float h, float dx)
    {
        if (dx == 0f) return false;
        float remaining = dx;
        float step = map.TileSize * 0.9f;
        while (MathF.Abs(remaining) > 0.0001f)
        {
            float move = Math.Clamp(remaining, -step, step);
            pos.X += move;
            var b = new Aabb(pos.X, pos.Y, w, h);
            if (map.OverlapsSolid(b))
            {
                if (move > 0)
                {
                    int tx = map.WorldToTileX(b.Right - 0.001f);
                    pos.X = tx * map.TileSize - w;
                }
                else
                {
                    int tx = map.WorldToTileX(b.Left);
                    pos.X = (tx + 1) * map.TileSize;
                }
                vel.X = 0f;
                return true;
            }
            remaining -= move;
        }
        return false;
    }

    public static (bool hit, bool ground, bool ceiling) MoveY(
        TileMap map, ref Vector2 pos, ref Vector2 vel, float w, float h, float dy)
    {
        if (dy == 0f) return (false, false, false);
        float remaining = dy;
        float step = map.TileSize * 0.9f;
        while (MathF.Abs(remaining) > 0.0001f)
        {
            float move = Math.Clamp(remaining, -step, step);
            pos.Y += move;
            var b = new Aabb(pos.X, pos.Y, w, h);
            if (map.OverlapsSolid(b))
            {
                bool ground = move > 0, ceiling = move < 0;
                if (ground)
                {
                    int ty = map.WorldToTileY(b.Bottom - 0.001f);
                    pos.Y = ty * map.TileSize - h;
                }
                else
                {
                    int ty = map.WorldToTileY(b.Top);
                    pos.Y = (ty + 1) * map.TileSize;
                }
                vel.Y = 0f;
                return (true, ground, ceiling);
            }
            remaining -= move;
        }
        return (false, false, false);
    }
}
