# Aetheria — Development TODO

Autonomous build log & roadmap. Checked items are implemented **and** covered by
either the xUnit suite or the `--smoke` head-less simulation.

## Milestone 0 — Project scaffolding
- [x] `.NET 9` solution with Engine (lib) / Aetheria (exe) / Tests (xUnit)
- [x] Raylib-cs NuGet reference
- [x] README + TODO plan

## Milestone 1 — Testable core (no rendering)
- [x] Deterministic RNG (`Rng`) — xorshift, reproducible for tests
- [x] Value/Perlin noise (`Noise`) — seeded, bounded output
- [x] `Aabb` axis-aligned box + intersection / sweep helpers
- [x] `GameConfig` tunables (gravity, speeds, tile size…)
- [x] Input abstraction (`IInputSource`, `InputState`, scripted source)
- [x] `TileType` + `TileMap` (grid, solidity, world↔tile, collision queries)
- [x] `AbilitySet` progression / inventory
- [x] xUnit: RNG, noise bounds, AABB, tilemap, abilities

## Milestone 2 — Physics & player controller
- [x] `Entity` base (position, velocity, size, aabb)
- [x] `Player` (Spark): accel/friction, variable jump, gravity
- [x] Tile collision resolution (X then Y sweep, ground/ceiling/wall flags)
- [x] Coyote time + jump buffering
- [x] Abilities: Double Jump, Dash, Wall-slide/climb/jump, Phase
- [x] Health / energy, damage, invulnerability window
- [x] xUnit: jump apex, gravity fall, landing, dash displacement, gating

## Milestone 3 — World & rooms
- [x] `Room` (tilemap + spawns + doors + pickups + metadata)
- [x] `RoomGenerator` — deterministic, guaranteed-traversable layouts, organic
      cellular-automata decoration + noise texturing hints
- [x] `World` graph — current room, edge-door transitions, respawn
- [x] Ability-gated progression path to the Core
- [x] xUnit: generator invariants (bounds, spawn not in solid, has floor),
      world transition wiring, reachability of the Core

## Milestone 4 — Enemies & combat
- [x] `Enemy` base + patroller / flyer / guardian AI
- [x] Pulse attack + dash damage, projectiles
- [x] Contact damage to player, enemy death, hazards
- [x] xUnit: AI stays in bounds, damage/health resolution

## Milestone 5 — Procedural rendering
- [x] `Palette` + `TextureFactory` (player glow, enemies, tiles, particles)
- [x] Parallax procedural background (noise nebula + drifting motes)
- [x] `FollowCamera` clamped to room bounds, smoothing, screen-shake
- [x] Particle system (dash trail, impacts, ambient motes)
- [x] HUD (health, energy, ability icons, room name)

## Milestone 6 — Audio
- [x] `WavSynth` — procedural 16-bit PCM WAV (jump/dash/land/hit/pickup/…)
- [x] `AudioManager` — load from memory, play, volume
- [x] xUnit: WAV header validity + sample counts

## Milestone 7 — Game loop & polish
- [x] `Game` state machine (Title, Playing, Paused, GameOver, Victory)
- [x] `RaylibInput` real input source
- [x] `--smoke` head-less simulation harness (N frames, scripted input)
- [x] Title / victory / death screens, transitions, screen flash
- [x] Final integration build + full `dotnet test` green

## Verification — done
- [x] **82 xUnit tests green** (core, physics, world, combat, audio, smoke, gates)
- [x] **Winnability proven twice**: the conservative reachability model gates the
      whole progression, AND every one of the 4 ability gates is beaten by the
      *real* `Player` physics under scripted input (`GateTraversalTests`)
- [x] **Head-less smoke** (`--smoke`): full sim runs thousands of frames across
      seeds with zero crashes
- [x] **Render self-test** (`--rendertest`): boots a hidden window and runs the
      entire Raylib draw path + every overlay without error; `--shots` captures
      PNGs; `--room N` warps for per-room visual checks
- [x] Debug + Release builds clean; visually verified every room + all overlays

## Backlog — done
- [x] Save/load of unlocked abilities & last room (checkpoint per room; Continue
      on title/death; `SaveGame`/`SaveStore`, tested round-trip + apply)
- [x] Minimap overlay (`Minimap`, room chain, visited/current/Core, Tab/M toggle)
- [x] Additional zone (**Sundered Span**), a **boss** (the Warden: HP bar, Core
      shield, enrage + projectile fans), and a second guardian pattern (**Charger**
      with telegraphed charge) — all with new tests
- [x] Gamepad support via the Raylib gamepad API (sticks/d-pad + buttons)

## Further ideas (not yet done)
- [ ] Settings screen (volume, key rebinding)

---

# PHASE 2 — Massive-scale Metroidvania

Refactor from a linear room chain to a **grid-based world** of fixed-size screens
so it scales to 60+ rooms with N/S/E/W connectivity, biomes, puzzles, an arsenal,
and advanced enemies. Build iteratively, test + commit each subsystem.

## P2.1 — Grid world foundation & 4-directional transitions
- [x] `Biome` enum + per-biome style/palette
- [x] Grid coords + `Biome` on `Room`; direction-based doors (neighbour = grid step)
- [x] Rewrite `World` as a grid (`(gx,gy)` → room), N/S/E/W transitions + entry placement
- [x] Small hand-built 4-room cross to prove all 4 transition directions + tests

## P2.2 — Procedural 60+ room generator across 3 biomes
- [x] `MapGenerator` — connected grid of ≥60 rooms, ≥3 biomes, spanning-tree + loops
- [x] `RoomInterior` — guaranteed-traversable interiors for any door combination
- [x] `TileReachability` flood-fill; tests: room count, connectivity, biomes, every
      door reachable, start→boss reachable

## P2.3 — Biome rendering
- [x] `TextureFactory` per-biome tilesets + backgrounds (Rust Vents / Crystal
      Conduits / Mainframe), selected by current room's biome

## P2.4 — Global state, locked doors & puzzles
- [x] `GameFlags` dictionary (persisted); events on change
- [x] Locked doors: Red Energy, Heavy Blast (flag-gated, block transition until open)
- [x] Switches (shootable / melee), pressure plates + pushable heavy blocks
- [x] Sequence puzzle (hit N switches in order within a time limit)
- [x] Tests for flags, door gating, switch/plate/sequence logic

## P2.5 — Expanded arsenal
- [ ] Weapon inventory + switching; Blaster / Scatter-Shot / Plasma Blade
- [ ] Scatter breaks cracked walls; Blade melee deflects projectiles + melee switches
- [ ] Procedural SFX per weapon; tests for weapon logic

## P2.6 — Advanced enemies
- [ ] Hover-Turret (predictive aim), Stalker-Drone (pathfinding chase),
      Armored Crawler (frontal-immune)
- [ ] Tests for each behaviour
