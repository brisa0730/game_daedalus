# Issues

Open issues only. Resolved issues live in [Changelog.md](Changelog.md) with full postmortems — git history preserves any prior states of this file.

**Lifecycle.** Add a row when a new issue is found. Delete the row when resolved (write the Changelog entry in the same commit). Do NOT annotate with "original preserved for audit trail" — Changelog + git is the audit trail.

**Issue numbering.** Monotonic, never recycled. When filing a new issue, use `ISS-NNN` where NNN is one greater than the counter below. Closed issues are deleted from this file but their numbers stay retired. Bump the counter in the same commit as the new issue.

**Last issue assigned:** none · **Next free:** ISS-001

**Severity:** HIGH (safety/correctness regression), MED (functional gap or structural drift), LOW (perf/convention/cosmetic).

---

## Open (0)

<!-- Stub shape for an inline issue:

## ISS-NNN — <one-line title>
**Severity:** MED · **Status:** OPEN — filed YYYY-MM-DD · **Introduced:** <version/phase>
**Summary.** What broke, when it surfaced, scope of impact.
**Status.** What shipped or what's pending; verification/escalation path.
**Related:** ISS-...

If the issue needs subheadings (root cause, fix-option tradeoffs, code locations, verification
queries), keep only the stub above here and move the detail to
`.claude/memory/logs/issues/issNNN-<name>.md`, linked from the Status line.
-->
