using System.Numerics;
using Raylib_cs;
using Aetheria.Engine.Abilities;
using Aetheria.Engine.Audio;
using Aetheria.Engine.Core;
using Aetheria.Engine.Entities;
using Aetheria.Engine.Gfx;
using Aetheria.Engine.World;

namespace Aetheria.Engine;

public enum GameState { Title, Playing, Paused, Dead, Victory }

/// <summary>
/// The top-level game: window/loop lifecycle, the Title→Playing→Dead/Victory
/// state machine, and all procedural rendering. Simulation is delegated to the
/// same engine classes the head-less tests exercise.
/// </summary>
public sealed class Game : IDisposable
{
    private const int TS = GameConfig.TileSize;
    private const float FixedMaxDt = 1f / 30f;

    private int _sw = GameConfig.WindowWidth;
    private int _sh = GameConfig.WindowHeight;
    private readonly uint _seed;

    private IInputSource _input = null!;
    private TextureFactory _tex = null!;
    private AudioManager _audio = null!;
    private FollowCamera _cam = null!;
    private ParticleSystem _particles = null!;

    private World.World _world = null!;
    private Player _player = null!;
    private List<Enemy> _enemies = new();
    private List<Projectile> _projectiles = new();

    private GameState _state = GameState.Title;
    private float _time;
    private float _flash;
    private float _bannerTime;
    private string _banner = "";
    private float _endTime;
    private int _deaths;
    private bool _hidden;
    private readonly Rng _fx = new(0x51A2);

    public Game(uint seed = 1337) => _seed = seed;

    // ---- lifecycle ----------------------------------------------------------
    public void Run()
    {
        Init();
        while (!Raylib.WindowShouldClose())
        {
            float dt = MathF.Min(Raylib.GetFrameTime(), FixedMaxDt);
            _time += dt;
            Update(_input.Poll(), dt);
            Draw();
        }
        Dispose();
    }

    /// <summary>
    /// Boots the window hidden, runs the full update+draw path for a fixed number
    /// of frames against scripted input, then renders every overlay state once.
    /// Verifies the entire Raylib rendering path (texture upload, draw calls,
    /// overlays) executes without runtime errors — no display interaction needed.
    /// Returns 0 on success, 2 if the window/GL context could not be created.
    /// </summary>
    public int RunSelfTest(int frames = 600, string? shotDir = null, int startRoom = 0)
    {
        _hidden = shotDir == null;   // screenshot capture needs a presented framebuffer
        try { Init(); }
        catch (Exception e) { Console.WriteLine("render self-test: init failed: " + e.Message); return 2; }

        NewGame();
        if (startRoom > 0)
        {
            _world.DebugEnter(startRoom, _player);
            LoadRoomEntities();
            _cam.SnapTo(_player.Center);
        }
        _state = GameState.Playing;
        for (int i = 0; i < frames; i++)
        {
            Update(SelfTestInput(i), 1f / 60f);
            Draw();
            if (shotDir != null && (i == 150 || i == 350)) Shot(shotDir, i == 150 ? "play1" : "play2");
        }
        // exercise (and optionally capture) every overlay's draw path
        foreach (var st in new[] { GameState.Title, GameState.Paused, GameState.Dead, GameState.Victory })
        {
            _state = st;
            Draw();
            if (shotDir != null) Shot(shotDir, st.ToString().ToLowerInvariant());
        }
        Dispose();
        Console.WriteLine($"render self-test: {frames} frames rendered OK");
        return 0;
    }

    private static void Shot(string dir, string name)
    {
        // raylib prepends the working directory, so take with a relative name then relocate.
        Directory.CreateDirectory(dir);
        string file = "aetheria_" + name + ".png";
        Raylib.TakeScreenshot(file);
        string src = Path.Combine(Directory.GetCurrentDirectory(), file);
        if (File.Exists(src))
        {
            File.Copy(src, Path.Combine(dir, name + ".png"), true);
            File.Delete(src);
        }
    }

