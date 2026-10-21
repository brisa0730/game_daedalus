# Entities & Data
**Last updated:** v0.03

Values here mirror the single source of truth in the sim assembly once it exists (CLAUDE.md § Coding Conventions → Tunable defaults). Until then the numbers below are a starting sketch, not tuned defaults. Milestone 1 tunes them with the operator.

**Units:** 1 Unity unit = 1 tile; positions and distances in tiles; speeds in tiles per second; times in seconds.

## Player tunables

**Exposed to students as sliders:** run speed, acceleration, jump height, double-jump count, gravity, max fall speed, wall slide speed, climb speed, max HP, attack damage, attack range, attack cooldown.
**Internal (good defaults, not exposed):** coyote time, jump buffer, hit-stop, wall-jump input lock, i-frame duration.

| Setting | Field (sketch) | Sketch value |
|---|---|---|
| Run speed | `runSpeed` | 6 |
| Jump height | `jumpHeight` | 3.5 |
| Gravity | `gravity` | 1 |
| Max fall speed | `maxFallSpeed` | 12 |
| Max HP | `maxHp` | 5 |
| Air jumps (double-jump count) | `airJumps` | 1 |
| Wall slide speed | `wallSlideSpeed` | 2 |
| Climb speed | `climbSpeed` | 3 |
| Attack damage | `attack.damage` | 1 |
| Attack range | `attack.range` | 6 |
| Attack cooldown | `attack.cooldown` | 0.25 |
| Starting abilities | `startingAbilities` | `["jump"]` |
| Hit-stop | (internal) | ~0.05 s |
| I-frames | (internal) | ~1 s |

Acceleration (ground and air), coyote time, jump buffer and the wall-jump lock have no sketch value yet; Milestone 1 sets them.

## Abilities

`run`, `jump`, `doubleJump`, `wallJump` (wall slide + wall jump), `wallClimb`, `ropeClimb`, `attack`. Pickups can unlock: double jump, wall jump, wall climb, rope climb, attack. Exact identifiers are set by the sim.

## Enemy settings

| Setting | Values |
| --- | --- |
| Movement | Walk, Jump, Fly, Stationary |
| Damage | Contact only, or Contact + projectile |
| HP | Number of hits to kill |
| Speed | Slider |
| Aggro range | Distance at which it starts chasing |
| Idle behavior | Stand still, or patrol |
| Ledge behavior (walkers) | Turn at ledges, or walk off |
| Wall behavior (flyers) | Pass through walls, or blocked by walls |
| Projectile | Fire rate, projectile speed, destructible by the player's attack (on/off) |
| Contact damage | Damage to the player on touch |
| Jump strength (jumpers) | Fixed, from a setting |

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
| Enemy | Instance of an enemy preset, with optional overrides |
| Exit / goal | Ends the game with a win screen |

Each placeable element is a prefab in the starter; its settings are fields on its component.

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
| Player settings | ScriptableObject asset | The player tunables above, the starting abilities |
| Enemy preset | ScriptableObject asset | The enemy settings above, a name, an animation set |
| Terrain | Tile asset | Layout, kind, slots (§ Terrains) |
| Animation set | ScriptableObject asset | Sprite frames and frame rate per animation state |
| Map | Scene: a Grid with Background, Collision and Foreground Tilemaps | Terrains and plain tiles per cell |
| Placed elements | Scene: prefab instances | Player start; ability pickup (ability); enemy (preset + overrides); rope (length); moving platform (end point, speed); crumbling platform; checkpoint; health pickup; exit |

Open in the sketch: whether `gravity` is a multiplier or an absolute value; whether a project has one player settings asset or settings on the Player start.
