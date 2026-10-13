---
name: audit
description: Full project health audit — system-by-system GDD/project comparison, architecture compliance, memory freshness, and pending work.
disable-model-invocation: false
model: opus
effort: xhigh
argument-hint: "[optional: module cluster to narrow the audit to]"
---

# Project Audit: $ARGUMENTS

> **daedalus note.** Browser web app, TypeScript. Shared project format in `packages/format/`, the pure game runtime (`step(state, input)`) in `packages/runtime/src/sim/`, its renderer and input in `packages/runtime/src/render/` and `src/input/`, the editor in `apps/editor/`, the play-only page in `apps/player/`. Paths follow CLAUDE.md § Coding Conventions → Layout; if the scaffold changed them, CLAUDE.md wins — update the clusters below to match. Until the first phases land, most clusters will report `UNIMPL` against the GDD — that is expected, not a defect. A cluster whose directories don't exist yet is reported as not-yet-implemented.

Run a comprehensive health check by reading the actual project and comparing it against GDD definitions system by system. If `$ARGUMENTS` is provided, run only the matching agent(s) in Section 2 and narrow Sections 3–5 to that area.

## Workflow

### 0. Audit Log Setup
Read `.claude/memory/audit-log.md`. If it doesn't exist, create it with a `# Audit Log` header. Determine the next audit number (increment the highest existing `## Audit #N`). This number tags all findings in this run.

### 1. State Check
Read these in parallel and extract current state:
- `.claude/MEMORY.md` — index
- `Documents/GDD/GDD.md` — version + engine
- `Documents/Operations/Issues.md` — open-issue sections (count by severity)
- `Documents/Operations/Changelog.md` — latest version narrative

### 2. System-by-System Audit

**This is the core section.** Do NOT rely on grep-based pattern matching alone. Agents must **read actual source files** and compare against GDD definitions.

Spawn **parallel Explore agents** (in focus mode, only the agent(s) covering `$ARGUMENTS`). Each covers a module cluster defined by **directory paths** (stable boundaries), discovers what systems exist within those paths, and audits each one.

#### Discovery Process (every agent does this first)

1. **Glob** all source files in the assigned directories (excluding generated/vendored files).
2. **Read** the relevant GDD files for the cluster to learn what systems SHOULD exist (class names, method signatures, specific values).
3. **Read** `Documents/GDD/architecture.md` — the section(s) covering the cluster — to learn what's been DOCUMENTED.
4. **Cross-reference** all three sources:
   - In code + in GDD + in architecture.md → audit normally
   - In GDD but NOT in code → flag **UNIMPL**
   - In code but NOT in GDD → flag **UNDOC**
   - In architecture.md but not in code → flag **STALE** GDD

#### Per-System Checklist (applied to every discovered system)

