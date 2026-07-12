# Aetheria — Amstrad CPC 6128 (Z80) Port Specification

**Purpose.** This is a self-contained design + implementation spec for re-creating
**Aetheria: The Bio-Mechanical Abyss** — a procedural Metroidvania originally
written in .NET/Raylib — as a native **Amstrad CPC 6128** game in **Z80
assembly**. Feed this whole file to a coding AI (or a human) and build from it.

It is **not** a code port. The original uses floating point, a 480×270 canvas and
megabytes of RAM; the CPC has a 4 MHz Z80 with no FPU, 128 KB of banked RAM and a
160×200 screen. This document therefore describes the **game's behaviour and feel
precisely**, then prescribes a **faithful 8-bit adaptation** with concrete CPC
numbers, data formats and routines. Where the original's values matter for *feel*,
they are given as the authority; CPC-scaled values are provided as tuned starting
points.

> **Golden rule for the implementer:** preserve the *feel* (tight, fast platforming;
> ability-gated exploration; readable procedural rooms; punchy retro juice), not the
> exact pixels. When a CPC constraint conflicts with fidelity, simplify — but keep
> movement responsive and the world provably completable.

---

## 0. Table of contents

1. Target hardware & adaptation philosophy
2. The game in one page (concept, tone, loop)
3. Units, coordinates & fixed-point math (Z80)
4. The player — "Spark" (state machine, physics, abilities)
5. Tiles & collision
6. World structure (grid of screens, doors, biomes, gating)
7. Global state, locked doors & puzzles
8. Arsenal (weapons & projectiles)
9. Enemies (eight kinds) & combat
10. Rendering on the CPC (Mode 0, screen RAM, tiles, sprites, HUD)
11. Audio (AY-3-8912 SFX)
12. Input (keyboard / joystick)
13. Memory map, banking & data layout
14. Pre-baking the world (recommended) vs on-CPU generation
15. Suggested module breakdown & milestone build order
16. Minimum-viable vs stretch
17. Appendix A — original tuning constants
18. Appendix B — CPC per-frame constants (8.8 fixed point)
19. Appendix C — enums & data record formats
20. Appendix D — key Z80 routines (screen address, Mode-0 plot, collision)

---

## 1. Target hardware & adaptation philosophy

**Amstrad CPC 6128:**
- **CPU:** Zilog Z80A @ 4 MHz. No multiply/divide, no floating point. 8-bit with a
  16-bit address bus; use lookup tables and shifts/adds for arithmetic.
- **RAM:** 128 KB = base 64 KB + 64 KB expansion, paged in 16 KB banks via the Gate
  Array RAM-config register (`OUT (&7Fxx)`, values `&C0..&FF`).
- **Video:** CRTC 6845 + Gate Array. Screen RAM is 16 KB, default base `&C000`.
  - **Mode 0:** 160×200, **16 inks**, 2 pixels/byte, 80 bytes/line. *(Use this.)*
  - Mode 1: 320×200, 4 inks. Mode 2: 640×200, 2 inks.
  - **27 hardware colours** (3 levels R,G,B). The mode picks 16 "inks" from those 27.
  - Screen layout is **interleaved** (see §10 and Appendix D).
- **Frame:** 50 Hz (PAL). The Gate Array raises 6 interrupts/frame (300 Hz).
  Sync gameplay to the 50 Hz VSYNC.
- **Sound:** AY-3-8912 PSG — 3 square-tone channels + 1 noise + 1 envelope.
- **Input:** keyboard matrix (read through the PSG port) + up to 2 joysticks.
- **Storage:** 3" floppy via AMSDOS (the "6128" has the disk drive). Use it for save
  files and for streaming/paging world data.

**Adaptation philosophy — what to keep, scale, cut:**

| Original | CPC adaptation |
|---|---|
| 480×270 canvas, 16 px tiles, 40×22 scrolling rooms | **Mode 0 160×200, 8×8 tiles, single-screen rooms of 20×20 tiles** (no in-room scroll). HUD in a strip. |
| Float physics, `dt`-scaled | **8.8 fixed point, fixed 50 Hz step** (one update per frame). |
| 60+ procedurally generated rooms at runtime + solver | **Pre-bake the world on PC** (run the original generator, export a room-descriptor table). Build each room's tilemap on entry from its descriptor. |
| Smooth follow camera | **Screen-flip transitions** (instant, or a 2–4 step wipe + palette flash). |
| Per-pixel procedural textures | **Small hand/procedurally-authored 8×8 tile set per biome** (a few dozen tiles), plus a couple of procedural touches (noise dither, flicker) if cheap. |
| 27 SFX synthesised as WAV | **~14 AY SFX** built from tone sweeps + noise bursts. |
| Everything | Keep: movement feel, abilities, the 8 enemy behaviours, 3 weapons, puzzles, biomes, ability-gated progression, boss, save. |

---

## 2. The game in one page

