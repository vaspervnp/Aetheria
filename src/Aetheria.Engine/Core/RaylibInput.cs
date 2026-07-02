using Raylib_cs;

namespace Aetheria.Engine.Core;

/// <summary>
/// Reads the keyboard through Raylib into an <see cref="InputState"/>. Jump is a
/// dedicated key (Space/Z) distinct from Up (W/Up-arrow) so wall-climbing —
/// which holds Up against a wall — never collides with wall-jumping.
/// </summary>
public sealed class RaylibInput : IInputSource
{
    private static bool Down(KeyboardKey k) => Raylib.IsKeyDown(k);
    private static bool Pressed(KeyboardKey k) => Raylib.IsKeyPressed(k);
    private static bool Released(KeyboardKey k) => Raylib.IsKeyReleased(k);

    public InputState Poll()
    {
        float mx = 0f;
        if (Down(KeyboardKey.Left) || Down(KeyboardKey.A)) mx -= 1f;
        if (Down(KeyboardKey.Right) || Down(KeyboardKey.D)) mx += 1f;

        bool up = Down(KeyboardKey.Up) || Down(KeyboardKey.W);
        bool down = Down(KeyboardKey.Down) || Down(KeyboardKey.S);

        bool jumpPressed = Pressed(KeyboardKey.Space) || Pressed(KeyboardKey.Z);
        bool jumpHeld = Down(KeyboardKey.Space) || Down(KeyboardKey.Z);
        bool jumpReleased = Released(KeyboardKey.Space) || Released(KeyboardKey.Z);

        bool dash = Pressed(KeyboardKey.LeftShift) || Pressed(KeyboardKey.RightShift) || Pressed(KeyboardKey.K);
        bool attack = Pressed(KeyboardKey.J) || Pressed(KeyboardKey.X);
        bool phase = Down(KeyboardKey.LeftControl) || Down(KeyboardKey.RightControl) || Down(KeyboardKey.F);

        bool pause = Pressed(KeyboardKey.P) || Pressed(KeyboardKey.Escape);
        bool confirm = Pressed(KeyboardKey.Enter) || Pressed(KeyboardKey.Space);
        bool restart = Pressed(KeyboardKey.R);

        return new InputState(mx, up, down, jumpPressed, jumpHeld, jumpReleased,
            dash, attack, phase, pause, confirm, restart);
    }
}
