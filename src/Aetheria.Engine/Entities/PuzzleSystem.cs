using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.Maths;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

/// <summary>
/// Drives all in-room puzzles for one frame: shootable/melee switches, sequence
/// puzzles (hit N switches in order within a time limit), pressure plates latched
/// by heavy blocks, and the block↔player dynamic-solid collision. Sets/clears
/// <see cref="GameFlags"/> which in turn open locked doors. Purely logical.
/// </summary>
public static class PuzzleSystem
{
    public static void Step(
        Room room, Player player, InputState input,
        List<Projectile> projectiles, List<PushBlock> blocks,
        GameFlags flags, Aabb? melee, TileMap map, float dt,
        Action<string, Vector2>? effect = null)
    {
        TickSequence(room, dt);
        UpdateSwitches(room, projectiles, flags, melee, effect);
        UpdatePlates(room, blocks, flags);

        foreach (var b in blocks) b.Update(map, dt);
        ResolvePlayerBlocks(player, blocks, map, input);
    }

    private static void TickSequence(Room room, float dt)
    {
        if (room.Sequence is not { } seq || seq.Solved || seq.Progress <= 0) return;
        seq.Timer -= dt;
        if (seq.Timer <= 0f)
        {
            seq.Progress = 0;
            foreach (var s in room.Switches) if (s.SequenceIndex >= 0) s.Active = false;
        }
    }

    private static void UpdateSwitches(Room room, List<Projectile> projectiles, GameFlags flags,
                                       Aabb? melee, Action<string, Vector2>? effect)
    {
        foreach (var sw in room.Switches)
        {
            bool hit = false;
            var box = sw.Bounds(GameConfig.TileSize);
            if (sw.Kind == SwitchKind.Shootable)
            {
                foreach (var p in projectiles)
                    if (p.FromPlayer && !p.Dead && p.Bounds.Intersects(box)) { hit = true; p.Dead = true; }
            }
            else if (melee is { } m && m.Intersects(box))
            {
                hit = true;
            }
            if (hit) Activate(room, sw, flags, effect);
        }
    }

    private static void Activate(Room room, PuzzleSwitch sw, GameFlags flags, Action<string, Vector2>? effect)
    {
        var pos = sw.WorldCenter(GameConfig.TileSize);
        if (sw.SequenceIndex >= 0 && room.Sequence is { } seq)
        {
            if (seq.Solved || sw.Active) return;
            if (sw.SequenceIndex == seq.Progress)
            {
                sw.Active = true;
                seq.Progress++;
                if (seq.Progress == 1) seq.Timer = seq.TimeLimit;
                if (seq.Progress >= seq.Count)
                {
                    seq.Solved = true;
                    if (seq.Flag != null) flags.SetFlag(seq.Flag);
                    effect?.Invoke("sequence_solved", pos);
                }
                else effect?.Invoke("switch_on", pos);
            }
            else
            {
                seq.Progress = 0;
                foreach (var s in room.Switches) if (s.SequenceIndex >= 0) s.Active = false;
                effect?.Invoke("switch_fail", pos);
            }
            return;
        }

        if (sw.Active) return;
        sw.Active = true;
        if (sw.Flag != null) flags.SetFlag(sw.Flag);
        effect?.Invoke("switch_on", pos);
    }

    private static void UpdatePlates(Room room, List<PushBlock> blocks, GameFlags flags)
    {
        foreach (var plate in room.Plates)
        {
            var box = plate.Bounds(GameConfig.TileSize);
            bool pressed = blocks.Exists(b => b.Bounds.Intersects(box));
            plate.Pressed = pressed;
            if (plate.Flag != null) flags.Set(plate.Flag, pressed ? 1 : 0);
        }
    }

    private static void ResolvePlayerBlocks(Player player, List<PushBlock> blocks, TileMap map, InputState input)
    {
        foreach (var block in blocks)
        {
            var pb = player.Bounds;
            var bb = block.Bounds;
            if (!pb.Intersects(bb)) continue;

            float ox = MathF.Min(pb.Right, bb.Right) - MathF.Max(pb.Left, bb.Left);
            float oy = MathF.Min(pb.Bottom, bb.Bottom) - MathF.Max(pb.Top, bb.Top);
            if (ox <= 0f || oy <= 0f) continue;

            if (ox < oy)   // horizontal: block is a wall, and can be shoved
            {
                bool playerLeft = player.Center.X <= block.Center.X;
                bool pushing = player.OnGround &&
                    ((playerLeft && input.MoveX > 0.1f) || (!playerLeft && input.MoveX < -0.1f));
                if (pushing)
                {
                    float moved = block.TryPush(map, (playerLeft ? 1f : -1f) * PushBlock.PushSpeed * (1f / 60f));
                    player.Position.X += moved;
                }
                player.Position.X = playerLeft ? block.Bounds.Left - player.Width - 0.01f : block.Bounds.Right + 0.01f;
                player.Velocity.X = 0f;
            }
            else            // vertical: stand on top / bonk head
            {
                if (player.Center.Y <= block.Center.Y)
                {
                    player.Position.Y = block.Bounds.Top - player.Height;
                    if (player.Velocity.Y > 0f) player.Velocity.Y = 0f;
                    player.MarkGrounded();
                }
                else
                {
                    player.Position.Y = block.Bounds.Bottom + 0.01f;
                    if (player.Velocity.Y < 0f) player.Velocity.Y = 0f;
                }
            }
        }
    }
}
