using System.Numerics;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Entities;

namespace Aetheria.Engine.World;

/// <summary>
/// The runtime graph of rooms. Owns which room is active, moves the player
/// between rooms through door trigger zones, hands out ability pickups, and
/// reports when the Core is reached.
/// </summary>
public sealed class World
{
    private readonly Dictionary<int, Room> _rooms;
    private readonly int _startRoomId;
    private float _transitionLock;

    public Room Current { get; private set; }
    public int CurrentRoomId => Current.Id;
    public IReadOnlyDictionary<int, Room> Rooms => _rooms;

    /// <summary>Fired (old, new) when the active room changes.</summary>
    public event Action<Room, Room>? RoomChanged;
    /// <summary>Fired when an ability pickup is collected.</summary>
    public event Action<AbilityType>? AbilityUnlocked;

    public bool JustTransitioned { get; private set; }
    public bool ReachedCore { get; private set; }

    public World(IEnumerable<Room> rooms, int startRoomId)
    {
        _rooms = rooms.ToDictionary(r => r.Id);
        _startRoomId = startRoomId;
        Current = _rooms[startRoomId];
    }

    public Vector2 StartSpawn => _rooms[_startRoomId].DefaultSpawn;

    /// <summary>Per-frame world logic: run AFTER the player has moved this frame.</summary>
    public void Update(float dt, Player player)
    {
        JustTransitioned = false;
        if (_transitionLock > 0) _transitionLock -= dt;

        TryTransition(player);
        CollectPickups(player);
        CheckCore(player);
    }

    private bool TryTransition(Player player)
    {
        if (_transitionLock > 0) return false;
        foreach (var door in Current.Doors)
        {
            if (!player.Bounds.Intersects(door.TriggerZone)) continue;
            EnterRoom(door.TargetRoomId, door.TargetEdge, player);
            return true;
        }
        return false;
    }

    private void EnterRoom(int roomId, Direction viaEdge, Player player)
    {
        if (!_rooms.TryGetValue(roomId, out var next)) return;
        var old = Current;
        Current = next;
        var entry = next.DoorOn(viaEdge);
        player.PlaceAt(entry?.EntrySpawn ?? next.DefaultSpawn);
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
    }

    private void CheckCore(Player player)
    {
        if (!Current.IsCore || Current.CoreCenter is not { } core) return;
        if (Vector2.Distance(player.Center, core) < 26f)
            ReachedCore = true;
    }

    /// <summary>Debug/testing aid: jump directly to a room and place the player at its spawn.</summary>
    public void DebugEnter(int roomId, Player player)
    {
        if (!_rooms.TryGetValue(roomId, out var r)) return;
        Current = r;
        player.PlaceAt(r.DefaultSpawn);
        _transitionLock = 0.3f;
    }

    /// <summary>Restart a fresh run: back to the start room with pickups restored.</summary>
    public void Reset()
    {
        Current = _rooms[_startRoomId];
        _transitionLock = 0f;
        ReachedCore = false;
        JustTransitioned = false;
        foreach (var room in _rooms.Values)
            foreach (var p in room.Pickups)
                p.Taken = false;
    }
}
