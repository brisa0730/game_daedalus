# Phase 8: Stages

## Overview

**Where things stand:** A troll game made with this tool is one long level. Reaching the exit always ends the game with the win screen, so a student can't make a game with several separate levels.

**What this phase changes:**
- An exit can send the player to another level, called a stage, which the student picks by dragging it in.
- A quick fade to black covers the change, and the player and traps wait until the new stage is showing.
- Deaths add up across the whole game, and the win screen appears only at the very end.
- Share my game finds every stage, checks each one and shows the order before building.
- The Starter gets a small example of several stages. Games with one level play as before.

**How you'll know it worked:** A student links two scenes, shares the game, and a classmate plays straight from one stage into the next in the browser.

**Not in this phase:** Choosing a stage from a menu, or coming back to a stage after closing the game.

---

## Source
- **GDD sections:** `GDD.md` § Design Pillars (5), § Core Loop, Decided lines; `systems.md` § Combat & Damage → Death and respawn, § World & Level Elements, § Editor Tools, § Share; `entities.md` § Placeable elements (Exit); `architecture.md` § Time Model, § Data Flow. **Decisions 1–7 (numbered in `Notes to GDD`) and the accepted panel recommendations are not in the GDD yet; this prompt is their home until Step 12.**
- **GDD version:** v0.25
- **Depends on:** Phases 3–4 (`LevelStepper`, Exit, HUD), Phase 6 (Share my game, `BuildScenes.Use`), Phase 7 (Starter, frozen pieces, 0.1.0 fixture).

**Settled by the operator (2026-11-09, as recommended; `Notes to GDD` carries them):** a chain in which no reachable Exit can end the game stops Share, naming the scenes, while a back-link in a chain that can be won warns and is built; two Exits with different links (or one empty, one linked) are allowed with a named warning and everything reachable is built; Play again after pressing Play on a later stage returns to the scene where Play began (in a build, always stage 1), with no hidden search; no "differing Players" message on Share, the Starter's Prefab Player teaches the habit.

**Reviewer's recommendations on those (the operator decides; recorded, not folded):**
- Chain loop. Reviewer recommends: stop only when no Exit on the reachable stages can win; warn, and don't stop, on a back-link when a win is reachable, with a back-link line under the main path ("Stage2 can also go back to Stage1"). A send-you-back Exit is a classic troll joke, and the run already handles any order (round 1, R1). Author's note: this is the one that changes the design. Step 6 would stop only on a chain with no winning Exit, and walk a visited set for the rest.
- Exits with different links. Reviewer recommends: as proposed; it is the only option that never ships a game with a missing stage (round 1, R2).
- Play again returns to where Play began. Reviewer recommends: as proposed, with the unsaved-edits case settled, now in Step 4 (round 1, R3).
- No differing-Players message. Reviewer recommends: as proposed; differing Players are a style choice, not an error (round 1, R4).
- The first `#if UNITY_EDITOR` in Runtime (`StageLoad.cs`). Reviewer recommends: accept it; the alternatives are a static hook (rule 5), an injected loader with no natural owner, or writing Build Settings in Play mode (round 1, R5).

