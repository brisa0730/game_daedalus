---
name: Unity and package gotchas
description: Non-obvious Unity Editor / batch mode / Tilemap / Input System / WebGL / UPM behaviour discovered while building
type: reference
---

> One short section per subsystem; add one the moment a non-obvious behaviour bites. Note the Unity editor version (6000.0 vs 6000.6), OS and package versions each fact was measured on.

## Batch mode and projects (Unity 6000.0.85f1, 6000.6.7f1, Windows 11)
- `Unity.exe -batchmode -quit -createProject <dir> -cloneFromTemplate <template .tgz>` works on both editors and creates a project the way Hub does, including every built-in `com.unity.modules.*` in the manifest. Extracting a template's `ProjectData~` by hand misses those modules. Templates live in `Editor/Data/Resources/PackageManager/ProjectTemplates/`; the clone resolves newer patch versions than the template pins (e.g. Input System 1.20.1 vs 1.19.0).
- The 6000.6 2D template already uses Force Text serialization (`m_SerializationMode: 2`) and URP 2D.
- `-runTests` exits 0 (all passed), 2 (test failures), other (launch / compile / licence error); **0 tests also exits 0**, so the result count must be checked.
- 6000.6 in batch mode logs **nothing** when the project is already open in another Unity: it exits 1 right after "Successfully changed project path to:". `Temp/UnityLockfile` exists while it is open.
- Batch mode logs `[Licensing::Module] Error: Access token is unavailable` and still runs with the Hub sign-in.
- Compiler warnings appear in the log only when Unity builds the assembly in that run; a Bee **cache hit** (`Csc … [CacheHit …]`) still prints them, a fully warm run (no `Csc` line) prints nothing. Deleting `Library/ScriptAssemblies/<asm>.*` and `Library/Bee/artifacts/*/<asm>.*` forces a cheap cache-hit rebuild.
- A package referenced by `file:` path from a 6000.0 project is not modified by the older editor (package files byte-identical after a run).
- The Visual Studio package writes `.vscode/launch.json` on first open.
- **Windows 260-character paths:** a git-installed package lives in `Library/PackageCache/<name>@<hash>/`. Unity compiles it fine from a deep folder, but `File.ReadAllText` in our EditMode guards throws `DirectoryNotFoundException` once a file path reaches 260 characters, even though `File.Exists` returned true. Keep generated test projects in short paths (`Tools/compat/`); a typical student path is ~150 characters.

- A **compile error** in batch mode also exits right after "Successfully changed project path to:" and **leaves `Temp/UnityLockfile` behind** (no Unity process running), so it looks exactly like "project already open". `unity_tests.py` checks for `error CS` lines before applying the lock heuristic (phase 1).

## Input System (1.11.2 on 6000.0.85f1, 1.20.1 on 6000.6.7f1)
- Active Input Handling: dev project `activeInputHandler: 1` (Input System), the compat project `2` (Both).
- Marking `com.unity.inputsystem` testable (needed for `InputTestFixture`) also runs its own `IntegrationTests` in PlayMode; `unity_tests.py` passes `-assemblyNames` with our test assembly only.
- `InputTestFixture` works by composition (`Setup()` / `TearDown()` from our `[SetUp]` / `[TearDown]`) on both editors. A press and a release queued in one frame read as `WasPressedThisFrame` true, `IsPressed` false.
- A Value action with several bindings follows **one** control at a time (conflict resolution), so two 2D-vector composites lost Right when Down was added. `PlayerControls` uses one Button action per direction.
- In tests, sequential `InputTestFixture.Press` / `Release` of different keys did not give the intended multi-key keyboard state; send the whole state with `InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys))`.

