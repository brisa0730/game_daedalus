# Phase 2: Movement Runtime

## Overview

**Where things stand:** The movement rules exist and pass their automatic tests, but nobody can play them yet. There is no player to add in Unity, no controls and no level to try.

**What this phase changes:**
- You can add a Player to any object with a sprite, paint tiles, press Play and run and jump with a keyboard or a gamepad.
- Painted tiles become floors, thin platforms you can jump up through, and climbable walls. Ropes can be placed and climbed.
- The player's feet sit exactly on the floor, and movement looks smooth on any screen.
- Problems in a scene give plain-language warnings instead of silent failures.
- A test level with every kind of obstacle is rebuilt automatically, and automatic tests play it and take pictures.
- The operator plays the test level, tunes how movement feels, and the best values become the new defaults.

**How you'll know it worked:** The operator plays the test level by hand, likes how it feels, and the tuned numbers are what a fresh Player starts with.

**Not in this phase:** a camera that follows the player, animations, and the play-from-here tool (later milestones).

---

## Source
- **GDD sections:** `systems.md` § Player, § World & Level Elements, § Tile System, § Editor Tools (play-from-here hook only); `entities.md` § Player tunables, § Abilities, § Placeable elements (Rope), § Authored data; `architecture.md` § System Map, § Time Model, § Data Flow; `platforms.md` § Input, § Performance Budget; `conventions.md` § Testing & Playtesting.
- **Decisions:** the operator's movement decisions of 2026-10-23 (runtime decisions 11–17 and the accepted design-panel recommendations). `decisions.md` holds them as Draft rows. This prompt is their canonical home until completion.
- **GDD version:** v0.07 (after Phase 1).
- **Depends on:** Phase 1 `movement-sim`. Its prompt is the contract for:
  - `TileGrid(width, height)` / `Set` / `Get`; `TileKind { None = 0, Solid = 1, OneWay = 2, Climbable = 3 }`
  - `RopeSegment(x, bottom, top)`; `[Flags] Abilities { Jump, DoubleJump, WallJump, WallClimb, RopeClimb }`
  - `MovementSettings` (fields `RunSpeed, TimeToFullSpeed, JumpHeight, Gravity, MaxFallSpeed, AirJumps, WallSlideSpeed, ClimbSpeed`, `Default*` / `Min*` / `Max*` consts, `Default`, `Clamped()`)
  - `MovementInternals`; `StartPlacement.Find` (`X, Y, Moved, Found`)
  - `PlayerInput(moveX, moveY, jumpPressed, jumpHeld)`; `PlayerState` (`X, Y` = feet centre, `Facing`, `Contact`, …)
  - `PlayerStepper(grid, ropes, settings)` with `CreateState`, `ApplySettings`, `Step`
  - the test helpers `AsciiLevel` / `PlayerScenario`
  - If an implemented name differs from the Phase 1 prompt, the implemented name wins. Note it in the phase log.

## File Manifest

