# Phase 6 log: `share-my-game`

Prompt: [phase6-share-my-game.md](../../../../Documents/Phases/phase6-share-my-game.md) (design panel; handshake-approved in 2 rounds, record in `Documents/Review/archive/phase6-share-my-game/`). Started 2026-11-03.

**Operator decisions:** "commit, and execute" — the PROPOSED defaults were taken (gallery address constant, exception-only check, refuse on no Player / no Main Camera, "several minutes" wording, Data Caching off). **Audience: college students** (operator, mid-phase): GDD.md § The Pitch was right; decisions.md's Korean-UI row said middle/high-school and is corrected. Editor messages stay short plain English.

## Per step
1. **Guard moved.** `Editor/BuildScenes.cs` (public, `AssetPathToGUID(OnlyExistingAssets)`); the dev copy, `GallerySpikeBuild.cs` and `SaveProbe.cs` deleted; `PerfCheckBuild` uses the package guard. 3 tests.
2. **Title.** `ShareTitle.Clean` ports `clean_label` scalar by scalar with Rust's White_Space / Cc sets spelled out; stops at 40 characters (a space appended at the cut is trimmed, same as Rust's take + trim_end). `FileName` replaces illegal characters, trims trailing dots, guards Windows reserved names.
3. **Zip.** **Finding:** Mono's `ZipArchive` (6000.6.7f1 and 6000.0.85f1) writes **Deflate (8) even for `CompressionLevel.NoCompression`**; nothing is Stored. The contract accepts it; the test and `share_check.py` pin Deflated. `CompressionLevel` is ambiguous with `UnityEngine.CompressionLevel` (alias needed). Oversize: the partial is checked before replacing, so an earlier good zip stays.
4. **First view.** `Player.ReadMap` (the include rect + Collision read) shared by `StartPlay` and the new `FirstView`; `CameraTarget` shared with `Render`. PlayMode parity test at the map's left edge and mid-map.
5. **Screenshot.** Temp camera `CopyFrom` + `ResetAspect` after `targetTexture`; `Camera.Render` works in Edit mode under URP 17.0 and 17.6 (no `SubmitRenderRequest` fallback needed). Looked at both `*-share-screenshot.png`: player centred, floor clamped to the bottom.
6. **Build core.** As planned; one addition: a picture failure has its own message (`NoPicture`) instead of the build-failed text, since there is nothing in the Console to fix.
7. **Menu, window, entry.** Untitled scenes get `SaveFilePanelInProject` + `SaveScene(scene, path)` (deterministic, instead of relying on `SaveScene` prompting). `EditorWindow.title` exists, so the window's field is `gameTitle` (hiding warning avoided).
8. **check_share_zip.** Compares the raw `daedalus.json` title with `clean_label(input)` and with the card title. `share_zip_from_build.rs` deleted.
9. **share_check.py.** PASS 6000.6.7f1 (build 118 s warm, zip 10,019,570 bytes) and 6000.0.85f1 (build 304 s, first WebGL build in the compat project, zip 11,807,868 bytes); the gallery accepted both with identical titles (`함정 지옥 😀 트롤 레벨 가…`, 40 characters from the hostile input). Looked at both `*-share-check-screenshot.png`: the trap bench's opening view, clamped left. `sys.dont_write_bytecode` keeps `Tools/__pycache__` untouched (a 3.14 `.pyc` there is tracked).
10. **Editor-version checks.**
    - Clean builds: no `LogType.Exception` on either editor.
    - Fail-closed probe (compat project, `IPreprocessBuildWithReport` that **logs** an exception): build `Result: Success`, Share refused with the exception text, no zip. Probe removed.
    - **Build Profiles (6000.6.7f1):** `EditorBuildSettings.scenes` *is* the active profile's list (docs and measured). With a WebGL profile active whose list held MovementTest, Share built only TrapTest (Level 0) and the profile's list came back unchanged; the gallery accepted the zip. Assigning a missing path to the list is filtered out by Unity. **Activating a profile starts a script compile**, so a Share in the same call stops with the "still getting ready" text (correct). No stop added. A profile **Player Settings override** can't be created by script: moved to the operator check.
    - QualitySettings: WebGL defaults to level 3 "High", Standalone to 5 "Ultra"; same URP asset, AA 0, vSync 1; the differences (pixel lights, anisotropic, LOD bias) don't touch 2D sprites. Not pinned.
