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

## WebGL (6000.6.7f1, Chrome, Windows 11)
- A release WebGL build of the perf check takes ~6 min cold, ~2 min warm. An incremental build keeps unchanged output files **with their old timestamps**, so freshness is only provable by emptying the output folder first.
- `-buildTarget WebGL` leaves the project's active target on WebGL; `PerfCheckBuild` switches it back to Windows.
- `Screen.currentResolution.refreshRateRatio` reports 60 Hz in the browser even on a ~240 Hz display (mean ~240 fps measured); judge by frame-time percentiles, not the reported rate.
- Chrome's `--window-size=1366,768` gave a 1440 × 900 canvas on this machine (display scaling).
