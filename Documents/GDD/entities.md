# Entities & Data
**Last updated:** v0.07

Values here mirror the single source of truth in the sim assembly (CLAUDE.md § Coding Conventions → Tunable defaults): movement defaults and ranges in `MovementSettings`, movement internals in `MovementInternals` (both pinned by `MovementSettingsTests`; change the code, the test and this file together). Movement values are **starting values**: Phase 2's operator play-check tunes them. Combat values are still a sketch.

**Units:** 1 Unity unit = 1 tile; positions and distances in tiles; speeds in tiles per second; times in seconds.

## Player tunables

Student-facing settings, by Player section (field = `MovementSettings` identifier):

| Section | Setting | Field | Default | Range |
|---|---|---|---|---|
| Movement | Run speed (tiles/s) | `RunSpeed` | 6 | 1–12 |
| Movement | Time to full speed (s, from rest on the ground) | `TimeToFullSpeed` | 0.1 | 0–0.5 |
| Movement | Jump height (tiles) | `JumpHeight` | 3.5 | 1–6 |
| Movement | Gravity (multiplier over base gravity) | `Gravity` | 1 | 0.25–2 |
| Movement | Max fall speed (tiles/s) | `MaxFallSpeed` | 12 | 2–24 |
| Abilities | Toggles: double jump, wall jump, wall climb, rope climb | `Abilities` | jump only | — |
| Abilities | Air jumps (with double jump) | `AirJumps` | 1 | 1–3 |
| Abilities | Wall slide speed (tiles/s, with wall jump; never above max fall) | `WallSlideSpeed` | 2 | 0.5–6 |
| Abilities | Climb speed (tiles/s, walls and ropes) | `ClimbSpeed` | 3 | 1–8 |

- **Time to full speed** replaces "acceleration". Ground deceleration (× 1.5) and turnaround (× 2) are derived from it; air control is internal (× 0.75 of the ground rates).
- **Gravity** is a multiplier over base gravity 57.142857 tiles/s², which comes from a 0.35 s time to apex at jump height 3.5. Jump velocity is derived from jump height and rising gravity, so changing gravity changes timing only, never the jump height.
- Out-of-range or non-finite values are clamped (non-finite → default) when applied.

Combat (sketch, combat-and-enemies): max HP 5, attack damage 1, attack range 6, attack cooldown 0.25; internal hit-stop ~0.05 s, i-frames ~1 s. The Player-start question is settled by Phase 2 (`movement-runtime`).

**Movement internals** (hidden; `MovementInternals`; 60 Hz ticks):

| Internal | Value | Derived at defaults |
|---|---|---|
| Time to apex (sets base gravity) | 0.35 s | base gravity 57.142857 tiles/s², jump velocity 20 tiles/s |
| Fall gravity factor (while vy ≤ 0) | 1.75 | fall gravity 100 tiles/s² |
| Minimum hop (a jump released at once) | 1.25 tiles | cut velocity 11.952 tiles/s |
| Coyote time / jump buffer / wall coyote | 0.1 s each | 6 ticks each |
| Wall-jump input lock / rope re-grab lockout | 0.2 s each | 12 ticks each |
| Air control factor | 0.75 | air accel 45, decel 67.5, turnaround 90 tiles/s² |
| Deceleration / turnaround factor | 1.5 / 2 | ground accel 60, decel 90, turnaround 120 tiles/s² |
| Wall-jump push factor (× run speed) | 1.5 | push-off 9 tiles/s |
| Wall-jump reach | 0.125 tile | |
| Ceiling-corner correction (max) | 0.25 tile | |
| Hitbox | 0.75 × 1.5 tiles | position = feet centre; grip point = hitbox centre (0.75 above the feet) |
| Ledge-pop inset | 0.375 tile | popped centre 0.75 past the wall face |
| Rope grab reach | 0.375 tile | |
| Rope top tolerance | 0.0625 tile | |
| Drop-through nudge | 0.0625 tile | |
| Contact epsilon | 0.0001 tile | |
| Start search radius | 8 cells | |

## Abilities