**Concept.** You are **Spark**, a sentient pulse of energy inside a colossal,
abandoned bio-mechanical planetary engine. Explore a large interconnected map,
reclaim four lost **traversal abilities**, wield three **weapons**, solve
**switch/block/sequence puzzles**, defeat the **Warden** guarding the Core, and
reignite the planet.

**Tone.** Atmospheric, lonely, fast-paced, punchy. Dark backgrounds, glowing
foreground, chunky readable pixels, tight controls, satisfying zaps and thuds.

**Core loop.**
1. Enter a screen → its tilemap builds from a descriptor; enemies/pickups/puzzles
   spawn.
2. Platform, fight, and solve to reach the screen's exits.
3. Walk into an edge door → transition to the neighbouring screen.
4. Find an **ability pickup** → it unlocks a new move and opens **ability-gated
   doors** (glowing energy seals) into the next biome.
5. Reach and defeat the **Warden**; touch the **Core** → victory.

**Game states:** `TITLE → PLAYING ⇄ PAUSED`, `PLAYING → DEAD → (resume checkpoint /
new game)`, `PLAYING → VICTORY → TITLE`.

**Progression gates (the spine — must remain solvable):**
`Start (Rust Vents, no abilities)` → find **Double Jump** → cross into **Crystal
Conduits** → find **Dash** → cross into **Mainframe** → find **Phase** (and the
bonus **Wall-Cling**) → open the Core seal → defeat the **Warden** → touch the
**Core**. Weapons (Scatter-Shot, Plasma Blade) are found along the way but are
**not required** to finish (the Blaster suffices).

---

## 3. Units, coordinates & fixed-point math (Z80)

- **World unit = 1 CPC pixel.** Screen is 160×200.
- **Tile = 8×8 pixels.** Playfield = **20 tiles wide × 20 tall** (160×160), with a
  **HUD strip** occupying the remaining rows (see §10). Tile coordinates: `tx = x>>3`,
  `ty = y>>3`.
- **Fixed point: 8.8** — a signed 16-bit value where the high byte is whole pixels
  and the low byte is 1/256ths. Positions and velocities are stored 8.8.
  - Integer pixel of a fixed value = high byte (`H`), with sign handling.
  - Add velocity to position: 16-bit add. Apply gravity: 16-bit add of a small
    constant to `vy`.
  - **No multiplies needed** in the hot path — accelerations are additive; sines use
    a 256-entry signed LUT; enemy aiming uses sign/threshold logic (§9), not vectors.
- **Fixed 50 Hz step.** One `Update` per frame. Do **not** scale by delta time.
- **Anti-tunnelling:** keep every per-frame speed **< 8 px** (one tile). All values
  in Appendix B satisfy this, so collision can move one axis then snap to the
  leading tile edge without sub-stepping.

---

## 4. The player — "Spark"

**Body.** ~**6×7 px** (roughly one tile). Store `x,y` as 8.8, `vx,vy` as 8.8,
`facing` (±1), plus the flags/timers below. Draw as a glowing orb (bright core +
1-px halo); tint shifts when phasing.

**Survivability.** `health` (start/max **5**, pips), `energy` (0..**100**,
regenerates ~**22/s** ≈ +0.44/frame in 8.8 unless phasing). Weapons and phasing
cost energy. Invulnerability window after a hit (~**1.0 s** = 50 frames), during
which Spark blinks.

### 4.1 Movement state machine (per frame, in order)

1. **Tick timers** (all counted in frames): `coyote`, `jumpBuffer`, `dashTimer`,
   `dashCooldown`, `wallJumpLock`, `invuln`, `fireCooldown`, `bladeTimer`.
2. **Dash** — if dash pressed & Dash unlocked & `dashCooldown==0` & not dashing:
   set `dashTimer`, `dashDir = input.x else facing`, `dashCooldown`. While
   `dashTimer>0`, force `vx = dashDir*DASH`, `vy = 0` (ignore gravity), grant brief
   i-frames.
3. **Phase** — if phase held & Phase unlocked & `energy>MIN`: `phasing = true`,
   drain energy; else off. While phasing, collision ignores **Phase** tiles.
4. **Horizontal** — if not dashing and `wallJumpLock==0`: accelerate `vx` toward
   `input.x*RUN` using `GROUND_ACCEL`/`AIR_ACCEL`; if no input, decelerate toward 0
   with `GROUND_FRICTION`/`AIR_FRICTION`.
5. **Jump** (buffered + coyote):
   - On jump press, set `jumpBuffer`.
   - If buffered and (on ground or `coyote>0`): `vy = -JUMP`, clear coyote/buffer,
     `hasDoubleJumped = false`. **Ground jump.**
   - Else if buffered and airborne and touching a wall and **Wall-Cling** unlocked:
     **wall jump** — `vy = -WALLJUMP_Y`, `vx = -wallDir*WALLJUMP_X`, set
     `wallJumpLock`, refresh double jump.
   - Else if buffered and airborne and **Double Jump** unlocked and not yet used:
     `vy = -DOUBLEJUMP`, `hasDoubleJumped = true`. **Double jump.**
   - **Variable height:** on jump *release* while `vy<0`, multiply `vy` by
     `JUMP_CUT` (~0.45).
