# Systems
**Last updated:** v0.11

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

Built in phase 1 (`movement-sim`) in `PlayerStepper`; values in [entities.md](entities.md) § Player tunables. Settled with the operator on 2026-10-23; the remaining proposals were confirmed by the operator's Phase 2 play-check (2026-11-02).

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
- **Map bounds:** the sides and top are solid; falling out of the bottom is a death (a pit, § Death and respawn).
- **Nearest free start:** a start inside solid moves to the nearest free cell-centred spot within 8 cells (ties: up, then left).
- With jump off there are no ground or coyote jumps, but air jumps work after walking off a ledge.
- Wall jump: full jump height at 1.5 × run speed push-off. Two walls in reach: the pressed side, then the nearer, then the left.
- Cling entry: toward a climbable wall in the air while falling or holding Up, or Up + toward on the ground. Neutral holds; pressing away lets go (with wall coyote). A rising jump past a climbable face doesn't cling by itself. Climbing off the bottom of a climbable run lets go without wall coyote.
- Timer convention: a window of N ticks is active on the N ticks after it is set (coyote, buffer, wall coyote 6; lock, lockout 12).
- A rope can be grabbed from above with Down while standing on a one-way at its top. Standing, Up grabs a rope in reach; Down grabs only in that case. Ropes are grabbed only from the air or standing.
- Down + jump while on a rope lets go without a jump (the re-grab lockout applies), so "hold Down, then jump" on a rope's platform drops instead of rope-jumping. Climbing down onto a one-way just under a rope's bottom stands on it.
- Rope jump horizontal speed = input × run speed. Climb speed is shared by walls and ropes.
- Landing ends the wall-jump input lock.
- Every numeric internal value ([entities.md](entities.md) § Player tunables).
- **A press next to an ordinary wall is a wall jump, not an air jump** (the priority above; operator, 2026-11-02). Within reach of a wall with wall jump unlocked, the air jump stays unused; a ledge that needs double jump is reached by air-jumping away from the wall.
- **Abilities can change during play** (`PlayerStepper.ChangeAbilities`; operator, 2026-10-26): a gained ability works from the next tick; a hold the new set can't keep lets go (a rope without rope climb, a cling without wall climb, a slide without wall jump). The Player's toggles use it live in Play mode; ability pickups (`world-systems`) will too.
- **Rope tops snap** onto the nearest one-way surface in their column within 0.5 tile at play start (ties go up; never onto solid; never inverting the rope), so freely placed ropes still let the player step off.

## Combat & Damage

- **Attack (auto-targeting):** picks the nearest enemy or destructible projectile within range and line of sight (raycast against solid tiles) and fires a fast homing bolt. No target in range: a short burst in the facing direction. No aiming. Damage, range and cooldown are settings.
- **On hit:** brief hit-stop (~50 ms), hit flash, enemy knockback.
- **Taking damage:** lose HP, knockback, i-frames (~1 s) with sprite flicker.
- **Hazards** (spike tiles, pits, Hazard traps, being crushed): the Player's **On hazard** setting decides: **Return to safe ground** (default) or **Respawn at checkpoint** (troll levels). Built in `traps`, before HP exists; from `combat-and-enemies` a hazard also costs HP, and HP 0 always respawns at the checkpoint.
- **Hazards don't block; they kill on overlap** of the player's **hazard core** (the hitbox inset by 0.2 tile per side), so grazing a spike's edge is safe. A one-row hazard strip can't be skipped at max fall or push speed.
- **Safe ground** is recorded only while standing fully on painted non-hazard tiles, outside every trap trigger and not overlapping any trap's box, so a dropped floor can't become the return point and a return never lands in a zone.
- **HP 0:** respawn at the last checkpoint with full HP. Unlocked abilities are kept.

### Death and respawn (`traps`, v0.10)

