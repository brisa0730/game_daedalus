# Plan: WebGL blank builds (ISS-001)

**Date:** 2026-11-02 | **Status:** complete

## Overview

**Where things stand:** Every web build of our project shows a blank screen. The game runs underneath, but nothing is drawn. The cause is a leftover entry in the project's scene list: it names a sample scene we deleted long ago. On newer Unity versions, the rendering setup gives up when it meets that missing scene, and the web build ships without the drawing pieces it needs. Our speed check didn't notice, because it only timed frames and never looked at them.

**What this plan changes:**
- The stale entry leaves the scene list.
- Our build scripts put exactly the scene they build into the scene list for the build, then put the old list back, and stop with a clear message if a listed scene is missing. This is the same guard the coming Share button will need.
- The speed check fails when the map and the moving squares aren't on screen, not only when frames are slow.
- The design notes that a student who deletes the sample scene would hit the same blank screen.

**How you'll know it worked:** The speed check's picture shows the tile map and moving squares, and the trap test level shows its ground and player in Chrome.

**Not in this plan:** the Share button itself. It reuses this guard when the gallery phase builds it.

**Why a plan and not a patch:** the fix spans the build scripts, the speed check and the project settings, and the speed check must be shown to fail before the fix.

**Design choice:** the build scripts set the scene list for the length of the build, instead of relying on the list being right. Unity's rendering setup reads the scene list itself, not the scenes a build names: an empty or wrong list silently drops the shaders for some 2D lights.

---

## Source
- **Trigger:** ISS-001 (Issues.md), found by the class gallery spike.
- **Related issues:** ISS-001
- **Scope:** remove the stale Build Settings entry; make the dev WebGL builds set Build Settings to the scenes they build (with a missing-scene check) and restore it; make the perf check fail closed on frames where nothing is drawn; record the student-facing risk.
- **Depends on:** the class gallery spike (v0.14): `GallerySpikeBuild` and the Chrome driver approach.

## Diagnosis (done 2026-11-02, before this plan)

- **Fresh projects draw:** fresh 2D-template projects on 6000.6.7f1 and 6000.0.85f1, with one camera clearing to magenta, draw correctly in WebGL.
- **Our project doesn't, at any level:** the same camera-only scene built in our project logs `Cannot create required material because shader is null` every frame. So do a Library-free copy of our project and that copy with a working project's whole `Library/` swapped in. Our URP assets, ProjectSettings and package code match the template or have no build hooks.
- **The trigger:** our `ProjectSettings/EditorBuildSettings.asset` lists `Assets/Scenes/SampleScene.unity`, which the scaffold deleted. The broken copy draws once the scene is restored, and with an empty scene list too.
- **The failure point:** every failing build log (for example `TestResults/perf-build.log:533`) shows `ArgumentException: Scene file not found: 'Assets/Scenes/SampleScene.unity'`, thrown twice from URP 17.6's `Light2DPrefilteringAnalysis.AnalyzeBuildScenes` (`Light2DPrefilteringAnalysis.cs:71`):
  - once in the constructor of `UpdateShaderPrefilteringDataBeforeBuild` (`ShaderBuildPreprocessor.cs:118`), during build-callback registration;
  - once in `ShaderBuildPreprocessor.OnPreprocessBuild` (`:260`).

  The analysis runs because `m_Strip2DUnusedVariants` is on, and it reads only `EditorBuildSettings.scenes`, skipping disabled entries (`:58-61`).
- **The chain to null shaders** (traced from the URP 17.6 source by the handshake Reviewer and re-checked by the Author):
  1. `GatherShaderFeatures` clears `s_SupportedFeaturesList` (`ShaderBuildPreprocessor.cs:290-291`) and reads the stripping settings, so `s_StripUnusedVariants` is true.
  2. It then throws at `:297`, before `HandleEnabledShaderStripping()` (`:311`) can refill the list. Both calls (`:118`, `:260`) throw, so the list is still empty when stripping runs.
  3. `ShaderScriptableStripper.CanRemoveVariant` starts from `removeInput = stripUnusedVariants` (`:1364`). Only an iteration over `supportedFeaturesList` resets it (`:1386`), so with an empty list **every URP variant is stripped**: Sprite-Lit-Default, CoreBlit and the rest.
  - **Still unexplained:** a native macOS build logs the same exception yet draws. The fix doesn't depend on it; it rests on the evidence above (restore the scene → draws; empty list → draws).
  - URP 17.0 (Unity 6000.0) has no `AnalyzeBuildScenes`, so 6000.0 is unaffected.