**Paths:** `P/` = `Packages/com.orcapafea.daedalus/`.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `P/Runtime/Exit.cs` | The Next stage setting, its built path, the link read |
| NEW | `P/Runtime/ExitLink.cs` | Value type: none / goes to a scene / broken |
| NEW | `P/Runtime/StageLoad.cs` | Link resolve and scene load; the only file in Runtime naming `UnityEditor` |
| NEW | `P/Runtime/StageRun.cs` | Owner of the go-next path, the fade and the carried death total |
| MODIFY | `P/Runtime/Sim/LevelStepper.cs` | `WonExit` |
| MODIFY | `P/Runtime/Sim/LevelRules.cs` | `StageFadeSeconds` |
| MODIFY | `P/Runtime/PlayerSession.cs` | `WonExit`, clock reset, doc of the per-stage counter |
| MODIFY | `P/Runtime/Player.cs` | Links, leaving, hold, carried total, Play again |
| MODIFY | `P/Runtime/PlayerControls.cs` | Held directions carry over on arrival |
| MODIFY | `P/Runtime/PlayHud.cs` | Missing-stage notice, "999+" |
| MODIFY | `P/Runtime/PlayerWarnings.cs`, `P/Runtime/HelpTexts.cs`, `P/Runtime/PlayStart.cs` | Texts; doc of what a start resets |
| MODIFY | `P/Editor/ExitEditor.cs` | Scene slot, story help box, prefab warning |
| NEW | `P/Editor/StagePathStamp.cs` | Writes each Exit's scene path into the built copy of the scene |
| NEW | `P/Editor/StageWalk.cs` | Follows the links; the stage checks; what links here |
| MODIFY | `P/Editor/ShareBuild.cs`, `P/Editor/ShareMyGame.cs`, `P/Editor/ShareTitleWindow.cs`, `P/Editor/ShareTexts.cs` | Build the chain, show it, stop and warn |
| NEW | `Assets/Dev/Editor/StageTestSceneBuilder.cs` and generated `Assets/Dev/StageTest/` | Two-stage Share bench |
| MODIFY | `Tools/share_check.py` | Stage bench build, real browser check |
| MODIFY | `Assets/Dev/Editor/StarterBuilder.cs`, `Samples~/Starter/` (new `Stage Example/` and `Prefabs/Stage Player.prefab`), `StarterFrozen.txt` (new rows only) | The multi-stage example |
| MODIFY | `P/Documentation~/install-ko.md`, `Tools/install_check.py` | Stage steps after the first share; the example is played |
| NEW | `P/Tests/PlayMode/Stages/StagePlayTest.cs`, `StageChangeTests.cs`, `StageScenes.cs` | Real scene-to-scene loads |
| NEW | `P/Tests/PlayMode/StarterStagesPlaythroughTests.cs` | Plays the imported example |
| NEW | `P/Tests/EditMode/Share/StageWalkTests.cs`, `P/Tests/EditMode/Runtime/ExitLinkTests.cs`, `P/Tests/EditMode/RuntimeGuardTests.cs` | Walk, link, guards |
| MODIFY | `TrapLayoutTests.cs`, `CheckpointExitTests.cs`, `ShareBuildTests.cs`, `HudAndWinTests.cs`, `HelpTextsTests.cs`, `StarterSampleTests.cs`, `InstallStepsTests.cs` | Pins and regressions |
| MODIFY | `Documents/GDD/*`, `Documents/Operations/Roadmap.md`, `Deferred.md`, `Changelog.md`, `.claude/memory/decisions.md`, `.claude/memory/framework.md` | Step 12 |

---

## Steps

### Step 1: The Next stage link
- **MODIFY `Exit.cs`:**
    - Add `[SerializeField, Tooltip(...)] string nextStage = ""`: the asset GUID of a scene. A GUID, so links survive rename and move, and "empty" is data rather than an absence.
    - Add `[SerializeField, HideInInspector] string builtNextStagePath = ""`, written only into the built copy of a scene.
    - Add `internal ExitLink Link => StageLoad.Resolve(nextStage, builtNextStagePath)`, plus an internal setter and stamp method for the editor tools and tests.
    - Update the doc comment. `dataVersion` stays 1: the fields are additive, as the Trap's were in Phase 4. Both field names are permanent.
- **NEW `ExitLink.cs`:** `readonly struct` with `Kind` (`None` 0, `Scene` 1, `Broken` 2, explicit values) and `Path`.
- **NEW `StageLoad.cs`** (internal static):
    - `Resolve(guid, builtPath)`. Empty GUID gives None.
    - Under `#if UNITY_EDITOR`: `AssetDatabase.GUIDToAssetPath`, then `AssetPathToGUID(path, OnlyExistingAssets)` (the `BuildScenes` idiom) and a `.unity` check; else Broken.
    - Otherwise: a pure `ResolveBuilt(guid, builtPath)`, empty built path = Broken (fail closed, never win). Keep `ResolveBuilt` compiled everywhere so EditMode tests it.
    - The doc comment names this as the one sanctioned Runtime use of `UnityEditor`.
- **NEW `StagePathStamp.cs`:** `IProcessSceneWithReport`.
    - Return at once when `report == null` (Play mode).
    - For every Exit under the scene's roots (inactive included) write the resolved path; Broken on an enabled Exit throws `BuildFailedException` naming scene and Exit.
    - It only touches the build's copy of the scene.
- **MODIFY `HelpTexts.cs`:** replace `ExitNotice` with `ExitStory(ExitLink, string sceneName)`: "Ends the game with the win screen." / "Goes to Stage2." / a broken text saying the scene was deleted or moved outside Unity and to drag it in again. Add `ExitOnPrefabAsset` (the slot on a prefab links every placed copy to one scene; set it on the placed Exit in each scene).
- **MODIFY `ExitEditor.cs`:**
    - Replace `DrawDefaultInspector` with a `SceneAsset` object field labelled Next stage, drawn through `SerializedProperty` (`BeginProperty`, `showMixedValue`, Undo, prefab-override bar).
    - Help box: story (Info), broken (Error), mixed selection (a plain line).
    - When editing a prefab asset or in Prefab Mode, always add `ExitOnPrefabAsset` as a Warning.
    - No gameplay logic; the story comes from `HelpTexts`.
