---
slug: phase6-share-my-game
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T14:50:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: phase6-share-my-game (delta)

## What I could run, what I verified, what I could not

**Ran:** the same shell in the Mac mini folder. I read `log.md`'s round 1 dispositions and re-read every section the Author changed: open questions, Steps 3, 5–12, Completion Criteria, Post-Phase step 9. No Unity, no cargo.

**Verified:** each of F1–F13 is folded where the log says. R1–R6 are recorded beside their questions.
- F1: Step 8 now compares the zip's raw `daedalus.json` title with `clean_label(raw_file)`. The staging copy `check_and_unpack` writes is the editor's bytes (`share_zip.rs`, `write_new(... meta_bytes)`), so the comparison is now C#(raw) vs Rust(raw). The second assert (`unpacked.title` equals the raw title) holds exactly when the editor's output is already clean, because `clean_label` is idempotent on its own output. **F1 closed.**
- F2, F3, F4, F6, F7, F8, F9, F10, F11, F12, F13: folded as described. They are closed, except that the F2 fold leaves two seams (F14, F15).

**Not verified (unchanged from round 1):** Unity API behaviour named there as accepted on trust.

## Findings

### F5 (re-opened, non-blocking): the fail-closed probe as written can pass by the wrong path
- **Type:** non-blocking · **Category:** design · **Severity:** medium
- **Location:** Step 10 "Fail-closed, end to end"; Completion Criteria (the "proved end to end with a throwing build callback" clause)
- **Defect:** an exception *thrown* from `IPreprocessBuildWithReport.OnPreprocessBuild` normally makes `BuildPlayer` fail. Share would then refuse through `Outcome`'s "not succeeded" branch with the build-failed text, never through the exception count. The probe passes even if `Run` never hands the watch's count to `Outcome`, which is exactly the wire F5 was about. ISS-001's real exceptions came from URP's shader preprocessing while the build reported success.
- **Fix:**
  - Make the probe log without failing the build: `Debug.LogException(new Exception("share-check probe"))` inside `OnPreprocessBuild`, or an `IPreprocessShaders` that throws once, as in ISS-001.
  - Have the check assert three things:
    - the build reported Succeeded;
    - the `ShareMyGame: fail` line carries the **exception-during-build** text, not the build-failed text;
    - no zip exists.

### F14 (new, non-blocking): the "Run on an Untitled scene" test needs a Player and a camera
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 Tests, `CheckBeforeBuild` group ("`Run` on an Untitled scene → the Untitled text")
- **Defect:** `Run` step 1 calls `CheckBeforeBuild` first. On an empty `NewScene` it returns the no-Player text, so the test as described either fails or pushes the path guard ahead of the checks.
- **Fix:** build that Untitled scene with a Player and a MainCamera-tagged camera, so the path guard is the first thing to stop it.

### F15 (new, non-blocking): the Step 12 Untitled stop can't be reached as written
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 12.2 ("in a new Untitled scene (Unity's save prompt; Cancel stops quietly)"); "Must be absent: a zip left after a failed share"
- **Defect:** after the F2 fold, a new Untitled scene (Main Camera, no Player) gets the no-Player dialog, never Unity's save prompt. The operator would record a fail, or the save-prompt path would go untested. Also, since F4, an oversize share deliberately leaves the *previous* zip in place, so "a zip left after a failed share" can be misread as a fail.
- **Fix:**
  - Untitled check: "an Untitled scene with a Player and a Main Camera (File → New Scene, add a Player): Unity's save prompt appears; Cancel stops quietly; Save continues to the title window."
  - Must-be-absent line: "a **new** zip after a failed share (an earlier zip at that path stays)."

## Late-pushback test

Is there anything in my view not already in a round file? No. The Korean-students-with-English-prompts question is an Active operator decision (decisions.md 2026-11-03), so I don't reopen it. The audience-age open question is already recorded as R1.

## Reviewer's view

Approved. The only blocking finding (F1) is fixed correctly, and the rest of round 1 was folded faithfully. Three non-blocking items remain for the close fold: F5, where the probe must log rather than throw so it actually tests the exception wire, and F14 and F15, where the F2 reordering left a unit test and an operator check describing the old flow. I would build it with those folded.