- **Why an empty list isn't enough:** with no build scenes, `AnalyzeBuildScenes` finds no light combinations, and URP strips every `Hidden/Light2D` variant (`ShaderBuildPreprocessor.cs:347-350`, `ShaderScriptableStripper.cs:1320-1329`). Our build scripts name their scenes in `BuildPlayerOptions`, which URP doesn't read.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `ProjectSettings/EditorBuildSettings.asset` | `m_Scenes: []`, removing the deleted SampleScene |
| NEW | `Assets/Dev/Editor/BuildScenes.cs` | `BuildScenes.Use(string[] scenePaths)`: checks every path exists, failing with a message naming it. It sets `EditorBuildSettings.scenes` to exactly those scenes and returns an `IDisposable` that restores the previous list. Dev-only; the gallery phase's Share build reuses the pattern |
| MODIFY | `Assets/Dev/Editor/PerfCheckBuild.cs` | Wrap only the `BuildPlayer` call, inside the existing `try`, in `using (BuildScenes.Use(new[] { ScenePath }))`, so the list is restored before the `finally` and `EditorApplication.Exit`. Exit ends the process without unwinding, so a `using` around the whole method would never restore |
| MODIFY | `Assets/Dev/Editor/GallerySpikeBuild.cs` | The same |
| MODIFY | `Assets/Dev/PerfCheck/PerfCheck.cs` | From the screenshot it already takes, report `tilePixelFraction` (pixels within ±4 per channel of any of the 4 `TileColors` or their 0.8× border shades) and `entityPixelFraction` (pixels matching the entity colour) |
| MODIFY | `Tools/perf_check.py` | Gates `tilePixelFraction >= 0.4` and `entityPixelFraction >= 0.05`, read with `.get(…, 0)` so an old build fails cleanly; docstring gate line |
| MODIFY | `.claude/CLAUDE.md` | § Build/run: the perf gate line names the two new gates |

## Steps

### Step 1: Make the perf check fail closed
- **MODIFY `PerfCheck.cs`**: after `ScreenCapture.CaptureScreenshotAsTexture()`, compute the two fractions from `GetPixels32()`. The tile colours are the `TileColors` array and the 0.8× shades `CheckerSprite` draws at each tile's edge. Add both fractions to `Result`.
- **MODIFY `perf_check.py`**: the two gates, using `.get(…, 0)`, plus the docstring.
- **Verify:** `python Tools/perf_check.py`, with the stale entry still present, **FAILS** on both fractions. Record the two readings, which should be near 0. This proves the gate catches this bug.

### Step 2: The build-scene guard
- **NEW `BuildScenes.cs`**, and **MODIFY `PerfCheckBuild.cs` and `GallerySpikeBuild.cs`** to build inside it.
- **Verify:**
  - With the stale entry still listed, `python Tools/perf_check.py` now **PASSES** every gate, including the fractions. The swap hides the stale entry from URP for the build.
  - Record both fractions and look at `perf-check.png`. If the picture shows the map and sprites but a fraction is low, the colour match is wrong (sRGB rounding, lit-sprite shading): fix the match, not the build.
  - After the build, `ProjectSettings/EditorBuildSettings.asset` still lists what it listed before, so the restore works.
  - A temporary call with a nonexistent path fails with the message naming it. This is checked once by hand and not kept.

### Step 3: Remove the stale entry
- **MODIFY `EditorBuildSettings.asset`**: `m_Scenes: []`.
- **Verify:** `python Tools/perf_check.py` passes. Look at `TestResults/perf-check.png`: the Tilemap and the moving sprites must be visible. Record both fractions.

### Step 4: Show that the trap bench draws
- Build the trap bench with `GallerySpikeBuild` (`-spikeCompression none`). Load it in Chrome through a local static server and the scratchpad driver.
- **Verify:** no `shader is null` line, and the screenshot in `TestResults/gallery-spike/` shows the ground, spikes, the player and the HUD.

