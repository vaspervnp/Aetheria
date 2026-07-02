namespace Aetheria.Engine.Core;

/// <summary>
/// An immutable snapshot of player intent for a single frame. This is the ONLY
/// thing the simulation consumes for control, which lets the exact same
/// <c>Player</c>/<c>World</c> update code run under the real Raylib input source
/// and under scripted head-less tests.
/// </summary>
public readonly struct InputState
{
    /// <summary>Horizontal intent, -1 (left) .. +1 (right).</summary>
    public readonly float MoveX;
    public readonly bool Up;
    public readonly bool Down;

    public readonly bool JumpPressed;   // edge: pressed this frame
    public readonly bool JumpHeld;      // level: currently down
    public readonly bool JumpReleased;  // edge: released this frame

    public readonly bool DashPressed;
    public readonly bool AttackPressed;
    public readonly bool PhaseHeld;

    // UI / meta
    public readonly bool Pause;
    public readonly bool Confirm;
    public readonly bool Restart;

    public InputState(
        float moveX = 0f, bool up = false, bool down = false,
        bool jumpPressed = false, bool jumpHeld = false, bool jumpReleased = false,
        bool dashPressed = false, bool attackPressed = false, bool phaseHeld = false,
        bool pause = false, bool confirm = false, bool restart = false)
    {
        MoveX = moveX;
        Up = up;
        Down = down;
        JumpPressed = jumpPressed;
        JumpHeld = jumpHeld;
        JumpReleased = jumpReleased;
        DashPressed = dashPressed;
        AttackPressed = attackPressed;
        PhaseHeld = phaseHeld;
        Pause = pause;
        Confirm = confirm;
        Restart = restart;
    }

    public static readonly InputState None = new();

    /// <summary>Convenience for tests: hold a horizontal direction.</summary>
    public static InputState Move(float x, bool jumpHeld = false)
        => new(moveX: x, jumpHeld: jumpHeld);

    /// <summary>Convenience for tests: a fresh jump press (also counts as held).</summary>
    public static InputState Jump(float x = 0f)
        => new(moveX: x, jumpPressed: true, jumpHeld: true);
}

/// <summary>Abstraction over a source of per-frame input.</summary>
public interface IInputSource
{
    InputState Poll();
}
