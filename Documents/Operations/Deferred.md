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

*(none yet)*
