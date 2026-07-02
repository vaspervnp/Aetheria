namespace Aetheria.Engine.Core;

/// <summary>
/// A head-less input source that replays a scripted sequence of frames. Used by
/// the <c>--smoke</c> self-test harness and by unit tests to drive the real
/// simulation deterministically. When the script is exhausted it keeps
/// returning <see cref="InputState.None"/> so the loop never faults.
/// </summary>
public sealed class ScriptedInputSource : IInputSource
{
    private readonly List<InputState> _frames = new();
    private int _index;

    public ScriptedInputSource() { }

    public ScriptedInputSource(IEnumerable<InputState> frames) => _frames.AddRange(frames);

    public int Count => _frames.Count;
    public int Index => _index;
    public bool Exhausted => _index >= _frames.Count;

    /// <summary>Append the same input for <paramref name="frames"/> frames.</summary>
    public ScriptedInputSource Hold(InputState state, int frames)
    {
        for (int i = 0; i < frames; i++) _frames.Add(state);
        return this;
    }

    public ScriptedInputSource Add(InputState state)
    {
        _frames.Add(state);
        return this;
    }

    public ScriptedInputSource Wait(int frames) => Hold(InputState.None, frames);

    public InputState Poll()
    {
        if (_index < _frames.Count) return _frames[_index++];
        return InputState.None;
    }

    public void Reset() => _index = 0;
}
