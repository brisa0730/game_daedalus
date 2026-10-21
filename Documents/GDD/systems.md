# Systems
**Last updated:** v0.03

Per-system contracts. Values and defaults live in [entities.md](entities.md). All Draft.

## Player

One player character with every ability built in. Per project, each ability is either **on from the start** or **unlocked by a pickup**. Movement feel is the top quality priority: it must feel good with default settings.

| Ability | Behavior | Notes |
| --- | --- | --- |
| Run | Accelerate / decelerate on ground and in air | Separate ground and air acceleration |
| Jump | Variable height (release early = shorter jump) | Coyote time and jump buffering required |
| Double jump | Extra jump(s) in the air; resets on landing, wall or rope | Count is a setting (default 1) |
| Wall slide + wall jump | Slow slide when pressing into a wall; jump pushes away | Brief input lock after a wall jump so the player doesn't re-stick |
| Wall climb | Cling and move up/down a wall | Only on **climbable** tiles (tile kind `climbable`); other walls allow wall slide + wall jump only |
| Rope / vine climb | Grab a vertical rope, climb up/down, jump off | Ladder-style, no swinging |
| Attack | One attack: auto-targeting; see Combat & Damage | No melee slash in v1 |

## Combat & Damage

- **Attack (auto-targeting):** picks the nearest enemy or destructible projectile within range and line of sight (raycast against solid tiles) and fires a fast homing bolt. No target in range: a short burst in the facing direction. No aiming. Damage, range and cooldown are settings.
- **On hit:** brief hit-stop (~50 ms), hit flash, enemy knockback.
- **Taking damage:** lose HP, knockback, i-frames (~1 s) with sprite flicker.
- **Hazards** (spike tiles, pits): damage, then return the player to the **last safe ground position** (not the checkpoint), unless HP hits 0.
- **HP 0:** respawn at the last checkpoint with full HP. Unlocked abilities are kept.

## Enemies

One enemy object. Students make enemy types by combining settings ([entities.md](entities.md) § Enemy settings) and save them as named presets ("Bat", "Slime"). Placed enemies reference a preset, with optional per-instance overrides.

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
- **Tile kinds:** solid; one-way platform (jump through from below, drop down with down + jump); climbable wall (if wall climb is restricted to marked tiles); hazard; none/decorative. Painted through terrains and tiles (§ Tile System).
- **Placeable elements:** [entities.md](entities.md) § Placeable elements.

## Camera

Follows the player smoothly with a small look-ahead in the movement direction, clamped to the map bounds. Camera-bound zones are deferred (Roadmap §3).

## In-game Progress (the played game)

On touching a checkpoint, the played game saves position, unlocked abilities and collected pickups through `PlayerPrefs` (kept in the browser by a WebGL build), so a long map can be continued later.

## Editor Tools

Students work in the Unity Editor, and its usability with our tools decides whether they succeed. **Unity provides** navigation (Scene view pan and zoom), tile painting (Tile Palette: brush, box fill, eraser, picker; the active Tilemap picks the layer), placement (dragging the starter's prefabs into the scene; move, delete, multi-select), the Inspector, undo/redo and saving. **Our tools add only what Unity lacks.** **Play-from-here is the most important one.**

- **Play-from-here:** a Scene-view shortcut enters Play mode at the cursor position with a chosen set of abilities (checkboxes in the daedalus window). Leaving Play mode returns to the same Scene view. The runtime accepts a start position and an ability set from `movement-prototype` on, so the tool only has to pass them.
- **Play from start:** Unity's Play button, with only the starting abilities.
- **Inspectors:** every student-facing setting as a slider, toggle or dropdown with a tooltip; internals hidden. Player settings live on a settings asset, so changes made during Play mode persist (live tuning) instead of reverting like ordinary component edits.
- **Enemy presets:** preset assets students create, name and edit; placed enemies reference one, with per-instance overrides.
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
