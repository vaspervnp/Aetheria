using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>
/// Breadth-first grid pathfinding over passable (non-solid) tiles — used by the
/// flying Stalker-Drone to route around walls toward Spark. Returns the next
/// tile to step to, or null if already there / no route.
/// </summary>
public static class Pathfinder
{
    private static bool Passable(TileMap map, int x, int y) => map.InBounds(x, y) && !map.IsSolidTile(x, y);

    public static GridPoint? NextStep(TileMap map, GridPoint from, GridPoint to, int limit = 2000)
    {
        if (from == to || !Passable(map, to.X, to.Y) || !Passable(map, from.X, from.Y)) return null;

        var came = new Dictionary<GridPoint, GridPoint> { [from] = from };
        var q = new Queue<GridPoint>();
        q.Enqueue(from);
        Span<(int, int)> dirs = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0 && limit-- > 0)
        {
            var c = q.Dequeue();
            if (c == to) break;
            foreach (var (dx, dy) in dirs)
            {
                var n = new GridPoint(c.X + dx, c.Y + dy);
                if (!Passable(map, n.X, n.Y) || came.ContainsKey(n)) continue;
                came[n] = c;
                q.Enqueue(n);
            }
        }

        if (!came.ContainsKey(to)) return null;
        var cur = to;
        while (came[cur] != from) cur = came[cur];
        return cur;
    }
}
