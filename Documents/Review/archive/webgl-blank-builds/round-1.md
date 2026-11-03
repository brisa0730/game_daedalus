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
