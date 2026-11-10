# Phase 8 log: `stages` (v0.26, 2026-11-10)

Troll games can be a linear run of stages, one Unity scene each. Prompt: [phase8-stages](../../../../Documents/Phases/archive/phase8-stages.md). Design panel, then handshake (2 rounds, approved; F1 blocking, the unsaved first stage on Play again).

## What shipped
An Exit's optional Next stage (a scene, stored by GUID; `ExitLink`, `StageLoad`), the sim's `WonExit`, the stage change and its fade with the carried death total (`StageRun`, made only at the first linked Exit), the hold until the fade clears, held directions on arrival, `999+`, Share following the links (`StageWalk`, `StagePathStamp`), the chain in the title window, a two-stage bench played in Chrome by `share_check.py`, the Starter's `Stage Example` and `Prefabs/Stage Player`, section 9 of the install guide.

## Changes from the prompt
- **Share stops only when nothing can win** (operator's "go ahead" on the reviewer's and my recommendation). A back-link in a chain that can be won warns and is built. A one-scene game with no Exit still shares, with a warning.
- **`WaitForEndOfFrame` is gone.** It never resumes in batch-mode PlayMode tests. Frame waits do the same job: black is shown for two more frames before the load, and the new stage's first frame is drawn under black.
- **`PlayerControls(keepHeldDirections: true)` replaces `KeepHeldDirections()`.** `wantsInitialStateCheck` is read when bindings first resolve, so setting it after the first Enable does nothing. On arrival the Player makes fresh controls.
- **Stage B is painted as an outline** (200 × 60): a filled map is ~28 MB of scene text in git for no gain. The full-size build was a one-off.
- **Back-links appear as warnings under the chain,** not as a separate line of the chain.
- **The test harness needed two fixes** nobody could predict from the plan: the `[PrebuildSetup]` must not replace the bootstrap scene, and the test runner must be marked `DontDestroyOnLoad` to survive an Exit's Single load (framework.md).

## Evidence (final code)
- **Suites.** 523 EditMode and 105 PlayMode (102 passed, 3 skipped: the two Starter playthroughs and the fixture, which need their own projects) on 6000.0.85f1 and 6000.6.8f1.
- **Share and Chrome (`share_check.py --editor all`).** Trap bench and two-stage bench on both editors. In Chrome: Stage A, then a fully black frame, then Stage B steady for 0.5 s. Longest frame across the change about 37 ms on the bench (32 to 37 ms over the runs). Zips ~10.0 MB (6000.6) and ~11.75 MB (6000.0); the second stage adds nothing measurable. `--no-gallery-check`: no cargo on this PC, so the gallery's own `check_share_zip` was not run. The zip's six entries, compression, metadata and settings restore were checked.
- **One scene unchanged.** `share_check.py --project Tools/fixtures/0.1.0 --scene "Assets/My Level.unity"` passes on both (11,466,744 and 11,759,723 bytes).
- **Clean install (`install_check.py --editor all`).** Starter playthrough, the new stage playthrough and the 0.1.0 fixture pass on both.
- **Frozen pieces.** `build_starter.py --refreeze`: two new rows (the Stage Player prefab and its `.meta`), every frozen row unchanged, `Starter.unity` no diff.

## Step 8 measurements (one-off, 6000.6.8f1)
A 500 × 200 Stage B filled except a corridor (27.8 MB scene file): longest frame across the change **37 ms** in Chrome, zip 10,000,561 bytes (no growth, far under 50 MB). The separate `PlayHud` and `ReadMap` stopwatch lines did **not** reach the page (Debug.Log isn't routed through the loader's `print`), so per-item costs are not known; 37 ms is the ceiling for both together, under black. The prompt's rule (move the string tables into `StageRun` if `PlayHud` passes 2 ms) wasn't decided by a number: the whole frame is too short to matter, so the tables stay per Player. What-links-here's time was not timed separately; it is inside a share measured in minutes.

## Review
Code review: PASS WITH FLAGS (0 HIGH, 1 MED, 5 LOW). Fixed: re-entry into a stage change (a duplicated Player) with a test; Play again on a missing-stage notice restarts in place; `FailedStage` clears on a restart in place; the save note hides with the notice; a stage listed but unloaded goes back to that; a stage file that can't be opened gives a dialog. Filed in Deferred.md: `FirstView` and the Collision Tilemap pick still look at every open scene.

## Operator checks (not done; both need a person)
**Editor check.** *Do:* on an Exit, drag a scene into Next stage; read the help box; rename and move the target; delete it; select the Exit prefab asset; open Share from stage 1, from stage 2, and with a broken link. Drag a second scene into the Hierarchy and press Play. Move a trap in stage 1, press Play without saving, finish the chain and read the win screen, then press Play again. *Observe:* the three stories; the link survives rename and move; a deleted target shows an error distinct from empty; the prefab warning; the window lists the chain; warnings name scenes; a broken link stops with a named dialog; the two-Players warning names the second scene; the win screen's unsaved line appears (**this answers whether `Scene.isDirty` reads true in Play**: if it doesn't, Step 4's fallback is an editor prompt that only asks). *Must be absent:* a raw GUID box; a broken link reading as empty; Build Settings changing; Play again showing an older stage with no message.

**Play-check.** *Do:* play A → B → C holding Right through each change, with Space held on one change; die in each stage; finish; Play again; repeat in the shared build in Chrome. *Observe:* the fade feels quick; the player keeps running on arrival; a held jump doesn't fire; deaths total on screen; Play again starts stage 1 at 0. Tune `StageFadeSeconds` by hand if needed. *Must be absent:* any win-screen flash mid-chain; a frame of the new stage before black clears; the player or traps moving before the reveal ends; a death burst at the Exit.
