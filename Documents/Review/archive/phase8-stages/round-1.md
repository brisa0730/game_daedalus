---
slug: phase8-stages
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-09T22:50:00+09:00
blocking: 1
recommendations: 5
---

# Round 1: phase8-stages

## What I could run, and what I verified

I had folder access to the office PC project (`device_bash` over the repo: `cat`, `grep`, `sed`, `git diff`). I ran no Unity, no tests, no build and no browser. Everything below about Unity, Chrome or Input System behaviour at run time is a prediction for the Author to confirm. A claim I accepted on trust is marked as such.

**Checked against the code and found true:**
- Runtime has no `UNITY_EDITOR`, `UnityEditor`, `SceneManager` or `DontDestroyOnLoad` anywhere outside `Runtime/Sim` today, so `StageLoad.cs` really would be the first, and the Step 10 greps start from zero.
- `Exit.cs` has only `dataVersion` (HideInInspector, version 1); `ExitEditor` calls `DrawDefaultInspector` plus `HelpTexts.ExitNotice` (`HelpTexts.cs:19`).
- `LevelStepper.Step` step (8) sets `Phase = Won` inside `for (int e …)` and returns (`LevelStepper.cs:286-295`), so `WonExit = e` fits there; `Begin` resets `Deaths` (`:165-178`); `Won` only counts up (`:193`).
- `PlayerSession.Restart` already calls `accumulator.Reset()`; `FixedStepAccumulator.Reset` exists and caps at 5 ticks.
- `Player.RunFrame` uses `Time.deltaTime`, the Play-again test `session.Phase == Won && CanPlayAgain && PlayAgainPressed`, and `hud.Show(session.Deaths, Phase == Won, …)` in `Render`; the plan's `won && !leaving` slots in cleanly.
- `PlayHud`: `CachedCounts = 1000`, index `Min(deaths, CachedCounts - 1)`; the first `Show` doesn't bump (`shownDeaths = -1`), so "no bump on arrival" holds by construction.
- `PlayerControls`: the four directions are `Button` actions read with `IsPressed()`, so a key held across a new Player's `OnEnable` is not seen today, which is the premise of Step 5. Jump uses `WasPressedThisFrame`.
- `PlayStartScan.FindActive` uses `FindObjectsByType`, which also returns `DontDestroyOnLoad` objects.
- `BuildScenes.Use(string[])` and its `AssetPathToGUID(…, OnlyExistingAssets)` idiom; `ShareBuild` uses `Camera.main` at `:48` (stop) and `:97` (picture), and passes `new[] { scene.path }` at `:117` and `:128`.
- The Deferred entry "Share's checks look at every open scene, but only the active one is built" exists, and its fix shape matches Step 6.
- `framework.md` records that the compat project reloads the domain on Play and drops in-memory tiles (Step 3's saved tile assets).
- `TrapLayoutTests` pins Checkpoint and Exit in one shared loop (`:43`); `SimPurityTests` skips compiler-generated types (`:82`).
- `install-ko.md` ends at `## 8. 공유하기`; `StarterBuilder.DeleteWhatWasNotProduced` exists; `share_check.py` has `--project/--scene`; `Tools/fixtures/0.1.0/Assets/My Level.unity` exists; tag `0.1.0` exists.
- Share pins Brotli with `decompressionFallback = false` (`ShareBuild.cs:206-207`).
- All the test files the plan modifies exist.

**Accepted on trust (not checkable here):** `EditorSceneManager.LoadSceneAsyncInPlayMode` with `Single` in a PlayMode test run; that `IProcessSceneWithReport` edits reach the built scene; that `InputAction.wantsInitialStateCheck` has a public setter in Input System 1.11.2 and makes `IsPressed()` true for a held Button on the next update; that the test runner survives a `Single` load.

## Findings

### F1 — blocking · design · Step 4 (`StageRun.PlayAgain`) and Step 3 (`StageLoad.Begin`)
**Play again in the Editor reloads the first stage from disk, so the student sees their stage without their unsaved edits.**
Unity plays the scene in memory when the student presses Play, including edits that haven't been saved. Play again with a run loads "the scene where Play began" by path through `LoadSceneAsyncInPlayMode`, which reads the saved file. Failure: a student edits Stage 1 (moves a trap), presses Play without Ctrl+S (the normal habit; Unity doesn't ask), reaches Stage 2, wins, presses a key. Stage 1 comes back with the trap in its old place. To the student this looks like lost work, which is the failure this project ranks worst, and they may redo it or panic. Their edits are still there after Stop, but nothing on screen says so. The same happens on an Untitled first scene, and it is worse there: `scene.path` is `""`, so the Play-again load has nothing to load (see F2).
The plan never says what Play again shows in this case, and no test or play-check covers it. Pick one behaviour, write it down, and test it. Cheap options:
(a) at play start, when the Player's own scene has a linked Exit and `Scene.isDirty` (a `UnityEngine` API; whether it reads true for the Play-mode copy is for the Author to check), log a plain warning and show a HUD line: "Save this scene (Ctrl+S) so the next stages and Play again use your latest changes";
(b) an editor-side save prompt on entering Play mode from a scene with a linked Exit (editor tools, no gameplay logic);
(c) Play again after a stage chain goes back in place when the first scene is the Play-mode scene. This is wrong, because that scene is already unloaded.
I'd pick (a), because it adds no editor hook. Whatever is picked, add it to the Step 11 play-check's *Must be absent*: "Play again shows an older stage with no message".