Sim identifiers (`Abilities` flags): `Jump`, `DoubleJump`, `WallJump` (wall slide + wall jump), `WallClimb`, `RopeClimb`. **Run is always on and has no flag** (a toggle that gates nothing would do nothing). `Attack` arrives with combat-and-enemies. Pickups can unlock: double jump, wall jump, wall climb, rope climb, attack.

## Enemy settings

| Setting | Values |
| --- | --- |
| Movement | Walk, Jump, Fly, Stationary |
| HP | Number of hits to kill |
| Speed | Slider |
| Aggro range | Distance at which it starts chasing |
| Idle behavior | Stand still, or patrol |
| Ledge behavior (walkers) | Turn at ledges, or walk off |
| Wall behavior (flyers) | Pass through walls, or blocked by walls |
| Contact damage | Damage to the player on touch |
| Jump strength (jumpers) | Fixed, from a setting |

Settings marked for walkers, flyers or jumpers show only for that movement type, so an enemy shows at most ~8.

**Shoots Projectiles** (add-on on an Enemy): fire rate, projectile speed, destructible by the player's attack (on/off).

## Placeable elements

| Element | Behavior |
| --- | --- |
| Player start | Exactly one per map |
| Ability pickup | Unlocks one ability |
| Health pickup / max-HP upgrade | Restores HP or raises max HP |
| Rope / vine | Vertical climbable segment of any length |
| Moving platform | Moves between two points; carries the player |
| Crumbling platform | Falls shortly after the player stands on it, respawns |
| Checkpoint | Sets the respawn point when touched; saves in-game progress |
| Enemy | Movement type and the settings above; enemy types are saved as prefabs |
| Exit / goal | Ends the game with a win screen |

Each element is one Daedalus component that students add to an object (a GameObject with a sprite) from the Add Component menu; the component's Inspector shows its settings. The starter also ships each one as a ready-made prefab. The player itself is one **Player** component holding every ability as a toggle; its settings are grouped into collapsible sections (Movement, Abilities, Attack, Health) of at most ~8 each.

Spikes are not a placed element: they are **hazard tiles** painted on the Collision Tilemap (§ Terrains).

## Terrains

A terrain is an auto-tiling brush (systems.md § Tile System): a tile asset students paint with the Tile Palette.

- **Layout:** blob (47 slots), dual-grid (16 slots) or single (1 slot); which layouts v1 builds is open (Roadmap §4).
- **Kind:** the collision kind: solid, one-way, climbable, hazard, none. It takes effect only on the Collision Tilemap.
- **Slots:** neighbor pattern → sprites; more than one sprite means random variants, picked per cell by position.
- **Animated tiles:** a frame list + speed per tile (shape set when the tile system is built).
- Tilemaps store the terrain asset per cell; the displayed sprite is resolved from the neighbors.

## Animation states

`idle`, `run`, `jump`, `fall`, `wall-slide`, `climb-wall`, `climb-rope`, `attack`, `hurt`, `death`. A missing animation falls back to the closest state (e.g. no `fall` → `jump` → `idle`); the full fallback chain is set when sprite import is built.

## Authored data (sketch)

A starting shape, not a contract. What a student saves is a Unity scene plus our assets; each of our assets and components carries a data version so a package update can upgrade it (CLAUDE.md Architecture rule 4).

| Piece | Kind | Holds |
| --- | --- | --- |
| Player | Player component on a GameObject | The player tunables above, the starting abilities |
| Enemy type | Prefab with an Enemy component (+ add-ons) | The enemy settings above, an animation set |
| Terrain | Tile asset | Layout, kind, slots (§ Terrains) |
| Animation set | ScriptableObject asset | Sprite frames and frame rate per animation state |
| Map | Scene: a Grid with Background, Collision and Foreground Tilemaps | Terrains and plain tiles per cell |
| Placed elements | Scene: GameObjects with Daedalus components | Player start; ability pickup (ability); enemy; rope (length); moving platform (end point, speed); crumbling platform; checkpoint; health pickup; exit |

Open in the sketch: whether `gravity` is a multiplier or an absolute value; whether the Player component sits on the player object itself or on a Player start marker.
