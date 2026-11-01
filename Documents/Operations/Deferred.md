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

### Play-start warnings appear only in the Console
- **Severity:** MED · **Source:** Phase 2 (`movement-runtime`, v0.08)
- **Description:** every Player / Rope problem (no Collision Tilemap, a Rigidbody 2D, a start in a wall, a rope top in a wall, grid settings…) is a one-shot `Debug.LogWarning` from [PlayerWarnings.cs](../../Packages/com.orcapafea.daedalus/Runtime/PlayerWarnings.cs). A student who never opens the Console misses it.
- **Why deferred:** Inspector help boxes need custom inspectors, which arrive with `editor-tools`.
- **Trigger to act:** the `editor-tools` custom inspectors. Fix shape: the same `PlayerWarnings` texts as help boxes on the Player / Rope Inspector (and in the daedalus window's validation list).

### A Rope with no sprite is invisible in Play
- **Severity:** LOW · **Source:** Phase 2 (`movement-runtime`, v0.08)
- **Description:** [Rope.cs](../../Packages/com.orcapafea.daedalus/Runtime/Rope.cs) draws only a Scene-view gizmo line; in Play a rope is visible only if the student added a sprite to it (the dev scene adds a Tiled SpriteRenderer child).
- **Why deferred:** art and missing-piece hints are later milestones; a default rope look belongs with the default art pack.
- **Trigger to act:** the `editor-tools` missing-piece hints ("This rope has no sprite") or the default art pack in `assets-and-sharing`, whichever comes first.