- **One death, one rule:** every hazard hit, return to safe ground included, plays a **death beat** of 0.4 s (24 ticks; the player flashes, then a burst replaces it; the same for spikes, pits and crush), then the player reappears at once. **Every death resets all traps.**
- **During the beat** input is ignored and pending trap Delays cancel; a jump held through the beat doesn't fire on respawn; death wins over an Exit touched on the same tick; the camera cuts (no slide) on respawn.
- **Pits** are a death only below the map bottom; there is no per-column pit rule.
- **Respawn point:** where Play began, until a checkpoint is touched. A moved start or respawn lands on non-hazard support, clear of hazards and of present Solid traps (a Player placed on a spike strip lives).
- **One touch rule:** Touch triggers, the Checkpoint and the Exit fire on the hitbox grown by 0.0625 tile (the contact margin), so standing against one counts; hazards use the inset core instead.
- **Death counter:** a skull and a number in the top-left corner, counting every death since Play began, with a small bump on each death; no student setting.
- **Checkpoint:** touching it sets the respawn point (the latest touched wins; recorded under either On hazard rule) and tints it gold while active. The in-game progress save arrives in `world-systems`. **Exit:** touching it freezes the game and dims it under a win screen ("You made it!", "Deaths: N"); input is ignored for 1 s, then any key plays again from the start (counter 0, traps reset).

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
- **Tile kinds:** solid; one-way platform (jump through from below, drop down with down + jump); climbable wall (if wall climb is restricted to marked tiles); hazard; none/decorative. Painted through terrains and tiles (§ Tile System). In the sim (`TileKind`): None 0, Solid 1, OneWay 2, Climbable 3 (collides as solid), Hazard 4 (doesn't block; kills). The runtime fills a 0-based `TileGrid` from the Collision Tilemap at play start.
- **Ropes** reach the sim as a list of vertical segments (`RopeSegment`: centre x, bottom, top).
- **Plain tiles are solid:** any tile on the Collision Tilemap that isn't a `DaedalusTile` counts as Solid; a `DaedalusTile` brings its own kind.
- **Map bounds:** the painted cells of the Collision Tilemap, grown to include the start, the ropes, the traps, checkpoints and exits, plus 24 empty rows on top ([architecture.md](architecture.md) § Data Flow). Past the painted extent the sides are solid, so a pit must sit between painted tiles.
- **Placeable elements:** [entities.md](entities.md) § Placeable elements.
- **Sections:** troll levels are short sections side by side on the one map, each starting at a checkpoint. No rooms, no level select.

### Traps

The **Trap** component turns an object into a piece of the level that betrays the player: a floor that drops, a spike strip that pops out, a ceiling that falls, an exit that runs away. **A trap holds its own trigger and one action; traps never trigger each other** (no chaining: that is where scripting starts). Settings in [entities.md](entities.md) § Trap.

- **Triggers:** the player touches the trap (the contact margin), or the player enters its **zone** (a box drawn in the Scene view, stored relative to the trap's object so copies keep their own; default: the trap's box grown **upward by 1 tile**, so a Drop Floor fires as the player steps onto it). Several traps fire together by sharing the same zone (multi-object editing, or copies at the same position), with Delay for timing. The inspector's **Fit zone around trap** button resets it; its Scene-view handles snap to whole tiles.
- **Actions:** **Vanish** and **Appear** (hidden until it fires) since v0.10; **Move** by an offset at a speed and **Fall** under the player's gravity until it lands on solid or climbable tiles arrive with the moving traps. Every action works on an Exit (no special cases); new traps still default to Vanish. One default zone for every action: a Falling Ceiling's zone is dragged under it (the starter's example carries its own).
- **Fires once per life.** A trap re-arms only after a death resets it and the player has been outside its trigger, from every start source (play start, checkpoint respawn, return to safe ground), so a start or checkpoint inside a zone doesn't fire the trap on every respawn.
- **Shape and look:** a box of whole tiles drawn with one Tile repeated across it (the object's SpriteRenderer in Tiled mode with the Tile's sprite), so a trap looks exactly like the ground around it. The box is centred on the object and snaps to whole cells (a **Snap to grid** button and a warning for an off-grid trap). The Tile's collision kind is the trap's kind: **Hazard** (a spike tile) or else **Solid**. Rule Tiles and animated tiles can't be drawn on a trap; sprites should use Mesh Type Full Rect. On an Exit, the trap moves the exit instead and has no shape or tile.
- **Traps over painted tiles:** the painted Collision tiles stay in charge, with a warning. Where traps overlap, Solid beats Hazard.
- **Appearing on the player:** a Solid trap that appears on the player pushes it out the shortest free way (ties: up, left, right, down), else crushes it. A start inside a present Solid trap is moved out by the start search.
- **Moving solids:** traps move before the player each tick. A Solid trap **carries** a player standing on it, **pushes** a player in its way and **crushes** (kills) a player whose push is blocked by the grid. A falling trap carries the player down no faster than the player falls, so the floor drops away. A push lets go of a rope. Traps ignore one-way tiles and each other. **Hazard traps** kill on touch and never carry or push.
- **Move and Fall** (moving traps, operator 2026-11-02, all as recommended in the design pass):
  - **Move** travels in a straight line to its end (the offset, whole tiles) at constant speed, with no easing, **through painted tiles**, and stays there. A help box warns when the end overlaps painted Solid ground.
  - **Fall** starts at rest and falls under the player's base gravity, capped at the player's max fall speed, until it lands on **painted Solid or Climbable** tiles. It falls through one-way tiles and other traps; out of the map bottom it is gone until the next reset.
  - **Riding:** a player standing on a Solid trap is standing (ground jumps work). Fall's gravity is weaker than the player's fall gravity, so a rider stays on a falling floor and can jump off it. A trap moving down faster than that pulls away; the player gets the usual coyote window and lands back on it.
  - **Push vs carry:** only a trap's **leading face** pushes, and only a **blocked push** crushes; a rising trap that lifts the player into a ceiling crushes. Carrying is soft: a rider carried sideways into a wall stops there and the trap slides out from under. A trap's sides push but never carry; only standing on top carries.
  - **Crush forgiveness:** before crushing, the player is nudged up to **0.25 tile** sideways (the ceiling-corner correction distance), so a falling ceiling that clips the player's edge pushes them aside.
  - **No momentum:** leaving a moving trap keeps only the player's own speed.
  - **Walls:** a trap is never Climbable, so the player can wall-slide on a trap's side but never cling to it.
  - **The zone stays where it was drawn;** a trap moves only after firing, and fires once per life.
  - **The death beat freezes every trap**, moving ones included; respawn resets them.
  - Resting traps count as ordinary trap cells; only moving ones are boxes between cells (the **moving-box core**, reused by `world-systems`' platforms).
- **Readable in the Scene view:** an outline always (selected or not), the zone as a box, an Appear trap as a faded ghost (Edit mode only), a Move trap's end as an arrow and outline with a **handle that drags the end**, snapping to whole tiles like the zone handles; a Fall trap's **landing outline** (no ground below: the help box says "Falls out of the map"); a help box tells each trap's story in words ("Hidden until the player enters the zone, then appears"), and for Move and Fall names the distance and travel time ("Slides 5 tiles right in 0.6 s after the player enters the zone").
- **Fake walls and floors** need no feature: paint them on the Foreground Tilemap.

## Camera

Follows the player smoothly with a small look-ahead in the movement direction, clamped to the map bounds. Camera-bound zones are deferred (Roadmap §3).

**Built so far (v0.10):** a minimal snap follow, clamped to the map and cut on respawn, attached automatically to the main camera at play start. Smoothing and look-ahead arrive in `world-systems`.

## In-game Progress (the played game)

On touching a checkpoint, the played game saves position, unlocked abilities and collected pickups through `PlayerPrefs` (kept in the browser by a WebGL build), so a long map can be continued later.

## Editor Tools

Students work in the Unity Editor, and its usability with our tools decides whether they succeed. **Unity provides** navigation (Scene view pan and zoom), tile painting (Tile Palette: brush, box fill, eraser, picker; the active Tilemap picks the layer), creating and placing objects, Add Component, prefabs and prefab overrides, the Inspector, undo/redo and saving. **Our tools add only what Unity lacks.** **Play-from-here is the most important one.**

- **Play-from-here:** a Scene-view shortcut enters Play mode at the cursor position with a chosen set of abilities (checkboxes in the daedalus window). Leaving Play mode returns to the same Scene view. The runtime accepts a start position and an ability set from `movement-prototype` on, so the tool only has to pass them.
- **Play from start:** Unity's Play button, with only the starting abilities.
- **Daedalus components:** every kind of game object is one component in the Add Component menu under **Daedalus** (§ Placeable elements in [entities.md](entities.md)). Adding it makes the object work; it needs nothing else from Unity. A few add-ons extend one component (e.g. Shoots Projectiles on an Enemy). The starter's ready-made prefabs are examples to inspect and copy, not the main way to build.
- **Inspectors:** at most ~8 settings per component (the Player: per collapsible section), each a slider, toggle or dropdown with a tooltip; mode-specific settings show only in their mode; internals hidden.
- **Play mode:** no special treatment. Edits made during Play mode work at once (every Player setting applies live) and are thrown away when Play ends, as everywhere in Unity; this is one of the things students learn. Unity's Play-mode tint (set in the starter's guidance) shows when edits won't stick.
- **Missing-piece hints:** a component that needs something (e.g. an Enemy with no sprite) says so in a help box in plain words.
- **Gizmos:** the Scene view shows ranges and paths (aggro range, attack range, moving-platform route, trap zones, ghosts and end positions).
- **Validation warnings** in the daedalus window and on entering Play mode: no player start or more than one, no exit, an ability pickup that's never placed, elements outside the map.
- **Trap warnings (v0.10)**, in the Trap's inspector and once in the Console at play start: a trap over painted Collision tiles, a hazard tile on a decoration Tilemap (Console only), a trap off the grid, no Tile, a Tile that can't be drawn (Rule or animated), a sprite that isn't Full Rect (inspector only: an import setting), a scaled or rotated trap, a trap on an Exit or Checkpoint. The trap's story names an early zone (one reaching more than a tile ahead). The Checkpoint notes that it has no effect under Return to safe ground.
- **Scene autosave:** Unity does not autosave scenes; the tools save the open scene on entering Play mode and on a timer.
- No minimap: Scene-view zoom covers it.

## Tile System

Built on Unity's Tilemap and Tile Palette: students paint *terrain*, and auto-tiling picks the correct edge, corner and inner-corner tiles. This is the main way simple art ends up looking clean.

- **Tile Palette:** Unity's. Click a tile, or drag-select a group to use as a multi-tile brush.
- **Terrain brushes (auto-tiling):** each terrain (e.g. "Grass ground", "Stone") is a preconfigured tile asset (a Rule Tile from 2D Tilemap Extras for the layouts it covers). Painting or erasing updates the painted cell and its neighbors.
- **Random tiles:** a terrain slot can hold several variants, picked per cell by position, so the result is stable across reloads.
- **Animated tiles:** a tile with a frame list and speed (water, lava, torches).
- **Per-tile kind:** every tile or terrain asset carries a collision kind (solid, one-way, climbable wall, hazard, none); built so far as `DaedalusTile` (v0.08), with plain tiles counting as solid. The kind takes effect **only on the Collision Tilemap**, so gameplay comes from what students paint there. **Spikes are hazard tiles** painted on the Collision Tilemap, not placed elements.
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

Students share games on the **class gallery** at `suwon.orcarin.com`, a website only the teacher and the students can enter.
- **Make the upload:** **Daedalus → Share my game** asks for a title and the student's nickname, makes a WebGL build with the settings the gallery needs, takes a screenshot for the card, and writes one zip. A build contains only the game. The editor never goes online.
- **Sign in:** each student has their own ID and password; the teacher has one too. Only signed-in users see or play anything.
- **Upload:** a signed-in student uploads their zip in the browser; uploading again replaces their earlier version of that game. The gallery checks the zip (only a WebGL build, size limit) and rejects anything else with a plain message.
- **Play:** the gallery lists every game as a card (title, nickname, screenshot); a click plays it in Chrome.
- **Teacher:** creates student accounts, resets passwords, removes any game or account.
- **Safety:** an uploaded game runs where it can't use the gallery's login, so a game can never act as the signed-in user (how is settled by the `class-gallery` spike).
