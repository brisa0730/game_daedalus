---
slug: phase1-movement-sim
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:31:00+09:00
blocking: 1
recommendations: 0
---

# Review round 2 (delta): `Documents/Phases/phase1-movement-sim.md`

## What I could run, what I verified, what I could not

**Could run:** file reads of the connected repo (staged from the device) and Python in my own cloud container. **Could not run:** Unity, either suite, a build, anything on the user's machine. The simulation model is the same as round 1, with counters decremented at the new item 9.

**Delta read:** `log.md` round-1 dispositions; Conventions; Steps 1, 3, 5–13; Completion Criteria; Notes to GDD. These are the sections the dispositions name. I didn't re-read Steps 2, 4, 14, 15 or Post-Phase Cleanup in full: the log reports no change there, and the section headings and sizes match.

**Verified in the fold:**
- **F1:** the Step 9 precondition is now "fired with the wall within `WallJumpReach`"; ≈0.2 is gone. Closed.
- **F3:** Conventions states impulse-first integration and names the 3.333 failure. Closed.
- **F4:** I re-traced the new timer convention (set on T, checked T+1…T+N, decremented at item 9 unless set this tick):
  - coyote: W+6 yes, W+7 no ✓
  - buffer: P+1…P+6, fires on L+1 iff P ≥ L−5 ✓
  - lock: J+1…J+12 locked, J+13 free ✓ (J itself is also locked; harmless, since the push sets vx)
  - wall coyote: E+6 / E+7 ✓
  - gap gate on W+6: 5.4 in my model ✓

  Closed.
- **F5, F6, F8, F9, F12, F13, F14:** folded as dispositioned; I checked each text. Closed.
- **F7:** Open question 1 lists all six levers. Closed.
- **F10:** chimney gate is in Step 12 and the Completion Criteria. Closed.
- **F11:** air cling needs vy ≤ 0 or Up. I re-checked the 3-tile climbable ledge: rising past the face has vy > 0, so there's no cling, and the box clears row 3 before the apex. Closed.
- **F2, option (b):** I re-ran my round-1 exploit loop with (b): slide, wall jump, air jump at the apex, back to a slide on the same face with no refill. Starts go 0 → 6.875 → 6.297 → 5.719. The first cycle spends the air jump it started with, and after that height falls 0.578 per cycle. So (b) bounds single-wall scaling as intended. Rule closed, but see F17, F18 and F20 on its spec and test.
- **F15, F16:** recorded beside the open questions. ✓

## Findings

### F18. The DoubleJump-on single-wall test fails on its first pair and never exercises rule (b)
- **Type:** blocking · **Category:** accuracy · **Severity:** high
- **Location:** Step 9 Tests, "no single-wall scaling … run twice"; Completion Criteria gate line ("with DoubleJump off and on"); Step 12 ability-matrix row `+DoubleJump +WallJump`.
- **Defect:** This is a seam from the round-1 fold.
  - **The first pair fails.** In the DoubleJump-on run the player starts with the air jump full, so the first cycle's apex press adds 3.5. My model, with press every tick plus a press at each apex, gives wall-jump starts **0 → 6.867 → 6.489 → 5.912**. "Each starting ≥ 0.1 tile lower than the previous one" fails on jump 1 → 2.
  - **It never tests (b).** The F1 fix means a press-every-tick script fires every wall jump *before* contact, so the player never slides, and a slide is the only thing that refills. This run gives the same result with or without (b), so it can't catch a regression of F2. The step's own note "the air jump is spent every cycle" assumes a refill this script never triggers.
  - **The matrix row has no script.** The `+DoubleJump +WallJump` row says "the ordinary wall still can't be scaled" but doesn't name the inputs, so it can be vacuous in the same way.
- **Fix:** For the DoubleJump-on run, and the matrix row, use the exploit script:
  - jump only from an active WallSlide (wait for contact while falling), and air jump at each apex;
  - assert that the first cycle's gain is bounded (≈ +6.9 in my model, i.e. no more than one air jump's worth);
  - assert that every later start is ≥ 0.1 lower and that each later slide did **not** refill `AirJumpsLeft`.

  Without (b), the same script gains ≈ +6.9 per cycle in my model, so the test is a real guard.

### F17. A wall jump from wall coyote has no face to record
- **Type:** non-blocking · **Category:** accuracy · **Severity:** med
- **Location:** Step 6 `PlayerState` fields; Step 9 wall jump ("`LastWallJumpSide`/`LastWallJumpFace` = this wall"); wall coyote ("with the side remembered").
- **Defect:** A wall jump can fire from `WallCoyoteTicks > 0` with no wall in reach. `PlayerState` keeps `WallCoyoteSide` but no face. So `LastWallJumpFace` for a coyote wall jump is undefined: a stale value, 0, or a probe that finds nothing. If it ends up not matching, the next slide on that same wall counts as "a different wall" and refills. That reopens F2 by one detour per cycle: slide, step off the wall, coyote wall jump, air jump, back to a slide that refills.
- **Fix:** Store `WallCoyoteFace` alongside `WallCoyoteSide` when wall coyote starts, and record it on a coyote wall jump. Add one test: the coyote wall jump, then a slide on the same face, doesn't refill.

### F19. The tick-start probe could stand up a player who is rising through a one-way
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 tick order item 2 ("the support probe updates the standing contact").
- **Defect:** Read literally, the probe turns any Air state whose feet are within `ContactEpsilon` of an integer row over a OneWay cell into OneWayTop, and standing sets vy = 0. A rising jump whose feet happen to sit on a one-way's top at a tick boundary would stop dead on the platform. That contradicts Step 7/8 ("lands on it on the way down only"). From an integer floor at defaults the trajectory n/3 − n²/126 never hits an integer while rising, but other settings or start heights can.
- **Fix:** Promote Air → standing at item 2 only when vy ≤ 0, matching the Landing rule.

### F20. Rule (b) appears as a settled rule, though it is Claude's choice
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Notes to GDD § systems.md "A wall slide refills only on a different wall face…"; § Operations "add the F2 rule".
- **Defect:** The log says (b) was "flagged to the operator as Claude's choice, overridable", but the Notes list it among the operator rules without **(proposal)**. It isn't an open question either, so the completion report won't put it in front of the operator. Approving it is the operator's call, since it refines their 2026-10-23 decision on resetting air jumps.
- **Fix:** Mark it **(proposal)**, or add it as Open question 3 with the (a)/(b)/(c) options, "Claude recommends (b)" and "Reviewer recommends: (b) (round 1, F2)".

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F18}. Both earlier blockers are closed. F18 is new, introduced where the F1 and F2 fixes meet. No reasoned rejection this round. None of my round-1 claims was shown wrong. The Author quoted my modelled numbers as mine, not as measured, which is correct.

## Reviewer's view

Close. F18 is the one thing stopping approval: the DoubleJump-on gate test fails on its first pair and wouldn't catch a regression of rule (b). F17 is the one gap left in (b)'s spec. F19 and F20 are one-line tightenings. Once those are folded, I expect to approve next round.