    private static InputState SelfTestInput(int i)
    {
        int j = i % 45;
        return new InputState(
            moveX: 1f, up: (i % 120) >= 60,
            jumpPressed: j == 0, jumpHeld: j < 11, jumpReleased: j == 11,
            dashPressed: (i % 80) == 20, attackPressed: (i % 40) == 5,
            phaseHeld: (i % 240) is >= 120 and < 190);
    }

    private void Init()
    {
        var flags = ConfigFlags.VSyncHint | ConfigFlags.Msaa4xHint;
        if (_hidden) flags |= ConfigFlags.HiddenWindow;
        Raylib.SetConfigFlags(flags);
        Raylib.InitWindow(_sw, _sh, GameConfig.Title);
        Raylib.SetExitKey(KeyboardKey.Null);           // ESC is used for pause, not exit
        Raylib.SetTargetFPS(60);

        _audio = new AudioManager();
        _tex = new TextureFactory(_seed);
        _cam = new FollowCamera();
        _particles = new ParticleSystem();
        _input = new RaylibInput();

        NewGame();
        _state = GameState.Title;
    }

    private void NewGame()
    {
        _world = WorldBuilder.Build(_seed);
        _world.RoomChanged += OnRoomChanged;
        _world.AbilityUnlocked += OnAbilityUnlocked;
        _player = new Player(_world.StartSpawn);
        _player.Respawn(_world.StartSpawn);
        LoadRoomEntities();
        _particles.Clear();
        _cam.SnapTo(_player.Center);
        _deaths = 0;
        _flash = 0f;
        _bannerTime = 0f;
    }

    private void LoadRoomEntities()
    {
        _enemies = new List<Enemy>();
        foreach (var s in _world.Current.Enemies)
            _enemies.Add(Enemy.FromSpawn(s, TS));
        _projectiles = new List<Projectile>();
    }

    private void OnRoomChanged(Room _, Room next)
    {
        LoadRoomEntities();
        _particles.Clear();
        _cam.SnapTo(_player.Center);
        _flash = MathF.Max(_flash, 0.5f);
        _audio.Play(Sfx.Transition);
    }

    private void OnAbilityUnlocked(AbilityType ability)
    {
        _audio.Play(Sfx.Unlock);
        _flash = MathF.Max(_flash, 0.55f);
        _banner = AbilitySet.DisplayName(ability) + " acquired";
        _bannerTime = 3.2f;
        _particles.Burst(_player.Center, 46, Palette.PickupGlow, 150f, 0.8f, 3f);
        _cam.Shake(3.5f, 0.35f);
    }

    // ---- update -------------------------------------------------------------
    private void Update(InputState input, float dt)
    {
        if (_flash > 0f) _flash = MathF.Max(0f, _flash - dt * 1.6f);
        if (_bannerTime > 0f) _bannerTime -= dt;

        switch (_state)
        {
            case GameState.Title:
                if (input.Confirm) { NewGame(); _state = GameState.Playing; }
                break;
            case GameState.Playing:
                UpdatePlaying(input, dt);
                break;
            case GameState.Paused:
                _particles.Update(dt);
                if (input.Pause || input.Confirm) _state = GameState.Playing;
                break;
            case GameState.Dead:
                _endTime += dt;
                _particles.Update(dt);
                if (input.Restart) { NewGame(); _state = GameState.Playing; }
                break;
            case GameState.Victory:
                _endTime += dt;
                _particles.Update(dt);
                if (_endTime > 1.5f) EmitVictorySparkle();
                if (input.Restart) { NewGame(); _state = GameState.Title; }
                break;
        }
    }

