namespace Aetheria.Engine.Abilities;

/// <summary>The traversal abilities Spark can reclaim through the Abyss.</summary>
public enum AbilityType
{
    DoubleJump,
    Dash,
    WallClimb,
    Phase,
}

/// <summary>
/// Tracks which abilities the player has unlocked. This is the Metroidvania
/// "inventory" that gates progression. Deliberately tiny and rendering-free so
/// it is trivially testable.
/// </summary>
public sealed class AbilitySet
{
    private readonly HashSet<AbilityType> _unlocked = new();

    public static readonly IReadOnlyList<AbilityType> All = new[]
    {
        AbilityType.DoubleJump,
        AbilityType.Dash,
        AbilityType.WallClimb,
        AbilityType.Phase,
    };

    public bool Has(AbilityType ability) => _unlocked.Contains(ability);

    /// <summary>Unlock an ability. Returns true if it was newly unlocked.</summary>
    public bool Unlock(AbilityType ability) => _unlocked.Add(ability);

    public int Count => _unlocked.Count;

    public bool HasAll => _unlocked.Count >= All.Count;

    public IEnumerable<AbilityType> Unlocked => _unlocked;

    public void Clear() => _unlocked.Clear();

    public static string DisplayName(AbilityType a) => a switch
    {
        AbilityType.DoubleJump => "Double Jump",
        AbilityType.Dash => "Phase Dash",
        AbilityType.WallClimb => "Wall Cling",
        AbilityType.Phase => "Matter Phasing",
        _ => a.ToString(),
    };

    public static string Description(AbilityType a) => a switch
    {
        AbilityType.DoubleJump => "Discharge a second time in mid-air.",
        AbilityType.Dash => "Blink forward through the aether.",
        AbilityType.WallClimb => "Cling to and scale conduit walls.",
        AbilityType.Phase => "Slip through phase-locked matter.",
        _ => string.Empty,
    };
}
