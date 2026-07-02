using System.Numerics;
using Aetheria.Engine.Core;
using Aetheria.Engine.World;

namespace Aetheria.Engine.Entities;

public enum EffectKind { Pulse, EnemyHit, EnemyDead, PlayerHit }

/// <summary>
/// Resolves all combat for one frame: spawning Spark's pulse, moving
/// projectiles, running enemy AI, and applying every damage interaction. Purely
/// logical — visual/audio feedback is delivered through the optional
/// <c>effect</c> callback so the same code runs under tests and the game loop.
/// </summary>
public static class CombatSystem
{
    private const float PulseSpeed = 360f;

    public static void Step(
        Player player,
        List<Enemy> enemies,
        List<Projectile> projectiles,
        TileMap map,
        float dt,
        Action<EffectKind, Vector2>? effect = null)
    {
        // 1. Spark fires a pulse this frame.
        if (player.WantsPulse)
        {
            projectiles.Add(new Projectile(
                player.PulseOrigin, new Vector2(player.PulseDir * PulseSpeed, 0f),
                4.5f, 0.5f, GameConfig.PulseDamage, fromPlayer: true));
            effect?.Invoke(EffectKind.Pulse, player.PulseOrigin);
        }

        // 2. Move projectiles.
        foreach (var p in projectiles) p.Update(map, dt);

        // 3. Enemy AI / movement (can add enemy projectiles).
        foreach (var e in enemies)
            e.Update(map, player, dt, projectiles);

        // 4a. Spark's pulses vs enemies.
        foreach (var proj in projectiles)
        {
            if (proj.Dead || !proj.FromPlayer) continue;
            foreach (var e in enemies)
            {
                if (!e.AliveState || !proj.Bounds.Intersects(e.Bounds)) continue;
                var kb = new Vector2(MathF.Sign(proj.Velocity.X) * 90f, -40f);
                if (e.TakeDamage(proj.Damage, kb))
                    effect?.Invoke(e.AliveState ? EffectKind.EnemyHit : EffectKind.EnemyDead, e.Center);
                proj.Dead = true;
                break;
            }
        }

        // 4b. Dashing through an enemy damages it (Spark is invulnerable while dashing).
        if (player.Dashing)
        {
            foreach (var e in enemies)
            {
                if (!e.AliveState || !player.Bounds.Intersects(e.Bounds)) continue;
                var kb = new Vector2(player.Facing * 140f, -60f);
                if (e.TakeDamage(GameConfig.DashDamage, kb))
                    effect?.Invoke(e.AliveState ? EffectKind.EnemyHit : EffectKind.EnemyDead, e.Center);
            }
        }

        // 5. Enemy projectiles vs Spark.
        foreach (var proj in projectiles)
        {
            if (proj.Dead || proj.FromPlayer) continue;
            if (!proj.Bounds.Intersects(player.Bounds)) continue;
            if (player.TakeDamage(proj.Damage, proj.Velocity))
                effect?.Invoke(EffectKind.PlayerHit, player.Center);
            proj.Dead = true;
        }

        // 6. Enemy bodies vs Spark.
        foreach (var e in enemies)
        {
            if (!e.AliveState || !e.Bounds.Intersects(player.Bounds)) continue;
            var away = player.Center - e.Center;
            if (away == Vector2.Zero) away = new Vector2(player.Facing, -1);
            if (player.TakeDamage(e.ContactDamage, away))
                effect?.Invoke(EffectKind.PlayerHit, player.Center);
        }

        // 7. Cull the dead.
        projectiles.RemoveAll(p => p.Dead);
        enemies.RemoveAll(e => !e.AliveState);
    }
}
