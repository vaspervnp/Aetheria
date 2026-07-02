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
        var visited = new HashSet<int>();
        try
        {
            var world = WorldBuilder.Build(seed);
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

            visited.Add(world.CurrentRoomId);

            for (int i = 0; i < frames; i++)
            {
                var input = Script(i);
                player.Update(input, world.Current.Map, Dt);
                CombatSystem.Step(player, enemies, projectiles, world.Current.Map, Dt);
                world.Update(Dt, player);
                visited.Add(world.CurrentRoomId);

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

    /// <summary>A rough "keep moving right, jump/dash/attack/phase" bot.</summary>
    private static InputState Script(int i)
    {
        int j = i % 45;
        bool jumpPressed = j == 0;
        bool jumpHeld = j < 11;
        bool jumpReleased = j == 11;
        bool dash = (i % 80) == 20;
        bool attack = (i % 40) == 5;
        bool phase = (i % 240) is >= 120 and < 190;   // hold phase for a stretch
        bool up = (i % 120) >= 60;                      // sometimes hold up (climb)
        return new InputState(
            moveX: 1f, up: up, down: false,
            jumpPressed: jumpPressed, jumpHeld: jumpHeld, jumpReleased: jumpReleased,
            dashPressed: dash, attackPressed: attack, phaseHeld: phase);
    }
}
