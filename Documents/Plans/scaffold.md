# Plan: Scaffold

**Filename:** `scaffold.md`

**Date:** 2026-10-21 | **Status:** approved by handshake (2 rounds, `Documents/Review/archive/scaffold/`); awaiting operator go

## Overview

**Where things stand:** The design is settled for Unity, but there is no Unity project, no package and no way to run tests. Nothing can be built or checked yet.

**What this plan changes:**
- A Unity project for development, made the same way a student's 2D project is made, with our package inside.
- The package's shell: three code parts that Unity itself keeps apart, so the game rules can never reach into Unity or the editor.
- One command that runs every test, without opening Unity, in the oldest and newest supported Unity 6, and fails if no tests ran.
- A saved in-game picture from the tests, plus a speed test on a full-size map with 200 moving things in Chrome.
- Proof that a fresh project in the oldest Unity 6 can install the package from GitHub.

**How you'll know it worked:** The test command prints a green summary for both Unity versions, the saved pictures show a small tile scene, and the speed test reports smooth frames in Chrome.

**Not in this plan:** the player or any movement. Only the game clock arrives now.

**Why a plan and not a patch:** it changes the project rules and creates a project, a package and tools, each with its own way to fail.

**Design choice:** the oldest-Unity check runs in a separate throwaway project that points at the package, instead of opening the dev project in an older Unity, which would try to downgrade it.

---

