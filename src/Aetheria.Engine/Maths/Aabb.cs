using System.Numerics;

namespace Aetheria.Engine.Maths;

/// <summary>
/// Axis-aligned bounding box defined by a minimum corner (X, Y) and a size
/// (W, H). Pure value type with no rendering dependencies so it can be used
/// freely from unit tests.
/// </summary>
public readonly struct Aabb
{
    public readonly float X;
    public readonly float Y;
    public readonly float W;
    public readonly float H;

    public Aabb(float x, float y, float w, float h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public float Left => X;
    public float Top => Y;
    public float Right => X + W;
    public float Bottom => Y + H;
    public float CenterX => X + W * 0.5f;
    public float CenterY => Y + H * 0.5f;
    public Vector2 Center => new(CenterX, CenterY);
    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(W, H);

    public static Aabb FromCenter(Vector2 center, float w, float h)
        => new(center.X - w * 0.5f, center.Y - h * 0.5f, w, h);

    public static Aabb FromCenter(float cx, float cy, float w, float h)
        => new(cx - w * 0.5f, cy - h * 0.5f, w, h);

    /// <summary>Overlap test using strict inequalities (touching edges do not count).</summary>
    public bool Intersects(in Aabb o)
        => X < o.Right && Right > o.X && Y < o.Bottom && Bottom > o.Y;

    /// <summary>Overlap test that treats shared edges as intersecting.</summary>
    public bool Touches(in Aabb o)
        => X <= o.Right && Right >= o.X && Y <= o.Bottom && Bottom >= o.Y;

    public bool Contains(Vector2 p)
        => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public bool Contains(float px, float py)
        => px >= X && px <= Right && py >= Y && py <= Bottom;

    public Aabb Translated(float dx, float dy) => new(X + dx, Y + dy, W, H);
    public Aabb Translated(Vector2 d) => new(X + d.X, Y + d.Y, W, H);

    /// <summary>Grow (or shrink, for negative values) the box on all sides.</summary>
    public Aabb Expanded(float amount)
        => new(X - amount, Y - amount, W + amount * 2f, H + amount * 2f);

    /// <summary>Signed overlap depth on each axis with another box (0 if no overlap).</summary>
    public float OverlapX(in Aabb o) => MathF.Min(Right, o.Right) - MathF.Max(X, o.X);
    public float OverlapY(in Aabb o) => MathF.Min(Bottom, o.Bottom) - MathF.Max(Y, o.Y);

    public override string ToString() => $"Aabb({X:0.##},{Y:0.##},{W:0.##},{H:0.##})";
}
