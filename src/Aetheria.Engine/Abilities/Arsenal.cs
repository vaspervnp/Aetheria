namespace Aetheria.Engine.Abilities;

public enum WeaponType
{
    Blaster,   // fast, low-damage single shot
    Scatter,   // short-range spread; shatters cracked walls
    Blade,     // melee arc; deflects projectiles, trips melee switches
}

/// <summary>Spark's weapon inventory: which weapons are unlocked and which is selected.</summary>
public sealed class Arsenal
{
    private readonly List<WeaponType> _order = new() { WeaponType.Blaster };
    private readonly HashSet<WeaponType> _have = new() { WeaponType.Blaster };

    public WeaponType Current { get; private set; } = WeaponType.Blaster;
    public IReadOnlyList<WeaponType> Owned => _order;
    public int Count => _order.Count;

    public bool Has(WeaponType w) => _have.Contains(w);

    public bool Unlock(WeaponType w)
    {
        if (!_have.Add(w)) return false;
        _order.Add(w);
        Current = w;
        return true;
    }

    public void Cycle()
    {
        if (_order.Count < 2) return;
        Current = _order[(_order.IndexOf(Current) + 1) % _order.Count];
    }

    public void Select(WeaponType w) { if (_have.Contains(w)) Current = w; }

    public void Clear()
    {
        _order.Clear(); _have.Clear();
        _order.Add(WeaponType.Blaster); _have.Add(WeaponType.Blaster);
        Current = WeaponType.Blaster;
    }

    public static string Name(WeaponType w) => w switch
    {
        WeaponType.Blaster => "Blaster",
        WeaponType.Scatter => "Scatter-Shot",
        WeaponType.Blade => "Plasma Blade",
        _ => w.ToString(),
    };
}
