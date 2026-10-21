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