# Claude AI Project Rules
**Project:** daedalus (code name; working title *Student Platformer Maker*) | **Stack:** Unity 6 package `com.orcapafea.daedalus`, C#: a custom kinematic sim on Unity's Tilemap and sprite rendering; students build inside the Unity Editor (§ Project Config) | **Genre:** a 2D platformer *maker*. Students build simplified Ori / Hollow Knight-style games in the Unity Editor from ready-made parts and settings, without code.

> **Operating model.** Claude is the **developer and the keeper of the design docs**: it owns the code, the GDD (`Documents/GDD/`) and the operations ledgers (`Documents/Operations/`). **Open decisions (Roadmap §4) belong to the operator**: Claude recommends, the operator decides. **Movement feel is judged by the operator by hand**, and so is anything else a screenshot can't show. Claude **builds and verifies from the command line**: Unity in batch mode runs the EditMode and PlayMode suites in both supported editors, and PlayMode tests capture screenshots. What happens in the Unity Editor UI (inspectors, Scene-view tools, play-from-here) is checked by the operator unless a test can drive it. Activate the `platformer` genre profile (plus `action` for combat) in design discussions. `ux-lens` speaks for the student working in the Unity Editor with our tools, since that usability decides whether students succeed. Learning the Unity Editor is a goal; reading code is not.

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
1. **Three parts, one package.** The **sim** (pure C#: player, collision, combat, enemies, level elements, the tunable defaults), the **runtime** (Unity glue: reads the scene's Tilemaps and Daedalus components into the sim, runs it, drives sprites, camera, input and saves) and the **editor tools** (custom inspectors, the daedalus window, play-from-here, validation). Each is its own assembly; references point one way: editor tools → runtime → sim. The sim references nothing; the runtime never references the editor assembly.
2. **The sim is pure.** `Step(state, input)` at a **fixed 60 Hz tick**. The sim assembly has `noEngineReferences: true` (no `UnityEngine`, no `UnityEditor`) and never reads the clock or a random source directly (injected seams only). The runtime interpolates between ticks and only reads sim state. This keeps the sim testable in EditMode with no scene and makes the WebGL build run the same rules.
3. **The editor tools contain no gameplay logic.** Play-from-here hands the runtime a start position and an ability set and enters Play mode; editor code never re-implements collision, damage, AI or ability rules. Scene-view gizmos read the same settings the runtime uses.
4. **Authored data survives every update.** A student's game is a Unity scene (Tilemaps + GameObjects carrying our components, often saved as the student's own prefabs) plus our assets (terrains, animation sets). Students receive package updates, so an update must never break or silently change a saved project: renamed serialized fields keep `[FormerlySerializedAs]`, structural changes get an upgrade step keyed by a data version on our assets and components, and a fixture project per released package version must still open, upgrade and play. Units: **1 Unity unit = 1 tile**; speeds in tiles per second; times in seconds. Pixels appear only in sprite import settings (16 pixels per unit).
5. **System ownership and communication.** One owner per slice of state. Systems talk through explicit constructor- or Inspector-injected dependencies or typed events, never by reaching into each other's internals. No global singletons or service locators (static instances, `FindObjectOfType`-style lookups); static mutable state is a singleton too.
6. **Performance budget.** 60 fps on a typical school laptop, both in the WebGL build in Chrome and in the Editor's Play mode, with ~200 entities on a ~500 × 200-tile map. Only entities within the active radius (~1.5 screens around the camera) update; Tilemaps render in Chunk mode; no per-frame allocations, LINQ, closures, `GetComponent` or `Find` calls in the step or per-frame loops. `perf-guardian` holds the budget.
7. **Wiring integrity: trace BOTH ends of every decision-influencing wire.** "Produced/computed but never consumed" (and its mirror, "consumer reads a dead or wrong source") is the most recurrent defect class in long-lived systems. A wire is broken whenever a *decision / render / spawn / save / gameplay effect* needs data that isn't flowing. Ship the producer, its consumer and a test asserting the consumer is wired *in the same change*, never the producer alone. The most dangerous variant is a guard/validation check that fails OPEN on missing input. When adding a producer, grep for the *consumer* first.
8. **Students use the tool; they don't read its code.** Students build by placing objects, adding our components with Add Component, and tuning them. One component makes a thing work: it needs nothing else from Unity (no colliders, Rigidbodies or physics layers). Each component shows **at most ~8 settings** (the Player: at most ~8 per collapsible section), and a setting that applies to one mode only (e.g. a flyer's wall behavior) shows only in that mode. Every student-facing setting is an Inspector slider, toggle or dropdown with a good default and a tooltip. Internals (coyote time, jump buffer, hit-stop, input locks) stay hidden from the Inspector. Students never need to open a script. When a feature would expose complexity, cut it.

