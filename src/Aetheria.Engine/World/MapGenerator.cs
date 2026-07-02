using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;

namespace Aetheria.Engine.World;

/// <summary>
/// Procedurally builds a large, connected grid of fixed-size screens across three
/// biomes. Cells are grown as a spanning tree (guaranteed connected) with extra
/// loop edges; biomes are distance-banded from the start so adjacency only ever
/// crosses one band; biome boundaries and the Core are gated by ability doors
/// whose ability is obtainable in the preceding region.
/// </summary>
public static class MapGenerator
{
    public const int TargetRooms = 64;
    private const int Floor = Doorways.FloorRow;

    public static World Generate(uint seed = 20240601u, int targetRooms = TargetRooms)
    {
        var rng = new Rng(seed);
        var start = new GridPoint(0, 0);

        // ---- 1. grow a connected set of cells + edges ----------------------
        var cells = new List<GridPoint> { start };
        var cellSet = new HashSet<GridPoint> { start };
        var edges = new HashSet<(GridPoint, GridPoint)>();

        while (cellSet.Count < targetRooms)
        {
            var from = cells[rng.Range(0, cells.Count)];
            var dir = (Direction)rng.Range(0, 4);
            var (dx, dy) = Doorways.Delta(dir);
            var to = new GridPoint(from.X + dx, from.Y + dy);
            if (cellSet.Add(to))
            {
                cells.Add(to);
                edges.Add(Norm(from, to));
            }
            else if (rng.Chance(0.12f))
            {
                edges.Add(Norm(from, to));   // occasional loop
            }
        }

        // extra loop edges between already-placed adjacent cells
        foreach (var c in cells)
            foreach (Direction d in Enum.GetValues<Direction>())
            {
                var (dx, dy) = Doorways.Delta(d);
                var n = new GridPoint(c.X + dx, c.Y + dy);
                if (cellSet.Contains(n) && rng.Chance(0.08f))
                    edges.Add(Norm(c, n));
            }

        // ---- 2. distances, biomes, core -----------------------------------
        var adj = BuildAdjacency(cells, edges);
        var dist = Bfs(start, adj);
        int maxD = dist.Values.Max();
        int t1 = Math.Max(1, maxD / 3);
        int t2 = Math.Max(t1 + 1, maxD * 2 / 3);

        Biome BiomeAt(GridPoint c) => dist[c] <= t1 ? Biome.RustVents
                                    : dist[c] <= t2 ? Biome.CrystalConduits
                                    : Biome.Mainframe;

        var core = cells.OrderByDescending(c => dist[c]).ThenBy(c => c.X).First();

        // ---- 3. build rooms ------------------------------------------------
        var doorDirs = new Dictionary<GridPoint, HashSet<Direction>>();
        foreach (var c in cells) doorDirs[c] = new HashSet<Direction>();
        foreach (var (a, b) in edges)
        {
            doorDirs[a].Add(DirFromDelta(b.X - a.X, b.Y - a.Y));
            doorDirs[b].Add(DirFromDelta(a.X - b.X, a.Y - b.Y));
        }

        var rooms = new Dictionary<GridPoint, Room>();
        foreach (var c in cells)
        {
            var biome = BiomeAt(c);
            uint cellSeed = seed ^ (uint)(c.X * 73856093) ^ (uint)(c.Y * 19349663);
            var map = RoomInterior.Build(doorDirs[c], biome, cellSeed);
            var room = new Room
            {
                GridX = c.X,
                GridY = c.Y,
                Biome = biome,
                Map = map,
                Seed = cellSeed,
                Name = Biomes.DisplayName(biome),
                DefaultSpawn = new Vector2(5.5f * GameConfig.TileSize, (Floor - 2) * GameConfig.TileSize),
            };
            if (c == core)
            {
                room.IsCore = true;
                room.CoreCenter = new Vector2((Doorways.RoomW / 2) * GameConfig.TileSize,
                                              (Floor - 2) * GameConfig.TileSize);
                room.Enemies.Add(new EnemySpawn
                {
                    Tile = new GridPoint(Doorways.RoomW / 2, 8),
                    Kind = EnemyKind.Warden,
                    Range = 10,
                });
            }
            rooms[c] = room;
        }
        rooms[start].DefaultSpawn = new Vector2(5.5f * GameConfig.TileSize, (Floor - 2) * GameConfig.TileSize);

        // ---- 4. doors (locked at biome boundaries / core) ------------------
        foreach (var (a, b) in edges)
        {
            var dir = DirFromDelta(b.X - a.X, b.Y - a.Y);
            var (kind, req) = DoorGate(BiomeAt(a), BiomeAt(b), a == core || b == core);
            rooms[a].Doors.Add(new Door { Edge = dir, Kind = kind, Requires = req });
            rooms[b].Doors.Add(new Door { Edge = Doorways.Opposite(dir), Kind = kind, Requires = req });
        }

        // ---- 4b. puzzles (self-contained: an in-room switch/plate/sequence
        //          opens a Blast door, so global connectivity is preserved) ----
        PlacePuzzles(rooms, cells, start, core, new Rng(seed ^ 0x5EED));

        // ---- 5. ability pickups (gate each region) -------------------------
        PlaceAbility(rooms, cells, dist, BiomeAt, AbilityType.DoubleJump, Biome.RustVents, start, core, rng);
        PlaceAbility(rooms, cells, dist, BiomeAt, AbilityType.Dash, Biome.CrystalConduits, start, core, rng);
        PlaceAbility(rooms, cells, dist, BiomeAt, AbilityType.Phase, Biome.Mainframe, start, core, rng);
        PlaceAbility(rooms, cells, dist, BiomeAt, AbilityType.WallClimb, Biome.Mainframe, start, core, rng);

        // ---- 6. enemies ----------------------------------------------------
        foreach (var c in cells)
        {
            if (c == start || c == core) continue;
            PopulateEnemies(rooms[c], BiomeAt(c), doorDirs[c], new Rng(rooms[c].Seed ^ 0xE1));
        }

        return new World(rooms.Values, start);
    }

