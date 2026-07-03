using Aetheria.Engine.Abilities;

namespace Aetheria.Engine.World;

public sealed class SolveResult
{
    public bool Beatable;
    public bool CoreReachable;
    public int RoomsReachableFromStart;   // with no abilities, at the very start
    public int RoomsEventuallyReachable;
    public List<AbilityType> AbilityOrder = new();
    public List<AbilityType> AbilitiesNeverReached = new();
}

/// <summary>
/// Simulates a player's progression through the door graph to prove the world is
/// actually beatable: an Open door is always passable; an AbilityGate opens once
/// its ability is held; a Blast (puzzle) door opens once the room holding its
/// switch/plate/sequence is reachable. Abilities are picked up from reachable
/// rooms; the whole thing runs to a fixpoint.
/// </summary>
public static class WorldSolver
{
    public static SolveResult Solve(World world)
    {
        var result = new SolveResult();

        // flag -> cell that hosts its mechanism (switch / plate / sequence)
        var mechanism = new Dictionary<string, GridPoint>();
        foreach (var room in world.Rooms.Values)
        {
            foreach (var s in room.Switches) if (s.Flag != null) mechanism[s.Flag] = room.Cell;
            foreach (var p in room.Plates) if (p.Flag != null) mechanism[p.Flag] = room.Cell;
            if (room.Sequence != null) mechanism[room.Sequence.Flag] = room.Cell;
        }

        var have = new HashSet<AbilityType>();
        var reach = new HashSet<GridPoint>();
        result.RoomsReachableFromStart = Flood(world, have, mechanism, reach: null).Count;

        bool changed = true;
        while (changed)
        {
            changed = false;
            var flood = Flood(world, have, mechanism, reach);
            if (flood.Count != reach.Count) { reach = flood; changed = true; }
            foreach (var cell in reach)
                foreach (var pk in world.Rooms[cell].Pickups)
                    if (have.Add(pk.Type)) { result.AbilityOrder.Add(pk.Type); changed = true; }
        }

        result.RoomsEventuallyReachable = reach.Count;
        var coreCell = world.Rooms.Values.First(r => r.IsCore).Cell;
        result.CoreReachable = reach.Contains(coreCell);
        result.Beatable = result.CoreReachable;
        foreach (var a in AbilitySet.All)
            if (!have.Contains(a)) result.AbilitiesNeverReached.Add(a);
        return result;
    }

    private static HashSet<GridPoint> Flood(World world, HashSet<AbilityType> have,
        Dictionary<string, GridPoint> mechanism, HashSet<GridPoint>? reach)
    {
        var seen = new HashSet<GridPoint> { world.StartCell };
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach (var cell in seen.ToList())
            {
                var room = world.Rooms[cell];
                foreach (var door in room.Doors)
                {
                    if (!Passable(door, have, mechanism, seen)) continue;
                    var (dx, dy) = Doorways.Delta(door.Edge);
                    var n = new GridPoint(cell.X + dx, cell.Y + dy);
                    if (world.Rooms.ContainsKey(n) && seen.Add(n)) grew = true;
                }
            }
        }
        return seen;
    }

    private static bool Passable(Door door, HashSet<AbilityType> have,
        Dictionary<string, GridPoint> mechanism, HashSet<GridPoint> seen) => door.Kind switch
    {
        DoorKind.Open => true,
        DoorKind.AbilityGate => door.Requires is { } a && have.Contains(a),
        _ => door.Flag is { } f && mechanism.TryGetValue(f, out var cell) && seen.Contains(cell),
    };
}