    private void UpdatePlaying(InputState input, float dt)
    {
        if (input.Pause) { _state = GameState.Paused; return; }

        var map = _world.Current.Map;
        _player.Update(input, map, dt);
        CombatSystem.Step(_player, _enemies, _projectiles, map, dt, OnEffect);
        _world.Update(dt, _player);

        HandlePlayerEvents();

        if (_player.Dashing)
            _particles.Trail(_player.Center, Palette.SparkTrail);

        // ambient drifting motes
        if (_fx.Chance(0.10f))
        {
            var p = new Vector2(_cam.Position.X + _fx.Range(-260f, 260f),
                                _cam.Position.Y + _fx.Range(-150f, 150f));
            _particles.Add(p, new Vector2(_fx.Range(-6f, 6f), _fx.Range(-14f, -4f)),
                _fx.Range(1.4f, 3f), _fx.Range(0.6f, 1.4f), Palette.Rgb(90, 150, 190, 120));
        }

        _particles.Update(dt);

        float rw = map.PixelWidth, rh = map.PixelHeight;
        _cam.Update(_player.Center, rw, rh, _sw, _sh, dt);

        if (!_player.Alive)
        {
            _state = GameState.Dead;
            _endTime = 0f;
            _deaths++;
            return;
        }
        if (_world.ReachedCore)
        {
            _state = GameState.Victory;
            _endTime = 0f;
            _audio.Play(Sfx.Victory);
            _cam.Shake(5f, 0.6f);
        }
    }

    private void HandlePlayerEvents()
    {
        if (_player.JustJumped) { _audio.Play(Sfx.Jump); Dust(); }
        if (_player.JustDoubleJumped) { _audio.Play(Sfx.DoubleJump); _particles.Burst(_player.Center, 10, Palette.Spark, 120f, 0.5f, 2.4f); }
        if (_player.JustWallJumped) { _audio.Play(Sfx.Jump); Dust(); }
        if (_player.JustDashed) { _audio.Play(Sfx.Dash); _particles.Burst(_player.Center, 14, Palette.SparkTrail, 160f, 0.4f, 2.6f); _cam.Shake(2f, 0.2f); }
        if (_player.JustLanded) Dust();
        if (_player.JustHurt)
        {
            _audio.Play(_player.Alive ? Sfx.Hurt : Sfx.Death);
            _flash = MathF.Max(_flash, 0.6f);
            _cam.Shake(6f, 0.4f);
            _particles.Burst(_player.Center, 20, Palette.Hazard, 150f, 0.6f, 3f);
        }
    }

    private void Dust()
    {
        var at = new Vector2(_player.Center.X, _player.Bounds.Bottom);
        _particles.Burst(at, 6, Palette.Rgb(120, 150, 180, 160), 60f, 0.35f, 2f, gravity: 200f, additive: false);
    }

    private void OnEffect(EffectKind kind, Vector2 pos)
    {
        switch (kind)
        {
            case EffectKind.Pulse:
                _audio.Play(Sfx.Pulse);
                _particles.Burst(pos, 6, Palette.Spark, 90f, 0.3f, 2f);
                break;
            case EffectKind.EnemyHit:
                _audio.Play(Sfx.EnemyHit);
                _particles.Burst(pos, 8, Palette.Rgb(255, 220, 180), 120f, 0.4f, 2.2f);
                break;
            case EffectKind.EnemyDead:
                _audio.Play(Sfx.EnemyDead);
                _particles.Burst(pos, 24, Palette.Rgb(255, 180, 120), 170f, 0.7f, 3f);
                _cam.Shake(3f, 0.25f);
                break;
            case EffectKind.PlayerHit:
                // handled via player.JustHurt for a single, consistent reaction
                break;
        }
    }

    private void EmitVictorySparkle()
    {
        if (_world.Current.CoreCenter is { } c && _fx.Chance(0.5f))
            _particles.Burst(c, 12, Palette.CoreGlow, 130f, 1.2f, 3f);
    }

    // ---- draw ---------------------------------------------------------------
    private void Draw()
    {
        _sw = Raylib.GetScreenWidth();
        _sh = Raylib.GetScreenHeight();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Void);
        DrawBackground();

        if (_state is GameState.Playing or GameState.Paused or GameState.Dead or GameState.Victory)
        {
            DrawWorld();
            Hud.Draw(_player, _world, _sw, _sh);
        }

