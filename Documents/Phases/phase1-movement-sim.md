# Phase 1: Movement Sim

## Overview

**Where things stand:** The project has its skeleton and a game clock, but there is no player yet. Nothing runs, jumps or bumps into anything.

**What this phase changes:**
- The rules for running, jumping, double-jumping, sliding down and jumping off walls, climbing marked walls and ropes, and dropping through thin platforms are written as pure game logic.
- That logic reads a simple grid of map squares and a list of ropes, so the next phase can feed it a real level.
- Every rule gets automatic tests on small hand-drawn maps. They include checks that a ledge meant to need double jump really does.
- A starting value is chosen and written down for every movement number, ready to be tuned by hand.

**How you'll know it worked:** All tests pass on both supported Unity versions and the code review is clean. There is still nothing to play. You first feel the movement, and tune it, in the next phase.

**Not in this phase:** controls, the test level, the Player you add in Unity, and the hand play-check (all next phase).

---

## Source
- **GDD sections:** `systems.md` § Player, § World & Level Elements (tile kinds, Collision Tilemap); `entities.md` § Player tunables, § Abilities; `architecture.md` § Time Model, § System Map; `conventions.md` § Testing & Playtesting.
- **Decisions:** the operator's movement decisions of 2026-10-23 and the accepted design-panel rules. **They are not in the GDD yet.** This prompt is their canonical home until completion, when `## Notes to GDD` is synced back.
- **GDD version:** v0.06
- **Depends on:** the scaffold (Milestone 0, v0.06). It provides `FixedStepAccumulator` (60 Hz, `TicksPerSecond = 60`), the sim assembly with `noEngineReferences`, the guard tests (`SimPurityTests`, `AssemblyDirectionTests`) and `Tools/unity_tests.py`.
- **Feeds:** Phase 2 `movement-runtime` (Unity glue, dev scene, input, PlayMode tests, operator play-check and tuning). Phase 1 provides, and Phase 2 consumes, everything listed below. Phase 1 plans none of Phase 2's work.
  - State creation from a start position and an ability set (`PlayerStepper.CreateState`)
  - The nearest free start (`StartPlacement.Find`)
  - A tile grid the runtime fills (`TileGrid.Set`)
  - A rope list (`RopeSegment[]`)
  - Live-replaceable settings (`PlayerStepper.ApplySettings`)
  - `MovementSettings` defaults and `Min*`/`Max*` constants for Inspector `[Range]`s

**Operator note: feel tuning happens at Phase 2's play-check.** Phase 1 is complete when the scenario tests are green on both editors and the code review passes. Nobody plays anything in Phase 1. Every number below is a starting value. The test breakpoints are stated so that a later retune shows exactly which gate moved.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `Packages/com.orcapafea.daedalus/Tests/EditMode/SimPurityTests.cs` | Guard: no static mutable state in the sim (reflection); no `System.Linq` in sim source |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/TileKind.cs` | `enum TileKind : byte { None = 0, Solid = 1, OneWay = 2, Climbable = 3 }` (4 reserved for hazard) |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/TileGrid.cs` | 0-based cell grid the runtime fills; map-bounds rules |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/RopeSegment.cs` | Readonly struct: one vertical rope (`X`, `Bottom`, `Top`) |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/TileCollision.cs` | Static, stateless: axis sweeps (X then Y), one-way rule, ceiling-corner correction, contact probes |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/Abilities.cs` | `[Flags] enum Abilities` |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/MovementSettings.cs` | Student-facing settings: defaults + `Min*`/`Max*` constants (the single source of truth), `Default`, `Clamped()` |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/MovementInternals.cs` | Hidden internals as constants, including tick counts |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/DerivedMovement.cs` | Readonly struct: per-settings derived numbers (gravities, jump/cut velocity, accelerations) |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/StartPlacement.cs` | Readonly struct + static `Find`: nearest free start |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/PlayerInput.cs` | Readonly struct: `MoveX`, `MoveY` (−1/0/1), `JumpPressed`, `JumpHeld` |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/PlayerContact.cs` | `enum PlayerContact { Air, Ground, OneWayTop, WallSlide, WallCling, Rope }` |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/PlayerState.cs` | Struct: position, velocity, contact, counters, abilities, start |
| NEW | `Packages/com.orcapafea.daedalus/Runtime/Sim/PlayerStepper.cs` | `CreateState`, `ApplySettings`, `Step(in PlayerState, in PlayerInput) → PlayerState` |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/AsciiLevel.cs` | Test helper: ASCII map → `TileGrid` + ropes + start |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/PlayerScenario.cs` | Test helper: stepper + state, scripted ticks, recorders |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/TileGridTests.cs` | Grid + bounds |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/TileCollisionTests.cs` | Sweeps, corners, one-way, ceiling correction, probes |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/MovementSettingsTests.cs` | Defaults pinned, derivation, clamps, worst-case per tick |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/StartPlacementTests.cs` | Nearest free start |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/RunAndFallTests.cs` | Run, gravity, landing, walk-off, ceilings, map bounds, reset |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/JumpTests.cs` | Jump, variable height, minimum hop, coyote, buffer, air jumps, `jump` gate |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/OneWayTests.cs` | One-way platforms and drop-through |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/WallJumpTests.cs` | Wall slide, wall jump, lock, wall coyote, no single-wall scaling, priorities |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/WallClimbTests.cs` | Cling, climb, ledge pop, topped by solid, climb down |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/RopeTests.cs` | Rope grab, climb, top/bottom, jump-off, lockout, one-way, wall, priorities |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/GateIntegrityTests.cs` | Default-value gates: ledges, gaps, corridor, double-jump 2× |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/AbilityMatrixTests.cs` | One map × several ability sets |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/Movement/StepAllocationTests.cs` | `Step` allocates nothing in steady play |

Every new file and folder is committed with its `.meta` (Unity generates them on the first test run). Post-phase doc edits are listed in `## Notes to GDD`.

---

## Conventions for every step

