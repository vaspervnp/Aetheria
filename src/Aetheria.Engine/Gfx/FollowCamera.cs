using System.Numerics;
using Raylib_cs;
using Aetheria.Engine.Core;

namespace Aetheria.Engine.Gfx;

/// <summary>
/// A smoothed follow camera clamped to the current room's bounds, with an
/// impulse screen-shake. Produces a Raylib <see cref="Camera2D"/> each frame.
/// </summary>
public sealed class FollowCamera
{
    private Vector2 _pos;
    private float _zoom = 1f;
    private float _shakeMag;
    private float _shakeTime;
    private float _t;

    public Vector2 Position => _pos;

    public void SnapTo(Vector2 center) => _pos = center;

    public void Shake(float magnitude, float time = 0.3f)
    {
        _shakeMag = MathF.Max(_shakeMag, magnitude);
        _shakeTime = MathF.Max(_shakeTime, time);
    }

    public void Update(Vector2 target, float roomW, float roomH, int screenW, int screenH, float dt)
    {
        _t += dt;
        _zoom = screenW / (float)GameConfig.VirtualWidth;

        float halfW = screenW * 0.5f / _zoom;
        float halfH = screenH * 0.5f / _zoom;

        float dx = roomW <= halfW * 2f ? roomW * 0.5f : Math.Clamp(target.X, halfW, roomW - halfW);
        float dy = roomH <= halfH * 2f ? roomH * 0.5f : Math.Clamp(target.Y, halfH, roomH - halfH);
        var desired = new Vector2(dx, dy);

        // exponential smoothing (frame-rate independent)
        float a = 1f - MathF.Exp(-dt * 12f);
        _pos += (desired - _pos) * a;

        if (_shakeTime > 0f) _shakeTime -= dt;
        else _shakeMag = 0f;
    }

    public Camera2D ToCamera2D(int screenW, int screenH)
    {
        Vector2 shake = Vector2.Zero;
        if (_shakeTime > 0f && _shakeMag > 0f)
        {
            // cheap deterministic wobble
            shake = new Vector2(
                MathF.Sin(_t * 63f) * _shakeMag,
                MathF.Cos(_t * 71f) * _shakeMag) * MathF.Min(1f, _shakeTime * 3f);
        }
        return new Camera2D
        {
            Offset = new Vector2(screenW * 0.5f, screenH * 0.5f),
            Target = _pos + shake,
            Rotation = 0f,
            Zoom = _zoom,
        };
    }
}
