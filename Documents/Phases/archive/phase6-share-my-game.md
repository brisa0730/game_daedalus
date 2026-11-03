# Phase 6: Share my game

## Overview

**Where things stand:** Classmates can play each other's games on the class website, but students have no way to make the file it accepts. Only the developer can make one, by hand.

**What this phase changes:**
- A new menu item, Share my game, asks for a title and makes one file ready to upload.
- The file holds the game, a picture of its opening view and the title.
- The student's project settings come back exactly as they were.
- Plain messages stop a share that would be empty, black or too big.
- A closing message says what to upload, and how to update an earlier share.
- Old experiment scripts are removed.

**How you'll know it worked:** A file made this way passes the website's own check, and a game shared with it plays in Chrome on the live site. The operator tries the menu by hand, typing a Korean title on a school laptop.

**Not in this phase:** uploading from inside Unity, or a share button anywhere but the menu.

---

## Source
- **GDD sections:** `systems.md` § Share (Make the upload), § Camera, § Editor Tools; `platforms.md` § Share zip, § External Services (Builds); `architecture.md` (editor tools row, gallery row); `conventions.md` § Error Handling (texts in one class, never naming code); `Glossary.md` (Share my game, Share zip, Shared build).
- **Decisions:** `decisions.md` 2026-11-02 (Brotli, Decompression Fallback off), 2026-11-03 (Korean gallery; "the editor's own prompts stay English"; Share zip contract). The phase-6 design decisions below are **not yet in the GDD**. This prompt is their home, and § Notes to GDD carries them back.
- **GDD version:** v0.16
- **Depends on:**
  - Phase 5 `gallery-server`: the live gallery, `share_zip.rs` and `text.rs`.
  - ISS-001 fix (v0.15): `BuildScenes` and the URP Build Settings finding.
  - Phase 3 `death-and-respawn`: `CameraFollow` and the play-start map bounds.