### Step 5: Run Tests
- `python Tools/unity_tests.py` (both editors, both modes), then look at the render-smoke, movement and traps screenshots.
- `git status`: nothing stray is staged. Builds may dirty `.vscode/settings.json`, the URP global settings, `Assets/Settings/UniversalRP.asset` and `ProjectSettings/EditorBuildSettings.asset`. URP saves the asset during builds: `ShaderBuildPreprocessor.cs:600-601` on this project's stripping path, `:523-524` when stripping is off. A crash mid-build can leave the swapped scene list behind. Restore each one unless the change is intended.

### Step 6: Verify
- Walk the Completion Criteria.

### Step 7 (Final): Code Review
- Run the code-review agent on the changed files. Fix any HIGH/MED.

---

## Completion Criteria

- [ ] `perf_check.py` failed on the fractions before Step 2 and passes after it, with `perf-check.png` showing the map and the sprites; both fractions are recorded.
- [ ] A build leaves `EditorBuildSettings.asset` as it found it; a missing scene stops the build with its name.
- [ ] A WebGL build of the trap bench draws the level in Chrome with no `shader is null` line, and a screenshot is saved.
- [ ] `python Tools/unity_tests.py` is green on both editors and both modes, with no warnings.
- [ ] Post-Plan Cleanup records the student-facing risk for the gallery phase.

---

## Notes to Spec / Open Risks
- **Students hit this too:** on editors whose URP has `AnalyzeBuildScenes` (6000.6 here), a student who deletes the template's SampleScene, or moves it outside Unity, without fixing Build Settings gets a blank WebGL game. A list that omits the scene being shared loses its 2D light shaders. **Daedalus → Share my game** must build inside the same guard: the shared scenes in Build Settings for the build, the old list restored afterwards, and a plain message naming any missing scene. That goes into the gallery phase prompt and systems.md § Share.
- **The risk is wider than one missing scene:** any exception escaping `GatherShaderFeatures` gives a blank WebGL game. A listed scene that exists but fails to open as a preview scene would do it, and so would a future URP throw in that method. The Share guard prevents the known trigger. The dev perf gate catches the rest for our own builds. For student builds, the gallery phase should consider a post-build check, for example scanning the build log for an exception from URP's build preprocessing.
- **Upstream:** the URP bug (an analysis that throws on a missing Build Settings scene and silently breaks the player) is worth reporting to Unity, with the chain above. It's optional and the operator's call.
- **Thresholds:** the critic estimates about 0.70 tile pixels and about 0.20 entity pixels on a good frame. If the first measurement differs, set the gates below the measured readings with margin and note them.
- **Not checked:** a WebGL build made on Windows. The fix doesn't depend on the platform, since the stale entry is in a committed file.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file.
2. Move this plan to `Documents/Plans/archive/`.
3. Delete ISS-001 from Issues.md, then add a Changelog entry (v0.15) and bump the GDD version. Update:
   - systems.md § Share: Make the upload builds with a clean scene list.
   - Roadmap Milestone 3: no longer blocked; the Share build's scene-list guard.
   - `framework.md` § WebGL: the ISS-001 bullet becomes the root cause and the guard.
   - `lessons-learned.md`: a gate that measures frames must also check something was drawn.
4. Deferred.md: update the GallerySpikeBuild entry, which `BuildScenes` now also serves. File anything left on purpose.
5. Commit.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: webgl-blank-builds
round: 1
verdict: approved
reviewer: claude-code (reviewer role, shell on the Mac Mini)
updated: 2026-11-03T10:49:00+09:00
blocking: 0
recommendations: 0
---

# Round 1: full read

## What I could run

I had a read-only shell on the repo: `cat`, `sed`, `grep`. I built nothing and ran no test suite, no perf check and no WebGL build. The probe projects live in the Author's scratchpad, so I took the diagnosis results on trust (fresh templates draw; restoring the scene draws; an empty list draws; the Library swap changed nothing). Everything below is checked against the files on disk.

## What I verified