6. **Gravity / wall behaviour:**
   - If **Wall-Cling** and against a wall and pressing into it and holding *up*:
     climb (`vy = -WALLCLIMB`).
   - Else `vy += GRAVITY`, clamp to `MAX_FALL`. If wall-clinging while falling, clamp
     `vy` to `WALL_SLIDE`.
7. **Integrate + collide** (§5): move X then Y against the tilemap; set
   `onGround/atCeiling` and side-wall probes.
8. **Hazards:** if the body overlaps a **Hazard** tile → `TakeDamage(1)` + knockback
   up-and-away.
9. **Energy + weapons** (§8): regen; handle weapon switch; fire the current weapon.
10. **Post:** if landed this frame → `JustLanded`; if on ground → refresh
    `coyote`, clear double-jump. Update `facing` from input.

### 4.2 Abilities (unlocked by pickups)

| Ability | Effect |
|---|---|
| **Double Jump** | One extra upward impulse while airborne. |
| **Dash** (Phase Dash) | Short high-speed horizontal blink; i-frames; damages enemies dashed through; ~8-frame duration, ~22-frame cooldown. |
| **Wall-Cling** | Wall slide + wall jump + climb (hold *up* into a wall). |
| **Phase** (Matter-Phasing) | Hold to pass through **Phase** tiles; drains energy. |

Feel targets (tune to these): a normal jump rises ~**4 tiles**; run crosses a
20-tile screen in ~**1.5 s**; dash covers ~**5 tiles**; double jump reaches ~**6–7
tiles** of total rise. See Appendices A/B for numbers.

### 4.3 One-shot events (for SFX/particles)

`JustJumped, JustDoubleJumped, JustWallJumped, JustDashed, JustLanded, JustHurt,
JustDied, JustFired (+weapon), JustBladed`.

---

## 5. Tiles & collision

**Tile types** (store one byte per tile; see Appendix C for the enum):

| Tile | Solid? | Notes |
|---|---|---|
| Empty | no | air |
| Solid | yes | wall/floor |
| Phase | yes, unless phasing | ghostly; pass with Phase held |
| Hazard | no | damages on contact (spikes/energy) |
| OneWay | from above only | jump-through platform; drop through with *down* |
| Cracked | yes | shattered by **Scatter-Shot** → becomes Empty |
| DoorRed | yes | ability/energy seal; opened by having the ability |
| DoorBlast | yes | heavy door; opened by a puzzle flag |

`IsSolid(tx,ty)`: out-of-bounds = solid (keeps Spark in the room). `Solid`,
`Cracked`, `DoorRed`, `DoorBlast` are solid; `Phase` is solid unless phasing;
`OneWay` handled specially by the Y resolver.

**Collision (axis-separated, single step):**
- Move X by `vx`; if the leading vertical edge enters a solid column, snap the body
  flush to the tile boundary and zero `vx`; set the corresponding wall flag.
- Move Y by `vy`; if descending into a solid (or resting on the top of a **OneWay**
  tile you were above last frame, and not holding *down*), snap on top, set
  `onGround`, zero `vy`. If ascending into a solid, snap below, set `atCeiling`.
- **Wall probes:** 1-px-wide boxes just left/right of the body decide
  `onWallLeft/Right` for wall-cling.

Because max speed < tile size (§3), one snap per axis is enough — no sweep loop.

---

## 6. World structure

The world is a **grid of single-screen rooms**. A room lives at integer cell
`(gx,gy)`; a door on an edge leads to the neighbour one cell in that direction, so
**no target IDs are stored** — the neighbour is computed.

### 6.1 Fixed door bands (auto-align between neighbours)

Because all screens are the same size, put doors at **standard positions** so a
room's East door always lines up with its East-neighbour's West door.

- Playfield 20×20 tiles. `FLOOR_ROW = 17` (floor at rows 17–19 solid).
- **E/W doors:** a 3-tile-tall opening at rows `14..16` on column `0` (West) or `19`
  (East); you walk in at floor level.
- **N/S doors:** a 3-tile-wide opening at cols `9..11` on row `0` (North) or `19`
  (South).
- **Entry placement** when arriving through an edge: West→near left floor;
  East→near right floor; North→fell in near the top; South→came up beside the
  bottom gap.
