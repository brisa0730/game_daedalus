# Systems
**Last updated:** v0.07

Per-system contracts. Values and defaults live in [entities.md](entities.md). All Draft.

## Player

One player character with every ability built in. Per project, each ability is either **on from the start** or **unlocked by a pickup**. Movement feel is the top quality priority: it must feel good with default settings.

| Ability | Behavior | Notes |
| --- | --- | --- |
| Run | Accelerate / decelerate on ground and in air | Air control is a fixed fraction of ground (internal) |
| Jump | Variable height (release early = shorter jump) | Coyote time and jump buffering required |
| Double jump | Extra jump(s) in the air; resets on landing, wall or rope | Count is a setting (default 1) |
| Wall slide + wall jump | Slow slide when pressing into a wall; jump pushes away | Brief input lock after a wall jump so the player doesn't re-stick |
| Wall climb | Cling and move up/down a wall | Only on **climbable** tiles (tile kind `climbable`); other walls allow wall slide + wall jump only |
| Rope / vine climb | Grab a vertical rope, climb up/down, jump off | Ladder-style, no swinging |
| Attack | One attack: auto-targeting; see Combat & Damage | No melee slash in v1 |

### Movement rules (sim, v0.07)

Built in phase 1 (`movement-sim`) in `PlayerStepper`; values in [entities.md](entities.md) § Player tunables. Settled with the operator on 2026-10-23 unless marked **(proposal)**; proposals are confirmed or changed at Phase 2's play-check.

- **Contact states:** air, ground (≥ 1 solid or climbable cell underfoot), one-way top (one-way cells only), wall slide, wall cling, rope.
- **Tick order:** tick-start contacts → jump press → input transitions (rope grab, leaving a slide or cling) → velocity by contact → move (X swept first at the old height, then Y; trapezoid integration, impulses before gravity) → post-move classification (land, walk off, slide/cling entry, climb and rope limits, head bump, fall-out) → facing → counters.
- **Jump-press priority** (one rule, first match wins): drop-through > ground/coyote jump > rope jump > wall jump (touching or within 0.125) > air jump > buffer.
- **Coyote** only after walking off a ledge; a coyote jump spends no air jump and clears the window. **Wall coyote:** yes. **Rope coyote:** no.
- **Buffer** fires only ground or wall jumps; a buffered press released before landing gives a minimum hop.
- **Variable height** by velocity cut; minimum hop 1.25 tiles; air-jump height = jump height.
- **Air jumps reset** only on landing, an active wall state (slide or cling, which need the ability) or a rope grab. A wall slide refills only on a **different wall face** than the one last jumped from (including a jump from wall coyote), so double jump + wall jump can't scale one wall; chimneys refill as usual (operator, 2026-10-23, option (b)).
- **Drop-through:** Down + a fresh press on a one-way top; works with jump off, consumes the press, no coyote. Straddling one-way and solid, solid wins (normal jump). A buffered jump landing on a one-way with Down held jumps.
- **One-way:** catches the feet only if they were at or above its top before the move; its sides are never walls; it is never a ceiling.
- **Walls:** slide only while falling and pressing in; the wall-jump input lock (0.2 s) holds the push speed. **No single-wall scaling on ordinary walls at defaults (deliberate, unlike HK/Celeste):** repeated wall jumps lose ~0.8–1.0 tile per jump (measured). Chimneys up to 5 tiles wide are climbable with wall jump; 6 wide is not (measured at defaults).
- **Wall climb** only on climbable tiles; climbable with wall climb off is an ordinary wall; ledge pop at an open top; wall climb without wall jump is allowed, and jumping off a cling always pushes away.
- **Ropes:** ladder-style; grab with Up or Down only and snap to the rope's centre; climb up/down; re-grab lockout 0.2 s; at the top, step onto a one-way platform there, else stop; off the bottom, let go; full-height rope jump that spends no air jump; overlapping ropes: the first in list order wins.
- **Collision:** X then Y sweeps (correct at any speed); falling diagonally onto an outside corner lands on top; a head bump zeroes upward speed; ceiling-corner correction slides a rising head at most 0.25 tile past a corner (never while on a rope).
- **Map bounds:** the sides and top are solid; falling out of the bottom resets to the start (temporary, until pits damage in combat-and-enemies).
- **Nearest free start:** a start inside solid moves to the nearest free cell-centred spot within 8 cells (ties: up, then left).
- **(proposal)** With jump off there are no ground or coyote jumps, but air jumps work after walking off a ledge.
- **(proposal)** Wall jump: full jump height at 1.5 × run speed push-off. Two walls in reach: the pressed side, then the nearer, then the left.
- **(proposal)** Cling entry: toward a climbable wall in the air while falling or holding Up, or Up + toward on the ground. Neutral holds; pressing away lets go (with wall coyote). A rising jump past a climbable face doesn't cling by itself. Climbing off the bottom of a climbable run lets go without wall coyote.
- **(proposal)** Timer convention: a window of N ticks is active on the N ticks after it is set (coyote, buffer, wall coyote 6; lock, lockout 12).
- **(proposal)** A rope can be grabbed from above with Down while standing on a one-way at its top. Standing, Up grabs a rope in reach; Down grabs only in that case. Ropes are grabbed only from the air or standing.
- **(proposal, from phase 1 code review)** Down + jump while on a rope lets go without a jump (the re-grab lockout applies), so "hold Down, then jump" on a rope's platform drops instead of rope-jumping. Climbing down onto a one-way just under a rope's bottom stands on it.
- **(proposal)** Rope jump horizontal speed = input × run speed. Climb speed is shared by walls and ropes.
- **(proposal)** Landing ends the wall-jump input lock.
- **(proposal)** Every numeric internal value ([entities.md](entities.md) § Player tunables).
- **Observed for the play-check (not a rule change):** against a wall with wall jump unlocked, a press at the apex is a wall jump (priority), not an air jump. A ledge that needs double jump is reached by air-jumping away from the wall, not while hugging it.

