# Aetheria — Development TODO

Autonomous build log & roadmap. Checked items are implemented **and** covered by
either the xUnit suite or the `--smoke` head-less simulation.

## Milestone 0 — Project scaffolding
- [x] `.NET 9` solution with Engine (lib) / Aetheria (exe) / Tests (xUnit)
- [x] Raylib-cs NuGet reference
- [x] README + TODO plan

## Milestone 1 — Testable core (no rendering)
- [ ] Deterministic RNG (`Rng`) — xorshift, reproducible for tests
- [ ] Value/Perlin noise (`Noise`) — seeded, bounded output
- [ ] `Aabb` axis-aligned box + intersection / sweep helpers
- [ ] `GameConfig` tunables (gravity, speeds, tile size…)
- [ ] Input abstraction (`IInputSource`, `InputState`, scripted source)
- [ ] `TileType` + `TileMap` (grid, solidity, world↔tile, collision queries)
- [ ] `AbilitySet` progression / inventory
- [ ] xUnit: RNG, noise bounds, AABB, tilemap, abilities

## Milestone 2 — Physics & player controller
- [ ] `Entity` base (position, velocity, size, aabb)
- [ ] `Player` (Spark): accel/friction, variable jump, gravity
- [ ] Tile collision resolution (X then Y sweep, ground/ceiling/wall flags)
- [ ] Coyote time + jump buffering
- [ ] Abilities: Double Jump, Dash, Wall-slide/climb/jump, Phase
- [ ] Health / energy, damage, invulnerability window
- [ ] xUnit: jump apex, gravity fall, landing, dash displacement, gating

## Milestone 3 — World & rooms
- [ ] `Room` (tilemap + spawns + doors + pickups + metadata)
- [ ] `RoomGenerator` — deterministic, guaranteed-traversable layouts, organic
      cellular-automata decoration + noise texturing hints
- [ ] `World` graph — current room, edge-door transitions, respawn
- [ ] Ability-gated progression path to the Core
- [ ] xUnit: generator invariants (bounds, spawn not in solid, has floor),
      world transition wiring, reachability of the Core

## Milestone 4 — Enemies & combat
- [ ] `Enemy` base + patroller / flyer / guardian AI
- [ ] Pulse attack + dash damage, projectiles
- [ ] Contact damage to player, enemy death, hazards
- [ ] xUnit: AI stays in bounds, damage/health resolution

## Milestone 5 — Procedural rendering
- [ ] `Palette` + `TextureFactory` (player glow, enemies, tiles, particles)
- [ ] Parallax procedural background (noise nebula + drifting motes)
- [ ] `FollowCamera` clamped to room bounds, smoothing, screen-shake
- [ ] Particle system (dash trail, impacts, ambient motes)
- [ ] HUD (health, energy, ability icons, room name)

## Milestone 6 — Audio
- [ ] `WavSynth` — procedural 16-bit PCM WAV (jump/dash/land/hit/pickup/…)
- [ ] `AudioManager` — load from memory, play, volume
- [ ] xUnit: WAV header validity + sample counts

## Milestone 7 — Game loop & polish
- [ ] `Game` state machine (Title, Playing, Paused, GameOver, Victory)
- [ ] `RaylibInput` real input source
- [ ] `--smoke` head-less simulation harness (N frames, scripted input)
- [ ] Title / victory / death screens, transitions, screen flash
- [ ] Final integration build + full `dotnet test` green

## Backlog / possible future polish
- [ ] Save/load of unlocked abilities & last room
- [ ] Minimap overlay
- [ ] Additional zones, bosses, and a second guardian pattern
- [ ] Gamepad support via Raylib gamepad API