        if (_flash > 0f)
            Raylib.DrawRectangle(0, 0, _sw, _sh, Raylib.Fade(new Color(220, 245, 255, 255), _flash * 0.5f));

        DrawOverlay();
        DrawBanner();
        Raylib.EndDrawing();
    }

    private void DrawBackground()
    {
        var src = new Rectangle(0, 0, _tex.Background.Width, _tex.Background.Height);
        var dst = new Rectangle(0, 0, _sw, _sh);
        Raylib.DrawTexturePro(_tex.Background, src, dst, Vector2.Zero, 0f, Color.White);
    }

    private void DrawWorld()
    {
        var cam = _cam.ToCamera2D(_sw, _sh);
        Raylib.BeginMode2D(cam);
        DrawTiles(cam);
        DrawPickups();
        DrawCore();
        DrawEnemies();
        DrawProjectiles();
        DrawPlayer();
        _particles.Draw();
        Raylib.EndMode2D();
    }

    private void DrawTiles(Camera2D cam)
    {
        var map = _world.Current.Map;
        float halfW = _sw * 0.5f / cam.Zoom, halfH = _sh * 0.5f / cam.Zoom;
        int minX = Math.Max(0, (int)((cam.Target.X - halfW) / TS) - 1);
        int maxX = Math.Min(map.Width - 1, (int)((cam.Target.X + halfW) / TS) + 1);
        int minY = Math.Max(0, (int)((cam.Target.Y - halfH) / TS) - 1);
        int maxY = Math.Min(map.Height - 1, (int)((cam.Target.Y + halfH) / TS) + 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var t = map.Get(x, y);
                if (t == TileType.Empty) continue;
                int variant = (x * 31 + y * 17) & 0x7fffffff;
                var tex = _tex.Tile(t, variant);
                var pos = new Vector2(x * TS, y * TS);
                Color tint = Color.White;
                if (t == TileType.Phase)
                    tint = new Color(255, 255, 255, _player.Phasing ? 70 : 235);
                Raylib.DrawTextureV(tex, pos, tint);
            }
        }
    }

    private void DrawPickups()
    {
        foreach (var pk in _world.Current.Pickups)
        {
            if (pk.Taken) continue;
            var c = pk.WorldCenter(TS);
            float bob = MathF.Sin(_time * 3f) * 3f;
            var p = new Vector2(c.X, c.Y + bob);
            float rot = _time * 90f;
            Raylib.BeginBlendMode(BlendMode.Additive);
            Raylib.DrawPoly(p, 4, 12f + MathF.Sin(_time * 5f) * 1.5f, rot, Raylib.Fade(Palette.Pickup, 0.35f));
            Raylib.EndBlendMode();
            Raylib.DrawPoly(p, 4, 7f, rot, Palette.Pickup);
            Raylib.DrawPoly(p, 4, 3.5f, rot, Palette.PickupGlow);
        }
    }

    private void DrawCore()
    {
        if (_world.Current.CoreCenter is not { } c) return;
        float pulse = 1f + MathF.Sin(_time * 3f) * 0.12f;
        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawCircleV(c, 26f * pulse, Raylib.Fade(Palette.Core, 0.25f));
        Raylib.DrawCircleV(c, 16f * pulse, Raylib.Fade(Palette.Core, 0.4f));
        Raylib.EndBlendMode();
        Raylib.DrawCircleV(c, 10f, Palette.Core);
        Raylib.DrawCircleV(c, 5f, Palette.CoreGlow);
        for (int i = 0; i < 3; i++)
        {
            float r = 20f + i * 6f + MathF.Sin(_time * 2f + i) * 2f;
            Raylib.DrawRing(c, r, r + 1.2f, 0, 360, 32, Raylib.Fade(Palette.Core, 0.5f));
        }
    }

    private void DrawEnemies()
    {
        foreach (var e in _enemies)
        {
            var c = e.Center;
            switch (e.Kind)
            {
                case EnemyKind.Crawler:
                    Raylib.BeginBlendMode(BlendMode.Additive);
                    Raylib.DrawCircleV(c, 11f, Raylib.Fade(Palette.Crawler, 0.3f));
                    Raylib.EndBlendMode();
                    Raylib.DrawRectangleRounded(new Rectangle(e.Position.X, e.Position.Y, e.Width, e.Height), 0.5f, 6, Palette.Crawler);
                    for (int i = -1; i <= 1; i++)
                        Raylib.DrawLineEx(new Vector2(c.X + i * 4, e.Bounds.Bottom),
                            new Vector2(c.X + i * 4 + e.Facing * 2, e.Bounds.Bottom + 3), 1.5f, Palette.Crawler);
                    Raylib.DrawCircleV(new Vector2(c.X + e.Facing * 3, c.Y - 1), 2f, Palette.EnemyEye);
                    break;
                case EnemyKind.Floater:
                    Raylib.BeginBlendMode(BlendMode.Additive);
                    Raylib.DrawCircleV(c, 12f, Raylib.Fade(Palette.Floater, 0.3f));
                    Raylib.EndBlendMode();
                    Raylib.DrawCircleV(c, e.Width * 0.5f, Palette.Floater);
                    Raylib.DrawRing(c, 9f, 10.5f, 0, 360, 20, Raylib.Fade(Palette.Floater, 0.7f));
                    Raylib.DrawCircleV(new Vector2(c.X + e.Facing * 2, c.Y), 2f, Palette.EnemyEye);
                    break;
                case EnemyKind.Sentinel:
                    Raylib.BeginBlendMode(BlendMode.Additive);
                    Raylib.DrawCircleV(c, 18f, Raylib.Fade(Palette.Sentinel, 0.3f));
                    Raylib.EndBlendMode();
                    Raylib.DrawPoly(c, 6, e.Width * 0.6f, _time * 40f, Palette.Sentinel);
                    Raylib.DrawPoly(c, 6, e.Width * 0.4f, -_time * 30f, Palette.Rgb(60, 30, 20));
                    Raylib.DrawCircleV(c, 3.5f, Palette.EnemyEye);
                    break;
            }
        }
    }

    private void DrawProjectiles()
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        foreach (var p in _projectiles)
        {
            var col = p.FromPlayer ? Palette.Spark : Palette.Hazard;
            Raylib.DrawCircleV(p.Position, p.Radius * 2.2f, Raylib.Fade(col, 0.35f));
            Raylib.DrawCircleV(p.Position, p.Radius, col);
        }
        Raylib.EndBlendMode();
    }

    private void DrawPlayer()
    {
        if (!_player.Alive) return;
        var c = _player.Center;
        bool blink = _player.Invulnerable && ((int)(_time * 20f) & 1) == 0;
        float a = blink ? 0.35f : 1f;
        float pulse = 1f + MathF.Sin(_time * 10f) * 0.12f;

        Raylib.BeginBlendMode(BlendMode.Additive);
        Raylib.DrawCircleV(c, 12f * pulse, Raylib.Fade(_player.Phasing ? Palette.Phase : Palette.Spark, 0.28f * a));
        Raylib.DrawCircleV(c, 7f * pulse, Raylib.Fade(Palette.Spark, 0.45f * a));
        Raylib.EndBlendMode();

        Raylib.DrawCircleV(c, 5f, Raylib.Fade(_player.Phasing ? Palette.PhaseGlow : Palette.Spark, a));
        Raylib.DrawCircleV(c, 2.6f, Raylib.Fade(Palette.SparkCore, a));
        Raylib.DrawCircleV(new Vector2(c.X + _player.Facing * 2.2f, c.Y - 0.5f), 1.1f, Raylib.Fade(Palette.Void, a));
    }

    // ---- overlays -----------------------------------------------------------
    private void DrawOverlay()
    {
        switch (_state)
        {
            case GameState.Title: DrawTitle(); break;
            case GameState.Paused: DrawPaused(); break;
            case GameState.Dead: DrawDead(); break;
            case GameState.Victory: DrawVictory(); break;
        }
    }

    private void Dim(float a) => Raylib.DrawRectangle(0, 0, _sw, _sh, Raylib.Fade(Color.Black, a));

    private void CenterText(string text, int y, int fontSize, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, (_sw - w) / 2, y, fontSize, color);
    }

    private void DrawTitle()
    {
        Dim(0.35f);
        int cy = _sh / 2;
        float g = 0.6f + 0.4f * MathF.Sin(_time * 2f);
        CenterText("AETHERIA", cy - 150, 84, Raylib.Fade(Palette.Spark, 0.85f + 0.15f * g));
        CenterText("THE BIO-MECHANICAL ABYSS", cy - 78, 26, Palette.InkDim);
        CenterText("You are Spark. Reignite the Core.", cy - 10, 20, Palette.Ink);
        CenterText("Press  ENTER  /  SPACE  to awaken", cy + 40, 22,
            Raylib.Fade(Palette.Pickup, 0.6f + 0.4f * MathF.Sin(_time * 4f)));
        CenterText("Move: A/D  ·  Jump: SPACE  ·  Dash: SHIFT  ·  Pulse: J  ·  Climb: hold into wall + W  ·  Phase: hold F",
            cy + 130, 15, Palette.InkDim);
    }

    private void DrawPaused()
    {
        Dim(0.5f);
        CenterText("PAUSED", _sh / 2 - 30, 56, Palette.Ink);
        CenterText("Press  P  /  ESC  to resume", _sh / 2 + 34, 20, Palette.InkDim);
    }

    private void DrawDead()
    {
        Dim(MathF.Min(0.6f, _endTime));
        Raylib.DrawRectangle(0, 0, _sw, _sh, Raylib.Fade(Palette.Hazard, 0.06f));
        CenterText("SIGNAL LOST", _sh / 2 - 40, 66, Raylib.Fade(Palette.Hazard, 0.9f));
        CenterText("The pulse fades into the dark…", _sh / 2 + 30, 20, Palette.InkDim);
        CenterText("Press  R  to reignite", _sh / 2 + 66, 22,
            Raylib.Fade(Palette.Ink, 0.5f + 0.5f * MathF.Sin(_time * 4f)));
    }

    private void DrawVictory()
    {
        Raylib.DrawRectangle(0, 0, _sw, _sh, Raylib.Fade(Palette.Core, 0.08f + 0.04f * MathF.Sin(_time * 3f)));
        int cy = _sh / 2;
        CenterText("THE CORE REKINDLES", cy - 90, 60, Raylib.Fade(Palette.Core, 0.9f));
        CenterText("The engine breathes again. The long night ends.", cy - 20, 22, Palette.Ink);
        CenterText($"Abilities reclaimed: {_player.Abilities.Count}/{AbilitySet.All.Count}    Falls: {_deaths}",
            cy + 24, 20, Palette.InkDim);
        CenterText("Press  R  to return to the title", cy + 80, 20,
            Raylib.Fade(Palette.Ink, 0.5f + 0.5f * MathF.Sin(_time * 4f)));
    }

    private void DrawBanner()
    {
        if (_bannerTime <= 0f || _state != GameState.Playing) return;
        float a = Math.Clamp(_bannerTime, 0f, 1f);
        int fs = 30;
        int w = Raylib.MeasureText(_banner, fs);
        int x = (_sw - w) / 2, y = 90;
        Raylib.DrawRectangle(x - 20, y - 10, w + 40, fs + 20, Raylib.Fade(Palette.Panel, a));
        Raylib.DrawText(_banner, x, y, fs, Raylib.Fade(Palette.Pickup, a));
    }

    public void Dispose()
    {
        _tex?.Dispose();
        _audio?.Dispose();
        if (Raylib.IsWindowReady()) Raylib.CloseWindow();
    }
}
