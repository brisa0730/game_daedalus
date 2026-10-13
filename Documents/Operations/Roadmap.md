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
- **Open decisions.** The operator answers §4, at least those that block the next steps: runtime (after the spike), editor UI framework, tile size, attack type, wall climb.
- **Scaffold** (plan): the npm-workspaces repo (`packages/format`, `packages/runtime`, `apps/editor`, `apps/player`), exact-pinned tooling, Vitest with a first failing-then-passing test, Playwright with a first screenshot, the performance check. Fills CLAUDE.md § Project Config (stack pins, build/run commands) and confirms the Layout line.
- **Runtime spike** (one day): custom Canvas/WebGL vs Phaser 3, judged on player-collision control, fixed-step + interpolation, chunked tile rendering at 500 × 200 tiles, and how cleanly a pure `step()` sits inside it. Output: a recommendation for the operator plus `framework.md` entries.

### Milestone 1 — `movement-prototype` (days 1–4)
Hard-coded test map; run, jump (variable height, coyote time, buffering), double jump, wall slide / jump / climb, rope climb. Scenario tests for every collision edge case. **Gate:** the operator play-checks the feel and tunes the defaults; this is the quality bar for everything else.

### Milestone 2 — `combat-and-enemies` (days 5–8)
The attack, HP, damage, i-frames, knockback, hit-stop, hazards and safe-ground return; the enemy object with all movement types and projectiles.

### Milestone 3 — `world-systems` (days 9–11)
Load maps from the project JSON, chunked rendering, active radius, camera, checkpoints (with in-game progress save), ability pickups, moving and crumbling platforms, exit and win screen.

### Milestone 4 — `editor-core` (days 12–24)
Pan / zoom / minimap, the tile system (palette, auto-tiling, random and animated tiles, tileset mapping), entity placement, inspector, enemy presets, undo/redo, autosave + export/import, validation warnings, play-from-here with ability selection, play from start. Build play-from-here early: editor usability is the biggest risk.

### Milestone 5 — `assets-and-sharing` (days 25–30)
Default art pack, tileset and sprite-sheet import with animation mapping and fallbacks, publish-to-link. Needs the hosting decision (§4).

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
| Attack type | Auto-targeting (Ori style) · melee slash · both as a project setting | Auto-targeting only in v1 | `combat-and-enemies` |
| Wall climb | All walls climbable · only marked tiles · wall jump only (no climb) | Only marked climbable tiles | `movement-prototype` |
| Runtime | Custom Canvas/WebGL · Phaser 3 | Custom for player physics; decide after a 1-day spike | Scaffold |
| Editor UI framework | React · plain TypeScript | React | Scaffold |
| Tile size | 16 px · 32 px | 16 px (pixel art, smaller assets) | `movement-prototype` |
| Hosting for shared links | Static hosting + simple upload API · a BaaS (e.g. Firebase/Supabase) · file export only | Decide before milestone 5 | `assets-and-sharing` |
| Student accounts | None (anonymous links) · class codes · school login | None in v1 | `assets-and-sharing` |
| Sound | None · fixed default sound effects · student-importable | Fixed default sound effects | `world-systems` |
| Default art pack | Make in-house · licensed free pack (CC0) | CC0 pack to start | `movement-prototype` (placeholder art until then) |
| Auto-tile layouts | Blob only · dual-grid only · both | Both: blob for imported packs, dual-grid for student-drawn art | `editor-core` |
| Collision source | Collision layer only · any layer's tile kind | None yet (systems.md § Tile System) | `world-systems` |
| Spikes | Placeable element · hazard tile kind · both | None yet | `combat-and-enemies` |