## Editor, scenes and play mode in batch tests (6000.0.85f1, 6000.6.7f1)
- `ObjectFactory.AddComponent<T>` (the Inspector's Add Component path) calls `Reset()` on both editors.
- **6000.6 deprecates every `FindObjectsByType` overload taking `FindObjectsSortMode`** (CS0618; "InstanceID will be replaced … with EntityId"). Use `FindObjectsByType<T>(FindObjectsInactive)` under `UNITY_6000_6_OR_NEWER`, the 6000.0 call otherwise. The first 6.x that deprecated it is unknown (6.1–6.5 untested).
- A new Tilemap's `cellBounds` grows from cell (0, 0) and never shrinks when tiles are erased; read the painted cells, never trust `cellBounds` as the content's extent (and never `CompressBounds()`, which writes authored data).
- `yield return new EnterPlayMode()` works in batch-mode EditMode tests on both editors. The dev project disables domain reload on Play (`m_EnterPlayModeOptions: 1`); the compat project (like a fresh 6000.0 project) reloads, which clears iterator locals (keep them in `SessionState`) and drops in-memory textures, sprites and tiles from the scene (save them as temporary assets).
- Batch mode runs Play-mode frames unthrottled (90 frames ≈ 0.03 s); set `Time.captureFramerate` for frame-based waits.
- URP 2D's default sprite material is lit; a generated scene needs a Global Light 2D (`Light2D.lightType = Global`) to show its art.

## Allocation checks in EditMode tests (6000.0.85f1, 6000.6.7f1, Windows 11)
- `GC.GetAllocatedBytesForCurrentThread()` compiles but **does not see allocations** in either editor's EditMode tests (a planted `new byte[1024]` gives a delta of 0). Never trust a zero from it without a planted-allocation check.
- The Test Framework's `Is.Not.AllocatingGCMemory()` (namespace `UnityEngine.TestTools.Constraints`; `.Not.AllocatingGCMemory()` is an extension method, so import the namespace and alias NUnit's `Is` to avoid CS0104) **works in batch mode on both editors**: it detects a planted `new byte[1024]` and a planted `new int[1]` inside `PlayerStepper.Step`.
- A planted `object x = someEnum;` boxing whose result is unused **did not register**: the compiled code doesn't allocate. Plant an allocation whose result is used.
- `Is.Not.AllocatingGCMemory()` also works in **PlayMode** on 6000.6 (and on 6000.0 in the v0.08 runs): it sees a planted 1 KB, and a used `new int[1]` planted in `Player.RunFrame` turned `PlayerAllocationTests` red.

## Rendering checks (6000.0.85f1, 6000.6.7f1)
- `Camera.targetTexture` + `Camera.Render()` + `ReadPixels` works in batch-mode PlayMode tests (with graphics) under URP 2D; with `Sprite-Unlit-Default` and point-filtered generated textures the colours come back exactly.

