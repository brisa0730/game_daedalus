# Issues

Open issues only. Resolved issues live in [Changelog.md](Changelog.md) with full postmortems — git history preserves any prior states of this file.

**Lifecycle.** Add a row when a new issue is found. Delete the row when resolved (write the Changelog entry in the same commit). Do NOT annotate with "original preserved for audit trail" — Changelog + git is the audit trail.

**Issue numbering.** Monotonic, never recycled. When filing a new issue, use `ISS-NNN` where NNN is one greater than the counter below. Closed issues are deleted from this file but their numbers stay retired. Bump the counter in the same commit as the new issue.

**Last issue assigned:** ISS-001 · **Next free:** ISS-002

**Severity:** HIGH (safety/correctness regression), MED (functional gap or structural drift), LOW (perf/convention/cosmetic).

---

## Open (1)

## ISS-001 — WebGL builds draw nothing: URP's resource shaders are null in the player
**Severity:** HIGH · **Status:** OPEN — filed 2026-11-02 · **Introduced:** v0.06 (scaffold), unnoticed until the `class-gallery` spike
**Summary.** A release or development WebGL build (6000.6.7f1, URP 17.6.0, 2D Renderer) logs `Cannot create required material because shader is null`, `Cannot initialize a LocalKeyword with a null Shader.` and a `NullReferenceException` every frame, and draws nothing: the canvas shows only the template background. The game still runs (scripts, `PlayerPrefs`, IMGUI). Seen in the trap bench spike builds and in the perf check (`TestResults/perf-check.png` shows only its IMGUI text, uncleared), so **the perf check's gate passes on empty frames**: it fails open. Every shared student game would be a blank screen.
**Status.** Investigated on the Mac Mini, not fixed.
- A native macOS player of the same scene logs none of these errors, so the fault is WebGL-only.
- URP's settings-stripping report for the build keeps `Renderer2DResources` and `UniversalRenderPipelineRuntimeShaders`, and their shader GUIDs resolve in the URP package, so the settings reach the player.
- The shaders they reference (`Hidden/Universal/CoreBlit`, `Hidden/Light2D`, …) did not appear among the shaders compiled in the cold build. Loading the global settings asset before `BuildPlayer` didn't change that.
- Not yet known: whether a WebGL build from the Editor UI, a WebGL build on Windows, or a 6000.0.85f1 WebGL build draws. 6000.0.85f1 on the Mac Mini has no WebGL module.
- **Fix includes:** the perf check must fail when nothing is drawn, for example by checking the screenshot isn't a single colour.
**Related:** [class gallery spike](../Plans/archive/class-gallery-spike.md) § Findings.

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