    // A minimal 5-room plus-shape (all open doors) for focused transition tests.
    public static World Cross(uint seed = 1u)
    {
        var center = new GridPoint(0, 0);
        var cells = new[]
        {
            center,
            new GridPoint(0, -1), new GridPoint(1, 0),
            new GridPoint(0, 1), new GridPoint(-1, 0),
        };
        var rooms = new Dictionary<GridPoint, Room>();
        foreach (var c in cells)
        {
            var dirs = new HashSet<Direction>();
            if (c == center) dirs = new HashSet<Direction> { Direction.North, Direction.East, Direction.South, Direction.West };
            else dirs.Add(DirFromDelta(center.X - c.X, center.Y - c.Y));
            var map = RoomInterior.Build(dirs, Biome.RustVents, seed + (uint)(c.X * 7 + c.Y * 13));
            rooms[c] = new Room
            {
                GridX = c.X, GridY = c.Y, Biome = Biome.RustVents, Map = map,
                Seed = seed, Name = "Test",
            };
        }
        rooms[center].DefaultSpawn = new Vector2(5.5f * GameConfig.TileSize, (Floor - 2) * GameConfig.TileSize);
        foreach (var c in cells)
            if (c != center)
            {
                var dirFromCenter = DirFromDelta(c.X - center.X, c.Y - center.Y);
                rooms[center].Doors.Add(new Door { Edge = dirFromCenter });
                rooms[c].Doors.Add(new Door { Edge = Doorways.Opposite(dirFromCenter) });
            }
        return new World(rooms.Values, center);
    }

    private static void PlacePuzzles(Dictionary<GridPoint, Room> rooms, List<GridPoint> cells,
                                     GridPoint start, GridPoint core, Rng rng)
    {
        var candidates = cells.Where(c => c != start && c != core
                && rooms[c].Doors.Count >= 2
                && rooms[c].Doors.Count(d => d.Kind == DoorKind.Open) >= 1).ToList();
        Shuffle(candidates, rng);

        int placed = 0, typeIdx = 0;
        foreach (var c in candidates)
        {
            if (placed >= 6) break;
            var room = rooms[c];
            var open = room.Doors.Where(d => d.Kind == DoorKind.Open).ToList();
            if (open.Count == 0) continue;
            var door = open[rng.Range(0, open.Count)];
            var (dx, dy) = Doorways.Delta(door.Edge);
            var nCell = new GridPoint(c.X + dx, c.Y + dy);
            if (!rooms.TryGetValue(nCell, out var nb)) continue;
            var nbDoor = nb.DoorOn(Doorways.Opposite(door.Edge));
            if (nbDoor is null || nbDoor.Kind != DoorKind.Open) continue;

            string flag = $"gate_{c.X}_{c.Y}_{(int)door.Edge}";
            ReplaceDoor(room, door, flag);
            ReplaceDoor(nb, nbDoor, flag);

            bool south = room.HasDoor(Direction.South);
            switch (typeIdx % 3)
            {
                case 0:
                    room.Switches.Add(new PuzzleSwitch
                    {
                        Tile = new GridPoint(RoomInterior.SafeFloorColumn(rng, south), Floor - 2),
                        Kind = SwitchKind.Shootable, Flag = flag,
                    });
                    break;
                case 1:
                    int plateCol = RoomInterior.SafeFloorColumn(rng, south);
                    int blockCol = Math.Clamp(plateCol + rng.Range(-8, 9), 5, Doorways.RoomW - 6);
                    if (Math.Abs(blockCol - plateCol) < 3) blockCol = Math.Min(Doorways.RoomW - 6, plateCol + 4);
                    room.Plates.Add(new PressurePlate { Tile = new GridPoint(plateCol, Floor - 1), Flag = flag });
                    room.BlockSpawns.Add(new GridPoint(blockCol, Floor - 3));
                    break;
                default:
                    room.Sequence = new SequencePuzzle { Flag = flag, Count = 3, TimeLimit = 6f };
                    var order = new List<int> { 0, 1, 2 };
                    Shuffle(order, rng);
                    for (int i = 0; i < 3; i++)
                        room.Switches.Add(new PuzzleSwitch
                        {
                            Tile = new GridPoint(8 + i * 8, Floor - 2),
                            Kind = SwitchKind.Shootable, SequenceIndex = order[i],
                        });
                    break;
            }
            typeIdx++;
            placed++;
        }
    }