- `ProjectSettings/EditorBuildSettings.asset` lists `Assets/Scenes/SampleScene.unity` (enabled, guid `8c9cfa26…`), and `Assets/Scenes/` does not exist. ✓
- `TestResults/perf-build.log:533` and `:580` both hold `ArgumentException: Scene file not found: 'Assets/Scenes/SampleScene.unity'`. The first comes through `UpdateShaderPrefilteringDataBeforeBuild..ctor` (`ShaderBuildPreprocessor.cs:118`) and the second through `ShaderBuildPreprocessor.OnPreprocessBuild` (`:260`). Both go via `GatherShaderFeatures` (`:297`) into `Light2DPrefilteringAnalysis.AnalyzeBuildScenes` (`:71`). ✓
- `Light2DPrefilteringAnalysis.cs:58-61`: it reads only `EditorBuildSettings.scenes` and skips disabled entries. `:71` is `OpenPreviewScene(sceneSettings.path)` and has no existence check. ✓
- `ShaderBuildPreprocessor.cs:297`: the analysis runs only when `s_Strip2DUnusedVariants` is on. In `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset:353`, the live `URPShaderStrippingSetting` has `m_Strip2DUnusedVariants: 1`. The legacy field at `:25` says 0, but that copy isn't the one read. ✓
- `:347-350` (StripUnused with `s_Light2DAnalyzedCombos ?? Empty`) and `ShaderScriptableStripper.cs:1320-1329` (strip a variant whose combo isn't in any StripUnused list): an empty list therefore strips every `Hidden/Light2D` variant. ✓
- `BuildPlayerOptions.scenes` is set in `PerfCheckBuild.cs:30` and `GallerySpikeBuild.cs:42`. Nothing in the URP editor path reads it. ✓
- `PerfCheck.cs`: there are 4 `TileColors`, and `CheckerSprite` draws a 0.8× border on the left column and the bottom row. The screenshot comes after `MoveCamera(0.5f)` at y = 100, which is below the y > 120 gap region. `Result` has no fraction fields yet. ✓
- **The threshold estimates, recomputed.** The camera's half-height is 12 tiles, so the view is 24 tall. At the default 960×600 WebGL canvas that is 38.4 wide (921.6 tiles²); in the 1366×768 window it is 42.7 wide. 200 one-tile entities on a 2-tile grid spanning x −20…19 cover at most 200/921.6 ≈ 0.22 of the view, less overlap and the off-screen edge, so about 0.18–0.20. The tiles fill 10/11 of what's left, about 0.70. The critic's ~0.70 / ~0.20 holds, and the 0.4 / 0.05 gates leave wide margin. ✓
- No PlayMode test, dev tool or script loads scenes through Build Settings (no `LoadScene`, `EditorBuildSettings` or `buildIndex` in the package, `Assets/Dev` or `Tools/*.py`). So `m_Scenes: []` breaks no suite. ✓
- `.claude/CLAUDE.md:132` holds the perf gate line the manifest edits. ✓

## Findings

### F1: the "not traced" chain can be traced from the URP source, and it widens the risk
- **Type:** non-blocking · **Category:** accuracy · **Severity:** MED
- **Location:** § Diagnosis, "What isn't traced" (line 41); § Notes, "Students hit this too" (line 101).
- **Defect.** The source closes most of the gap the plan calls untraced:
  1. `GatherShaderFeatures` clears `s_SupportedFeaturesList` and `s_Light2DPrefilteringPerAsset` (`ShaderBuildPreprocessor.cs:290-291`), then reads the stripping settings (`:292`, which sets `s_StripUnusedVariants` = true from `m_StripUnusedVariants: 1`).
  2. It throws at `:297`, before `HandleEnabledShaderStripping()` (`:311`) can refill the feature list. Both calls (`:118` and `:260`) throw the same way, so the list is still empty when stripping runs.
  3. `ShaderScriptableStripper.CanRemoveVariant` starts from `removeInput = stripUnusedVariants` (`:1364`). Only an iteration of the loop over `supportedFeaturesList` sets it back to false (`:1386`). With zero entries the loop never runs, so **every variant of every shader the URP stripper sees is removed.** Sprite-Lit-Default, CoreBlit and the rest go, which matches ISS-001's "the shaders … did not appear among the shaders compiled" and the per-frame `shader is null`. (`CanRemoveLight2DVariant` returns false on the empty per-asset list, `:1311-1312`, so Light2D isn't the culprit here. The generic path is.)
