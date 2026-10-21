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

## Rendering checks (6000.0.85f1, 6000.6.7f1)
- `Camera.targetTexture` + `Camera.Render()` + `ReadPixels` works in batch-mode PlayMode tests (with graphics) under URP 2D; with `Sprite-Unlit-Default` and point-filtered generated textures the colours come back exactly.

## WebGL (6000.6.7f1, Chrome, Windows 11)
- A release WebGL build of the perf check takes ~6 min cold, ~2 min warm. An incremental build keeps unchanged output files **with their old timestamps**, so freshness is only provable by emptying the output folder first.
- `-buildTarget WebGL` leaves the project's active target on WebGL; `PerfCheckBuild` switches it back to Windows.
- `Screen.currentResolution.refreshRateRatio` reports 60 Hz in the browser even on a ~240 Hz display (mean ~240 fps measured); judge by frame-time percentiles, not the reported rate.
- Chrome's `--window-size=1366,768` gave a 1440 × 900 canvas on this machine (display scaling).