- **Tests:**
    - `TrapLayoutTests`: Exit's visible paths are `m_Script`, `nextStage`, with a tooltip and version 1. Split the shared loop so Checkpoint's pin is unchanged. Add 0.1.0-shape JSON with no new field loads as None.
    - `ExitLinkTests` (temp scenes in `Assets/DaedalusTestTemp`): none / scene / deleted = Broken / survives `MoveAsset`; `ResolveBuilt` both ways; stamper writes paths, skips on a null report, throws on broken.
    - `HelpTextsTests`: three stories.
    - **NEW `RuntimeGuardTests`:** reflection finds no static field in the runtime assembly that is neither `const` nor `readonly` (Architecture rule 5; compiler-generated types skipped, as `SimPurityTests` does at `:82`). This is deliberately looser than `SimPurityTests`' value-type-or-`string` rule (`:90-91`): `PlayHud.Skull` (`static readonly string[]`) and `Checkpoint.ReachedTint` must pass. Like the sim test, it fails if it checked no fields. Source scan finds `UnityEditor` only in `StageLoad.cs`.
- **Verify:** `[automated]` `python Tools/unity_tests.py` (both editors). Operator editor check in Step 11.
- **Depends on:** none.

### Step 2: Which Exit won, and the win screen honours it
- **MODIFY `LevelStepper.cs`:** `WonExit` (−1 until won; reset in `Begin`, set in the exit loop before `return`). Nothing else in the sim changes; `Won` already freezes everything.
- **MODIFY `PlayerSession.cs`:** expose `WonExit`.
- **MODIFY `Player.cs`:**
    - Keep `ExitLink[] exitLinks` from play start (same order as the sim's exits); one warning per Broken link at play start (`PlayerWarnings.NextStageMissing`, Exit as context).
    - At play start, when an active Player sits in another loaded scene, one warning (`PlayerWarnings.PlayerInAnotherScene`) naming that scene and how to take it out (right-click the scene in the Hierarchy, Remove Scene). Dragging a scene into the Hierarchy instead of the Next stage slot opens it alongside, and both Players then run and read each other's Tilemaps (`ScanTilemaps` searches every loaded scene).
    - In `RunFrame`, the first frame `Won` is seen: Scene kind sets `leaving = true`.
    - The win screen and Play again use `won && !leaving`, computed before `Render` in that same frame.
    - Broken shows the HUD notice instead of the win text.
- **MODIFY `PlayHud.cs`:** keep the title `Text` as a field; `ShowMissingStage()` sets the title to a plain "This Exit's next stage is missing. Fix it in the Inspector." and blanks the deaths line. A Broken link never reads as a win; the prompt stays so the student can continue. `ClearNotice()` restores "You made it!" and the current deaths line (today the title is set once at build and the deaths line only when the count changes, so a notice would otherwise stay). `Player` calls it on every in-place restart, so a later win on an empty Exit reads normally.
- **Tests:**
    - Sim scenario in `CheckpointExitTests`: two exits, `WonExit` names the one touched, −1 after `Begin`, no allocation.
    - `HudAndWinTests`-style code-built level: empty link wins as today, Broken shows the notice and no "You made it!", a linked Exit never activates `hud.Win` on any frame.
    - A second scene loaded additively with an active Player: the warning names it. One Player: no warning.
    - Existing tests stay green.
- **Verify:** `[automated]` both editors, both modes.
- **Depends on:** Step 1.

### Step 3: The stage change, one path
- **MODIFY `LevelRules.cs`:** `StageFadeSeconds = 0.25f` (one length, each way; the operator tunes it by hand). Mirror it in `entities.md` at Step 12.
- **NEW `StageRun.cs`** (`[AddComponentMenu("")]`, sealed, internal, `DontDestroyOnLoad`):
    - Created only at the first linked Exit; found by one `PlayStartScan.FindActive<StageRun>()` at each Player start (no static field anywhere).
    - Builds its own black overlay canvas (`ScreenSpaceOverlay`, top sorting), with no raycast target. It covers the HUD by render order: the HUD is a camera-space canvas when a camera exists (`PlayHud.cs:63`), and overlays draw after cameras. With no Main Camera both are overlays at the same order and the HUD may show over the fade: accepted, since that is Play mode only (Share stops on it) and lasts one fade.
    - One coroutine, in this exact order:
      1. Fade to alpha 1 (each frame's step clamped to 1/30 s, so a hitch can't skip the fade).
      2. `WaitForEndOfFrame`, then `yield return null`, so black has been presented before anything blocks the main thread.
      3. `StageLoad.Begin(path)` until done. An empty path, a null operation or an exception from the load is a **failed load**: log an error naming the scene, set `LoadFailed`, and fade back. The player is never left on a screen with no message and no way on (Step 4).
      4. `yield return null`, then `WaitForEndOfFrame`: the new Player's `Start` has run and its first frame has been drawn under black.
      5. `yield return null`: drop the load's long frame.
      6. Fade to 0.
      7. Mark the reveal finished.
    - Expose read-only `Revealing`, `CarriedDeaths` and `LoadFailed`.
- **MODIFY `StageLoad.cs`:** `Begin(path)`: under `#if UNITY_EDITOR`, `EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single))`; otherwise `SceneManager.LoadSceneAsync(path, LoadSceneMode.Single)`. Build Settings are never touched in Play mode.
- **MODIFY `Player.cs`:**
    - At `leaving`, once: find or create the run and call `run.GoNext(path, session.Deaths)`.
    - In `StartPlay`: find the run; if it is revealing, hold.
    - In `RunFrame` while holding: read controls, skip the clock and the ticks, `Render`, return (no allocation).
    - When the reveal ends: `session.ResetClock()`, `controls.ClearLatch()`, stop holding. The first tick comes from one ordinary frame's time, so no catch-up ticks.
- **MODIFY `PlayerSession.cs`:** `ResetClock()` (`accumulator.Reset`).
- **Tests:**
    - **NEW `StageScenes.cs`:** `IPrebuildSetup` / `IPostBuildCleanup` writing Stage A → B → C as real scene files built from code, with saved tile assets (the compat project reloads the domain on Play, framework.md), under a temp `Assets/` folder. Each has a Player, Main Camera, Global Light and distinct backgrounds.
    - **NEW `StagePlayTest`:** base on `SavedScenePlayTest` (it already loads a saved scene and finds the one Player after the input fixture is up; called from the test, not `[UnitySetUp]`). Its first load is additive; the Exit's load is `Single`, so the test re-finds the Player after arrival. Teardown creates an empty scene, unloads whichever stage is left and destroys any `StageRun`.
    - **`StageChangeTests`**, a real load through the real Exit link:
      - `hud.Win` is false every frame through arrival.
      - No death burst.
      - The load starts at least 2 frames after alpha reaches 1.
      - Alpha is 1 on the frame the new Player first has a session.
      - The new Player's position and tick count don't change during the reveal, then do.
      - A long frame can't come from the load itself, because `MovementPlayTest` sets `Time.captureFramerate = 60`. Inject it by calling `RunFrame(0.5f)` on the arrived Player while it holds: no ticks run, and after the reveal one ordinary frame runs one tick.
    - Screenshot `stages-arrival.png`. A `ScreenSpaceOverlay` fade doesn't appear in these screenshots (they are drawn through the camera), so a fade-free picture is not a failure: the alpha assertions and Step 8's browser check prove black.
- **Verify:** `[automated]` both editors, both modes; look at `TestResults/*-stages-*.png`.
- **Depends on:** Step 2.

### Step 4: One death total, and Play again
- **MODIFY `Player.cs`:** `carriedDeaths` read once at `StartPlay`; `hud.Show(carriedDeaths + session.Deaths, ...)`. Play again on the win screen: `run != null && !run.LoadFailed` calls `run.PlayAgain()` and sets `leaving`; otherwise `session.Restart(firstStart)` exactly as today.
- **Failed load:** while `leaving`, a Player that sees `run.LoadFailed` clears `leaving` and shows `hud.ShowStageLoadFailed(sceneName)` in place of the win text ("Couldn't open {scene}. Check that it is saved and still in the project."; an empty path reads "the first scene"). Play again then restarts this stage in place, so retrying can't repeat the failure. Earlier stages' deaths stay on the counter after that restart (not zeroed): this is a dead end almost only reached in the Editor, and it adds no second reset path.
- **MODIFY `StageRun.cs`:** `GoNext` clears `LoadFailed`, adds to the total only once the load has started (so a failed load and its retry can't count a stage's deaths twice), and remembers the first scene (the Player's own scene path at creation, the scene where Play began). It also remembers `FirstSceneUnsaved`: that scene's `isDirty` when the run is created, while it is still loaded. `PlayAgain` zeroes the total only once the load has started, runs the same path to the first scene, and destroys the run when the reveal ends.
- **Play again and unsaved edits:** Unity plays the open scene from memory, unsaved edits included, while a load by path reads the saved file. So after a stage chain, Play again shows the last saved first stage. Nothing is lost (the edits are still there after Stop), but it looks like lost work. When `FirstSceneUnsaved` is true, the win screen adds one line where Play again is offered, and the Console gets the same text: "Play again uses the last saved {scene}. Your changes aren't lost: Stop, then press Ctrl+S." It says Stop first because Unity doesn't save during Play. `isDirty` is always false in a build, so shared games never show it.
    - `isDirty` on a Play-mode scene is a prediction batch mode can't check (a test run can't enter Play with a dirty scene); the Step 11 editor check does. If it reads false, replace the line with an editor-side prompt on entering Play from an unsaved scene with a linked Exit. It only asks, leaving the save to the student: the editor tools never save for them (decision of 2026-11-09).
- **MODIFY `PlayHud.cs`:** cache one more entry (`CachedCounts = 1001`), last index "999+" / "Deaths: 999+", index `Mathf.Min(deaths, 1000)`. Exactly 999 still reads 999.
- **Docs of the reset:** `PlayStart.cs`, `LevelStepper.Begin`, `PlayerSession.Restart`: the session's counter is per stage; the run owns the game total.
- **Tests:**
    - Deaths carried A → B on HUD with no bump on arrival; a death in B adds and bumps.
    - Win screen shows the total.
    - Play again on C goes to A, deaths 0, no run left afterwards.
    - Play begun on B then B → C, Play again returns to B.
    - Play begun on the last stage plays again in place, no run.
    - `HudAndWinTests`: a one-scene game has no `StageRun`, plays again in place with no load and no fade.
    - "999+" at 1000 and above, 999 unchanged.
    - Failed load: delete the target scene after play start and touch the Exit (`LogAssert.Expect` the error): the notice shows, no "You made it!", and Play again restarts in place. The same with an empty first-scene path at Play again. After either, a later win on an empty-link Exit reads "You made it!" with a deaths line.
    - The win screen's unsaved line, driven through an internal seam on the run: shown when set, absent when not.
    - `PlayerAllocationTests` stays green.
- **Verify:** `[automated]` both editors, both modes.
- **Depends on:** Step 3.

### Step 5: Input across a stage change
- **MODIFY `PlayerControls.cs`:** `KeepHeldDirections()`: disable the four Direction actions, set `wantsInitialStateCheck = true` on them, enable. Jump and Play Again untouched. Doc: a new action doesn't see a key already held.
- **MODIFY `Player.cs`:** call it only when arriving by a stage change (run present at `StartPlay`). A first Play is untouched.
- **Tests:** real load with Right held: the player moves right on the first tick after the reveal. Jump held through the change: no jump; release and press: jumps. A jump pressed during the reveal is dropped. One-scene first Play with Right held behaves as before.
- **Verify:** `[automated]` both editors (`InputTestFixture`, Input System 1.11.2 on 6000.0). Operator play-check in Step 11.
- **Depends on:** Step 3.

### Step 6: Share follows the links
- **NEW `StageWalk.cs`** (Editor, run once per Share; no gameplay logic): `StageWalk.From(startScene)` opens each unloaded stage additively, reads it, and closes only what it opened (active scene unchanged, an already-open scene stays open). It follows every enabled Exit with a visited set, so a back-link ends the walk there. It returns `Stages` (start first, walk order), `Stop` (first stop message or null), `Warnings` and the display chain ("Stage1 → Stage2 → win"; each back-link adds a line under it, "Stage2 can also go back to Stage1").
    - **Stops, each naming the scene and object:** broken link on an enabled Exit; **no way to win** (no enabled Exit with an empty link on any reachable stage, naming the scenes: "Stage1 → Stage2 → Stage1 never ends"); a stage with no active enabled Player; a stage with no enabled Main Camera.
    - **Warnings:** a stage with no Exit; a back-link in a chain that can be won (a fake Exit that sends you back is a legitimate troll, but duplicating a scene copies its link by accident, so it is named); Exits in one stage that go to different places; another scene links to this start.
    - **What links here (once per Share):** `AssetDatabase.FindAssets("t:Scene", new[]{"Assets"})`, skip the start, `File.ReadAllText(path).Contains(guid)`. A warning, never a stop: "{Scene} goes to this scene. If it comes first, share from it instead: stages before this one won't be in your game." When `EditorSettings.serializationMode` isn't Force Text, warn that it couldn't be checked. Record the time in the phase log.
- **MODIFY `ShareBuild.cs`:**
    - `SharedPlayer` and the camera check look only in the active scene (`gameObject.scene`; replaces `Camera.main` in the stop and the picture). This resolves the Deferred entry.
    - `Run`: save the active scene, walk, stop if `Stop`, save any other open dirty stage on the chain, then pass the stage list to `BuildScenes.Use` and `BuildPlayerOptions.scenes`.
    - `SceneMissing` still applies.
- **MODIFY `ShareTexts.cs`:** new stop and warning texts (plain, no code words, under 350 characters; nothing named `Gallery(Upload|UpdateMyGame|NewGame)`). `WindowNote` says stages are saved first.
- **Tests:** `StageWalkTests`: linear chain, broken, a loop with no winning Exit stops, a back-link with a winning Exit warns and builds every stage, no Player, no camera, no Exit, two Exits both built, a disabled Exit ignored, what-links-here found and absent, the rename/move survival, scenes opened are closed. `ShareBuildTests`: a Player or camera in another open scene doesn't count; one-scene walk lists one stage ending "→ win".
- **Verify:** `[automated]` EditMode both editors; then Step 8.
- **Depends on:** Step 1.

### Step 7: The title window shows the chain
- **MODIFY `ShareMyGame.cs`:** in `Share()`, after the save prompt, walk; a stop opens the dialog and ends; otherwise pass the chain and warnings to the window. The title key stays the start scene's GUID.
- **MODIFY `ShareTitleWindow.cs`:** show "Your game: Stage1 → Stage2 → Stage3 → win", warnings below in a scroll view, window tall enough (no fixed 170 px). `Run` re-walks and stays the authority.
- **Tests:** chain text in `StageWalkTests` for one scene and several. The window itself is the Step 11 editor check.
- **Verify:** `[automated]` EditMode both editors; `[manual]` Step 11.
- **Depends on:** Step 6.

### Step 8: Proof on a real multi-stage WebGL build
- **NEW `StageTestSceneBuilder.cs`** (pattern of `TrapTestSceneBuilder`, self-check, stable GUIDs, committed output):
    - **Stage A**, about 40 × 22, its Player placed several tiles above its Exit, so it falls onto it with no input after at least 0.5 s of clean Stage A frames, bright background.
    - **Stage B**, about 200 × 60 painted (a size that keeps the scene file small in git), different bright background, Exit out of reach.
    - Both backgrounds must be far from black.
- **MODIFY `share_check.py`**, for each editor after the trap bench:
    - Share `Assets/Dev/StageTest/Stage A.unity`, the same zip checks, gallery check and settings-restored check.
    - Extract the zip, serve it on loopback with `Content-Encoding: br` and the right types (`perf_check.py`'s `Handler` sets no encoding today and binds `127.0.0.1`, so this is new code on that pattern; no new dependency), with a small page calling `createUnityInstance` with `preserveDrawingBuffer`. Whether Chrome decodes `br` over plain HTTP on `127.0.0.1` is a prediction: confirm it on the first run and bind to `localhost` if that is what works. When Unity never starts, the script prints the loader's error text, so a server problem can't look like a stage bug.
    - The page samples the canvas each animation frame, ignoring frames before Unity's first non-empty one, and must see, in order: Stage A's colour (mean within a stated tolerance, on a frame from the clean fall), a fully black frame, Stage B's colour stable for 0.5 s. It also records the longest frame interval across the change, then posts a PNG and JSON.
    - Gates: order seen; Stage B covers at least 0.4 of the pixels; hitch under 5 s (the game must not look hung). Write `TestResults/<unity version>-share-check-stage2.png` and print zip bytes and growth over the trap bench.
    - Confirm the game keeps running without focus or that Chrome is focused (`PlayerSettings.runInBackground`).
- **Measure and record in the phase log** (one-off, not committed):
    - Stopwatch `Debug.Log` lines around `new PlayHud` and `ReadMap`, forwarded by the page.
    - A one-off full 500 × 200 Stage B build, for hitch and zip size against 50 MB.
    - Rule: if `PlayHud` construction exceeds 2 ms on the Share build, `StageRun` holds the string tables and hands them to each new HUD. Otherwise leave it per Player.
- **Tests:** the builder's self-check; `share_check --editor all`.
- **Verify:** `[automated]` `python Tools/share_check.py --editor all`; `python Tools/share_check.py --project Tools/fixtures/0.1.0 --scene "Assets/My Level.unity"` (one scene, unchanged). `[manual]` look at both `*-share-check-*.png`.
- **Depends on:** Steps 3, 6.

### Step 9: The Starter's example and the steps
- **MODIFY `StarterBuilder.cs`:**
    - A new frozen `Prefabs/Stage Player.prefab`.
    - `Stage Example/Stage 1.unity` and `Stage 2.unity`: Player from that prefab, the existing frozen `Exit` prefab with Next stage set on the placed instance. Do not re-save frozen pieces; add self-checks for each stage.
    - `DeleteWhatWasNotProduced` keeps them.
- **MODIFY `StarterSampleTests`:** the new folder's scenes are exempt from the frozen list as `Starter.unity` is; check their prefabs are in the sample.
- **MODIFY `install-ko.md`:** a new section **after 8 (공유하기)**, so sharing one level never depends on it: save, **Ctrl+S before duplicating a scene (Ctrl+D)**, repaint, drag the next scene into the placed Exit's Next stage, use a Player prefab so stages feel the same, Share from stage 1.
- **MODIFY `InstallStepsTests`:** the section comes after the share section; Ctrl+S before Ctrl+D.
- **NEW `StarterStagesPlaythroughTests`** (ignored unless imported, like the Starter's): plays Stage 1 through Stage 2 with the real keyboard and link. In `install_check.py`, turn the single `PLAYTHROUGH` constant (`:33`) into a list and run the skip check (`:343`) per test, so a skipped second playthrough fails rather than being required in name only.
- **Verify:** `[automated]` `python Tools/build_starter.py --refreeze` (adds the new prefab's two rows, the file and its `.meta`; frozen rows unchanged; `Starter.unity` no diff), then `python Tools/install_check.py --editor all`; look at `TestResults/starter-picture.png` and `TestResults/*-starter-*.png`.
- **Depends on:** Steps 3, 4.

### Step 10: Run the whole suite on the one-scene promise
- Confirm decision 7 end to end: `FixtureProjectTests` through `install_check` passes; the trap bench still shares; nothing writes to `ProjectSettings/EditorBuildSettings.asset` in Play mode.
- **Verify:** `[grep]` the Runtime has no `SceneManager` outside `StageLoad.cs` and no `DontDestroyOnLoad` outside `StageRun.cs`. `[automated]` Step 13 covers the suite.
- **Depends on:** Steps 1–9.

### Step 11: Operator checks (documented; report them, don't block review)
- **Operator editor check.**
  - *Do:* on an Exit, drag a scene into Next stage; read the help box; rename and move the target; delete it; select the Exit prefab asset; open Share from stage 1, from stage 2, and with a broken link. Drag a second scene into the Hierarchy and press Play. Move a trap in stage 1, press Play without saving, finish the chain and read the win screen, then press Play again.
  - *Observe:* the three stories; link survives rename and move; deleted target shows an error distinct from empty; prefab warning; the window lists "A → B → win"; warnings name scenes; broken link stops with a named dialog; the two-Players warning names the second scene; the win screen's unsaved line appears (this also answers whether `isDirty` reads true in Play; see Step 4).
  - *Must be absent:* a raw GUID box; a broken link reading as empty; Build Settings changing; Play again showing an older stage with no message.
- **Operator play-check.**
  - *Do:* play A → B → C holding Right through each change, with Space held on one change; die in each stage; finish; Play again; repeat in the shared build in Chrome.
  - *Observe:* the fade feels quick; the player keeps running on arrival; a held jump doesn't fire; deaths total on screen; Play again starts stage 1 at 0. Tune `StageFadeSeconds` by hand if needed.
  - *Must be absent:* any win-screen flash mid-chain; a frame of the new stage before black clears; the player or traps moving before the reveal ends; a death burst at the Exit.

### Step 12: Docs
- `GDD.md`: Pillar 5, Core Loop, new Decided line (reverses v0.09 for troll games); `systems.md` § Death and respawn (counter, Exit), § World (stages; the big map stays one scene), § Editor Tools (Next stage help box), § Share (chain, stops, warnings); `entities.md` Exit row and fade length; `architecture.md` (`StageRun`, `StageLoad`, Time Model order, rule-5 scan note, Data Flow reset); `conventions.md` (real-scene PlayMode tests); `Glossary.md` add **Stage** (Draft).
- `Roadmap.md`: add this phase to §0 when closed and remove its §1 entry (§3's narrowing and the requirements on `combat-and-enemies` and `world-systems` were written at planning).
- `Deferred.md`: delete "Share's checks look at every open scene". `decisions.md`: add a row for how the pending recommendations were settled (the stages row itself was written at planning). `framework.md`: `wantsInitialStateCheck`, scene loads in Play mode, Brotli localhost serving. `Changelog.md`, GDD version v0.26.
- **Verify:** `[automated]` `python Tools/unity_tests.py --platform editmode` (both editors).

### Step 13: Run Tests
- `python Tools/unity_tests.py` (both editors, both modes); inspect `TestResults/*-render-smoke.png`, `*-movement-*.png`, `*-traps-*.png`, `*-stages-*.png`.
- `python Tools/share_check.py --editor all`, `python Tools/build_starter.py`, `python Tools/install_check.py --editor all`.
- If tests fail, fix the failing tests or the code causing the failure. Do not proceed to code review until all tests pass.

### Step 14 (Final): Code Review
- Run the code-review agent (`agents/workflow/code-review.md`) on all files changed during this phase.
- Fix any HIGH/MED issues.
- Report the review result to the user.

---

## Completion Criteria

- [ ] An Exit with a Next stage goes there in Play mode with Build Settings untouched, and in the shared WebGL build.
- [ ] An empty link shows the win screen; a broken link is distinct in the Inspector, never reads as a win, and stops Share.
- [ ] A mid-chain Exit never shows or flashes the win screen; the fade order holds, and black is seen in Chrome before the next stage.
- [ ] Player and traps stay still until the reveal ends; no catch-up ticks; a held direction continues, a held jump needs a fresh press.
- [ ] One death total across the game, no static state; Play again returns to where Play began, and says so when that stage has unsaved edits.
- [ ] A failed stage load shows a named notice and leaves Play again working.
- [ ] A second loaded scene holding a Player is warned about at play start, naming it.
- [ ] Share builds every reachable stage, shows the chain, stops and warns as specified; zip contract and gallery check unchanged; the zip is measured against 50 MB.
- [ ] One-scene games play as before (0.1.0 fixture, in-place Play again, no `StageRun`).
- [ ] The Starter's example plays through import; frozen pieces unchanged; the steps come after the first share.
- [ ] Runtime guard tests pass; all suites green on 6000.0.85f1 and 6000.6.8f1.
- [ ] `PlayHud` cost and multi-stage size are measured and recorded.
- [ ] No release tag; no package version bump.
- [ ] Operator editor check and play-check documented and reported.

---

## Notes to GDD

**If anything here contradicts the Overview, fix the Overview.**

- **New decisions to write back:**
  1. Troll games may be one Unity scene per stage (reverses 2026-11-02 / v0.09 for troll games; the big map stays one scene). Goes before `combat-and-enemies`.
  2. `Exit` gets one optional setting, Next stage; empty is the win screen as today. Linear chains, no level select.
  3. Workflow: duplicate a scene, repaint. Each scene has its own Player.
  4. Between stages, a quick fade with no card and no new settings.
  5. A death stays inside its stage. One death total across the game, on screen, without static state. Play again on the final win screen goes back to the start.
  6. Share follows the links, builds every stage, checks each one and warns if shared from anywhere but stage 1. Zip contract and gallery server unchanged.
  7. One-scene games play exactly as before, in-place Play again included. The Starter gets a multi-stage example under new names.
- **Added by the handshake:** Play again after a chain shows the last saved first stage, with a win-screen line when it had unsaved edits; a failed stage load shows a named notice and Play again restarts in place; a play-start warning when another loaded scene holds a Player.
- **Settled by the operator (2026-11-09):** Share stops only when no reachable Exit can end the game, and warns on a back-link in a chain that can be won; differing Exits warn and all are built; Play again returns to where Play began; no differing-Players message; the one `#if UNITY_EDITOR` in Runtime.
- **Architecture notes:** `StageLoad.cs` is the only Runtime file naming `UnityEditor`, under `#if UNITY_EDITOR`, for scene loads without Build Settings (no reference to our editor assembly). `StageRun` is found by one play-start scan; rule 5's exception says "placed" components, so its wording needs extending.
- **What links here** is a text search of `Assets/` scenes, not asset dependencies, because the link is a GUID string. It doesn't see a link set on a prefab asset; on a non-Force-Text project it says it couldn't check.
- **Numbers:** `StageFadeSeconds` 0.25 s each way (starting value, for the operator's play-check); counter "999+" from 1000.
- **CLAUDE.md, operator-approved update only:** scope guard "rooms" becomes "walk-back rooms"; add the Stage bench command; note `share_check`'s browser step.
- **Deferred, stated on their milestones or Roadmap:** level select; stage cards or names; resuming at a stage (`world-systems`); HP and abilities carrying (`combat-and-enemies`, `world-systems`); walk-back rooms; "No Exit" in the daedalus window's list (`editor-tools`; Share warns meanwhile).

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md (with the operator-approved wording changes above).
3. Write `memory/logs/phases/phase8-log.md`, including the Step 8 measurements and the what-links-here time.
4. Update `MEMORY.md` current state.
5. If applicable: update `CLAUDE.md`, `Documents/GDD/architecture.md`, `memory/lessons-learned.md`, `memory/decisions.md`, or GDD files with any convention/rule the phase established.
6. **Deferred items.** File any leftover in `Documents/Operations/Deferred.md` (Source = this phase) with a concrete trigger; delete "Share's checks look at every open scene, but only the active one is built" in the same commit. See CLAUDE.md → Log Discipline.
7. Commit all phase work. Do not tag a release: that is the operator's call.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

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

---

## Review round 2

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
