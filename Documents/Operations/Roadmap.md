# Roadmap

Active work plan: the **ordered milestone/phase sequence** (§1), plus pre-launch gates, deferred features and open decisions. Live state: GDD version / stack in the [GDD.md](../GDD/GDD.md) header; open issues in [Issues.md](Issues.md). Per-version narrative lives in [Changelog.md](Changelog.md). Estimated 6–8 weeks to a class-ready v1.

⚠️ **Phase numbers are assigned when a phase STARTS, never in advance.** §1 lists unstarted work by name and order only.

---

## 0. Completed Record

**What belongs in this table:** a phase moves here when its **work is closed**: code shipped and play-checks discharged, or explicitly re-scoped away.

| # | Phase | Shipped | Record |
|---|---|---|---|
| — | Scaffold (plan, Milestone 0) | v0.06 | [Changelog v0.06](Changelog.md), [plan](../Plans/archive/scaffold.md) |
| 1 | `movement-sim` (Milestone 1, part 1) | v0.07 | [Changelog v0.07](Changelog.md), [prompt](../Phases/archive/phase1-movement-sim.md), [log](../../.claude/memory/logs/phases/phase1-log.md) |
| 2 | `movement-runtime` (Milestone 1 `movement-prototype`, part 2; play-check passed) | v0.08 | [Changelog v0.08](Changelog.md), [prompt](../Phases/archive/phase2-movement-runtime.md), [log](../../.claude/memory/logs/phases/phase2-log.md) |
| 3 | `death-and-respawn` (Milestone 2 `traps`, part 1: hazards, pits, checkpoint, exit, static traps, HUD, follow camera; editor check and play-check passed) | v0.10 | [Changelog v0.10](Changelog.md), [prompt](../Phases/archive/phase3-death-and-respawn.md), [log](../../.claude/memory/logs/phases/phase3-log.md) |
| 4 | `moving-traps` (Milestone 2 `traps` complete: Move and Fall on the moving-box core, riding, pushing, crush nudge, runaway exit, Scene-view previews; editor check and play-check passed) | v0.13 | [Changelog v0.13](Changelog.md), [prompt](../Phases/archive/phase4-moving-traps.md), [log](../../.claude/memory/logs/phases/phase4-log.md) |
| 5 | `gallery-server` (Milestone 3 `class-gallery`, part 1: the Korean class gallery live under launchd; Chrome check through the tunnel passed; ISS-002 open) | v0.16 | [Changelog v0.16](Changelog.md), [prompt](../Phases/archive/phase5-gallery-server.md), [log](../../.claude/memory/logs/phases/phase5-log.md) |
| 7 | `first-release` (package 0.1.0 published on OpenUPM ahead of combat: MIT license, the Starter sample, Korean install guide, clean-install and fixture checks on both editors from file, Git URL and the registry; install hand check passed in v0.29) | v0.24, v0.25 | [Changelog v0.24, v0.25](Changelog.md), [prompt](../Phases/archive/phase7-first-release.md), [log](../../.claude/memory/logs/phases/phase7-log.md) |
| 8 | `stages` (troll games made of stages, one scene each: an Exit's Next stage, a quick fade, one death total, Share builds the chain; editor check and play-check passed; not yet in a package release) | v0.26 | [Changelog v0.26](Changelog.md), [prompt](../Phases/archive/phase8-stages.md), [log](../../.claude/memory/logs/phases/phase8-log.md) |
| 6 | `share-my-game` (Milestone 3 `class-gallery` complete: Daedalus → Share my game makes the Share zip; the gallery's own check accepts it on both editors; real upload, play and update on the live gallery passed; operator editor check not yet done (Deferred.md)) | v0.17 | [Changelog v0.17](Changelog.md), [prompt](../Phases/archive/phase6-share-my-game.md), [log](../../.claude/memory/logs/phases/phase6-log.md) |

## 1. Milestones & Phases

Each milestone ends with something playable.

### Milestone 4 — `combat-and-enemies` (days 16–19)
**Inherited from `first-release`:** a level saved before combat plays exactly as before. A Player setting "Hazard damage" defaults to all HP, and Players saved at data version 1 upgrade to that value.
**Inherited from `stages`:** HP carries between stages. Say what the stage change does to it, and test it across a real scene load.

The attack, HP, damage, i-frames, knockback, hit-stop (HP 0 respawns at the checkpoint); the Enemy component with all movement types and the Shoots Projectiles add-on. Hazards, death and respawn land in `traps`.

### Milestone 5 — `world-systems` (days 20–22)
**Inherited from `stages`:** abilities carry between stages, and the progress save resumes at the stage the player reached after the game is closed.

Read the scene's placed elements into the sim (the Collision Tilemap read and ropes land in `movement-runtime`; checkpoints, the exit and traps in `traps`), active radius, camera smoothing and look-ahead (a minimal snap follow lands in `traps`), the in-game progress save at checkpoints, ability pickups, moving and crumbling platforms (on the `traps` moving-box core).

### Milestone 6 — `editor-tools` (days 23–31)
Play-from-here with ability selection (build it first), the daedalus window, the Daedalus Add Component menu, custom inspectors (≤ ~8 settings, mode-specific settings hidden) and gizmos, validation warnings, terrain tiles with collision kinds (layouts per §4), and growing the Starter sample (0.1.0's ships from `first-release`; its pieces are frozen, so new ones arrive under new names). Much smaller than the browser plan's editor: Unity provides painting, navigation, Add Component, prefabs, undo and saving. **Mid-milestone student check:** put a student in front of the Unity Editor with the tools about halfway through, not at the classroom test. Time a few tasks (make a floor that drops when the player gets close, make an enemy that chases, put double jump behind a pickup) against the operator's experience of Corgi / Flare.

### Milestone 7 — `assets-and-sharing` (days 32–36)
Default art pack (CC0, pre-mapped, with a seamless single-tile ground style so traps look like normal ground) in the starter, tileset setup templates, sprite-sheet animation sets with fallbacks, and Share my game moved into the daedalus window.

### Milestone 8 — `classroom-test` (days 37+)
Run a class session with students, starting with troll levels, including the install path on a school machine (Unity Hub, the editor with WebGL support, the OpenUPM registry, the package, the starter) and a first sign-in and upload to the class gallery; fix what blocks them.

## 2. Pre-launch gates

- **Classroom test passed.** A student completes the five v1 success steps (GDD.md § Core Loop: paint, place, tune, play-from-here, save + share) in one class session.
- **Performance.** 60 fps on a typical school laptop with ~200 entities on the map, in the WebGL build in Chrome and in Play mode.
- **Both editors.** Every suite green on 6000.0.85f1 and the current dev editor.
- **Update safety.** A fixture project from each released package version opens, upgrades and plays on the newest version.
- **Default art pack licensed.** License file beside every third-party pack in the package's `Samples~/`.
- **Class gallery** running at `suwon.orcarin.com`: every student has an account, an uploaded game plays in Chrome, the teacher can remove a game, and an uploaded game can't act as the signed-in user.

## 3. Deferred features

Out of scope for v1. Each trigger is "an operator decision after v1" unless stated; record costs and risks here when known, never a preferred answer.

- **Swinging ropes** (physics-based). v1 ropes are ladder-style.
- **Multiple attacks, combos, magic, ranged player attacks.**
- **Pathfinding enemy AI.** Getting stuck on terrain is accepted in v1.
- **Boss scripting, multi-phase patterns.**
- **Trap chaining** (one trap triggering another), **repeating traps**, **gravity flip, reversed controls** and other rule-changing traps. v1 traps hold their own trigger and fire once per life; several traps fire together by sharing a zone.
- **Event / scripting system for students, including C# scripting.** Against the design pillar "students use the tool, they don't read its code"; treat as the strongest scope-creep signal.
- **Rooms the player can walk back and forth between,** with screen transitions (deferred 2026-11-09, when linear stages came in). The big map stays one continuous scene; troll games may be linear stages.
- **Level select; stage cards or names** (deferred 2026-11-09). Stages are a linear chain with a quick fade between them.
- **Dialogue, NPCs, shops, inventory.**
- **Multiplayer, online leaderboards.**
- **Mobile / touch controls.** Keyboard + gamepad only.
- **Collaborative real-time editing.**
- **Sound editor / student-importable sound.** v1 ships fixed sound effects.
- **Store publishing / native builds.** v1 shares WebGL builds only.
- **Terrain-to-terrain auto-tile transitions.** In v1 each terrain auto-tiles only against itself.
- **Student-authored auto-tile rules.** v1 ships preconfigured terrains; students fill sprite slots, never rules.
- **Minimap.** Scene-view zoom covers it in v1.
- **Camera-bound zones.**
- **Input rebinding.** Not needed in v1.
- **Enemy drops.** Death drops nothing in v1.

## 4. Open decisions

**The operator decides.** Each settled decision gets a `decisions.md` row and a GDD update, and its line is deleted here.

| Decision | Options | Recommendation | Blocks |
| --- | --- | --- | --- |
| Auto-tile layouts on Unity's Tilemap (reopens v0.02's "all three, dual-grid first") | Rule Tiles for blob + single only · plus a custom dual-grid tool (a display Tilemap offset half a tile, driven from the logical Collision Tilemap) | Blob + single on Rule Tiles first: they come with 2D Tilemap Extras and cover a CC0 pack. Build dual-grid only if the mid-`editor-tools` student check shows students drawing their own terrain need it | `editor-tools` |
