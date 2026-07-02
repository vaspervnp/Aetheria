using Aetheria.Engine.Abilities;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.World;

namespace Aetheria.Engine;

public sealed class SmokeResult
{
    public int Frames;
    public int RoomsVisited;
    public int Deaths;
    public int AbilitiesUnlocked;
    public bool ReachedCore;
    public bool Crashed;
    public string? Error;

    public override string ToString() =>
        $"frames={Frames} roomsVisited={RoomsVisited} deaths={Deaths} " +
        $"abilities={AbilitiesUnlocked}/{AbilitySet.All.Count} core={ReachedCore} crashed={Crashed}" +
        (Error is null ? "" : $"\n  error: {Error}");
}

/// <summary>
/// Runs the full simulation (world + player + combat) with scripted input and
/// NO rendering or audio — the head-less integration smoke test. Drives the same
/// update code the real game loop runs, so it catches logic regressions and
/// crashes without needing a window.
/// </summary>
public static class HeadlessSim
{
    private const float Dt = 1f / 60f;

    public static SmokeResult Run(int frames = 4000, uint seed = 1337)
    {
        var result = new SmokeResult();
        var visited = new HashSet<GridPoint>();
        try
        {
            var world = MapGenerator.Generate(seed);
            var player = new Player(world.StartSpawn);
            player.Respawn(world.StartSpawn);

            List<Enemy> enemies = SpawnEnemies(world.Current);
            var projectiles = new List<Projectile>();

            world.RoomChanged += (_, next) =>
            {
                enemies = SpawnEnemies(next);
                projectiles.Clear();
            };
            world.AbilityUnlocked += _ => { };

            visited.Add(world.CurrentCell);

            for (int i = 0; i < frames; i++)
            {
                var input = Steer(world, player, i);
                player.Update(input, world.Current.Map, Dt);
                CombatSystem.Step(player, enemies, projectiles, world.Current.Map, Dt);
                world.Update(Dt, player);
                visited.Add(world.CurrentCell);

                if (!player.Alive)
                {
                    result.Deaths++;
                    player.Respawn(world.Current.DefaultSpawn);
                }
                if (world.ReachedCore) { result.ReachedCore = true; break; }

                result.Frames = i + 1;
            }

            result.RoomsVisited = visited.Count;
            result.AbilitiesUnlocked = player.Abilities.Count;
        }
        catch (Exception ex)
        {
            result.Crashed = true;
            result.Error = ex.ToString();
        }
        return result;
    }

    private static List<Enemy> SpawnEnemies(Room room)
    {
        var list = new List<Enemy>();
        foreach (var s in room.Enemies) list.Add(Enemy.FromSpawn(s, room.Map.TileSize));
        return list;
    }

    /// <summary>A door-seeking bot: steers toward an unlocked door and crosses it.</summary>
    private static InputState Steer(World.World world, Player p, int frame)
    {
        var room = world.Current;
        int ts = room.Map.TileSize;

        Door? target = null;
        foreach (var pref in new[] { Direction.East, Direction.West, Direction.South, Direction.North })
        {
            target = room.Doors.FirstOrDefault(d => d.Edge == pref && !d.IsLocked(world.Flags, p.Abilities));
            if (target != null) break;
        }

        float mx = 1f;
        bool up = false, wantJump = false;
        if (target != null)
        {
            switch (target.Edge)
            {
                case Direction.East: mx = 1f; break;
                case Direction.West: mx = -1f; break;
                case Direction.South:
                    float sx = (Doorways.NsDoorCol + 1) * ts;
                    mx = p.Center.X < sx - 4 ? 1f : p.Center.X > sx + 4 ? -1f : 0f;
                    break;
                case Direction.North:
                    float nx = Doorways.NorthClimbCol * ts;
                    mx = p.Center.X < nx - 4 ? 1f : p.Center.X > nx + 4 ? -1f : 0f;
                    up = true; wantJump = true;
                    break;
            }
        }

        bool wall = (mx > 0 && p.OnWallRight) || (mx < 0 && p.OnWallLeft);
        int j = frame % 40;
        return new InputState(
            moveX: mx, up: up,
            jumpPressed: wall || wantJump || j == 0,
            jumpHeld: wantJump || j < 12,
            jumpReleased: j == 12,
            dashPressed: frame % 90 == 30, attackPressed: frame % 40 == 5);
    }
}