### F2 — non-blocking · design · Step 3, item 3 ("a null operation logs an error naming the scene and fades back")
**What the player sees after a failed load is undefined, and as written it is a soft lock.** After fading back, the old Player is still in `Won` with `leaving = true`. The win screen and Play again are both gated on `!leaving`, so there is no win screen, no Play again and no message, only a Console error. This happens in the Editor when the target is deleted or moved outside Unity during Play, when the first scene is Untitled (F1), and in a student's own Build Settings build that leaves out Stage 2 (the stamper writes the path, and `LoadSceneAsync` returns null at run time). Fix: on a null operation, clear `leaving` and take Step 2's Broken path (`hud.ShowMissingStage()`, so Play again restarts the stage in place). Add one test: delete the target scene after play start, touch the Exit, and expect the notice with Play again working.

### F3 — non-blocking · design · Steps 1, 6 and 11 (multi-scene Play)
**Stages make "two scenes open in the Hierarchy" likely, and then two Players run at once.** Dragging a scene asset into the Hierarchy instead of the Next stage slot opens it additively. That is an easy miss while a student is learning to drag scenes around for this phase. On Play, both scenes' Players start, with two HUDs, two cameras, and two sims reading each other's Tilemaps (`ScanTilemaps` and the level scans search every loaded scene). Today this is a rare edge; after this phase it is an expected student mistake. Step 6 fixes it for Share only. Add a play-start warning when another loaded scene holds an active Player, naming the scene and how to remove it (right-click, then Remove Scene), and add the case to the Step 11 editor check.

### F4 — non-blocking · accuracy · Step 3 tests (`StagePlayTest`)
**The plan picks the wrong base class, and two tests can't work under the harness as written.**
- `SavedScenePlayTest` (`Tests/PlayMode/SavedScenePlayTest.cs`) already loads a saved scene through `LoadSceneAsyncInPlayMode`, finds the one Player after the input fixture is up (its comment says why that order matters), and unloads in teardown. Base `StagePlayTest` on it, or on what it shares, rather than straight on `MovementPlayTest`.
- `MovementPlayTest` sets `Time.captureFramerate = 60`, so every frame's `deltaTime` is exactly 1/60 s. "With a simulated long frame at load" can't come from the scene load itself. Say how it is injected: for example, call `RunFrame(0.5f)` on the arrived Player, or override `CaptureFramerate` for that test.
- The PlayHud comment says screenshots are drawn through the camera. A `ScreenSpaceOverlay` fade won't appear in `stages-arrival.png`, so only the alpha assertion and Step 8's browser check prove black. That is fine, but say so, so nobody reads a fade-free screenshot as a failure.

### F5 — non-blocking · accuracy · Step 8 (`share_check` browser step)
**Brotli over plain HTTP needs a first-run check.** Share builds Brotli with `decompressionFallback = false`. The plan serves the build over `http://127.0.0.1` with `Content-Encoding: br`. Whether Chrome decodes `br` on a plain-HTTP loopback origin (as opposed to `localhost`, or HTTPS) is a prediction I can't check from here. If it doesn't, Unity's loader stops with its "server not configured" error, and the gate fails in a way that looks like a stage bug. Confirm it on the first run, bind to `localhost` if that's what works, and make the script print the loader's error text when Unity never starts. `perf_check.py`'s `Handler` sets no `Content-Encoding` today, so the extension is new code, not a reuse.

### F6 — non-blocking · design · Step 8 (Stage A bench)
**The "Stage A's colour" sample has almost no clean frames.** The Player starts on the Exit, so the sim wins on tick 1 and the fade starts on the first frame Unity draws (alpha around 0.07 to 0.13 after one frame; about 8 to 15 frames to black). The page also sees the canvas before Unity's first frame. State the match rule: for example, "a frame whose mean is within N of Stage A's colour, scaled by the frame's darkening", or "the first non-empty frame's hue". Or delay the change by a beat with no input, for instance a short walk off a ledge onto the Exit, so A is seen clearly.

