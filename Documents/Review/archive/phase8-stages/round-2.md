---
slug: phase8-stages
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-09T22:50:00+09:00
blocking: 0
recommendations: 0
---

# Round 2: phase8-stages (delta review)

## What I could run, and what I verified

Same access as round 1: `device_bash` over the office PC repo, and no Unity, tests, build or browser. This is a delta review: I read `log.md`'s round 1 dispositions and the changed parts of the document (Source, Steps 2–4, 8, 9 and 11, Completion Criteria, Notes to GDD), not a fresh full pass.

## Correction to round 1

**F1's suggested HUD text was wrong.** I wrote "Save this scene (Ctrl+S)" as a line shown during Play. Unity doesn't save scenes in Play mode, so the instruction couldn't be followed as written. The Author's text, "Stop, then press Ctrl+S", is right, and moving the line to the win screen, where the confusion actually happens, is better than my play-start placement. My round 1 file stays as it was; this is the correction.

## Round 1 findings

- **F1 (blocking): closed.** `FirstSceneUnsaved` is captured while the first scene is still loaded, and the win screen shows a line when it is set. `isDirty` in a build is always false, so shared games never show it. The fallback, if `isDirty` reads false in Play, is named (an editor prompt that only asks). The Step 11 editor check drives exactly the failing case and answers the `isDirty` question, and "Play again showing an older stage with no message" is under Must be absent and in the Completion Criteria. The seam holds: after Play again the run is destroyed, and the next run is created on a stage loaded from disk, so the flag is correctly false there.
- **F2: closed, and the extension is right.** Without the Author's `!run.LoadFailed` check, Play again after a failed return would repeat the failing load. Counting deaths only once the load has started closes the double count I didn't see. One leftover is filed as F11.
- **F3–F10: closed** as folded. Each fold matches the finding. The F4 long-frame injection during the hold is weaker than a real long first frame after the reveal, but `captureFramerate` rules that out in batch mode, and the reason is now written down.
- **R1–R5:** recorded in Source, beside the pending questions. R1 is marked as the one that changes the design.

## New finding

### F11 — non-blocking · design · Step 4 (failed-load notice) and Step 2 (`ShowMissingStage`)
**The HUD's notice state is never cleared, so a later win can read wrong.** `ShowMissingStage()` and `ShowStageLoadFailed()` overwrite the win title and blank the deaths line, and nothing says they restore it. Failure: a student plays from an Untitled first scene through the chain to the last stage. Play again fails ("Couldn't open the first scene"), and the last stage restarts in place. The student reaches its empty-link Exit, and the win screen shows "Couldn't open the first scene" with no deaths line, instead of "You made it!". Also, because `PlayAgain` zeroes the total only once the load has started, the in-place restart keeps showing the old game total. Fix: restore the title and deaths line whenever a normal win is shown, and on an in-place restart after a failed Play again either zero the run's total or state that it carries on. Add one assertion to the failed-load test: a later empty-link win reads "You made it!" with a deaths line.

## Late-pushback test
Would my answer to "what do you think?" contain anything not already filed? No. F11 was the only new seam the round 2 changes opened.

## Reviewer's view
I would build this as written. The one blocking gap (F1, the unsaved first stage on Play again) is closed, with a fallback and an operator check that settles the one Unity behaviour batch mode can't. F11 is a small HUD reset for the Author to fold before closing. R1 (allow a send-you-back Exit when a win is reachable) is the operator's open design decision, and the document builds correctly either way.