11. **Real upload (live gallery, headed Chrome 157 over CDP, driver in the scratchpad; test student `kbiny` made by the operator).** The Step 9 zip (6000.6.7f1) uploaded as 새 게임 → `/play/myk749yt8n`. **Draws** (trap bench, HUD death counter), a click into the frame and a held right arrow **moved the player**; the card shows the screenshot, the editor-cleaned title and the teacher-set display name. **Update** with a second zip (MovementTest scene, same title) through 내 게임 업데이트하기: same game id, the new level plays. Test game removed afterwards (gone from the list). No console errors, no `shader is null`. **Not checkable:** "touch the checkpoint and reload, progress kept": the in-game checkpoint save is `world-systems` work and the spike's save probe is deleted, so the game saves nothing yet. **ISS-002 watch:** 6 loads, Unity started every time: 12 s (first, fresh download), 2 s, 1 s, 1 s, then **63 s on the first load after the update** (a fresh ~10 MB version); no stall stayed black. The slow load wasn't captured (no DevTools network log in this driver).
12. **Operator editor check:** checklist below; pending.
13. **Tests.** Unity suites green on both editors and modes (476 EditMode, 85 PlayMode); cargo test + clippy clean; share_check PASS on both; perf_check PASS (p95 17.0 ms, 60.0 fps, tiles 0.74, entities 0.18; fresh build through the moved guard). Looked at the render-smoke, movement, traps and perf pictures.

14. **Code review: PASS WITH FLAGS** (0 HIGH, 2 MED, 14 LOW). Fixed:
    - MED 1: a throwing share (a locked zip, a full disk, a Temp file locked on Windows) ended with no dialog. `ShareMyGame.Continue` now catches, logs and shows `ShareTexts.Unexpected`; `Run`'s doc says it throws only for file system failures, after every restore.
    - MED 2: the dev and compat projects already held the pinned values for 3 of 4 keys, so the restore check couldn't fail. `share_check.py` now seeds the opposite of every pin (Gzip, fallback, caching, hashes, threads all on), checks the restore against that, and puts the settings file back. Both editors PASS from the seeded state, so the pin is proven on a real build too.
    - LOW, fixed: an unchecked Player no longer passes the no-Player stop (`isActiveAndEnabled`, test); the missing-scene catch covers only `BuildScenes.Use`; a failed save has its own text (`SaveFailed`); `File.Replace` swaps the zip; `FirstView` runs once per picture; **threads support is pinned off** (a threaded build needs COOP/COEP headers the gallery doesn't send); BuildFailed points at a red message "about your scene or scripts" or the teacher (WebGL builds always log licensing noise as errors); `share_check.py` cross-checks the editor's copies of the gallery limits against `config.rs`; `share_check.py --probe` makes the ExceptionWatch → Outcome wire a repeatable check (PASS on 6000.0.85f1).
    - LOW, not changed: the pin's constructor setting four values without unwinding (a setter throwing is theoretical); multi-scene editing (checks see every open scene, only the active one is built): filed in Deferred.md.
    - Post-phase: CLAUDE.md rule 5's exception gains the editor's one-off commands (Share's scans); the Mono and Build Profile findings move into framework.md.
    Re-verified: Unity suites green on both editors and modes (477 EditMode, 85 PlayMode); share_check PASS on both from the seeded settings; probe PASS.

## Operator editor check (Step 12)
On the Mac dev project (6000.6.7f1), Standalone active, trap bench open (`Assets/Dev/TrapTest/TrapTest.unity`):
1. Make an unsaved change; **Daedalus → Share my game**. The window shows "Scene: TrapTest" and the Title box.
2. Type a title with Korean and double spaces: "The gallery will show: …" appears with the cleaned title.
3. Share → the save dialog opens on the Desktop with the title as the file name.
4. Unity shows its build progress; no save prompt (the scene is saved silently).
5. Done dialog: **Show the zip** opens Finder at the file; repeat once and press **Open the gallery** (the site opens in the browser).
6. Share again: the title is prefilled and the save dialog opens on the last folder.
7. Build Settings and Player Settings → Web → Publishing look as before (compression, decompression fallback, data caching, name files as hashes). The project stays on Web.
8. Stops, each one plain message and nothing changed: in Play mode; while scripts reload; in a scene with no Player; in a scene with no Main Camera. Then File → New Scene, add a camera tagged MainCamera and a Player: Unity's save dialog appears; Cancel stops quietly; Save continues to the title window.
9. Optional: in Build Profiles, a Web profile with a Player Settings override set to Gzip, active → Share → the gallery accepts the zip (or tell me what Share said).
On a Windows school-type laptop with 6000.0.85f1:
    (Also on the Mac: Enter in the Title box shares.)
10. Type a **Korean title with the IME**: it composes correctly in the field and in the file name; Enter while composing doesn't share early.
11. Use a project under a realistic user folder. **Time one first share** (cold).
12. Optionally `python Tools/share_check.py --editor 6000.0 --no-gallery-check` there, and send me the zip for `check_share_zip`.
13. Read every message (`Editor/ShareTexts.cs`); too long or unclear → tell me.
**Must be absent:** a message naming code, a changed Build Settings list, a **new** zip after a failed share (an earlier zip at that path stays), a frozen Unity.

## Close (2026-11-03)
The operator closed the phase ("I am satisfied with it for now. Let's move on") **without running the editor check above**; it is filed in Deferred.md ("Share my game's operator editor check isn't done", trigger: before the classroom test). Post-phase cleanup done; shipped as v0.17.
