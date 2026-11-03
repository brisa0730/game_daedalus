# Issues

Open issues only. Resolved issues live in [Changelog.md](Changelog.md) with full postmortems — git history preserves any prior states of this file.

**Lifecycle.** Add a row when a new issue is found. Delete the row when resolved (write the Changelog entry in the same commit). Do NOT annotate with "original preserved for audit trail" — Changelog + git is the audit trail.

**Issue numbering.** Monotonic, never recycled. When filing a new issue, use `ISS-NNN` where NNN is one greater than the counter below. Closed issues are deleted from this file but their numbers stay retired. Bump the counter in the same commit as the new issue.

**Last issue assigned:** ISS-002 · **Next free:** ISS-003

**Severity:** HIGH (safety/correctness regression), MED (functional gap or structural drift), LOW (perf/convention/cosmetic).

---

## Open (1)

## ISS-002 — Class-gallery games loaded slowly (Cloudflare served Korea from LAX); school check pending
**Severity:** MED · **Status:** FIX SHIPPED v0.23, open until the school check · **Introduced:** Phase 5 `gallery-server` (v0.16)
**Summary.** Cloudflare serves our zone from **LAX** on the office's KT line (`colo=LAX`, ~258 ms; cloudflare.com itself is ICN), so every build file crossed the Pacific twice: a 7.3 MB `game.wasm.br` took 2–162 s and games 10–107 s to start, slow from the school network too. HTTP/2 didn't help (v0.22). Since v0.23 the four build files load from a Naver Object Storage bucket in Korea: on the live gallery Unity starts in 2.5–4 s from the Mac, ~2 s on a phone over mobile data (from the bucket test page). The v0.22 loading box stays. **Still unexplained:** the Phase 5 stall that survived a Chrome restart and cleared only with Chrome's HTTP cache; bucket files are now versioned and `immutable`, and the bucket answers `Range` with 206, so the old `Accept-Ranges: none` safeguard covers only the Mac fallback.
**Status.** Resolve when the operator opens gallery games from the school network (back ~2026-11-11) and they start within 15 s; then write the Changelog close-out and delete this section. If a school filter blocks `daedalus.kr.object.ncloudstorage.com`, games fall back to the Mac (slow): record it here. Details: [plan](../Plans/archive/gallery-naver-storage.md), Changelog v0.22–v0.23.
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