- Namespace `Orcapafea.Daedalus.Sim` (tests: `Orcapafea.Daedalus.Tests`). One primary type per file, filename = type.
- The sim stays pure:
  - no `UnityEngine`, `UnityEditor`, clock or random source
  - **no identifier starting with `Random`** (the purity scan's `\bRandom\w*` would flag our own names)
  - no static mutable fields
- In `Step` and anything it calls: no allocations, LINQ, closures, boxing or `foreach` over interfaces. Use `for` loops over arrays.
- Units: 1 unit = 1 tile; y points up; speeds in tiles/s; times are authored in seconds and converted to ticks once (`dt = 1f / FixedStepAccumulator.TicksPerSecond`).
- **Player position = feet centre** (`X` = hitbox centre, `Y` = hitbox bottom). Hitbox is 0.75 wide × 1.5 tall. **Grip point** (ropes and climbable walls) = hitbox centre, at height `Y + 0.75`.
- **Integration (both axes):** per tick, compute the new velocity, then move by `(vOld + vNew) / 2 · dt` (trapezoid). Impulses come first: a jump of any kind, the variable-height cut, a wall push, a drop, and zeroing on landing or a ceiling all *set* the velocity before integration. `vOld` is that post-impulse value, and gravity or acceleration then produce `vNew`. With that order, jump height is exact under constant gravity. With the stored pre-tick velocity as `vOld`, a jump from standing would peak at 3.333, not 3.5.
- **Timer convention (every window):** a counter set to `N` on tick `T` is active on each of ticks `T+1 … T+N` (it is checked during the tick and decremented at the end of it, at tick order item 9). So coyote, the buffer and wall coyote at 6 accept the action on 6 ticks, and the lock and lockout at 12 block 12 ticks. Ticks are named once:
  - **walk-off tick `W`:** the tick whose move leaves the player unsupported;
  - **landing tick `L`:** the tick whose move ends standing. `L+1` is the first tick that *starts* standing;
  - **jump tick `J`:** the tick on which any jump fires.
- Tests: plain NUnit, assert tick by tick through the real entry point (`PlayerStepper.Step`). Every test that relies on a precondition asserts that the precondition was met (e.g. "the jump actually fired"). Breakpoint tests assert both sides of the breakpoint.
- During a step, run `python Tools/unity_tests.py --editor 6000.6 --platform editmode`. Both editors run at Step 13.

---

## Steps

### Step 1: Guard the sim against static mutable state and LINQ
- **MODIFY `Tests/EditMode/SimPurityTests.cs`**:
    - Add a reflection test over `typeof(FixedStepAccumulator).Assembly`. Every static field (public and non-public, declared-only, every type including nested ones) must be `IsLiteral` (`const`), or `IsInitOnly` with a value type or `string` field type. A `static readonly` array or other reference type is mutable state and fails. Compiler-generated backing fields of get-only static properties are init-only and pass. The failure message lists `Type.Field`.
    - Types marked `[CompilerGenerated]` are skipped on purpose. The compiler's lambda caches (`<>c.<>9__N_M`) are static and not readonly, but they hold no game state. Lambdas stay banned in `Step` and below by the conventions and code review, not by this guard; play-start code (e.g. a sort comparison) may use them.
    - Add `new Regex(@"\bSystem\s*\.\s*Linq\b")` to `Forbidden`.
    - Extend `ScannerCatchesForbiddenUseButIgnoresComments` with `using System.Linq;`.
- **Tests:** the two guards above. Prove the reflection guard can fail: plant a `static int` in a scratch sim file, watch it go red, then remove it. Record in the phase log that it was seen failing.
- **Verify:** `[automated]` EditMode green; `[manual]` planted failure observed and reverted.

### Step 2: Tile kinds, tile grid, rope segments, ASCII level helper
- **NEW `TileKind.cs`**: values `None = 0, Solid = 1, OneWay = 2, Climbable = 3`, explicit, never renumbered. A comment reserves `4` for hazard (combat-and-enemies); hazard is not in the sim yet. Climbable cells collide exactly like Solid; they differ only with `wallClimb`.
- **NEW `TileGrid.cs`**: `sealed class`.
    - `TileGrid(int width, int height)` (both ≥ 1, else throw), `Width`, `Height`.
    - `Set(int x, int y, TileKind kind)`: throws when out of range (fail loud; the runtime fills cells by 0-based coordinates and keeps the Tilemap's origin as a render offset).
    - `Get(int x, int y)`. **Map bounds:**
      - columns `x < 0` or `x ≥ Width` → `Solid` (any row)
      - rows `y ≥ Height` → `Solid`
      - rows `y < 0` inside the columns → `None` (the bottom is open)
    - Backing `TileKind[]`, row-major, allocated once in the constructor.
- **NEW `RopeSegment.cs`**: `readonly struct RopeSegment(float x, float bottom, float top)`. Throws if `top ≤ bottom`. `X` is the rope's centre line.
- **NEW `Tests/EditMode/Movement/AsciiLevel.cs`**: `AsciiLevel.Parse(params string[] rows)`. The first string is the top row; all rows have equal width (throw otherwise).
    - Characters: `.` none, `#` solid, `-` one-way, `C` climbable, `R` empty + rope, `+` one-way + rope, `P` empty + player start (feet at that cell's bottom centre; exactly one `P`, else throw).
    - Each vertical run of `R`/`+` in a column becomes one `RopeSegment`: `X` = column + 0.5, `Bottom` = lowest cell's bottom, `Top` = highest cell's top.
    - Ropes are ordered by (column, bottom), so "lowest index wins" is deterministic.
    - Exposes `Grid`, `Ropes`, `StartX`, `StartY`.
- **Tests (`TileGridTests.cs`):** set/get round trip; out-of-range `Set` throws; side/top bounds read Solid, below-map reads None; `AsciiLevel` builds the expected cells, rope segments (a rope crossing a `+` is one segment) and start; malformed maps throw.
- **Verify:** `[automated]` EditMode green.

### Step 3: Tile collision (axis sweeps, one-way, corners, ceilings, probes)
- **NEW `TileCollision.cs`**: `static class`, no state. Boxes are given as feet centre + half width + height. A box covers columns `floor(left) … ceil(right) − 1` and rows `floor(bottom) … ceil(top) − 1` (half-open, so a box resting exactly on a face doesn't overlap the next cell).
    - **Blocking** for both axes: Solid, Climbable, out-of-bounds sides and top (via `TileGrid.Get`). **OneWay never blocks horizontally or upward**, so its sides are never walls and it is never a ceiling.
    - **`SweepX`**: moves the box horizontally by `dx`. It checks every column face between the start edge and the end edge over the rows the box covers at its current height. This makes it correct at any speed; the "under one tile per tick" clamp in Step 4 is defence in depth, not a precondition. On a hit it stops with the leading edge exactly on the face and reports the hit side.
    - **`SweepY`**: moves vertically by `dy`, checking every row face on the path over the box's columns.
      - **Down:** stops on Solid/Climbable tops. Also stops on a OneWay top **only if the box bottom at the start of the move was at or above that top** (≥ top − `ContactEpsilon`). This is the rule "feet were at/above the top on the previous tick". On landing the bottom snaps exactly onto the top and the result reports `landed` plus whether the support was OneWay-only.
      - **Up:** stops with the head on a blocking cell's bottom and reports `ceiling` (the caller zeroes upward velocity).
      - A flag lets rope climbing ignore OneWay cells in both directions.
    - **Ceiling-corner correction (up moves only):** applies when the head would hit blocking cells that all lie on one side of the box and the horizontal overlap with them is ≤ `CeilingCornerCorrection` (0.25 tile). The box is shifted sideways by exactly that overlap, away from the cells, if the shifted box is free over the whole vertical path; the up move then continues. Otherwise the head stops. The shift never exceeds the cap.
    - **Diagonal-corner rule:** the caller always sweeps X first (at the old height), then Y. A box moving diagonally onto an outside corner therefore lands on top when falling, and bumps or gets the ceiling correction when rising. It never snags on the corner's side.
    - **Probes:**
      - `Support(grid, x, y, halfWidth)` → none / solid / one-way-only, for a box whose bottom lies on an integer row within `ContactEpsilon`; any Solid or Climbable cell under the box means solid (straddling wins).
      - `WallGap(grid, x, y, halfWidth, height, side, reach)` → distance to the nearest blocking column face on that side within `reach`, else "none".
      - `IsClimbableAt(grid, x, y)` → the cell kind at a point.
      - `BoxIsFree(grid, x, y, halfWidth, height)` → overlaps no blocking cell and lies inside the side and top bounds. It has no bottom bound (below the map is open), which is right for ceiling correction, the rope snap and the ledge pop; `StartPlacement.Find` adds its own `y ≥ 0` check (Step 5).
- **Tests (`TileCollisionTests.cs`):**
  - horizontal stop exactly on a face, from both sides, including a box spanning two rows
  - a 0.9-tile move into a 1-wide wall still stops (sweep, not teleport)
  - map side and top act as walls and ceiling
  - landing snaps onto a top; down through a OneWay from below or inside doesn't land, from at/above does
  - OneWay never blocks horizontally or upward
  - falling diagonally onto an outside corner lands on top; rising diagonally into an underside corner bumps
  - ceiling overlap 0.2 → shifted by 0.2 and keeps rising; 0.3 → stops (`ceiling`); with a wall beside the corrected position → no shift
  - the shift never exceeds 0.25 across a sweep of overlaps from 0.01 to 0.5
  - `Support` returns one-way-only only when no solid cell is under the box
- **Verify:** `[automated]` EditMode green.

### Step 4: Settings, abilities, internals, derived numbers
- **NEW `Abilities.cs`**: `[Flags] enum Abilities { None = 0, Jump = 1, DoubleJump = 2, WallJump = 4, WallClimb = 8, RopeClimb = 16 }`. Run is always on and has no flag. `Jump` exists because the sim must handle `jump` off (drop-through still works; the tests require it). The Phase 2 Player always sets it.
- **NEW `MovementSettings.cs`**: `struct` with public fields, constants on the struct, and `public static MovementSettings Default => new MovementSettings { … }` built from the `Default*` constants. It is a property, so there is no static field. **These constants are the single source of truth;** Phase 2 reads them for initial values and `[Range]`s.

  | Field | `Default*` | `Min*` | `Max*` | Note |
  |---|---|---|---|---|
  | `RunSpeed` | 6 | 1 | 12 | tiles/s |
  | `TimeToFullSpeed` | 0.1 | 0 | 0.5 | s from rest to run speed on ground; replaces "acceleration" |
  | `JumpHeight` | 3.5 | 1 | 6 | tiles |
  | `Gravity` | 1 | 0.25 | 2 | multiplier over base gravity (floor > 0) |
  | `MaxFallSpeed` | 12 | 2 | 24 | tiles/s |
  | `AirJumps` | 1 | 1 | 3 | int; used only with double jump |
  | `WallSlideSpeed` | 2 | 0.5 | 6 | tiles/s; used only with wall jump |
  | `ClimbSpeed` | 3 | 1 | 8 | tiles/s; walls and ropes |

  - `Clamped()` returns a copy with each field clamped to [Min, Max]. Non-finite values fall back to the default. Then `WallSlideSpeed = min(WallSlideSpeed, MaxFallSpeed)` enforces max fall ≥ wall slide.
- **NEW `MovementInternals.cs`**: `static class`, `const` only. Seconds → ticks with `(int)(seconds * FixedStepAccumulator.TicksPerSecond + 0.5f)`. These are starting values; Phase 2's play-check tunes them.

  | Constant | Value | Derived at defaults |
  |---|---|---|
  | `TargetTimeToApex` | 0.35 s | `BaseGravity = 2 · DefaultJumpHeight / 0.35² = 57.142857` tiles/s² |
  | `FallGravityFactor` | 1.75 | fall gravity 100 tiles/s² (applies while vy ≤ 0) |
  | `MinHopHeight` | 1.25 tiles | cut velocity √(2·57.142857·1.25) = 11.952 tiles/s |
  | `CoyoteTime` | 0.1 s | `CoyoteTicks = 6` |
  | `JumpBufferTime` | 0.1 s | `JumpBufferTicks = 6` |
  | `WallCoyoteTime` | 0.1 s | `WallCoyoteTicks = 6` |
  | `WallJumpInputLock` | 0.2 s | `WallJumpLockTicks = 12` |
  | `RopeRegrabLockout` | 0.2 s | `RopeLockoutTicks = 12` |
  | `AirControlFactor` | 0.75 | air accel/decel/turnaround = ground × 0.75 |
  | `DecelerationFactor` | 1.5 | ground decel 90 tiles/s² |
  | `TurnaroundFactor` | 2.0 | ground turnaround 120 tiles/s² |
  | `WallJumpPushFactor` | 1.5 | push-off 9 tiles/s (× run speed) |
  | `WallJumpReach` | 0.125 tile | wall jump allowed up to this gap |
  | `CeilingCornerCorrection` | 0.25 tile | cap |
  | `HitboxWidth` / `HitboxHeight` | 0.75 / 1.5 | half width 0.375, grip at +0.75 |
  | `LedgePopInset` | 0.375 tile | popped hitbox's near edge this far past the wall face |
  | `RopeGrabReach` | 0.375 tile | max \|player centre X − rope X\| to grab |
  | `RopeTopTolerance` | 0.0625 tile | rope-top / surface match |
  | `DropThroughNudge` | 0.0625 tile | feet moved below the one-way top on a drop |
  | `ContactEpsilon` | 0.0001 tile | touching / on-a-row tolerance |
  | `StartSearchRadius` | 8 cells | nearest-free-start search |

- **NEW `DerivedMovement.cs`**: `readonly struct`, built from `Clamped()` settings in the constructor (once per settings change, never per tick):
  - `RiseGravity = BaseGravity × Gravity`; `FallGravity = RiseGravity × FallGravityFactor`
  - `JumpVelocity = √(2 · RiseGravity · JumpHeight)`. Rising gravity only, so the jump height in tiles holds at any gravity.
  - `MinHopVelocity = √(2 · RiseGravity · min(MinHopHeight, JumpHeight))`
  - `GroundAccel = RunSpeed / max(TimeToFullSpeed, dt)`; `GroundDecel = ×1.5`; `GroundTurnaround = ×2`; air variants × 0.75
  - `WallPushSpeed = RunSpeed × 1.5`
  - copies of `RunSpeed`, `MaxFallSpeed`, clamped `WallSlideSpeed`, `ClimbSpeed`, `AirJumps`
- **Tests (`MovementSettingsTests.cs`):**
  - every `Default*`, `Min*` and `Max*` pinned to the table (a retune must touch the test and entities.md together)
  - every internal pinned, including the tick counts 6 / 6 / 6 / 12 / 12
  - at defaults: BaseGravity 57.142857, JumpVelocity 20.0, FallGravity 100, MinHopVelocity 11.952, accelerations 60 / 90 / 120 and 45 / 67.5 / 90, push 9 (all ±1e-3)
  - at Gravity 2 the JumpVelocity is 28.284 and the computed apex `v²/2g` is still 3.5
  - clamps: Gravity 0 → 0.25; AirJumps 0 → 1; MaxFallSpeed 2 with WallSlideSpeed 6 → slide 2; NaN → default; TimeToFullSpeed 0 → full speed in 1 tick
  - **worst case per tick:** over every Min/Max combination of the settings that affect speed, the largest per-tick displacement on either axis (jump velocity, max fall, push-off, run, climb) is < 0.75 tile. Expected worst case: jump 6 at gravity 2 → 37.03 tiles/s = 0.617 tile/tick.
- **Verify:** `[automated]` EditMode green.

### Step 5: Nearest free start
- **NEW `StartPlacement.cs`**: `readonly struct` with `X`, `Y`, `Moved`, `Found` and `public static StartPlacement Find(TileGrid grid, float feetX, float feetY)`.
    - The hitbox at the given feet must be `BoxIsFree` **and** have `feetY ≥ 0` (checked here, not in `BoxIsFree`). Free means: no Solid/Climbable overlap, and inside the map, so feet below row 0 or outside the sides are not free. That prevents an instant fall-out loop. If free → return it unchanged (`Moved = false`, `Found = true`).
    - Otherwise consider every cell-centred feet position `(c + 0.5, r)` with `|c − cx| ≤ 8` and `|r − cy| ≤ 8` (`cx, cy` = the start's cell) inside the map whose hitbox is free.
      - Pick the smallest Euclidean distance to the original feet.
      - Ties go to higher `r` (prefer up), then lower `c`.
      - Return it with `Moved = true`.
    - If none is free → original position, `Moved = true`, `Found = false`.
    - Play-start only; plain loops, no LINQ. Allocation-free anyway.
- **Tests (`StartPlacementTests.cs`):**
  - free start unchanged
  - start inside a ground block → moved onto the nearest free cell above
  - start overlapping a wall's side → nearest free cell beside it
  - deterministic tie-break (left/right equidistant → left; up/down equidistant → up)
  - start below the map → moved inside
  - fully enclosed 17×17 solid → `Found = false`
  - one-way cells don't count as occupied
- **Verify:** `[automated]` EditMode green.

### Step 6: Player state, input and stepper core (run, gravity, landing, walk-off, ceilings, bounds)
- **NEW `PlayerInput.cs`**: `readonly struct PlayerInput(int moveX, int moveY, bool jumpPressed, bool jumpHeld)`. The constructor normalises the axes with `Math.Sign`.
    - `JumpPressed` is a per-tick edge (the runtime latches it).
    - Pressed with `JumpHeld = false` is a valid one-tick tap.
    - Up never means jump.
- **NEW `PlayerContact.cs`**: `Air, Ground` (standing with ≥ 1 solid/climbable cell underfoot), `OneWayTop` (standing on one-way cells only), `WallSlide, WallCling, Rope`.
- **NEW `PlayerState.cs`**: `struct`, public fields:
    - `X, Y, VelocityX, VelocityY, Contact`
    - `Facing` (+1/−1, starts +1), `AirJumpsLeft`
    - `CoyoteTicks, BufferTicks, WallCoyoteTicks, WallCoyoteSide, WallCoyoteFace, WallCoyoteFromCling` (`WallCoyoteFace` = the integer x of the wall face the coyote window belongs to)
    - `WallLockTicks, RopeLockoutTicks`, `WallSide` (−1/0/+1), `RopeIndex` (−1 = none)
    - `LastWallJumpSide` (−1/0/+1, 0 = none), `LastWallJumpFace` (int): the wall last jumped from (Step 9)
    - `Abilities, StartX, StartY`
    - Getter `IsStanding` (Ground or OneWayTop).
- **NEW `PlayerStepper.cs`**: `sealed class`.
    - `PlayerStepper(TileGrid grid, RopeSegment[] ropes, MovementSettings settings)`: null grid or ropes → throw. Ropes are copied once (play-start allocation); lowest index wins on overlaps.
    - `ApplySettings(MovementSettings)` rebuilds `DerivedMovement` (number settings apply live); `Settings` getter.
    - `CreateState(float feetX, float feetY, Abilities abilities)`: Air, zero velocity, `AirJumpsLeft = AirJumps`, counters 0, `StartX/StartY` = the given feet. The caller applies `StartPlacement.Find` first.
    - `PlayerState Step(in PlayerState s, in PlayerInput input)`. **Fixed tick order (later steps plug into it):**
      1. (No decrement here; see item 9.)
      2. Probe contacts at tick start: support, wall gaps both sides (reach 0.125), climbable at grip height both sides, rope in reach. **The support probe updates the standing contact** (Ground / OneWayTop, or Air when a standing player has lost support) before items 3–5 use it. It promotes Air to standing **only when vy ≤ 0**, matching the Landing rule, so a rising jump whose feet happen to sit on a one-way top at a tick boundary keeps rising. So a state fresh from `CreateState` (Air) that is resting on the floor uses ground rates and ground jumps on tick 1. Wall, cling and rope contacts are kept from the previous tick and changed only by items 4 and 7.
      3. Jump-press resolution: one method, priority order defined in Step 7.
      4. Input transitions: rope grab, cling entry/exit.
      5. Velocity by contact.
      6. Move: `SweepX` then `SweepY`.
      7. Post-move classification: land, walk off, slide/cling entry and exit, climb limits, rope limits, head bump, fall-out reset.
      8. Facing.
      9. Decrement every active counter that was not set this tick (coyote, buffer, wall coyote, lock, lockout), per the timer convention.
    - **Horizontal (not locked, not rope/cling):** target = `MoveX × RunSpeed`. The rate is:
      - decel when `MoveX = 0`
      - turnaround when the velocity is non-zero and opposite to `MoveX`
      - accel when in the same direction at or under run speed
      - decel when over run speed in the same direction
      Velocity moves toward the target by `rate·dt` without passing it. Air rates are × 0.75.
    - **Vertical in Air:** gravity `RiseGravity` while vy > 0, `FallGravity` while vy ≤ 0. vy is clamped at `−MaxFallSpeed`. Standing: vy = 0, no Y move. Walk-off is detected by the post-move `Support` probe.
    - **Landing** (`SweepY` landed, or vy ≤ 0 with support): Ground/OneWayTop, vy = 0, `AirJumpsLeft = AirJumps`, wall coyote cleared, `LastWallJumpSide = 0`.
    - **Walking off** (was standing, no support, no jump this tick): Air with `CoyoteTicks = 6`.
    - **Ceiling hit** zeroes upward velocity.
    - **Fall-out:** when the hitbox top < 0 after the move, the result is `CreateState(StartX, StartY, Abilities)` (abilities kept, counters cleared). This is temporary until pits damage in combat-and-enemies.
    - **Facing** follows a non-zero `MoveX` when not locked.
- **NEW `Tests/EditMode/Movement/PlayerScenario.cs`**: built from `AsciiLevel` + `Abilities` + optional `MovementSettings` (defaults). Exposes `State`, `Tick(PlayerInput, int count = 1)` and small input helpers. Keeps a per-run record (max Y, ticks run, contacts seen) for apex and outcome asserts. Closures and allocation are fine here (test code).
- **Tests (`RunAndFallTests.cs`):**
  - from rest holding right: vx 1, 2 … 6 on ticks 1–6, then stays 6
  - release from 6 → 0 in 4 ticks (decel 90)
  - reverse from +6 → 0 in 3 ticks, then reaches −6 six ticks later
  - same sequences in the air use the × 0.75 rates
  - TimeToFullSpeed 0 → full speed in 1 tick
  - spawned on the ground stays put and becomes Ground on tick 1
  - falling from rest reaches −12 and stays there (max fall)
  - walking off a ledge → Air with coyote 6
  - running into a wall stops flush and vx = 0
  - a head bump zeroes vy
  - map sides block, map top is a ceiling
  - falling out of the bottom resets to the start: position = start, velocity 0, air jumps full, abilities kept
  - after `ApplySettings(RunSpeed 10)` mid-run the target becomes 10
- **Verify:** `[automated]` EditMode green.

### Step 7: Jump, variable height, minimum hop, coyote, buffer, air jumps, jump-press priority
- **MODIFY `PlayerStepper.cs`**:
    - **Jump-press priority (one explicit method, first match wins):**
      1. drop-through: Down held and `Contact == OneWayTop`; filled in at Step 8
      2. ground or coyote jump: standing, or `CoyoteTicks > 0`; needs `Jump`
      3. rope jump: filled in at Step 11
      4. wall jump: filled in at Step 9
      5. air jump: Air, `DoubleJump`, `AirJumpsLeft > 0`
      6. otherwise store `BufferTicks = 6` (a fresh press overwrites)
    - **Ground/coyote jump:** vy = `JumpVelocity`, Air, coyote and buffer cleared. It doesn't spend an air jump. Coyote is granted only by walking off (Step 6), never after a jump or a drop.
    - **Air jump:** vy = `JumpVelocity` (replaces any fall speed, so air-jump height = ground-jump height); `AirJumpsLeft − 1`.
    - **Variable height:** while airborne with vy > `MinHopVelocity` and `!JumpHeld`, set vy = `MinHopVelocity`. This is applied on the jump tick too, so a press with `JumpHeld = false` (a one-tick tap) peaks at exactly the minimum hop. Applies to every jump kind.
    - **Buffer:** on a tick without a press, with `BufferTicks > 0`: if a ground/coyote jump (or, from Step 9, a wall jump) is available at tick start, fire it with the current `JumpHeld` (a buffered press released before landing → minimum hop). Otherwise it counts down at item 9. A press on tick `P` is buffered for ticks `P+1 … P+6`. Landing on tick `L`, it fires on `L+1` (the first tick that starts standing) if `P ≥ L − 5`. The buffer never fires a drop, a rope jump or an air jump. Landing on a one-way with Down held and a pending buffer → jumps (a drop needs a fresh press).
    - **`jump` off:** ground and coyote jumps don't exist. Pressing on the ground does nothing (it is buffered, but a buffered press can only fire a ground jump if `Jump` is on). With `DoubleJump` on, air jumps still work after walking off a ledge.
- **Tests (`JumpTests.cs`):**
  - full jump apex 3.5 (±0.01) at gravity 1 and at gravity 2 (height holds, time-to-apex shortens: 21 vs ~15 ticks)
  - one-tick tap apex 1.25 (±0.01)
  - release mid-rise → apex between 1.25 and 3.5, monotonic in release tick
  - coyote: a press on `W+6` jumps and doesn't spend an air jump; a press on `W+7` doesn't jump (DoubleJump off)
  - no coyote after a jump (pressing on the descent of a ground jump with DoubleJump off → no jump)
  - buffer: a press on `L−5` jumps on `L+1`; a press on `L−6` doesn't jump (DoubleJump off)
  - buffered press with jump released before landing → apex 1.25
  - air jump: with AirJumps 1, a second air press does nothing; AirJumps 2 allows two
  - air jumps reset on landing
  - air-jump apex from the top of a full jump is 7.0 (±0.02)
  - `jump` off: no ground jump, no coyote jump; with DoubleJump on, walking off and pressing → air jump
- **Verify:** `[automated]` EditMode green.

### Step 8: One-way platforms and drop-through
- **MODIFY `PlayerStepper.cs`**:
    - Standing classification uses `Support` (OneWayTop only when no solid cell is underfoot).
    - **Drop-through** (priority 1, needs no ability, works with `Jump` off): Down held + fresh press while `OneWayTop`. Feet move down by `DropThroughNudge`, vy = 0, Air, `CoyoteTicks = 0`, the press is consumed (not buffered). The next tick's start bottom is below the top, so the platform doesn't catch the player.
    - Down + jump on solid or straddling ground = normal jump.
- **Tests (`OneWayTests.cs`):**
  - jumping up through a one-way from below lands on it on the way down only if the feet clear its top
  - a jump whose apex leaves the feet inside the platform falls back through and doesn't snap up
  - a one-way's side never stops horizontal motion or allows a wall slide
  - straddling one-way + solid, Down + jump → jumps (solid wins)
  - Down + jump on a one-way drops through with `Jump` both on and off, consumes the press (no jump after leaving it) and grants no coyote
  - dropping onto a lower one-way while holding Down lands on it
  - a buffered press landing on a one-way with Down held → jumps
  - Down alone does nothing
- **Verify:** `[automated]` EditMode green.

### Step 9: Wall slide, wall jump, input lock, wall coyote
- **MODIFY `PlayerStepper.cs`**:
    - **Wall:** a blocking column face (Solid; Climbable when `WallClimb` is off; map sides). OneWay is never a wall.
    - **Wall slide** (post-move): Air, vy ≤ 0, `WallJump` on, not locked, pressing toward a side with gap ≤ `ContactEpsilon`. Contact becomes WallSlide (or WallCling on climbable with `WallClimb`, Step 10) and `WallSide` is set.
      - **Air-jump refill on slide entry only on a different wall** than the one last jumped from. `PlayerState` keeps `LastWallJumpSide` (−1/0/+1) and `LastWallJumpFace` (the integer x of the wall face). A slide on the same side and face as the last wall jump does **not** refill `AirJumpsLeft`. Any other wall refills (`AirJumpsLeft = AirJumps`), so chimneys (alternating faces) refill as usual. Landing, a cling and a rope grab clear the memory (`LastWallJumpSide = 0`). Without this, double jump + wall jump climbs any single wall without limit (round 1, F2: ≈ +6.9 tiles per cycle), contradicting the no-single-wall-scaling rule.
      - While sliding, gravity applies and vy is clamped at `−WallSlideSpeed`.
      - The slide ends on releasing or pressing away, on leaving the wall, or on landing. Leaving without a jump grants `WallCoyoteTicks = 6` with the side and face remembered (`WallCoyoteSide`, `WallCoyoteFace`).
      - Mere contact without the ability resets nothing.
    - **Wall jump** (priority 4): airborne (Air/WallSlide/WallCling). The wall comes from one of:
      - a wall gap ≤ `WallJumpReach` (0.125) on either side
      - an active slide or cling
      - `WallCoyoteTicks > 0`
      It needs `WallJump`, except that a jump from a cling, or from wall coyote that started at a cling, is always allowed (Step 10).
      - Push direction is away from the wall. Wall choice when walls are on both sides: the pressed side, then the nearer wall, then the left wall.
      - vx = `−side × WallPushSpeed` (9), vy = `JumpVelocity`, Air, `WallLockTicks = 12`, Facing = push direction, `LastWallJumpSide`/`LastWallJumpFace` = this wall (for a jump from wall coyote with no wall in reach: `WallCoyoteSide`/`WallCoyoteFace`). It doesn't spend an air jump.
      - **During the lock, horizontal input is ignored and vx stays at the push speed.**
    - The buffer (Step 7) also fires as a wall jump when a wall becomes available.
- **Tests (`WallJumpTests.cs`):**
  - slide starts only while falling and pressing in, not while rising, not when neutral; slide speed caps at 2
  - releasing ends the slide on tick `E`; a wall jump on `E+6` works (wall coyote), on `E+7` it doesn't
  - a wall jump on `J` pushes away at 9 tiles/s; pressing back on `J+1 … J+12` changes nothing, on `J+13` it does
  - a buffered press fires as a wall jump on contact
  - wall jump within 0.125 of the wall works, at 0.2 it doesn't
  - air jumps reset on slide entry but not on contact without `WallJump`
  - air jumps are **not** refilled by a slide on the same wall face just jumped from, and are refilled on the opposite wall of a chimney and after landing
  - **priorities:** in the air near a wall with air jumps left → wall jump and `AirJumpsLeft` unchanged; in coyote next to a wall → ground jump (vx unchanged, no push)
  - **no single-wall scaling (defaults).** A tall ordinary wall, the player sliding on it. Two scripts, because they guard different rules:
    - **Script A — reach (DoubleJump off):** hold jump and press it every tick; hold toward the wall whenever not locked. Assert three consecutive wall jumps, each starting ≥ 0.1 tile lower than the previous one, each fired with the wall within `WallJumpReach` (pressing every tick fires the jump *before* contact, because the return speed of ≤ 0.1 tile per tick is under the 0.125 reach). Print the measured loss per cycle. Reviewer's round-1 model: ≈ 0.38–0.58 tile per cycle.
    - **Script B — the refill rule (DoubleJump on, AirJumps 1), the exploit loop:** jump only from an active WallSlide (wait while falling until contact is WallSlide, then press); press again at each apex (the air jump); hold toward the wall whenever not locked. Assert:
      - every cycle really reached WallSlide before its wall jump (precondition);
      - the first cycle's gain (second start − first start) is ≤ 7.0, i.e. at most the one air jump the player started with (Reviewer's exploit-loop model: +6.875);
      - each later start is ≥ 0.1 lower than the previous one (Reviewer's exploit-loop model: 0 → 6.875 → 6.297 → 5.719, i.e. −0.578 per cycle after the first);
      - each later slide entry left `AirJumpsLeft` at 0 (no refill on the same face).
      Without rule (b) the same script gains ≈ +6.9 per cycle (Reviewer's model), so this is a real guard. Print the measured starts.
    - **Script C — coyote detour:** as B, but each cycle steps off the wall (press away) and wall-jumps from wall coyote with no wall in reach. Assert the next slide on the same face does not refill `AirJumpsLeft`.
- **Verify:** `[automated]` EditMode green.

### Step 10: Wall climb (cling, climb, ledge pop)
- **MODIFY `PlayerStepper.cs`**:
    - **Grip:** the cell beside the hitbox on a side, in row `floor(gripY − ContactEpsilon)` (grip height `gripY = Y + 0.75`; top edge inclusive), is Climbable, and `WallClimb` is on. With `WallClimb` off, Climbable is an ordinary wall. With the inclusive top edge, a grip resting exactly at `T` still reads the run's top cell.
    - **Cling entry:**
      - airborne, pressing toward a touching climbable wall with grip, **and either falling (vy ≤ 0) or holding Up**; or
      - standing with Up held while pressing toward it.
      A rising jump past a climbable face is therefore not stopped dead unless the player asks for it with Up, so WallClimb never makes a plain jump onto a ledge worse.
      Contact becomes WallCling, `WallSide` is set, `AirJumpsLeft = AirJumps`, `LastWallJumpSide = 0`, velocity 0.
    - **While clinging:**
      - vx = 0, vy = `MoveY × ClimbSpeed` (swept), no gravity
      - neutral input holds still; pressing away releases (Air, wall coyote 6 from cling)
      - jump = push-away wall jump (works with `WallJump` off)
    - **Top:** let `T` = top of the climbable run beside the grip.
      - Climbing up stops with the grip at `T`.
      - If an Up move would carry the grip past `T` and the cell above the run's top on that side is free, try the **ledge pop**. The target is feet at `T`, with the hitbox moved toward the wall so its near edge lies 0.375 past the wall face (centre 0.75 past the face). If that box is free and stands on the wall top, the player is placed there, Ground, velocity 0.
      - Otherwise (topped by solid, or the pop target is blocked) the grip stays at `T`.
    - **Bottom:** climbing down onto ground lands (Ground). Climbing below the run's bottom with no ground releases (Air).
- **Tests (`WallClimbTests.cs`):**
  - with `WallClimb` off a climbable wall behaves exactly like an ordinary wall (slide with `WallJump`, no cling)
  - cling from the air (falling, or rising with Up) and from the ground (Up + toward)
  - rising past a climbable face while holding only toward it does not cling; the same jump reaches a 3-tile climbable-faced ledge with WallClimb on and off
  - holding still at the top (grip at `T`) for 60 ticks stays WallCling
  - climb up and down at 3 tiles/s; neutral holds still
  - pressing away releases
  - climbing over an open top pops onto the ledge (standing, feet at `T`, centre 0.75 past the face)
  - climbable topped by solid: climbing stops with the grip at `T`, no pop
  - pop blocked by a low ceiling → stops at `T`
  - climbing down onto ground lands; off the bottom of a floating climbable strip → falls
  - **`WallClimb` on + `WallJump` off:** cling and jump off pushes away; an ordinary wall gives no slide and no wall jump
  - air jumps reset on cling
- **Verify:** `[automated]` EditMode green.

### Step 11: Ropes
- **MODIFY `PlayerStepper.cs`**:
    - **Reach:** `|centreX − rope.X| ≤ 0.375` and `rope.Bottom ≤ gripY ≤ rope.Top`. The first rope in list order that matches wins.
    - **Grab** (only with Up or Down held, never on contact alone): needs `RopeClimb` and `RopeLockoutTicks = 0`. Also allowed: standing on a OneWay top within 0.0625 of a rope's top with Down held, in X reach (climbing down a rope from the platform it hangs through).
      - Snap: centre X = rope X. If the snapped box isn't free → no grab.
      - Contact becomes Rope, `RopeIndex` is set, velocity 0, `AirJumpsLeft = AirJumps`, `LastWallJumpSide = 0`.
    - **On the rope:**
      - vx = 0, horizontal input ignored, no swinging
      - vy = `MoveY × ClimbSpeed`, swept with OneWay ignored and solid still blocking
    - **Top:** if a **OneWay** cell lies at the rope top (row `round(Top) − 1` under the hitbox, `|round(Top) − Top| ≤ 0.0625`), climbing continues until the feet reach `Top`, then the player stands on it. Otherwise climbing stops with the grip at `Top`. Only OneWay qualifies: a rope ending inside a Solid or Climbable cell blocks the head before the feet arrive, so it just stops at the head bump (the runtime warns about ropes overlapping solid tiles, Phase 2).
    - **Bottom:** the grip going below `Bottom` releases (Air, no coyote). Climbing down onto ground lands.
    - **Rope jump** (priority 3):
      - vy = `JumpVelocity` (full height), vx = `MoveX × RunSpeed`, Air
      - `RopeLockoutTicks = 12`; no air jump spent; no rope coyote
      - variable height applies
- **Tests (`RopeTests.cs`):**
  - overlapping without Up/Down → no grab; Up grabs and snaps to the centre; Down grabs
  - `RopeClimb` off → passive (Up/Down do nothing, the player falls past)
  - climb up and down at 3
  - top without a surface stops with the grip at Top; off the bottom releases
  - a rope through a one-way: climbing up steps onto the platform; standing there, Down grabs and descends through it
  - jump off at full height (apex 3.5 above the jump point) without spending an air jump
  - re-grab blocked for 12 ticks after a rope jump, allowed on tick 13
  - snapping blocked by a wall → no grab
  - overlapping ropes → the lower index is grabbed
  - air jumps reset on grab
  - **priorities:** on a rope next to a wall, a press → rope jump (vx = input × 6, no push-off); a one-way top with a rope and Down + press → drop-through, not a grab
- **Verify:** `[automated]` EditMode green.

### Step 12: Gate integrity and ability matrix (defaults)
- **NEW `GateIntegrityTests.cs`**: default settings and internals, best-case inputs.
  - **Ledges:** standing 1 tile from a wall, holding toward it, jump held → a 3-tile ledge is reached (standing on top), a 4-tile ledge never is.
  - **Double jump:** a second press at the apex reaches a 6-tile ledge and never an 8-tile one. Open-air apex is 2 × jump height (7.0 ± 0.02).
  - **Gaps:** same-height ground, running at full speed, press on `W+6` (the last coyote tick): a 5-tile gap clears, a 6-tile gap doesn't. The expected maximum is ≈ 5.4 tiles (Reviewer's round-1 model: 5.1 jumping on `W`, 5.3 on `W+5`, 5.4 on `W+6`); the test also checks that the jump was a coyote jump.
  - **Corridor:** a 2-tall corridor lets the hitbox through; a 1-tall one blocks it at the entrance.
  - **Chimney:** a 3-wide, 12-tall shaft is climbed with WallJump (alternating walls) and not without; a 6-wide shaft is not climbed. Print the measured gain per jump and the widest climbable shaft (Reviewer's round-1 model: up to 5 wide, gains ≈ 0.33 / 1.5 / 2.2 / 3.2 / 3.3 / 1.6 at widths 1 / 1.5 / 2 / 3 / 4 / 5).
  - **Ceiling correction:** over the scenario suite's ceiling cases, no correction exceeds 0.25.
  - Each test's failure message names the gate and the defaults it assumes, so a Phase 2 retune that moves a breakpoint reads as "gate moved", not "bug".
- **NEW `AbilityMatrixTests.cs`**: one ASCII map (an ordinary wall, a climbable wall, a rope, a one-way, a 4-tile ledge) × ability sets: `Jump` only; `+DoubleJump`; `+WallJump`; `+DoubleJump +WallJump` (the ordinary wall still can't be scaled, using Step 9's Script B inputs and its assertions); `+WallClimb` without `WallJump`; `+RopeClimb`; all; `DoubleJump` without `Jump`. For each set, assert which surfaces respond (slide, cling, grab, air jump, ledge reached) and that the others stay passive. `[TestCase]`-driven.
- **Verify:** `[automated]` EditMode green.

### Step 13: Step allocates nothing
- **NEW `StepAllocationTests.cs`**:
  - Build a scenario that exercises every branch: run, jump, air jump, slide, wall jump, cling, pop, rope, drop, fall-out reset.
  - Warm up for 120 ticks, then measure `GC.GetAllocatedBytesForCurrentThread()` around 600 further `Step` calls (inputs precomputed in an array). Assert a delta of 0.
  - **Probe sanity first:** allocating a `new byte[1024]` must show a delta ≥ 1024, otherwise the test fails, saying the probe is unsupported (no vacuous pass).
  - If the probe is unsupported on either editor, use Unity Test Framework's `Is.Not.AllocatingGCMemory()` constraint instead (it reads the profiler's GC.Alloc recorder), wrapped around the same 600 steps, with its own sanity check that a planted allocation fails it. Whether it works in batch mode on 6000.0 is unverified; prove it here.
  - If neither works on an editor: record it in `.claude/memory/framework.md`, mark the test `[Ignore]` with the reason, and say plainly in the phase log that **code review alone** carries the guarantee on that editor. Phase 2's guard uses the same probes, so it is not a second layer.
- **Verify:** `[automated]` EditMode green, with the sanity branch observed passing.

### Step 14: Run Tests
- `python Tools/unity_tests.py --platform editmode` (both editors). Close the dev project in the Unity Editor first.
- If tests fail, fix the failing tests or the code causing the failure.
- Do not proceed to code review until all tests pass on 6000.0.85f1 and 6000.6.7f1. Then also run `python Tools/unity_tests.py --platform playmode` once to confirm nothing regressed (no runtime change is expected).

### Step 15 (Final): Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on all files changed during this phase. Ask it specifically about:
  - purity (including no `Random*` identifiers)
  - static state
  - per-tick allocations, closures and LINQ in `Step`
  - one type per file
  - every internal defined once in `MovementInternals` and every default once in `MovementSettings`
  - the tick order and priority method matching this prompt
- Fix any HIGH/MED issues, re-run Step 14, and report the review result to the user.

---

## Completion Criteria

- [ ] Sim purity holds: no engine/clock/random use, no `Random*` identifiers, no LINQ, no static mutable fields. The reflection guard was seen failing on a planted field.
- [ ] `MovementSettings` holds every student-facing default plus `Min*`/`Max*` constants. `MovementInternals` holds every internal at the values in Step 4. Both are pinned by tests.
- [ ] Jump height holds across gravity multipliers (3.5 ± 0.01 at gravity 1 and 2). A one-tick tap peaks at 1.25 ± 0.01. An air jump from the apex peaks at 7.0 ± 0.02.
- [ ] Coyote (6 ticks), buffer (6), wall coyote (6), wall-jump lock (12) and rope lockout (12) are each tested at the boundary on both sides.
- [ ] Jump-press priority is one method in the order drop > ground/coyote > rope > wall > air > buffer, with the pairwise tests green.
- [ ] One-way, wall, climb, rope, map-bounds and nearest-free-start scenario tests green, covering every Step 8–11 bullet.
- [ ] Gates at defaults: 3-tile ledge yes / 4 no; with double jump 6 yes / 8 no; gap 5 yes / 6 no; 2-tall corridor yes / 1 no; ceiling correction ≤ 0.25; repeated wall jumps on an ordinary wall lose ≥ 0.1 tile per cycle, with DoubleJump off and on; a 3-wide shaft is climbed with WallJump and not without, a 6-wide one is not.
- [ ] Ability-matrix tests green, including `DoubleJump` without `Jump` and `WallClimb` without `WallJump`.
- [ ] `Step` allocation test green (or the documented fallback recorded in framework.md and the phase log).
- [ ] EditMode suite green on 6000.0.85f1 and 6000.6.7f1, no compiler warnings; PlayMode still green.
- [ ] Code review: no open HIGH/MED.
- [ ] Feel is **not** a completion criterion. The operator play-check and default tuning happen in Phase 2.

---

## Notes to GDD

These are synced to the GDD at completion (GDD v0.06 → v0.07). Items marked **(proposal)** are Claude's fill-ins, not operator decisions. Show them to the operator with the completion report; they are confirmed or changed at Phase 2's play-check.

**entities.md § Player tunables**
- Replace the sketch table with the two student-facing sections and their defaults/ranges from Step 4 (field names = `MovementSettings` identifiers):
  - **Movement:** run speed 6, time to full speed 0.1 s, jump height 3.5, gravity 1× multiplier, max fall 12
  - **Abilities:** toggles double jump / wall jump / wall climb / rope climb; air jumps 1 (min 1), wall slide speed 2, climb speed 3
- "Acceleration" is replaced by "time to full speed". Deceleration (× 1.5) and turnaround (× 2) are derived. Air control is internal (× 0.75).
- Resolve the sketch's open line: gravity is a multiplier over base gravity 57.142857 tiles/s², derived from a 0.35 s time-to-apex at jump height 3.5. Jump velocity is derived from jump height and rising gravity, so gravity changes timing only.
- Add the internals table from Step 4 (all values), including the hitbox 0.75 × 1.5, feet-centre position and grip point at the hitbox centre.
- Keep the attack/HP rows (not in this phase). The Player-start-marker line is resolved by Phase 2, not here.

**entities.md § Abilities**
- Sim identifiers: `Jump, DoubleJump, WallJump, WallClimb, RopeClimb` (`Abilities` flags). Run is always on and has no flag. `attack` arrives with combat-and-enemies.

**systems.md § Player** (add the rules):
- Contact states (air, ground, one-way top, wall slide, wall cling, rope) and the tick order.
- Jump-press priority: drop-through > ground/coyote > rope > wall (touching or within 0.125) > air > buffer.
- Coyote only after walking off a ledge; a coyote jump doesn't spend an air jump and clears the window. Wall coyote: yes. Rope coyote: no.
- Buffer fires only ground or wall jumps; a buffered press released before landing gives a minimum hop.
- Variable height by velocity cut; minimum hop 1.25 tiles; air-jump height = jump height.
- Air jumps reset only on landing, an active wall state (slide/cling, which needs the ability) or a rope grab.
  - A wall slide refills only on a different wall face than the one last jumped from (including a jump from wall coyote), so double jump + wall jump can't scale a single wall; chimneys refill as usual (operator, 2026-10-23: Open question 3, option (b)).
- Drop-through: Down + fresh press on a one-way top, works with jump off, consumes the press, no coyote; solid wins when straddling; a buffered jump onto a one-way with Down held jumps.
- One-way: land only if the feet were at or above its top on the previous tick; sides never walls; never a ceiling.
- Walls: slide only while falling and pressing in; wall-jump input lock 0.2 s holds the push speed; **no single-wall scaling on ordinary walls at defaults (deliberate)**.
- Wall climb only on climbable tiles; climbable with wall climb off = ordinary wall; ledge pop at an open top; wall climb without wall jump allowed, and jumping off a cling always pushes away.
- Ropes: ladder-style, grab with Up/Down only, snap to centre, re-grab lockout 0.2 s, top steps onto a platform else stops, bottom releases, full-height jump without spending an air jump, lowest index wins.
- Collision: X then Y sweeps; diagonal corners land on top; ceiling hit zeroes upward speed; ceiling-corner correction ≤ 0.25 tile.
- Map bounds: sides and top are solid; falling out of the bottom resets to the start (temporary until pits damage in combat-and-enemies).
- Nearest free start for a start inside solid.
- Replace "Separate ground and air acceleration" with "air control is a fixed fraction of ground".
- Proposals:
  - **(proposal)** With `jump` off there are no ground or coyote jumps, but air jumps work after walking off a ledge.
  - **(proposal)** Wall jump gives full jump height at 1.5 × run speed push-off.
  - **(proposal)** Cling entry: toward a climbable wall in the air while falling or holding Up, or Up + toward on the ground. Neutral input holds; pressing away lets go. A rising jump past a climbable face doesn't cling by itself.
  - **(proposal)** Timer convention: a window of N ticks is active on the N ticks after it is set (coyote, buffer, wall coyote 6; lock, lockout 12).
  - **(proposal)** A rope can be grabbed from above with Down while standing on a one-way at its top.
  - **(proposal)** Rope jump horizontal speed = input × run speed.
  - **(proposal)** Climb speed is shared by walls and ropes.
  - **(proposal)** Two-wall tie-break: pressed side, then nearer, then left.
  - **(proposal)** Every numeric internal value.

**systems.md § World & Level Elements / § Tile System**
- In the sim the tile kinds are None/Solid/OneWay/Climbable. Hazard is reserved (value 4) for combat-and-enemies. Climbable collides as solid.
- Ropes reach the sim as a list of vertical segments.

**Glossary.md (add as Draft):** Contact state, Coyote time, Wall coyote, Jump buffer, Air jump, Minimum hop, Jump-press priority, Wall slide, Wall jump, Wall-jump input lock, Wall cling, Ledge pop, Drop-through, One-way platform, Climbable wall, Rope (sim: rope segment), Re-grab lockout, Hitbox, Grip point, Base gravity, Gravity multiplier, Time to full speed, Ceiling-corner correction, Nearest free start, Map bounds, Tile grid, Scenario test.

**conventions.md § Testing & Playtesting:** scenario tests are built from ASCII maps (`AsciiLevel`) and driven through `PlayerScenario`; matrix tests (one map × ability sets) for gates; gate tests name the defaults they assume. **§ Performance Idioms:** `Step` is allocation-free, guarded by `StepAllocationTests` (or its documented fallback). The sim has no static mutable state, guarded by reflection.

**architecture.md § System Map:** the sim now owns the tile grid and tile collision (filled by the runtime at play start).

**Operations (same commit):**
- Already applied when this prompt was written (do not repeat): the Roadmap §1 Milestone 1 split into `movement-sim` / `movement-runtime` with the "Pulled forward (2026-10-23)" line, and the four 2026-10-23 rows in `decisions.md`. At completion, flip the movement-rules row from Draft to Active, and add the air-jump refill rule (b), which the operator settled on 2026-10-23 (Open question 3); its decisions.md row was added then.
- Roadmap §0: row for this phase.
- Changelog v0.07; GDD.md version bump.

**Open questions (operator):**
1. Default gap breakpoint is 5 clears / 6 doesn't (best case ≈ 5.4 tiles). It is set by run speed, jump height and max fall, **and** by the internals time-to-apex, fall-gravity factor and coyote ticks, so a retune of any of them moves it. Acceptable until Phase 2's play-check? Claude recommends yes; the levers, if not, are the ones just listed.
   Reviewer recommends: yes, keep it — a starting value with real margin on both sides; Phase 2's play-check is where it should move (round 1, F15).
2. `Abilities` carries no `Run` flag, although entities.md lists `run` as an ability: run is always on and nothing gates it. Claude recommends no flag, and entities.md says so.
   Reviewer recommends: no flag, and entities.md says so — a flag nothing gates would be a toggle that does nothing, which CLAUDE.md rule 8 forbids (round 1, F16).
3. With double jump and wall jump both on, sliding back onto the same wall refilled the air jump, so a single ordinary wall could be climbed without limit (round 1, F2), against the 2026-10-23 rule "no single-wall scaling". Options: (a) keep the refill and scope the rule to "with double jump off"; (b) refill on a slide only when it is a different wall face than the one last jumped from (chimneys still refill); (c) no refill on a slide at all, only on landing, cling or rope. The prompt is written for (b). Claude recommends (b): it keeps the operator's rule and the genre-standard chimney refill.
   Reviewer recommends: (b) — it keeps the decision's stated intent (sound single-wall gates) and the genre-standard refill in chimneys (round 1, F2).
   **Settled by the operator (2026-10-23): (b).**

**For Phase 2 (not built here):**
- Falling out of the map is a teleport; add a one-tick reset signal together with its runtime consumer so interpolation snaps.
- Re-run the single-wall and gap gate tests after any tuning.

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md.
3. Write `memory/logs/phases/phase1-log.md`.
4. Update `MEMORY.md` current state.
5. If applicable: update `CLAUDE.md`, `Documents/GDD/architecture.md`, `memory/lessons-learned.md`, `memory/decisions.md`, or GDD files with any convention/rule the phase established.
6. **Deferred items.** If this phase left known cleanup, dead-code, or non-blocking observability gaps that weren't in scope, file each as an entry in `Documents/Operations/Deferred.md` (Source = this phase). Conversely, if any existing Deferred entry's trigger fired during this phase, delete that entry in the same commit. See CLAUDE.md → Log Discipline.
7. Commit all phase work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: phase1-movement-sim
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:12:00+09:00
blocking: 2
recommendations: 2
---

# Review round 1: `Documents/Phases/phase1-movement-sim.md`

## What I could run, what I verified, what I could not

**Could run:** file reads of the connected repo (staged from the device; the device shell was still installing and unavailable this round), plus Python in my own cloud container for arithmetic and small tick simulations. **Could not run:** Unity, either test suite, a build, or anything on the user's machine.

**Simulation model** (stated so the Author can reproduce or refute it): 60 Hz; trapezoid integration with any impulse (jump, cut, push) applied *before* gravity, so `vOld` is the post-impulse velocity (see F3); rising gravity while `vOld > 0`; tick-order and counter placement as Steps 6–9 state; air rates ×0.75; hitbox 0.75 × 1.5. Scripts: gap, single-wall, single-wall + double jump, chimney widths.

**Verified (recomputed):**
- `BaseGravity = 7 / 0.1225 = 57.142857`; `JumpVelocity = √400 = 20.0`; apex at exactly 21 ticks; fall gravity 100; `MinHopVelocity = √142.857 = 11.952`; accelerations 60/90/120 and 45/67.5/90; push 9.
- Gravity 2: `JumpVelocity = √800 = 28.284`, apex 3.5, time to apex 14.85 ticks ("~15") ✓.
- Worst case per tick: jump 6 at gravity 2 → √(2·114.286·6) = 37.03 tiles/s = 0.617 tile/tick ✓; max fall 24 → 0.4; push 18 → 0.3.
- Run test: +1/tick to 6 in 6 ticks; release 6 → 0 in 4 ticks; reverse 3 ticks to 0, then 6 more to −6 ✓ (given F5).
- One-tick tap apex: sampled max 1.2484 (within ±0.01 of 1.25) ✓.
- Gap gate: jump on the last coyote-valid tick clears ≈5.3 tiles (5.1 at the walk-off tick, 5.4 if one more tick were allowed); 5 clears / 6 doesn't ✓.
- Ledge gates 3/4 and double-jump 6/8 follow from apex 3.5 and 7.0 ✓. Corridor 2/1 follows from height 1.5 ✓.
- Float precision: 0.375 / 0.75 / 1.5 are exact binary fractions; `ContactEpsilon` 1e-4 is ≥ 3× the float ulp at x = 500 (3.05e-5) ✓.
- Code citations: `FixedStepAccumulator.TicksPerSecond = 60` ✓; `SimPurityTests.Forbidden` contains `\bRandom\w*` (so the "no `Random*` identifiers" rule is necessary) ✓; `ScannerCatchesForbiddenUseButIgnoresComments` exists ✓; sim asmdef `noEngineReferences: true`, no references ✓.
- Phase 2 seam: every Phase 1 API named in Phase 2 § Depends on exists in this prompt's manifest. Phase 2 adds `Teleported`, matching this prompt's "For Phase 2" note ✓.

**Accepted on trust:** that `GC.GetAllocatedBytesForCurrentThread` behaves as the doc assumes on Unity's Mono (the doc already guards it with a sanity probe).

## Findings

### F1. Single-wall test asserts a wall contact that the doc's own rules prevent
- **Type:** blocking · **Category:** accuracy · **Severity:** high
- **Location:** Step 9, Tests, "no single-wall scaling"; Completion Criteria, gate line.
- **Defect:** The script presses jump every tick. Wall jump (priority 4) fires whenever the tick-start gap is ≤ `WallJumpReach` (0.125). The player comes back toward the wall at ≤ 6 tiles/s, which is ≤ 0.1 tile per tick, and 0.1 < 0.125. So the first tick-start gap ≤ 0.125 always falls in (0.025, 0.125]. The wall jump therefore fires one or more ticks *before* contact (gap ≤ `ContactEpsilon`). It never re-contacts and never slides. The required assert "each cycle really re-contacted the wall" can't pass as written, unless "contact" quietly means "within reach".
- **Number:** "Expected loss ≈ 0.2 tile per cycle" doesn't match my model. Press-every-tick loses 0.378 / 0.378 / 0.578 / 0.378 per cycle (fire gaps 0.05 / 0.1). Pressing only once touching and falling loses 0.578 per cycle. Either way the ≥ 0.1 gate holds with margin, but the stated expectation is off by about 2×.
- **Fix:** Define the precondition as "the jump fired with the wall within `WallJumpReach`", or script the jump only on contact and assert WallSlide. Replace ≈ 0.2 with the implementation's measured value.

### F2. With DoubleJump + WallJump, a single ordinary wall is climbable without limit
- **Type:** blocking · **Category:** design · **Severity:** high
- **Location:** Step 9 (air jumps reset on slide entry); Step 12 (ability matrix); Notes to GDD § systems.md "no single-wall scaling on ordinary walls at defaults (deliberate)"; decisions.md row 2026-10-23 (rationale: "ability gates sound for student-built levels").
- **Defect:** Slide entry refills `AirJumpsLeft`. One loop: slide → wall jump (+3.5 apex, pushed 1.8 tiles away by the lock) → air jump near the apex → drift back → re-contact while falling → slide (refill). At default settings in my model this gains **+6.875 tiles per cycle** (starts at 0, 6.875, 13.75, 20.625). So the rule the doc writes into the GDD is false for any player holding both abilities. A student's "tall plain wall = needs wall climb" gate leaks as soon as double jump and wall jump are both unlocked. No test covers it: the single-wall test runs with DoubleJump off, and the ability matrix has no DoubleJump + WallJump row.
- **Fix:** This is the operator's call, and the doc should put it to them. Options:
  - (a) Keep the mechanics and scope the rule to "with double jump off", pinning the scaling with a test.
  - (b) Refill air jumps on slide entry only when the wall differs from the last wall jumped from, i.e. a different face or side. Chimneys still work.
  - (c) No refill on slide; refill only on landing, cling or rope.
- **Reviewer suggests:** (b). It keeps the decision's stated intent (sound single-wall gates) and the genre-standard refill in chimneys. Whichever option is chosen, add the DoubleJump + WallJump row to `AbilityMatrixTests` and a single-wall test with DoubleJump on.

### F3. Trapezoid integration doesn't say which velocity is `vOld` on an impulse tick
- **Type:** non-blocking · **Category:** accuracy · **Severity:** med
- **Location:** Conventions, "Integration (both axes)"; Steps 6, 7, 9, 11.
- **Defect:** "Compute the new velocity, then move by (vOld + vNew)/2·dt" leaves the jump tick ambiguous. If `vOld` is the stored pre-tick vy (0 when standing), the jump tick moves (0 + 19.05)/2·dt instead of (20 + 19.05)/2·dt, and the apex lands at **3.333**. That fails the pinned 3.5 ± 0.01, the 7.0 air-jump apex, the 1.25 tap and the gate breakpoints. "This keeps jump height exact" is true only if impulses replace `vOld`.
- **Fix:** One sentence. Impulses (jump, air/wall/rope jump, the cut, wall push, drop, landing/ceiling zeroing) set the velocity before integration; `vOld` is that post-impulse value, and gravity/acceleration produce `vNew`.

### F4. "6 ticks" means different windows for coyote and buffer, and two tests count from undefined ticks
- **Type:** non-blocking · **Category:** accuracy · **Severity:** med
- **Location:** Step 6 tick order item 1; Step 7 Buffer and Tests; Step 9 wall coyote test; Step 12 Gaps.
- **Defect:**
  - **Coyote:** decremented at tick order item 1, before use. Walk-off on tick W sets 6. Ticks W+1…W+5 see 5…1, and W+6 sees 0. That is **5 airborne ticks (0.083 s)**, not 0.1 s.
  - **Buffer:** not in item 1's list. Step 7 decrements it *after* the availability check, so it fires up to 6 ticks after the press.
  - **Wall coyote:** decremented like coyote, so also 5 ticks.
  - **Test wording:** "the 6th tick after the last grounded tick" and "6 ticks before the landing tick" pass only under particular readings. "Last grounded tick" has to be W−1, and "landing tick" has to be the first tick that *starts* grounded (the buffer fires the tick after the landing move). Step 12's "press on the 6th coyote tick" names a tick that doesn't exist under Step 6/7: only 5 airborne ticks have `CoyoteTicks > 0`. An implementer reading it as W+6 gets a red gate test.
- **Fix:** Pick one convention for every window, e.g. "a counter of N accepts the action on each of the next N ticks, decremented after its check". Define "landing tick" and "walk-off tick" once in Conventions, and restate the boundary tests in those terms.

### F5. Does the tick-start probe reclassify `Contact` before velocity is computed?
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 tick order items 2 and 5; `CreateState` (returns Air); RunAndFallTests first bullet.
- **Defect:** `CreateState` returns Air. If item 5 uses the stored contact, tick 1 of "from rest holding right" uses the **air** rate: vx 0.75, not 1, and the test fails. The same question decides whether a standing player's ground jump uses the probe or the stored contact.
- **Fix:** State that item 2's probe updates the standing contact (Ground/OneWayTop/Air) used by items 3–5.

### F6. `BoxIsFree` is defined without the bottom bound that Step 5 relies on
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 3 Probes (`BoxIsFree`: "inside the side and top bounds"); Step 5 ("feet below row 0 … are not free").
- **Defect:** `TileGrid.Get` returns None below row 0, so Step 3's `BoxIsFree` accepts a box below the map. Step 5 says the same call rejects it, and its "start below the map → moved inside" test depends on that. `BoxIsFree` is also used for ceiling correction, the rope snap and the ledge pop, where a bottom bound has a different effect.
- **Fix:** Either `StartPlacement.Find` adds the `y ≥ 0` check itself, or `BoxIsFree` includes the bottom and the other callers are checked.

### F7. Open question 1 says the gap breakpoint doesn't depend on the internals, but it does
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Notes to GDD, Open questions 1.
- **Defect:** Air time comes from `TargetTimeToApex` (21 rising ticks) and `FallGravityFactor` (the descent), and the coyote window adds horizontal distance (about 0.1 tile per coyote tick: 5.1 → 5.3 in my model). Max fall does matter: an uncapped landing speed would be about 25.6 tiles/s. A Phase 2 retune of the internals moves the gate, and the operator would be told the lever is only run speed or jump height.
- **Fix:** "Set by run speed, jump height, max fall, and the internals time-to-apex, fall-gravity factor and coyote ticks."

### F8. A rope ending in a Solid/Climbable cell can't reach the "stand on top" branch
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 11 Top.
- **Defect:** Rope climbing is "swept with OneWay ignored and solid still blocking". A Solid/Climbable cell in row `round(Top) − 1` under the snapped hitbox blocks the head before the feet reach `Top`. The hitbox centred on the rope covers only the rope's column, so that cell is directly overhead. Only the OneWay case can work. `AsciiLevel` also has no character for solid + rope, so the branch can't be tested.
- **Fix:** Limit the standable top to OneWay, or define what a rope inside a solid cell means.

### F9. The allocation-test fallback is circular with Phase 2
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 13 fallback ("Phase 2's PlayMode allocation guard … carry the guarantee"); `phase2-movement-runtime.md` Step 14.
- **Defect:** Phase 2's guard uses the same `GC.GetAllocatedBytesForCurrentThread` probe, and its fallback is "follow Phase 1 Step 13". If the probe is unsupported, neither phase carries the guarantee and only code review is left. The doc presents that as two layers.
- **Fix:** Name a different fallback, e.g. Unity Test Framework's `Is.Not.AllocatingGCMemory()` constraint, which uses the profiler's GC.Alloc recorder. I couldn't verify it works in batch mode on 6000.0. Author to run. Or state plainly that code review alone carries it.

### F10. No gate test proves wall jump opens a chimney
- **Type:** non-blocking · **Category:** design · **Severity:** med
- **Location:** Step 12 Gate integrity.
- **Defect:** The wall-jump gate tests only the negative case (no single-wall scaling). The positive gate that students will build with is missing: a vertical shaft climbable *only* with WallJump. In my model, at defaults, alternating wall jumps climb shafts up to 5 tiles wide (gain ≈ 0.33 / 1.5 / 2.2 / 3.2 / 3.3 / 1.6 tiles per jump at widths 1 / 1.5 / 2 / 3 / 4 / 5), and a 6-wide shaft loses height. A Phase 2 retune of push or lock could silently break chimneys.
- **Fix:** Add "a 3-wide, 12-tall shaft is climbed with WallJump and not without; a 6-wide one is not". State the breakpoint the implementation measures.

### F11. Cling entry while rising cancels jumps along climbable walls
- **Type:** non-blocking · **Category:** design · **Severity:** med
- **Location:** Step 10 Cling entry ("airborne and pressing toward a touching climbable wall").
- **Defect:** Unlike the slide (vy ≤ 0), cling entry has no velocity condition and sets velocity to 0. Take a player with WallClimb who jumps at a 3-tile ledge whose face is climbable while holding toward it. They stop dead on the first contact mid-rise and must climb and ledge-pop. The same jump clears the ledge with WallClimb off. Unlocking an ability makes a basic move worse, and on a climbable face the Step 12 ledge gate passes or fails depending on WallClimb.
- **Fix:** Require vy ≤ 0 for air cling entry (like the slide), or Up held. If the current rule is intended, pin it with a test and list it among the (proposal) items for the play-check.

### F12. The static-field guard also bans every lambda in the sim, with a cryptic failure
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 1.
- **Defect:** Any lambda anywhere in the sim makes the compiler emit `<>c.<>9__N_M`, a static, non-readonly delegate cache. The guard as specified flags it, so it fails on e.g. an `Array.Sort` comparison lambda in play-start code. That is stricter than the stated convention (closures banned only in `Step` and below), and the message will read `<>c.<>9__0_0`.
- **Fix:** Say the ban is intended (no lambdas in the sim) and make the failure message explain compiler-generated names. Or skip `[CompilerGenerated]` types deliberately.

### F13. Notes to GDD → Operations repeats edits that are already on disk
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Notes to GDD § Operations, first bullet.
- **Defect:** The working-tree `Roadmap.md` already has the `movement-sim` / `movement-runtime` split and the "Pulled forward (2026-10-23)" line, and `decisions.md` already has the four 2026-10-23 rows. Both are uncommitted. Done again at completion, they produce duplicates.
- **Fix:** Mark them as already applied and leave only the §0 row, Changelog and version bump.

### F14. The grip point at exactly `T` sits outside the climbable cell under a half-open reading
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 10 Grip and Top.
- **Defect:** Climbing "stops with the grip at `T`". At grip height `Y + 0.75 = T`, the cell at that height is row `T`, the first row *above* the run. The grip test (the cell beside the hitbox at grip height is Climbable) then fails at the rest position, and the next tick would drop the cling.
- **Fix:** Define the grip cell as row `floor(gripY − ContactEpsilon)`, top edge inclusive, and add "hold at `T` for 60 ticks stays WallCling" to the tests.

### F15. Open question 1 (gap 5 / 6 until the play-check)
- **Type:** recommendation · **Category:** design
- **Reviewer recommends:** yes, keep it. It's a starting value, the measured best case (≈ 5.3) has real margin on both sides, and Phase 2's play-check is where it should move. Note the full lever list from F7.

### F16. Open question 2 (no `Run` flag)
- **Type:** recommendation · **Category:** design
- **Reviewer recommends:** no flag, and entities.md says so. A flag nothing gates would be a toggle that does nothing, which CLAUDE.md rule 8 forbids in the student-facing Player.

## Reviewer's view

I would not build this as written yet. F1 specifies a gate test that can't pass, and F2 writes into the GDD a rule ("no single-wall scaling") that is false once a player has double jump and wall jump. F2 is the operator's decision to make. Everything else is a definitional tightening the Author can fold in one pass: F3 and F4 matter most, because the pinned numbers depend on them. F10 and F11 are the design gaps I'd most want covered before Phase 2 tuning starts.

---

## Review round 2

---
slug: phase1-movement-sim
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:31:00+09:00
blocking: 1
recommendations: 0
---

# Review round 2 (delta): `Documents/Phases/phase1-movement-sim.md`

## What I could run, what I verified, what I could not

**Could run:** file reads of the connected repo (staged from the device) and Python in my own cloud container. **Could not run:** Unity, either suite, a build, anything on the user's machine. The simulation model is the same as round 1, with counters decremented at the new item 9.

**Delta read:** `log.md` round-1 dispositions; Conventions; Steps 1, 3, 5–13; Completion Criteria; Notes to GDD. These are the sections the dispositions name. I didn't re-read Steps 2, 4, 14, 15 or Post-Phase Cleanup in full: the log reports no change there, and the section headings and sizes match.

**Verified in the fold:**
- **F1:** the Step 9 precondition is now "fired with the wall within `WallJumpReach`"; ≈0.2 is gone. Closed.
- **F3:** Conventions states impulse-first integration and names the 3.333 failure. Closed.
- **F4:** I re-traced the new timer convention (set on T, checked T+1…T+N, decremented at item 9 unless set this tick):
  - coyote: W+6 yes, W+7 no ✓
  - buffer: P+1…P+6, fires on L+1 iff P ≥ L−5 ✓
  - lock: J+1…J+12 locked, J+13 free ✓ (J itself is also locked; harmless, since the push sets vx)
  - wall coyote: E+6 / E+7 ✓
  - gap gate on W+6: 5.4 in my model ✓

  Closed.
- **F5, F6, F8, F9, F12, F13, F14:** folded as dispositioned; I checked each text. Closed.
- **F7:** Open question 1 lists all six levers. Closed.
- **F10:** chimney gate is in Step 12 and the Completion Criteria. Closed.
- **F11:** air cling needs vy ≤ 0 or Up. I re-checked the 3-tile climbable ledge: rising past the face has vy > 0, so there's no cling, and the box clears row 3 before the apex. Closed.
- **F2, option (b):** I re-ran my round-1 exploit loop with (b): slide, wall jump, air jump at the apex, back to a slide on the same face with no refill. Starts go 0 → 6.875 → 6.297 → 5.719. The first cycle spends the air jump it started with, and after that height falls 0.578 per cycle. So (b) bounds single-wall scaling as intended. Rule closed, but see F17, F18 and F20 on its spec and test.
- **F15, F16:** recorded beside the open questions. ✓

## Findings

### F18. The DoubleJump-on single-wall test fails on its first pair and never exercises rule (b)
- **Type:** blocking · **Category:** accuracy · **Severity:** high
- **Location:** Step 9 Tests, "no single-wall scaling … run twice"; Completion Criteria gate line ("with DoubleJump off and on"); Step 12 ability-matrix row `+DoubleJump +WallJump`.
- **Defect:** This is a seam from the round-1 fold.
  - **The first pair fails.** In the DoubleJump-on run the player starts with the air jump full, so the first cycle's apex press adds 3.5. My model, with press every tick plus a press at each apex, gives wall-jump starts **0 → 6.867 → 6.489 → 5.912**. "Each starting ≥ 0.1 tile lower than the previous one" fails on jump 1 → 2.
  - **It never tests (b).** The F1 fix means a press-every-tick script fires every wall jump *before* contact, so the player never slides, and a slide is the only thing that refills. This run gives the same result with or without (b), so it can't catch a regression of F2. The step's own note "the air jump is spent every cycle" assumes a refill this script never triggers.
  - **The matrix row has no script.** The `+DoubleJump +WallJump` row says "the ordinary wall still can't be scaled" but doesn't name the inputs, so it can be vacuous in the same way.
- **Fix:** For the DoubleJump-on run, and the matrix row, use the exploit script:
  - jump only from an active WallSlide (wait for contact while falling), and air jump at each apex;
  - assert that the first cycle's gain is bounded (≈ +6.9 in my model, i.e. no more than one air jump's worth);
  - assert that every later start is ≥ 0.1 lower and that each later slide did **not** refill `AirJumpsLeft`.

  Without (b), the same script gains ≈ +6.9 per cycle in my model, so the test is a real guard.

### F17. A wall jump from wall coyote has no face to record
- **Type:** non-blocking · **Category:** accuracy · **Severity:** med
- **Location:** Step 6 `PlayerState` fields; Step 9 wall jump ("`LastWallJumpSide`/`LastWallJumpFace` = this wall"); wall coyote ("with the side remembered").
- **Defect:** A wall jump can fire from `WallCoyoteTicks > 0` with no wall in reach. `PlayerState` keeps `WallCoyoteSide` but no face. So `LastWallJumpFace` for a coyote wall jump is undefined: a stale value, 0, or a probe that finds nothing. If it ends up not matching, the next slide on that same wall counts as "a different wall" and refills. That reopens F2 by one detour per cycle: slide, step off the wall, coyote wall jump, air jump, back to a slide that refills.
- **Fix:** Store `WallCoyoteFace` alongside `WallCoyoteSide` when wall coyote starts, and record it on a coyote wall jump. Add one test: the coyote wall jump, then a slide on the same face, doesn't refill.

### F19. The tick-start probe could stand up a player who is rising through a one-way
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 tick order item 2 ("the support probe updates the standing contact").
- **Defect:** Read literally, the probe turns any Air state whose feet are within `ContactEpsilon` of an integer row over a OneWay cell into OneWayTop, and standing sets vy = 0. A rising jump whose feet happen to sit on a one-way's top at a tick boundary would stop dead on the platform. That contradicts Step 7/8 ("lands on it on the way down only"). From an integer floor at defaults the trajectory n/3 − n²/126 never hits an integer while rising, but other settings or start heights can.
- **Fix:** Promote Air → standing at item 2 only when vy ≤ 0, matching the Landing rule.

### F20. Rule (b) appears as a settled rule, though it is Claude's choice
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Notes to GDD § systems.md "A wall slide refills only on a different wall face…"; § Operations "add the F2 rule".
- **Defect:** The log says (b) was "flagged to the operator as Claude's choice, overridable", but the Notes list it among the operator rules without **(proposal)**. It isn't an open question either, so the completion report won't put it in front of the operator. Approving it is the operator's call, since it refines their 2026-10-23 decision on resetting air jumps.
- **Fix:** Mark it **(proposal)**, or add it as Open question 3 with the (a)/(b)/(c) options, "Claude recommends (b)" and "Reviewer recommends: (b) (round 1, F2)".

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F18}. Both earlier blockers are closed. F18 is new, introduced where the F1 and F2 fixes meet. No reasoned rejection this round. None of my round-1 claims was shown wrong. The Author quoted my modelled numbers as mine, not as measured, which is correct.

## Reviewer's view

Close. F18 is the one thing stopping approval: the DoubleJump-on gate test fails on its first pair and wouldn't catch a regression of rule (b). F17 is the one gap left in (b)'s spec. F19 and F20 are one-line tightenings. Once those are folded, I expect to approve next round.

---

## Review round 3

---
slug: phase1-movement-sim
round: 3
verdict: approved
reviewer: claude-cowork
updated: 2026-10-23T21:33:00+09:00
blocking: 0
recommendations: 0
---

# Review round 3 (delta): `Documents/Phases/phase1-movement-sim.md`

## What I could run, what I verified, what I could not

**Could run:** file reads of the connected repo (staged from the device) and Python in my own cloud container. **Could not run:** Unity, either suite, a build, anything on the user's machine. Nothing in this document has been built or tested; every number below is from my model or the document's own arithmetic.

**Delta read:** `log.md` round-2 dispositions; Step 6 `PlayerState` fields and tick order item 2; Step 9 (wall-coyote grant, wall-jump recording, Scripts A/B/C); Step 12 matrix row; Completion Criteria gate line; Notes to GDD (proposal list, Operations, Open question 3).

**Verified in the fold:**
- **F17:** `WallCoyoteFace` is in `PlayerState` and set together with the side when wall coyote starts. A coyote wall jump records `WallCoyoteSide`/`WallCoyoteFace`. Script C asserts no refill on the next same-face slide. Closed.
- **F18:** Script B jumps only from an active WallSlide and asserts that precondition. Its bounds hold in my exploit-loop model with rule (b): first-cycle gain +6.875 ≤ 7.0, later starts −0.578 each (≥ 0.1), no refill on same-face slides. Without (b) the same loop gains +6.875 per cycle and fails the "later starts lower" assert, so it is a real guard. The matrix row reuses Script B. Closed.
- **F19:** item 2 promotes Air to standing only when vy ≤ 0. A fresh `CreateState` on the floor has vy = 0, so F5's tick-1 behaviour holds. Closed.
- **F20:** rule (b) is marked **(proposal)** and is Open question 3 with (a)/(b)/(c), Claude's and the Reviewer's recommendations beside it. The decisions.md addition waits for the operator. Closed.

## Findings

### F21. Script B quotes the wrong model numbers
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 9 Script B ("Reviewer's round-2 model: +6.867"; "model: 6.867 → 6.489 → 5.912").
- **Defect:** These are the numbers from my round-2 run of the *press-every-tick plus apex air jump* script, which F18 showed can't exercise rule (b). My exploit-loop model, which matches Script B, gives starts **0 → 6.875 → 6.297 → 5.719**: +6.875, then −0.578 per cycle. Both sets satisfy Script B's assertions (≤ 7.0; ≥ 0.1 lower), so no test changes. But a future reader comparing measured prints with the quoted model would see an unexplained 0.2-tile gap per cycle.
- **Fix:** Replace the quoted numbers with 6.875 → 6.297 → 5.719. If Script A wants a DoubleJump-on reference, keep the press-every-tick numbers there, labelled as such.

The late-pushback test found nothing else to file. Asked "what do you think?", my answer contains nothing that isn't already in round 1–3 files.

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F18} → round 3 {}. Each fix was accepted with a re-derivation. No reasoned rejection, and none of my claims was shown wrong. F21 is a slip in quoting my own earlier numbers, corrected above.

## Approval and next custody

Approved as a proposal. The Author folds F21 on close, records nothing further (all recommendations are already recorded), appends this review and the closing dispositions, sets `status: done` and archives the handshake.

Approval is a verdict on the document only. Nothing is implemented and no test has run.

Conditions this approval rests on and could not verify:
- The scenario breakpoints and model numbers are from my Python model, not the sim. The tests print the implementation's measured values.
- `GC.GetAllocatedBytesForCurrentThread` and `Is.Not.AllocatingGCMemory()` are unproven on 6000.0 batch mode. Step 13 proves them or falls back to code review.
- Open question 3, the air-jump refill rule, is the operator's decision. The prompt is written for (b).

## Reviewer's view

I would build this document as written, with F21 folded. The two round-1 blockers (F1: a test that couldn't pass; F2: single-wall scaling with double jump) and the round-2 seam (F18) are resolved and guarded by tests that fail without the rule. The pinned numbers now rest on stated conventions (F3, F4, F5, F19). The one open design choice, rule (b), is correctly put to the operator as Open question 3.
