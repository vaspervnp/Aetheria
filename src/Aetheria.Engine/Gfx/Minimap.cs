using Raylib_cs;

namespace Aetheria.Engine.Gfx;

/// <summary>
/// A compact overview of the (linear) room chain in the top-right corner:
/// visited rooms lit, the current room highlighted, the Core flagged, and
/// unvisited rooms shown only faintly.
/// </summary>
public static class Minimap
{
    public static void Draw(World.World world, int screenW, int screenH)
    {
        int count = world.Rooms.Count;
        const int node = 16, gap = 10, h = 16;
        int totalW = count * node + (count - 1) * gap;
        int x0 = screenW - totalW - 20;
        int y0 = 20;

        // backing panel
        Raylib.DrawRectangleRounded(
            new Rectangle(x0 - 10, y0 - 10, totalW + 20, h + 20), 0.3f, 6, Palette.Panel);

        for (int i = 0; i < count; i++)
        {
            if (!world.Rooms.TryGetValue(i, out var room)) continue;
            int x = x0 + i * (node + gap);
            var box = new Rectangle(x, y0, node, h);

            if (i > 0) // connector
                Raylib.DrawLineEx(new System.Numerics.Vector2(x - gap, y0 + h / 2f),
                    new System.Numerics.Vector2(x, y0 + h / 2f), 2f, Palette.InkDim);

            bool visited = world.Visited.Contains(i);
            bool current = i == world.CurrentRoomId;
            Color fill = current ? Palette.Spark
                       : room.IsCore ? Palette.Core
                       : visited ? Palette.Rgb(60, 90, 120)
                       : Palette.Rgb(30, 38, 52);

            if (visited || current)
                Raylib.DrawRectangleRounded(box, 0.3f, 4, fill);
            Raylib.DrawRectangleLinesEx(box, current ? 2f : 1f,
                current ? Palette.SparkCore : (visited ? Palette.InkDim : Palette.Rgb(50, 60, 76)));

            if (room.IsCore)
                Raylib.DrawCircle(x + node / 2, y0 + h / 2, 3f,
                    visited ? Palette.CoreGlow : Palette.Rgb(60, 100, 88));
        }
    }
}
