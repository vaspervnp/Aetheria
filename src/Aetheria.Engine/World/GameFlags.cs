namespace Aetheria.Engine.World;

/// <summary>
/// Global, persistable game state: a string→int dictionary tracking solved
/// puzzles, opened doors, thrown switches, and collected keys. Ints double as
/// booleans (0/1) and as small counters (e.g. sequence-puzzle progress). Raises
/// <see cref="Changed"/> so doors/puzzles can react.
/// </summary>
public sealed class GameFlags
{
    private readonly Dictionary<string, int> _values = new();

    public event Action<string, int>? Changed;

    public int Get(string key) => _values.TryGetValue(key, out var v) ? v : 0;
    public bool IsSet(string key) => Get(key) != 0;

    public void Set(string key, int value)
    {
        if (Get(key) == value) return;
        _values[key] = value;
        Changed?.Invoke(key, value);
    }

    public void SetFlag(string key) => Set(key, 1);
    public void Increment(string key, int by = 1) => Set(key, Get(key) + by);

    public IReadOnlyDictionary<string, int> All => _values;
    public IEnumerable<KeyValuePair<string, int>> NonZero => _values.Where(kv => kv.Value != 0);

    public void Clear() => _values.Clear();

    public void LoadFrom(IEnumerable<KeyValuePair<string, int>> data)
    {
        _values.Clear();
        foreach (var kv in data) _values[kv.Key] = kv.Value;
    }
}
