# Issues

Open issues only. Resolved issues live in [Changelog.md](Changelog.md) with full postmortems — git history preserves any prior states of this file.

**Lifecycle.** Add a row when a new issue is found. Delete the row when resolved (write the Changelog entry in the same commit). Do NOT annotate with "original preserved for audit trail" — Changelog + git is the audit trail.

**Issue numbering.** Monotonic, never recycled. When filing a new issue, use `ISS-NNN` where NNN is one greater than the counter below. Closed issues are deleted from this file but their numbers stay retired. Bump the counter in the same commit as the new issue.

**Last issue assigned:** ISS-002 · **Next free:** ISS-003

**Severity:** HIGH (safety/correctness regression), MED (functional gap or structural drift), LOW (perf/convention/cosmetic).

---

## Open (1)

## ISS-002 — A game can load for minutes in the class gallery, and once stayed black until Chrome's cache was cleared
**Severity:** MED · **Status:** OPEN — filed 2026-11-03, rediagnosed 2026-11-04 (v0.22) · **Introduced:** Phase 5 `gallery-server` (v0.16), seen in the Step 22 Chrome check
**Summary.** Most "black frame" reports are a slow download, not a stall. The gallery server sends a game's 7.3 MB `game.wasm.br` in ~12 ms locally, but through the Cloudflare tunnel the same file took anywhere from 2 s to 162 s (curl on the Mac Mini, 2026-11-03/04), and a Chrome load once took 107 s to start Unity. Orcarin's tunnel on the same Mac shows the same ~160 KB/s ceiling at times (its files are small, so nobody notices). Switching the tunnel to HTTP/2 didn't help: 4 alternating rounds, HTTP/2 median 29.2 s vs QUIC 18.1 s, so QUIC stays. Since v0.22 the player shows 게임을 불러오는 중… with a moving bar while it loads, so a slow load no longer looks broken. **Still unexplained:** in Phase 5 a stall survived a Chrome restart and cleared only after clearing Chrome's HTTP cache (the cached-partial theory); a slow download doesn't explain that.
**Status.** Safeguard (v0.16): build files send `Accept-Ranges: none`, and a range request gets the whole file. Loading box (v0.22). Next steps: a load from another network (the operator's office PC is on the same network; a phone off Wi-Fi counts) to see whether outside players hit the same swings; when slow, capture the wasm request in DevTools (status, `If-None-Match`, `Range`, size received). Options: a ~60 s player watchdog that adds the Korean "reopen" line under the bar; versioned build URLs with `immutable` caching so Cloudflare's edge can keep build files (needs the gate moved off the file requests); smaller builds. Trigger: any recurrence, or the first real-student builds (classroom use). Details: [plan](../Plans/archive/gallery-load-speed.md), [phase5-log](../../.claude/memory/logs/phases/phase5-log.md) Step 22, [phase6-log](../../.claude/memory/logs/phases/phase6-log.md) Step 11.
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