## Source
- **Trigger:** Roadmap §1 Milestone 0 (`scaffold`); operator: "editors are ready. let's begin", routed via `/scope` → plan (rule 2: CLAUDE.md change; also rules 3–4).
- **Related issues:** none
- **Scope:** dev Unity project, package skeleton with guards, batch-mode test wrapper for both editors, PlayMode screenshot, WebGL performance check, git-URL install proof; CLAUDE.md § Project Config filled.
- **Depends on:** GDD v0.05; Unity 6000.0.85f1 and 6000.6.7f1 with WebGL support (verified); Python 3.14, Git + LFS, Chrome (verified).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `Assets/Settings/*`, `Assets/Scenes/SampleScene.unity`, `ProjectSettings/*`, `Packages/manifest.json`, `Packages/packages-lock.json` | Dev project cloned from the 6000.6.7f1 2D template (URP 2D, Input System, Force Text) |
| NEW | `Packages/com.orcapafea.daedalus/package.json`, `README.md` | Package manifest (`"unity": "6000.0"`) |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/Orcapafea.Daedalus.Sim.asmdef`, `FixedStepAccumulator.cs` | Pure sim assembly (`noEngineReferences`, no references) + the GDD's fixed 60 Hz time model |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Orcapafea.Daedalus.asmdef`, `Runtime/AssemblyInfo.cs` | Runtime assembly → Sim |
| NEW | `Packages/com.orcapafea.daedalus/Editor/Orcapafea.Daedalus.Editor.asmdef`, `Editor/AssemblyInfo.cs` | Editor-tools assembly → Runtime (Editor platform only) |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/*` | Test asmdef; `FixedStepAccumulatorTests`, `SimPurityTests`, `AssemblyDirectionTests`, `PackageManifestTests` |
| NEW | `Packages/com.orcapafea.daedalus/Tests/PlayMode/*` | Test asmdef; `RenderSmokeTests` (screenshot) |
| NEW | `Assets/Dev/PerfCheck/PerfCheck.cs`, `Assets/Dev/PerfCheck/PerfCheck.unity`, `Assets/Dev/Editor/PerfCheckBuild.cs` | Dev-only WebGL performance harness, its scene, build entry point |
| NEW | `Tools/unity_tests.py` | Batch-mode test wrapper (both editors, zero-test + warning failure, XML summary) |
| NEW | `Tools/perf_check.py` | Builds WebGL, serves it, runs Chrome, records results + screenshot |
| MODIFY | `.gitignore` | Unity ignores (`Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`, `.vs/`), `TestResults/`, `Tools/compat/`; node lines removed |
| MODIFY | `.vscode/extensions.json`, `.vscode/settings.json` | Unity extension recommendation; drop the TypeScript SDK setting; exclude Unity folders |
| MODIFY | `.claude/CLAUDE.md` | Project Config pins + commands, Layout confirmed, Testing section and table |
| MODIFY | `.claude/skills/audit/SKILL.md` | Only if the confirmed layout differs from its clusters |

All `.meta` files Unity generates are committed with their assets. No new top-level folders: build output goes to `TestResults/perf-build/`, the compat project to `Tools/compat/` (both ignored).

---

## Steps

### Step 1: Dev project, cloned from the 2D template
- `Unity.exe` (6000.6.7f1) `-batchmode -quit -createProject <scratch dir> -cloneFromTemplate <...ProjectTemplates/com.unity.template.2d-cross-platform-2d-7.0.0.tgz> -logFile <abs>` (the way Hub creates projects, so the built-in `com.unity.modules.*` land in the manifest). **Fallback** if the flag is rejected: extract `ProjectData~` and copy every depth-0 `com.unity.modules.*` entry from the template's `packages-lock.json` into the manifest.
- Copy `Assets/`, `Packages/`, `ProjectSettings/` (not `Library/`) into the repo root; delete `Assets/Welcome/` and `ProjectSettings/Packages/com.unity.learn.iet-framework/`.
- `Packages/manifest.json`: remove `com.unity.learn.iet-framework`, `com.unity.collab-proxy`, `com.unity.visualscripting`; add `"testables": ["com.orcapafea.daedalus"]`. Other versions stay as the template pins them.
- `ProjectSettings.asset`: `productName` → `daedalus-dev`, `companyName` → `orcapafea`; a fresh `productGUID` if the clone kept the template's.
- `.gitignore` / `.vscode` updates.
- Open once in batch mode to resolve packages.
- **Verify:** exit 0; `ProjectVersion.txt` = 6000.6.7f1; `m_SerializationMode: 2`; the manifest lists the built-in modules (incl. `com.unity.modules.unitywebrequest`, `com.unity.modules.tilemap`); `git status` shows no `Library/`/`Temp/`. No license in batch mode → stop and ask the operator to sign in via Unity Hub.

### Step 2: Package skeleton and assemblies
- `package.json`: `name` `com.orcapafea.daedalus`, `version` `0.1.0`, `displayName` "Daedalus Platformer Maker", `unity` `6000.0`, `description`, `author`, `dependencies` `{"com.unity.modules.tilemap": "1.0.0"}` (the PlayMode smoke test draws a Tilemap). Rule for later milestones: declare **the version 6000.0's template ships**; newer editors resolve upward.
- Asmdefs per the File Manifest (Editor references Runtime only; Sim is added when an editor tool needs it). Test asmdefs: `autoReferenced: false`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, references `UnityEngine.TestRunner` (+ `UnityEditor.TestRunner`, `includePlatforms: ["Editor"]` for EditMode).
- `AssemblyInfo.cs` in Runtime and Editor: `InternalsVisibleTo` the test assemblies.
- **Verify:** batch open compiles with no errors.

### Step 3: Test wrapper + first sim code, each seen failing
- `Tools/unity_tests.py`: `--editor {6000.0,6000.6,all}` (default all), `--platform {editmode,playmode,all}` (default all), `--filter`, `--project`. Editor path from `UNITY_EDITORS` env, default `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`.
  - **6000.6** runs against the dev project. **6000.0** creates (once, reused) `Tools/compat/6000.0/` with `-createProject … -cloneFromTemplate` from the 6000.0 2D template, then adds `"com.orcapafea.daedalus": "file:<abs path to Packages/com.orcapafea.daedalus>"` and testables.
  - Each invocation gets its own fresh output folder, `<abs>/TestResults/runs/<timestamp>-<project name>-<editor>-<platform>/` (created empty; never reused), so no earlier result can stand in for this run, and a `--project` run can never pick up another project's output.
  - Runs `-batchmode -projectPath P -runTests -testPlatform X -testResults <run dir>/results.xml -logFile <run dir>/unity.log` with `DAEDALUS_TEST_RESULTS=<abs>/TestResults`; never `-nographics`; a timeout (default 30 min) kills Unity and fails the run.
  - **Passes only when all of these hold**, otherwise exits non-zero naming the first that failed: Unity exited with code 0 (2 = test failures, anything else = launch/compile/licence error, reported with the log's `error CS` lines, or "project is open in another Unity instance" when the log says so); `results.xml` exists in this run's folder, was written after launch, parses, and its root `test-run` is complete; `total > 0` and `failed == 0`; **without `--filter`**, the mode's own assembly is present: EditMode XML must contain `Orcapafea.Daedalus.Tests.EditMode`, PlayMode XML must contain `Orcapafea.Daedalus.Tests.PlayMode` (an all-platform run checks each against its own result); **with `--filter`**, the selected mode must still run ≥ 1 test, and the assembly check is skipped; the log has no `warning CS` in our package (matches `/` and `\` paths).
  - Copies the latest passing XML per editor/mode to `TestResults/<editor>-<platform>.xml` only after it passes, for people to read; the wrapper never reads those copies.
- `FixedStepAccumulator` (Sim): fixed tick 1/60 s; `Advance(double elapsedSeconds)` returns the ticks to run and carries the fractional remainder; exposes the interpolation fraction, always in [0,1). **Hitch policy:** at most 5 ticks per call; whole ticks beyond the cap are **dropped** (the game slows during a hitch instead of spiralling), and only the fractional part of a tick is kept. Recorded in architecture.md's Time Model in cleanup. No allocation.
- `FixedStepAccumulatorTests`: tick counts for 1/60, 1/30, 0.5/60 + carry; the hitch case `Advance(60.5/60)` → 5 ticks, fraction 0.5, then `Advance(0)` → 0 ticks, fraction still 0.5 (no debt carried); fraction in [0,1) throughout. Fractions are compared with a tolerance (1e-9): depending on how the remainder is computed, `60.5/60` in doubles can come out as 0.4999999999999982 (round 2) rather than exactly 0.5. Written first with one wrong expectation → run → **see it fail by name** → fix.
- `--filter NoSuchTest` → **see the wrapper fail on zero tests**.
- **Stale-result check:** after a passing run, run again with the dev project locked (open it in the 6000.6 Editor, or make the run fail to launch) → **see the wrapper fail**, with the earlier passing XML still on disk.
- **Verify:** all three deliberate failures exit non-zero with the right message; the corrected run exits 0.

### Step 4: Guards
- Package root resolved with `PackageInfo.FindForAssembly(typeof(FixedStepAccumulator).Assembly).resolvedPath` (works for embedded, `file:` and git installs). Each guard asserts it found the files it checks, so it can't pass on an empty scan.
- `SimPurityTests`: Sim asmdef `references: []`, `noEngineReferences: true`; every `.cs` under `Runtime/Sim/` (≥ 1, must include `FixedStepAccumulator.cs`), comments stripped, free of `UnityEngine`, `UnityEditor`, `DateTime`, `Stopwatch`, `\bRandom\b`, `TickCount`. A self-test feeds the scanner a string with a forbidden token and expects a hit.
- `AssemblyDirectionTests`: Runtime refs ⊆ {Sim}; Editor refs ⊆ {Runtime, Sim} and `includePlatforms == ["Editor"]`; Sim refs = ∅; neither Sim nor Runtime references Editor; all three asmdef files exist.
- `PackageManifestTests`: `name`, `unity == "6000.0"`, every dependency version exact (no `^`, `~`, ranges).
- **Verify:** `python Tools/unity_tests.py --platform editmode` green on both editors.

### Step 5: PlayMode render smoke + screenshot
- `RenderSmokeTests`: builds in code a camera, a Grid + Tilemap (Chunk mode) and one sprite, using generated 16×16 textures with point filtering and an **unlit** sprite material (URP's `Sprite-Unlit-Default` when URP is active, else `Sprites-Default`) so lights and tonemapping can't change the colours; sets the camera's `targetTexture`, calls `Camera.Render()`, reads back to `Texture2D`, writes `<DAEDALUS_TEST_RESULTS or Application.dataPath/../TestResults>/<unity version>-render-smoke.png`; asserts the image is not one colour and contains the tile colour within a tolerance.
- Seen failing once (wrong expected colour), then fixed.
- **Verify:** `python Tools/unity_tests.py` (all, both editors) green; **open and look at both PNGs**.

### Step 6: WebGL performance check
- `PerfCheck` (dev-only MonoBehaviour): a 500 × 200 Tilemap in Chunk mode from 4 generated tiles, 200 SpriteRenderers moving on paths near the camera, the camera sweeping the full map width; 3 s warm-up, 20 s measurement into a preallocated frame-interval array; POSTs JSON (`fpsMean`, `frameIntervalP95Ms`, `refreshRate`, `tiles`, `entities`, `sweepTiles`, Unity version, screen size) via `UnityWebRequest.Post(url, body, "application/json")` to `/result`, and a PNG to `/screenshot`.
- `PerfCheckBuild.Build()` (`-executeMethod`, `-buildTarget WebGL`): saves `Assets/Dev/PerfCheck/PerfCheck.unity`; a **release** (non-development) WebGL build to `TestResults/perf-build/` with compression **disabled** and `runInBackground` on, both set only for the build and **restored in `finally`**; calls `EditorApplication.Exit(1)` unless `report.summary.result == Succeeded`.
- `Tools/perf_check.py`: runs the build (6000.6.7f1), checks the build output is newer than the start time, serves it (stdlib HTTP server with explicit `.js` / `.wasm` / `.data` types + the two POST handlers), launches Chrome (`--user-data-dir` temp, `--window-size=1366,768`, `--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows`), waits ≤ 120 s, closes Chrome, writes `TestResults/perf-check.json` + `perf-check.png`. **Gate:** `frameIntervalP95Ms ≤ 20` (primary) and `fpsMean ≥ 58`, `sweepTiles ≥ 500`; the refresh rate is recorded so a > 60 Hz screen is visible in the result.
- **Verify:** gate passes; **look at the screenshot**; `git status` shows no `ProjectSettings` change after the build.

### Step 7: CLAUDE.md
- § Project Config: package dependency pins + the "declare 6000.0's version" rule; the dev project's template package versions; Build / run commands (`python Tools/unity_tests.py [--editor] [--platform] [--filter] [--project]`, `python Tools/perf_check.py`); the dev project must be closed in the Editor during batch runs; read-only dirs add `Tools/compat/`.
- § Coding Conventions → Layout: drop "(proposed…)", match what landed. § Testing: remove the "nothing to run yet" preamble; real commands in the table.
- **Verify:** no "proposed" in the Layout line; every command listed was run in this plan.

### Step 8: Run tests
- `python Tools/unity_tests.py` green on both editors; then `git status --porcelain` (including `??` entries) shows no change under `Packages/com.orcapafea.daedalus/` from the 6000.0 run.

### Step 9: Verify
- Walk the Completion Criteria.

### Step 10: Code review, commit, push, git-URL proof
- Code-review agent (`.claude/agents/workflow/code-review.md`) on all changed files; fix HIGH/MED.
- Commit 1 (version-bumping, with the Post-Plan Cleanup docs), then **push `main` to `origin`**: the proof needs it on GitHub (approving this plan approves the push, including the unpushed v0.03–v0.05 commits).
- Git-URL proof: a throwaway 6000.0 project in the scratchpad (`-createProject … -cloneFromTemplate` 6000.0 2D template) with `"com.orcapafea.daedalus": "https://github.com/brisa0730/game_daedalus.git?path=/Packages/com.orcapafea.daedalus#<sha>"` + testables; `python Tools/unity_tests.py --editor 6000.0 --project <it> --platform editmode` green.
- Commit 2 (`docs:`): the plan's trailers + archive move. A stated exception to "one commit": the proof can only run after commit 1 is pushed.

---

## Completion Criteria

- [ ] `ProjectVersion.txt` → 6000.6.7f1; `m_SerializationMode: 2`; manifest lists the built-in modules.
- [ ] `python Tools/unity_tests.py` exits 0 on 6000.0.85f1 **and** 6000.6.7f1: Unity exit 0 per run, fresh XML per run, EditMode result contains `Orcapafea.Daedalus.Tests.EditMode`, PlayMode result contains `Orcapafea.Daedalus.Tests.PlayMode`, > 0 tests each, no `warning CS` from our package.
- [ ] Seen failing: a wrong EditMode expectation, a wrong PlayMode colour, a zero-test filter, and a failed launch after a passing run (stale XML present) each made the wrapper exit non-zero.
- [ ] `FixedStepAccumulator` hitch test: `Advance(60.5/60)` → 5 ticks, fraction 0.5 (± 1e-9); then `Advance(0)` → 0 ticks.
- [ ] Both `*-render-smoke.png` files exist and were inspected.
- [ ] `TestResults/perf-check.json`: `frameIntervalP95Ms ≤ 20`, `fpsMean ≥ 58`, `sweepTiles ≥ 500`, refresh rate recorded; screenshot inspected.
- [ ] `git status --porcelain` clean apart from intended files after all runs and the build; every asset has a `.meta`; nothing under `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `TestResults/`, `Tools/compat/` tracked.
- [ ] Git-URL install in a fresh 6000.0 project: EditMode suite green.
- [ ] CLAUDE.md § Project Config lists the pins and the commands actually run.

---

## Notes to Spec / Open Risks
- **License for OpenUPM.** OpenUPM lists open-source packages; the repo needs a license before the first release. Added to Roadmap §4 in cleanup.
- **OpenUPM with a package in a subfolder** is unverified; check at submission (milestone 5).
- **Dependency drift:** 6000.6's template ships far newer packages (Tilemap Extras 9.0.0, Input System 1.19.0) than 6000.0's (Input System 1.11.2). The "declare 6000.0's version" rule keeps the package installable on both; APIs that differ need a version define.
- **`-cloneFromTemplate`** is how Hub creates projects but is not in Unity's public CLI docs; Step 1 has a fallback.
- **Batch-mode licensing** depends on the Hub sign-in; Step 1 stops if missing.
- **Perf numbers** come from the dev laptop with no CPU throttling; the school-laptop run stays the real gate (Roadmap §2). Editor Play-mode fps is first measured in `world-systems`.

---

## Post-Plan Cleanup
1. Append trailers (Approval / Execution / Verification / Version note); `git mv` to `Documents/Plans/archive/`.
2. Changelog v0.06 (scaffold); GDD.md header → v0.06 with the stack line; architecture.md: drop "Draft until the scaffold lands", Time Model gains the 5-tick catch-up cap; MEMORY.md latest/next (`movement-prototype`, phase 1); Roadmap §0 record + Milestone 0 removed from §1; Roadmap §4: package license decision.
3. `framework.md`: batch-mode, template, WebGL facts learned (with editor versions).
4. `decisions.md`: ADR rows for the compat-project approach and the dependency-version rule.
5. Deferred.md entries for any leftover, each with a trigger.
6. Commits per Step 10.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: scaffold
round: 1
verdict: changes-requested
reviewer: codex
updated: 2026-10-21T23:01:02+09:00
blocking: 2
recommendations: 0
---

## Scope and evidence

Reviewed the entire `Documents/Plans/scaffold.md` (SHA256 `2F9D60509FCB4481FACB3C6BA760C81C5D8BDE20BDF7912CE3083CABF42B46C4`), the ready round-1 status and opening log, `.claude/CLAUDE.md`, `.claude/MEMORY.md`, GDD index and architecture, Roadmap, Issues, Deferred, framework notes, decisions, and existing ignore/editor settings. `.codex/CODEX.md` and root `CLAUDE.md` are absent; the review-root override is in `.claude/CLAUDE.md` and agrees with the protocol.

Available execution: read-only PowerShell, repository searches, Git inspection, and short arithmetic reasoning; scratch snapshot retained outside the Author's files. `rg --files -g '*.cs' -g '*.asmdef' -g 'package.json' -g '*manifest*'` found no existing implementation to check. `git status --short` showed only the untracked proposal and review directory. `git log -4 --oneline` confirms the v0.02 through v0.05 history, but does not establish push authorization or remote state.

Checked primary Unity documentation: [Unity 6 rendering feature comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/render-pipelines-feature-comparison.html), Scripting / Render Pipeline Hooks, lists `Camera.Render` as supported in URP. No finding alleges it is unsupported. [Unity's PlayMode test explanation](https://docs.unity.com/en-us/engine/6000.7/manual/programming-environment/test-framework-introduction/overview/general-introduction/play-mode-tests) explains that Editor-only assemblies are EditMode tests and enabling other platforms makes an assembly PlayMode. The reviewed plan itself specifies this separation.

No Unity app, test suite, build, browser performance check, install proof, or installed-editor inspection was run. Editor/template versions and installed tooling are Author-reported, not independently verified. Proposed APIs, licensing, shader availability and performance still require the Author's execution evidence. This is document review only.

Counts: 2 blocking, 1 non-blocking, 0 recommendations. No prior dispositions exist.

## F1 — Require fresh results and process success for each test invocation

- **Type:** blocking
- **Category:** design
- **Severity:** HIGH
- **Location:** `Documents/Plans/scaffold.md:77-78` (Step 3 wrapper failure conditions; also Step 10's install proof)
- **Defect / scenario:** The wrapper reuses `<editor>-<platform>.xml` and its acceptance rules only reject a missing XML, zero tests, failed results, missing assemblies, or package warnings. After a successful run, a later launch that fails before producing XML (project locked, licensing failure, compiler error, or crash) can leave the previous passing XML at that same path. All listed XML checks can then pass. In particular a previous compat-project result must not stand in for the fresh Git-install proof. A process failure is not explicitly an acceptance failure either.
- **Required change:** Specify removal of prior outputs before each launch or unique per-invocation output paths; require current-invocation completed XML and a successful Unity process exit, failing on launch errors/timeouts. Reject malformed or incomplete results as well. Scope artifacts to the actual project/run so `--project` cannot inherit another project's success. Have the Author exercise a failure after a passing run and show that stale XML cannot produce green. This is a missing fail-closed requirement, not an assertion about code that already exists.

## F2 — Map required test assemblies to their execution mode

- **Type:** blocking
- **Category:** accuracy
- **Severity:** MED
- **Location:** `Documents/Plans/scaffold.md:70,78,124` (test assembly configuration and wrapper/completion rules)
- **Defect / scenario:** The plan defines one Editor-only EditMode assembly and a separate PlayMode assembly, but says to fail when "either of our test assemblies is absent from the XML for that platform." Read literally, each invocation requires both, so a correct EditMode-only XML fails for missing PlayMode and vice versa; Step 4's EditMode command and Step 10's EditMode-only install proof cannot pass. If "for that platform" intends a mode-specific set, that set needs to be explicit rather than requiring the implementer to resolve the conflicting wording.
- **Required change:** Name the two assemblies and specify EditMode XML requires the EditMode assembly, PlayMode XML requires the PlayMode assembly, and an all-platform run checks both across its two results. Define how a legitimate `--filter` interacts with completeness checking (the selected mode must still run at least one matching test; a filter must not accidentally require unrelated suites). Align the completion criterion with those rules.

## F3 — Define what happens to elapsed time beyond the catch-up cap

- **Type:** non-blocking
- **Category:** design
- **Severity:** MED
- **Location:** `Documents/Plans/scaffold.md:79-80` (FixedStepAccumulator contract)
- **Defect / scenario:** "Carries the remainder" and "caps catch-up at 5 ticks" do not specify whether excess whole ticks are dropped, retained as debt, or input time is clamped. For a one-second input, 60 ticks are due and only five are returned. Retaining all remaining elapsed time leaves about 55 ticks of debt and a raw interpolation fraction about 55, inconsistent with the stated [0,1) test; dropping it changes simulated time, while clamping it changes the fractional remainder. Each can be implemented, but they are observably different policies for the first sim API.
- **Requested change:** State the intended overflow policy and assert the next call and interpolation result after a hitch containing both excess whole ticks and a fractional tick, e.g. `Advance(60.5 / 60)`, followed by `Advance(0)`. Record that same policy with the five-tick cap in architecture.md. My arithmetic is a contract trace, not an executed accumulator test.

## Reviewer's view

I would build the overall scaffold: the isolated minimum-version project, package boundaries and explicit render/performance evidence fit the project's requirements.
I would first fix F1 and F2 so a green summary proves the current intended run and can actually be reached by each mode.
F3 should be folded before close to give the first clock API one clear hitch policy. Approval would cover the proposal only; implementation and verification remain the Author's work.
## Review round 2

---
slug: scaffold
round: 2
verdict: approved
reviewer: codex
updated: 2026-10-21T23:10:53+09:00
blocking: 0
recommendations: 0
---

## Scope and verification

Delta review against the retained round-1 snapshot. Read current ready round-2 status and all round-1 dispositions in `log.md`, compared the complete round-2 document snapshot with round 1 using `git diff --no-index`, and checked the affected Step 3 and Completion Criteria against the unchanged Step 2 assembly split and Step 10 install-proof flow. The diff exit code 1 denotes differences, not a failed review. Round-2 snapshot SHA256: `B2C781318686CAA7C941DF6F7BD40D4D3127FC0335CDD9F6E3013E041B8E06EB`.

A short PowerShell arithmetic calculation independently yields 60 whole ticks due, 5 returned, 55 dropped, and a fraction of 0.4999999999999982 for `60.5/60` seconds. This is the expected floating-point approximation to 0.5, so assertions should use a numerical tolerance. The carried fractional remainder alone is below one tick and a following zero-time call has no whole ticks to return. This calculation checks the proposed policy, not an implemented accumulator. A separate string comparison confirmed the appended round-1 review matches `round-1.md` after trimming surrounding whitespace.

`git status --short` still shows only the untracked proposal and review directory. No Unity app, test suite, build, performance check, editor/template inspection, or Git install proof was executed. Those are future Author verification tasks, not passed checks. No implementation files were changed by this review.

## Findings and dispositions

- **F1 — resolved.** Fresh, never-reused invocation directories and checking only their XML prevent an earlier run or another project's result from satisfying the current run. Unity exit 0, current parseable complete XML, and timeout/launch failure handling are now explicit requirements. The stale-result failure demonstration is included in both the execution step and completion criteria. The human-readable latest-result copies are explicitly never inputs to acceptance.
- **F2 — resolved.** The two test assemblies are named, each unfiltered mode checks its own assembly, and an all-mode run checks each result separately. The filtered path explicitly permits a subset while still requiring at least one test in the selected mode. The unfiltered EditMode-only guard and install-proof commands are consistent with these rules.
- **F3 — resolved.** The document chooses dropping excess whole ticks, retaining only the fractional remainder, and specifies the hitch followed by a zero-time call. The architecture cleanup references this same policy. Arithmetic checked above supports the example.

Counts: 0 blocking, 0 non-blocking, 0 recommendations; no new findings. All three previous findings were accepted and corrected; no reasoned rejection requires reopening or withdrawal. The earlier Camera.Render incompatibility hypothesis was disproved before round 1 and was never a finding. The late-pushback check found no additional concern to file.

## Approval and next custody

Approved as a proposal. Claude Code should perform the protocol's closing fold, append this review and closing dispositions, mark done, and archive the handshake. This approval is not implementation, test evidence, or operator authorization to execute/push the plan. Implementation remains separate work under the operator's existing authorization and project rules. The later package-license choice and school-laptop performance gate remain as documented future decisions/checks, not blockers to this scaffold proposal.

The watch resumed in this turn, found round 2 already ready, and terminates on this approved verdict as required by the protocol. No recurring automation was created and no further polling is needed.

## Reviewer's view

I would build this scaffold as revised: fresh-run evidence, per-mode assembly checks, and the explicit hitch policy resolve F1, F2, and F3.
The document is approved; actual editor compatibility, rendering, browser performance, and Git installation remain to be demonstrated during implementation.