# Architecture
**Last updated:** v0.26

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Architecture. The three assemblies and their reference direction landed with the scaffold (v0.06) and are guarded by EditMode tests.

## System Map

| Part | Owns | Must not |
|---|---|---|
| **Sim** (`Runtime/Sim/`, assembly `Orcapafea.Daedalus.Sim`) | `Step(state, input) → state`: player, collision, combat, enemies, level elements, in-game progress state; the tunable defaults. Since v0.07: the tile grid and tile collision (filled by the runtime at play start), player movement (`PlayerStepper`), `MovementSettings` / `MovementInternals`. Since v0.10: the world step `LevelStepper` around `PlayerStepper` (death, beat, respawn, checkpoints, exit, static traps; it owns its trap and checkpoint state in place and stays allocation-free), `LevelRules` (death, respawn and trap numbers), `TrapSpec`, `Box`, and a trap-cell layer in `TileGrid` (painted tiles stay in charge). Since v0.13: `MovingBoxes` (the moving-box layer `TileGrid` carries; every collision query tests it), `TrapMotion` (Move/Fall kinematics, shared with the editor's previews), `BoxPush` (carry, leading-face push, crush nudge, the one push effect) | Reference `UnityEngine` or `UnityEditor`; read the clock or a random source directly |
| **Runtime** (`Runtime/`, assembly `Orcapafea.Daedalus`) | The Daedalus components students add and tune, and our assets; reading the scene (Collision Tilemap, objects with Daedalus components) into sim data at play start; the 60 Hz tick loop; sprites, animation, camera and interpolation; Input System → one `Input` per tick; checkpoint saves. Since v0.08: `Player`, `Rope`, `DaedalusTile`; `CollisionMapReader` (Collision Tilemap → `TileGrid` at play start), `RopeCollector` (the play-start rope scan), `PlayerSession` (plain C# driver: stepper, accumulator, interpolation, restart), `PlayerControls` (Input System actions built in code), one-shot plain-words warnings (`PlayerWarnings`). Since v0.10: `Checkpoint`, `Exit`, `Trap` (with `TrapLook`: box, zone and Play-mode look), `LevelCollector` and `PlayStartScan` (play-start scans), `HelpTexts` (the trap story and notes, shared with the inspectors), `CameraFollow`, `PlayHud` (counter and win screen, uGUI built in code), `DeathBurst`; `PlayerSession` now drives `LevelStepper`. Since v0.13: `PlayerSession` interpolates moving traps (snapping on respawn and restart). Since v0.17: `Player.ReadMap` (the play-start map read) and `Player.FirstView` (the first frame's map and camera target, for the Share picture). Since v0.26: `StageRun` (a `DontDestroyOnLoad` object made at the first linked Exit: the fade, the scene change, the carried deaths), `StageLoad` (link resolve and scene load, the one Runtime file naming `UnityEditor`, under `#if UNITY_EDITOR`, because a stage loads by path without Build Settings), `ExitLink` | Make gameplay decisions outside the sim; write to authored assets; reference the editor assembly |
| **Editor tools** (`Editor/`, assembly `Orcapafea.Daedalus.Editor`) | Custom inspectors and gizmos, the Add Component menu entries, the daedalus window (play-from-here abilities, validation warnings), Scene-view play-from-here, terrain setup helpers. Since v0.10: the Trap, Checkpoint and Exit inspectors (help boxes, Fit zone, Snap to grid, zone handles; Undo-recorded), `TrapAuthoring` (Add Component setup), the Appear ghost gizmo (Edit mode only). Since v0.17: Share my game: `ShareMyGame` (menu and command-line entry), `ShareTitleWindow`, `ShareBuild` (stops, settings pin, scene guard, exception watch, build), `ShareZip`, `ShareScreenshot` (first-frame picture through `Player.FirstView` and `CameraFollow`), `ShareTitle` (the gallery's title cleaning), `ShareTexts`, `BuildScenes`. Since v0.26: `StageWalk` (follows the links, the stage checks), `StagePathStamp` (writes each Exit's scene path into a build's copy of the scene) | Contain gameplay logic (Architecture rule 3) |
| **Unity** (not ours) | Scene view, Tile Palette, Tilemap, Inspector, prefabs, undo/redo, scene saving, Sprite Editor, Package Manager, WebGL build | — |
| **Gallery server** (`Deploy/gallery/`, Rust crate `gallery`; since v0.16) | Accounts and sessions (since v0.18 a user's own password change, rotating the session), the teacher page, uploads and the Share zip check, game files and versions, play grants, the gallery's and the games host's pages, backups | Listen beyond loopback; trust anything from an upload (names, types, paths, HTML); change state on GET or from another origin; serve a gallery page into another site's frame. Shares nothing with the Unity package except the Share zip contract (platforms.md § Share zip); the `check_share_zip` dev example runs the upload check on a zip from the command line (since v0.17) |

**Dependency direction:** editor tools → runtime → sim, enforced by assembly references. The sim references nothing; the runtime also references the Input System and uGUI (`UnityEngine.UI`, v0.10).

**Play-start scans (the one exception to Architecture rule 5):** at play start, or when a component is added in the editor, the runtime may gather placed Daedalus components or Tilemaps with one `FindObjectsByType` scan and a deterministic sort (ropes by x, bottom, top; the Collision Tilemap pick: the one named Collision, else the only one; checkpoints, exits and traps by world box, through `PlayStartScan`). The main camera (`Camera.main`) is likewise found once at play start (v0.10). A new Player finds the `StageRun` the same way, with one scan at its play start (v0.26): it isn't placed, but there is no static reference to it. Never per frame, never as a lookup service.

## Time Model

- The sim advances in **fixed 60 Hz ticks**, driven by the runtime from Unity's frame loop with an accumulator (`FixedStepAccumulator`, in the sim); Unity's physics step is not used for gameplay. Rendering runs at the display rate and interpolates transforms between the last two sim states using the accumulator's interpolation fraction, always in [0, 1).
- **Hitch policy:** after a long frame at most **5 ticks** run at once; whole ticks beyond that are dropped (the game slows down for a moment instead of spiralling), and only the fraction of a tick carries over.
- Hit-stop (~50 ms) pauses the sim's gameplay advance, not rendering. Exact mechanism: decided in `combat-and-enemies`.
- Authored times are seconds; the runtime converts them to ticks.
- The clock advances by scaled `Time.deltaTime`, so `timeScale` 0 pauses.
- The death beat (0.4 s) and the win screen's input lock (1 s) are counted in ticks (24 and 60).
- **Reset snap:** on the tick where the sim replaces the state (a respawn), `PlayerState.Teleported` is true and the renderer draws the new position at once instead of interpolating across the jump.
- **Stage change (v0.26):** ticks stop at the Exit (`Won`) and start again only once the fade has cleared; the clock is reset first, so a long load frame can't become catch-up ticks. Details in [systems.md](systems.md) § Stages.
- **World tick order (v0.13):** pending trap delays → traps move (riders, pushes, crush, moving-Hazard check) → the player's step → trap triggers → death → checkpoints, then exits → safe ground. Moving traps are interpolated like the player and snap back with it on a respawn or restart.

## Data Flow

1. Students edit the scene and our assets with Unity's tools; Unity owns undo/redo and saving.
2. **Play from start** (Unity's Play button): the runtime reads the Collision Tilemap and the Daedalus components into the sim; the player starts where it was placed with the starting abilities (the Player's toggles; Jump is always on). The map is the cells of the Collision Tilemap that hold a tile, grown to include the start, every rope, trap (and every Move trap's end), checkpoint and exit, plus 24 empty rows on top (operator, 2026-10-23). Every start source (play start, play again, `Restart`) resets the level: traps, this stage's deaths, the respawn point (a `StageRun`, when there is one, keeps the game's total). Plain tiles count as solid. Every Player setting then applies live during Play, an ability toggle switching only its own ability.
3. **Play-from-here:** an editor tool records a start position and an ability set (the student's checkboxes) and enters Play mode; it calls `Player.Restart(PlayStart)` before the Player's `Start`, and that ability set replaces the toggles (Jump always on). The map is fixed at play start, so a later `Restart` is reliable only for a point inside it (checkpoints are). Leaving Play mode returns to the same Scene view.
4. A WebGL build runs step 2.
5. **1 Unity unit = 1 tile.** Sprites import at 16 pixels per unit; nothing else sees pixels.

## Active Radius

Enemies, projectiles and moving objects update only within ~1.5 screens of the camera; outside it they freeze in place. Tilemaps render in Chunk mode.

## Technology (v0.03)

- **Unity 6** (minimum 6000.0), C#, distributed as the UPM package `com.orcapafea.daedalus` through OpenUPM.
- **Sim: custom, kinematic.** Our own tile collision on the Collision Tilemap's grid and a pure `Step()`; no Rigidbody2D / Collider2D for gameplay. Exact platformer feel (coyote time, one-way platforms, wall climb) and EditMode scenario tests both need rules we own.
- **Rendering:** Unity's Tilemap and SpriteRenderer with the **URP 2D Renderer**. **Input:** the Input System package. **Tiles:** 2D Tilemap Extras (rule, random and animated tiles).
