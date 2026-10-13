# Claude AI Project Rules
**Project:** daedalus (code name; working title *Student Platformer Maker*) | **Stack:** browser web app, TypeScript. Runtime and editor framework are not chosen yet (§ Project Config) | **Genre:** a 2D platformer *maker*. Students build simplified Ori / Hollow Knight-style games from ready-made parts and settings, without code.

> **Operating model.** Claude is the **developer and the keeper of the design docs**: it owns the code, the GDD (`Documents/GDD/`) and the operations ledgers (`Documents/Operations/`). **Open decisions (Roadmap §4) belong to the operator**: Claude recommends, the operator decides. **Movement feel is judged by the operator by hand**, and so is anything else a screenshot can't show. The stack is a browser app so Claude can **build, run, screenshot and verify autonomously** (headless runtime tests + Playwright). Activate the `platformer` genre profile (plus `action` for combat) in design discussions. `ux-lens` speaks for the student using the editor, since editor usability decides whether students succeed.

---

## Behavioral Rules

1. **Think Before Coding.** State assumptions. If uncertain, ask, in plain text in the response, never via the `AskUserQuestion` tool. If a simpler approach exists, say so.
2. **Simplicity First.** Minimum code. No speculative features, no abstractions for single use, no systems for mechanics that aren't in the design yet.
3. **Surgical Changes.** Don't "improve" adjacent code. Match existing style. Only remove what YOUR changes made unused.
4. **Goal-Driven Execution.** Define success criteria and loop until verified. "Verified" means tests green **and** Claude has run the app and inspected it (automated screenshot check, see Testing). Movement and combat feel additionally get an operator play-check.
5. **Plan Mode.** Enter plan mode for 3+ step tasks. Follow `Documents/Plans/plan-template.md` (read it; don't crib from sibling plans). Self-review + critic sub-agent before confirming. Save plans to `Documents/Plans/` with a descriptive filename (`tile-collision.md`), not issue-indexed. When scope is ambiguous, use `/scope` to route to patch / plan / phase.
6. **Sub-agents.** Delegate research/exploration. One task per agent. Don't duplicate their work. Never for small edits or memory writes.
7. **Self-improvement.** On correction, update `memory/lessons-learned.md`. A lesson repeated 3+ times gets promoted to CLAUDE.md.
8. **Autonomous Bug Fixing.** Investigate and fix. Only escalate if still unclear after investigation.
9. **Verify Before Done.** Prove it works before declaring done. For anything a student or player sees, that means running it and looking at it.

## Workflow Rules
- Execute phase steps sequentially without waiting for user approval. Only stop if blocked.
- All memory files belong in project `.claude/memory/`, never in global `~/.claude/`.
- **Scope guard.** The v1 boundary is the scope the GDD describes; Roadmap §3 lists what is out. Anything out of scope (swinging ropes, combos, pathfinding, bosses, scripting, rooms, dialogue, multiplayer, touch, real-time collaboration, sound editor, native builds) is not built, stubbed or "prepared for" without an operator decision. It lives in Roadmap §3.

---

## Session Start
Read `.claude/MEMORY.md` and `Documents/GDD/GDD.md` to orient.

**Phase starts:** also read:
- `Documents/Operations/Roadmap.md`: milestones, deferred features, pre-launch gates, open decisions.
- `Documents/Operations/Issues.md`: currently open bugs and tasks.
- `Documents/Operations/Deferred.md`: known cleanup / dead-code / non-blocking gaps, each with a trigger. Scan for entries whose trigger matches the work about to be done and roll the cleanup in. Scan again before *any* non-trivial change; Deferred.md is the project-wide pickup list.

## Architecture
1. **Three parts, one format.** The **project format** (shared types, validation, migrations, defaults), the **runtime** (plays a project) and the **editor** (builds one). Dependencies point one way: editor → runtime → format. The runtime never imports the editor; the format imports neither.
2. **The runtime sim is pure.** `step(state, input) → state` at a **fixed 60 Hz tick**. The sim imports nothing from the DOM, canvas, Web APIs or a UI framework, and never reads the clock or `Math.random()` directly (injected seams only). Rendering interpolates between ticks and only reads sim state. This keeps the sim headless-testable and lets the play-only page reuse it unchanged.
3. **The editor contains no gameplay logic.** Play-from-here hands the runtime a project, a start position and an ability set; the editor never re-implements collision, damage, AI or ability rules. The editor's canvas view reuses the runtime's renderer.
4. **Data-driven, versioned format.** A project is plain JSON (map, entities, settings, asset references), and the runtime reads only that JSON. `formatVersion` from day one, with a migration per bump, so every old student project keeps loading. Units in the format: tiles, tiles per second, seconds. Pixels exist only inside the runtime.
5. **System ownership and communication.** One owner per slice of state. Systems talk through explicit constructor-injected dependencies or typed events, never by reaching into each other's internals. No global singletons or service locators; module-level mutable state is a singleton too.
6. **Performance budget.** 60 fps in Chrome on a typical school laptop with ~200 entities on a ~500 × 200-tile map. Only entities within the active radius (~1.5 screens around the camera) update; tiles render from cached chunks; no per-frame allocations, closures or array-chaining in the step or render loops. `perf-guardian` holds the budget.
7. **Wiring integrity: trace BOTH ends of every decision-influencing wire.** "Produced/computed but never consumed" (and its mirror, "consumer reads a dead or wrong source") is the most recurrent defect class in long-lived systems. A wire is broken whenever a *decision / render / spawn / save / gameplay effect* needs data that isn't flowing. Ship the producer, its consumer and a test asserting the consumer is wired *in the same change*, never the producer alone. The most dangerous variant is a guard/validation check that fails OPEN on missing input. When adding a producer, grep for the *consumer* first.
8. **Students use the tool; they don't read its code.** Every student-facing setting is a slider, toggle or dropdown with a good default. Internals (coyote time, jump buffer, hit-stop, input locks) stay internal. When a feature would expose complexity, cut it.

## Coding Conventions
- **Language:** TypeScript, `strict: true`. No `any` without a comment saying why.
- **Naming:** PascalCase for types/classes/enums; camelCase for functions, methods, variables and members; UPPER_CASE for module constants. One primary export per file, and the filename matches it (`stepPlayer.ts`, `TileGrid.ts`).
- **Layout (proposed; the scaffold plan confirms it, then this line is updated):** an npm-workspaces monorepo. `packages/format/`: project types, schema validation, `formatVersion` migrations, default settings. `packages/runtime/`: `src/sim/` (pure), `src/render/`, `src/input/`. `apps/editor/`: the editor. `apps/player/`: the play-only page for shared links. `assets/default-pack/`: the bundled art. `tests/`: cross-package unit, scenario and e2e tests. `tools/`: scripts.
- **Data-driven systems:** player, enemy presets, entities and map are authored as project JSON and validated at load. Runtime objects created from data own their transient state.
- **Tunable defaults:** every default value (run speed, gravity, coyote time, attack cooldown…) lives in ONE source of truth in `packages/format/`, never duplicated as a code default elsewhere. `Documents/GDD/entities.md` mirrors the values.
- **Student projects:** a lost or unloadable student project is the worst bug this tool can have. Autosave to browser storage, explicit export/import, a versioned format with migrations, and a fixture per past `formatVersion` that must still load. A validator checks shape and invariants. It never rejects a project because a tunable default changed.
- **Events & messages:** readonly, typed event payloads for runtime lifecycle (player hurt, enemy defeated, ability unlocked, checkpoint reached, game won). Use an injected interface for a direct owner-to-owner call; use an event for one-to-many state changes.
- **Boundaries (hard rules):** Architecture rules 1–3. Third-party packages are consumed, never patched in place.
- **Error handling discipline:**
  - Expected, recoverable (missing optional asset, corrupt autosave, a project from an older format) → handle gracefully, degrade, log a warning, tell the student in plain words.
  - Unexpected / broken invariant (null where there should never be one) → fail loud in dev (assert/throw), never silently swallow.
- **Naming registry:** `Documents/GDD/Glossary.md` is the registry of canonical terms, each marked **Draft** (provisional) or **Active** (committed). Introduce a user-facing or architecture name under its real identifier, with no `Draft_` code prefix. Register it in the Glossary as Draft when provisional; flip it to Active when it stabilizes.
- **Git:** trunk-based: commit directly on `main`, no feature branches (overrides the harness "branch first" default). One commit per phase / audit pass / small task. Git LFS is configured for binary art/audio (`.gitattributes`).
- **Commit titles:** version-bumping commits (Changelog entry + `GDD.md` bump) lead with the version prefix: `vX.YY: <subject> — <desc>`, where `<subject>` is `Phase NN` / `ISS-NNN` / a short topic, never the prefix itself. Non-version commits get a plain area prefix (`docs:`, `art:`, `memory:`, `Issues.md:`, etc.). The `validate-commit-title.py` hook blocks the disallowed `ISS-NNN:` and `[phaseNN]` prefixes.

## Testing
> The test stack lands with the scaffold. Until then there is nothing to run; this section is the contract the scaffold must meet.

- **Unit + scenario:** `npm test` (Vitest). Covers the format (validation, migrations, every past-version fixture loads) and the pure runtime. **Scenario tests** drive `step()` headlessly with a scripted input sequence on a small hand-built map and assert positions and states tick by tick. They cover the collision edge cases (one-way platforms, moving platforms, ropes, wall climb, and their interactions), coyote time, jump buffering, the ability gates and the enemy AI rules. Must **fail on a zero-test run** (Vitest's default; never enable `passWithNoTests`).
- **App / e2e:** `npm run e2e` (Playwright, Chromium). Drives the editor and the player page: paint, place, play-from-here, undo/redo, save/export/import round-trip. Writes screenshots to `TestResults/`. Claude runs this itself and **looks at the screenshots** before calling visible work done.
- **Operator play-check:** for what a screenshot can't judge: movement feel, combat feel, whether a student can find a tool. Each one is documented (what to do, what to observe, what must be absent).
- **Rule:** every phase step that adds or changes behavior includes or updates a test, plus an e2e/screenshot check where it is visible. No step is verified without it.

| Change touches | Run |
|---|---|
| Docs, format defaults, pure runtime sim | `npm test` |
| Anything visible (renderer, editor, player page) | + `npm run e2e`, and inspect the screenshots |
| Render loop, active radius, chunking, entity counts | + the performance check (defined by the scaffold: 200-entity map, fps readout in Chromium) |

## Log Discipline — Write Once

Every fact has ONE home. When you finish a unit of work:

| What | Where | Shape |
|------|-------|-------|
| Version + stack | `Documents/GDD/GDD.md` header line | Edit in place when it changes. |
| Issue counter (last assigned / next free) | `Documents/Operations/Issues.md` preamble | Bump in the same commit as the new issue. |
| Full resolution narrative (what broke, what fixed, why) | `Documents/Operations/Changelog.md` | New version entry, append-only, newest on top. |
| Open issue (problem, analysis, fix options, trigger) | `Documents/Operations/Issues.md` | Rich section per issue. Delete the section when resolved. |
| Planned work + pre-launch gates + deferred features + open decisions | `Documents/Operations/Roadmap.md` | Active items only; completed phases collapse to §0's table when their work closes. Delete resolved sections. |
| Deliberately deferred cleanup / dead code | `Documents/Operations/Deferred.md` | New entry per item with Source / Severity / Description / Why deferred / Trigger. Delete on resolution. |
| Session-to-session index | `.claude/MEMORY.md` | Pure link index. `git log` is the session history. |
| Per-issue analysis beyond what fits inline | `.claude/memory/logs/issues/issNNN-<name>.md` | Only when the issue needs root-cause detail / fix-option tradeoffs. |
| Per-phase implementation log | `.claude/memory/logs/phases/phaseN-log.md` | One log per completed phase. |
| Design/mechanic learned or tuned | `Documents/GDD/` (systems/entities/…) | The GDD is the design source of truth. Update it when a mechanic changes, bump the version. |
| Architectural rule learned | `.claude/memory/lessons-learned.md` | One-line rule + reason. |
| Architectural or Open-decision outcome (ADR) | `.claude/memory/decisions.md` | ADR row. |
| Browser / library gotcha | `.claude/memory/framework.md` | One short section per subsystem. |
| Audit findings + dispositions | `.claude/memory/audit-log.md` | Populated by `/audit`. |

**Forbidden patterns:**
- Duplicating Changelog postmortem text in Issues.md sections. Issues.md is for OPEN issues; once resolved, delete the section.
- Keeping "original analysis preserved below for audit trail" in resolved issue rows. The audit trail is Changelog + git history.
- Leaving `~~strikethrough~~` prose in Roadmap. Resolved items are deleted.
- Filing the same item in both Issues.md and Deferred.md. Pick by impact: Issues.md = incorrect behavior right now; Deferred.md = incomplete but works.
- Adding a Deferred.md entry without a concrete trigger condition.

**Session-end writes (in the same commit as the work):**
- **Resolution session:** Changelog.md new version + GDD.md version bump + Issues.md (delete resolved section) + Roadmap.md if triggers changed + Deferred.md if a deferred entry's trigger fired.
- **Filing-only session (new bug found, no fix):** Issues.md (bump counter + new section).
- **Shipping work with a known leftover:** Deferred.md (new entry naming Source / Severity / Description / Why deferred / Trigger).
- **Design change or Open decision settled:** update the affected `Documents/GDD/` file in the same commit, bump the GDD version, add the ADR row to `decisions.md`, and remove the item from Roadmap §4.
- **Rule learned:** append to `memory/lessons-learned.md` (one line + reason).

Skipping is a workflow violation.

## Project Config
- **Stack:** **not chosen yet.** Browser, TypeScript. The runtime (custom Canvas/WebGL vs Phaser 3) is decided by a one-day spike; the editor UI framework (React recommended) is an Open decision (Roadmap §4). When chosen, list exact pins here (no `^`/`~` in `package.json`).
- **Tooling Node:** **24.14.1** (LTS, `.nvmrc`), which runs npm, Vite, Vitest and Playwright. The product itself runs in the browser.
- **Target platform:** desktop Chrome on a typical school laptop; keyboard + gamepad (Gamepad API). No mobile/touch in v1.
- **Networking:** none in the editor or runtime. Shared links need a small hosting backend; the choice is an Open decision (Roadmap §4) to settle before the `assets-and-sharing` milestone. Student accounts: none in v1 unless decided otherwise.
- **Working directories:** see Coding Conventions → Layout. Do not create other top-level directories without asking.
- **Art:** a default art pack ships with the tool (CC0 recommended, Open decision). Every third-party pack in `assets/` carries its license file beside it.
- **Handshake review root:** `Documents/Review/` overrides the portable default of `handshake/` at the repo root (see `.claude/skills/handshake/protocol.md`). Created per handshake; nothing persists there between runs.
- **Read-only directories** (do not edit): `node_modules/`, and any vendored third-party code (list each one here when it lands).
- **Build / run:** defined by the scaffold plan; list every command here when it lands. Claude runs all of them.
- **Version control:** GitHub remote `origin`. Git LFS tracked types: see `.gitattributes`.

## Agents & Skills

| Agent | Purpose |
|-------|---------|
| `.claude/agents/workflow/codebase-scan.md` | Find existing code related to GDD changes |
| `.claude/agents/workflow/phase-planner.md` | Synthesize GDD changes + scan into phase prompt |
| `.claude/agents/workflow/code-review.md` | Code integrity, conventions, GDD alignment |
| `.claude/agents/workflow/design-review.md` | Multi-perspective game-design review |

**Specialist agents by role:**
- `.claude/agents/panel/`: core advisors (systems-thinker, fun-advocate, player-voice, emotionalist, feasibility-check, skeptic, simplifier)
- `.claude/agents/vision/`: direction & strategy (creative-director, market-strategist, community-mind, pitch-crafter)
- `.claude/agents/craft/`: execution quality (juice-artist, level-crafter, sound-architect, balance-tuner, ux-lens, perf-guardian)
- `.claude/agents/narrative/`: story pipeline (worldbuilder, plot-architect, prose-writer, story-critic, continuity-keeper). Little use here: v1 has no dialogue or story systems.
- `.claude/agents/genres/`: 14 genre profiles; **activate `platformer` (+ `action` for combat)** for daedalus

**How to use:** panel agents join every design discussion. Vision agents join when shaping direction. Craft agents join when reviewing feel and execution. For this project, `player-voice` and `ux-lens` speak for **two players**: the student building in the editor and the classmate playing the result.

**Discovery rule:** glob the relevant subfolder and read agent descriptions to find the right specialist. Don't guess from memory; check the files.

| Skill | Purpose |
|-------|---------|
| `/scope [work]` | Decide patch / plan / phase based on blast radius, then execute or delegate |
| `/new-phase [N] [name]` | Extract session decisions → phase prompt (invoked by `/scope` for phase-sized work) |
| `/review [files]` | Code review |
| `/design [topic]` | Design panel review |
| `/update [scope]` | Sync stale GDD references |
| `/audit [focus]` | Full project health check: GDD sync, architecture, memory, pending work |
| `/handshake [doc]` | Two-agent document review with Claude (Cowork), iterated to approval |

**Subagent rules:** use for parallel/complex work only. Never for small edits or memory writes.