Package root `P/` = `Packages/com.orcapafea.daedalus/`.

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `P/package.json` | Dependency `com.unity.inputsystem` 1.11.2 |
| MODIFY | `Packages/manifest.json` | `testables` += `com.unity.inputsystem` |
| MODIFY | `Tools/unity_tests.py` | Compat project: pin Input System to the package's declared version; `testables` += inputsystem |
| MODIFY | `P/Runtime/Orcapafea.Daedalus.asmdef` | Reference `Unity.InputSystem` |
| MODIFY | `P/Tests/PlayMode/Orcapafea.Daedalus.Tests.PlayMode.asmdef` | Reference `Unity.InputSystem`, `Unity.InputSystem.TestFramework` |
| MODIFY | `P/Tests/EditMode/AssemblyDirectionTests.cs` | Runtime may reference Sim + `Unity.InputSystem`, never an editor assembly |
| MODIFY | `P/Tests/EditMode/PackageManifestTests.cs` | Exact expected dependency set |
| NEW | `P/Tests/PlayMode/InputFixtureTests.cs` | Proves `InputTestFixture` runs (both editors) |
| NEW | `P/Runtime/TileCollisionKind.cs` | Serialized enum: None 0, Solid 1, OneWay 2, Climbable 3 (4 reserved for hazard, not declared) |
| NEW | `P/Runtime/DaedalusTile.cs` | `Tile` subclass with a collision kind + `dataVersion` |
| NEW | `P/Runtime/PlayerMovementSection.cs` | Serializable Movement section (5 settings) |
| NEW | `P/Runtime/PlayerAbilitiesSection.cs` | Serializable Abilities section (4 toggles + 3 settings) |
| NEW | `P/Runtime/Player.cs` | The Player component |
| NEW | `P/Runtime/PlayStart.cs` | Readonly start: feet (world) + abilities |
| NEW | `P/Runtime/Rope.cs` | The Rope component: length + Scene-view line |
| MODIFY | `P/Runtime/Sim/RopeSegment.cs` | `DefaultLength` / `MinLength` / `MaxLength` consts (Step 4); `SnapTopTo(TileGrid)`: rope tops snap onto a one-way surface (Step 6) |
| MODIFY | `P/Runtime/Sim/MovementInternals.cs` | `MapHeadroomRows` (Step 5), `RopeTopSnap` (Step 6) |
| MODIFY | `P/Runtime/Sim/PlayerState.cs` | `Teleported` one-tick flag |
| MODIFY | `P/Runtime/Sim/PlayerStepper.cs` | Sets `Teleported` on fall-out reset |
| NEW | `P/Runtime/CollisionMap.cs` | Read result: `TileGrid` + world origin |
| NEW | `P/Runtime/CollisionMapReader.cs` | Collision Tilemap → `CollisionMap` (play start) |
| NEW | `P/Runtime/RopeCollector.cs` | One sorted `FindObjectsByType<Rope>` scan → `RopeSegment[]` |
| NEW | `P/Runtime/PlayerSession.cs` | Plain C# driver: grid, ropes, stepper, states, accumulator, interpolation, restart |
| NEW | `P/Runtime/PlayerControls.cs` | Input System actions in code → per-tick `PlayerInput` with jump latch |
| NEW | `P/Runtime/PlayerWarnings.cs` | Plain-words warning texts (one place) |
| NEW | `P/Tests/EditMode/Runtime/TileAssetTests.cs` | Enum values, defaults |
| NEW | `P/Tests/EditMode/Runtime/PlayerLayoutTests.cs` | Serialized names, defaults from the sim, ranges, tooltips, menu paths, auto-fill |
| NEW | `P/Tests/EditMode/Runtime/RopeComponentTests.cs` | Rope defaults, ranges, segment conversion, collection and sort (not `RopeTests`: Phase 1's sim `RopeTests` shares the test namespace) |
| MODIFY | `P/Tests/EditMode/Movement/RopeTests.cs` | Rope-top snap tests (Step 6) |
| NEW | `P/Tests/EditMode/Runtime/CollisionMapReaderTests.cs` | Kinds, origin, bounds growth, no authored writes, grid warnings |
| NEW | `P/Tests/EditMode/Runtime/PlayerSessionTests.cs` | Ticks, latch use, interpolation, snap, restart, settings |
| NEW | `P/Tests/EditMode/Runtime/StudentPathTests.cs` | Add Component → paint plain tiles → Play → lands |
| MODIFY | `P/Tests/EditMode/Movement/RunAndFallTests.cs` | `Teleported` true only on the reset tick |
| MODIFY | `P/Tests/EditMode/Movement/MovementSettingsTests.cs` | Pin `MapHeadroomRows`; retuned values (final step) |
| MODIFY | `P/Tests/EditMode/Movement/TileGridTests.cs` | Pin rope length consts |
| NEW | `P/Tests/PlayMode/TestImages.cs` | Shared screenshot helpers extracted from `RenderSmokeTests` |
| MODIFY | `P/Tests/PlayMode/RenderSmokeTests.cs` | Uses `TestImages` (no behaviour change) |
| NEW | `P/Tests/PlayMode/Movement/TestLevel.cs` | ASCII → Grid/Tilemaps/tiles/ropes/Player/camera in code |
| NEW | `P/Tests/PlayMode/Movement/PlayerControlsTests.cs` | Bindings, digitizing, latch, press+release in a frame |
| NEW | `P/Tests/PlayMode/Movement/PlayerPlayTests.cs` | Landing, keyboard run/jump, restart, live settings, ability timing |
| NEW | `P/Tests/PlayMode/Movement/PlayerRenderTests.cs` | Feet alignment, interpolation, reset snap, facing, screenshots |
| NEW | `P/Tests/PlayMode/Movement/PlayerWarningTests.cs` | Each warning once, nothing auto-removed |
| NEW | `P/Tests/PlayMode/Movement/MovementTourTests.cs` | Scripted run over one-way, wall, climbable, rope with screenshots |
| NEW | `P/Tests/PlayMode/Movement/PlayerAllocationTests.cs` | Zero allocation in the frame loop |
| NEW | `Assets/Dev/Editor/MovementTestSceneBuilder.cs` | Generates the dev test scene, tiles, sprites, palette; batch entry |
| NEW (generated) | `Assets/Dev/MovementTest/…` | `MovementTest.unity`, tile assets, PNG sprites (LFS), palette, `.meta`s |

Every new file and folder is committed with its `.meta`. Doc edits are listed in `## Notes to GDD`.

---

## Conventions for every step

- **Namespaces:**
  - `Orcapafea.Daedalus` (runtime), `Orcapafea.Daedalus.Editor` (editor), `Orcapafea.Daedalus.Sim` (sim edits)
  - `Orcapafea.Daedalus.Dev` (dev code in `Assets/Dev/`), `Orcapafea.Daedalus.Tests` (tests)
  - One primary type per file; the filename matches the type.
- **Frame loop (`Player.Update` and everything it calls):**
  - no allocations, LINQ, closures, boxing, `foreach` over interfaces, `GetComponent`, `Find*` or logging
  - components cached in `Awake`; play-start work in `Start`
  - warnings are one-shot, issued at play start or on a state change
- **Runtime boundaries:**
  - The runtime makes no gameplay decision: it converts data, feeds the sim and draws sim state.
  - It never writes authored data: no `CompressBounds`, no edits to assets or to other objects' serialized fields.
- **Tunable defaults** come only from sim constants (`MovementSettings.Default*`, `RopeSegment.DefaultLength`). Inspector `[Range]`s use the sim's `Min*`/`Max*` consts. No literal default appears in runtime code.
- **Warnings:**
  - `Debug.LogWarning(text, context)` in plain words, with the object as context
  - every text lives in `PlayerWarnings` so tests assert the same string
  - a warning says what is wrong and what to do; it never names code
- **During a step:** run `python Tools/unity_tests.py --editor 6000.6` with the relevant `--platform`. Both editors run at Step 16. Close the dev project in the Unity Editor first.
- **Tests:** assert through the real entry points (`Player` lifecycle, `PlayerSession.Tick`, the real Input System actions). Every test asserts its precondition. Screenshots go to `DAEDALUS_TEST_RESULTS` / `TestResults` as `<unity version>-movement-<name>.png`.

---

## Steps

### Step 1: Prove the Input System dependency and `InputTestFixture` on both editors
- **MODIFY `P/package.json`**: add `"com.unity.inputsystem": "1.11.2"`. This is the version Unity 6000.0's 2D template pins; it was confirmed in `com.unity.template.2d-cross-platform-5.1.0.tgz`.
- **MODIFY `Packages/manifest.json`**: `"testables": ["com.orcapafea.daedalus", "com.unity.inputsystem"]`. The dev project keeps Input System 1.20.1 (it resolves upward).
- **MODIFY `Tools/unity_tests.py`** (`ensure_compat_project`):
    - Set `testables` to our package + `com.unity.inputsystem`.
    - Set `dependencies["com.unity.inputsystem"]` to the version read from our `package.json`, so the minimum editor runs the minimum dependency. The 6000.0 clone currently resolves 1.20.0, so nothing has tested 1.11.2 yet.
    - Update the docstring.
- **MODIFY `P/Runtime/Orcapafea.Daedalus.asmdef`**: `references` += `"Unity.InputSystem"`.
- **MODIFY `P/Tests/PlayMode/…asmdef`**: `references` += `"Unity.InputSystem"`, `"Unity.InputSystem.TestFramework"`.
- **MODIFY `AssemblyDirectionTests.cs`**:
    - `RuntimeReferencesOnlyTheSim` becomes `RuntimeReferencesOnlyTheSimAndTheInputSystem`. The references must be a subset of {Sim, `Unity.InputSystem`}.
    - Add an explicit assert that the runtime references no assembly named `Orcapafea.Daedalus.Editor` and none ending in `.Editor`. The direction guard stays meaningful.
    - The sim and editor tests are unchanged.
- **MODIFY `PackageManifestTests.cs`**: add `DependenciesAreExactlyTheExpectedSet`. It parses the block into name → version and asserts equality with {`com.unity.modules.imageconversion` 1.0.0, `com.unity.modules.tilemap` 1.0.0, `com.unity.inputsystem` 1.11.2}. A dependency added without a test edit fails.
- **NEW `P/Tests/PlayMode/InputFixtureTests.cs`**:
    - Uses an `InputTestFixture` by composition (`Setup()` / `TearDown()` from `[SetUp]` / `[TearDown]`).
    - Adds a `Keyboard` and a `Gamepad`.
    - Creates an `InputAction` bound to `<Keyboard>/space` and `<Gamepad>/buttonSouth` and enables it.
    - `Press` → `yield return null` → asserts `WasPressedThisFrame()` and `IsPressed()`.
    - `Release` → asserts `!IsPressed()`.
    - Press and release in the same frame → `WasPressedThisFrame()` true, `IsPressed()` false (the latch contract Step 8 relies on).
- **Checks while doing it:**
    - Read the compat project's `Packages/packages-lock.json` after the run: `com.unity.inputsystem` must be 1.11.2.
    - Note Active Input Handling in both projects (dev `activeInputHandler: 1`, compat `2`).
    - Confirm from `results.xml` that no Input System test assemblies of its own ran. If they did, add `-assemblyNames` with our two test assemblies to the wrapper's command.
- **Fallbacks (record whichever fired in `framework.md` and the phase log):**
    - If 1.11.2 does not resolve or compile on 6000.0: leave the compat pin out, keep declaring 1.11.2, and tell the operator. Declaring a higher minimum is an operator decision.
    - If `InputTestFixture` cannot run on either editor: drop the TestFramework reference. Steps 8–9 then use a **thin input seam**:
      - `PlayerControls` gets an internal constructor taking a per-frame provider of raw values (move vector, stick vector, jump pressed/held), so the latch and digitizing stay tested.
      - The device → action bindings are covered only by the operator play-check.
      - Only in that case.
- **Tests:** the guard updates above and `InputFixtureTests`. Plant a failure: change only the expected set in `DependenciesAreExactlyTheExpectedSet` and watch it go red, then revert.
- **Verify:** `[automated]` `python Tools/unity_tests.py` (both editors, both modes) green; `[manual]` lock-file version and handler settings recorded; the planted failure seen.

### Step 2: Tile asset with a collision kind
- **NEW `TileCollisionKind.cs`**:
    - `public enum TileCollisionKind { None = 0, Solid = 1, [InspectorName("One-way")] OneWay = 2, Climbable = 3 }`. Explicit values that are never renumbered; a comment says so and reserves `4` for hazard. `[InspectorName]` only changes the label (Unity would otherwise show "One Way"); the saved value is unchanged.
    - Hazard is **not declared** until combat-and-enemies (operator, 2026-10-23), so Unity's plain enum dropdown shows exactly the four kinds and no editor code is needed. Adding `Hazard = 4` later changes no saved value.
    - Values 0–3 equal the sim's `TileKind` numbers.
- **NEW `DaedalusTile.cs`**:
    - `[CreateAssetMenu(menuName = "Daedalus/Tile", fileName = "New Daedalus Tile")] public class DaedalusTile : Tile`
    - `[SerializeField, Tooltip("What this tile does on the Collision Tilemap: Solid, One-way (jump up through, drop down with Down + Jump), Climbable (climb with Wall Climb) or None (decoration). Ordinary tiles count as Solid.")] TileCollisionKind kind = TileCollisionKind.Solid;`
    - `[SerializeField, HideInInspector] int dataVersion = CurrentDataVersion;` with `internal const int CurrentDataVersion = 1`
    - `public TileCollisionKind Kind => kind;` and an `internal` setter for tests only (the dev generator lives in Assembly-CSharp-Editor, which has no `InternalsVisibleTo`, and writes `kind` through `SerializedObject`)
    - `internal static TileKind ToSimKind(TileCollisionKind)` as an explicit switch:
      - None/Solid/OneWay/Climbable map 1:1
      - an undefined value → throw (broken invariant)
- **Tests (`TileAssetTests.cs`, EditMode):**
    - enum numeric values pinned (0–3), and the enum declares exactly four members
    - `OneWay` carries `[InspectorName("One-way")]` (reflection)
    - `ToSimKind` maps 0–3 onto `TileKind` with the same number
    - a new `DaedalusTile` has kind Solid and `dataVersion` 1, read through `SerializedObject` on the field names `kind` / `dataVersion`
    - the `CreateAssetMenu` path is `Daedalus/Tile`
- **Verify:** `[automated]` EditMode green.

### Step 3: Player component, serialized layout only
- **NEW `PlayerMovementSection.cs`**: `[Serializable] public sealed class PlayerMovementSection`. Private `[SerializeField]` fields with final names, each initialised from `MovementSettings.Default*` and ranged from `Min*`/`Max*`:
    - `runSpeed`: "How fast the player runs, in tiles per second."
    - `timeToFullSpeed`: "Seconds from standing still to full run speed. 0 = instant."
    - `jumpHeight`: "How high a full jump goes, in tiles. Hold Jump for a full jump, tap it for a short hop."
    - `gravity`: "1 = normal. Higher = snappier jumps and falls. Jump height stays the same."
    - `maxFallSpeed`: "The fastest the player can fall, in tiles per second."
    - `internal` get/set properties for tests.
- **NEW `PlayerAbilitiesSection.cs`**: `[Serializable] public sealed class PlayerAbilitiesSection`.
    - Toggles, default off **(proposal)**. Each tooltip ends "Changes take effect the next time you press Play."
      - `doubleJump`: "On from the start: jump again in mid-air."
      - `wallJump`: "On from the start: slide down walls and jump off them."
      - `wallClimb`: "On from the start: cling to and climb Climbable tiles."
      - `ropeClimb`: "On from the start: grab ropes with Up or Down and climb them."
    - Numbers, from `MovementSettings` defaults and ranges:
      - `airJumps` (int): "How many extra jumps in mid-air. Used only when Double Jump is on."
      - `wallSlideSpeed`: "How fast the player slides down a wall, in tiles per second. Used only when Wall Jump is on."
      - `climbSpeed`: "How fast the player climbs walls and ropes, in tiles per second. Used only when Wall Climb or Rope Climb is on."
    - `internal` get/set properties.
    - `internal Abilities ToAbilities()` = `Jump` | the toggles' flags.
    - `airJumps`, `wallSlideSpeed` and `climbSpeed` stay visible even when their toggle is off. Rule 8's hiding of mode-only settings arrives with the `editor-tools` custom inspectors (Roadmap §1); until then the tooltips say when a setting applies. This is an interim state, not the final layout.
- **NEW `Player.cs`**: `[AddComponentMenu("Daedalus/Player")] [DisallowMultipleComponent] public sealed class Player : MonoBehaviour`.
    - `[SerializeField, Tooltip("The Tilemap the player stands on and bumps into. Filled in automatically with the Tilemap named Collision.")] Tilemap collisionTilemap;`
    - `[SerializeField] PlayerMovementSection movement = new PlayerMovementSection();` and `[SerializeField] PlayerAbilitiesSection abilities = new PlayerAbilitiesSection();`. These show as free foldouts; four plain bool toggles, no `[Flags]` dropdown.
    - `[SerializeField, HideInInspector] int dataVersion = CurrentDataVersion;` (1)
    - `internal MovementSettings BuildSettings()`: a struct, no allocation.
    - **`Reset()` (editor add time):**
      - one `FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)`
      - pick the one named exactly "Collision", else the only Tilemap, else leave empty
      - If 6000.6 marks this overload obsolete (warning gate), use the replacement behind a `UNITY_6000_x_OR_NEWER` version define with the 6000.0 call as the fallback. Record it in `framework.md`.
    - `internal` accessors for `CollisionTilemap`, `Movement`, `Abilities`.
- **Tests (`PlayerLayoutTests.cs`, EditMode):**
    - Through `SerializedObject`: these property paths exist, and nothing else is visible beyond `m_Script`: `collisionTilemap`, `movement.runSpeed`, `movement.timeToFullSpeed`, `movement.jumpHeight`, `movement.gravity`, `movement.maxFallSpeed`, `abilities.doubleJump|wallJump|wallClimb|ropeClimb|airJumps|wallSlideSpeed|climbSpeed`, `dataVersion`.
    - Section sizes: Movement 5, Abilities 7 (≤ 8).
    - Every number's initial value equals its `MovementSettings.Default*`, and every `[Range]` equals `Min*`/`Max*` (reflection over field attributes).
    - The gravity tooltip text is exact, and the three "Used only when …" tooltips are present. Every visible field has a tooltip.
    - `dataVersion` is 1 and `[HideInInspector]`; `AddComponentMenu` is "Daedalus/Player".
    - `ToAbilities()` always contains `Jump`.
    - **Auto-fill:** in a new empty scene, `ObjectFactory.AddComponent<Player>` (the Inspector's path) fills the field. Cases: a "Collision" Tilemap among three; a single unnamed Tilemap; two unnamed → empty; none → empty. If `ObjectFactory.AddComponent` does not invoke `Reset()` on either editor: record it in `framework.md`, call `Reset` through an internal hook in the test, and let the operator editor check carry the real-path proof.
- **Verify:** `[automated]` EditMode green.

### Step 4: Rope component
- **MODIFY `P/Runtime/Sim/RopeSegment.cs`**: `public const float DefaultLength = 4, MinLength = 1, MaxLength = 50` **(proposal)**.
- **NEW `Rope.cs`**: `[AddComponentMenu("Daedalus/Rope")] public sealed class Rope : MonoBehaviour`.
    - `[SerializeField, Range(RopeSegment.MinLength, RopeSegment.MaxLength), Tooltip("How far the rope hangs down from this object, in tiles. Changes take effect the next time you press Play.")] float length = RopeSegment.DefaultLength;`
    - `[SerializeField, HideInInspector] int dataVersion = 1`
    - The rope hangs from the object's position: top = `position.y`, bottom = top − length, centre line x = `position.x`.
    - `internal RopeSegment ToSegment(Vector2 origin)` subtracts the map origin.
    - `OnDrawGizmos()`: a line from the top to the bottom plus a short cross-bar at the top, so students see the length in the Scene view. This is a MonoBehaviour message, not editor code.
- **Tests (`RopeComponentTests.cs`, EditMode, part 1):**
    - the defaults come from the sim consts and the range is Min/Max
    - field names via `SerializedObject`; `dataVersion` 1
    - `ToSegment` with a rope at (3.5, 10), length 4, origin (−2, 1) → X 5.5, Bottom 5, Top 9
    - menu path "Daedalus/Rope"
- **Tests (`TileGridTests.cs`):** pin the three rope consts.
- **Verify:** `[automated]` EditMode green.

### Step 5: Read the Collision Tilemap
- **MODIFY `P/Runtime/Sim/MovementInternals.cs`**: `public const int MapHeadroomRows = 24` (operator, 2026-10-23). 6 × (1 + 3) = 24 is how far the **feet** can rise above the highest surface with every setting at max (jump height 6, 3 air jumps). The head is 1.5 above the feet, so at those extreme settings the head meets the map top 1.5 tiles before that apex; at defaults (3.5 × 2 = 7) the headroom is far more than needed. Pinned in `MovementSettingsTests`. With it, a student who paints one floor row and places the Player above it never starts inside the map's solid top.
- **NEW `CollisionMap.cs`**: `sealed class` holding `Grid` (`TileGrid`), `Origin` (`Vector2`, the world position of sim cell (0, 0)'s lower-left corner) and `CellMin` (`Vector2Int`). Methods `WorldToSim(Vector2)` / `SimToWorld(Vector2)`.
- **NEW `CollisionMapReader.cs`**: `internal static class`. `Read(Tilemap tilemap, Rect includeWorld, List<string> problems)` → `CollisionMap`. Play start only.
    - **Bounds:** start from `tilemap.cellBounds`. **Never `CompressBounds()`**, which writes authored data.
      - Grow it to contain the cells under `includeWorld` (the Player start hitbox and every rope; Step 6 / Step 9 pass it).
      - Then add `MapHeadroomRows` rows on top.
      - An empty Tilemap with nothing to include → a 1 × 1 grid of None, plus a problem text.
    - **Cells:** one `GetTilesBlock(cellBounds)` call. Then, per cell in the authored bounds, read by cell coordinates and set `(cell − CellMin)`:
      - `null` → None
      - a `DaedalusTile` → `ToSimKind(kind)`
      - any other `TileBase` (plain `Tile`, Rule Tile, Animated Tile…) → Solid
      - The kind is cached per distinct `TileBase` in a `Dictionary` local to the call.
    - **Origin:** `tilemap.CellToWorld(CellMin)` (covers Grid and Tilemap position offsets).
    - **Grid checks** (each adds one plain-words problem from `PlayerWarnings`):
      - Grid cell size ≠ (1, 1)
      - cell layout ≠ Rectangle
      - Tilemap or Grid rotation ≠ identity
      - lossy scale ≠ (1, 1)
    - Only the given Tilemap is read. Background/Foreground never are.
- **Tests (`CollisionMapReaderTests.cs`, EditMode, objects built in a new scene):**
    - plain `Tile` → Solid; `DaedalusTile` OneWay/Climbable/None → those kinds; empty → None
    - two Tilemaps: tiles on the other one don't appear
    - a Tilemap starting at cell (−5, −3) with the Grid at (0.5, 2) → `Origin` correct, and `SimToWorld(WorldToSim(p)) == p`
    - **bounds growth:** a one-row floor with the start 3 tiles above → the grid includes the start and its top is ≥ start row + 24; a rope to the right of all tiles is included; a start below the floor is included
    - `tilemap.cellBounds` is unchanged after `Read` (no authored write); the tile count is unchanged
    - grid cell size 0.5, a rotated Tilemap and a scaled Grid each add exactly one problem; a clean setup adds none
    - an empty Tilemap → a 1 × 1 grid + a problem
- **Verify:** `[automated]` EditMode green.

### Step 6: Collect the ropes (the play-start exception to Architecture rule 5)
- **NEW `RopeCollector.cs`**: `internal static class`.
    - `Rope[] FindSorted()`:
      - exactly one `FindObjectsByType<Rope>(FindObjectsSortMode.None)`; keep `isActiveAndEnabled` ropes
      - sort deterministically by (x, bottom, top) with a static comparison method, so "lowest index wins" in the sim is stable across runs
      - The XML doc names this the documented play-start-only exception to rule 5 and says it is never called per frame.
    - `bool TryGetWorldBounds(Rope[], out Rect bounds)` for the reader's `includeWorld`: false for no ropes, and the caller then includes only the start hitbox. A default `Rect` at (0, 0) would otherwise grow the map to the world origin.
    - `RopeSegment[] ToSegments(Rope[], Vector2 origin)`.
    - The same version-define note as Step 3 applies if the overload is obsolete on 6000.6.
- **Tests (`RopeComponentTests.cs`, part 2, EditMode):**
    - three ropes created in shuffled order come back sorted by x; equal x sorts by bottom
    - a disabled Rope and a Rope on an inactive object are skipped
    - segments match `ToSegment`
    - no ropes → an empty array (not null), and `TryGetWorldBounds` returns false
    - no ropes, tiles painted far from the origin (x 200–220) → the read map's `CellMin` is the tiles' min, not (0, 0)
- **MODIFY `P/Runtime/Sim/MovementInternals.cs`**: `public const float RopeTopSnap = 0.5f` (tiles; operator, 2026-10-23).
- **MODIFY `P/Runtime/Sim/RopeSegment.cs`**: `public RopeSegment SnapTopTo(TileGrid grid)`. Students place ropes without grid snap, so a rope top almost never lands within Phase 1's `RopeTopTolerance` (0.0625) of a platform. This rule lives in the sim, not the runtime.
    - In the rope's column `floor(X)`, find the OneWay cell whose top surface (integer row top) is nearest to `Top` within `RopeTopSnap`. Ties go to the higher surface.
    - If one exists, return the segment with `Top` set exactly to that surface (and `Bottom` unchanged; if that makes `Top ≤ Bottom`, return the segment unchanged). Otherwise return it unchanged.
- **MODIFY `P/Runtime/Sim/PlayerStepper.cs`**: the constructor snaps each rope with `SnapTopTo(grid)` as it copies the list (play start, once). `ApplySettings` doesn't re-snap.
- **Tests:**
    - sim (`Tests/EditMode/Movement/RopeTests.cs`, Phase 1's file): a rope top 0.3 below a one-way surface snaps up to it and the player steps onto the platform; 0.4 above snaps down; 0.6 away doesn't snap; with surfaces at equal distance the higher one wins; a Solid cell at the top is never a snap target; a snap that would invert the segment is skipped
    - runtime (`RopeComponentTests.cs`, part 2): segments match `ToSegment`, before the sim snap
- **Verify:** `[automated]` EditMode green; `[grep]` `FindObjectsByType` appears only in `RopeCollector.cs` and the Player's shared Tilemap pick (used by `Reset` and by `Start` when the field is empty).

### Step 7: Session driver, interpolation, `Restart(PlayStart)` and the reset snap
- **NEW `PlayStart.cs`**: `public readonly struct PlayStart`.
    - `Vector2 Feet` (world, feet centre) and `Abilities Abilities`, set by the constructor.
    - XML doc: play-from-here's ability set **replaces** the Player's toggles, and the same struct serves checkpoint respawn and tests.
- **MODIFY `P/Runtime/Sim/PlayerState.cs` + `PlayerStepper.cs`**: add `public bool Teleported`. It is true only on the tick where the stepper replaced the state with a fall-out reset, and false on every other tick, including the tick after a reset.
- **NEW `PlayerSession.cs`**: `internal sealed class`.
    - **Constructor** `(CollisionMap map, RopeSegment[] ropes, MovementSettings settings, PlayStart start)`: builds a `PlayerStepper` and a `FixedStepAccumulator`, then calls `Restart(start)`.
    - **`Restart(PlayStart start)`:**
      - converts feet to sim coordinates
      - `StartPlacement.Find`, stored as `LastPlacement`
      - `CreateState(x, y, start.Abilities | Abilities.Jump)` (`Jump` is always on in the runtime) **(proposal)**
      - `Previous = Current`; a fresh accumulator (fraction 0)
    - **`int AdvanceClock(double seconds)`:** `accumulator.Advance(seconds)`. It is separate from `Tick` so the caller pulls one input per tick and the session stays input-agnostic.
    - **`Tick(in PlayerInput input)`:** `Previous = Current; Current = stepper.Step(Current, input); if (Current.Teleported) Previous = Current;`
    - **`Vector2 InterpolatedFeetWorld`:** `map.SimToWorld(lerp(Previous, Current, (float)InterpolationFraction))`.
    - **`ApplySettings(MovementSettings)`:** forwards to the stepper.
    - Getters `Current`, `Previous`, `LastPlacement`, `Map`.
- **Tests:**
    - **`RunAndFallTests.cs` (sim):** walking into a pit → `Teleported` true on exactly one tick, at the start position; false on the next tick and on every tick of an ordinary run/jump sequence.
    - **`PlayerSessionTests.cs` (EditMode, `AsciiLevel` grids wrapped in a `CollisionMap` with a non-zero origin):**
      - `AdvanceClock(1/60.0)` → 1 tick; `AdvanceClock(0.5)` → 5 (hitch cap)
      - at 1/90 s per frame the fraction cycles and `InterpolatedFeetWorld` lies on the segment Previous→Current at that fraction (a falling player, so the positions differ)
      - **reset snap:** on the reset tick the rendered position equals the start exactly, with no point between the pit and the start
      - `Restart` mid-fall → state at the new start, `Previous == Current`, fraction 0, abilities = the given set plus `Jump` (given only `RopeClimb`: the toggles don't matter, `Jump` is present)
      - a start inside solid → `LastPlacement.Moved`; enclosed → `Found == false`
      - `ApplySettings(RunSpeed 10)` changes the run target on the next tick
- **Verify:** `[automated]` EditMode green.

### Step 8: Controls (Input System actions in code)
- **NEW `PlayerControls.cs`**: `internal sealed class : IDisposable`. Created and enabled in `Player.OnEnable`, disabled and disposed in `OnDisable`. It never uses the project-wide actions asset.
    - **Actions:**
      - `move` (Value, Vector2): `2DVector` composites in Digital mode (no diagonal normalising) for WASD and the arrow keys, plus `<Gamepad>/dpad`
      - `stick` (Value, Vector2): `<Gamepad>/leftStick`
      - `jump` (Button): `<Keyboard>/space`, `<Gamepad>/buttonSouth`
      - **No binding on Up for jump.**
    - **`ReadFrame()`**, once per frame:
      - `jumpLatched |= jump.WasPressedThisFrame()`; `jumpHeld = jump.IsPressed()`
      - digital axes: `move` components with |v| ≥ 0.5 → sign
      - the stick, per axis, only where the digital axis is 0:
        - x counts when |x| ≥ 0.5
        - up counts when y ≥ 0.5 **(proposal)**
        - down counts when y ≤ −0.5 **and** |y| > |x**, so a running diagonal never drops through a platform by accident
    - **`PlayerInput TakeTickInput()`**, called once per tick: returns `(moveX, moveY, jumpLatched, jumpHeld)` and clears the latch. Only the first tick of a frame sees the press. A frame with zero ticks keeps the latch. Press + release in one frame → pressed true, held false (a one-tick tap).
    - No allocation in `ReadFrame` / `TakeTickInput`.
- **Tests (`PlayerControlsTests.cs`, PlayMode, `InputTestFixture`; `PlayerControls` constructed directly):**
    - right arrow / D → (1, 0); left arrow / A → (−1, 0); S / down arrow → moveY −1; Right + Down on keys → (1, −1)
    - d-pad left/up
    - stick (0.4, 0) → 0; (0.6, 0) → 1; (0.6, −0.7) → down; (0.8, −0.7) → no down; (0, 0.6) → up
    - Space and gamepad South → pressed on the first take only, held while down
    - **Up arrow, W, d-pad up and stick up never set `jumpPressed`**
    - press + release within one frame → pressed true, held false
    - **latch survives a zero-tick frame:** frame 1 press, `ReadFrame`, no take; frame 2 `ReadFrame`, take → pressed; take again → not pressed
    - `Dispose` disables the actions
- **Verify:** `[automated]` PlayMode green (6000.6).

### Step 9: Player drives the session (play start, frame loop, restart, live settings)
- **MODIFY `Player.cs`**:
    - **`Awake`:** cache `SpriteRenderer` (same object), `Rigidbody2D`, `Collider2D` (for Step 11) and the **placed feet**. The placed feet are captured before anything moves the transform, so `DefaultStart` stays correct after play has moved the player. Feet = transform x, transform y + the feet offset from Step 10.
    - **`public PlayStart DefaultStart`** → `new PlayStart(placedFeet, abilities.ToAbilities())`.
    - **`Start`:**
      1. Resolve the Tilemap: the `collisionTilemap` field, else the play-start pick described below the list.
      2. `RopeCollector.FindSorted()`.
      3. `CollisionMapReader.Read(resolved Tilemap, include = start hitbox ∪ rope bounds, problems)`. The start is `pendingStart ?? DefaultStart`.
      4. `new PlayerSession(map, ropes.ToSegments(origin), BuildSettings(), start)`.
      5. Problems → warnings (Step 11).
      - **The pick (used by item 1):** an empty `collisionTilemap` field (a Player saved as a prefab can't keep a scene reference, and `Reset` doesn't run on instantiation; or the Tilemap was created or renamed after the Player) → apply the same pick as `Reset` at play start ("Collision", else the only Tilemap), **without** writing the field, covered by the rule-5 play-start exception. Only when that also finds nothing: build an empty map, so the player falls and resets, with the Step 11 warning; no exception.
    - **`public void Restart(PlayStart start)`:** before `Start` → store as `pendingStart`; after → `session.Restart(start)` and render immediately. No statics; the later editor tool keeps its own `SessionState` and calls this on `EnteredPlayMode`. Either order works for the **state**. The **map** is fixed at play start, grown around `pendingStart ?? DefaultStart`, so a `Restart` that arrives after `Start` is reliable only for a point inside that map; outside it, `StartPlacement.Find` moves or fails it, with the Step 11 warning. Checkpoints are inside the map, so they are fine. **`editor-tools` inherits this:** play-from-here must deliver its start before `Start` runs, or rebuild the map; that milestone verifies the callback order.
    - **`Update()` → `RunFrame(Time.deltaTime)`.** Scaled time, so `timeScale = 0` pauses. `internal void RunFrame(float dt)` does:
      1. `if (settingsDirty) { settingsDirty = false; session.ApplySettings(BuildSettings()); }`
      2. `controls.ReadFrame()`
      3. `int n = session.AdvanceClock(dt)`; `n ×` `session.Tick(controls.TakeTickInput())`
      4. render (Step 10)
    - **`OnValidate()`:** `settingsDirty = true`. That is all; no struct compares, no boxing. Number settings apply live; ability toggles are read only at play start (`Start` / `DefaultStart`).
    - `internal PlayerSession Session` for tests.
- **Tests (`PlayerPlayTests.cs`, PlayMode, `InputTestFixture`, `Time.captureFramerate = 60`, so one tick per frame; restored in teardown; levels built with `TestLevel`, Step 12 helper, created here):**
    - spawned 3 tiles above a plain-tile floor → after 60 frames `Contact` is Ground and the feet are on the floor top
    - hold right 30 frames → x increases by the expected run distance (± one tick); Space → apex ≈ jump height above the floor (± 0.05)
    - `Restart` called right after `AddComponent` (before `Start`) → the first state is at that start; `Restart` after 30 frames → the state and transform are at the new start on the same frame
    - `Restart` with a `PlayStart` containing `RopeClimb` while the toggle is off → the rope can be grabbed; toggle on but `PlayStart` without it → passive
    - **live settings:**
      - under `#if UNITY_EDITOR`, set `movement.runSpeed` to 10 through `SerializedObject` + `ApplyModifiedProperties`, which triggers `OnValidate`, the real path. The player then reaches 10 within the expected ticks.
      - `abilities.doubleJump` toggled on mid-play → no air jump until restart (prompt rule: toggles apply at play start only)
- **Verify:** `[automated]` PlayMode green.

### Step 10: Rendering, feet alignment and facing
- **MODIFY `Player.cs`**:
    - **Feet offset**, computed once in `Awake`: `spriteRenderer.sprite.bounds.min.y × transform.lossyScale.y`, or 0 without a sprite.
    - **Render:** `transform.position = (feet.x, feet.y − feetOffset, z)` from `session.InterpolatedFeetWorld`, with z unchanged.
    - **Facing:** `spriteRenderer.flipX = Current.Facing < 0`, written only when it changes. The placeholder art faces right.
    - Render once in `Start` after the session is built, so the first frame is at the placed start, not snapped later.
    - **Hitbox outline:** `OnDrawGizmosSelected()` draws the collision box (`MovementInternals.HitboxWidth` × `HitboxHeight`, 0.75 × 1.5) as a rectangle standing on the feet: in edit mode the feet are the transform y + the sprite's `bounds.min.y` offset, in Play the session's feet. A MonoBehaviour message like the Rope line, no editor code. A student whose sprite isn't 0.75 × 1.5 tiles then sees why the player bumps a ceiling above its visible top or won't fit a gap the sprite seems to fit.
- **Tests (`PlayerRenderTests.cs`, PlayMode):**
    - **feet:** after landing, `spriteRenderer.bounds.min.y` equals the floor top (± 1e-4) for a centre-pivot 12 × 24 px sprite, a bottom-pivot sprite and a centre-pivot sprite at scale 2
    - **pixels:** with a 16-px-per-tile camera, the pixel row just above the floor top inside the player's columns is player colour and the row just below is tile colour (no gap, no sink). Save `movement-feet.png`.
    - **interpolation:** at `captureFramerate = 90`, a falling player's transform is strictly between `Previous` and `Current` (as world positions) on a frame where the fraction is in (0, 1)
    - **reset snap:** walking into a pit, the transform goes from below the map straight to the start on one frame; no frame shows a point between them
    - walking left sets `flipX`, walking right clears it
- **Verify:** `[automated]` PlayMode green; `[manual]` look at `movement-feet.png`.

### Step 11: Play-start warnings in plain words
- **NEW `PlayerWarnings.cs`**: `internal static class` of `const string` texts **(proposal wording)**:
    - **No Collision Tilemap:** "The Player has no Collision Tilemap, so there is nothing to stand on. Drag your Collision Tilemap into the Player's Collision Tilemap field."
    - **Rigidbody 2D or Collider 2D on the Player:** "The Player doesn't need a Rigidbody 2D or a Collider 2D; Daedalus moves it by itself. Remove them from the Player, or they may push it around." Never removed automatically.
    - **Moved start:** "The Player started inside a wall, so it was moved to the nearest free spot. Move the Player in the Scene view to fix this."
    - **No free start:** "The Player is buried in walls and no free spot is nearby. Move the Player out of the walls."
    - **Cell size:** "The Collision Tilemap's Grid has a Cell Size of {x} × {y}. Daedalus needs 1 × 1, so collisions won't match the picture."
    - **Rotation / scale:** "The Collision Tilemap (or its Grid) is rotated or scaled. Reset its Rotation to 0 and its Scale to 1, or collisions won't match the picture."
    - **Cell layout:** "The Collision Tilemap's Grid must use the Rectangle cell layout."
    - **Empty Tilemap:** "The Collision Tilemap is empty. Paint some tiles on it."
    - **No sprite:** "The Player has no sprite, so you won't see it. Add a Sprite Renderer with a sprite to this object."
    - **Rope top in a wall:** "This rope's top is inside a wall or ceiling tile, so the player stops below it. Move the rope down so its top is in an empty cell or on a one-way platform." Phase 1 Step 11 relies on this warning for ropes ending in Solid/Climbable.
    - **Old input setting:** under `#if !ENABLE_INPUT_SYSTEM`: "The controls need the Input System. Set Edit > Project Settings > Player > Active Input Handling to Input System Package (New) or Both."
- **MODIFY `Player.cs`**:
    - In `Start`, log each applicable warning **once** with the Player as context: reader problems, the cached Rigidbody2D/Collider2D (`GetComponent` in `Awake` only), `LastPlacement`, a missing sprite, the input setting.
    - In `Start`, after the ropes are snapped (the session's stepper holds the snapped segments): for each rope whose top cell (column `floor(X)`, row `ceil(Top) − 1`) is Solid or Climbable in the read map, log the rope warning once **with that Rope as context**, so clicking the message selects the rope.
    - `Restart` re-warns only for a moved or unfound start.
- **Tests (`PlayerWarningTests.cs`, PlayMode):**
    - each condition → its text logged **exactly once** over 120 frames, counted with the same test-local `Application.logMessageReceived` listener the clean case uses (`LogAssert.Expect` consumes one match and wouldn't fail on a second)
    - a rope whose top is inside a solid tile → the rope warning once, with that Rope as context; a rope whose top sits on (or snaps onto) a one-way → no warning
    - field empty, one Tilemap named "Collision" → the player lands and no warning is logged (the play-start pick)
    - the "Old input setting" warning can't be produced in either test project (handlers 1 and 2); it is **code-review only**, and the phase log says so
    - with a Rigidbody2D and a BoxCollider2D added, both components are still present afterwards and the player still lands
    - a clean `TestLevel` logs no warning: a test-local `Application.logMessageReceived` listener counts zero warnings
    - empty field in a scene with **no** Tilemap (and, separately, with two Tilemaps neither named "Collision") → no exception, the player falls and resets, and the "No Collision Tilemap" warning is logged once
- **Verify:** `[automated]` PlayMode green; `[grep]` no `Debug.Log` in `RunFrame` or anything it calls.

### Step 12: Dev test scene generator
- **NEW `Assets/Dev/Editor/MovementTestSceneBuilder.cs`**: `Orcapafea.Daedalus.Dev`, Assembly-CSharp-Editor, following the `PerfCheckBuild` pattern.
    - **Entry points:** menu `Daedalus Dev/Rebuild Movement Test Scene`, and `public static void Build()` for batch mode, which ends with `EditorApplication.Exit(0|1)`.
    - **Layout:** built from an ASCII array in the builder, with Phase 1's `AsciiLevel` symbols: `#` plain Tile, `-` one-way, `C` climbable, `R` rope cells, `+` one-way with a rope through it, `P` player; plus `b` Background-only decoration.
    - **One screen, fixed orthographic camera:** at most 35 × 22 tiles, camera size 11, centred, so the whole level fits a 16:10 school-laptop Game view (size 11 shows 22 rows and 35.2 columns at 16:10; 40 columns would lose about 5). Features may share rows to fit. It contains:
      - a floor
      - 3- and 4-tile ledges, and a 6-tile ledge for double jump
      - 5- and 6-tile gaps
      - a pit that drops out of the map
      - a stack of one-way platforms, one with a rope hanging through it
      - a pair of ordinary walls for wall jumps, and one tall single ordinary wall
      - a climbable wall with an open top (ledge pop) and one topped by solid
      - a 2-tall corridor
      - a rope next to a wall
      - Background decoration the player walks through
      - Each gate's width or height matches the Phase 1 gate tests.
    - **Assets**, all under `Assets/Dev/MovementTest/`, updated in place when present so GUIDs stay stable:
      - 16-px PNG sprites (LFS), imported as Sprite, 16 PPU, point filter, no compression
      - one plain `Tile` (solid) and `DaedalusTile`s for one-way and climbable
      - a Tile Palette with all three (`GridPaletteUtility.CreateNewPalette`)
      - a 12 × 24 px player sprite with a face pixel showing the facing
      - a rope sprite (a SpriteRenderer in Tiled mode, sized to the rope length)
    - **Scene:**
      - a Grid with Background, Collision and Foreground Tilemaps (Chunk mode), plus the camera
      - a Player with every ability on. The generator writes **every** setting through `SerializedObject` (it cannot see `internal` members; no `InternalsVisibleTo` is added for Assembly-CSharp-Editor): `collisionTilemap`, `abilities.doubleJump`, `abilities.wallJump`, `abilities.wallClimb`, `abilities.ropeClimb`, each `DaedalusTile`'s `kind` and each Rope's `length`. That also proves those field names.
      - the Rope objects
      - saved as `Assets/Dev/MovementTest/MovementTest.unity` and added to no build settings
    - **Self-check before exit:** exactly one Player; the collision tile count equals the ASCII count; the rope count is right; Background has tiles but Collision has none in decoration cells.
    - **Picture:** renders the camera once at 16:10 (560 × 350) to `TestResults/movement-dev-scene.png`, so the picture shows what the operator's view shows.
    - The scene's fileIDs change on every rebuild; the generator is the source.
- **Tests:** the generator's self-check, run in batch mode: `Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.MovementTestSceneBuilder.Build -logFile TestResults/dev-scene.log` (dev editor 6000.6.7f1, dev project closed).
- **Verify:** `[automated]` exit 0; `[manual]` look at `movement-dev-scene.png`: every feature is visible and nothing overlaps; `git status` shows the expected assets with `.meta`s. Run it twice and confirm the second run keeps the asset GUIDs.

### Step 13: PlayMode movement tour with screenshots
- **NEW `P/Tests/PlayMode/TestImages.cs`**: `Save(Texture2D, string name)`, `ReadBack(RenderTexture)`, `Count(Color32[], Color32)`, `SolidSprite(Color32, w, h, pivot)`. All moved from `RenderSmokeTests` with identical behaviour; the caller passes created objects in for teardown.
- **MODIFY `RenderSmokeTests.cs`**: uses `TestImages`. The render-smoke picture and asserts are unchanged.
- **NEW `P/Tests/PlayMode/Movement/TestLevel.cs`** (started in Step 9, finished here):
    - ASCII (the `AsciiLevel` symbols, Step 12) → Grid + Collision/Background Tilemaps + plain/`DaedalusTile` tiles + Rope objects + a Player (abilities via internal accessors, created inactive then activated so `Awake` sees the setup)
    - a camera on a `RenderTexture` at 16 px per tile
    - `Shot(name)`; cleanup of everything created
- **NEW `MovementTourTests.cs`** (`InputTestFixture`, `captureFramerate = 60`). Each scripted run asserts its outcome state and saves a picture:
    - jump up through a one-way, land on it, Down + Space drops back → `movement-oneway.png`
    - slide on an ordinary wall and wall-jump away → `movement-wall.png`
    - cling, climb and ledge-pop on a climbable wall → `movement-climb.png`
    - press Up at a rope (grab), climb to the one-way at its top and stand on it → `movement-rope.png`
    - Up arrow alone on the ground never leaves the ground
- **Verify:** `[automated]` PlayMode green; `[manual]` look at every `*-movement-*.png`: player standing on surfaces, nothing floating or clipped.

### Step 14: Zero-allocation guard for the frame loop
- **NEW `PlayerAllocationTests.cs`** (PlayMode, `InputTestFixture`):
    - **Setup:** build a tour level and warm up for 120 frames.
    - **Freeze Unity's own calls:** set `Time.timeScale = 0`. `Update` still runs but advances no ticks.
    - **Measure:** call `player.RunFrame(1/60f)` directly 600 times, in batches. Between batches, change the device state (hold right, press/release Space, hold Down, Up at the rope) and call `InputSystem.Update()`.
      - Each batch of `RunFrame` calls (and nothing else) runs inside the Test Framework's `Is.Not.AllocatingGCMemory()` constraint, as in Phase 1's `StepAllocationTests` (`GC.GetAllocatedBytesForCurrentThread` sees nothing in Unity; `framework.md`). The batch delegates are created before the window.
      - Assert every batch passes, and that the run passed through Ground, Air, OneWayTop, Rope and a reset.
      - The `settingsDirty` path is exercised once inside the window.
    - **Probe sanity first:** the same constraint around a used `new byte[1024]` must fail (no vacuous pass).
    - **Fallback:** Phase 1 proved the constraint in EditMode only. If it doesn't detect the planted allocation in PlayMode on an editor, record it in `framework.md`, `[Ignore]` with the reason, and code review alone carries the guarantee there.
- **Verify:** `[automated]` PlayMode green with the sanity branch observed.

### Step 15: Student-path test
- **NEW `StudentPathTests.cs`** (EditMode, `[UnityTest]`):
    1. `EditorSceneManager.NewScene(EmptyScene)`.
    2. Create a Grid with a Tilemap named "Collision", painted with a row of plain `Tile`s (sprite from code).
    3. Create a GameObject with a `SpriteRenderer` **with a sprite** 3 tiles above the floor. The sprite is made in the test the same way item 2 makes the tile sprite (`Texture2D` + `Sprite.Create`, 12 × 24 px, centre pivot, 16 PPU); `TestImages` lives in the PlayMode test assembly, which the EditMode assembly doesn't reference. Without one, the "No sprite" warning fires and the feet offset is 0.
    4. `ObjectFactory.AddComponent<Player>` → the field was auto-filled.
    5. `yield return new EnterPlayMode()`, then find the Player again (test code may `Find`; references die in the reload) and wait 90 frames.
    6. Assert Ground, the sprite bottom on the floor top, and no warnings.
    7. `yield return new ExitPlayMode()`.
    - No other component, setting or wiring is touched: this is a beginner's first Play.
    - **Fallback** if `EnterPlayMode` is unreliable in batch mode on either editor: keep the edit-mode half (Step 3 auto-fill) and add the PlayMode landing case without wiring beyond the field (Step 9 already lands on plain tiles). Record why in `framework.md`.
- **Verify:** `[automated]` EditMode green on 6000.6.

### Step 16: Run all tests
- `python Tools/unity_tests.py` (6000.0.85f1 and 6000.6.7f1, EditMode and PlayMode). Close the dev project first.
- Fix failures in the code or the test; never weaken an assertion to pass.
- Inspect `TestResults/*-render-smoke.png`, every `TestResults/*-movement-*.png` from **both** editors, and `movement-dev-scene.png`. Record what was seen in the phase log.
- Do not continue until all four runs are green with no compiler warnings.

### Step 17: Operator editor check (STOP for the operator)
Hand the operator this check in the dev project (6000.6.7f1). Record the outcome in the phase log.
- **Do:**
    1. New 2D scene.
    2. GameObject > 2D Object > Tilemap > Rectangular; rename the Tilemap "Collision".
    3. Open the Tile Palette, choose "MovementTest Palette" and paint a floor with the plain tile.
    4. GameObject > 2D Object > Sprites > Square; Add Component, type "Player", pick Daedalus > Player.
    5. Hover each setting.
    6. Press Play.
    7. Stop. Clear the Collision Tilemap field and press Play again: the player still lands, with no warning (the play-start pick). Stop.
    7b. Rename the Tilemap away from "Collision", add a second Tilemap, keep the field empty and press Play: the "No Collision Tilemap" warning appears once. Stop, rename it back.
    8. Add a Rigidbody 2D and press Play. Stop.
    9. Create > Daedalus > Tile, open its Kind dropdown.
    10. Add an empty object with Daedalus > Rope and drag its Length slider.
- **Observe:**
    - Player and Rope under Add Component → Daedalus
    - Collision Tilemap filled in by itself
    - two foldouts: Movement (5 sliders) and Abilities (4 checkboxes, 3 sliders); every one has a tooltip
    - the gravity tooltip reads exactly as specified; the "Used only when …" tooltips are present
    - the Kind dropdown shows None, Solid, One-way, Climbable
    - the rope's line hangs down in the Scene view and follows the slider
    - with the Player selected, a 0.75 × 1.5 hitbox outline stands on the Square's bottom edge (narrower and taller than the 1 × 1 Square)
    - in Play the square drops and stands on the floor
    - each problem gives one plain-words Console warning
- **Must be absent:**
    - any error or exception
    - a Hazard option in the Kind dropdown
    - a Data Version field, or any internal (coyote, buffer…), in the Inspector
    - a need to add any other component
    - the Rigidbody 2D removed by us
    - a warning on the clean setup
- Fix what fails, re-run Step 16, and repeat the check.

### Step 18: Operator play-check and tuning (STOP for the operator: the milestone gate)
In the dev scene `Assets/Dev/MovementTest/MovementTest.unity`, every ability on. Keyboard; gamepad too if one is available (note if not). Play at the laptop's normal refresh rate, and on a high-refresh display if available.
- **Do:**
    - run both ways
    - start, stop and turn
    - tap vs hold jump
    - jump just after running off a ledge (coyote)
    - press jump just before landing (buffer)
    - double jump to the 6-tile ledge; try the 3- and 4-tile ledges with a single jump
    - with a single jump (no air jump), clear the 5-tile gap and fail the 6-tile gap
    - wall-slide and wall-jump between the paired walls
    - try to climb the single plain wall by wall jumps
    - cling, climb and pop over the open climbable top; climb the one topped by solid
    - press Up and Down at the ropes; climb; jump off; step onto the platform at the rope top
    - jump up through and drop down through one-way platforms
    - run through the 2-tall corridor
    - fall into the pit
    - press Up with nothing around
    - stop Play, untick one ability at a time and replay: that surface must stay passive
    - **Phase 1's hand-over** (phase 1 log, "For the operator"): try each **(proposal)** in `systems.md` § Movement rules and confirm or change it, including Down + Jump on a rope (lets go without a jump) and climbing down a rope onto a one-way just under its bottom (lands on it); a jump pressed at the apex next to a wall is a wall jump, not an air jump; Phase 1's open questions: the 5 / 6 gap breakpoint, and no Run ability flag
- **Observe:**
    - movement feels responsive and the jump arc is satisfying
    - the full jump clears the 3-tile ledge and visibly not the 4
    - the sprite stands exactly on surfaces
    - smooth motion with no jitter at any refresh rate
    - facing flips with direction
    - the pit returns you to the start in one clean cut
- **Must be absent:**
    - jitter or stutter
    - the sprite floating above or sinking into the floor
    - snagging on corners
    - falling through a floor, or through a one-way without Down + Jump
    - a jump from Up
    - two jumps from one press
    - sticking to a wall without pressing toward it
    - gaining height on the single plain wall
    - grabbing a rope by touching it
    - a smear or slide during the reset
    - any Console warning or error
- **Tuning:**
    - The operator tunes the Movement and Abilities sliders live in Play mode and writes down or copies the values (Copy Component Values).
    - Internals (coyote, buffer, wall lock, fall-gravity factor, air control, minimum hop, time-to-apex, push-off) are changed by Claude in `MovementInternals` on the operator's description ("floaty", "wall jump too strong"), one change per replay.
    - Keep a tuning table in the phase log: setting, old, new, why.
- **The milestone gate is the operator saying the feel is right.** Do not continue without it.

### Step 19: Make the tuned values the defaults
- **MODIFY** `P/Runtime/Sim/MovementSettings.cs` (`Default*`; `Min*`/`Max*` only if the operator moved a range) and `P/Runtime/Sim/MovementInternals.cs` with the tuned values.
- **MODIFY** `MovementSettingsTests.cs` pins. Update any Phase 1 gate test whose breakpoint moved (ledge, gap, double-jump ledge, single-wall loss). The failure text names the new breakpoint, and the operator confirms each moved gate.
- **MODIFY** `Documents/GDD/entities.md` § Player tunables in the same change: the values mirror the sim.
- If a gate moved, update the dev layout's gate sizes in `MovementTestSceneBuilder.cs`.
- Re-run `python Tools/unity_tests.py --platform editmode` (both editors, the gate tests), then regenerate the dev scene (Step 12 command; its Player now carries the new defaults), then the full `python Tools/unity_tests.py`. Look at the pictures again.
- **Verify:** `[automated]` all green; `[grep]` no tuned number appears outside `MovementSettings` / `MovementInternals` / their tests / entities.md.

### Step 20 (Final): Code review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on every file changed in this phase. Ask specifically about:
    - per-frame allocations, LINQ, closures, `GetComponent` / `Find` / logging in `RunFrame` and below
    - `FindObjectsByType` only in `RopeCollector` and the Player's shared Tilemap pick (`Reset`, and `Start` when the field is empty)
    - no writes to authored data (no `CompressBounds`)
    - every default taken from the sim; serialized names matching this prompt
    - the jump latch consumed only by ticks
    - both ends of every wire: `Teleported` → snap; `OnValidate` → `ApplySettings`; `PlayStart` abilities → state; reader problems → warnings; rope tops in solid → the rope warning (Phase 1 relies on it)
    - the assembly direction; one type per file; warnings in plain words
- Fix any HIGH/MED issues, re-run Step 16, and report the review result to the user.

---

## Completion Criteria

- [ ] The package depends on Input System 1.11.2. The compat project runs on exactly 1.11.2, or the recorded fallback applies. `InputTestFixture` tests run on both editors. The guard tests were updated, and the manifest guard was seen failing on a planted change.
- [ ] `DaedalusTile`, `Player` and `Rope` have the serialized names, sections, `dataVersion` 1, sim-sourced defaults and ranges, tooltips and `Daedalus/…` menu entries in this prompt, pinned by tests. The tile enum declares exactly None/Solid/OneWay/Climbable (4 reserved for hazard). Rope tops snap onto a one-way surface within 0.5 tile.
- [ ] The Collision Tilemap read covers plain tiles as Solid, our kinds, origin offsets and bounds growth with headroom, and writes nothing. Grid problems warn.
- [ ] Ropes come from one sorted play-start scan; disabled ones are skipped.
- [ ] Session tests green: hitch cap, interpolation, reset snap through `Teleported`, `Restart` (before and after `Start`) replacing abilities, live number settings, ability toggles at play start only.
- [ ] Controls: keyboard (arrows/WASD/Space), gamepad (stick with a 0.5 dead zone, down only when it outweighs sideways, d-pad, South). Up never jumps. Press + release in a frame is a tap. The latch survives zero-tick frames.
- [ ] Feet align to the floor top within 1e-4 for the tested pivots and scales; the pixel check passes; facing flips.
- [ ] Every warning appears once, in plain words. Nothing is auto-removed. A clean scene warns about nothing.
- [ ] The dev scene rebuilds in batch mode with its self-check passing; its picture was inspected.
- [ ] The PlayMode tour and render tests are green and their pictures inspected on both editors.
- [ ] Zero allocations in `RunFrame` over 600 steady frames (or the documented fallback).
- [ ] The student-path test is green (or its documented split).
- [ ] The operator editor check passed.
- [ ] **The operator play-check passed (the milestone gate).** Tuned values are in `MovementSettings` / `MovementInternals` and entities.md, the gates were re-run and the dev scene regenerated.
- [ ] All suites green on 6000.0.85f1 and 6000.6.7f1 with no compiler warnings. Code review: no open HIGH/MED.

---

## Notes to GDD

Synced at completion (GDD v0.07 → v0.08). **(proposal)** marks Claude's fill-ins; the operator confirms them with the completion report.

**CLAUDE.md**
- **Architecture rule 5** gets an exception sentence: "Exception: at play start (or when a component is added in the editor) the runtime may gather placed Daedalus components or Tilemaps with one `FindObjectsByType` scan followed by a deterministic sort; never per frame, never as a lookup service."
- **Project Config:**
  - package dependencies += Input System 1.11.2
  - dev project `testables` += `com.unity.inputsystem`
  - the compat project pins Input System to the package's declared version
- **Build/run:** the dev-scene rebuild command. **Testing table:** inspect `TestResults/*-movement-*.png` too.

**decisions.md**
- A new ADR row: "Play-start scans are the one exception to rule 5". Rationale: zero wiring for students, deterministic order. Alternatives: a static registry, Inspector lists. Status Active.
- Flip the 2026-10-23 Draft rows for the Player/gravity/acceleration decisions and the runtime rules to Active once the play-check passed.

**architecture.md**
- § System Map, runtime row: `PlayerSession` (plain C# driver), `CollisionMapReader`, `RopeCollector`, `PlayerControls` (actions in code), one-shot warnings.
- A rule-5 note mirroring CLAUDE.md.
- § Data Flow 2–3:
  - the map is the Collision Tilemap's bounds grown to include the start and ropes plus 24 rows of headroom (operator, 2026-10-23)
  - the Player's placed position is the start
  - play-from-here calls `Player.Restart(PlayStart)`; its ability set replaces the toggles; `Jump` is always on
- § Time Model: the reset snap (`Teleported`); scaled `Time.deltaTime`.

**entities.md**
- § Player tunables: the tuned values. Sections: Movement (5) and Abilities (4 toggles, default off **(proposal)**, + 3). Serialized field names.
- Remove the open line "Player component on the object or a Player start marker": resolved, on the object. Remove "gravity multiplier or absolute": a multiplier.
- § Placeable elements: replace "Player start: exactly one per map" with "Player: its placed position is the start".
- Rope: length in tiles, default 4, range 1–50 **(proposal)**, hangs down from the object, Scene-view line.
- § Terrains / tiles: `DaedalusTile` with Kind (None/Solid/One-way/Climbable; value 4 reserved for hazard, declared in combat-and-enemies). Plain Unity tiles on the Collision Tilemap count as Solid.
- systems.md § Player (ropes): a rope's top snaps onto a one-way surface within 0.5 tile at play start, so freely placed ropes still let the player step off.
- § Authored data: `dataVersion` 1 on Player, Rope, DaedalusTile.

**systems.md**
- § World & Level Elements: plain tiles are solid; map bounds per architecture.md.
- § Tile System: per-tile kind via `DaedalusTile`.

**platforms.md § Input**
- Default bindings: arrows/WASD, gamepad stick (dead zone 0.5, down only when it outweighs sideways) and d-pad move; Space / gamepad South jump; Down + Jump drops through; Up never jumps. Input System 1.11.2 minimum.

**conventions.md**
- § Authored Data & Upgrades: every component/asset has a hidden `dataVersion` (starts at 1); explicit enum values that are never renumbered; serialized names fixed from first release.
- § Performance Idioms (extending Phase 1's `Step` rules): the frame loop rules; one-shot warnings; the `PlayerAllocationTests` guard.
- § Error Handling: warning texts live in one class, plain words, the object as context.
- § Testing & Playtesting: `InputTestFixture` by composition; `Time.captureFramerate = 60`; PlayMode levels built in code (`TestLevel`) so both editors run them; the dev scene is generated and verified by its self-check and picture.

**systems.md § Movement rules:** drop the **(proposal)** marker from each rule the play-check confirmed; rewrite the ones it changed.

**Glossary.md (Draft):** new: Player (component), Daedalus tile, Collision kind, Play start, Player session, Feet offset, Reset snap, Play-start scan. Extend Phase 1's existing entries instead of adding duplicates: **Rope** (+ the Rope component, its length and top snap), **Map bounds** (+ the grown bounds and 24-row map headroom).

**framework.md:** Input System versions per project; `InputTestFixture` setup; `ObjectFactory.AddComponent` and `Reset()`; `EnterPlayMode` in batch mode; any `FindObjectsByType` obsolescence; `Is.Not.AllocatingGCMemory()` in PlayMode.

**Operations (same commit):**
- Roadmap §0 rows for `movement-sim` and `movement-runtime`; Milestone 1 removed from §1.
- Changelog v0.08; GDD.md version bump.
- **Deferred.md:**
  - "Warnings only in the Console": trigger `editor-tools` inspectors, fix shape = Inspector help boxes
  - "A Rope with no sprite is invisible in Play": trigger the `editor-tools` missing-piece hints
  - "Map bounds never shrink after erasing (no `CompressBounds`)": trigger a student report of an invisible wall

**Settled by the operator (2026-10-23):** the map is the Collision Tilemap's bounds grown to include the start and ropes plus 24 rows of headroom; Hazard is not declared until combat-and-enemies; rope tops snap onto a one-way surface within 0.5 tile in the sim.

**Open questions for the operator:**
1. Ability toggles (double jump, wall jump, wall climb, rope climb) default **off**, so a fresh Player runs and jumps only, matching entities.md's starting abilities `["jump"]`; students switch on what their level starts with, and pickups (world-systems) unlock the rest. Claude recommends off.
   Reviewer recommends: off — matches entities.md's starting abilities, keeps a beginner's first Play to run and jump, and leaves world-systems' pickups something to unlock (round 1, F15).
2. Type names, registered as Draft: `DaedalusTile`, `TileCollisionKind`, `PlayStart`, `PlayerSession`, `PlayerControls`, `PlayerMovementSection`, `PlayerAbilitiesSection`, `CollisionMap`, `CollisionMapReader`, `RopeCollector`, `PlayerWarnings`. Serialized field names are fixed now; type names can change cheaply until the first release. Claude recommends keeping them.
   Reviewer recommends: keep them — they read as what they are, none collides with a Unity type in the namespaces used, and only the serialized field names are costly to change later (round 1, F16).

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. CLAUDE.md has no phase-status line; nothing to update (as in Phase 1).
3. Write `memory/logs/phases/phase2-log.md` (including the tuning table, the inspected screenshots, the fallbacks that fired, and the editor/play-check outcomes).
4. Update `MEMORY.md` current state.
5. Apply the Notes to GDD: CLAUDE.md rule 5 exception + Project Config; `architecture.md` rule-5 note; the rule-5 ADR in `memory/decisions.md`; `framework.md`; `lessons-learned.md` if a rule was learned.
6. **Deferred items.** File the Deferred.md entries listed in Notes to GDD (Source = Phase 2). Delete any existing entry whose trigger fired. See CLAUDE.md → Log Discipline.
7. Ask the operator, then commit all phase work as `v0.08: Phase 2 — movement runtime: Player, Rope, Collision Tilemap, controls, dev scene, tuned defaults`.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked, and at Steps 17 and 18, which are planned operator stops. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: phase2-movement-runtime
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:52:00+09:00
blocking: 2
recommendations: 2
---

# Review round 1: `Documents/Phases/phase2-movement-runtime.md`

## What I could run, what I verified, what I could not

**Could run:** a shell over the connected `daedalus` folder (read files, `grep`, `find`, short Python). Nothing outside it: no Unity, no test suite, no build, no Input System installed to probe.

**Verified against files:**
- The whole document (675 lines), the approved Phase 1 prompt (its API, Step 11 ropes, Step 13 allocation fallback, "For Phase 2" notes), `decisions.md` 2026-10-23 rows, `Roadmap.md`, `entities.md`.
- Code on disk: `FixedStepAccumulator.cs` (Advance returns ≤ 5, epsilon 1e-9, `InterpolationFraction`), so `AdvanceClock(1/60.0) → 1` and `AdvanceClock(0.5) → 5` hold; `package.json` (two deps today); the runtime and both test asmdefs; `Runtime/AssemblyInfo.cs` (IVT to the two test assemblies only); `AssemblyDirectionTests.cs`, `PackageManifestTests.cs`; `Tools/unity_tests.py` `ensure_compat_project` (testables overwritten each run, line 81).
- Compat project: `Tools/compat/6000.0/Packages/manifest.json` and `packages-lock.json` show `com.unity.inputsystem` 1.20.0 (the doc's "currently resolves 1.20.0" is right); `activeInputHandler` is 1 in the dev project and 2 in compat (as the doc says).
- Dev `packages-lock.json`: `com.unity.modules.tilemap` depends on `com.unity.modules.physics2d`, so `Rigidbody2D`/`Collider2D` in `Player.Awake` compile without a new package dependency.
- `Assets/Dev/Editor/` has no asmdef (Assembly-CSharp-Editor), as the doc says.
- Arithmetic: `ToSegment` example (3.5, 10), length 4, origin (−2, 1) → X 5.5, Bottom 5, Top 9 ✓. Headroom 6 × (1 + 3) = 24 ✓ (but see F3). Stick cases (0.6, −0.7) down / (0.8, −0.7) no down ✓ under both raw and dead-zone-processed values (the processor scales radially, so the |y| > |x| test is unchanged).

**Not verified (taken on trust or left to the Author):** that 1.11.2 resolves and `InputTestFixture` runs on 6000.0.85f1; that `ObjectFactory.AddComponent` calls `Reset()`; `EnterPlayMode` in batch mode; `FindObjectsByType` obsolescence on 6000.6; the Input System's same-frame press/release behaviour of `WasPressedThisFrame` (the doc tests it in Step 1, which is the right place). Unity's enum-popup display names and `LogAssert` semantics (F5, F6) are from my knowledge of Unity, not run here; please confirm.

## Findings

### F1. Phase 1's promised "rope overlapping solid" warning is dropped
- **Type:** blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 11 (warning list), Step 6 (rope snap); Phase 1 Step 11 "Top".
- **Defect:** The approved Phase 1 prompt rests a rule on this phase: "a rope ending inside a Solid or Climbable cell blocks the head before the feet arrive, so it just stops at the head bump (**the runtime warns about ropes overlapping solid tiles, Phase 2**)". Phase 2's warning list has no such text, no test and no Deferred entry; `grep -i overlap` finds only the dev-scene "nothing overlaps". The Step 6 snap only rescues tops near a **one-way**; a rope whose top sits in a Solid cell (the common "hang it from the ceiling" placement, since students place without grid snap) still stops silently 1.5 tiles under the ceiling. That is the producer-without-consumer pattern of Architecture rule 7, across a phase seam.
- **Fix:** add a `PlayerWarnings` text (e.g. "This rope's top is inside a wall or ceiling tile, so the player stops below it. Move the rope down so its top is in an empty cell or on a one-way platform."), issued once per rope at play start with the Rope as context (check the cells under the rope's top after the snap), plus a `PlayerWarningTests` case; or record explicitly that it is deferred, with a Deferred.md trigger, and correct the expectation Phase 1 set.

### F2. The dev generator calls `internal` members it cannot see
- **Type:** blocking · **Category:** accuracy · **Severity:** medium (compile error)
- **Location:** Step 2 (`DaedalusTile` "an `internal` setter for tests and the dev generator"), Step 12 (scene with "a Player with every ability on", Rope lengths).
- **Defect:** `MovementTestSceneBuilder.cs` is in Assembly-CSharp-Editor (Step 12; confirmed: no asmdef in `Assets/Dev/Editor/`). `Runtime/AssemblyInfo.cs` grants `InternalsVisibleTo` only to `Orcapafea.Daedalus.Tests.EditMode` and `.PlayMode`. So the generator cannot use the internal kind setter, and has no way to turn the Player's abilities on or set rope lengths except through serialized fields, which the doc names only for `collisionTilemap`. As written, Step 12 does not compile.
- **Fix:** the generator writes everything through `SerializedObject` (`kind`, `abilities.doubleJump|wallJump|wallClimb|ropeClimb`, `length`), which also proves those field names, and the internal setter's purpose becomes "tests only". Do not add an IVT for Assembly-CSharp-Editor.

### F3. "24 rows is the highest reachable" leaves out the hitbox
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5, `MapHeadroomRows`.
- **Defect:** 6 × (1 + 3) = 24 is how high the **feet** can rise above the highest surface. The head is 1.5 above the feet, and rows `y ≥ Height` are Solid, so with every setting at max the head reaches the invisible map top 1.5 tiles before the apex (feet 22.5 above the surface). The value is the operator's (decisions.md row 34) and I am not asking to change it; the justification sentence is wrong, and the pinned test would encode it.
- **Fix:** reword to "the feet's highest reach with every setting at max; the head meets the map top 1.5 tiles before that apex, which only matters at extreme settings". If the operator wants the stated property, the number is 26 (`ceil(24 + 1.5)`); that is their call.

### F4. A play-from-here start that arrives after `Start` is outside the grown map
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 9 `Restart` ("either order works"), Step 5 bounds growth.
- **Defect:** The bounds grow to include `pendingStart ?? DefaultStart`, computed once in `Start`. If the later editor tool's `EnteredPlayMode` call lands after `Start`, the map was grown around the placed Player, not the play-from-here point. A point beyond the painted extent or above the 24 rows is then outside the map, and `StartPlacement.Find` moves it up to 8 cells or fails. "Either order works" holds for the state, not the map. The same applies to any later `Restart` (checkpoints are inside the map, so they are fine).
- **Fix:** either state that the map is fixed at play start and play-from-here must call `Restart` before `Start` (and say which Unity callback guarantees that), or narrow "either order works" to "inside the map". No code needed now; the editor-tools milestone inherits the note.

### F5. The Kind dropdown will say "One Way", not "One-way"
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 17 Observe ("None, Solid, One-way, Climbable"); Step 2 tooltip.
- **Defect:** Unity's enum popup shows `ObjectNames.NicifyVariableName` of each member, so `OneWay` displays as "One Way". The operator check would fail on wording, or pass on a mismatch. (From Unity knowledge; not run here.)
- **Fix:** add `[InspectorName("One-way")]` on `OneWay` (runtime attribute, saved value unchanged) and pin it in `TileAssetTests`, or change the observation to "One Way".

### F6. `LogAssert.Expect` cannot prove "exactly once", and two warnings have no test path
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 11 tests; Completion "Every warning appears once".
- **Defect:** `LogAssert.Expect(LogType.Warning, text)` consumes one matching message; a second identical warning is not a failure (unexpected *warnings* do not fail a test the way unexpected errors do). So "exactly once over 120 frames" is not asserted by the tool named. Separately, the "Old input setting" warning (`#if !ENABLE_INPUT_SYSTEM`) cannot be produced in either test project (handlers 1 and 2), and no operator step covers it.
- **Fix:** count with the same test-local `Application.logMessageReceived` listener the clean case already uses (count == 1 per text). Mark the input-setting warning as code-review-only in the phase log, or add it to Step 17 if the operator is willing to flip the setting once.

### F7. An empty rope list must not put the world origin into the map bounds
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 6 `RopeCollector.WorldBounds(Rope[])`, Step 9 "include = start hitbox ∪ rope bounds".
- **Defect:** The doc does not say what `WorldBounds` returns for no ropes. The natural `Rect` default is a zero rect at (0, 0); unioned with the start, it grows the map to the world origin. A level painted at x = 200 then gets its left map wall at x = 0, so the edge behaviour (invisible wall vs. fall-out) changes with whether the scene has any rope. The reader test "a rope to the right of all tiles is included" would not catch it.
- **Fix:** `WorldBounds` returns a bool / nullable for "no ropes" and the union skips it; add a reader or `RopeComponentTests` case: no ropes, tiles far from the origin → `CellMin` is the tiles' min, not (0, 0).

### F8. A Player saved as a prefab loses its Collision Tilemap, and only `Reset` fills it
- **Type:** non-blocking · **Category:** design · **Severity:** medium
- **Location:** Step 3 `Reset()` auto-fill; Step 9 `Start` item 5; CLAUDE.md Architecture rule 4 ("often saved as the student's own prefabs").
- **Defect:** A prefab asset cannot reference a scene Tilemap, so a student's Player prefab dropped into a scene has an empty field; `Reset` does not run on instantiation. The same happens when a student adds the Player before creating or renaming the Tilemap (Step 17 does it in the safe order). Result: the player falls forever with a warning, on what the student experiences as a working object. The new rule-5 exception text already allows a play-start Tilemap scan.
- **Fix:** in `Start`, when the field is empty, apply the same pick as `Reset` (named "Collision", else the only Tilemap) without writing the field, and warn only when that also finds nothing. One more `FindObjectsByType` site (update the Step 6 grep and the Step 20 review list) and a `PlayerWarningTests` case: field empty, one "Collision" Tilemap → lands, no warning.

### F9. The dev scene does not fit a 16:10 laptop at camera size 11
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 12 "40 × 22 tiles, camera size 11"; Step 18 play-check.
- **Defect:** Size 11 shows 22 rows; 40 columns need an aspect ≥ 40/22 = 1.82. At 16:9 the view is 39.1 tiles wide (≈ 0.9 cut); at 16:10 (common on school laptops) 35.2 (≈ 4.8 cut); a docked Game view in Free Aspect is usually narrower still. The batch picture renders at whatever size the builder picks, so it can look complete while the operator's view is not.
- **Fix:** lay the scene out in ≤ 35 columns, or set the camera size from the aspect so the width fits (a dev-only script is fine), and render the picture at 16:10.

### F10. Play-check gates are not single-ability with every ability on
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 18 Do ("clear the 5-tile gap and fail the 6-tile gap"); Step 12 "every ability on".
- **Defect:** With double jump on, the 6-tile gap and the 4-tile ledge are cleared with the air jump, so "fail the 6-tile gap" is only meaningful with a single jump. The ledge line says "with a single jump"; the gap line does not.
- **Fix:** "with a single jump (no air jump)" on the gap line, or run the gap and ledge items with Double Jump unticked (the last Do line already does per-ability replays).

### F11. The student path test needs a sprite to be warning-free
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 15 items 3 and 6.
- **Defect:** Item 3 creates "a GameObject with a `SpriteRenderer`" but no sprite; item 6 asserts "no warnings" and "the sprite bottom on the floor top". Without a sprite the "No sprite" warning fires and the feet offset is 0.
- **Fix:** item 3: a `SpriteRenderer` with a sprite (the same code-made sprite as the tiles, or `TestImages.SolidSprite`).

### F12. "File the four Deferred.md entries above" lists three
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Post-Phase Cleanup item 6; Notes to GDD → Deferred.md.
- **Defect:** The Deferred list has three entries (warnings only in the Console; rope without a sprite; bounds never shrink). F1 or F13 may add a fourth; as written the count is wrong.
- **Fix:** say "the Deferred.md entries above" or fix the count after folding.

### F13. Mode-only settings stay visible; the doc does not say so
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 3 Abilities section; Step 17 Observe; CLAUDE.md Architecture rule 8 ("a setting that applies to one mode only … shows only in that mode").
- **Defect:** `airJumps`, `wallSlideSpeed` and `climbSpeed` apply only with their toggles on, and are always shown, with "Used only when …" tooltips. Roadmap §1 `editor-tools` already plans "mode-specific settings hidden", so this is a deliberate interim state, but the doc presents the layout as final and the operator check encodes it.
- **Fix:** one sentence in Step 3: "Rule 8's hiding arrives with the editor-tools custom inspectors (Roadmap §1); until then the tooltips say when a setting applies." No code.

### F14. Rope length changes in Play do nothing, and the tooltip doesn't say so
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 4 Rope tooltip; Step 6 (ropes read once at play start).
- **Defect:** The Player's numbers apply live in Play, the Rope's length does not, but its gizmo line follows the slider live, so a student sees the rope get longer while the player still stops at the old bottom.
- **Fix:** end the Rope tooltip with the same sentence the ability toggles use: "Changes take effect the next time you press Play."

### F15. Open question 1 (ability toggles default off)
- **Type:** recommendation
- **Location:** Open questions, 1.
- **Reviewer recommends:** off. It matches entities.md's starting abilities `["jump"]`, keeps a fresh Player's first Play to run and jump (no surprise wall-cling on the first wall a beginner paints), and leaves the pickups in world-systems something to unlock.

### F16. Open question 2 (type names)
- **Type:** recommendation
- **Location:** Open questions, 2.
- **Reviewer recommends:** keep them. They read as what they are, none collides with a Unity type in the namespaces used, and only the serialized field names (already fixed) are costly to change later.

## Reviewer's view

I would build this after F1 and F2. F2 is a compile error in Step 12 with a one-line fix; F1 is a promise the approved Phase 1 made on this phase's behalf and should either be kept or explicitly deferred. Of the rest, F8 (prefab Players lose their Tilemap) is the one with the most real student impact; F3–F7 and F9–F14 are wording, test-precision and layout fixes.

---

## Review round 2

---
slug: phase2-movement-runtime
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:58:00+09:00
blocking: 1
recommendations: 0
---

# Review round 2 (delta): `Documents/Phases/phase2-movement-runtime.md`

## What I could run, what I verified, what I could not

**Could run:** the same shell over the connected `daedalus` folder as in round 1. No Unity, no tests.

**Verified:** `log.md` round 1 dispositions (all 14 findings accepted, F15/F16 recorded), and every folded passage in the doc: Step 2 (`[InspectorName("One-way")]`, tests-only setter), Step 3 (rule-8 interim sentence), Step 4 (rope tooltip), Step 5 (headroom wording), Step 6 (`TryGetWorldBounds`, the grep naming the shared pick), Step 9 items 2–5 and `Restart`, Step 11 (rope warning text, cell rule, listener counts, new test cases), Step 12 (35 × 22, 560 × 350, every setting through `SerializedObject`), Step 15 item 3, Step 18 gap line, Step 20 wire list, Post-Phase item 6, the two recorded recommendations.

**Rechecked the F1 cell rule against Phase 1:** top cell = column `floor(X)`, row `ceil(Top) − 1`. A rope whose top is exactly on a Solid block's top face (Top = 9.0, row 8 Solid) → row 8 → warns ✓. A rope touching a ceiling's bottom face (Top = 10.0, row 10 Solid) → row 9, empty → no warning, and Phase 1's head bump there is the normal "head touches the ceiling" case ✓. A one-way at the top (after the snap, `|round(Top) − Top| ≤ 0.0625`) → OneWay → no warning, consistent with Phase 1's stand-on-top rule ✓. F9: 560 × 350 is 16:10, and 35.2 columns at size 11 ✓.

**Not verified:** the same Unity-behaviour items as round 1.

## Findings

### F1–F16: closed
F1–F14 are folded as described in `log.md`; F15 and F16 are recorded beside the open questions. No corrections to my round-1 claims.

### F17. The student-path test (EditMode) uses a PlayMode-assembly helper
- **Type:** blocking · **Category:** accuracy · **Severity:** medium (compile error)
- **Location:** Step 15 item 3 (the F11 fold): "a `SpriteRenderer` **with a sprite** (`TestImages.SolidSprite`)"; Step 13 / manifest line 77 (`P/Tests/PlayMode/TestImages.cs`).
- **Defect:** `StudentPathTests.cs` is in `Tests/EditMode/` (assembly `Orcapafea.Daedalus.Tests.EditMode`); `TestImages` is in `Tests/PlayMode/` (`Orcapafea.Daedalus.Tests.PlayMode`). The EditMode asmdef references Sim, runtime, Editor and the two TestRunner assemblies, not the PlayMode test assembly, and a test assembly referencing another test assembly would be the wrong fix. As written, the EditMode suite doesn't compile. This is a seam defect: my F11 asked for a sprite, and the fold reached for the nearest helper across the assembly line.
- **Fix:** make the sprite in the test the way item 2 makes the tile sprite ("sprite from code": `Texture2D` + `Sprite.Create`, 12 × 24 px, centre pivot, 16 PPU), or name the EditMode-side helper that does it.

### F18. The play-start Tilemap pick changes what "empty field" means in three places
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 9 `Start` items 2 and 5; Step 11 test "empty field → no exception, the player falls and resets"; Step 17 Do item 7 and Observe "each problem gives one plain-words Console warning".
- **Defect:** The F8 fold is right in substance, but:
  1. In `Start`, item 2 reads `collisionTilemap` and item 5 applies the pick afterwards; read literally, the pick runs after the map is already built from a null field.
  2. The Step 11 test "empty field → … falls and resets" now lands instead whenever its `TestLevel` has a Collision Tilemap; it only holds in a scene with no Tilemap (or two unnamed ones).
  3. Step 17 item 7 (clear the field, press Play) now lands silently in the operator's scene, so the Observe line "each problem gives one plain-words Console warning" fails on a behaviour that is now correct.
- **Fix:** (1) resolve the Tilemap (field, else the pick) as item 1 of `Start`, before the read; (2) the "falls and resets" test uses a scene with no Tilemap; (3) Step 17 item 7 becomes "clear the field and press Play: the player still lands, with no warning", and a separate item produces the warning (e.g. rename the Tilemap away from "Collision" and add a second Tilemap, then press Play).

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F17}. Both round-1 blockers are closed; F17 is new and comes from a fold. Converging.

## Reviewer's view

I would build it once F17 is fixed, a one-line change to Step 15. F18 is wording and test setup that follows from the F8 fold. Nothing else stands between this and approval.

---

## Review round 3

---
slug: phase2-movement-runtime
round: 3
verdict: approved
reviewer: claude-cowork
updated: 2026-10-23T22:00:00+09:00
blocking: 0
recommendations: 0
---

# Review round 3 (delta): `Documents/Phases/phase2-movement-runtime.md`

## What I could run, what I verified, what I could not

**Could run:** the same shell over the connected `daedalus` folder. No Unity, no tests.

**Verified:** `log.md` round 2 dispositions and the folded text:
- F17: Step 15 item 3 makes the sprite in the test (`Texture2D` + `Sprite.Create`, 12 × 24 px, centre pivot, 16 PPU) and says why `TestImages` isn't used. Closed.
- F18 (1): `Start` now reads resolve Tilemap → ropes → read (on the resolved Tilemap) → session → warnings, with the pick described under the list and referenced from item 1. (2): Step 11's "falls and resets" case uses a scene with no Tilemap, and separately two Tilemaps neither named "Collision", and asserts the warning once. (3): Step 17 item 7 expects a silent landing; item 7b (rename away + a second Tilemap, field empty) is a real producer of the warning: the pick finds no "Collision" and more than one Tilemap. Item 8 (Rigidbody) still lands via the pick with the field empty. Closed.

**Not verified:** the Unity-behaviour items listed in round 1 (Input System 1.11.2 on 6000.0, `InputTestFixture`, `ObjectFactory.AddComponent` → `Reset`, `EnterPlayMode` in batch, `FindObjectsByType` obsolescence, enum labels, `LogAssert`). Each has a test or a recorded fallback in the doc; they are proven at build time, not here.

## Findings

### F17, F18: closed

### F19. A sprite that isn't 0.75 × 1.5 tiles won't match the collision, and nothing shows the hitbox
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Overview ("add a Player to any object with a sprite"); Step 10 (feet offset only); Step 17 item 4 (the 1 × 1 Square).
- **Defect:** Filed by the late-pushback test: it is not new to this round, but I hadn't written it down. The hitbox is fixed at 0.75 × 1.5 (Phase 1). Step 10 aligns only the feet. With the Square from Step 17, the player bumps ceilings 0.5 tile above its visible top, can't enter a 1-tall gap the square looks like it fits, and its sides show 0.125 inside walls. A student sees "invisible ceilings" and can't find the cause in the Inspector. The dev scene's 12 × 24 px sprite matches exactly, so neither the tests nor the play-check would show it.
- **Fix:** an `OnDrawGizmosSelected` hitbox outline on the Player (a MonoBehaviour message, like the Rope line; no editor code), plus a Step 17 Observe line; or a Deferred.md entry with the `editor-tools` / default-art-pack trigger. Either one is enough.

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F17} → round 3 {}. Converged.

## Approval and next custody

No blocking finding remains, so I approve. The late-pushback test turned up one item, F19, filed above as non-blocking. The Author folds F19 on close.

Approval is a verdict on the document only. Nothing has been built or run: the phase's 20 steps, both operator stops (Step 17 editor check, Step 18 play-check and tuning, the milestone gate) and the operator's two open questions are all still ahead.

## Reviewer's view

I would build this as written. The two round-1 blockers (F1, the rope-in-wall warning Phase 1 relied on; F2, the generator's compile error) and the round-2 one (F17) are closed, and the student-facing fixes, F8 (prefab Players find their Tilemap) above all, are in. F19 is a small, cheap visibility fix.
