# Roadmap

Active work plan: the **ordered milestone/phase sequence** (§1), plus pre-launch gates, deferred features and open decisions. Live state: GDD version / stack in the [GDD.md](../GDD/GDD.md) header; open issues in [Issues.md](Issues.md). Per-version narrative lives in [Changelog.md](Changelog.md). Estimated 4–6 weeks to a class-ready v1.

⚠️ **Phase numbers are assigned when a phase STARTS, never in advance.** §1 lists unstarted work by name and order only.

---

## 0. Completed Record

**What belongs in this table:** a phase moves here when its **work is closed**: code shipped and play-checks discharged, or explicitly re-scoped away.

| # | Phase | Shipped | Record |
|---|---|---|---|

## 1. Milestones & Phases

Each milestone ends with something playable.

### Milestone 0 — Foundation
- **Scaffold** (plan `scaffold`): the npm-workspaces repo (`packages/format`, `packages/runtime`, `apps/editor`, `apps/player`), exact-pinned tooling, Vitest seen failing then passing, Playwright with a first screenshot, and the **render check** (Canvas 2D, 500 × 200 chunked map, 200 moving entities, fps under a throttled CPU). Fills CLAUDE.md § Project Config (stack pins, build/run commands) and confirms the Layout line.

### Milestone 1 — `movement-prototype` (days 1–4)
Hard-coded test map; run, jump (variable height, coyote time, buffering), double jump, wall slide / jump / climb (climbable tiles), rope climb. Scenario tests for every collision edge case. The runtime accepts a **start position and an ability set** from the start (the hook play-from-here needs). The test map has **live sliders** for the movement settings so feel is tuned together, early. **Gate:** the operator play-checks the feel and tunes the defaults; this is the quality bar for everything else.

### Milestone 2 — `combat-and-enemies` (days 5–8)
The attack, HP, damage, i-frames, knockback, hit-stop, hazards and safe-ground return; the enemy object with all movement types and projectiles.

### Milestone 3 — `world-systems` (days 9–11)
Load maps from the project JSON, chunked rendering, active radius, camera, checkpoints (with in-game progress save), ability pickups, moving and crumbling platforms, exit and win screen.

### Milestone 4 — `editor-core` (days 12–24)
Pan / zoom / minimap, the tile system (palette, auto-tiling with **dual-grid + single** layouts, random and animated tiles, tileset mapping), entity placement, inspector, enemy presets, undo/redo, autosave + export/import, validation warnings, play-from-here with ability selection, play from start. Build play-from-here early: editor usability is the biggest risk. **Mid-milestone student check:** put a student in front of the editor about halfway through, not at the classroom test.

### Milestone 5 — `assets-and-sharing` (days 25–30)
Default art pack (CC0, pre-mapped), the **blob** auto-tile layout if the pack needs it, tileset and sprite-sheet import with animation mapping and fallbacks, publish-to-link. Needs the hosting decision (§4).

### Milestone 6 — `classroom-test` (days 31+)
Run a class session with students; fix what blocks them.

## 2. Pre-launch gates

- **Classroom test passed.** A student completes the five v1 success steps (GDD.md § Core Loop: paint, place, tune, play-from-here, save + share) in one class session.
- **Performance.** 60 fps in Chrome on a typical school laptop with ~200 entities on the map.
- **Default art pack licensed.** License file beside every third-party pack in `assets/`.
- **Hosting for shared links** running, with whatever moderation / takedown path the operator wants for student content.

## 3. Deferred features

Out of scope for v1. Each trigger is "an operator decision after v1" unless stated; record costs and risks here when known, never a preferred answer.

- **Swinging ropes** (physics-based). v1 ropes are ladder-style.
- **Multiple attacks, combos, magic, ranged player attacks.**
- **Pathfinding enemy AI.** Getting stuck on terrain is accepted in v1.
- **Boss scripting, multi-phase patterns.**
- **Event / scripting system for students.** Against the design pillar "students use the tool, they don't read its code"; treat as the strongest scope-creep signal.
- **Separate rooms with screen transitions.** v1 is one continuous map.
- **Dialogue, NPCs, shops, inventory.**
- **Multiplayer, online leaderboards.**
- **Mobile / touch controls.** Keyboard + gamepad only.
- **Collaborative real-time editing.**
- **Sound editor / student-importable sound.** v1 may ship fixed sound effects (§4).
- **Store publishing / native builds.**
- **Terrain-to-terrain auto-tile transitions.** In v1 each terrain auto-tiles only against itself.
- **Free-form auto-tile rule editor.** v1 has fixed layouts only.
- **Camera-bound zones.**
- **Input rebinding.** Not needed in v1.
- **Enemy drops.** Death drops nothing in v1.

## 4. Open decisions

**The operator decides.** Each settled decision gets a `decisions.md` row and a GDD update, and its line is deleted here.

| Decision | Options | Recommendation | Blocks |
| --- | --- | --- | --- |
| Hosting for shared links | Static hosting + simple upload API · a BaaS (e.g. Firebase/Supabase) · file export only | Decide before milestone 5 | `assets-and-sharing` |