## Coding Conventions
- **Language:** C#, as Unity 6000.0 supports it. The package must compile without warnings on 6000.0 (the minimum); an API added after 6.0 needs a version define and a 6000.0 fallback.
- **Naming:** PascalCase for types, methods, properties, events and constants; camelCase for fields, locals and parameters (serialized private fields use `[SerializeField]`, no prefix). One primary type per file, and the filename matches it (`PlayerStepper.cs`, `TileGrid.cs`); Unity requires this for MonoBehaviours and ScriptableObjects. Namespaces: `Orcapafea.Daedalus.Sim`, `Orcapafea.Daedalus`, `Orcapafea.Daedalus.Editor`.
- **Layout (proposed; the scaffold plan confirms it, then this line is updated):** the repo root is the dev Unity project (6000.6.7f1). `Packages/com.orcapafea.daedalus/` is the package: `Runtime/Sim/` (asmdef `Orcapafea.Daedalus.Sim`, no engine references), `Runtime/` (`Orcapafea.Daedalus`), `Editor/` (`Orcapafea.Daedalus.Editor`), `Tests/EditMode/`, `Tests/PlayMode/`, `Samples~/` (the starter: scene, example prefabs and the default art pack, imported from Package Manager). `Assets/`: dev-only test scenes, never shipped. `ProjectSettings/`. `Tools/`: scripts (batch-mode test and build wrappers).
- **Data-driven systems:** the player, enemies and level elements are GameObjects with Daedalus components; terrains and animation sets are assets; the map is three Tilemaps. At play start the runtime converts them into sim data and validates them. Runtime objects own their transient state, and runtime code never writes to authored data. Play mode behaves as Unity's always does: edits made during it are thrown away when it ends, and students learn that.
- **Tunable defaults:** every default value (run speed, gravity, coyote time, attack cooldown…) lives in ONE source of truth in the sim assembly, and the components take their initial values from it; never duplicated as a code default elsewhere. `Documents/GDD/entities.md` mirrors the values.
- **Student projects:** a lost or broken student project is the worst bug this tool can have. Scene autosave from the editor tools (Unity does not autosave scenes), update safety per Architecture rule 4, and a fixture project per released package version that must still open. Validation warns; it never rejects a project because a tunable default changed.
- **Events & messages:** readonly, typed event payloads for runtime lifecycle (player hurt, enemy defeated, ability unlocked, checkpoint reached, game won). Use an injected interface for a direct owner-to-owner call; use an event for one-to-many state changes.
- **Boundaries (hard rules):** Architecture rules 1–3, enforced by assembly references. Third-party packages are consumed, never patched in place (no embedding a Unity package to edit it).
- **Error handling discipline:**
  - Expected, recoverable (a missing sprite or optional asset, data from an older package version) → handle gracefully, degrade, `Debug.LogWarning`, and tell the student in plain words (an Inspector help box or the daedalus window).
  - Unexpected / broken invariant (null where there should never be one) → fail loud in dev (assert/throw), never silently swallow.
- **Naming registry:** `Documents/GDD/Glossary.md` is the registry of canonical terms, each marked **Draft** (provisional) or **Active** (committed). Introduce a user-facing or architecture name under its real identifier, with no `Draft_` code prefix. Register it in the Glossary as Draft when provisional; flip it to Active when it stabilizes.
- **Git:** trunk-based: commit directly on `main`, no feature branches (overrides the harness "branch first" default). One commit per phase / audit pass / small task. Git LFS is configured for binary art/audio (`.gitattributes`). Unity serializes assets as text with visible `.meta` files; every asset is committed with its `.meta`.
- **Commit titles:** version-bumping commits (Changelog entry + `GDD.md` bump) lead with the version prefix: `vX.YY: <subject> — <desc>`, where `<subject>` is `Phase NN` / `ISS-NNN` / a short topic, never the prefix itself. Non-version commits get a plain area prefix (`docs:`, `art:`, `memory:`, `Issues.md:`, etc.). The `validate-commit-title.py` hook blocks the disallowed `ISS-NNN:` and `[phaseNN]` prefixes.