- **Trigger zones:** a thin box just inside each edge opening; overlapping it fires
  the transition (with a ~0.3 s lock so you don't bounce straight back).

### 6.2 Guaranteed-traversable interiors

Build each room's tilemap from its descriptor so **every present door is reachable
from the floor**, for any door combination:
- Solid border + a solid floor at `FLOOR_ROW`.
- Carve each present door opening.
- **North door → a zig-zag platform ladder** from floor to the top opening (each
  step ≤3 up / ≤4 across — base-jumpable).
- **South door → a gap in the floor** to drop/rise through.
- **E/W doors → floor-level** (already reachable).
- Optional decoration (a couple of one-way ledges, an occasional hazard) placed
  **away from** the spawn column and door approaches so it never blocks a route.

### 6.3 Biomes

Three zones, distance-banded from the start so you cross them in order:

| Biome | Palette feel | Tile shape motif | Enemy mix |
|---|---|---|---|
| **The Rust Vents** (start) | warm orange/red, embers | riveted plates | Crawler, Charger, Armored Crawler |
| **The Crystal Conduits** (mid) | teal/cyan, glassy | faceted diagonals | Floater, Hover-Turret, Stalker-Drone |
| **The Mainframe** (deep / Core) | cold blue, green datastreams | circuit grid + nodes | Sentinel, Hover-Turret, Stalker-Drone, Armored Crawler |

### 6.4 Door kinds & gating

| Door kind | Opens when |
|---|---|
| **Open** | always (same-biome interior link) |
| **AbilityGate** (renders as **DoorRed** seal) | you hold the required ability |
| **Blast** (renders as **DoorBlast**) | its puzzle flag is set |

- **Biome boundaries** are AbilityGates: Rust→Crystal needs **Double Jump**,
  Crystal→Mainframe needs **Dash**, the **Core** seal needs **Phase**.
- The required ability is always obtainable in the *preceding* region → the world is
  completable. (The original proves this with a solver; you pre-bake a proven map —
  §14.)

### 6.5 Rendering the locked-door barrier

When a door is locked, fill its opening tiles with the barrier tile
(**DoorRed**/**DoorBlast**) so the player sees a real door, not a blank wall. When
it unlocks (ability gained / flag set), clear those tiles to Empty each frame.

---

## 7. Global state, locked doors & puzzles

**Flags.** A global bitfield (e.g., **64 bytes = 512 flags**) tracks opened doors,
thrown switches and solved puzzles. Persisted in the save.

Puzzles are **self-contained and optional**: each gates a **dead-end** side room via
a **Blast** door, with the mechanism in the *accessible* room, so they never block
the main path.

| Mechanism | Behaviour |
|---|---|
| **Shootable switch** | a hit from any player projectile sets its flag (lights up). |
| **Melee switch** | only the **Plasma Blade** hitbox trips it. |
| **Pressure plate + heavy block** | push a procedurally-placed block onto the plate; flag stays set while the block rests on it. Block = a small dynamic solid with gravity that Spark shoves along the floor. |
| **Sequence** | 3 shootable switches must be hit **in the right order within ~6 s**; wrong order or timeout resets. Solving sets the flag. |

A Blast door's opening clears when its flag is set (§6.5).

---

## 8. Arsenal (weapons & projectiles)

Spark carries a **weapon inventory**; **cycle** with a key. Weapons are unlocked by
pickups (Blaster is default).

| Weapon | Behaviour | Cost / cooldown (frames) |
|---|---|---|
| **Blaster** | one fast projectile, low damage | ~4 energy / ~10 |
| **Scatter-Shot** | a **spread of ~6 pellets**, short life; each **shatters Cracked walls** | ~22 energy / ~25 |
| **Plasma Blade** | a **melee arc hitbox** in front for ~8 frames: high damage, **deflects enemy projectiles** (flips them to hit enemies), **trips melee switches** | ~12 energy / ~17 |

**Projectiles** (pool of ~8–12): `x,y` (8.8), `vx,vy` (8.8), `life`, `damage`,
`fromPlayer` flag, `breaksWalls` flag. Each frame: move; on hitting a solid tile
despawn (if `breaksWalls` and the tile is **Cracked**, clear it first). Player
projectiles damage enemies; enemy projectiles damage Spark (unless invulnerable).

**Dash** also damages enemies it passes through (Spark is invulnerable mid-dash).

---

## 9. Enemies (eight kinds) & combat

Keep AI **integer and table-driven**. Common fields: `kind`, `x,y,vx,vy` (8.8),
`hp`, `facing`, `homeX,homeY`, `range`, `hitInvuln`, plus per-kind timers. A room
holds a small active list (≤ ~6).

| Kind | HP | Behaviour (integer) |
|---|---|---|
| **Crawler** | 2 | Ground patrol; turn at a wall or a ledge (no floor ahead). |
| **Floater** | 2 | Drifts on a **sine LUT** around its home (x and y bob); harmless-ish but contact hurts. |
| **Sentinel** | 6 | Hovers, tracks the player's X within a range, **fires** toward the player every ~1.7 s. |
| **Charger** | 3 | Patrol → on player in line & same level, **wind-up** (telegraph, ~0.45 s) → **charge** fast (~0.8 s) → **recover**; won't charge off a ledge. |
| **Hover-Turret** | 3 | Stationary; when the player is in range **with line of sight**, fires with **predictive lead** (aim ≈ player + player-velocity × dist/projSpeed; on Z80, approximate with sign/threshold or an 8-direction table). |
| **Stalker-Drone** | 2 | Fast flyer that **chases** the player, routing around walls. *(Original uses BFS pathfinding; on Z80 use greedy chase: try direct step; if blocked, try the other axis, or a small per-room flow-field.)* |
| **Armored Crawler** | 4 | Like Crawler but **immune to frontal hits** — attacks landing on its facing side are deflected; you must hit it **from behind** (jump over it). |
| **Warden (BOSS)** | ~22 | Big hovering guardian in the Core room. Tracks the player's X; fires **fans** of projectiles on a timer; **enrages below half HP** (faster, wider fans). The Core seal only accepts Spark once the Warden is dead. |

**Line of sight:** step along the segment player↔enemy in ~half-tile increments; if
any tile is solid, no LOS. **Sine/aim:** precompute a 256-entry signed sine table
and, for fans, a small table of `(dx,dy)` offsets per fan angle.

**Frontal-armour test:** the attacker (projectile position, or Spark for blade/dash)
is "in front" if `sign(attackerX − enemyX) == enemy.facing`; if so, an Armored
Crawler ignores the hit.

**Combat resolution order each frame:** spawn the player's shot → move projectiles →
run enemy AI (may spawn enemy shots) → blade arc (damage + deflect) → player
projectiles vs enemies (respect armour) → dash vs enemies → enemy projectiles vs
Spark → enemy bodies vs Spark → cull the dead.

**Enemy hit-invuln:** ~0.14 s between damage instances so one blade swing = one hit.

---

## 10. Rendering on the CPC

**Use Mode 0** (160×200, 16 inks). Screen base `&C000`.

### 10.1 Screen memory layout (interleaved!)

For pixel row `Y` (0..199) and byte column `XB` (0..79):

```
addr = &C000 + &800 * (Y AND 7) + &50 * (Y >> 3) + XB
```

(`&50 = 80` bytes/line.) Precompute a **200-entry table of line base addresses** to
avoid the per-pixel math.

### 10.2 Mode 0 pixel packing (2 pixels/byte, 16 pens)

Each byte holds a **left** and **right** pixel, each a 4-bit pen. The 4 pen bits are
**spread** across the byte. The standard mapping (left pixel P0, right pixel P1;
pen bits `d0..d3`):

```
byte = (P0d0<<7)|(P1d0<<6)|(P0d1<<3)|(P1d1<<2)|(P0d2<<5)|(P1d2<<4)|(P0d3<<1)|(P1d3<<0)
```

> Verify this on an emulator (WinAPE/CPCEC) before trusting it — the bit order is a
> classic source of bugs. **Build lookup tables**: a 16-entry `penToLeft[]` and
> `penToRight[]` (each pen → its byte contribution in that pixel position), then a
> tile pixel pair = `penToLeft[a] OR penToRight[b]`.

### 10.3 Tiles

- An **8×8 tile in Mode 0 is 4 bytes wide × 8 rows = 32 bytes** (pre-packed).
- Store per biome a small set (say **16–48 tiles**): solid variants, floor, one-way
  ledge, hazard, cracked, the two door barriers, plus a few decorative tiles.
- **Room draw:** blit the 20×20 tile buffer to the playfield. On a **screen flip**,
  redraw the whole playfield (fast enough — 400 tiles × 32 bytes ≈ 12.8 KB copy).
  During play, redraw only what changed or use **double buffering** (two 16 KB
  screens, flip the CRTC display start `R12/R13`) if RAM/timing allow.

### 10.4 Sprites (Spark, enemies, projectiles, blade)

- Small (≈1 tile). Use **masked sprites** (AND mask then OR data) or pre-shifted
  copies if you want sub-byte X positions (Mode 0 byte = 2 px, so X has a
  half-byte phase → keep two pre-shifted versions, or restrict sprites to even X for
  simplicity in the MVP).
- **Save/restore background** under each sprite (store the bytes you overwrite, put
  them back next frame) so you don't full-redraw the room every frame.
- Draw order: tiles → pickups → enemies → projectiles → Spark → blade arc → HUD.

### 10.5 Palette (16 inks from 27 colours), per biome

Set inks via firmware `SCR SET INK` or direct Gate-Array writes. Suggested ink sets
(firmware colour numbers 0–26):

- **Rust Vents:** 0 black, 3 red, 6 bright-red, 15 orange, 24 bright-yellow, 13 grey,
  5 mauve, 26 white (+ mid tones).
- **Crystal Conduits:** 0 black, 1 blue, 10 cyan, 20 bright-cyan, 19 sea-green,
  23 pastel-cyan, 13 grey, 26 white.
- **Mainframe:** 0 black, 1 blue, 2 bright-blue, 11 sky-blue, 18 bright-green,
  21 lime, 13 grey, 26 white.

Reserve fixed ink slots for Spark (bright cyan/white), hazards (bright red/magenta),
pickups (yellow), so sprites read against every biome. A **1–2 frame palette
flash** is the cheap "juice" for hits/transitions/unlocks.

### 10.6 HUD

Bottom strip (rows 20–24). Show: **health pips**, **energy bar**, **current weapon**
name, a tiny **grid minimap** (visited cells as blocks, current highlighted, Core
flagged), and an **objective hint** — a small arrow toward the nearest uncollected
ability plus its name ("→ DOUBLE JUMP"). When Spark stands at a locked door, show a
one-line hint: `SEALED — NEEDS DOUBLE JUMP` (ability) or `SEALED — FIND THE SWITCH`
(blast). These hints are essential on a big map.

---

## 11. Audio (AY-3-8912)

The AY has registers: R0/R1 = tone A period (12-bit), R2/R3 = B, R4/R5 = C,
R6 = noise period, R7 = mixer (tone/noise enables per channel), R8/R9/R10 =
amplitudes (bit4 = use envelope), R11/R12 = envelope period, R13 = envelope shape.
Drive it directly (via the PPI) for tight SFX, or use the firmware `SOUND`.

Implement a tiny **SFX engine**: each effect is a short **frame-stepped script**
(per frame: set channel period/amplitude/noise). Reserve 1 channel for SFX (or share
3). Effects to provide (map from the original's palette):

| SFX | Recipe |
|---|---|
| Jump | rising square sweep, ~7 frames |
| Double Jump | higher rising sweep |
| Dash | noise burst + falling saw-ish sweep |
| Land | short low thud (low tone, fast decay) |
| Pulse/Blaster | short square blip, falling |
| Scatter | noise burst + a couple of blips |
| Blade | quick metallic sweep (saw, high→mid) |
| Enemy hit | short noise tick |
| Enemy dead | falling tone + noise |
| Pickup | two-note up arpeggio |
| Unlock | 3–4 note ascending arpeggio |
| Hurt | falling square |
| Death | long falling tone + noise |
| Transition | soft short sweep |
| Victory | 5-note ascending fanfare |

Optionally a low-CPU ambient drone on one channel.

---

## 12. Input

Default keys (also expose joystick): **A/D** or **O/Q** or arrows = move; **Space** =
jump; **Shift** = dash; **Return/M** = fire weapon; **N** = switch weapon; **C** =
phase (hold); **P/Esc** = pause; **Tab** = toggle map; **R** = restart/resume.

Read via firmware key routines or scan the keyboard matrix through the PSG port each
frame. Joystick: fire = jump or attack (pick a mapping), etc. Debounce edge actions
(jump/dash/fire/switch/pause) — track "pressed this frame" vs "held".

---

## 13. Memory map, banking & data layout

A workable 64 KB base layout (lower ROM off for RAM under `&0000`):
- `&0000–&3FFF` code + tables (or keep lower ROM if you use firmware).
- `&4000–&7FFF` **paged data window** — swap in room/graphics/audio banks here via
  the RAM-config register.
- `&8000–&BFFF` engine RAM: tile buffer (400 B), line-address table (400 B), active
  entity arrays, sprite backup buffers, flags bitfield, sound scripts, stacks.
- `&C000–&FFFF` screen (16 KB). (Two screens if double-buffering — one in a bank.)

Use the extra **64 KB** for: the pre-baked **world descriptor table**, per-biome
**tile/sprite graphics**, **SFX scripts**, and optional off-screen buffers. Page a
bank into `&4000` when you need it. Bank-select via `OUT (&7Fxx),val` with
`val ∈ &C0..&FF` (standard CPC RAM configurations; confirm the exact codes for the
64 KB banks).

**Save game** (AMSDOS file, a few dozen bytes): current cell `(gx,gy)`, abilities
bitmask, weapons bitmask, deaths, and the **flags bitfield**. Offer *Continue* on the
title/death screens. (A password system is an acceptable no-disk fallback.)

---

## 14. Pre-baking the world (recommended)

Implementing the full spanning-tree generator **and** the solvability solver on the
Z80 is heavy. Instead, **pre-generate on a PC** and export a data table the CPC just
reads. The original guarantees a completable map (its `MapGenerator` retries until
`WorldSolver` proves the Core reachable), so bake a proven seed.

**Export, per room (cell):** `gx, gy, biome, doorMask(NESW, 4 bits), perEdge
doorKind+lockAbility/flag, pickup(type or none), weaponPickup(type or none),
enemyList(kind+tile ×N), puzzleType+params, isCore`. A room is ~a dozen bytes; 60
rooms ≈ under 1 KB. The **tilemap is not stored** — it is rebuilt on entry from the
descriptor by the on-CPU `BuildInterior` routine (§6.2), exactly as the original
does. This keeps world data tiny and fits many rooms easily.

*(If you insist on runtime generation: use a Z80 LCG for the growth walk, keep the
grid ≤ 64 cells, hard-code the gate order, and gate puzzles to dead-ends only — then
you can skip the solver because dead-end-only puzzles + region-placed abilities are
solvable by construction.)*

---

## 15. Suggested module breakdown & milestone build order

Mirror the original's clean split (logic testable without the renderer):

```
core/    fixedpoint, rng (LCG), input, timing (VSYNC), sinetable
world/   tiletypes, tilemap buffer, doorways (bands), roominterior builder,
         worldgrid (transitions), flags, puzzles
ent/     player, enemy (8 kinds), projectile, pushblock, combat, weapons
gfx/     mode0 setup, line-addr table, tile blit, sprite blit+restore, palette,
         hud, minimap
audio/   ay driver, sfx scripts
data/    baked world table, tile graphics, sprite graphics
main/    state machine, room load, save/load
```

**Build order (test each before moving on):**
1. **Boot + Mode 0** + palette + a static tilemap on screen. Line-address table.
2. **Player controller** (run/jump/gravity/coyote/buffer) with tile collision on one
   hand-made room. Tune the feel against §4.2 / Appendix B.
3. **Abilities** (double jump, dash, wall-cling, phase) + hazards + health/energy.
4. **One-screen doors → 3–4 room grid**; N/S/E/W transitions + entry placement.
5. **RoomInterior builder** + the **baked world table**; roam the full map (all
   Open doors) — verify you can reach every room.
6. **Biomes** (per-biome tile sets + palettes).
7. **Ability gates + locked-door barriers**; the objective compass + hints.
8. **Weapons** (Blaster→Scatter→Blade), projectiles, cracked walls.
9. **Enemies** (start with Crawler/Floater/Sentinel; then Charger/Turret/Drone/
   Armored) + combat.
10. **Puzzles** (switch → plate+block → sequence) + flags.
11. **Warden boss** + Core victory.
12. **AY SFX**, HUD polish, **save/continue**, title/death/victory screens.

---

## 16. Minimum-viable vs stretch

**MVP that still feels like Aetheria:** Mode 0; ~15–25 hand-picked screens across the
3 biomes; run/jump/double-jump/dash + phase; Blaster + one more weapon; 3–4 enemy
kinds; ability-gated boundaries; a couple of switch puzzles; the Warden; save via
disk; core SFX. Screen-flip transitions, even-X sprites.

**Stretch:** all 8 enemy kinds; all 3 weapons; plate/sequence puzzles; 40–60 screens
(page world/graphics from disk or upper banks); double-buffered rendering; wipe/slide
transitions; drone flow-field pathing; ambient AY drone; minimap + objective compass.

---

## 17. Appendix A — original tuning constants (authority for *feel*)

Original units: pixels & seconds, **tile = 16 px**, updated with delta-time.

```
Gravity            1600 px/s^2      MaxFall        720 px/s
Run (max)           190 px/s        GroundAccel   2100   GroundFriction 2600
                                    AirAccel      1500   AirFriction     700
Jump                470 px/s        DoubleJump     430   JumpCut        0.45
CoyoteTime         0.10 s           JumpBuffer    0.12 s
WallJumpX           250             WallJumpY      430   WallJumpLock   0.14 s
WallSlide            90             WallClimb       92
Dash                500 px/s        DashTime      0.18 s DashCooldown   0.45 s
MaxHealth             5             MaxEnergy      100   EnergyRegen     22/s
InvulnTime         1.0 s            Knockback      240
Blaster: cost 7,  cd 0.20 s, dmg 2, speed 390
Scatter: cost 22, cd 0.50 s, dmg 1, speed 320, pellets 6, spread 34°, life 0.26 s
Blade:   cost 12, cd 0.34 s, time 0.16 s, dmg 4
```

---

## 18. Appendix B — CPC per-frame constants (8.8 fixed point)

**Derivation:** CPC pixel = ½ original pixel (8 px tile vs 16); step = 1/50 s.
Velocity `px/frame = (orig px/s) / 100`; acceleration added per frame
`= (orig px/s²) / 5000`. Value below = `round(cpc_px * 256)` for 8.8. **Starting
points — tune by feel.** All are < 8 px/frame (no tunnelling).

```
                     cpc px/frame    8.8 value (dec / hex)
Gravity (add to vy)      0.32          82   / $0052
MaxFall                  7.20        1843   / $0733
Run (max vx)             1.90         486   / $01E6
GroundAccel (to vx)      0.42         107   / $006B
GroundFriction           0.52         133   / $0085
AirAccel                 0.30          77   / $004D
AirFriction              0.14          36   / $0024
Jump (set vy)           -4.70       -1203   / (neg $04B3)
DoubleJump              -4.30       -1101   / (neg $044D)
WallJumpY               -4.30       -1101
WallJumpX (set vx)       2.50         640   / $0280
WallSlide (clamp vy)     0.90         230   / $00E6
WallClimb (set vy)      -0.92        -235
Dash (set vx)            5.00        1280   / $0500
```

**Timers (frames @ 50 Hz):** Coyote 5, JumpBuffer 6, WallJumpLock 7, DashTime 9,
DashCooldown 22, Invuln 50, EnergyRegen +2/frame (8.8) when not phasing,
BlasterCd 10, ScatterCd 25, BladeCd 17, BladeTime 8, EnemyHitInvuln 7.

*(If you keep 16 px tiles / a bigger scale, double the distances accordingly.)*

---

## 19. Appendix C — enums & baked data record formats

```
TileType:   0 Empty  1 Solid  2 Phase  3 Hazard  4 OneWay
            5 Cracked  6 DoorRed  7 DoorBlast
Biome:      0 RustVents  1 CrystalConduits  2 Mainframe
Direction:  0 North  1 East  2 South  3 West   (North = up = gy-1)
DoorKind:   0 Open  1 AbilityGate  2 Blast
Ability:    0 DoubleJump  1 Dash  2 WallCling  3 Phase   (bitmask in 1 byte)
Weapon:     0 Blaster  1 Scatter  2 Blade                (bitmask in 1 byte)
EnemyKind:  0 Crawler 1 Floater 2 Sentinel 3 Charger 4 Warden
            5 HoverTurret 6 StalkerDrone 7 ArmoredCrawler
SwitchKind: 0 Shootable  1 Melee
PuzzleType: 0 none 1 shootSwitch 2 plate+block 3 sequence
```

Suggested per-room record (variable length, terminated lists):
```
db gx, gy
db biome
db doorMask                    ; bit0 N, bit1 E, bit2 S, bit3 W
; for each set door bit, one edge byte: (kind<<4) | reqAbilityOrFlagIndex
db pickup                      ; 0=none else (Ability|0x10) or (Weapon|0x20)
db enemyCount
   ; enemyCount × { db kind, tx, ty }
db puzzleType, puzzleParam     ; flag index etc.
db coreFlag                    ; 1 if Core room (spawns Warden)
```
Header: `roomCount`, then an index `(gx,gy)->offset` (or just scan). Keep the whole
table < 2 KB for 60 rooms.

---

## 20. Appendix D — key Z80 routines (sketches)

**Screen line-address table** (build once): for `Y=0..199`,
`lineAddr[Y] = &C000 + &800*(Y&7) + &50*(Y>>3)`. Then a pixel byte address =
`lineAddr[Y] + XB`.

**Mode-0 pen→byte LUTs** (build once from §10.2):
```
; penToLeft[pen]  = byte with only the LEFT pixel set to 'pen'
; penToRight[pen] = byte with only the RIGHT pixel set to 'pen'
; tile pixel pair(a,b) = penToLeft[a] OR penToRight[b]
```

**Blit an 8×8 tile** at tile column `TC` (0..19), tile row `TR` (0..19):
```
; XB = TC*4  (4 bytes per 8-px tile in Mode 0)
; for row = 0..7:
;   Y = TR*8 + row
;   HL = lineAddr[Y] + XB
;   copy 4 pre-packed bytes from the tile bitmap to (HL)
```
Use `LDI`×4 or unrolled `LD (HL),A`. 400 tiles ≈ fast enough for a room flip.

**Player X move + collide** (integer sketch, per axis):
```
; add vx (8.8) to x (8.8)
; nx = new integer x (high byte, signed)
; leading edge tile column = (nx + (vx>0 ? width : 0)) >> 3
; for each body row tile (top,bottom): if IsSolid(col,row) ->
;     snap x flush to (col*8 - width) or ((col+1)*8), vx = 0, set wall flag; done
```
Y move is analogous, plus the OneWay "was above last frame & not holding down" case
sets `onGround`.

**Room transition:**
```
; on player box ∩ door.trigger and NOT door.locked:
;   nextCell = cell + Delta(edge)
;   load descriptor(nextCell) -> BuildInterior -> tileBuffer
;   spawn enemies/pickups/puzzles; refresh locked-door barriers
;   place player at EntryPosition(opposite edge); set transitionLock
;   flip screen (redraw playfield) + palette flash
```

---

### Faithfulness checklist (what "done" looks like)

- [ ] Movement feels tight: coyote + jump-buffer + variable height; ~4-tile jump.
- [ ] All four abilities work and gate progression; gates are visible seals with hints.
- [ ] ≥ 3 biomes with distinct palettes/tiles; screen-to-screen exploration in 4 directions.
- [ ] ≥ 3 weapons incl. Scatter breaking cracked walls and Blade deflecting shots.
- [ ] ≥ 4 enemy behaviours incl. the frontal-immune Armored Crawler and the Warden boss.
- [ ] At least switch + one other puzzle type, driven by the global flags.
- [ ] The baked world is completable start → Core; objective compass helps navigation.
- [ ] Save/continue; title/death/victory; AY SFX for the key actions.

*Build it in the milestone order, test each layer, and keep the world provably
completable. Good luck, and mind the Mode-0 pixel bit order.*
```
