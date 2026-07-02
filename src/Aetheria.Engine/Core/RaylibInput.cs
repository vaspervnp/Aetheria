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
        bool secondary = Pressed(KeyboardKey.N);
        bool toggleMap = Pressed(KeyboardKey.Tab) || Pressed(KeyboardKey.M);

        // ---- gamepad 0 (layered over the keyboard) --------------------------
        if (Raylib.IsGamepadAvailable(0))
        {
            const int g = 0;
            float ax = Raylib.GetGamepadAxisMovement(g, GamepadAxis.LeftX);
            float ay = Raylib.GetGamepadAxisMovement(g, GamepadAxis.LeftY);
            if (ax < -0.35f || GpDown(GamepadButton.LeftFaceLeft)) mx = -1f;
            else if (ax > 0.35f || GpDown(GamepadButton.LeftFaceRight)) mx = 1f;
            up |= ay < -0.4f || GpDown(GamepadButton.LeftFaceUp);
            down |= ay > 0.4f || GpDown(GamepadButton.LeftFaceDown);

            jumpPressed |= GpPressed(GamepadButton.RightFaceDown);
            jumpHeld |= GpDown(GamepadButton.RightFaceDown);
            jumpReleased |= Raylib.IsGamepadButtonReleased(g, GamepadButton.RightFaceDown);
            dash |= GpPressed(GamepadButton.RightTrigger1);
            attack |= GpPressed(GamepadButton.RightFaceLeft);
            phase |= GpDown(GamepadButton.LeftTrigger1);
            pause |= GpPressed(GamepadButton.MiddleRight);
            confirm |= GpPressed(GamepadButton.RightFaceDown) || GpPressed(GamepadButton.MiddleRight);
            restart |= GpPressed(GamepadButton.MiddleLeft);
            secondary |= GpPressed(GamepadButton.RightFaceUp);
            toggleMap |= GpPressed(GamepadButton.RightFaceRight);
        }

        return new InputState(mx, up, down, jumpPressed, jumpHeld, jumpReleased,
            dash, attack, phase, pause, confirm, restart, secondary, toggleMap);
    }

    private static bool GpDown(GamepadButton b) => Raylib.IsGamepadButtonDown(0, b);
    private static bool GpPressed(GamepadButton b) => Raylib.IsGamepadButtonPressed(0, b);
}