## Combat & Damage

- **Attack (auto-targeting):** picks the nearest enemy or destructible projectile within range and line of sight (raycast against solid tiles) and fires a fast homing bolt. No target in range: a short burst in the facing direction. No aiming. Damage, range and cooldown are settings.
- **On hit:** brief hit-stop (~50 ms), hit flash, enemy knockback.
- **Taking damage:** lose HP, knockback, i-frames (~1 s) with sprite flicker.
- **Hazards** (spike tiles, pits): damage, then return the player to the **last safe ground position** (not the checkpoint), unless HP hits 0.
- **HP 0:** respawn at the last checkpoint with full HP. Unlocked abilities are kept.

## Enemies

One **Enemy** component. Students make enemy types by choosing its settings ([entities.md](entities.md) § Enemy settings) and, for shooters, adding the **Shoots Projectiles** add-on; they save a finished enemy as a prefab ("Bat", "Slime"). Placed copies of the prefab can override values (Unity's prefab overrides).

**AI rules (no pathfinding):**
- **Idle:** stand still or patrol back and forth until the player enters aggro range.
- **Chase:** move horizontally toward the player. Return to idle when the player leaves aggro range by a margin (hysteresis, so it doesn't flicker).
- **Walk:** turn or walk off at ledges per setting; turn at walls.
- **Jump:** hop toward the player; jump when blocked by a wall or when the player is above. Fixed jump strength from a setting.
- **Fly:** move directly toward the player in 2D; through or blocked by walls per setting.
- **Projectile:** when in range and the cooldown is ready, fire a straight projectile aimed at the player's current position. Projectiles die on walls; the player's attack can destroy them if the setting allows.
- **Hurt:** knockback and a short stun when hit. **Death:** plays an animation, drops nothing in v1.

Getting stuck on terrain is acceptable and expected. Enemies update only inside the active radius.

## World & Level Elements

One large continuous map: no rooms, no screen transitions. Progression comes from **ability gating**: areas are reachable only once the player has the right pickup (e.g. a high ledge needs double jump).

- **Map:** a Unity Grid of 1-unit cells, with 16 px tile art. Up to ~500 × 200 tiles.
- **Layers:** three Tilemaps: Background, Collision, Foreground. **Only the Collision Tilemap affects gameplay**: a tile's kind counts only there; Background and Foreground are always decoration.
- **Tile kinds:** solid; one-way platform (jump through from below, drop down with down + jump); climbable wall (if wall climb is restricted to marked tiles); hazard; none/decorative. Painted through terrains and tiles (§ Tile System). In the sim (`TileKind`): None 0, Solid 1, OneWay 2, Climbable 3 (collides as solid); 4 is reserved for hazard (combat-and-enemies). The runtime fills a 0-based `TileGrid` from the Collision Tilemap at play start.
- **Ropes** reach the sim as a list of vertical segments (`RopeSegment`: centre x, bottom, top).
- **Placeable elements:** [entities.md](entities.md) § Placeable elements.

## Camera

Follows the player smoothly with a small look-ahead in the movement direction, clamped to the map bounds. Camera-bound zones are deferred (Roadmap §3).

## In-game Progress (the played game)

On touching a checkpoint, the played game saves position, unlocked abilities and collected pickups through `PlayerPrefs` (kept in the browser by a WebGL build), so a long map can be continued later.

## Editor Tools

Students work in the Unity Editor, and its usability with our tools decides whether they succeed. **Unity provides** navigation (Scene view pan and zoom), tile painting (Tile Palette: brush, box fill, eraser, picker; the active Tilemap picks the layer), creating and placing objects, Add Component, prefabs and prefab overrides, the Inspector, undo/redo and saving. **Our tools add only what Unity lacks.** **Play-from-here is the most important one.**

- **Play-from-here:** a Scene-view shortcut enters Play mode at the cursor position with a chosen set of abilities (checkboxes in the daedalus window). Leaving Play mode returns to the same Scene view. The runtime accepts a start position and an ability set from `movement-prototype` on, so the tool only has to pass them.
- **Play from start:** Unity's Play button, with only the starting abilities.
- **Daedalus components:** every kind of game object is one component in the Add Component menu under **Daedalus** (§ Placeable elements in [entities.md](entities.md)). Adding it makes the object work; it needs nothing else from Unity. A few add-ons extend one component (e.g. Shoots Projectiles on an Enemy). The starter's ready-made prefabs are examples to inspect and copy, not the main way to build.
- **Inspectors:** at most ~8 settings per component (the Player: per collapsible section), each a slider, toggle or dropdown with a tooltip; mode-specific settings show only in their mode; internals hidden.
- **Play mode:** no special treatment. Edits made during Play mode are thrown away when it ends, as everywhere in Unity; this is one of the things students learn. Unity's Play-mode tint (set in the starter's guidance) shows when edits won't stick.
- **Missing-piece hints:** a component that needs something (e.g. an Enemy with no sprite) says so in a help box in plain words.
- **Gizmos:** the Scene view shows ranges and paths (aggro range, attack range, moving-platform route).
- **Validation warnings** in the daedalus window and on entering Play mode: no player start or more than one, no exit, an ability pickup that's never placed, elements outside the map.
- **Scene autosave:** Unity does not autosave scenes; the tools save the open scene on entering Play mode and on a timer.
- No minimap: Scene-view zoom covers it.

## Tile System

Built on Unity's Tilemap and Tile Palette: students paint *terrain*, and auto-tiling picks the correct edge, corner and inner-corner tiles. This is the main way simple art ends up looking clean.

- **Tile Palette:** Unity's. Click a tile, or drag-select a group to use as a multi-tile brush.
- **Terrain brushes (auto-tiling):** each terrain (e.g. "Grass ground", "Stone") is a preconfigured tile asset (a Rule Tile from 2D Tilemap Extras for the layouts it covers). Painting or erasing updates the painted cell and its neighbors.
- **Random tiles:** a terrain slot can hold several variants, picked per cell by position, so the result is stable across reloads.
- **Animated tiles:** a tile with a frame list and speed (water, lava, torches).
- **Per-tile kind:** every tile or terrain asset carries a collision kind (solid, one-way, climbable wall, hazard, none). The kind takes effect **only on the Collision Tilemap**, so gameplay comes from what students paint there. **Spikes are hazard tiles** painted on the Collision Tilemap, not placed elements.
- **Layers:** Background, Collision and Foreground Tilemaps (§ World & Level Elements). Auto-tiling works on every layer.

### Auto-tile layouts

No rule editing for students: they paint with preconfigured terrains. Fixed layouts, each with a template image. **Which layouts v1 builds on Unity's Tilemap is open** (Roadmap §4): blob and single map directly onto Rule Tiles; Unity has no dual-grid tilemap, so dual-grid needs a custom tool.

| Layout | Tiles per terrain | Use |
| --- | --- | --- |
| Blob | 47 | Imported commercial tilesets; full edges, corners and inner corners |
| Dual-grid | 15–16 | Default for student-drawn terrain; clean corners with few tiles |
| Single tile | 1 | Simple blocks, decoration, flat-color styles |

- **Blob:** 8-neighbor bitmask; a diagonal bit counts only when both adjacent edge bits are set, which reduces 256 cases to 47.
- **Dual-grid:** display tiles are offset half a tile from the logical grid; each display tile looks at its 4 surrounding logical cells (16 cases). **Collision uses the logical grid**, not the display grid.
- Map edges count as "same terrain", so terrain touching the border shows no edge.
- In v1 each terrain auto-tiles only against itself; other terrains count as empty. Terrain-to-terrain transitions are out of scope (Roadmap §3).
- Each edit recomputes only the changed cell and its neighbors.

### Tileset setup

- Import a PNG and slice it in Unity's Sprite Editor (16 pixels per unit), then drag the sprites into the slots of a template terrain asset for the chosen layout. The template shows each slot's neighbor pattern visually.
- Downloadable blank template PNGs for each layout, so students can draw terrain directly in the right slots and import with no mapping step.
- The default art pack ships pre-mapped.

## Asset Import

- A **default art pack** (tiles, player, a few enemies, props) ships in the starter sample so students can build immediately.
- **Tileset import:** a PNG, sliced in the Sprite Editor, then mapped to an auto-tile layout (§ Tile System → Tileset setup).
- **Sprite-sheet import:** slice the sheet in the Sprite Editor, then assign frame ranges to animation states in an animation-set asset with a live preview; frame rate per state. Missing states fall back ([entities.md](entities.md) § Animation states).
- Imported assets live in the student's Unity project.

## Share

Students make a **WebGL build** from Unity's build window; a build contains only the game. Where builds are hosted for classmates is an open decision (Roadmap §4).