    private static void ReplaceDoor(Room room, Door old, string flag)
    {
        room.Doors.Remove(old);
        room.Doors.Add(new Door { Edge = old.Edge, Kind = DoorKind.Blast, Flag = flag });
    }

    private static void Shuffle<T>(IList<T> list, Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---- helpers -----------------------------------------------------------
    private static (DoorKind, AbilityType?) DoorGate(Biome a, Biome b, bool core)
    {
        if (core) return (DoorKind.AbilityGate, AbilityType.Phase);
        if (a == b) return (DoorKind.Open, null);
        var higher = (Biome)Math.Max((int)a, (int)b);
        return higher switch
        {
            Biome.CrystalConduits => (DoorKind.AbilityGate, AbilityType.DoubleJump),
            Biome.Mainframe => (DoorKind.AbilityGate, AbilityType.Dash),
            _ => (DoorKind.Open, null),
        };
    }

    private static void PlaceAbility(
        Dictionary<GridPoint, Room> rooms, List<GridPoint> cells, Dictionary<GridPoint, int> dist,
        Func<GridPoint, Biome> biomeAt, AbilityType ability, Biome biome, GridPoint start, GridPoint core, Rng rng)
    {
        var candidates = cells.Where(c => biomeAt(c) == biome && c != start && c != core
                                          && !rooms[c].Pickups.Any()).ToList();
        if (candidates.Count == 0) candidates = cells.Where(c => biomeAt(c) == biome && c != core).ToList();
        if (candidates.Count == 0) return;
        var cell = candidates[rng.Range(0, candidates.Count)];
        int col = RoomInterior.SafeFloorColumn(rng, rooms[cell].HasDoor(Direction.South));
        rooms[cell].Pickups.Add(new AbilityPickup { Tile = new GridPoint(col, Floor - 2), Type = ability });
    }

    private static void PopulateEnemies(Room room, Biome biome, HashSet<Direction> doors, Rng rng)
    {
        int count = rng.Range(1, 4);
        bool south = doors.Contains(Direction.South);
        for (int i = 0; i < count; i++)
        {
            var kind = PickEnemy(biome, rng);
            if (IsGround(kind))
            {
                int col = RoomInterior.SafeFloorColumn(rng, south);
                room.Enemies.Add(new EnemySpawn { Tile = new GridPoint(col, Floor - 1), Kind = kind, Range = rng.Range(3, 6) });
            }
            else
            {
                int col = rng.Range(6, Doorways.RoomW - 6);
                int y = rng.Range(6, Floor - 4);
                room.Enemies.Add(new EnemySpawn { Tile = new GridPoint(col, y), Kind = kind, Range = rng.Range(3, 6) });
            }
        }
    }

    private static bool IsGround(EnemyKind k) =>
        k is EnemyKind.Crawler or EnemyKind.Charger or EnemyKind.ArmoredCrawler;

    private static EnemyKind PickEnemy(Biome biome, Rng rng)
    {
        EnemyKind[] pool = biome switch
        {
            Biome.RustVents => new[] { EnemyKind.Crawler, EnemyKind.Crawler, EnemyKind.Charger, EnemyKind.ArmoredCrawler },
            Biome.CrystalConduits => new[] { EnemyKind.Floater, EnemyKind.HoverTurret, EnemyKind.StalkerDrone, EnemyKind.Crawler },
            _ => new[] { EnemyKind.Sentinel, EnemyKind.HoverTurret, EnemyKind.StalkerDrone, EnemyKind.ArmoredCrawler },
        };
        return pool[rng.Range(0, pool.Length)];
    }

    private static (GridPoint, GridPoint) Norm(GridPoint a, GridPoint b)
        => (a.X < b.X || (a.X == b.X && a.Y <= b.Y)) ? (a, b) : (b, a);

    private static Direction DirFromDelta(int dx, int dy)
        => dy < 0 ? Direction.North : dy > 0 ? Direction.South : dx > 0 ? Direction.East : Direction.West;

    private static Dictionary<GridPoint, List<GridPoint>> BuildAdjacency(
        List<GridPoint> cells, HashSet<(GridPoint, GridPoint)> edges)
    {
        var adj = cells.ToDictionary(c => c, _ => new List<GridPoint>());
        foreach (var (a, b) in edges) { adj[a].Add(b); adj[b].Add(a); }
        return adj;
    }

    private static Dictionary<GridPoint, int> Bfs(GridPoint start, Dictionary<GridPoint, List<GridPoint>> adj)
    {
        var dist = new Dictionary<GridPoint, int> { [start] = 0 };
        var q = new Queue<GridPoint>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in adj[c])
                if (!dist.ContainsKey(n)) { dist[n] = dist[c] + 1; q.Enqueue(n); }
        }
        return dist;
    }
}
