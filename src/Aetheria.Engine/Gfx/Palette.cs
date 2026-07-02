using Raylib_cs;

namespace Aetheria.Engine.Gfx;

/// <summary>The cold, bio-mechanical colour language of the Abyss.</summary>
public static class Palette
{
    public static Color Rgb(int r, int g, int b, int a = 255) => new(r, g, b, a);

    // backdrop
    public static readonly Color Void = Rgb(6, 8, 16);
    public static readonly Color Nebula1 = Rgb(18, 28, 58);
    public static readonly Color Nebula2 = Rgb(44, 20, 58);
    public static readonly Color NebulaGlow = Rgb(30, 70, 90);

    // terrain (metal / bio panels)
    public static readonly Color MetalDark = Rgb(24, 30, 42);
    public static readonly Color MetalMid = Rgb(40, 50, 66);
    public static readonly Color MetalLight = Rgb(66, 82, 102);
    public static readonly Color Circuit = Rgb(52, 190, 210);
    public static readonly Color CircuitDim = Rgb(30, 90, 110);

    // energy / player
    public static readonly Color Spark = Rgb(130, 235, 255);
    public static readonly Color SparkCore = Rgb(240, 255, 255);
    public static readonly Color SparkTrail = Rgb(90, 200, 255);

    // hazards
    public static readonly Color Hazard = Rgb(255, 60, 110);
    public static readonly Color HazardDim = Rgb(150, 20, 60);

    // phase matter
    public static readonly Color Phase = Rgb(150, 95, 220);
    public static readonly Color PhaseGlow = Rgb(200, 165, 255);

    // pickups
    public static readonly Color Pickup = Rgb(255, 200, 90);
    public static readonly Color PickupGlow = Rgb(255, 244, 190);

    // core
    public static readonly Color Core = Rgb(120, 255, 210);
    public static readonly Color CoreGlow = Rgb(220, 255, 240);

    // enemies
    public static readonly Color Crawler = Rgb(210, 80, 96);
    public static readonly Color Floater = Rgb(120, 175, 255);
    public static readonly Color Sentinel = Rgb(240, 140, 70);
    public static readonly Color EnemyEye = Rgb(255, 245, 210);

    // ui
    public static readonly Color Ink = Rgb(206, 230, 240);
    public static readonly Color InkDim = Rgb(120, 150, 168);
    public static readonly Color Panel = Rgb(12, 16, 26, 210);
    public static readonly Color HealthOn = Rgb(120, 235, 255);
    public static readonly Color HealthOff = Rgb(40, 54, 70);
    public static readonly Color EnergyOn = Rgb(255, 200, 90);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            (int)(a.A + (b.A - a.A) * t));
    }
}