1. Read every source file in the system (not just entry points).
2. For each class: verify constructor params, public method signatures, and return types match the GDD.
3. When the GDD states a specific value (threshold, count, mode, cooldown, buffer size), verify the exact value in code.
4. For each repo consumed: verify it returns typed objects, not untyped records.
5. Check error handling: expected/recoverable → degrade gracefully and log a warning; broken invariant → throw (never silently return a neutral default).
6. Check dependency injection: no global singletons, no module-level mutable state.
7. Check data boundaries: no untyped records in public returns that cross module boundaries.
8. Check import rules per `CLAUDE.md` (the project's import-boundary contract).
9. Check caches: every cache has an explicit bound — no unbounded maps.
10. Check logging per CLAUDE.md (structured, mandatory fields present, no bare prints).
11. Check inter-module communication: modules talk only through defined interfaces / shared infrastructure — no direct imports from another module's internals.

#### Agent Assignments (by directory path)

Clusters are keyed to stable directory boundaries under `packages/` and `apps/`. Skip a cluster whose directories don't exist yet (report it as not-yet-implemented).

**Agent A — Project Format & Data:**
- Directories: `packages/format/` (project JSON types, schema validation, `formatVersion` migrations, default settings), `assets/default-pack/` metadata
- GDD refs: `architecture.md`, `entities.md`, `conventions.md`
- Special focus: default player / enemy values vs `entities.md` tables (values must match; ONE source of truth, no code defaults duplicating them); every project validated at load; every past `formatVersion` has a migration and a fixture that still loads; units in tiles / tiles-per-second / seconds in the format, pixels only inside the runtime.

**Agent B — Runtime Simulation:**
- Directories: `packages/runtime/src/sim/`
- GDD refs: `systems.md` (Player, Combat, Enemies, World & Level Elements, Camera)
- Special focus: purity (no DOM, `window`, `document`, canvas, Web APIs or editor imports); determinism (fixed 60 Hz step, no `Date.now()` / `performance.now()` / `Math.random()` inside the sim — injected seams only); the step advances by fixed ticks, never by render frame; collision edge cases (one-way, moving platforms, ropes, wall climb) covered by headless scenario tests; values verified against `entities.md`.

**Agent C — Rendering, Input & Player Page:**
- Directories: `packages/runtime/src/render/`, `packages/runtime/src/input/`, `apps/player/`
- GDD refs: `systems.md` (Camera, Presentation), `platforms.md` (input, performance), `content.md`
- Special focus: rendering reads sim state and never mutates it; interpolated rendering between fixed ticks; chunked tile rendering and the active radius; no per-frame allocations in the render loop; keyboard + Gamepad API mapped to one `Input` shape; the player page loads a project and runs it without any editor code.

**Agent D — Editor & Wiring:**
- Directories: `apps/editor/`, entry points
- GDD refs: `systems.md` (Editor, Tile System, Asset Import, Share), `architecture.md`, `conventions.md`
- Special focus: **the editor contains no gameplay logic** — play-from-here hands a project + start position + ability set to the runtime; every edit goes through the command stack (undo/redo); autosave and export/import round-trip a project byte-for-byte; validation warnings cover the GDD list.
- **Inter-module wiring verification** (this agent's unique responsibility):
  - Read the entry points end-to-end and trace the dependency-injection graph: verify every module receives correct dependencies via constructor.
  - Check that no module is wired to another module's internals — only to interfaces or shared infrastructure.
  - Verify data-flow direction matches architecture.md (editor → format → runtime; runtime never imports editor); flag backward or circular dependencies not documented there.
  - Verify every class claiming to implement an interface actually satisfies it.
  - Confirm the shared-infrastructure components in architecture.md (project format, the runtime's public API) are the *only* cross-package channels.
  - Every producer has a consumer, and every decision-influencing value reaches the code that needs it (CLAUDE.md Architecture rule 7).

**Each agent reports per-system:** `[OK/DRIFT/VIOLATION/UNIMPL/UNDOC] SystemName — detail (file:line)`

### 3. Cross-Cutting Checks
Run these via Grep in parallel (fast pattern-based, complements the deep audit), scoped to `packages/` and `apps/`:
- `packages/runtime/src/sim/` importing anything from `render/`, `input/`, `apps/`, a UI framework, or touching `document` / `window` / `navigator` / `localStorage` / `Date.now` / `performance.now` / `Math.random` — purity violation.
- `packages/runtime/` or `packages/format/` importing from `apps/` — dependency-direction violation.
- Gameplay rules (collision, damage, AI, ability checks) implemented under `apps/editor/` — the editor must call the runtime instead.
- `any` without an explaining comment.
- Allocations (`new`, object/array literals), closures or array-chaining (`.map(` / `.filter(` / `.forEach(`) inside per-tick sim paths or the per-frame render loop — should be zero.
- Performance regressions against the GDD budget (60 fps, ~200 entities, school laptop): entities updated outside the active radius, tiles redrawn per frame instead of per chunk — flag each.
- `console.log` in `packages/` or `apps/` outside tests.
- `passWithNoTests` anywhere — must be absent.
- `TODO|FIXME|HACK|XXX` in source.
- Direct edits under the read-only dirs listed in CLAUDE.md (`node_modules/`, vendored third-party code) — should be zero in the diff.

### 4. Memory Freshness
- Does `GDD.md` version match `Changelog.md`'s latest version entry?
- Does the Open table header at the top of `Issues.md` match the actual `## ISS-NNN` sections below (count by severity)?
- Does `architecture.md` reflect all systems that exist in code?
- Are there Roadmap/Deferred items older than 30 days that should be promoted or dropped?
- Does `lessons-learned.md` have lessons that should be promoted (3+ occurrences)?
- Does `decisions.md` contain decisions contradicted by current code?
- Does `feedback.md` reference patterns or files that no longer exist?

### 5. Pending Work
- Deferred items whose triggers are now met
- Open issues by severity
- TODOs/FIXMEs from cross-cutting checks

## Output Format

```
# Project Audit Report

## Summary
Phase: [N] — [status]
GDD: v[current] (last reviewed: v[X]) [UP TO DATE / BEHIND BY N VERSIONS]
Active issues: [count] ([H] HIGH, [M] MED, [L] LOW)
Tests: [count]
Health: [GOOD / NEEDS ATTENTION / ACTION REQUIRED]

## System-by-System Findings

### Foundation & Data
| Status | System | Detail |
|--------|--------|--------|
| OK/DRIFT/VIOLATION | [system] | [finding with file:line] |

### Simulation & Gameplay
...

### Presentation
...

### Platform & Wiring
...

### Inter-Module Wiring
| Status | Check | Detail |
|--------|-------|--------|
| OK/VIOLATION | Dependency injection graph | [finding] |
| OK/VIOLATION | Protocol compliance | [finding] |
| OK/VIOLATION | Data flow direction | [finding] |
| OK/VIOLATION | Shared infra channels | [finding] |

## Cross-Cutting Violations
- [SEVERITY] [pattern] — [count] violations: [file:line list]
(Omit patterns with zero violations)

## Memory Health
- [STALE/OK] [file] — [detail]

## Pending Work
- [N] deferred items ready for pickup
- [N] GDD versions behind
- [N] open issues (by severity)
- [N] Notes to GDD pending
- [N] TODOs/FIXMEs in code

## Recommended Next Actions
1. [Most urgent]
2. [Second priority]
3. [Third priority]
```

Keep findings concise — specific file:line references, not explanations of obvious fixes.

## Audit Log

After presenting the report, write all findings to `.claude/memory/audit-log.md`:

```
## Audit #N — YYYY-MM-DD [full / focus: area]

### Findings
- [A#N.1] SEVERITY | Category | Detail (file:line)
- [A#N.2] SEVERITY | Category | Detail (file:line)
...

### Fixes
(pending user approval)
```

- **Finding IDs** use format `A#N.X` (N = audit number, X = sequential finding).
- **Category** is one of: DRIFT, VIOLATION, STALE, UNIMPL, UNDOC, WIRING.
- Only record actual problems — don't log OK systems.
- Present the report to the user first. **Do not fix anything until the user says to proceed.**

### Recording Fixes

When the user approves fixes, update the same audit entry's `### Fixes` section as each finding is addressed:

```
### Fixes
- [A#N.1] FIXED — description of what was changed
- [A#N.2] DEFERRED — reason (e.g., "needs design decision", "target *<phase name>*" — name the phase, never a forward number)
```

Mark every finding FIXED or DEFERRED before closing the audit. Commit policy: one commit for the audit fixes with message `[audit] Audit #N fixes`.
