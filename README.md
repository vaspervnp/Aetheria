# Aetheria: The Bio-Mechanical Abyss

A fast-paced, atmospheric **Metroidvania** built from scratch in **.NET 9** with
**[Raylib-cs](https://github.com/ChrisDill/Raylib-cs)**. Every visual and every
sound is **generated procedurally at runtime** — there are no external art or
audio files anywhere in this repository.

You are **Spark**, a sentient pulse of energy trapped inside a colossal,
abandoned bio-mechanical planetary engine. Navigate the interconnected zones of
the Abyss, reclaim lost traversal abilities, silence the corrupted guardian
algorithms, and reach the Core to reignite the planet.

---

## Features

- **Procedural graphics** — the player, enemies, tiles, particles and parallax
  backgrounds are all drawn into textures at load time using value/Perlin noise,
  cellular automata and custom pixel routines. No `.png` assets.
- **Procedural audio** — retro/synth SFX (jump, dash, land, hit, pickup, unlock,
  hurt, death) are synthesized as 16-bit PCM WAV data in memory and handed to
  Raylib's audio engine. No `.wav`/`.ogg` files on disk.
- **Tight platforming physics** — variable-height jumps, coyote time, jump
  buffering, acceleration/friction, wall sliding.
- **Metroidvania abilities** — Double Jump, Dash, Wall-Climb / Wall-Jump and
  Matter-Phasing. Abilities gate progression through the world.
- **Interconnected rooms** — a graph of hand-tuned, procedurally-decorated rooms
  with seamless edge-door transitions and a follow camera clamped to room bounds.
- **Enemies & combat** — patrolling crawlers, flying floaters, hovering
  sentinels, charging bruisers, and a multi-phase **Warden boss** that shields
  the Core until it falls; all damaged by Spark's pulse attack and dash.
- **Checkpoints & save** — every room is a checkpoint; progress (reclaimed
  abilities + last room) is auto-saved and offered as **Continue** on the title
  and on death. Saved in a tiny text file under your app-data folder.
- **Minimap** — a corner overview of the room chain (visited / current / Core),
  toggled with `Tab` / `M`.
- **Gamepad** — full controller support (sticks/d-pad + face/trigger buttons),
  layered over the keyboard.
- **Testable core** — all simulation logic (physics, collision, noise, world
  graph, abilities, audio synthesis, save/restore) is isolated from rendering so
  it can be exercised head-less by an xUnit suite and a `--smoke` harness.

## Architecture

```
Aetheria.sln
├── src/
│   ├── Aetheria.Engine/     class library — ALL game logic + rendering helpers
│   │   ├── Core/            input abstraction, config, RNG
│   │   ├── Maths/           Aabb, noise
│   │   ├── World/           tiles, tilemap, rooms, world graph, generator
│   │   ├── Entities/        player (Spark), enemies, projectiles
│   │   ├── Abilities/       ability set / progression
│   │   ├── Gfx/             procedural texture factory, palette, camera
│   │   ├── Audio/           WAV synthesizer + audio manager
│   │   └── Game.cs          window, main loop, state machine, rendering
│   └── Aetheria/            thin executable (Program.cs → Game.Run)
└── tests/
    └── Aetheria.Tests/      xUnit — physics, noise, tilemap, world, abilities,
                             audio synthesis, and full player simulation
```

The key design rule: **nothing in the simulation path calls Raylib rendering or
window APIs.** Input is fed through an `IInputSource` abstraction, so the exact
same `World`/`Player` update code runs both under the real game loop and under
the head-less test harness.

## Requirements

- .NET 9 SDK (builds/tests also run on the .NET 10 SDK targeting `net9.0`)
- A desktop OS with OpenGL 3.3 (Windows/Linux/macOS). Raylib's native binary is
  pulled in automatically by the `Raylib-cs` NuGet package.

## Build & Run

```bash
dotnet build                                   # build everything
dotnet run --project src/Aetheria              # play the game
dotnet test                                    # run the xUnit suite
dotnet run --project src/Aetheria -- --smoke   # head-less simulation self-test
```

## Controls

| Action                    | Keys                    |
|---------------------------|-------------------------|
| Move                      | `A` / `D` or `←` / `→`  |
| Jump / Double Jump        | `Space` / `Z`           |
| Up / Wall-climb (hold)    | `W` / `↑` (into a wall) |
| Down / drop through       | `S` / `↓`               |
| Dash                      | `L-Shift` / `K`         |
| Pulse attack              | `J` / `X`               |
| Phase (hold)              | `L-Ctrl` / `F`          |
| Pause                     | `P` / `Esc`             |
| Toggle minimap            | `Tab` / `M`             |
| Continue / confirm        | `Enter` / `Space`       |
| Restart (resume checkpoint) | `R`                   |
| New game (from title/death) | `N`                   |

A gamepad works too: left stick / d-pad to move, **A** jump, **RB** dash,
**X** pulse, **LB** phase, **Start** pause, **B** map, **Select** resume, **Y** new game.

Jump is a dedicated key (`Space`/`Z`) kept separate from Up (`W`/`↑`) so
wall-climbing — which holds Up against a wall — never triggers a wall-jump.

## Story

The engine that once warmed a world has gone dark. Its maintenance algorithms,
starved of purpose, curdled into hostile guardians. Spark — the last live pulse
in the grid — must thread the flooded conduits and collapsed galleries of the
Abyss, reclaiming the movement subroutines the engine shed as it died, until the
Core can be reached and the long night ended.

---

*Built autonomously as a demonstration of end-to-end procedural game development.*
