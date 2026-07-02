using System.Numerics;
using Raylib_cs;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Gfx;

/// <summary>Screen-space heads-up display: health, energy, abilities, room name.</summary>
public static class Hud
{
    public static void Draw(Player player, World.World world, int screenW, int screenH)
    {
        int pad = 18;

        // --- health pips ---
        int pip = 20, gap = 6;
        for (int i = 0; i < player.MaxHealth; i++)
        {
            int x = pad + i * (pip + gap);
            var rect = new Rectangle(x, pad, pip, pip);
            bool on = i < player.Health;
            Raylib.DrawRectangleRounded(rect, 0.4f, 4, on ? Palette.HealthOn : Palette.HealthOff);
            if (on)
            {
                Raylib.BeginBlendMode(BlendMode.Additive);
                Raylib.DrawRectangleRounded(rect, 0.4f, 4, Palette.Rgb(120, 235, 255, 60));
                Raylib.EndBlendMode();
            }
        }

        // --- energy bar ---
        int barY = pad + pip + 8, barW = 160, barH = 10;
        Raylib.DrawRectangleRounded(new Rectangle(pad, barY, barW, barH), 0.5f, 4, Palette.HealthOff);
        float e = Math.Clamp(player.Energy / GameConfig.MaxEnergy, 0f, 1f);
        if (e > 0.01f)
            Raylib.DrawRectangleRounded(new Rectangle(pad, barY, barW * e, barH), 0.5f, 4, Palette.EnergyOn);
        Raylib.DrawText("AETHER", pad + barW + 8, barY - 2, 12, Palette.InkDim);

        // --- ability icons ---
        int ay = barY + barH + 10;
        int ax = pad;
        foreach (var ab in AbilitySet.All)
        {
            bool has = player.Abilities.Has(ab);
            var c = has ? AbilityColor(ab) : Palette.HealthOff;
            var box = new Rectangle(ax, ay, 22, 22);
            Raylib.DrawRectangleRounded(box, 0.3f, 4, Palette.Rgb(14, 18, 28, 220));
            Raylib.DrawRectangleLinesEx(box, 1.5f, c);
            Raylib.DrawText(Glyph(ab), ax + 7, ay + 5, 14, c);
            if (has && ab == AbilityType.Dash)
            {
                // dash cooldown sweep
                float cd = player.DashCooldownFraction;
                if (cd > 0f)
                    Raylib.DrawRectangle(ax, ay + (int)(22 * (1 - cd)), 22, (int)(22 * cd),
                        Palette.Rgb(0, 0, 0, 120));
            }
            ax += 28;
        }

        // --- room name (bottom centre) ---
        string name = world.Current.Name.ToUpperInvariant();
        int fs = 18;
        int tw = Raylib.MeasureText(name, fs);
        Raylib.DrawText(name, (screenW - tw) / 2, screenH - 34, fs, Palette.InkDim);
    }

    private static Color AbilityColor(AbilityType a) => a switch
    {
        AbilityType.DoubleJump => Palette.Spark,
        AbilityType.Dash => Palette.Circuit,
        AbilityType.WallClimb => Palette.Pickup,
        AbilityType.Phase => Palette.Phase,
        _ => Palette.Ink,
    };

    private static string Glyph(AbilityType a) => a switch
    {
        AbilityType.DoubleJump => "^",
        AbilityType.Dash => ">",
        AbilityType.WallClimb => "|",
        AbilityType.Phase => "*",
        _ => "?",
    };
}
