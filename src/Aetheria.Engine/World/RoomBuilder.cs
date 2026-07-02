using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;

namespace Aetheria.Engine.World;

/// <summary>
/// Fluent helper for authoring a <see cref="Room"/>: lays down structured,
/// guaranteed-traversable collision geometry plus doors, pickups, enemies and
/// the critical-path waypoints the reachability checker verifies.
/// </summary>
public sealed class RoomBuilder
{
    private const int T = GameConfig.TileSize;
    public const int DoorOpening = 3; // tiles tall

    private readonly int _id;
    private readonly string _name;
    private readonly TileMap _map;
    private readonly uint _seed;
    private readonly int _floorRow;
    private readonly bool _isCore;

    private readonly List<Door> _doors = new();
    private readonly List<AbilityPickup> _pickups = new();
    private readonly List<EnemySpawn> _enemies = new();
    private readonly List<PathEdge> _path = new();

    private Vector2 _spawn;
    private GridPoint? _lastWaypoint;

    public int Width => _map.Width;
    public int Height => _map.Height;
    public int FloorRow => _floorRow;

    public RoomBuilder(int id, string name, int w, int h, uint seed, int floorRow, bool isCore = false)
    {
        _id = id;
        _name = name;
        _seed = seed;
        _floorRow = floorRow;
        _isCore = isCore;
        _map = new TileMap(w, h, T);
        _map.Border(TileType.Solid);
        _map.Fill(0, floorRow, w - 1, h - 1, TileType.Solid); // solid base/floor
    }

    // ---- geometry primitives -----------------------------------------------
    public RoomBuilder Platform(int x0, int x1, int row, TileType t = TileType.Solid)
    {
        _map.Fill(x0, row, x1, row, t);
        return this;
    }

    public RoomBuilder Block(int x0, int y0, int x1, int y1, TileType t = TileType.Solid)
    {
        _map.Fill(x0, y0, x1, y1, t);
        return this;
    }

    public RoomBuilder Clear(int x0, int y0, int x1, int y1)
    {
        _map.Fill(x0, y0, x1, y1, TileType.Empty);
        return this;
    }

    /// <summary>Cut a pit in the floor and line its bottom with a hazard.</summary>
    public RoomBuilder Pit(int x0, int x1, int depthFromFloor = 3)
    {
        _map.Fill(x0, _floorRow, x1, Height - 1, TileType.Empty);
        int hazRow = Math.Min(Height - 2, _floorRow + depthFromFloor);
        _map.Fill(x0, hazRow, x1, hazRow, TileType.Hazard);
        return this;
    }

    public RoomBuilder Hazard(int x0, int x1, int row)
    {
        _map.Fill(x0, row, x1, row, TileType.Hazard);
        return this;
    }

    // ---- doors --------------------------------------------------------------
    public RoomBuilder EastDoor(int topRow, int targetRoomId)
    {
        int col = Width - 1;
        for (int r = topRow; r < topRow + DoorOpening; r++) _map.Set(col, r, TileType.Empty);
        _map.Fill(Width - 4, topRow + DoorOpening, col, topRow + DoorOpening, TileType.Solid); // ledge
        _map.Fill(Width - 4, topRow, Width - 2, topRow + DoorOpening - 1, TileType.Empty);     // approach
        _doors.Add(new Door
        {
            Edge = Direction.East,
            TargetRoomId = targetRoomId,
            TargetEdge = Direction.West,
            TriggerZone = new Aabb((col - 0.5f) * T, topRow * T, 2f * T, DoorOpening * T),
            EntrySpawn = new Vector2((Width - 3.5f) * T, (topRow) * T),
        });
        return this;
    }

    public RoomBuilder WestDoor(int topRow, int targetRoomId)
    {
        for (int r = topRow; r < topRow + DoorOpening; r++) _map.Set(0, r, TileType.Empty);
        _map.Fill(0, topRow + DoorOpening, 3, topRow + DoorOpening, TileType.Solid);  // ledge
        _map.Fill(1, topRow, 3, topRow + DoorOpening - 1, TileType.Empty);            // approach
        _doors.Add(new Door
        {
            Edge = Direction.West,
            TargetRoomId = targetRoomId,
            TargetEdge = Direction.East,
            TriggerZone = new Aabb(-0.5f * T, topRow * T, 2f * T, DoorOpening * T),
            EntrySpawn = new Vector2(3.5f * T, (topRow) * T),
        });
        return this;
    }

    // ---- entities & metadata -----------------------------------------------
    public RoomBuilder Spawn(int col, int standRow)
    {
        _spawn = new Vector2((col + 0.5f) * T, (standRow - 1) * T);
        _lastWaypoint = new GridPoint(col, standRow);
        return this;
    }

    /// <summary>Place an ability pickup floating one tile above platform row.</summary>
    public RoomBuilder Pickup(int col, int platformRow, AbilityType type)
    {
        _pickups.Add(new AbilityPickup { Tile = new GridPoint(col, platformRow - 1), Type = type });
        return this;
    }

    public RoomBuilder Enemy(int col, int row, EnemyKind kind, int range = 4)
    {
        _enemies.Add(new EnemySpawn { Tile = new GridPoint(col, row), Kind = kind, Range = range });
        return this;
    }

    /// <summary>Record a critical-path waypoint (a standable platform tile).</summary>
    public RoomBuilder Waypoint(int col, int platformRow, AbilityType? requires = null, bool walk = false)
    {
        var wp = new GridPoint(col, platformRow);
        if (_lastWaypoint is { } prev)
            _path.Add(new PathEdge(prev, wp, requires, walk));
        _lastWaypoint = wp;
        return this;
    }

    /// <summary>Convenience: a walk edge along the floor to a new column on the same row.</summary>
    public RoomBuilder Walk(int col, int row) => Waypoint(col, row, requires: null, walk: true);

    private Vector2? _core;
    public RoomBuilder Core(int col, int row)
    {
        _core = new Vector2((col + 0.5f) * T, (row + 0.5f) * T);
        return this;
    }

    public Room Build()
    {
        var room = new Room
        {
            Id = _id,
            Name = _name,
            Map = _map,
            DefaultSpawn = _spawn,
            Seed = _seed,
            IsCore = _isCore,
            CoreCenter = _core,
        };
        room.Doors.AddRange(_doors);
        room.Pickups.AddRange(_pickups);
        room.Enemies.AddRange(_enemies);
        room.CriticalPath.AddRange(_path);
        return room;
    }
}
