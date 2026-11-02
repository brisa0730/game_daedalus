# Deferred Items

Known cleanup and follow-on work that has been *deliberately deferred* — not forgotten. Each entry names the source (which phase/commit surfaced it), the severity, why it was deferred, and what trigger would promote it to active work.

**Lifecycle.** Add an entry when shipping work that leaves a known leftover. **Delete the entry when the trigger fires and the work lands** — record the resolution in [Changelog.md](Changelog.md) for the version that performed it, in the same commit. Do not "mark done in place"; deletion + Changelog narrative is the audit trail.

**This file vs. its siblings:**
- [Roadmap.md](Roadmap.md) — pre-live gates + multi-phase deferred *features*.
- **Deferred.md (this file)** — code-level cleanup, dead-code removal, and non-blocking observability gaps left in the wake of larger landings.
- [Issues.md](Issues.md) — currently *open* bugs/tasks (anything with operator-visible incorrect behavior).

**Entry shape.** Every entry uses the same five fields:
- **Title** — one-line description of the leftover.
- **Severity** / **Source** — operator-visible impact if left forever (LOW = none today, MED = degrades but works, HIGH = will eventually break) and the phase/commit that surfaced it.
- **Description** — what the leftover is, with file:line references.
- **Why deferred** — the reason it wasn't done in the surfacing change.
- **Trigger to act** — the concrete condition that promotes this to active work, plus the fix shape so the next operator doesn't re-derive it.

Never add an entry without a concrete trigger — open-ended "do this someday" entries become permanent and stop being read.

---

## Pending

### `RebuildTrapCells` clears the whole trap layer on each start, rest or landing tick
- **Severity:** LOW · **Source:** Phase 4 (`moving-traps`, v0.13)
- **Description:** [LevelStepper.cs](../../Packages/com.orcapafea.daedalus/Runtime/Sim/LevelStepper.cs) `RebuildTrapCells` clears every trap cell and restamps every resting trap whenever any trap starts, comes to rest or leaves the map. Ordinary moving ticks never rebuild.
- **Why deferred:** a handful of traps on a classroom map costs nothing measurable; the full rebuild is the simplest correct order-independent rule.
- **Trigger to act:** `perf_check` or a profile showing it on a 500 × 200 map. Fix shape: restamp only the changed trap's old and new area plus any resting trap overlapping it (Hazard first, then Solid).

### The help box's Fall time uses the first Player found
- **Severity:** LOW · **Source:** Phase 4 (`moving-traps`, v0.13)
- **Description:** [TrapEditor.cs](../../Packages/com.orcapafea.daedalus/Editor/TrapEditor.cs) `ScenePlayer` takes the first Player in the scene for the Fall preview's gravity and max fall; with several Players the time may not match the one that plays.
- **Why deferred:** v1 scenes have one Player.
- **Trigger to act:** a scene with several Players (out of v1 scope) or a student report. Fix shape: use the Player the play start would use, or say which Player the time is for.

### Play-start warnings appear only in the Console
- **Severity:** MED · **Source:** Phase 2 (`movement-runtime`, v0.08)
- **Description:** every Player / Rope problem (no Collision Tilemap, a Rigidbody 2D, a start in a wall, a rope top in a wall, grid settings…) is a one-shot `Debug.LogWarning` from [PlayerWarnings.cs](../../Packages/com.orcapafea.daedalus/Runtime/PlayerWarnings.cs). A student who never opens the Console misses it.
- **Why deferred:** Inspector help boxes need custom inspectors, which arrive with `editor-tools`.
- **Trigger to act:** the `editor-tools` custom inspectors. Fix shape: the same `PlayerWarnings` texts as help boxes on the Player / Rope Inspector (and in the daedalus window's validation list), reusing the Trap inspector's pattern (v0.10: [TrapEditor.cs](../../Packages/com.orcapafea.daedalus/Editor/TrapEditor.cs) shows the trap warnings as help boxes from the same conditions and texts as play start).

### A Rope with no sprite is invisible in Play
- **Severity:** LOW · **Source:** Phase 2 (`movement-runtime`, v0.08)
- **Description:** [Rope.cs](../../Packages/com.orcapafea.daedalus/Runtime/Rope.cs) draws only a Scene-view gizmo line; in Play a rope is visible only if the student added a sprite to it (the dev scene adds a Tiled SpriteRenderer child).
- **Why deferred:** art and missing-piece hints are later milestones; a default rope look belongs with the default art pack.
- **Trigger to act:** the `editor-tools` missing-piece hints ("This rope has no sprite") or the default art pack in `assets-and-sharing`, whichever comes first.

### The Edit-mode trap look syncs only through the Trap inspector and Add Component
- **Severity:** LOW · **Source:** Phase 3 (`death-and-respawn`, v0.10)
- **Description:** a Trap's SpriteRenderer (Tiled draw mode, size, sprite from the Tile) is set by [TrapAuthoring.cs](../../Packages/com.orcapafea.daedalus/Editor/TrapAuthoring.cs) when the component is added and when Size or Tile change in the [Trap inspector](../../Packages/com.orcapafea.daedalus/Editor/TrapEditor.cs). A change made another way (a prefab override reverted, Paste Component Values, a script) leaves the Scene view showing the old look until the trap is next edited. Play is unaffected: play start applies the look from the settings ([TrapLook.ApplyPlayLook](../../Packages/com.orcapafea.daedalus/Runtime/TrapLook.cs)).
- **Why deferred:** the common paths (Add Component, the inspector, Undo) are covered; a global change hook is more machinery than the gap is worth today.
- **Trigger to act:** a student or operator report of a stale trap look in the Scene view. Fix shape: an `ObjectChangeEvents` hook that re-syncs changed Traps.

### A scene with no Exit isn't warned about
- **Severity:** LOW · **Source:** Phase 3 (`death-and-respawn`, v0.10)
- **Description:** a level without an `Exit` simply can't be won; nothing tells the student. systems.md § Editor Tools lists "no exit" among the validation warnings.
- **Why deferred:** the validation list lives in the daedalus window, which arrives with `editor-tools`; a play-start Console warning alone would fire on every movement test level.
- **Trigger to act:** the `editor-tools` validation list. Fix shape: one "no Exit in the scene" entry there.

### A Grid or Collision Tilemap moved by a fraction of a tile isn't warned about
- **Severity:** LOW · **Source:** Phase 3 (`death-and-respawn`, v0.10; code review)
- **Description:** the sim's cells assume the Collision Tilemap's grid lines sit at whole world units. If a student moves the Grid or the Tilemap by a fraction, [TrapLook.ToSpec](../../Packages/com.orcapafea.daedalus/Runtime/TrapLook.cs) and [LevelCollector](../../Packages/com.orcapafea.daedalus/Runtime/LevelCollector.cs) round trap, checkpoint and exit boxes onto cells up to half a tile from where they are drawn, with no warning. The same assumption has held for the player and ropes since Phase 2; traps make it visible.
- **Why deferred:** no student path moves a Grid by a fraction today (the starter's Grid sits at the origin), and the check belongs with the other validation warnings.
- **Trigger to act:** the `editor-tools` validation list, or any report of a trap firing or killing away from its drawn box. Fix shape: a play-start warning (and validation entry) when the Collision Tilemap's cell (0, 0) isn't at a whole world position.
