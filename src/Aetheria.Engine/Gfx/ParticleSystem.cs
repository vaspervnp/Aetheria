using System.Numerics;
using Raylib_cs;
using Aetheria.Engine.Core;

namespace Aetheria.Engine.Gfx;

public struct Particle
{
    public Vector2 Pos;
    public Vector2 Vel;
    public float Life;
    public float MaxLife;
    public float Size;
    public Color Color;
    public float Gravity;
    public bool Additive;
}

/// <summary>Lightweight fire-and-forget particle pool for juice: trails, bursts, motes.</summary>
public sealed class ParticleSystem
{
    private readonly List<Particle> _particles = new();
    private readonly Rng _rng = new(0xA37C);

    public int Count => _particles.Count;

    public void Clear() => _particles.Clear();

    public void Add(Vector2 pos, Vector2 vel, float life, float size, Color color,
                    float gravity = 0f, bool additive = true)
    {
        if (_particles.Count > 1200) return;
        _particles.Add(new Particle
        {
            Pos = pos, Vel = vel, Life = life, MaxLife = life,
            Size = size, Color = color, Gravity = gravity, Additive = additive,
        });
    }

    public void Burst(Vector2 pos, int count, Color color, float speed, float life,
                      float size = 2.4f, float gravity = 0f, bool additive = true)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = _rng.Range(0f, MathF.Tau);
            float sp = _rng.Range(speed * 0.3f, speed);
            var vel = new Vector2(MathF.Cos(ang) * sp, MathF.Sin(ang) * sp);
            Add(pos, vel, _rng.Range(life * 0.6f, life), _rng.Range(size * 0.6f, size), color, gravity, additive);
        }
    }

    public void Trail(Vector2 pos, Color color, float size = 3f)
    {
        var jitter = new Vector2(_rng.Range(-2f, 2f), _rng.Range(-2f, 2f));
        Add(pos + jitter, new Vector2(_rng.Range(-8f, 8f), _rng.Range(-8f, 8f)), 0.35f, size, color, 0f, true);
    }

    public void Update(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f) { _particles.RemoveAt(i); continue; }
            p.Vel.Y += p.Gravity * dt;
            p.Pos += p.Vel * dt;
            _particles[i] = p;
        }
    }

    public void Draw()
    {
        // normal-blend particles first
        foreach (var p in _particles)
        {
            if (p.Additive) continue;
            DrawOne(p);
        }
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var p in _particles)
        {
            if (!p.Additive) continue;
            DrawOne(p);
        }
        Raylib.EndBlendMode();
    }

    private static void DrawOne(in Particle p)
    {
        float k = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
        var c = new Color((int)p.Color.R, (int)p.Color.G, (int)p.Color.B, (int)(p.Color.A * k));
        Raylib.DrawCircleV(p.Pos, p.Size * (0.4f + 0.6f * k), c);
    }
}