## uGUI and trap editor tools (6000.0.85f1, 6000.6.7f1)
- **uGUI is a built-in package:** `com.unity.ugui` resolves to **2.0.0** on 6000.0.85f1 (the compat lock file) and **2.6.0** on 6000.6.7f1 (the dev manifest), both `source: builtin`. Our `package.json` declares 2.0.0; the assembly to reference is `UnityEngine.UI`.
- `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` works on both editors for a code-built legacy `Text` (no font asset to ship).
- A HUD on a `ScreenSpaceCamera` canvas shows in `Camera.Render()` screenshots; a `ScreenSpaceOverlay` canvas would not.
- `ObjectFactory.componentWasAdded` fires for the Inspector's Add Component on both editors; hook it from an `[InitializeOnLoad]` static constructor to set up a new component (Undo-recorded).
- The Appear ghost is drawn with GL immediate mode (`GL.QUADS` with a `Sprites/Default` material, one quad per cell from the sprite's `textureRect`) inside a `[DrawGizmo]` method; it never draws in Play or the Game view.
- **Not checked:** whether a SpriteRenderer in Tiled mode draws a **Tight**-mesh sprite correctly (the phase 3 prompt asked for it). Our dev sprites are Full Rect, and the Trap inspector warns on a non-Full-Rect sprite instead (an import setting, so play start can't see it). Check it when a third-party art pack lands.
- **`Handles.FreeMoveHandle(position, size, snap, cap)`** (the overload without a rotation) compiles without warnings on both editors; the Move-end handle rounds the dragged centre to whole tiles itself (snap passed as zero).
- **`Tilemap.tilemapTileChanged`** (static event) fires for Tile Palette painting; the Trap inspector uses it to invalidate its cached Fall landing outline. Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **`EditorJsonUtility.ToJson` / `FromJsonOverwrite`** round-trip a component with fields stripped from the JSON: the missing fields keep the object's field initializers, which is how old saved data with fewer fields loads.
- **An assembly that doesn't reference another can't call any overload set that mentions that assembly's types** (CS0012 on overload resolution, even when the chosen overload doesn't use them). The editor assembly doesn't reference the sim, so runtime methods it calls need names of their own when a sibling overload takes a sim type (`HelpTexts.TrapStoryInScene`, `TrapLook.FallLandingBox`).

## macOS (Mac Mini, 6000.6.7f1 and 6000.0.85f1)
- Editors live at `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity`; project templates under `Unity.app/Contents/Resources/PackageManager/ProjectTemplates`. A module (WebGL) sits in `<version>/PlaybackEngines/`.
- Opening the dev project rewrites `.vscode/settings.json`'s `dotnet.defaultSolution` to match the folder's case (`Daedalus.slnx` here, `daedalus.slnx` on Windows). Don't commit that change.
- `UniversalRenderPipelineGlobalSettings.asset`'s `m_RuntimeSettings` list empties on the first editor save: URP clears it on every editor serialize and fills it only while building a player. The committed list is a stale build artifact; don't commit the emptied one either way without a reason.
- A player's relative `-logFile` path resolves against the app bundle, not the shell; pass an absolute path.

## Cloudflare Tunnel (cloudflared 2026.6.1)
- `cloudflared tunnel route dns <name> <host>` with no `--config` takes the tunnel from `~/.cloudflared/config.yml` (octopus on the Mac Mini), **not** the name given: the record silently points at the wrong tunnel. Always pass `--config Deploy/cloudflared/suwon.yml` (and `--overwrite-dns` to repoint), and check the `tunnelID=` in the output.
- The tunnel keeps the public `Host`; a request with `Host: 127.0.0.1` can only come from the machine itself.
- Through the tunnel, `Content-Encoding: br` / `gzip` from the origin reach Chrome unchanged (HTTP/3), and an uncompressed `.wasm` is not compressed on the way, with `Cache-Control: no-transform`.
- **Our zone is served from LAX, not Seoul,** on the Mac's KT line: `curl https://<host>/cdn-cgi/trace` shows `colo=LAX` (~258 ms) for every orcarin.com host, while cloudflare.com is `colo=ICN` (~3 ms). The tunnel itself registers at ICN, so visitors' traffic crosses the Pacific twice. Check `colo=` first when the tunnel is slow.
- Tunnel throughput on the Mac Mini swings widely and is not the line's (TCP speed test ~36 MB/s up): a 7.3 MB file took 2–162 s (2026-11-03/04); Orcarin's tunnel hits the same ~160 KB/s at times. `protocol: http2` didn't help (4 alternating rounds: median 29.2 s vs QUIC 18.1 s), so the tunnel stays on QUIC. A test from the same LAN (the office PC) shares the uplink and skews timings. curl without `Accept-Encoding: br` gets the file decompressed by Cloudflare (38 MB for a 7.3 MB `.wasm.br`).

## Naver Object Storage (S3 API, kr-standard)
- Virtual-host URLs (`https://<bucket>.kr.object.ncloudstorage.com/<key>`) work with TLS; bucket names need no dots. SigV4 with region `kr-standard`, service `s3`. Objects keep the `Content-Type`, `Content-Encoding: br` and `Cache-Control` given at PUT and serve them back; `x-amz-acl: public-read` per object makes it public while the bucket stays private. `Range` gets 206.
- PutBucketCORS needs `Content-MD5`; with CORS set, responses carry `Access-Control-Allow-Origin` for listed origins only and `Vary: Origin`.
- The console's main 인증키 covers the whole account: use a Sub Account with `NCP_OBJECT_STORAGE_MANAGER` and API access (its key tab appears only once API access is on).

## Unity WebGL loader
- A framework `<script>` that fails to load or parse, and a data file without its `Content-Encoding`, are reported only through `showBanner(…, "error")` (`console.error` without one); `createUnityInstance`'s promise never settles. Pass `showBanner` to detect them (6000.0 and 6000.6 loaders alike).
- `createUnityInstance`'s progress callback counts unpacked bytes against the Brotli `Content-Length`, so with `.br` files it hits 0.9 early (~35 s of an ~80 s throttled load) and parks there. The gallery's player uses an indeterminate bar instead.

## WebGL (6000.6.7f1, Chrome)
- **A missing scene in Build Settings blanks every WebGL build (URP 17.6, ISS-001, fixed v0.15).** `Light2DPrefilteringAnalysis.AnalyzeBuildScenes` opens every enabled Build Settings scene (not `BuildPlayerOptions.scenes`) and throws on a missing one. That empties URP's feature list, and URP then strips every shader variant: the player logs `Cannot create required material because shader is null` each frame. An empty list strips every `Hidden/Light2D` variant instead. Our dev builds use `BuildScenes.Use`. URP 17.0 (6000.0) doesn't have the analysis. A native macOS player drew despite the same exception (unexplained).
- A build also rewrites `Assets/Settings/UniversalRP.asset`'s `m_Light2DKeptVariantCombos` with the 2D light combinations it found; that is per-build churn, so don't commit it.
- Chrome accepts Brotli from `http://127.0.0.1` (a secure context), so loopback tests can serve `.br` files.
- A WebGL build's `Application.persistentDataPath` is `/idbfs/<md5 of the page URL up to its last '/'>` (query ignored), stored in IndexedDB `/idbfs`; `PlayerPrefs` live there too. In an opaque-origin (sandboxed without `allow-same-origin`) frame, IndexedDB throws `SecurityError`.
- On the Mac Mini a WebGL build of the trap bench takes ~4 min cold and 13–18 s warm; the build report's "2 errors" are licensing and network noise ("Access token is unavailable", "Curl error 42") and the build succeeds.
- URP writes which graphics settings reach the player to `Temp/graphics-settings-stripping.json`, deleted when Unity exits (nothing copies it since the spike build script went in v0.17).
- **Mono's `ZipArchive` never Stores** (6000.0.85f1 and 6000.6.7f1): `CompressionLevel.NoCompression` still writes method 8 (Deflate level 0). `System.IO.Compression.CompressionLevel` clashes with `UnityEngine.CompressionLevel`; alias it. The editor assemblies (unity-4.8-api profile) reference System.IO.Compression on both editors with no extra setup.
- **Build Profiles (6000.6.7f1):** `EditorBuildSettings.scenes` reads and writes the *active profile's* scene list when it overrides the global one, so `BuildScenes.Use` guards and restores it too (measured: a profile listing MovementTest built only the shared TrapTest, list restored). Assigning a missing path to that list is filtered out by Unity. `BuildProfile.SetActiveBuildProfile` switches the platform and starts a script compile, so `EditorApplication.isCompiling` is true right after it in the same call. A profile with a Player Settings override can't be created from script (no public API).
- `Camera.Render` into a RenderTexture works in Edit mode under URP 17.0 and 17.6 (the Share picture). After `targetTexture`, call `ResetAspect()`: `CopyFrom` can carry the source camera's aspect.
- A share that switches Standalone → WebGL in batch mode recompiles scripts mid-method and carries on (`SwitchActiveBuildTarget` then `BuildPlayer` in one call works on both editors). A logged (not thrown) exception during `BuildPlayer` arrives on `Application.logMessageReceivedThreaded` while the build still reports Success; a *thrown* one from `IPreprocessBuildWithReport` fails the build instead.
- WebGL quality: the 2D template defaults WebGL to level 3 "High" and Standalone to 5 "Ultra", same URP asset, AA off, vSync on; the difference doesn't touch 2D sprites, so staying on WebGL after a share changes nothing visible.

## WebGL (6000.6.7f1, Chrome, Windows 11)
- A release WebGL build of the perf check takes ~6 min cold, ~2 min warm. An incremental build keeps unchanged output files **with their old timestamps**, so freshness is only provable by emptying the output folder first.
- `-buildTarget WebGL` leaves the project's active target on WebGL; `PerfCheckBuild` switches it back to the host's standalone target.
- `Screen.currentResolution.refreshRateRatio` reports 60 Hz in the browser even on a ~240 Hz display (mean ~240 fps measured); judge by frame-time percentiles, not the reported rate.
- Chrome's `--window-size=1366,768` gave a 1440 × 900 canvas on this machine (display scaling).

## Gallery server (Rust: axum 0.8, sqlx 0.8, zip 9; Chrome 157; launchd)
- SQL `x != NULL` is NULL, so `DELETE … WHERE token_hash != ?` bound to `None` deletes nothing; branch to a second statement. An sqlx `Transaction` dropped before `commit` rolls back, so `?` mid-transaction can't leave a half state.
- A router test can't reach a race *inside* a handler by ordering requests: `require_access` turns a request with an ended session away first. Prove such a guard at the db layer (change-password plan, handshake F1).
- `DefaultBodyLimit` limits only buffering extractors (`Bytes`, `Form`, `Json`); a handler that streams `Body` frames must count bytes itself.
- Axum 0.8 merges `.route()` calls on the same path with different methods; a capture takes a whole segment (`/shots/{file}`, not `/shots/{id}.png`).
- zip 9: `ZipArchive::has_duplicate_names()` (the index map collapses duplicates), `ZipFile::name()` returns `ZipResult<Cow<str>>`, `enclosed_name()` catches absolute, `..` and NUL names. argon2 0.5's `SaltString::generate(&mut OsRng)` needs password-hash's `getrandom`; `SaltString::encode_b64(&rand::random::<[u8; 16]>())` doesn't.
- curl won't send `Secure` / `__Host-` cookies from a cookie jar over plain HTTP: loopback smokes pass cookies as `-H "Cookie: …"`.
- `suwon.orcarin.com` and `suwon-play.orcarin.com` are one *site*: Chrome runs the game frame in the gallery's renderer process, so CDP sees no separate iframe target; address the frame by its execution context (origin `https://suwon-play.orcarin.com`, `auxData.isDefault`).
- Cloudflare injects its Web Analytics beacon (`static.cloudflareinsights.com`) into proxied HTML; a strict CSP blocks it (console noise only; turn Web Analytics off for the host to silence it).
- A page shown inside another page's frame must set its own background and text colours: the games host's 403 line was black on the gallery's black frame.
- Through the tunnel a 10 MB upload from a browser on the same Mac takes ~30 s (out to Cloudflare and back); drivers must wait for it.
- launchd user agents: XML comments can't contain `--`; launchd doesn't create log folders; a job reading `~/Documents` may be blocked by macOS privacy protection, so the tunnel job reads a config copy in `~/.cloudflared/`. FileVault on means no auto-login: user agents start only after the operator logs in.
- Replacing a running binary: `cargo build --release` writes `target/`; the live copy is a separate installed file, so redeploy with `install` + `kickstart -k`.

