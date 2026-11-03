---
slug: phase6-share-my-game
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T14:36:30+09:00
blocking: 1
recommendations: 6
---

# Review round 1: phase6-share-my-game

## What I could run, what I verified, what I could not

**Ran:** a shell in the connected Mac mini folder: `grep`, `sed`, `git`, and one Python script over the repo files. Nothing outside the folder: no Unity, no cargo, no build, no browser. I could not list `/Applications/Unity/Hub/Editor` from the VM.

**Verified against the files (at `280ae28` + the uncommitted prompt and decisions row):**
- `text.rs` `clean_label`: the algorithm, the bidi set, and `labels_are_cleaned` (max 40 and max 3). The plan's explicit whitespace set matches Rust's `White_Space` and its control set matches `Cc`. U+200B and U+180E are kept. The function is idempotent on its own output.
- `share_zip.rs`: six files, `[A-Za-z0-9_-]{1,64}` build name, Stored/Deflated only, `daedalus.json` read through `serde_json` (a BOM rejects), `format == 1`, PNG IHDR ≤ 1280 × 720, ≤ 1,000,000 bytes, 256 entries, other files ignored.
- `config.rs`: `title_max_chars` 40, `max_upload_bytes` 50,000,000, `max_unpacked_bytes` 120,000,000, `max_metadata_bytes` 4096, screenshot limits. `upload.rs`: the body is raw `application/zip`, so the 50,000,000 cap is on the zip's own bytes, as the plan assumes.
- `strings.rs`: `UPLOAD_TITLE` 게임 올리기, `UPDATE_MY_GAME` 내 게임 업데이트하기, `NEW_GAME` 새 게임 are verbatim. Also `NAV_UPLOAD` 업로드 (see F3).
- `Player.cs`: `StartPlay` lines 189–255 (the include rect, `CollisionMapReader.Read`/`Empty`, `new CameraFollow(Camera.main, new Rect(map.Origin, grid size))`), `Render` line 375 (feet + `GripHeight`), `FeetOffset` line 475, `Awake` placed feet. `LevelCollector` boxes come from transforms (`ElementBox`), so `FirstView` works in edit mode without `Awake`.
- `CameraFollow.cs`: clamps with `camera.aspect`, so the screenshot's aspect must be the picture's (F7).
- `Assets/Dev/Editor/BuildScenes.cs` (`File.Exists`, namespace `Orcapafea.Daedalus.Dev`) and `PerfCheckBuild.cs` (uses `BuildScenes.Use`). There is no asmdef under `Assets/Dev`, and the package Editor asmdef is `autoReferenced: true`, so "public" is required, as the plan says.
- `ProjectSettings.asset` has the keys `webGLCompressionFormat`, `webGLDecompressionFallback`, `webGLDataCaching`, `webGLNameFilesAsHashes`.
- `unity_tests.py` exports `EDITORS`, `editor_exe`, `ensure_compat_project`, `looks_locked`, `run_unity`. No suite uses `-nographics`. The compat project is the 6000.0 2D URP template (URP 17.0.4).
- `Assets/Dev/TrapTest/`: every GUID the scene, tiles and palette reference is inside the folder, in our package, or in URP (Sprite-Lit-Default, Light2D). So the 6000.0 copy in Step 9 is self-contained. It has a `MainCamera`-tagged orthographic camera and a Player.
- The ISS-001 plan's evidence: URP's throw shows in the log as `ArgumentException`, so a `LogType.Exception` watch is the right signal.
- `framework.md` line 75 is the `GallerySpikeBuild` line. The Deferred entry and the CLAUDE.md/Roadmap mentions of `share_zip_from_build` and the spike are all covered by Post-Phase Cleanup.

**Accepted on trust (could not verify):** both editors have WebGL Build Support (CLAUDE.md line 119 says so). Mono's `ZipArchive` `NoCompression` behaviour (the plan already handles both outcomes). `Camera.CopyFrom` copying `aspect` (F7). `Camera.Render` under URP 17 in edit mode (F7; Step 5 case 1 would catch a black render). Build Profiles' scene-list override arriving in 6000.1 (F13). Synchronous `SwitchActiveBuildTarget` followed by `BuildPlayer` inside one menu call (a common CI pattern; Step 10 records it).

---

## Findings

### F1: the cross-language title check can't see the drift it exists to catch
- **Type:** blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 8 (`--expect-title-file`), Step 9.7, Completion Criteria "Title cleaning matches `clean_label`"
- **Defect:** the assertion is `clean_label(raw, 40) == unpacked.title`. But `unpacked.title` is the gallery's `clean_label` applied to the title the editor wrote (`share_zip.rs` step 11). So the check compares Rust(C#(raw)) with Rust(raw), not C#(raw) with Rust(raw). `clean_label` repairs any C# output that kept too much, so every "kept too much" drift passes:
  - C# keeps U+0085 or U+3000 as a character;
  - C# fails to collapse a run of spaces;
  - C# takes 41 scalars, or counts UTF-16 units and splits an emoji pair the wrong way;
  - C# keeps a bidi control.
  - In each case the gallery cleans it again and the assert passes, yet the editor's "The gallery will show: …" preview, the prefill and the zip filename show a different title from the card.
