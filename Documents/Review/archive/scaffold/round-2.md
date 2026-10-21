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