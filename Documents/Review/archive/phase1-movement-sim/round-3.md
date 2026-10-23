---
slug: phase1-movement-sim
round: 3
verdict: approved
reviewer: claude-cowork
updated: 2026-10-23T21:33:00+09:00
blocking: 0
recommendations: 0
---

# Review round 3 (delta): `Documents/Phases/phase1-movement-sim.md`

## What I could run, what I verified, what I could not

**Could run:** file reads of the connected repo (staged from the device) and Python in my own cloud container. **Could not run:** Unity, either suite, a build, anything on the user's machine. Nothing in this document has been built or tested; every number below is from my model or the document's own arithmetic.

**Delta read:** `log.md` round-2 dispositions; Step 6 `PlayerState` fields and tick order item 2; Step 9 (wall-coyote grant, wall-jump recording, Scripts A/B/C); Step 12 matrix row; Completion Criteria gate line; Notes to GDD (proposal list, Operations, Open question 3).

**Verified in the fold:**
- **F17:** `WallCoyoteFace` is in `PlayerState` and set together with the side when wall coyote starts. A coyote wall jump records `WallCoyoteSide`/`WallCoyoteFace`. Script C asserts no refill on the next same-face slide. Closed.
- **F18:** Script B jumps only from an active WallSlide and asserts that precondition. Its bounds hold in my exploit-loop model with rule (b): first-cycle gain +6.875 ≤ 7.0, later starts −0.578 each (≥ 0.1), no refill on same-face slides. Without (b) the same loop gains +6.875 per cycle and fails the "later starts lower" assert, so it is a real guard. The matrix row reuses Script B. Closed.
- **F19:** item 2 promotes Air to standing only when vy ≤ 0. A fresh `CreateState` on the floor has vy = 0, so F5's tick-1 behaviour holds. Closed.
- **F20:** rule (b) is marked **(proposal)** and is Open question 3 with (a)/(b)/(c), Claude's and the Reviewer's recommendations beside it. The decisions.md addition waits for the operator. Closed.

## Findings

### F21. Script B quotes the wrong model numbers
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 9 Script B ("Reviewer's round-2 model: +6.867"; "model: 6.867 → 6.489 → 5.912").
- **Defect:** These are the numbers from my round-2 run of the *press-every-tick plus apex air jump* script, which F18 showed can't exercise rule (b). My exploit-loop model, which matches Script B, gives starts **0 → 6.875 → 6.297 → 5.719**: +6.875, then −0.578 per cycle. Both sets satisfy Script B's assertions (≤ 7.0; ≥ 0.1 lower), so no test changes. But a future reader comparing measured prints with the quoted model would see an unexplained 0.2-tile gap per cycle.
- **Fix:** Replace the quoted numbers with 6.875 → 6.297 → 5.719. If Script A wants a DoubleJump-on reference, keep the press-every-tick numbers there, labelled as such.

The late-pushback test found nothing else to file. Asked "what do you think?", my answer contains nothing that isn't already in round 1–3 files.

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F18} → round 3 {}. Each fix was accepted with a re-derivation. No reasoned rejection, and none of my claims was shown wrong. F21 is a slip in quoting my own earlier numbers, corrected above.

## Approval and next custody

Approved as a proposal. The Author folds F21 on close, records nothing further (all recommendations are already recorded), appends this review and the closing dispositions, sets `status: done` and archives the handshake.

Approval is a verdict on the document only. Nothing is implemented and no test has run.

Conditions this approval rests on and could not verify:
- The scenario breakpoints and model numbers are from my Python model, not the sim. The tests print the implementation's measured values.
- `GC.GetAllocatedBytesForCurrentThread` and `Is.Not.AllocatingGCMemory()` are unproven on 6000.0 batch mode. Step 13 proves them or falls back to code review.
- Open question 3, the air-jump refill rule, is the operator's decision. The prompt is written for (b).

## Reviewer's view

I would build this document as written, with F21 folded. The two round-1 blockers (F1: a test that couldn't pass; F2: single-wall scaling with double jump) and the round-2 seam (F18) are resolved and guarded by tests that fail without the rule. The pinned numbers now rest on stated conventions (F3, F4, F5, F19). The one open design choice, rule (b), is correctly put to the operator as Open question 3.