- **Recovered backlog:** Deferred.md "The spike's WebGL build script and save probe stay in `Assets/Dev`" (trigger: Share my game landed). ISS-002 (trigger includes Phase 6's first real builds; watch only).

### Phase decisions (operator-confirmed 2026-11-03, design panel accepted)
1. **Daedalus → Share my game** asks only for a **title**. The card shows the teacher-set display name.
2. The editor never goes online. The output is one zip that the student uploads in the browser. Opening the gallery page in the browser from the done dialog is not the editor going online.
3. The build is Brotli WebGL with Decompression Fallback off. Also pinned: **Data Caching off** (PROPOSED), **Name Files As Hashes off**, no development build. The student's Player Settings are restored afterwards.
4. **Scene-list guard:** during the build, Build Settings hold exactly the shared scene; the student's list comes back afterwards. A missing scene stops with a plain message naming it. Existence is checked with `AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)`.
5. **Post-build exception check, fail closed.** Any exception logged during `BuildPlayer` means no zip and a plain message. No positive shader check (PROPOSED).
6. The zip follows platforms.md § Share zip exactly. The build output folder is a fixed ASCII name, so Unity's `<name>` is always `game`. The title lives only in `daedalus.json` and the zip filename.
7. The editor cleans the title exactly like the gallery (`clean_label`, at most 40 Unicode scalars). It refuses a zip over 50,000,000 bytes with a plain message.
8. The **active scene** is shared and is **saved without asking**. Unity's save prompt appears only for an Untitled scene.
9. A save-file dialog starts on the Desktop (later: the last folder used). The default filename is the cleaned title with characters that are illegal in filenames replaced.
10. **Screenshot:** the main camera's view rendered off-screen at 1280 × 720 in edit mode, framed as the game's first frame (the runtime's own map clamp). It falls back to 960 × 540, then 640 × 360, if the PNG is over 1,000,000 bytes.
11. The project **stays on WebGL** after the build. A missing WebGL module gets a plain message pointing at Unity Hub, before anything changes.
12. **Pre-build stops:**
    - always: Play mode, compiling, missing WebGL module;
    - plus no Player and no Main Camera. These are refused, not warned (PROPOSED).
13. **Title window:** shows which scene is shared, the Title field and the cleaned title (only when cleaning changed it). It says the scene is saved first. One line warns that the first share takes several minutes and Unity will look busy (PROPOSED wording, no number). There is no custom progress or cancel beyond Unity's own.
14. **Done dialog:**
    - text: names the file and the next step on the gallery, plus the re-share rule (choose 내 게임 업데이트하기 so players keep progress);
    - buttons: Show the zip / Open the gallery / Close;
    - the gallery address `suwon.orcarin.com` is one constant in the package (PROPOSED).
15. **Prefill and language:**
    - The title is prefilled from this scene's last share: one string per project + scene, stored in user settings, never in the scene.
    - Editor prompts are in short, plain English. Gallery labels are quoted in Korean exactly as in `strings.rs`.
16. **Batch entry and cross-language test:**
    - A batch entry wraps the menu's own build core. It is used by a new `Tools/share_check.py`, which feeds the zip to the gallery's Rust check through a new `cargo run --example check_share_zip`.
    - The script also asserts that Build Settings and Player Settings are restored.
17. A crash mid-build that leaves swapped settings is noted in the log; no recovery is engineered.

### Open operator questions (planned with the PROPOSED default; flip at approval)
- **Audience age conflict:** GDD.md:13 says "adults in their early 20s"; decisions.md 2026-11-03 says "Korean middle/high-school students in Suwon". It decides how plain the English must be. The plan writes for the younger reader either way.
    - Reviewer recommends: take decisions.md 2026-11-03 (Korean middle and high school students, an Active operator row) as current and fix GDD.md:13 to match — writing for the younger reader is right either way (handshake round 1, R1).
- **Gallery address in the package:** one constant, used for the done message and the Open the gallery button. PROPOSED: yes.
    - Reviewer recommends: yes, one constant in `ShareTexts` (handshake round 1, R2).
- **Post-build check:** exception check only, or also a positive shader check? PROPOSED: exception only.
    - Reviewer recommends: exception only, with the Step 10 end-to-end fail-closed proof; a positive shader check waits for a Deferred trigger ("a shared game that draws nothing") (handshake round 1, R3).
- **No Player / no Main Camera:** refuse, or warn with "Share anyway"? PROPOSED: refuse.
    - Reviewer recommends: refuse, with the camera text corrected (no Main Camera means no follow and no card picture, not a black game) — folded into Step 6 (handshake round 1, R4).
- **Time warning:** "several minutes" until one cold Share is timed on a school-type laptop (Step 12). PROPOSED: keep it without a number.
    - Reviewer recommends: keep "several minutes" until Step 12 times a cold Share (handshake round 1, R5).
- **Data Caching:** pinned off, or test the gallery with it on? PROPOSED: off.
    - Reviewer recommends: off — one variable fewer while ISS-002 is open, and the gallery already versions each game's folder (handshake round 1, R6).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `Packages/com.orcapafea.daedalus/Editor/BuildScenes.cs` | Scene-list guard moved from `Assets/Dev`, public, GUID existence check |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareTitle.cs` | Title cleaning (parity with `text.rs` `clean_label`) and the default zip filename |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareZip.cs` | Writes the Share zip: six entries, explicit `/` names, Stored/Deflated, UTF-8 `daedalus.json` |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareScreenshot.cs` | Off-screen 1280 × 720 first-frame picture with size fallback |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareBuild.cs` | Build core: pre-build stops, scene save, settings pins, guard, exception watch, build, zip, size cap |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareTexts.cs` | Every student-facing Share text and the gallery address constant |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareMyGame.cs` | Menu item, save dialog, done dialog, prefill storage, batch entry for the Tools script |
| NEW | `Packages/com.orcapafea.daedalus/Editor/ShareTitleWindow.cs` | The small title window |
| MODIFY | `Packages/com.orcapafea.daedalus/Runtime/Player.cs` | Map-bounds read and camera target pulled out of `StartPlay`; `FirstView` for edit-mode framing |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Share/BuildScenesTests.cs` | Guard swap, restore (paths, flags, GUIDs), missing scene |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Share/ShareTitleTests.cs` | Cleaning parity cases, filename cases |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Share/ShareZipTests.cs` | Entry names, compression methods, JSON, replace and partial behavior |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Share/ShareScreenshotTests.cs` | Framing, size, fallback, scene untouched; writes a PNG to `TestResults/` |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Share/ShareBuildTests.cs` | Pins and restore, exception watch, fail-closed outcome, pre-build stops, texts, prefill storage |
| MODIFY | `Packages/com.orcapafea.daedalus/Tests/PlayMode/Traps/CameraFollowTests.cs` | Parity: `FirstView` framing equals the camera's first frame |
| MODIFY | `Assets/Dev/Editor/PerfCheckBuild.cs` | Use the package's `BuildScenes` |
| DELETE | `Assets/Dev/Editor/BuildScenes.cs` (+ `.meta`) | Moved into the package |
| DELETE | `Assets/Dev/Editor/GallerySpikeBuild.cs` (+ `.meta`) | Replaced by Share my game (Deferred trigger) |
| DELETE | `Assets/Dev/GallerySpike/` (`SaveProbe.cs` + metas) and `Assets/Dev/GallerySpike.meta` | Spike save probe (Deferred trigger) |
| NEW | `Deploy/gallery/examples/check_share_zip.rs` | Runs the gallery's real check on a zip from the command line; title-parity assert |
| DELETE | `Deploy/gallery/examples/share_zip_from_build.rs` | Its stand-in role ends with Share my game |
| NEW | `Tools/share_check.py` | Batch Share build of the trap bench, zip layout checks, gallery check, settings-restored check |

Unity creates `.meta` files for the new scripts and the `Tests/EditMode/Share/` folder. Commit each with its asset.

---

## Steps

### Step 1: Move the scene-list guard into the package; delete the spike files
- **NEW `Editor/BuildScenes.cs`** (namespace `Orcapafea.Daedalus.Editor`, `public static class`):
    - Same API as the dev version: `public static IDisposable Use(string[] scenePaths)`.
    - It is public because `PerfCheckBuild` compiles into Assembly-CSharp-Editor, which sees only public types of our auto-referenced Editor assembly.
    - Existence check: `AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)` is non-empty, not `File.Exists`. Scenes inside a registry package live under `Library/PackageCache`, where the `File.Exists` check would wrongly reject them.
    - A missing path throws `FileNotFoundException` naming the scene, **before** the list is touched.
    - Dispose restores the exact previous array: paths, enabled flags and GUIDs.
    - Keep the doc comment: the ISS-001 reason (URP 17.6 scans Build Settings, not `BuildPlayerOptions.scenes`), and "wrap only BuildPlayer; restore before `EditorApplication.Exit`".
- **DELETE** `Assets/Dev/Editor/BuildScenes.cs`, `Assets/Dev/Editor/GallerySpikeBuild.cs`, `Assets/Dev/GallerySpike/` (folder and `SaveProbe.cs`), each with its `.meta`, and `Assets/Dev/GallerySpike.meta`.
- **MODIFY `Assets/Dev/Editor/PerfCheckBuild.cs`**: add `using Orcapafea.Daedalus.Editor;`. Nothing else changes.
- **Tests:** NEW `Tests/EditMode/Share/BuildScenesTests.cs`.
    - Setup and teardown:
        - Create a temp scene asset under `Assets/DaedalusTestTemp/` (deleted in TearDown).
        - Save and restore the real `EditorBuildSettings.scenes` in SetUp/TearDown regardless of outcome.
    - Cases:
        1. With a starting list holding a missing path and a disabled entry, `Use(temp scene)` leaves exactly `[temp scene, enabled]`. Dispose restores the original paths, enabled flags and GUIDs.
        2. `Use` with a missing path throws, the message names the path, and the list is unchanged.
        3. The list is restored when the `using` body throws.
- **Verify:**
    - `[automated]` `python Tools/unity_tests.py --platform editmode` on both editors. A compile error in `PerfCheckBuild` would fail the 6000.6 run.
    - `[grep]` no `GallerySpikeBuild`, `SaveProbe`, `DAEDALUS_GALLERY_SPIKE` or `Orcapafea.Daedalus.Dev.BuildScenes` left in code.

### Step 2: Title cleaning and the default filename
- **NEW `Editor/ShareTitle.cs`** (`internal static`):
    - **`internal const int MaxChars = 40`**, with a comment pointing at the gallery's `title_max_chars` (`Deploy/gallery/src/config.rs`) and `text.rs` `clean_label`.
    - **`internal static string Clean(string raw)`**: returns `null` when nothing is left. A port of `clean_label`:
        - Walk the string by **Unicode scalar**: a surrogate pair is one character; a lone surrogate is dropped (Rust strings can't hold one).
        - Drop control-but-not-whitespace characters.
        - Drop bidi controls U+202A–202E, U+2066–2069, U+200E, U+200F and U+061C.
        - Collapse whitespace runs to one space and drop leading whitespace.
        - Take the first 40 scalars, then trim the end.
    - **Use explicit character sets, not `char.IsWhiteSpace` / `char.IsControl`**, so Mono's Unicode tables can't drift from Rust's:
        - whitespace = Rust's `White_Space`: U+0009–000D, 0020, 0085, 00A0, 1680, 2000–200A, 2028, 2029, 202F, 205F, 3000;
        - control = `Cc`: U+0000–001F, U+007F–009F.
    - **`internal static string FileName(string cleanTitle)`**: the default zip filename.
        - Replace `\ / : * ? " < > |` and control characters with `-`.
        - Trim trailing dots and spaces (Windows).
        - A Windows reserved name (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`9`, `LPT1`–`9`) gets `-game` appended; an empty result becomes `my-game`.
        - Add `.zip`. Korean stays.
- **Tests:** NEW `Tests/EditMode/Share/ShareTitleTests.cs`.
    - `text.rs` `labels_are_cleaned` cases verbatim, with max 40 / 3 as there (an internal overload taking `maxChars` is fine).
    - 41 Korean syllables → the first 40.
    - 41 emoji (82 UTF-16 units) → 40 emoji.
    - A Korean + emoji mix across the 40 boundary.
    - NBSP, U+3000 (the full-width space a Korean IME may type) and U+0085 collapse to one space.
    - U+200B and U+180E are kept (not whitespace in Rust).
    - A lone surrogate is dropped.
    - Filename cases: illegal characters, trailing dot, a reserved name, all-illegal → `my-game.zip`, Korean title.
- **Verify:** `[automated]` EditMode on both editors. Real cross-language parity is asserted in Step 9.

### Step 3: The Share zip writer
- **NEW `Editor/ShareZip.cs`** (`internal static`):
    - `internal const string BuildName = "game"`: the fixed ASCII output folder's name, so Unity names the files `game.*`; it must match `[A-Za-z0-9_-]{1,64}`.
    - `internal static long Write(string zipPath, string cleanTitle, byte[] screenshotPng, string buildDir, long maxBytes)` returns the zip's size.
    - Entries, in this order, with literal names joined by `/` (never `Path.Combine`):
        - `daedalus.json`
        - `screenshot.png`
        - `Build/game.loader.js`
        - `Build/game.data.br`
        - `Build/game.framework.js.br`
        - `Build/game.wasm.br`
    - No directory entries. Other files in the build folder (`index.html`, `TemplateData/`, symbol files) are never added.
    - `.br` files and `screenshot.png` use `CompressionLevel.NoCompression` (Stored; they are already compressed). `daedalus.json` and the loader use `Optimal` (Deflated).
    - `daedalus.json`:
        - `JsonUtility.ToJson` of a small `[Serializable]` `{ int format = 1; string title; }` (nested private type).
        - Encoded with `new UTF8Encoding(false)`, no BOM (serde_json rejects one).
        - At most 4096 bytes, else throw.
    - Use only `System.IO.Compression.ZipArchive` over a `FileStream` (no `ZipFile` / `.FileSystem`). It must compile warning-free on both editors.
    - Write to `<zipPath>.partial`. **If the partial is over `maxBytes`, delete it, leave any existing `zipPath` untouched and return its size without replacing** (the caller shows the too-big text; an oversize re-share never destroys the last good zip; handshake round 1, F4). Otherwise replace `zipPath` (delete, then move). On any failure, delete the partial and rethrow. A previous good zip is never left half-written.
- **Tests:** NEW `Tests/EditMode/Share/ShareZipTests.cs`. Use a fake build folder in a temp dir: the four files with distinct bytes, plus `index.html` and `game.symbols.json.br` as junk.
    - Exactly six entries with exactly those names; no directory entries; no backslash.
    - Read each entry's compression method from the central-directory headers (signature `0x02014b50`, method at offset 10): 0 for `.br` / `.png`, 8 for the rest.
    - **If Mono's `ZipArchive` can't produce Stored**, keep Deflated (the contract allows both). Assert the observed method, and record it in `framework.md`.
    - `daedalus.json`: parses to `format` 1 and a Korean + emoji title intact; it does not start with `EF BB BF`.
    - Every entry's bytes round-trip.
    - An existing zip at the path is replaced; no `.partial` remains.
    - With `maxBytes` below the written size: the returned size is over the cap, the existing zip at the path is byte-identical to before, and no `.partial` remains (F4).
    - A missing build file throws and leaves neither the zip nor the partial.
- **Verify:** `[automated]` EditMode on both editors, no compiler warnings (the wrapper fails on any).

### Step 4: First-frame framing in the runtime (one source for play start and the screenshot)
- **MODIFY `Runtime/Player.cs`** (no behavior change at play start):
    - Pull the map-bounds part of `StartPlay` into `internal static CollisionMap ReadMap(Tilemap tilemap, Vector2 feet, Rope[] ropes, Rect[] checkpointBoxes, Rect[] exitBoxes, Trap[] traps, List<string> problems)`. That part is the include rect (start hitbox, ropes, checkpoints, exits, trap boxes and end boxes) plus `CollisionMapReader.Read` / `Empty`. `StartPlay` calls it.
    - Add `static Vector2 CameraTarget(Vector2 feet)` (feet + `MovementInternals.GripHeight`), used by `Render`'s `cameraFollow.Follow(...)`.
    - Add `internal Rect FirstView(out Vector2 cameraTarget)`. It works in edit mode and in play:
        - Feet come from `transform.position` + `FeetOffset(GetComponent<SpriteRenderer>(), transform)`, the same as `DefaultStart` after `Awake`.
        - It runs the same one-time scans as `StartPlay` (`ScanTilemaps` / `Pick` or the field, `RopeCollector.FindSorted`, `LevelCollector` finds and boxes) and calls `ReadMap`.
        - It returns `new Rect(map.Origin, size)` and `CameraTarget(feet)`. Problems are discarded.
    - Editor-side use is an edit-time one-off, inside the rule-5 scan exception; it never runs per frame.
- **Tests:** MODIFY `Tests/PlayMode/Traps/CameraFollowTests.cs`. Add `FirstViewFramesLikeTheFirstFrame`:
    - Build a level with `MainCamera = true` and the player standing on ground, so the first ticks don't move it.
    - After the first frame, take a second orthographic camera with the same size and aspect. Drive it by `new CameraFollow(cam, player.FirstView(out t)).Follow(t)`. It must sit at the main camera's x/y (±1e-4).
    - Run one variant with the player near the map's left edge (clamped view).
    - Existing camera tests stay green.
- **Verify:** `[automated]` `python Tools/unity_tests.py` (both editors, both modes). Look at the existing `*-traps-*.png` for regressions.

### Step 5: The screenshot
- **NEW `Editor/ShareScreenshot.cs`** (`internal static`):
    - `internal static byte[] Take(Player player, Camera mainCamera)` (the caller passes `ShareBuild.SharedPlayer()` and `Camera.main`) returns PNG bytes, or `null` if no size fits.
    - Make a temp camera object with `HideFlags.HideAndDontSave` and `CopyFrom(mainCamera)` (settings, transform, layer). After setting `targetTexture`, call `temp.ResetAspect()` so a fixed aspect copied from the main camera can't stretch or misclamp the picture (handshake round 1, F7).
    - Set `targetTexture` to a `RenderTexture(w, h, 24)` **before** framing, so `camera.aspect` is the picture's.
    - Frame with `new CameraFollow(temp, player.FirstView(out target)).Follow(target)`, then `Render`, `ReadPixels`, `EncodeToPNG`.
    - Restore `RenderTexture.active`; destroy the camera, texture and image in `finally`.
    - Never move the main camera; never dirty the scene.
    - Appear traps are already hidden in edit mode (`TrapAuthoring`), as at play start. The HUD isn't drawn; that is acceptable.
    - Sizes: 1280 × 720, then 960 × 540, then 640 × 360. Limit 1,000,000 bytes (constants with a comment naming the gallery limits in `config.rs`).
    - The choice is a pure helper `internal static byte[] FirstThatFits(Vector2Int[] sizes, Func<int, int, byte[]> render, int maxBytes)`.
- **Tests:** NEW `Tests/EditMode/Share/ShareScreenshotTests.cs`.
    - The scene is built in code with `EditorSceneManager.NewScene`:
        - a Grid and a `Collision` Tilemap painted with an in-memory `Tile` whose sprite is a solid colour;
        - a Player with a distinctly coloured sprite;
        - an orthographic main camera tagged MainCamera.
    - Cases:
        1. The PNG decodes (`Texture2D.LoadImage`) to 1280 × 720 and is ≤ 1,000,000 bytes. Both the player colour and the tile colour are present.
        2. With the player at the map's left edge, the image's left columns show map, not background beyond it (the clamp).
        3. The main camera's transform is unchanged and the scene is not dirty after `Take`.
        4. `FirstThatFits` with a fake renderer: an oversize first size gives the second; all oversize gives `null`.
    - The test saves case 1 as `<unity version>-share-screenshot.png` in `DAEDALUS_TEST_RESULTS`, else `TestResults/`. `TestImages` is internal to the PlayMode assembly, so the EditMode test carries its own few-line copy of that folder rule (handshake round 1, F9).
    - If case 1 renders black under URP in edit mode, switch `Take` to `RenderPipeline.SubmitRenderRequest` with a `RenderPipeline.StandardRequest` targeting the same texture (F7), and record it in `framework.md`.
- **Verify:**
    - `[automated]` EditMode on both editors.
    - `[manual]` Claude **looks at** `TestResults/6000.0.85f1-share-screenshot.png` and `6000.6.7f1-share-screenshot.png`: the player and the tiles are visible and framed like a game's opening view.

### Step 6: The build core and its texts
- **NEW `Editor/ShareTexts.cs`** (`internal static`):
    - Every student-facing Share string. They are short, plain English and name no code, as in `conventions.md` § Error Handling.
    - `internal const string GalleryAddress = "https://suwon.orcarin.com"` (PROPOSED).
    - The Korean gallery labels are copied exactly from `Deploy/gallery/src/strings.rs`: `NAV_UPLOAD` 업로드 (the navigation link every signed-in student sees; 게임 올리기 is a link only when "My games" is empty — handshake round 1, F3), `UPDATE_MY_GAME` 내 게임 업데이트하기, `NEW_GAME` 새 게임. A comment says they must match.
    - Draft texts (final wording reviewed in Step 12):
        - Play mode: "Stop Play mode first, then choose Share my game again."
        - Compiling: "Unity is still getting ready. Wait until it finishes, then try again."
        - No WebGL module: "This Unity can't make web games yet. In Unity Hub, open Installs, pick this Unity version, choose Add modules and tick WebGL Build Support. Then restart Unity."
        - Untitled scene not saved: "Save your scene first, then share it."
        - No Player: "This scene has no Player, so the game would be empty. Add a Player (Add Component → Daedalus → Player) and share again."
        - No Main Camera: "This scene has no Main Camera, so the game can't follow the player and the gallery has no picture for it. Tag a camera MainCamera and share again." (The runtime treats no Main Camera as normal, and an untagged camera still draws, so "black" would be wrong; handshake round 1, R4.)
        - Empty title: "Type a title for your game."
        - Scene missing: "The scene {0} can't be found. Open the scene you want to share and try again."
        - Build failed: "The build didn't finish, so no zip was made. Open the Console (Window → General → Console), fix the red message and share again."
        - Build stopped: "The build was stopped, so no zip was made."
        - Exception during build: "Something went wrong while Unity prepared your game, so it might show nothing. No zip was made. Open the Console to see what happened, then share again."
        - Too big: "Your game is {0} MB. The class gallery takes up to 50 MB. Use fewer or smaller pictures and sounds, then share again." ({0} has one decimal, rounded up, so 50,000,001 bytes reads 50.1, never 50; handshake round 1, F11.)
        - Done: "Your game is ready: {0}. Next: open the class gallery, sign in, choose 업로드 and pick this file as 새 게임. Already shared this game? Choose 내 게임 업데이트하기 instead, so players keep their progress."
        - Title window line: "Your scene is saved first. The first share takes several minutes and Unity will look busy. Don't close Unity."
- **NEW `Editor/ShareBuild.cs`** (`internal static`):
    - **`internal static string CheckBeforeBuild()`** returns the first stop text, or `null`. Nothing is changed before these. **The order is fixed** (handshake round 1, F2):
        1. Play mode, compiling or updating;
        2. `!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL)`;
        3. no Player (the `FindObjectsByType<Player>` pattern with the `UNITY_6000_6_OR_NEWER` define, as in `CheckpointEditor`);
        4. `Camera.main == null`.
        - It does **not** check for an Untitled scene. The menu handles that after these checks with Unity's save prompt (Step 7). `Run` refuses a scene with no path with the Untitled text, as a guard for the batch entry.
    - **`internal static Player SharedPlayer()`**: the Player that is framed and checked. When there are several, take the first after a deterministic sort by hierarchy path, then instance position (Architecture rule 5; handshake round 1, F10). v1 scenes have one.
    - **`internal static ShareResult Run(string rawTitle, string zipPath)`**: `ShareResult` = `Ok`, `Message`, `ZipPath`, `ZipBytes`. In order:
        1. `CheckBeforeBuild`, then a scene with no path → the Untitled text (batch guard; the menu has already saved it).
        2. `ShareTitle.Clean` (null → empty-title text).
        3. Save the active scene if dirty (`EditorSceneManager.SaveScene`, no prompt).
        4. Take the screenshot, **before** the platform switch (null → build-failed text).
        5. Switch the active target to WebGL if needed (`EditorUserBuildSettings.SwitchActiveBuildTarget`) and stay there.
        6. Empty `Temp/DaedalusShare/` first: Unity keeps unchanged files with old timestamps, per `framework.md`.
        7. Inside `using` scopes for `PlayerSettingsPin`, `BuildScenes.Use(new[] { scenePath })` and `ExceptionWatch`, call `BuildPipeline.BuildPlayer`:
            - `scenes = [scenePath]`
            - `locationPathName = Temp/DaedalusShare/game`
            - `target = WebGL`
            - `options = BuildOptions.None`
        8. After all restores, decide with a pure `internal static string Outcome(BuildResult result, int exceptions, bool filesPresent)`:
            - `Succeeded` + 0 exceptions + all four `Build/game.*` files present → continue;
            - `Cancelled` → build-stopped text;
            - an exception → the exception text;
            - anything else → build-failed text.
        9. `ShareZip.Write` with the 50,000,000-byte cap.
        10. A returned size over the cap → the too-big text with MB. The previous zip at that path (if any) is untouched.
        11. Success → one `Debug.Log` line (scene, zip path, bytes, seconds).
    - A `FileNotFoundException` from `BuildScenes` becomes the scene-missing text.
    - Nested `internal sealed class PlayerSettingsPin : IDisposable`:
        - saves `PlayerSettings.WebGL.compressionFormat`, `decompressionFallback`, `dataCaching` and `nameFilesAsHashes`;
        - sets Brotli / false / false (PROPOSED) / false;
        - restores them on Dispose.
        - A comment says why: the gallery sends `Content-Encoding` itself, Phase 5 tested this, and the file names must be `game.*`.
    - Nested `internal sealed class ExceptionWatch : IDisposable`:
        - counts `LogType.Exception` from `Application.logMessageReceivedThreaded` and keeps the first message. The threaded event is used because build preprocessing may log from worker threads, which the main-thread event misses. The count is interlocked.
        - unsubscribes on Dispose.
        - Errors are not counted: the "2 errors" licensing/network noise in `framework.md` makes the report's error count unusable.
    - Restores run in `finally`, before any return. A crash mid-build is not recovered (decision 17).
- **Tests:** NEW `Tests/EditMode/Share/ShareBuildTests.cs`.
    - **`PlayerSettingsPin`:** start from non-default values (Gzip / true / true / true). Inside the pin they are the pinned values; after Dispose they are back; they are also back when the body throws. TearDown restores the project's originals.
    - **`ExceptionWatch`:**
        - counts one `Debug.LogException` (with `LogAssert.Expect`);
        - ignores `LogError` and `LogWarning`;
        - stops counting after Dispose.
    - **`Outcome`:** fails closed for each of: not succeeded, cancelled, one exception, a missing file. Ok only when all are clean.
    - **`CheckBeforeBuild`:**
        - an Untitled scene with no Player → the no-Player text (the path is not checked here);
        - `Run` on an Untitled scene **that has a Player and a MainCamera-tagged camera** (so the path guard is the first thing to stop it) → the Untitled text, with nothing changed (handshake round 2, F14);
        - a saved temp scene with no Player → the no-Player text;
        - a Player but no camera → the camera text;
        - both present → `null`.
        - This assumes the WebGL module is installed (CLAUDE.md: both editors have it). If it is missing, assert the module text instead.
    - **`ShareTexts`:** no text contains `Orcapafea`, `.cs`, `PlayerSettings`, `EditorBuildSettings` or `Exception`; every text is under ~350 characters.
- **Verify:** `[automated]` EditMode on both editors. The real build is verified in Step 9.

### Step 7: Menu, title window, done dialog and the batch entry
- **NEW `Editor/ShareMyGame.cs`** (`public static class`, so `-executeMethod` can reach it; its doc comment says the batch entry is for `Tools/share_check.py`):
    - **Menu:** `[MenuItem("Daedalus/Share my game")]` (private static). It stays enabled in Play mode and while compiling, and answers with the plain text. Flow:
        1. `CheckBeforeBuild` (Play mode, compiling, WebGL module, no Player, no Main Camera, in that order). If any stop applies → `EditorUtility.DisplayDialog("Share my game", text, "OK")`. Nothing is saved before this.
        2. Only then, an Untitled scene → `EditorSceneManager.SaveScene(scene)`. Unity's own save prompt appears; verify it does for Untitled, otherwise use `SaveFilePanelInProject` + `SaveScene(scene, path)`. Cancel → stop quietly.
        3. Open `ShareTitleWindow` prefilled. On Share it closes, and the rest runs from `EditorApplication.delayCall` (never build from inside `OnGUI`).
        4. `EditorUtility.SaveFilePanel` opens on the last folder (`EditorUserSettings` key `Daedalus.Share.Folder`), else the Desktop (`Environment.SpecialFolder.DesktopDirectory`), with the name `ShareTitle.FileName(clean)`. Cancel → stop quietly.
        5. Call `ShareBuild.Run`.
        6. On success, `DisplayDialogComplex` with the Done text and buttons Show the zip (`EditorUtility.RevealInFinder`), Close, and Open the gallery (`Application.OpenURL(ShareTexts.GalleryAddress)`). On failure, one `DisplayDialog` with the result's text.
    - **Prefill:**
        - one string per project + scene: `EditorUserSettings` key `Daedalus.Share.Title.<scene GUID>`;
        - written after a successful share;
        - never stored in the scene.
        - Small internal `LoadTitle(guid)` / `SaveTitle(guid, title)` helpers.
    - **Batch entry `public static void BuildFromCommandLine()`:**
        - Arguments: `-shareScene PATH -shareTitleFile FILE -shareOutput ZIP`. The title is read as UTF-8 from a file, so non-ASCII text survives Windows command lines.
        - Opens the scene (`EditorSceneManager.OpenScene`), remembers the active build target, then calls **the same `ShareBuild.Run`**.
        - Afterwards it switches back to the remembered target. This is dev-project hygiene only, like `PerfCheckBuild`; the menu stays on WebGL. It is documented as the one difference.
        - Calls `AssetDatabase.SaveAssets()` after `Run`, so `ProjectSettings.asset` on disk reflects the in-memory Player Settings that Step 9.8 checks (a broken restore must show on disk; handshake round 1, F6).
        - Logs one line, `ShareMyGame: ok|fail <bytes> <seconds>s <message>`, then `EditorApplication.Exit(0 or 1)`. All restores have already run inside `Run`.
        - Missing arguments → a usage line and exit 1.
- **NEW `Editor/ShareTitleWindow.cs`** (`internal sealed class : EditorWindow`, shown with `ShowModalUtility`, fixed small size):
    - "Scene: <name>".
    - The **Title** field, focused on open; Enter = Share.
    - "The gallery will show: <cleaned>", only when cleaning changed the title.
    - The title-window line from `ShareTexts`.
    - Buttons Share (disabled while the cleaned title is empty, with the empty-title text shown) and Cancel. One field; nothing else.
- **Tests:** add to `ShareBuildTests.cs`:
    - prefill storage round-trips per scene GUID;
    - two GUIDs don't share a title;
    - TearDown clears the keys.
- **Verify:**
    - `[automated]` EditMode on both editors (compile, no warnings).
    - `[grep]` exactly one `MenuItem("Daedalus/Share my game")` in the package.
    - `[manual]` the window and dialogs are judged in Step 12.

### Step 8: A command-line gallery check for a zip
- **NEW `Deploy/gallery/examples/check_share_zip.rs`**: `cargo run --example check_share_zip -- <zip> [--expect-title-file <utf8 file>]`.
    - Refuses a zip over `Config::new(..).max_upload_bytes` (the 50,000,000 cap enforced in `upload.rs`).
    - Runs `share_zip::check_and_unpack` into a `tempfile` staging dir with `Limits::from_config(&Config::new(tmp))`.
    - Prints one JSON line: `{"ok":true,"title":…,"build_name":…,"bytes":…,"zip_bytes":…}`, or `{"ok":false,"reject":"<code>","message":"<Korean>"}` (`too-big` for the size cap).
    - With `--expect-title-file`, asserts that the **raw** `title` string in the zip's `daedalus.json` (what the editor wrote, read from the staging copy) equals `text::clean_label(raw_file, title_max_chars)`, byte for byte. `unpacked.title` is the gallery re-cleaning the editor's output, so comparing against it would hide every "C# kept too much" drift (handshake round 1, F1). A second assert, `unpacked.title == ` the raw zip title, confirms the gallery leaves the editor's title unchanged. A mismatch prints both strings (and their scalar code points) and fails.
    - Exits 0 or 1.
- **DELETE `Deploy/gallery/examples/share_zip_from_build.rs`.** Its doc says it stands in "before the editor's Share my game exists".
- No change to `files.rs` / `db.rs`, so Deferred.md's "Leftover spike code in the gallery crate" does not trigger.
- **Tests:** `cargo test` builds the example. Its real input comes from Step 9.
- **Verify:** `[automated]` in `Deploy/gallery/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`.

### Step 9: The cross-language check script
- **NEW `Tools/share_check.py`**: `python Tools/share_check.py [--editor 6000.0|6000.6|all]`, default `6000.6`. It reuses `unity_tests.py`'s `editor_exe`, `EDITORS`, `ensure_compat_project` and `looks_locked` (import from the same folder), and runs Unity with the same process-group handling as `run_unity` but a **60-minute** build timeout like `perf_check.py`'s (`run_unity`'s 30 minutes can be too short for a cold build; handshake round 1, F8). `--no-gallery-check` skips step 7 for machines without cargo (the Windows laptop); the zip is then checked on the Mac with `check_share_zip`. Per editor:
    1. **Project:**
        - 6000.6: the dev project, scene `Assets/Dev/TrapTest/TrapTest.unity`.
        - 6000.0: the compat project. Copy `Assets/Dev/TrapTest/` (with `.meta` files; GUIDs stable) into `Tools/compat/6000.0/Assets/ShareCheck/TrapTest/` first; that folder is generated and ignored.
    2. **Snapshot:** `ProjectSettings/ProjectSettings.asset` keys `webGLCompressionFormat`, `webGLDecompressionFallback`, `webGLDataCaching` and `webGLNameFilesAsHashes`, plus the `m_Scenes` block of `ProjectSettings/EditorBuildSettings.asset`.
    3. **Title file:** write a UTF-8 title file to `TestResults/share-check/title.txt` holding a hostile title: Korean, emoji, a tab, NBSP, U+3000, a bidi control, leading spaces and more than 40 characters.
    4. **Run Unity:**
        - Delete the old zip.
        - `-batchmode -projectPath P -executeMethod Orcapafea.Daedalus.Editor.ShareMyGame.BuildFromCommandLine -shareScene … -shareTitleFile … -shareOutput TestResults/share-check/<editor>.zip -logFile TestResults/share-check/<editor>.log`.
        - **No `-buildTarget`** (the switch path runs). **No `-nographics`** (the screenshot renders). **No `-quit`** (the entry exits).
        - Fail on a non-zero exit or the lock, with the same messages as `unity_tests.py`.
    5. **The zip is fresh** (mtime after start) and ≤ 50,000,000 bytes.
    6. **Python `zipfile` checks:**
        - exactly the six names, no directories;
        - Stored for `.br` / `.png` and Deflated for the rest, or as recorded in Step 3;
        - `daedalus.json` has no BOM and `format` 1.
        - Extract `screenshot.png` to `TestResults/<unity version>-share-check-screenshot.png`.
    7. **Gallery check:** `cargo run --quiet --example check_share_zip -- <zip> --expect-title-file <title.txt>` in `Deploy/gallery/` → exit 0, `ok` true, `build_name` `game`.
    8. **Restored:** the snapshot keys and `m_Scenes` match exactly.
    9. **Korean labels:** each Korean label in `ShareTexts.cs` (업로드, 내 게임 업데이트하기, 새 게임) appears verbatim in `Deploy/gallery/src/strings.rs`, and 업로드 is the `NAV_UPLOAD` value.
    10. Print `PASS|FAIL <version>: <reason> (build Ns, zip N bytes)`; exit non-zero on any failure.
- **Tests:** the script is the test (outside the Unity suites, like `perf_check.py`).
- **Verify:**
    - `[automated]` `python Tools/share_check.py` (6000.6), then `--editor 6000.0`. Record the cold and warm build times in the phase log.
    - `[manual]` Claude **looks at** both `*-share-check-screenshot.png`: the trap bench's opening view, framed and drawn.

### Step 10: Editor-version checks (record findings)
- **Build Profiles (6000.6.7f1):**
    - Make a WebGL Build Profile with its own scene list that overrides the global one, and make it active.
    - Run the Share (batch entry on a copy of the setting, or the menu).
    - Confirm that the shared scene is the one built, and that URP's 2D scan reads the guarded list. Check `Temp/graphics-settings-stripping.json` lists the 2D features, and the Step 11 game draws.
    - If an active profile defeats the guard, add a pre-build stop to `CheckBeforeBuild` + `ShareTexts`: "Switch back to the Web platform in Build Profiles, then share again." Use a version define (`UNITY_6000_1_OR_NEWER`) with a 6000.0 fallback, and add a test.
    - **Player Settings overrides (F13):** in the same check, give the active profile a Player Settings override (Gzip, if the editor offers it) and confirm the zip still holds the four `.br` files. If not, the Build Profiles stop above covers it too.
    - Do the check on 6000.0.85f1 as well, to confirm the version define's premise (no profiles with scene lists there).
    - Record the result in `framework.md`.
- **QualitySettings:** record the WebGL default quality level in the dev project and in a fresh 2D template, and whether it changes what the shared game draws. Don't pin it unless it does.
- **Exceptions in clean builds:** confirm the Step 9 logs show no `LogType.Exception` on either editor. If a harmless one appears, do **not** filter it silently: record it and ask the operator.
- **Fail-closed, end to end (handshake rounds 1–2, F5; Architecture rule 7):** put a probe in a scratch folder of the compat project (`Tools/compat/6000.0/Assets/ShareCheck/Probe/`): an `IPreprocessBuildWithReport` whose `OnPreprocessBuild` **logs without throwing**, `Debug.LogException(new Exception("share-check probe"))`. A thrown exception would fail the build and refuse through `Outcome`'s not-succeeded branch, never testing the exception count; ISS-001's exceptions came while the build reported success. Run `python Tools/share_check.py --editor 6000.0` and assert three things in its log: the build reported Succeeded; the `ShareMyGame: fail` line carries the **exception-during-build** text, not the build-failed text; no zip exists. Remove the probe and record the result in the phase log.
- **Compression:** record in `framework.md` what Mono's `ZipArchive` writes for `NoCompression` (from Step 3).
- **Verify:** `[manual]` findings in `framework.md` and the phase log. Any new stop comes with a test, and EditMode runs on both editors.

### Step 11: Real upload to the live gallery (watching ISS-002)
- **The test account:** use the test student account from the Phase 5 Chrome check; ask the operator if it is gone.
- **First upload:** in Chrome, sign in at `suwon.orcarin.com`, choose 업로드, upload the Step 9 zip as 새 게임.
    - The card shows the editor's cleaned title and the screenshot.
    - The game opens, **draws** (no black frame, no `shader is null` in DevTools), and the keys move the player.
    - Touch the checkpoint and reload: progress is kept.
- **Update:** make a second zip (a small visible scene change, same title), upload it with 내 게임 업데이트하기 on the same game, and confirm the progress is kept and the change shows.
- **ISS-002:** if the frame stays black, **first** capture the `*.wasm.br` request in DevTools (status, `If-None-Match`, `Range`, size received) and note the count of loads. Don't build fixes (versioned URLs, watchdog) unless it recurs.
- Remove the test game afterwards.
- **Verify:** `[manual]` screenshots of the card and the playing game saved to `TestResults/`, and the load count and outcome noted in the phase log.

### Step 12: Operator editor check
Claude writes the checklist into the phase log, asks the operator, and continues with Steps 13–14. The phase closes only when this passes.
1. **On the Mac dev project (6000.6.7f1):** start on a Standalone-active project and open the trap bench.
    - Make an unsaved change; Daedalus → Share my game.
    - The window shows the scene name. A Korean + spaces title shows "The gallery will show: …".
    - Share → the save dialog is on the Desktop with the title as the filename.
    - Unity's progress bar appears. The scene is saved, with no prompt.
    - Done dialog: Show the zip opens the folder; Open the gallery opens the site.
    - Second share: the title is prefilled and the save dialog opens on the last folder.
    - Build Settings and Player Settings look as before.
2. **Stops:**
    - in Play mode;
    - during a script reload;
    - in an Untitled scene with a Player and a Main Camera (File → New Scene, add a Player): Unity's save prompt appears; Cancel stops quietly; Save continues to the title window (handshake round 2, F15);
    - in a scene with no Player;
    - in a scene with no Main Camera.
    - Each shows one plain message, and nothing changed.
3. **On a Windows school-type laptop with 6000.0.85f1:**
    - Type a **Korean title with the IME**; it composes correctly in the field and in the filename.
    - Use a project path under a realistic user folder.
    - **Time one cold Share** (decides the time-warning wording).
    - Run `python Tools/share_check.py --editor 6000.0 --no-gallery-check` there once (or have the operator run it), then `check_share_zip` on the Mac against the copied zip.
4. **Wording:** the operator reads every `ShareTexts` message. Too long or unclear → reword. This is where the audience-age question bites.
- **Observe:** each step behaves as written.
- **Must be absent:** a dialog naming code, a changed Build Settings list, a **new** zip after a failed share (an earlier zip at that path stays), a frozen Unity.

### Step 13: Run Tests
- `python Tools/unity_tests.py`: both editors, both modes. Look at `TestResults/*-share-screenshot.png`, `*-render-smoke.png`, `*-movement-*.png` and `*-traps-*.png`.
- `python Tools/share_check.py --editor all` (plus the Windows run from Step 12).
- In `Deploy/gallery/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`.
- `python Tools/perf_check.py` once, to prove `PerfCheckBuild` works with the moved guard. Read `perf-check.json` and look at `perf-check.png`.
- If tests fail, fix the failing tests or the code causing the failure. Do not proceed to code review until all tests pass.

### Step 14 (Final): Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on all files changed during this phase.
- Fix any HIGH/MED issues.
- Report the review result to the user.

---

## Completion Criteria

- [ ] Daedalus → Share my game makes a zip from the active scene that the gallery's own check accepts (`check_share_zip` ok, build name `game`, title parity), on 6000.6.7f1 and 6000.0.85f1, and once on Windows.
- [ ] The zip has exactly the six entries with `/` names; `.br` / `.png` are Stored (or as recorded), the rest Deflated; `daedalus.json` is UTF-8 without a BOM; it is ≤ 50,000,000 bytes.
- [ ] Build Settings and the four pinned Player Settings are restored after the build (Step 9 asserts them); the project stays on WebGL after a menu share.
- [ ] Title cleaning matches `clean_label`: the NUnit cases plus the Step 9 hostile-title parity, comparing the editor's raw `daedalus.json` title with Rust's `clean_label` of the same input.
- [ ] The screenshot is ≤ 1,000,000 bytes and ≤ 1280 × 720, framed like the first frame (PlayMode parity test). Claude looked at the EditMode and share-check PNGs.
- [ ] Pre-build stops (Play mode, compiling, no WebGL module, no Player, no Main Camera, in that order) show one plain message and change nothing; an Untitled scene gets Unity's save prompt only after them (Cancel stops quietly). A build exception, failure or cancel makes no zip (the exception path proved end to end by a build callback that logs an exception while the build succeeds, Step 10); an oversize zip is never written over the previous one and gets a plain message.
- [ ] Build Profiles, QualitySettings, clean-build exceptions and Mono Stored behaviour are checked and recorded in `framework.md`.
- [ ] A real upload to `suwon.orcarin.com` (new, then update) plays and draws in Chrome and keeps progress. The ISS-002 observation is recorded.
- [ ] The operator editor check passed, including the Korean IME title on a Windows school-type laptop, and one cold Share was timed.
- [ ] `GallerySpikeBuild.cs`, `SaveProbe.cs`, the dev `BuildScenes.cs` and `share_zip_from_build.rs` are gone; `PerfCheckBuild` uses the package guard and `perf_check.py` passes.
- [ ] All Unity suites green on both editors with no compiler warnings; `cargo test` and clippy clean; code review has no open HIGH/MED.

---

## Notes to GDD

- **New design, to write into `systems.md` § Share (Make the upload), bump GDD v0.16 → v0.17:**
    - phase decisions 1–17 above: active scene saved without asking; title-only window with cleaned preview; Desktop / last-folder save dialog; first-frame screenshot with size fallback; stays on WebGL; pinned settings; fixed build name `game`; exception check fail-closed; pre-build stops; done dialog and re-share rule; prefill per scene in user settings; English editor prompts quoting the Korean gallery labels.
- **`platforms.md` § Share zip:**
    - "Phase 6 must produce exactly this" becomes "Share my game produces exactly this (v0.17)".
    - Note that Share my game Stores `screenshot.png` as well as the `.br` files (allowed by the contract), and always names the build `game`.
- **`architecture.md` editor tools row:** "Since v0.17: Share my game (`ShareMyGame`, `ShareBuild`, `ShareZip`, `ShareScreenshot`, `ShareTitle`, `BuildScenes`)."
- **Runtime row:** `Player.FirstView`, the shared map read.
- **Gallery row:** the `check_share_zip` dev example.
- **PROPOSED, to confirm at approval:**
    - the gallery address constant;
    - exception-only post-build check;
    - refuse (not warn) on no Player / no Main Camera;
    - "several minutes" wording;
    - Data Caching off.
- **Open:**
    - The audience age conflict (GDD.md:13 "adults in their early 20s" vs decisions.md 2026-11-03 "Korean middle/high-school students in Suwon"). Fix GDD.md § The Pitch to the operator's answer.
- **Deviations or new detail:**
    - `share_zip_from_build` is removed and replaced by `check_share_zip`.
    - The Tools script copies the trap bench into the 6000.0 compat project.
    - The batch entry switches the dev project back to its previous target (the menu does not).
- **Deferred features:**
    - Share inside the daedalus window: Milestone 7 already owns it.
    - Share history, an in-editor upload UI, per-scene settings beyond one remembered title: Roadmap §3 if the operator agrees. An in-editor upload would contradict "the editor never goes online".

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md.
3. Write `.claude/memory/logs/phases/phase6-log.md`. Include the build times (cold/warm, Mac, Windows), the Step 10 findings, the Step 11 load count and ISS-002 observation, and the operator check results.
4. Update `.claude/MEMORY.md` current state.
5. **CLAUDE.md:**
    - Build / run: remove the "Gallery spike build" line.
    - Add `python Tools/share_check.py [--editor 6000.0|6000.6|all]` with a one-line description.
    - In the Class gallery line, replace the `share_zip_from_build` sentence with `cargo run --example check_share_zip -- <zip> [--expect-title-file F]`.
    - Testing table: add a row "Share my game, WebGL build settings, the Share zip contract → + `python Tools/share_check.py`".
    - Project Config → Networking: "the editor's Share my game arrives with `share-my-game`" becomes present tense.
6. **`.claude/memory/framework.md`:**
    - Update line 75: `GallerySpikeBuild` is gone, and nothing copies the stripping report any more.
    - Add the Step 10 findings: Build Profiles, QualitySettings, Mono `ZipArchive` Stored, the platform switch inside a menu call, `logMessageReceived` during `BuildPlayer`.
7. **GDD:**
    - Bump to v0.17 in `GDD.md`. Update its header line ("the editor's Share my game next" → landed) and § The Pitch audience if the operator settled it.
    - Update `systems.md` § Share, `platforms.md` § Share zip and `architecture.md` per § Notes to GDD.
8. **`Documents/GDD/Glossary.md`:** flip **Share my game** and **Share zip** to Active (proposal; the contract is now produced and consumed). Shared build stays Draft.
9. **`.claude/memory/decisions.md`:** **update the existing** "Share my game design (Phase 6 `share-my-game`, prompt stage)" row (no second row; handshake round 1, F12): drop "prompt stage", fold in the operator's answers to the open questions and any change made during the phase, and set the status to Active. Remove any Roadmap §4 line it settles.
10. **`Documents/Operations/Roadmap.md`:**
    - Add the §0 row for Phase 6 `share-my-game`: Milestone 3 `class-gallery` complete; editor check and real upload passed.
    - Collapse the Milestone 3 section out of §1 (its §0 row lands now).
    - Add the deferred features to §3 if agreed.
11. **`Documents/Operations/Issues.md`:**
    - ISS-002: update its Status with the Step 11 observation. If it recurred, add the DevTools capture and next step.
    - It stays open until its fix is proven. Its trigger "first real-student builds" still points at classroom use.
12. **`Documents/Operations/Deferred.md`:**
    - **Delete** "The spike's WebGL build script and save probe stay in `Assets/Dev`" (its trigger fired; resolved in Step 1).
    - File new entries for any leftover, for example: no positive shader check (if the operator kept exception-only; trigger: a shared game that draws nothing); a non-ASCII project path problem if Step 12 found one.
    - "Leftover spike code in the gallery crate" stays (no `files.rs` / `db.rs` change).
13. **`Documents/Operations/Changelog.md`:** a v0.17 entry: landed, key surfaces, verified, design and staleness, GDD deltas.
14. Commit all phase work in one commit: `v0.17: Phase 6 — share my game: <short description>`. Don't commit URP's per-build `UniversalRP.asset` churn.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

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

---

## Review round 2

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
