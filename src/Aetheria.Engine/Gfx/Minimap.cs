using Raylib_cs;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Gfx;

/// <summary>
/// A 2D grid minimap in the top-right corner. Shows the rooms Spark has visited,
/// laid out by their grid cell, biome-coloured, connected by their doors, with
/// the current room highlighted and the Core flagged.
/// </summary>
public static class Minimap
{
    private const int Cell = 9, Gap = 3, Step = Cell + Gap;
    private const int HalfCols = 6, HalfRows = 4;   // visible radius around the current room

    public static void Draw(World.World world, int screenW, int screenH)
    {
        int panelW = (HalfCols * 2 + 1) * Step + Gap;
        int panelH = (HalfRows * 2 + 1) * Step + Gap;
        int px = screenW - panelW - 16, py = 16;
        Raylib.DrawRectangleRounded(new Rectangle(px, py, panelW, panelH), 0.12f, 6, Palette.Panel);

        var cur = world.CurrentCell;
        int cx = px + panelW / 2, cy = py + panelH / 2;

        foreach (var cell in world.Visited)
        {
            int gx = cell.X - cur.X, gy = cell.Y - cur.Y;
            if (Math.Abs(gx) > HalfCols || Math.Abs(gy) > HalfRows) continue;
            if (!world.Rooms.TryGetValue(cell, out var room)) continue;

            int rx = cx + gx * Step - Cell / 2;
            int ry = cy + gy * Step - Cell / 2;
            bool current = cell.Equals(cur);
            var col = current ? Palette.Spark : BiomeColor(room.Biome);
            Raylib.DrawRectangle(rx, ry, Cell, Cell, col);
            if (room.IsCore) Raylib.DrawRectangle(rx + 2, ry + 2, Cell - 4, Cell - 4, Palette.CoreGlow);
            if (current) Raylib.DrawRectangleLinesEx(new Rectangle(rx - 1, ry - 1, Cell + 2, Cell + 2), 1.5f, Palette.SparkCore);

            // door connectors to visited neighbours
            foreach (Direction d in Enum.GetValues<Direction>())
            {
                if (!room.HasDoor(d)) continue;
                var (dx, dy) = Doorways.Delta(d);
                var n = new GridPoint(cell.X + dx, cell.Y + dy);
                if (!world.Visited.Contains(n)) continue;
                int mxp = rx + Cell / 2 + dx * (Cell / 2 + Gap / 2);
                int myp = ry + Cell / 2 + dy * (Cell / 2 + Gap / 2);
                Raylib.DrawRectangle(mxp - 1, myp - 1, 2, 2, Palette.InkDim);
            }
        }
    }

    public static Color BiomeColor(Biome b) => b switch
    {
        Biome.RustVents => Palette.Rgb(150, 84, 54),
        Biome.CrystalConduits => Palette.Rgb(70, 150, 150),
        Biome.Mainframe => Palette.Rgb(90, 110, 160),
        _ => Palette.Rgb(90, 100, 120),
    };
}
