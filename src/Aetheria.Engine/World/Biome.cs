namespace Aetheria.Engine.World;

/// <summary>
/// The distinct zones of the Abyss. Each biome drives its own procedural
/// rendering (palette, tile shapes, background) and enemy/hazard mix. Kept as a
/// plain enum here so the world model stays free of any rendering dependency.
/// </summary>
public enum Biome
{
    RustVents,        // corroded iron, steam, ember light — the entry zone
    CrystalConduits,  // glassy teal/violet lattices — the mid zone
    Mainframe,        // cold circuitry and datastreams — the deep zone / Core
}

public static class Biomes
{
    public static readonly Biome[] All = { Biome.RustVents, Biome.CrystalConduits, Biome.Mainframe };

    public static string DisplayName(Biome b) => b switch
    {
        Biome.RustVents => "The Rust Vents",
        Biome.CrystalConduits => "The Crystal Conduits",
        Biome.Mainframe => "The Mainframe",
        _ => b.ToString(),
    };
}