## Testing
> The test stack lands with the scaffold. Until then there is nothing to run; this section is the contract the scaffold must meet.

- **EditMode (unit + scenario):** Unity Test Framework (NUnit), run in batch mode. Covers the sim and the data upgrade path. **Scenario tests** drive `Step()` with a scripted input sequence on a small hand-built map and assert positions and states tick by tick. They cover the collision edge cases (one-way platforms, moving platforms, ropes, wall climb, and their interactions), coyote time, jump buffering, the ability gates and the enemy AI rules. A run with zero tests must **fail** (the wrapper checks the result count).
- **PlayMode:** batch mode with graphics (never `-nographics`) loads test scenes, exercises the runtime glue (Tilemap → sim, input, play-from-here entry, checkpoint saves) and captures screenshots to `TestResults/`. Claude **looks at the screenshots** before calling visible work done.
- **Both editors:** every suite runs on **6000.0.85f1** (the minimum) and **6000.6.7f1** (dev).
- **Guards** (EditMode): the sim asmdef has no references and `noEngineReferences: true`; a scan of the sim source for clock, random, `UnityEngine` and `UnityEditor` use; the asmdef reference direction (editor tools → runtime → sim).
- **Operator editor check:** for what batch mode can't show: inspectors, Scene-view tools, Tile Palette use. Documented like a play-check.
- **Operator play-check:** for what a screenshot can't judge: movement feel, combat feel, whether a student can find a tool. Each one is documented (what to do, what to observe, what must be absent).
- **Rule:** every phase step that adds or changes behavior includes or updates a test, plus a PlayMode screenshot check where it is visible. No step is verified without it.

| Change touches | Run |
|---|---|
| Docs, tunable defaults, pure sim | EditMode suite, both editors |
| Runtime glue, anything visible in Play mode | + PlayMode suite, and inspect the screenshots |
| Editor tools, inspectors | + an operator editor check |
| Active radius, Tilemap rendering, entity counts | + the performance check (defined by the scaffold: a WebGL build with 200 entities, fps read in Chrome) |

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
| Unity / package gotcha | `.claude/memory/framework.md` | One short section per subsystem. |
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
- **Stack:** Unity 6, C#, shipped as the UPM package **`com.orcapafea.daedalus`** (`"unity": "6000.0"` minimum). A **custom kinematic sim** (own tile collision on the Collision Tilemap's grid, pure `Step()`; no Unity 2D physics for gameplay), Unity's Tilemap and SpriteRenderer for drawing, the **URP 2D Renderer**, the **Input System**, **2D Tilemap Extras** for rule, random and animated tiles. Exact package dependency versions land with the scaffold and are listed here.
- **Editors:** develop on **6000.6.7f1**; every test also runs on **6000.0.85f1**, the minimum. Both installed through Unity Hub (`C:\Program Files\Unity\Hub\Editor\<version>`).
- **Distribution:** students install from **OpenUPM** (a scoped registry added once per project; Package Manager's Update button; no Git needed on student machines). Development installs use the git URL `https://github.com/brisa0730/game_daedalus.git?path=/Packages/com.orcapafea.daedalus`. Releases are semver git tags on the public GitHub repo.
- **Target platform:** students run the Unity Editor on their school laptops (the operator confirmed the hardware). Finished games are WebGL builds played in desktop Chrome. Keyboard + gamepad (Input System). No mobile/touch in v1.
- **Networking:** none in the editor or runtime. Sharing WebGL builds needs hosting; the choice is an Open decision (Roadmap §4) to settle before the `assets-and-sharing` milestone. Student accounts: none of ours in v1 (the Unity account students need is Unity's).
- **Working directories:** see Coding Conventions → Layout. Do not create other top-level directories without asking.
- **Art:** a default art pack ships with the tool (a CC0 pack to start), at 16 px tiles. It ships in the package's `Samples~/`; every third-party pack carries its license file beside it.
- **Handshake review root:** `Documents/Review/` overrides the portable default of `handshake/` at the repo root (see `.claude/skills/handshake/protocol.md`). Created per handshake; nothing persists there between runs.
- **Read-only directories** (do not edit): `Library/` (including the package cache), `Temp/`, `Logs/`, `UserSettings/`, and any vendored third-party code (list each one here when it lands).
- **Build / run:** defined by the scaffold plan (batch-mode test wrappers per editor, the WebGL build); list every command here when it lands. Claude runs all of them.
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
