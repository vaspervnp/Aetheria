using System.Numerics;
using Aetheria.Engine.Abilities;
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
        // 1. Spark fires the selected weapon this frame.
        if (player.WantsFire) FireWeapon(player, projectiles, effect);

        // 2. Move projectiles (Scatter pellets shatter cracked walls).
        foreach (var p in projectiles) p.Update(map, dt);

        // 3. Enemy AI / movement (can add enemy projectiles).
        foreach (var e in enemies)
            e.Update(map, player, dt, projectiles);

        // 3b. Plasma Blade: damage enemies in the arc and deflect enemy shots.
        if (player.MeleeHitbox is { } blade)
        {
            foreach (var e in enemies)
            {
                if (!e.AliveState || !blade.Intersects(e.Bounds)) continue;
                if (e.ArmoredFront && e.IsFrontalHit(player.Center.X)) { effect?.Invoke(EffectKind.EnemyHit, e.Center); continue; }
                var kb = new Vector2(player.Facing * 170f, -70f);
                if (e.TakeDamage(GameConfig.BladeDamage, kb))
                    effect?.Invoke(e.AliveState ? EffectKind.EnemyHit : EffectKind.EnemyDead, e.Center);
            }
            foreach (var proj in projectiles)
            {
                if (proj.Dead || proj.FromPlayer || !blade.Intersects(proj.Bounds)) continue;
                float sp = MathF.Max(320f, proj.Velocity.Length());
                proj.Velocity = new Vector2(player.Facing * sp, proj.Velocity.Y * 0.2f);
                proj.FromPlayer = true;
                proj.Damage = Math.Max(proj.Damage, 2);
                effect?.Invoke(EffectKind.EnemyHit, proj.Position);
            }
        }

        // 4a. Spark's pulses vs enemies.
        foreach (var proj in projectiles)
        {
            if (proj.Dead || !proj.FromPlayer) continue;
            foreach (var e in enemies)
            {
                if (!e.AliveState || !proj.Bounds.Intersects(e.Bounds)) continue;
                if (e.ArmoredFront && e.IsFrontalHit(proj.Position.X))
                {
                    effect?.Invoke(EffectKind.EnemyHit, e.Center);   // deflected off the armour
                    proj.Dead = true;
                    break;
                }
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
                if (e.ArmoredFront && e.IsFrontalHit(player.Center.X)) continue;
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

    private static void FireWeapon(Player player, List<Projectile> projectiles, Action<EffectKind, Vector2>? effect)
    {
        var o = player.FireOrigin;
        switch (player.FireWeapon)
        {
            case WeaponType.Blaster:
                projectiles.Add(new Projectile(o, new Vector2(player.FireDir * GameConfig.BlasterSpeed, 0f),
                    4.5f, 0.5f, GameConfig.BlasterDamage, fromPlayer: true));
                break;
            case WeaponType.Scatter:
            {
                float baseAng = player.FireDir > 0 ? 0f : MathF.PI;
                float spread = GameConfig.ScatterSpreadDeg * MathF.PI / 180f;
                int n = GameConfig.ScatterPellets;
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0.5f : i / (float)(n - 1);
                    float ang = baseAng + (t - 0.5f) * spread;
                    var v = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * GameConfig.ScatterSpeed;
                    projectiles.Add(new Projectile(o, v, 3.2f, GameConfig.ScatterLife, GameConfig.ScatterDamage, true)
                    {
                        BreaksWalls = true,
                    });
                }
                break;
            }
        }
    }
}
