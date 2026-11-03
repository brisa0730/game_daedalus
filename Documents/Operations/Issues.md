# Issues

Open issues only. Resolved issues live in [Changelog.md](Changelog.md) with full postmortems — git history preserves any prior states of this file.

**Lifecycle.** Add a row when a new issue is found. Delete the row when resolved (write the Changelog entry in the same commit). Do NOT annotate with "original preserved for audit trail" — Changelog + git is the audit trail.

**Issue numbering.** Monotonic, never recycled. When filing a new issue, use `ISS-NNN` where NNN is one greater than the counter below. Closed issues are deleted from this file but their numbers stay retired. Bump the counter in the same commit as the new issue.

**Last issue assigned:** ISS-002 · **Next free:** ISS-003

**Severity:** HIGH (safety/correctness regression), MED (functional gap or structural drift), LOW (perf/convention/cosmetic).

---

## Open (1)

## ISS-002 — A game sometimes stays black in the class gallery (Unity never starts)
**Severity:** MED · **Status:** OPEN — filed 2026-11-03 · **Introduced:** Phase 5 `gallery-server` (v0.16), seen in the Step 22 Chrome check
**Summary.** Opening a game in the live gallery sometimes leaves the frame black: Unity's loader never starts the engine (the canvas stays 300 × 150, no Unity log). Seen 2 times in 4 full automated Chrome runs, 0 times in 15 targeted loads (fresh loads, 304 revalidations, re-uploads, a download cut by a server restart). In the stalled loads Chrome received the start of `*.wasm.br` (200) and the body never finished. The server's bytes were verified identical to the build; the tunnel log had no errors. Once, after two forced server restarts mid-load, the stall survived a Chrome restart and only clearing Chrome's HTTP cache recovered it, which points at a cached partial file, but that was never reproduced on purpose. A student would see a black box until they clear their cache.
**Status.** Safeguard shipped, not proven to be the fix: build files send `Accept-Ranges: none` and a range request always gets the whole file, never a 304 (tested). Next steps when it recurs: capture the wasm request in DevTools (status, `If-None-Match`, `Range`, size received); consider versioned build URLs with `immutable` caching (no revalidation at all), and a player-page watchdog that shows the Korean "reopen" line if Unity hasn't started in ~60 s. Trigger: any recurrence, or Phase 6's first real-student builds. Details: [phase5-log](../../.claude/memory/logs/phases/phase5-log.md) Step 22.
**Related:** ISS-001 (WebGL builds drew nothing; different cause, fixed v0.15).


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
