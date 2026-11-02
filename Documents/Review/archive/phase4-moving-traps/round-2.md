---
slug: phase4-moving-traps
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-02T18:36:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: `Documents/Phases/phase4-moving-traps.md` (delta)

## What I could run, what I verified, what I could not

**Could run:** the device shell over the `daedalus` folder (`sed`, `grep`), as in round 1. **Could not run:** Unity, the suites, the bench. Nothing here is a test result.

**Delta read:** `log.md` round 1 dispositions in full, plus every changed passage they point to: Source (v0.12), Step 4 (Starting state, rest-cell rounding, start moment), Step 5 (`ApplyPush` flag, ride-speed seed), Step 8 (no win on the fire tick, new ExitTrapTests), Step 10 (`IsOnExit` / `TrapOnDisabledExit`), Notes 8, open questions 1–5 with my recommendations recorded, and Post-Phase (v0.13).

## Round 1 findings: status

| Id | Was | Now | Checked |
| --- | --- | --- | --- |
| F1 | blocking | **closed** | Source v0.12, sync v0.12 → v0.13, commit `v0.13: Phase 4 — …`, ADR cited by title. |
| F2 | blocking | **closed** | `IsOnExit` is enabled-Exit-only and matches `FindActive`; the assert is gone; a Trap on a disabled Exit is ignored with `TrapOnDisabledExit`; manifest, `FindTraps`, gizmos, `WarnLevel` and tests all updated. One edge left over, see F16. |
| F3 | blocking | **closed** | The seed is the pre-step `VelocityY` (`max(0, −preStepVy)`) when the player newly stands on a moving box. Notes 3 ("fall speed on landing") still reads correctly against it. |
| F6 | blocking | **closed** | (a) the default exit zone is the exit box grown 1 on every side, so it fires 1 − 0.0625 = 0.9375 tile before a touch; (b) no win on the fire tick. Arithmetic re-checked: at Run Speed 6 (0.1/tick) a Move at 8 (0.133/tick) pulls away; at 12 (0.2/tick) the fire gap is in [0.8, 1), and closing at 0.067/tick catches it about 1.5 tiles out, never at the start box. That matches the new test's claim. (a) is correctly raised as open question 5, since it bends "one default zone for every action". |
| F4 | non-blocking | closed | `ApplyPush(…, playerStepsThisTick)`; both Appear paths tested. |
| F5 | non-blocking | closed | The `Starting` state stays stamped until the first phase-4 advance. Checked against the delay-fired same-tick start, zero offset, the rider's "just-started" old box, and the rebuild trigger. No hole is left, and no double rebuild. |
| F7 | non-blocking | closed | A bottom already inside a painted row lands on its top. Tile traps have integer bottoms, so they are unaffected. |
| F8–F11 | non-blocking | closed | As listed in the log. |
| F12–F15 | recommendation | recorded | Beside open questions 1–4. |

**No new seam from the folds** except F16 below. Blocking id set: round 1 {F1, F2, F3, F6} → round 2 {} (converged).

## New

### F16: Prefab assets would read as "Exit turned off" in the inspector
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 10 (`IsOnExit`: "true only for an enabled Exit on an active object (`isActiveAndEnabled` …)"); Step 13's inspector use of `IsOnExit` / `TrapOnDisabledExit`.
- **Defect:** `isActiveAndEnabled` is false for any component on a prefab **asset** (not in a scene), because `activeInHierarchy` is false there. CLAUDE.md expects students to save their own prefabs. Selecting a runaway-exit prefab in the Project window would show the false warning "This Trap's Exit is turned off", show Size/Tile, and tell the tile story instead of the exit's.
- **Fix:** use `exit != null && exit.enabled` for both edit time and play start. At play start it is equivalent, because the Trap shares the Exit's GameObject: if the Trap was found by `FindActive`, the object is active in the hierarchy, so `exit.enabled` matches the exit list. Add a TrapLayoutTests case on a prefab-asset instance (or an inactive object) asserting `IsOnExit` follows `enabled`.

## Late-pushback test

Asked "what do you think?", my answer contains nothing beyond F16 and the recorded recommendations. Two conditions stay true after approval: the play-feel claims (the pull-away hop, crush fairness) are judged only at Step 19, and open question 5 needs the operator's answer before Step 10 builds the exit default zone.

## Reviewer's view

I would build this as written, with F16 folded on close. Every round 1 blocker (F1, F2, F3, F6) is fixed at its root rather than patched in tests, and the folds (the Starting state, the ride seed, the exit zone and fire-tick rule) agree with each other and with the code paths I traced. Approved.
