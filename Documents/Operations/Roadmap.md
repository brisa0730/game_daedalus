# Roadmap

Active work plan: the **ordered milestone/phase sequence** (§1), plus pre-launch gates, deferred features and open decisions. Live state: GDD version / stack in the [GDD.md](../GDD/GDD.md) header; open issues in [Issues.md](Issues.md). Per-version narrative lives in [Changelog.md](Changelog.md). Estimated 4–6 weeks to a class-ready v1.

⚠️ **Phase numbers are assigned when a phase STARTS, never in advance.** §1 lists unstarted work by name and order only.

---

## 0. Completed Record

**What belongs in this table:** a phase moves here when its **work is closed**: code shipped and play-checks discharged, or explicitly re-scoped away.

| # | Phase | Shipped | Record |
|---|---|---|---|
| — | Scaffold (plan, Milestone 0) | v0.06 | [Changelog v0.06](Changelog.md), [plan](../Plans/archive/scaffold.md) |

## 1. Milestones & Phases

Each milestone ends with something playable.

### Milestone 1 — `movement-prototype` (days 1–4)
A dev test scene; run, jump (variable height, coyote time, buffering), double jump, wall slide / jump / climb (climbable tiles), rope climb. Scenario tests for every collision edge case. The runtime accepts a **start position and an ability set** from the start (the hook play-from-here needs). The Player component is tuned in Play mode (values copied out with Copy / Paste Component Values) so feel is tuned together, early. **Gate:** the operator play-checks the feel and tunes the defaults; this is the quality bar for everything else.

### Milestone 2 — `combat-and-enemies` (days 5–8)
The attack, HP, damage, i-frames, knockback, hit-stop, hazards and safe-ground return; the Enemy component with all movement types and the Shoots Projectiles add-on.

### Milestone 3 — `world-systems` (days 9–11)
Read the scene's Tilemaps and placed elements into the sim, active radius, camera, checkpoints (with in-game progress save), ability pickups, moving and crumbling platforms, exit and win screen.

### Milestone 4 — `editor-tools` (days 12–20)
Play-from-here with ability selection (build it first), the daedalus window, the Daedalus Add Component menu, custom inspectors (≤ ~8 settings, mode-specific settings hidden) and gizmos, validation warnings, scene autosave, terrain tiles with collision kinds (layouts per §4), and the starter sample (scene + example prefabs). Much smaller than the browser plan's editor: Unity provides painting, navigation, Add Component, prefabs, undo and saving. **Mid-milestone student check:** put a student in front of the Unity Editor with the tools about halfway through, not at the classroom test. Time a few tasks (make an enemy that chases, put double jump behind a pickup) against the operator's experience of Corgi / Flare.

### Milestone 5 — `assets-and-sharing` (days 21–26)
Default art pack (CC0, pre-mapped) in the starter, tileset setup templates, sprite-sheet animation sets with fallbacks, the WebGL build-and-share path, and the first OpenUPM release. Needs the hosting decision (§4).

### Milestone 6 — `classroom-test` (days 27+)
Run a class session with students, including the install path on a school machine (Unity Hub, the editor with WebGL support, the OpenUPM registry, the package, the starter); fix what blocks them.

## 2. Pre-launch gates

- **Classroom test passed.** A student completes the five v1 success steps (GDD.md § Core Loop: paint, place, tune, play-from-here, save + share) in one class session.
- **Performance.** 60 fps on a typical school laptop with ~200 entities on the map, in the WebGL build in Chrome and in Play mode.
- **Both editors.** Every suite green on 6000.0.85f1 and the current dev editor.
- **Update safety.** A fixture project from each released package version opens, upgrades and plays on the newest version.
- **Default art pack licensed.** License file beside every third-party pack in the package's `Samples~/`.
- **Hosting for shared builds** running, with whatever moderation / takedown path the operator wants for student content.

## 3. Deferred features

Out of scope for v1. Each trigger is "an operator decision after v1" unless stated; record costs and risks here when known, never a preferred answer.

- **Swinging ropes** (physics-based). v1 ropes are ladder-style.
- **Multiple attacks, combos, magic, ranged player attacks.**
- **Pathfinding enemy AI.** Getting stuck on terrain is accepted in v1.
- **Boss scripting, multi-phase patterns.**
- **Event / scripting system for students, including C# scripting.** Against the design pillar "students use the tool, they don't read its code"; treat as the strongest scope-creep signal.
- **Separate rooms with screen transitions.** v1 is one continuous map.
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
| License for the package | An OSI open-source license (e.g. MIT, Apache-2.0) · source-available without OpenUPM (git URL only) | Decide before milestone 5: OpenUPM lists only open-source packages | `assets-and-sharing` (first OpenUPM release) |
| Hosting for shared WebGL builds | A teacher-run static site (e.g. GitHub Pages) · itch.io under a teacher account · handing the build folder around (no hosting) | Decide before milestone 5 | `assets-and-sharing` |