- **Scope:** only "dropped too much" drift (e.g. C# treats U+200B as whitespace) is caught. Step 9.3's hostile title is mostly "keeps too much" cases, the ones this masks.
- **Fix:** compare the **raw** `title` string from the zip's `daedalus.json` (what the editor wrote) with `clean_label(raw_file, title_max_chars)`, byte for byte. `check_share_zip` already has the bytes (staging `daedalus.json`). Keep the `unpacked.title` comparison as a second assert if wanted.

### F2: the Untitled-scene flow contradicts itself and can prompt to save in Play mode
- **Type:** non-blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 6 `CheckBeforeBuild` ("the active scene has no path" → save text) and its test; Step 7.1–7.2; decision 8; Step 12.2
- **Defect:** `CheckBeforeBuild` returns "Save your scene first" for an Untitled scene, and its test asserts that. Step 7.1 shows any `CheckBeforeBuild` stop as a dialog. So the menu would show the dialog and never Unity's save prompt, against decision 8 and Step 12.2.
  - "Untitled scenes are handled first" read literally means saving before the Play mode check. In Play mode `SaveScene` can't save.
  - A new Untitled scene usually has no Player either: the student is made to save it, then refused.
- **Fix:** state the order once. First Play mode / compiling / WebGL module, then no Player / no Main Camera (neither needs a path), then for Untitled Unity's save prompt (Cancel → stop quietly), then the title window. Either keep the path check in `CheckBeforeBuild` for the batch entry only, or move it to the menu, and make the test match.

### F3: the done text names a button a returning student doesn't see
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 Done text ("choose 게임 올리기"), Step 11, Step 9.9
- **Defect:** the gallery's navigation link to the upload page is `NAV_UPLOAD` **업로드** (`pages.rs:29`). 게임 올리기 is the upload page's heading, and it is a link only in the empty "my games" state (`UPLOAD_FIRST`, `gallery.rs:42`). A student who already shared a game, exactly the one told to pick 내 게임 업데이트하기, sees only 업로드. Step 9.9 can't catch this, because 게임 올리기 does appear in `strings.rs`.
- **Fix:** "open the class gallery, sign in, choose 업로드, pick this file as 새 게임…", and add `NAV_UPLOAD` to the copied labels and to the Step 9.9 check.

### F4: an oversize re-share deletes the student's last good zip
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 3 (write `.partial`, then replace) and Step 6.9–6.10 (size check after `ShareZip.Write`)
- **Defect:** a re-share to the same filename replaces the previous good zip first, and then the too-big check deletes the new one. The student is left with no zip at all, not the earlier one that still uploads.
- **Fix:** check the size of the `.partial` before the replace. Either let `Write` take the cap, or split it into write-partial and commit.

### F5: nothing proves fail-closed works end to end
- **Type:** non-blocking · **Category:** design · **Severity:** medium
- **Location:** Step 6 (`ExceptionWatch`, `Outcome`), Step 10 "Exceptions in clean builds"; CLAUDE.md Architecture rule 7
- **Defect:** the watch is unit-tested with `Debug.LogException`, and `Outcome` is unit-tested with a count. Nothing shows that `Run` passes the watch's count into `Outcome`, or that an exception thrown by a real build callback reaches `logMessageReceivedThreaded`. That is the "guard that fails open" variant rule 7 names, and this check is the backstop for ISS-001's class of blank game.
- **Fix:** add a one-off Step 10 check. Put a throwing `IPreprocessBuildWithReport` in a scratch folder of the compat project, run `share_check.py --editor 6000.0`, and expect FAIL with the exception text and no zip. Remove the script and record the result in the phase log.

### F6: the settings-restored check can pass without testing anything
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 7 batch entry, Step 9.8
- **Defect:** Step 9.8 compares `ProjectSettings.asset` on disk. `PlayerSettings` changes live in memory until Unity saves them. If the restore were broken, whether the pinned values reach the file depends on when Unity happens to save (during `BuildPlayer`, or at exit). The plan doesn't pin that down, so a broken restore may not show on disk.
- **Fix:** have `BuildFromCommandLine` call `AssetDatabase.SaveAssets()` after `Run` and before `EditorApplication.Exit`, so the file reflects the in-memory state the check means to test.

### F7: the screenshot camera's aspect and render path
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 5
- **Defect:** `CameraFollow` clamps with `camera.aspect`. If `CopyFrom` carries over a fixed aspect (I couldn't verify this; edit mode's main camera takes the Game view's), the 1280 × 720 picture is stretched and clamped wrong. Separately, if `Camera.Render` misbehaves under URP 17 in edit mode, the plan names no fallback.
- **Fix:** call `temp.ResetAspect()` after setting `targetTexture`. Name `RenderPipeline.SubmitRenderRequest` (`StandardRequest`) as the fallback if case 1 renders black.

### F8: `share_check.py` timeout and the Windows run
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 9 (reuses `run_unity`), Step 12.3
- **Defect:** `run_unity` kills Unity after `TIMEOUT_SECONDS = 30 * 60`. `perf_check.py` gives builds 60 minutes. A cold 6000.0 Share, with a first compat import, on a school-type Windows laptop can exceed 30 minutes. The Windows run also needs cargo and the gallery crate on that laptop, which a school laptop is unlikely to have.
- **Fix:** give `share_check.py` a build timeout like `perf_check.py`'s. Add `--no-gallery-check`: on Windows, run the zip layout and settings checks, then run `check_share_zip` on the Mac against the copied zip.

### F9: `TestImages` isn't visible to EditMode
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5 test ("using the `TestImages` folder rule")
- **Defect:** `TestImages` is `internal` in the PlayMode test assembly (`Tests/PlayMode/TestImages.cs:12`). `ShareScreenshotTests` is EditMode.
- **Fix:** say which it is: duplicate the five-line folder rule in the EditMode test, or move `TestImages` somewhere both assemblies see.

### F10: which Player the screenshot frames
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5 `Take(Player player, …)`, Step 6 no-Player check
- **Defect:** with two Players in a scene, the plan doesn't say which one is framed. The `CheckpointEditor` pattern takes `players[0]` from `FindObjectsSortMode.None`, which is unordered.
- **Fix:** use one rule, for example the first Player after a deterministic sort (rule 5).

### F11: the too-big message's MB can contradict the limit
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 Too big text
- **Defect:** a 50,400,000-byte zip, truncated or rounded to whole MB, reads "Your game is 50 MB. The class gallery takes up to 50 MB."
- **Fix:** show one decimal (50.4 MB), or round up.

### F12: Post-Phase Cleanup would add a second ADR row
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Post-Phase Cleanup step 9; `.claude/memory/decisions.md:60` (uncommitted)
- **Defect:** the prompt-stage row "Share my game design (Phase 6 …)" already exists. Step 9 says to add "an ADR row" for the same design.
- **Fix:** update the existing row with the operator's answers and drop "prompt stage".

### F13: Build Profiles may override Player Settings, not only scenes
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 10 Build Profiles; `PlayerSettingsPin`
- **Defect:** Step 10 checks only the profile's scene list. If an active WebGL profile also overrides Player Settings, the pin's `PlayerSettings.WebGL.*` writes may not be what the build uses. Then a Gzip or fallback build comes out, and the gallery rejects it with `BuildSettings`.
- **Fix:** in the same check, give the profile a Player Settings override (Gzip, if the editor offers it) and confirm the zip still holds the four `.br` files. If it doesn't, the Build Profiles stop already planned covers this too. Do the check on both editors, since the version define rests on an unverified claim.

---

## Recommendations (open operator questions)

- **R1 Audience age:** take decisions.md 2026-11-03 (Korean middle and high school students, an Active operator row) as current and fix GDD.md:13 to match. Writing for the younger reader is right either way.
- **R2 Gallery address constant:** yes. One constant in `ShareTexts`.
- **R3 Post-build check:** exception only, with F5's end-to-end proof. A positive shader check waits for a Deferred trigger ("a shared game that draws nothing").
- **R4 No Player / no Main Camera:** refuse, but fix the camera text. The runtime treats no Main Camera as normal (`NoMainCameraMeansNoFollowAndNoWarning`). An untagged camera still draws, so "the game would be black" is wrong in that case. Suggest: "This scene has no Main Camera, so the game can't follow the player and the gallery has no picture for it. Tag a camera MainCamera and share again."
- **R5 Time warning:** keep "several minutes" with no number until Step 12 times a cold Share.
- **R6 Data Caching:** off. It removes one variable while ISS-002 is open, and the gallery already versions each game's folder.

## Reviewer's view

I would build this. The zip contract, the limits, the gallery labels and the runtime refactor all check out against the code. The one blocking item is F1: as written, the cross-language title check can't detect the drift it is there for, and the fix is a one-line change of what gets compared. Of the rest, F2 (the Untitled order) and F5 (proof that fail-closed works) matter most. The others are small wording or test-detail fixes.
