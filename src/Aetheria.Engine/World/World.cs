using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;

namespace Aetheria.Engine.World;

/// <summary>
/// The runtime grid of rooms. Rooms live at integer cells; a door on an edge
/// leads to the grid neighbour in that direction. Owns the active room, N/S/E/W
/// transitions with correct entry placement, locked-door barriers driven by
/// <see cref="Flags"/>, ability pickups, visited tracking, and the Core check.
/// </summary>
public sealed class World
{
    private readonly Dictionary<GridPoint, Room> _rooms;
    private readonly GridPoint _startCell;
    private float _transitionLock;

    public Room Current { get; private set; }
    public GridPoint CurrentCell => Current.Cell;
    public IReadOnlyDictionary<GridPoint, Room> Rooms => _rooms;
    public GameFlags Flags { get; } = new();

    public event Action<Room, Room>? RoomChanged;
    public event Action<AbilityType>? AbilityUnlocked;
    public event Action<WeaponType>? WeaponUnlocked;

    public bool JustTransitioned { get; private set; }
    public bool ReachedCore { get; private set; }

    private readonly HashSet<GridPoint> _visited = new();
    public IReadOnlyCollection<GridPoint> Visited => _visited;

    public World(IEnumerable<Room> rooms, GridPoint startCell)
    {
        _rooms = rooms.ToDictionary(r => r.Cell);
        _startCell = startCell;
        Current = _rooms[startCell];
        _visited.Add(startCell);
    }

    public Vector2 StartSpawn => _rooms[_startCell].DefaultSpawn;
    public GridPoint StartCell => _startCell;

    public Room? Neighbour(Room room, Direction edge)
    {
        var (dx, dy) = Doorways.Delta(edge);
        return _rooms.TryGetValue(new GridPoint(room.GridX + dx, room.GridY + dy), out var n) ? n : null;
    }

    /// <summary>Per-frame world logic: run AFTER the player has moved this frame.</summary>
    public void Update(float dt, Player player)
    {
        JustTransitioned = false;
        if (_transitionLock > 0) _transitionLock -= dt;

        RefreshDoors(Current, player.Abilities);   // keep locked-door barriers in sync with flags
        TryTransition(player);
        CollectPickups(player);
        CheckCore(player);
    }

    /// <summary>Fill each locked door's opening with its barrier tile; clear opened ones.</summary>
    public void RefreshDoors(Room room, AbilitySet abilities)
    {
        foreach (var door in room.Doors)
        {
            var tile = door.IsLocked(Flags, abilities) ? door.BarrierTile : TileType.Empty;
            foreach (var (x, y) in Doorways.OpeningTiles(door.Edge))
                room.Map.Set(x, y, tile);
        }
    }

    private bool TryTransition(Player player)
    {
        if (_transitionLock > 0) return false;
        foreach (var door in Current.Doors)
        {
            if (door.IsLocked(Flags, player.Abilities)) continue;   // barrier blocks it anyway
            if (!player.Bounds.Intersects(Doorways.TriggerZone(door.Edge))) continue;
            var neighbour = Neighbour(Current, door.Edge);
            if (neighbour == null) continue;
            EnterCell(neighbour, Doorways.Opposite(door.Edge), player);
            return true;
        }
        return false;
    }

    private void EnterCell(Room next, Direction arriveEdge, Player player)
    {
        var old = Current;
        Current = next;
        _visited.Add(next.Cell);
        RefreshDoors(next, player.Abilities);
        player.PlaceAt(Doorways.EntryPosition(arriveEdge));
        _transitionLock = 0.3f;
        JustTransitioned = true;
        RoomChanged?.Invoke(old, next);
    }

    private void CollectPickups(Player player)
    {
        int ts = Current.Map.TileSize;
        foreach (var pickup in Current.Pickups)
        {
            if (pickup.Taken) continue;
            var box = new Maths.Aabb(pickup.Tile.X * ts, pickup.Tile.Y * ts, ts, ts);
            if (!player.Bounds.Intersects(box)) continue;
            pickup.Taken = true;
            if (player.Abilities.Unlock(pickup.Type))
                AbilityUnlocked?.Invoke(pickup.Type);
            player.RefillEnergy();
        }
        foreach (var wp in Current.WeaponPickups)
        {
            if (wp.Taken) continue;
            var box = new Maths.Aabb(wp.Tile.X * ts, wp.Tile.Y * ts, ts, ts);
            if (!player.Bounds.Intersects(box)) continue;
            wp.Taken = true;
            if (player.Weapons.Unlock(wp.Type))
                WeaponUnlocked?.Invoke(wp.Type);
            player.RefillEnergy();
        }
    }

    private void CheckCore(Player player)
    {
        if (!Current.IsCore || Current.CoreCenter is not { } core) return;
        if (Vector2.Distance(player.Center, core) < 26f)
            ReachedCore = true;
    }

    /// <summary>Debug/testing aid: jump directly to a cell and place the player at its spawn.</summary>
    public void DebugEnter(GridPoint cell, Player player)
    {
        if (!_rooms.TryGetValue(cell, out var r)) return;
        Current = r;
        _visited.Add(cell);
        RefreshDoors(r, player.Abilities);
        player.PlaceAt(r.DefaultSpawn);
        _transitionLock = 0.3f;
    }

    public void Reset()
    {
        Current = _rooms[_startCell];
        _transitionLock = 0f;
        ReachedCore = false;
        JustTransitioned = false;
        Flags.Clear();
        _visited.Clear();
        _visited.Add(_startCell);
        foreach (var room in _rooms.Values)
        {
            foreach (var p in room.Pickups) p.Taken = false;
            foreach (var wp in room.WeaponPickups) wp.Taken = false;
            foreach (var s in room.Switches) s.Active = false;
            foreach (var pl in room.Plates) pl.Pressed = false;
            room.Sequence?.Reset();
        }
    }
}