- **Consequence for the plan.** The fix is unaffected: no throw means the list gets filled. But the stated risk is too narrow. Any exception escaping `GatherShaderFeatures` produces a blank WebGL game, not only a missing Build Settings scene. A listed scene that exists but fails to open as a preview scene would do it, and so would a future URP throw elsewhere in that method. What I could not explain: why the native macOS player draws despite the same exception (the plan and ISS-001 both record that it does). I offer no theory for it.
- **Ask.** Replace "What isn't traced" with this chain, keeping the macOS anomaly as the one untraced part. Broaden the student-risk note: the Share guard prevents the known trigger, while the perf gate (dev) and a post-build check, if the gallery phase wants one, are what catch the rest. Pass the chain on to the upstream report as well.

### F2: the URP asset save is cited at the wrong lines and on the wrong path
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Step 5 (line 80), "`ShaderBuildPreprocessor.cs:522-523`".
- **Defect.** `:522` is a comment, `:523` is `SetDirty` and `:524` is `SaveAssetIfDirty`. That block sits in `GetEveryShaderFeatureAndUpdateURPAssets`, the path for **stripping off**. This project strips (`m_StripUnusedVariants: 1`), so a fixed build saves the asset at `:600-601` in the stripping path. The claim that URP saves the asset during builds holds either way.
- **Ask.** Cite `:600-601` (or both sites).

### F3: say where the `using` goes, and add the Build Settings file to the restore list
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** File Manifest rows for `PerfCheckBuild.cs` / `GallerySpikeBuild.cs`; Step 5.
- **Defect.** Both methods end in `EditorApplication.Exit(...)` (`PerfCheckBuild.cs:47`, `GallerySpikeBuild.cs:67`), which ends the process without unwinding. If the `using` wraps the method body instead of the `BuildPlayer` call, the restore never runs. Unity has then already been handed the one-scene list, and the dev project can be left listing only the bench scene. Step 2's restore check would catch this, but only after the fact. Separately, a crash mid-build leaves the swapped list in place, and Step 5's `git status` list doesn't name `EditorBuildSettings.asset`.
- **Ask.** State that the `using` wraps only the `BuildPlayer` call, inside the existing `try`, so `Dispose` runs before the `finally` and `Exit`. Add `ProjectSettings/EditorBuildSettings.asset` to Step 5's restore-unless-intended list.

### F4: Step 2 should record the fractions and look at the picture, not only the pass/fail
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 1 Verify (line 61), Step 2 Verify (line 66), § Notes "Thresholds" (line 103).
- **Defect.** The colour match (±4 per channel against `TileColors` and the 0.8× shades) is uncalibrated until the first good build. If the match itself is off (sRGB/linear rounding, 2D lit-sprite shading in a scene with no `Light2D`), Step 2 fails with fractions near 0. That reading looks the same as "the guard didn't fix it", and the Thresholds note ("set the gates below the measured readings") then can't help, because the reading is wrong rather than low. Step 3 is the first step that looks at `perf-check.png`.
- **Ask.** Step 1 records the broken readings, which should be near 0. Step 2 records both fractions and looks at `perf-check.png`. If the picture shows the map but the fractions are low, fix the match, not the build.

## Late-pushback test

What else would I say if asked "what do you think?" Only that F1 strengthens the plan's own case for the fail-closed gate, since the URP failure mode is broader than one missing scene. Nothing outside F1–F4.

## Reviewer's view

I would build this as written. The diagnosis checks out against the log and the URP 17.6 source at every cited line except F2's off-by-one. The guard and the pixel gate address the trigger and the fail-open check, and the step order (gate fails → guard fixes with the stale entry still present → entry removed) proves each part on its own. F1 is the substantive fold-in: the mechanism is traceable, and the student risk is wider than a missing scene. F2–F4 are small. Approved, with F1–F4 to fold at close.

---

**Approval:** User confirmed 2026-11-03 (handshake approved in round 1; plan committed `24b28d9`).
**Execution:** COMPLETE 2026-11-03.
**Verification:** perf check FAIL at 0.00 / 0.00 before the guard, PASS at 0.74 / 0.18 with the guard and after removing the entry, `perf-check.png` showing map and sprites; Build Settings restored unchanged; a missing path threw naming it; trap bench WebGL draws in Chrome with no `shader is null`; Unity suites green on both editors and both modes; code review PASS WITH FLAGS (no HIGH/MED).
**Version note:** shipped as GDD v0.15. The entity fraction also counts the sprites' edge shade (more accurate than the plan's wording). The trap bench screenshot shows ground, player and HUD; the spikes start off-screen to the right.
