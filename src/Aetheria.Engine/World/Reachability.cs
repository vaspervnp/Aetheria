using Aetheria.Engine.Abilities;

namespace Aetheria.Engine.World;

/// <summary>
/// A conservative model of what platforming moves Spark can make between two
/// standable platform tiles, given a set of abilities. Used to validate — at
/// build time and in tests — that every room's critical path is traversable
/// with the abilities the player is expected to hold, and that gate edges are
/// genuinely impassable without the required ability.
///
/// The numbers are deliberately a touch tighter than the real physics can
/// achieve, so anything this model says is reachable is comfortably reachable
/// in-game.
/// </summary>
public static class Reachability
{
    // base jump: ~4 tiles of rise / ~6 tiles of gap in the real physics.
    public const int BaseUp = 3;
    public const int BaseAcross = 5;
    public const int DoubleJumpUp = 3;      // additional
    public const int DoubleJumpAcross = 3;  // additional
    public const int DashAcross = 4;        // additional

    public static bool CanTraverse(in PathEdge edge, AbilitySet abilities)
    {
        // A walk along continuous solid floor is always possible.
        if (edge.Walk) return true;

        // Explicit ability gates that the jump-arc model can't express.
        switch (edge.Requires)
        {
            case AbilityType.WallClimb:
                return abilities.Has(AbilityType.WallClimb);
            case AbilityType.Phase:
                return abilities.Has(AbilityType.Phase);
        }

        int up = edge.From.Y - edge.To.Y;         // >0 => target is higher
        int across = Math.Abs(edge.To.X - edge.From.X);

        int maxUp = BaseUp;
        int maxAcross = BaseAcross;
        if (abilities.Has(AbilityType.DoubleJump)) { maxUp += DoubleJumpUp; maxAcross += DoubleJumpAcross; }
        if (abilities.Has(AbilityType.Dash)) { maxAcross += DashAcross; }

        if (across > maxAcross) return false;
        if (up > maxUp) return false;    // dropping down (up<=0) is always fine
        return true;
    }

    /// <summary>True if the whole critical path of a room can be walked with these abilities.</summary>
    public static bool PathClear(Room room, AbilitySet abilities)
    {
        foreach (var edge in room.CriticalPath)
            if (!CanTraverse(edge, abilities))
                return false;
        return true;
    }
}
