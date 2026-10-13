# Systems
**Last updated:** v0.01

Per-system contracts. Values and defaults live in [entities.md](entities.md). All Draft.

## Player

One player character with every ability built in. Per project, each ability is either **on from the start** or **unlocked by a pickup**. Movement feel is the top quality priority: it must feel good with default settings.

| Ability | Behavior | Notes |
| --- | --- | --- |
| Run | Accelerate / decelerate on ground and in air | Separate ground and air acceleration |
| Jump | Variable height (release early = shorter jump) | Coyote time and jump buffering required |
| Double jump | Extra jump(s) in the air; resets on landing, wall or rope | Count is a setting (default 1) |
| Wall slide + wall jump | Slow slide when pressing into a wall; jump pushes away | Brief input lock after a wall jump so the player doesn't re-stick |
| Wall climb | Cling and move up/down a wall | **Open:** all walls, only climbable tiles (recommended), or no climb |
| Rope / vine climb | Grab a vertical rope, climb up/down, jump off | Ladder-style, no swinging |
| Attack | One attack; see Combat & Damage | **Open:** auto-targeting (recommended), melee slash, or both |

## Combat & Damage

- **Attack (auto-targeting, recommended):** picks the nearest enemy or destructible projectile within range and line of sight (raycast against solid tiles) and fires a fast homing bolt. No target in range: a short burst in the facing direction. No aiming. Damage, range and cooldown are settings.
- **On hit:** brief hit-stop (~50 ms), hit flash, enemy knockback.
- **Taking damage:** lose HP, knockback, i-frames (~1 s) with sprite flicker.
- **Hazards** (spikes, pits): damage, then return the player to the **last safe ground position** (not the checkpoint), unless HP hits 0.
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

- **Map:** a tile grid with a fixed tile size (**open:** 16 px recommended, or 32 px). Up to ~500 × 200 tiles.
- **Layers:** background decoration, collision, foreground decoration. Only the collision layer affects gameplay (but see § Tile System → Open questions).
- **Tile kinds:** solid; one-way platform (jump through from below, drop down with down + jump); climbable wall (if wall climb is restricted to marked tiles); hazard; none/decorative. Painted through terrains and tiles (§ Tile System).
- **Placeable elements:** [entities.md](entities.md) § Placeable elements.

## Camera

Follows the player smoothly with a small look-ahead in the movement direction, clamped to the map bounds. Camera-bound zones are deferred (Roadmap §3).

## In-game Progress (the played game)

On touching a checkpoint, the played game saves position, unlocked abilities and collected pickups to browser storage, so a long map can be continued later.

## Editor

Most of the work, and its usability decides whether students succeed. **Play-from-here is the most important feature.**

- **Navigation:** pan (middle-drag or space + drag), zoom (wheel), and a minimap that jumps the view on click.
- **Tile painting:** brush, rectangle fill, eraser, eyedropper; layer selector.
- **Entity placement:** a palette of elements; drag to place, move, delete. Multi-select is nice to have.
- **Inspector:** select an entity to see its settings as sliders, toggles and dropdowns. Player settings in a project-level panel.
- **Enemy presets:** create, name and edit presets; per-instance overrides on placed enemies.
- **Play-from-here:** start at the cursor position with a chosen set of abilities (checkboxes). Esc returns to the editor at the same view.
- **Play from start:** the real experience, with only the starting abilities.
- **Undo / redo** for every edit (command pattern).
- **Save / load:** autosave to browser storage, plus export/import of a project file.
- **Validation warnings:** no player start, no exit, an ability pickup that's never placed, entities outside the map.

## Tile System

Tile painting works like Unity's Tilemap with Rule Tiles: students paint *terrain*, and the tool picks the correct edge, corner and inner-corner tiles automatically. This is the main way simple art ends up looking clean.

- **Tile palette:** a panel showing the tileset. Click a tile, or drag-select a group to use as a multi-tile brush.
- **Terrain brushes (auto-tiling):** each terrain (e.g. "Grass ground", "Stone") is a brush. Painting or erasing updates the painted cell and its neighbors.
- **Random tiles:** a terrain slot can hold several variants; the tool picks one per cell with a seed derived from the cell position, so the result is stable across reloads.
- **Animated tiles:** a tile with a frame list and fps (water, lava, torches).
- **Per-tile kind:** every tile or terrain carries a collision kind (solid, one-way, climbable wall, hazard, none). Painting spikes also places spike collision, so the collision layer is derived from what students paint.
- **Layers:** background, collision and foreground (§ World & Level Elements). Auto-tiling works on every layer.

### Auto-tile layouts

No free-form rule editor. The tool supports fixed layouts, each with a template image.

| Layout | Tiles per terrain | Use |
| --- | --- | --- |
| Blob | 47 | Imported commercial tilesets; full edges, corners and inner corners |
| Dual-grid | 15–16 | Default for student-drawn terrain; clean corners with few tiles |
| Single tile | 1 | Simple blocks, decoration, flat-color styles |

Which layouts ship is an open decision (Roadmap §4; recommendation: blob + dual-grid).

- **Blob:** 8-neighbor bitmask; a diagonal bit counts only when both adjacent edge bits are set, which reduces 256 cases to 47.
- **Dual-grid:** display tiles are offset half a tile from the logical grid; each display tile looks at its 4 surrounding logical cells (16 cases). **Collision uses the logical grid**, not the display grid.
- Map edges count as "same terrain", so terrain touching the border shows no edge.
- In v1 each terrain auto-tiles only against itself; other terrains count as empty. Terrain-to-terrain transitions are out of scope (Roadmap §3).
- Each edit recomputes only the changed cell and its neighbors; a load recomputes everything.

### Tileset setup

- Import a PNG, set the tile size, then drag tiles into the template slots for the chosen layout. The template shows each slot's neighbor pattern visually.
- Downloadable blank template PNGs for each layout, so students can draw terrain directly in the right slots and import with no mapping step.
- The default art pack ships pre-mapped.

### Open questions

- **Where collision comes from.** § World & Level Elements has a separate collision layer students paint; this section derives collision from each tile's or terrain's kind and auto-tiles every layer. Does a kind on a background/foreground tile count, or only tiles on the collision layer?
- **Spikes: tile or element?** Spikes are a placeable element (entities.md § Placeable elements) and also a `hazard` tile kind. One, the other, or both?

## Asset Import

- A **default art pack** (tiles, player, a few enemies, props) ships so students can build immediately.
- **Tileset import:** a PNG, cut by tile size, then mapped to an auto-tile layout (§ Tile System → Tileset setup).
- **Sprite-sheet import:** set the frame size, then assign frame ranges to animation states with a live preview; frame rate per state. Missing states fall back ([entities.md](entities.md) § Animation states).
- Assets are embedded in the project file, or stored alongside it for hosting.

## Share

Publish a project to a link that opens the game in **play mode only** (no editor). The hosting backend is an open decision (Roadmap §4).