### F7 — non-blocking · accuracy · Step 1 (`RuntimeGuardTests`)
**The static-field rule differs from `SimPurityTests`, and the plan says "as `SimPurityTests` does" without saying which part.** `SimPurityTests` allows only `const`, or `static readonly` of a value type or `string` (`:90-91`). The plan's rule is "no static non-const, non-`readonly` field". `PlayHud.Skull` is a `static readonly string[]`: it passes the plan's rule and fails the sim's. An implementer who copies the sim test will see a red test on day one. Say which rule applies and that `PlayHud.Skull` (and `Checkpoint.ReachedTint`, a value type) is expected to pass.

### F8 — non-blocking · accuracy · Source and Step 10 ("Decisions 1–7", "decision 7")
**The plan points to numbered decisions it never lists.** Source says "Decisions 1–7 and the accepted panel recommendations are not in the GDD yet; this prompt is their home until Step 12". Step 10 checks "decision 7". Nothing in the document is numbered 1–7. "Notes to GDD → New decisions" lists six unnumbered items, and the one-scene promise is in the Overview. Number them in one place (Notes to GDD) and refer to them by number, so Step 12's write-back has a fixed list.

### F9 — non-blocking · accuracy · Steps 9 and 1 (Starter rows and install check)
- `StarterFrozen.txt` has one row per file, with the `.meta` on its own row (for example `Prefabs/Exit.prefab` and `Prefabs/Exit.prefab.meta`). The new `Stage Player.prefab` adds two rows, not "one row only".
- `install_check.py` has a single `PLAYTHROUGH` constant (`:33`). "Add it to the required tests" means turning that into a list, with the skip check per test. Name that so the second playthrough can't be required in name only.

### F10 — non-blocking · design · Step 3 (fade canvas order)
`PlayHud` draws at `sortingOrder = short.MaxValue`: in camera space when there is a camera, and as an overlay when there isn't. The fade is always above the HUD only because an overlay canvas draws after the cameras. In a stage with no Main Camera (Play mode only, since Share stops on it), both are overlays at the same top order, and their order is undefined. Say "the fade canvas is ScreenSpaceOverlay; it covers a camera-space HUD by render order", and either accept the no-camera case or draw the HUD below `short.MaxValue`.

## Recommendations (the operator's decisions)

### R1 — recommendation · design · pending: "a chain loop stops Share"
**Reviewer recommends: stop only when no Exit on the reachable stages can win. Warn, and don't stop, on a back-link when a win is reachable.** "This Exit sends you back to the start" is a classic troll joke in the genre the course opens with, and the plan's own note says the stop forbids it. The walk already keeps a visited set, the run already handles any order (a death total, a remembered first scene), and Play again doesn't care. A pure loop with no winning Exit still stops, naming the scenes. The display chain then needs a back-link line ("Stage2 can also go back to Stage1") under the main path.

### R2 — recommendation · design · pending: Exits with different links
**Reviewer recommends: as proposed (warn, naming the scenes, and build everything reachable).** It is the only option that never ships a game with a missing stage.

### R3 — recommendation · design · pending: Play again returns to where Play began
**Reviewer recommends: as proposed, with F1 settled.** In a build it is always stage 1, and in the Editor it matches what the student pressed Play on. A hidden search for "the real first stage" would surprise more than it helps.

### R4 — recommendation · design · pending: no "differing Players" message
**Reviewer recommends: as proposed.** Differing Players are a style choice, not an error, and a message would teach students to fear a legitimate design. The Starter's Prefab Player teaches the habit.

### R5 — recommendation · design · pending: the first `#if UNITY_EDITOR` in Runtime (`StageLoad.cs`)
**Reviewer recommends: accept it.** Loading a scene that isn't in Build Settings during Play needs `EditorSceneManager`. The alternatives are a static hook the editor assembly fills in (static mutable state, banned by rule 5), an injected loader with no natural owner to inject it, or writing Build Settings in Play mode (ruled out by the one-scene promise). One guarded file, pinned by the source-scan guard, is the smallest option.

## Reviewer's view
I would build this once F1 is settled. The architecture is sound: a GUID link resolved once at play start, a fail-closed built path, one non-static owner for the cross-stage total, and a fade order with tests for each frame boundary. I checked every code premise I could reach, and each one matched. F1 is the one gap where a student sees something that looks like lost work. F2 and F3 are cheap guards on the same student path. F4–F10 are precision fixes to tests and wording. R1 is the one recommendation that changes the design.
