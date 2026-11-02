# Phase 4: Moving Traps

## Overview

**Where things stand:** Traps can vanish or pop out, but nothing in a level moves. A trap set on an exit or a checkpoint does nothing and shows a warning, and there are no falling ceilings, sliding floors or crushers.

**What this phase changes:**
- Two new trap actions: Move slides a trap to a chosen spot at a set speed, and Fall drops it until it lands on the ground.
- The player can ride a moving floor and jump off it. A moving block pushes the player, and squashes them only when there is truly no room, after a small sideways shove.
- An exit can run away, fall, vanish or appear, using the same trap settings.
- While building, the student sees where a moving trap will end up or land and can drag that spot. The trap's description says how far it goes and how long it takes.

**How you'll know it worked:** The operator rides, dodges, gets crushed by and outruns each moving trap in the test level, then chases a runaway exit, and every death looks fair.

**Not in this phase:** moving and crumbling platforms that loop or come back (a later milestone), and traps on checkpoints.

---

## Source
- **GDD sections (the spec; designed in v0.11, current GDD v0.12):**
  - `systems.md` § Traps: Actions, Appearing on the player, Moving solids, the **Move and Fall** bullets, Readable in the Scene view; § Combat & Damage (Hazards, Safe ground); § Death and respawn
  - `entities.md` § Trap (Offset, Speed, Action values, Fixed line), § Placeable elements (Exit: "a Trap on the same object makes it run away")
  - `architecture.md` § System Map, § Time Model, § Data Flow
  - `conventions.md` § Testing & Playtesting
- **Decisions:** `decisions.md`, the row dated 2026-11-02 that opens **Moving traps (Move, Fall)**. Design-panel findings from the same day are folded in below: precedence order, swept hazard, a landing tick with no coyote hiccup, rope lockout after a push, fixed per-tick steps with exact snapping, results that don't depend on trap order, travel time and previews.
- **GDD version:** v0.12 (v0.12 is the class-gallery design; it doesn't touch traps).
- **Depends on:** Phase 3 `death-and-respawn` (v0.10). Its code is the contract:
  - `LevelStepper` (tick order, `TrapState`, `Fire`, `ResolveAppeared`, `PushOut`, `RebuildTrapCells`, `Inside`, `OutsideEveryTrap`)
  - `TileGrid` trap-cell layer, `TileCollision`, `TrapSpec`, `TrapActionKind`, `LevelRules`, `Box`
  - `Trap`, `TrapAction`, `TrapLook`, `LevelCollector`, `HelpTexts`, `PlayerWarnings`, `Player`, `PlayerSession`
  - `TrapEditor`, `TrapAuthoring`, `TrapGhostGizmo`
  - the test helpers `LevelScenario`, `TestLevel`, `AsciiLevel`, and the trap bench `TrapTestSceneBuilder`
  - If a name differs from this prompt, the code wins; note it in the phase log.

## File Manifest

Package root `P/` = `Packages/com.orcapafea.daedalus/`. Every new file is committed with its `.meta`.

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `P/Runtime/Sim/TrapActionKind.cs` | `Move = 2`, `Fall = 3` |
| MODIFY | `P/Runtime/Sim/LevelRules.cs` | Move offset/speed defaults and ranges; `CrushNudge` |
| MODIFY | `P/Runtime/Sim/TrapSpec.cs` | Offset, speed, exit link, `StartBox` |
| NEW | `P/Runtime/Sim/TrapMotion.cs` | Pure Move/Fall kinematics, painted landing, travel ticks (shared with the editor's preview) |
| NEW | `P/Runtime/Sim/MovingBoxes.cs` | The moving-box layer: fixed-capacity Solid/Hazard boxes between cells |
| MODIFY | `P/Runtime/Sim/TileGrid.cs` | Carries the `MovingBoxes` layer |
| MODIFY | `P/Runtime/Sim/TileCollision.cs` | Every query also sees moving boxes |
| MODIFY | `P/Runtime/Sim/Box.cs` | `SweptOverlaps` (moving box vs a still box) |
| NEW | `P/Runtime/Sim/BoxPush.cs` | Carry, leading-face push, crush and nudge, plus the shared push effect (rope release + lockout) |
| MODIFY | `P/Runtime/Sim/LevelStepper.cs` | Trap motion state, the "traps move" phase, riders, pushes, exit traps, current boxes everywhere |
| MODIFY | `P/Runtime/TrapAction.cs` | `Move = 2`, `Fall = 3` |
| MODIFY | `P/Runtime/Trap.cs` | `offset`, `speed` fields; tooltips; exit-aware gizmos and `Reset` |
| MODIFY | `P/Runtime/TrapLook.cs` | Clamped offset/speed, end box, exit box, on-exit / on-checkpoint, `ToSpec`, Play look for exits, Fall landing query |
| MODIFY | `P/Runtime/LevelCollector.cs` | Ignore Traps on a Checkpoint or a disabled Exit; end boxes for the map include |
| MODIFY | `P/Runtime/Player.cs` | Exit links, map include of end boxes, per-frame interpolated trap transforms |
| MODIFY | `P/Runtime/PlayerSession.cs` | Previous/current trap offsets, snap on respawn and restart |
| MODIFY | `P/Runtime/HelpTexts.cs` | Move/Fall/Exit stories with distance and travel time; "Falls out of the map" |
| MODIFY | `P/Runtime/PlayerWarnings.cs` | `TrapOnCheckpoint` (renamed, reworded); `TrapOnDisabledExit`; `TrapEndInGround` |
| MODIFY | `P/Editor/TrapEditor.cs` | Offset/Speed only for Move; Size/Tile hidden on an Exit; Move-end handle and arrow; Fall landing outline; new warnings |
| MODIFY | `P/Editor/TrapAuthoring.cs` | Look shown for "not Appear"; exit renderer untouched; exit-aware Fit zone |
| MODIFY | `P/Tests/EditMode/Level/LevelRulesTests.cs` | Pins the new values |
| NEW | `P/Tests/EditMode/Level/TrapMotionTests.cs` | Kinematics, snapping, landing, travel ticks |
| NEW | `P/Tests/EditMode/Movement/MovingBoxCollisionTests.cs` | Each collision query against boxes |
| NEW | `P/Tests/EditMode/Level/MovingTrapTests.cs` | Move/Fall in the level step: start, arrive, land, gone, reset, freeze, current boxes |
| NEW | `P/Tests/EditMode/Level/TrapRideTests.cs` | Riding, carrying, pull-away coyote, landing tick |
| MODIFY | `P/Tests/EditMode/Level/TrapPushTests.cs` | Leading-face push, squeeze, crush ± nudge, rising into a ceiling, rope lockout (appear and move) |
| MODIFY | `P/Tests/EditMode/Level/HazardTests.cs` | Moving Hazard swept check |
| NEW | `P/Tests/EditMode/Level/ExitTrapTests.cs` | Every action on an Exit |
| MODIFY | `P/Tests/EditMode/Level/TrapTests.cs`, `SafeGroundTests.cs` | Current boxes for Touch and safe ground |
| MODIFY | `P/Tests/EditMode/Level/LevelScenario.cs` | `.Trap(...)` offset/speed; `.ExitTrap(...)` |
| MODIFY | `P/Tests/EditMode/Movement/StepAllocationTests.cs` | Move/carry/push/fall/land/gone branches |
| MODIFY | `P/Tests/EditMode/Movement/TileGridTests.cs` | Layer attached; empty layer changes nothing |
| MODIFY | `P/Tests/EditMode/Runtime/TrapLayoutTests.cs` | 8 paths, enum values, defaults, ranges, ToSpec, exit/checkpoint, old-data load |
| MODIFY | `P/Tests/EditMode/Runtime/TrapAuthoringTests.cs` | Move/Fall shown, Appear hidden, exit renderer untouched |
| MODIFY | `P/Tests/EditMode/Runtime/HelpTextsTests.cs` | New story cases |
| MODIFY | `P/Tests/EditMode/Runtime/PlayerSessionTests.cs` | Trap interpolation and snap |
| MODIFY | `P/Tests/PlayMode/Movement/TestLevel.cs` | `AddTrap` offset/speed; `AddExitTrap` |
| MODIFY | `P/Tests/PlayMode/Traps/TrapPlayTests.cs` | Moving-trap scenarios with screenshots |
| MODIFY | `P/Tests/PlayMode/Traps/LevelWarningTests.cs` | Checkpoint warning, end-in-ground warning, exit trap no warning |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerAllocationTests.cs` | A moving trap and a rider in the window |
| MODIFY | `Assets/Dev/Editor/TrapTestSceneBuilder.cs` (+ generated `Assets/Dev/TrapTest/…`) | Moving-trap sections, Runaway Exit, self-check |

Doc edits are listed in `## Notes to GDD`.

---

## Conventions for every step

- **Namespaces, purity and allocation:** as in Phase 3.
  - The sim has no `UnityEngine`, clock, random, LINQ or static mutable state.
  - `LevelStepper.Step` and everything it calls allocate nothing; arrays are sized in the constructor.
  - `Player.RunFrame` and `Render` allocate nothing and call no `GetComponent` / `Find`.
- **One source of truth:**
  - Every new default and range lives in `LevelRules` (or comes from `MovementInternals` / `DerivedMovement`).
  - Components, the editor, help texts and the bench read them; entities.md mirrors them.
- **Editor tools contain no gameplay logic:**
  - The Fall landing preview, travel times and end boxes come from `TrapMotion` (sim) through `TrapLook` / `HelpTexts` (runtime).
  - Every authoring write goes through `Undo`.
- **Serialized data:**
  - `TrapAction` values are pinned (Vanish 0, Appear 1, Move 2, Fall 3) and never renumbered.
  - New fields `offset`, `speed` take their defaults from field initializers.
  - `dataVersion` stays 1: no saved value changes meaning, and saved actions are only 0/1.
- **Fixed per-tick steps:**
  - Move's position is computed from its tick count, not accumulated.
  - Move snaps exactly to its end, and Fall to its landing row.
  - At rest, a trap is whole cells again.
- **Order independence:**
  - Every rule that combines several traps in one tick (new boxes, pushes, the carrier choice, cell rebuilds) gives the same result with the trap array reversed.
  - Tests assert it.
- **Tests:**
  - Assert through the real entry points; assert every precondition; breakpoint tests assert both sides.
  - Every new guard is seen failing once on a planted change, then reverted.
  - Screenshots: `<unity version>-traps-<name>.png`.
- **During a step:** `python Tools/unity_tests.py --editor 6000.6 --platform <mode>`; both editors at Step 18. Close the dev project first.

---

## Steps

### Step 1: Action values and Move numbers
- **MODIFY `P/Runtime/Sim/TrapActionKind.cs`**:
    - `Move = 2` (doc: "Present; slides in a straight line to its end at constant speed, then rests there").
    - `Fall = 3` (doc: "Present; falls under the player's rise gravity until it lands on painted Solid/Climbable or leaves the map").
    - Drop "reserved".
- **MODIFY `P/Runtime/TrapAction.cs`**: `Move = 2`, `Fall = 3`; drop "reserved".
- **MODIFY `P/Runtime/Sim/LevelRules.cs`** (all **(proposal)** until Step 19):
    - Offset: `DefaultMoveOffsetX = 0`, `DefaultMoveOffsetY = -3`, `MinMoveOffset = -30`, `MaxMoveOffset = 30` (whole tiles, per axis).
    - Speed: `DefaultMoveSpeed = 8f`, `MinMoveSpeed = 1f`, `MaxMoveSpeed = 30f` (tiles/s).
    - `CrushNudge = MovementInternals.CeilingCornerCorrection` (0.25): "tiles a blocked push may shift the player at right angles before it crushes."
- **Tests (`LevelRulesTests.ValuesArePinned`):** each new value pinned; `CrushNudge == CeilingCornerCorrection`; `MaxMoveSpeed / 60 == 0.5` (the largest per-tick step, which the no-skip tests rely on).
- **Verify:** `[automated]` EditMode green; `[grep]` no "reserved" left in either enum file.

### Step 2: `TrapSpec` motion fields and `TrapMotion` (pure kinematics)
- **MODIFY `P/Runtime/Sim/TrapSpec.cs`**:
    - Constructor gains trailing optional `int offsetX = 0, int offsetY = 0, float speed = LevelRules.DefaultMoveSpeed`, so existing call sites compile.
    - Offset is clamped to `MinMoveOffset..MaxMoveOffset`; speed is clamped to `MinMoveSpeed..MaxMoveSpeed` (non-finite → default).
    - Add `int ExitIndex` (−1 = a tile trap).
    - Add `Box StartBox`: `CellBox` for tile traps, the exit's box for exit traps.
    - `static TrapSpec ForExit(int exitIndex, Box exitBox, TrapActionKind action, TrapTriggerKind trigger, Box zone, int delayTicks, int offsetX, int offsetY, float speed)`. Its `Kind` is `TileKind.None`, which only this factory allows; the public constructor still throws for kinds other than Solid/Hazard.
- **NEW `P/Runtime/Sim/TrapMotion.cs`**, `public static class`, pure and allocation-free:
    - `float MoveStep(float speed)`: speed × `TickSeconds`.
    - `int MoveTicks(int offsetX, int offsetY, float speed)`: ticks to arrive = `ceil(length / step)`, 0 for a zero offset.
    - `void MovePosition(Box start, int offsetX, int offsetY, float speed, int tick, out float minX, out float minY, out bool arrived)`: `f = min(tick × step, length) / length`; min = start + offset × f. At arrival it is **exactly** start + offset (no float residue).
    - `bool FallStep(TileGrid grid, Box box, float riseGravity, float maxFall, ref float vy, out float newMinY)`, returning true when it landed:
      - `vy = min(vy + riseGravity × dt, maxFall)`, then `dy = vy × dt`.
      - Landing scans the rows whose top lies in (new bottom, old bottom + ε] across the columns the box covers, half-open as `TileCollision`. It reads **`GetPainted`** Solid/Climbable only (falls through one-way, trap cells and other boxes).
      - Landed → `newMinY = row + 1` exactly and `vy = 0`.
      - **A bottom already inside a painted Solid/Climbable row** (only an exit's unsnapped sprite box can start that way) counts as landed on that row's top: the box snaps up to it and doesn't fall (round 1, F7).
    - `bool TryLandingRow(TileGrid grid, Box box, out int row)`: the first painted Solid/Climbable row at or below the box's bottom in its columns, a row the bottom is inside included (F7) (false = falls out of the map). The editor preview and the help text use this.
    - `int FallTicks(TileGrid grid, Box box, float riseGravity, float maxFall)`: iterates `FallStep` until it lands; −1 = falls out of the map. Same arithmetic as the stepper, so the help-box time matches play.
- **Tests (`TrapMotionTests`):**
    - Move 5 × 0 at 8 tiles/s: `MoveTicks` = 38. Position at tick 37 < end; at tick 38 exactly (5, 0) with `arrived`. Diagonal 3 × 4 at 30 tiles/s arrives in 10 ticks exactly at (3, 4). Zero offset arrives at tick 0.
    - A 30-tile move at speed 1: `MoveTicks` = 1800 (float32: 30 / 0.016666668 = 1799.9999); not arrived at tick 1799; at tick 1800 the end is bit-exact (`Assert.AreEqual` with no tolerance).
    - Fall from rest (default settings: rise gravity 57.14, max fall 12):
      - speed after tick 1 is `RiseGravity / 60`
      - capped at 12
      - lands with min Y exactly on the floor top
      - falls through a one-way row and a trap-cell row, lands on painted Climbable
      - no floor below → never lands
      - a floor directly under the box → `TryLandingRow` = that row, `FallTicks` = 0
    - `FallTicks` equals the tick count of a manual `FallStep` loop.
    - Constructor clamps: offset (40, −40) → (30, −30); speed 0 → 1, NaN → 8.
- **Verify:** `[automated]` EditMode green.

### Step 3: The moving-box layer and box-aware collision
- **NEW `P/Runtime/Sim/MovingBoxes.cs`**, `public sealed class`:
    - Constructor `(int capacity)`; preallocated `Box[]`, `TileKind[]`, active flags, plus an active-index list so iteration costs only the moving boxes.
    - `Set(int slot, Box box, TileKind kind)` (Solid or Hazard), `Remove(int slot)`, `Clear()`, `ActiveCount`, `GetActive(int k, out Box, out TileKind)`.
    - Doc: "the moving-box core: traps in motion between cells; a trap at rest is trap cells (systems.md § Traps)."
- **MODIFY `P/Runtime/Sim/TileGrid.cs`**:
    - `public MovingBoxes Boxes { get; private set; }`, an empty capacity-0 layer by default.
    - `public void AttachBoxes(MovingBoxes boxes)` (null throws). `LevelStepper` attaches its layer in its constructor.
    - Class doc: painted → trap cells → moving boxes; painted stays in charge only for cells, and a moving box blocks wherever it is.
- **MODIFY `P/Runtime/Sim/TileCollision.cs`**. Each query also tests the active boxes, and the nearest hit (cell or box) wins. Overlap is strict, as `Box.Overlaps`; touching faces don't overlap.
    - `SweepX`: Solid boxes whose Y-range strictly overlaps the hitbox's; stop at the nearest face in the travel range.
    - `SweepY` down: Solid box tops in (end, y + ε] that strictly overlap in X → land (`SupportKind.Solid`, never one-way).
    - `SweepY` up: Solid box bottoms in [top − ε, endTop) → ceiling. **Corner correction stays cells-only** (Notes 9).
    - `Support`: also Solid when a Solid box's top is within ε of `y` and strictly overlaps in X. This must work for a fractional `y`: the integer-row early return applies only to the cell check.
    - `WallGap`: Solid box faces within reach whose Y-range strictly overlaps. A box is never Climbable, so `climbableIsWall` doesn't matter, and `IsClimbableAt` / `GripIsClimbable` never see boxes (no cling on a trap).
    - `BoxIsFree`: false when a Solid box strictly overlaps.
    - `CoreTouchesHazard`: true when a Hazard box strictly overlaps the core.
    - `StandsFullyOnPainted`: unchanged (painted only).
- **MODIFY `P/Runtime/Sim/Box.cs`**: `static bool SweptOverlaps(in Box from, float dx, float dy, in Box target)`. Strict slab test of `from` moving along (dx, dy) against `target`; true if they overlap at any point of the move.
- **Tests (`MovingBoxCollisionTests`, a `TileGrid` + `MovingBoxes` built by hand):**
    - one case per query against a box at fractional coordinates (top at y = 2.37):
      - land on it from above and stand on it (`Support` Solid)
      - run into its side (stop at the face)
      - bonk its bottom
      - `WallGap` 0.1 away
      - `BoxIsFree` false/true at ± ε
      - a Hazard box kills the core at 0.2 − ε / doesn't at 0.2 + ε
    - a Hazard box neither blocks nor supports
    - corner correction slides past a cell corner but not a box corner
    - with an empty layer every `TileCollisionTests` case is unchanged (re-run them)
    - `SweptOverlaps`: a box passing fully through a target within one call → true; parallel miss → false; edge graze → false
- **Tests (`TileGridTests`):** a new grid has an empty layer; `AttachBoxes(null)` throws.
- **Verify:** `[automated]` EditMode green.

### Step 4: Moving traps in the level step (motion only)
- **MODIFY `LevelStepper`**:
    - **`TrapState`** gains:
      - `Motion` (private enum Resting 0 / Starting 1 / Moving 2 / Gone 3; Starting = fired but not yet advanced, still stamped as cells, round 1 F5)
      - `MinX`, `MinY` (current box min, sim)
      - `FallSpeed` (≥ 0, downward)
      - `MoveTick`
      - a private `Box CurrentBox(i)` (start size at the current min)
    - **A trap's current box is the one source** for:
      - `Inside` (Touch trigger; a Gone trap has no box, so a Touch trigger on it is false)
      - `ResolveAppeared`
      - `OutsideEveryTrap` (a Gone trap has no box; its zone still counts)
      - `RebuildTrapCells` (Resting or Starting, and Present, at the current rest cells `(int)MathF.Floor(MinX + 0.5f)` and the same for Y, as `TrapLook.CellMin` rounds; Moving and Gone are never stamped)
      - **The zone never moves** (it is the spec's zone).
    - **`ResetTraps`**:
      - `Present = Action != Appear` (Phase 3 bug: Move/Fall would start absent)
      - Motion Resting at `StartBox`, `FallSpeed = 0`, `MoveTick = 0`
      - `boxes.Clear()`
      - `TrapChanges++` when any presence **or position** changed
    - **`Fire`**:
      - Vanish → `Present = false`; Appear → `Present = true` (as now)
      - Move → `Motion = Starting` (zero offset: nothing happens); Fall → `Motion = Starting`
      - Move/Fall never change `Present` (Phase 3 bug: they would act like Vanish).
    - **New tick phase 4, "traps move"**, between the delays (+`ResolveAppeared`) and the player's step. **It is inside the Playing branch, so the death beat freezes every trap** (Dying returns early, unchanged).
      1. Stash every moving trap's old box in a preallocated array.
      2. Advance every Starting or Moving trap one tick from its old state (Starting becomes Moving here):
         - Move → `TrapMotion.MovePosition(++MoveTick)`; arrived → Resting at the exact end.
         - Fall → `TrapMotion.FallStep` with the level's `DerivedMovement` rise gravity and max fall (rebuilt in `ApplySettings`; not per tick). Landed → Resting at the exact row. The box fully below row 0 (`MaxY < 0`) → Gone, `Present = false`, `TrapChanges++`.
      3. Write each Moving Solid/Hazard trap's new box into `boxes`; remove Resting and Gone ones. Exit traps never enter the layer.
      4. If any trap started moving, came to rest or went this tick → **one** `RebuildTrapCells()` (never on ordinary moving ticks).
      5. Steps 5–6 (riders, pushes) and 7 (hazards) come next.
    - **Start moment** (document it in the `Step` comment):
      - A trap fired by its trigger (phase 6) starts moving on the next tick.
      - A trap fired by its Delay running out (phase 3) moves in the same tick's phase 4.
      - Until its first advance it stays Starting and stamped, so collision never has a one-tick hole (round 1, F5). In the phase 4 where it first advances, its cells leave the cell layer and its box enters the box layer in the same rebuild.
    - Exposed (read-only, no allocation): `TrapOffsetX(int)` / `TrapOffsetY(int)` (current min − start min) and `IsTrapMoving(int)`.
- **MODIFY `LevelScenario.Trap(...)`**: trailing `int offsetX = 0, int offsetY = 0, float speed = LevelRules.DefaultMoveSpeed`.
- **Tests (`MovingTrapTests`):**
    - A Move 0 × −3 Solid 3 × 1 with a far zone (no player contact):
      - starts the tick after the trigger tick
      - on the trigger tick its cells are still stamped (Starting; a Hazard Move still kills at that tick's death check, F5); on the start tick the box is in the layer and its cells are gone
      - it arrives after `MoveTicks` ticks, with the cells stamped exactly at the end and the layer empty
      - it stays there for 300 ticks
    - The same with Delay 0.5 s: moves on exactly the 30th tick after the trigger.
    - **Move end inside painted ground:** it travels through painted tiles and rests at the end; painted cells read as painted (painted wins); the trap cells sit under them.
    - **Fall:** starts at rest; speed after N ticks pinned; lands on painted Solid and Climbable; falls through a one-way row and a resting trap; rests as cells at the exact row.
    - **Fall out of the map:** Gone once `MaxY < 0`; `Present` false; `TrapChanges` moved; the layer is empty; it never comes back until a death; after the death it is back at the start, Resting and present.
    - **Death mid-move, then reset:** a moving trap killed mid-travel (a spike under the player) doesn't move during the 24 beat ticks (offset unchanged on every beat tick). On respawn it is back at the start, Resting, re-armed, and `TrapChanges` moved.
    - **Touch on a moving trap** uses the current box: touching it at its end fires a second Touch trap's logic; touching where it started doesn't.
    - **Zone stays where drawn:** after a Move, entering the original zone doesn't re-fire it (once per life), and the zone box is unchanged.
    - **Order independence:** two Move traps crossing each other's paths and a Fall through a resting trap, run with the array reversed → identical cell layer, boxes and offsets on every tick.
    - **`ResetTraps` presence:** Move and Fall are present at reset (Phase 3 regression test); Vanish present, Appear absent.
    - **Live gravity:** changing Gravity during a fall changes the next tick's acceleration.
- **Verify:** `[automated]` EditMode green.

### Step 5: Riding (carry and pull-away)
- **NEW `P/Runtime/Sim/BoxPush.cs`**, `internal static class` (pure, allocation-free; reused by `world-systems`):
    - `ApplyPush(ref PlayerState s, int dirX, int dirY, bool playerStepsThisTick)`, the **one push effect** for every push (Phase 3 push-out included):
      - zero the velocity component pointing into the pusher
      - `Contact = Air` unless lifted onto a top
      - `WallSide = 0`
      - on a rope: `RopeIndex = −1` and `RopeLockoutTicks = MovementInternals.RopeLockoutTicks + (playerStepsThisTick ? 1 : 0)`. A push before `stepper.Step` (phase 4, or a delay-fired Appear at phase 3) is counted down by that tick's step, hence +1; a push after it (a trigger-fired Appear's push-out) isn't. Either way re-grab is blocked through push tick + 12 and free at + 13, as after a rope jump (round 1, F4); document it.
- **MODIFY `LevelStepper` phase 4, riders** (using the old boxes stashed in Step 4):
    - **Rider:** the player `IsStanding`, its feet within ε of a moving (or just-started) Solid trap's **old** top, overlapping it strictly in X.
    - **Several carriers:** the one whose new top is highest; ties → smaller |dx|, then smaller dx (by value, never by index).
    - **Carry Y first, then X:**
      - **Up:** the feet go to the new top. If `BoxIsFree` fails there (a ceiling) → the crush rule (Step 6, nudge first).
      - **Down:** only when nothing else (cells or another box) supports the feet at the old height, and only while the trap's descent speed ≤ the **ride speed**.
        - Ride speed = min(previous ride speed + `FallGravity` × dt, `MaxFallSpeed`).
        - **Seed (round 1, F3):** `Land` zeroes `VelocityY` (`PlayerStepper.Land`), so the post-step speed can't be the source. `LevelStepper` stashes the player's `VelocityY` before `stepper.Step`; when the post-step player stands on a moving box it wasn't standing on before the step, ride speed = `max(0, −preStepVy)`. It is 0 when the player was standing on the trap before it started.
        - It resets to 0 when the player isn't riding a trap going down.
      - Carried down → feet on the new top. Otherwise (**pull-away**): no vertical carry, `Contact = Air`, `CoyoteTicks = MovementInternals.CoyoteTicks`, so a ground jump is allowed on the pull-away tick and the 5 after (6 ticks; Notes 3).
      - **X:** `TileCollision.SweepX` by the carrier's dx against the updated world. **Soft:** a wall stops the rider there and the trap slides out from under it.
    - **No momentum:** carry moves the position only; `VelocityX`/`VelocityY` are untouched.
    - **Landing tick:** a Fall that lands this tick carries the rider by its actual (snapped) displacement. The trap becomes cells in the same tick's rebuild, so the player's step sees cell support and stays standing (no Air tick).
    - **Sides never carry:** only a rider is carried.
- **Tests (`TrapRideTests`):**
    - **Sliding floor:** standing on a Move 6 × 0 at speed 4:
      - X advances by exactly the trap's step every tick; Y stays on the top
      - `IsStanding` on every tick of the ride
      - a ground jump works mid-ride with no horizontal speed added (`VelocityX` stays the player's own; no momentum)
    - **Carry into a wall:** the rider stops at the wall face on the tick it reaches it, stays standing while the trap remains under it, and falls (walk-off coyote) the tick the trap leaves its feet.
    - **Rising lift:** a Move 0 × +3 carries the rider up, feet exactly on the top every tick, standing.
    - **Rising into a ceiling:** with a ceiling 1 tile above the lift's end, the rider dies by crush on the first tick its head would enter the ceiling (assert that tick and that the nudge was tried).
    - **Fall with rider:**
      - the rider stays standing on every tick of the fall at default Gravity and at Gravity 0.25 and 2 (factor 1.75 > 1)
      - landing tick: standing on the landing tick and the next, feet exactly on the integer top
      - a jump off mid-fall: `VelocityY == JumpVelocity`, with nothing from the trap added
    - **Pull-away coyote:** a Move 0 × −3 at speed 8:
      - pulls away on its first moving tick (assert Air, and `CoyoteTicks` 5 after the tick: set to 6 before the player's step, then counted down by it, F9)
      - a jump pressed on the 6th tick (pull-away tick + 5) fires; on a separate run, a press on the 7th doesn't
      - without a jump, the player lands back on the trap before it arrives, then rides it to the end without another pull-away (ride speed seeded from the pre-step fall speed, about 12 at default settings, F3)
    - **Faster than max fall:** a Move 0 × −8 at speed 30 with MaxFallSpeed 12: pulls away and the player lands on it after it has arrived.
    - **Standing half on a ledge** as a Fall trap drops: no vertical carry, still standing on the ledge.
    - **Two carriers** (two adjacent Move traps with different dx): the highest-top rule chooses; the result is identical with the array reversed.
- **Verify:** `[automated]` EditMode green.

### Step 6: Leading-face push, crush and the nudge
- **MODIFY `BoxPush`** with `Resolve(...)`: given the player after carry, the old and new boxes of the moving Solid traps that overlap the player's hitbox at their new boxes, and the grid. It returns the player's new feet, or crushed.
    - **Leading face**, for each overlapping trap:
      - old X-ranges already strictly overlapped → vertical push (sign of dy)
      - else old Y-ranges overlapped → horizontal push (sign of dx)
      - else (corner) → the axis of smaller penetration at the new box; ties vertical
      - A face that isn't leading never pushes. A trap moving away doesn't pull.
    - **Targets:**
      - right: `x ≥ new.MaxX + HalfWidth`
      - left: `x ≤ new.MinX − HalfWidth`
      - up: `y ≥ new.MaxY`
      - down: `y ≤ new.MinY − Height`
    - **Combine across traps by value:** the furthest target per direction. Pushes in opposite directions on one axis that can't both be met → crushed (squeezed).
    - **Blocked:** the final spot fails `BoxIsFree` (cells + every box) or the core touches a hazard. Then the **nudge**: from the pre-push position on the push axis, shift at right angles by the least amount that clears the pushers, at most `LevelRules.CrushNudge`, to a spot that is free and hazard-free. The smaller shift wins; ties go up, left, right, down. No nudge spot → crushed.
    - Success → `ApplyPush`, with a lift up counted as landing onto the top (the stepper's re-probe classifies it).
- **MODIFY `LevelStepper` phase 4**:
    - Order: riders (Step 5) → pushes → hazard sweep (Step 7).
    - **Crushed (here or in a carry) → `Die()` and return at once.** The player doesn't step this tick, and the death spot is where it was crushed (Notes 5).
    - Phase 3's appear crush keeps its order.
    - **`PushOut`** (appear) calls `BoxPush.ApplyPush`, so it gains the rope lockout (one push rule).
- **Tests (`TrapPushTests`, extended):**
    - **Crusher against a wall:** a Move −4 × 0, 2 × 3 Solid pushes a standing player left:
      - X equals the trap's new min − HalfWidth on every push tick
      - `VelocityX` toward the trap zeroed
      - crushed on the tick the wall blocks
      - Deaths + 1; the beat follows
    - **Falling ceiling, no nudge room:** a Fall 3 × 1 centred over a player standing on the floor → crushed on the first tick its bottom reaches the head.
    - **Falling ceiling clipping the edge:** the ceiling overlaps the player's hitbox by 0.2 in X → nudged 0.2 sideways, alive. By 0.3 → crushed (both sides of 0.25).
    - **Sideways push with nudge** (Notes 4): a jumping player whose feet are 0.2 below a horizontal pusher's top on the push tick, against a wall → nudged up onto it, alive; 0.3 → crushed. (A grounded player's feet and a horizontal Move's top are both whole numbers, so only an airborne or riding player can meet this case, F10.)
    - **Airborne push down:** a Move 0 × −3 meets a jumping player → pushed down, upward speed zeroed, alive.
    - **Rising trap from below** an airborne player → lifted onto its top, standing on the next tick.
    - **Two traps pushing one tick:** two Moves pushing the same way → the furthest target. Opposite ways → crushed. Both cases identical with the array reversed (player state and `Deaths`).
    - **Sides never carry:** a trap moving up beside the player (touching its side) leaves it in place.
    - **Rope push lockout:**
      - a Move pushes a player off a rope while Up is held → `RopeIndex` −1; re-grab blocked through push tick + 12, allowed on tick + 13 (both sides)
      - the same for a Phase 3 Appear push-out (the fix), once delay-fired (push before the player's step) and once trigger-fired (push after it): both blocked through push tick + 12, free at + 13 (round 1, F4)
    - **Hazard traps never push or carry:** standing on a moving Hazard trap isn't possible (it doesn't support); a moving Hazard overlapping the player kills instead (Step 7).
- **Verify:** `[automated]` EditMode green.

### Step 7: Moving Hazard traps (swept check)
- **MODIFY `LevelStepper` phase 4**:
    - For every Moving Hazard trap, `Box.SweptOverlaps(old box, dx, dy, core)` against the player's hazard core (after carry and pushes) → `Die()` and return.
    - The end-of-tick death check also sees Hazard boxes (Step 3's `CoreTouchesHazard`).
- **Tests (`HazardTests`):**
    - **No skip:** a 1 × 1 Hazard Move at speed 30 crossing a standing player's core, and again with the player running toward it at Run Speed 12 (the slider maximum), over 64 sub-tick start offsets each:
      - death in every case, on the first tick whose swept move overlaps the core (assert that tick)
      - the case count is asserted
      - the same with a 1 × 1 strip passing ε outside the core (inside the hitbox's 0.2 inset) → no death in any case; ε inside the core → death
    - A moving Hazard never moves the player (X/Y unchanged on the tick before death).
- **Verify:** `[automated]` EditMode green.

### Step 8: Traps on an Exit (sim)
- **MODIFY `LevelStepper`**:
    - A trap with `ExitIndex ≥ 0` moves the exit's box. Its current box replaces `exits[ExitIndex]` for the exit check, and an absent exit trap (Vanish fired, Appear unfired, Fall Gone) means that exit can't be touched.
    - Exit traps are never stamped, never in the box layer, and never carry, push or crush.
    - Fall on an exit lands its box bottom on painted Solid/Climbable through the columns its (fractional) box covers.
    - Safe ground counts an exit trap by its trigger only.
    - A Touch-triggered trap on an exit fires in phase 6, before exits are checked, so a Touch Vanish exit vanishes as it is touched (no win).
    - **No win on the fire tick (round 1, F6):** an exit whose own trap fired in this tick's triggers phase can't be won this tick, so a Move/Fall exit starts moving (next tick's phase 4) before it can be reached, and a Touch-triggered Move/Fall exit runs instead of winning. During a Delay the exit stays winnable (the student chose the timing).
- **MODIFY `LevelScenario`**: `.ExitTrap(int exitIndex, TrapActionKind action, ...)`.
- **Tests (`ExitTrapTests`):**
    - **Runaway Exit** (Move +6 × 0 at speed 8, zone 3 tiles before the exit):
      - the exit box moves; touching the start box after it left doesn't win
      - touching the end box wins
      - after a death the exit is back at the start
    - **Vanish exit:** gone after firing (no win while standing in its old box). A Touch Vanish exit doesn't win on the touch tick.
    - **Appear exit:** no win before firing; win after.
    - **Fall exit:** lands on painted ground with its box bottom exactly on the row; a Fall exit over a pit is Gone and unwinnable until a death.
    - **Fall exit dipping into a ledge** (F7): an exit box whose bottom is at 2.95 over a ledge with top 3 doesn't fall; it snaps up to 3. The same exit over a one-row ledge above a pit doesn't fall out of the map.
    - **Default-zone Runaway Exit** (F6): a Move +6 × 0 exit with the default zone (exit box grown by 1 on every side), approached from the side at Run Speed 6 and at 12, over 16 sub-tick start offsets each (count asserted): the trap fires before any touch of the start box, the exit is moving before the player can reach it, and no case wins at the start box.
    - **Touch-triggered Move exit** (F6): touching it doesn't win on the fire tick; the exit runs.
    - **Trap on a disabled Exit** (F2): ignored; the trap doesn't move the exit.
    - An exit trap never blocks: the player walks through its box at every tick of the move.
- **Verify:** `[automated]` EditMode green.

### Step 9: Zero allocation in the world step
- **MODIFY `StepAllocationTests`**:
    - Recorded level scripts also exercise:
      - a Move trap with a rider
      - a pull-away
      - a carry into a wall
      - a push
      - a crush with and without the nudge
      - a Fall landing with a rider
      - a Fall out of the map
      - a moving Hazard death
      - an exit trap
    - The branch list grows accordingly (`"move"`, `"carry"`, `"pull-away"`, `"push"`, `"nudge"`, `"fall land"`, `"fall gone"`, `"swept hazard"`, `"exit trap"`), each asserted as a precondition.
    - Seen failing with a used `new Box[1]` planted in phase 4; reverted.
- **Verify:** `[automated]` EditMode green, planted failure seen.

### Step 10: Trap component, spec conversion and map include (runtime)
- **MODIFY `P/Runtime/Trap.cs`**:
    - `[SerializeField] Vector2Int offset = new Vector2Int(LevelRules.DefaultMoveOffsetX, LevelRules.DefaultMoveOffsetY);` tooltip: "Where a Move trap ends, in whole tiles from where it starts (x right, y up). Drag the arrow's handle in the Scene view."
    - `[SerializeField, Range(LevelRules.MinMoveSpeed, LevelRules.MaxMoveSpeed)] float speed = LevelRules.DefaultMoveSpeed;` tooltip: "How fast a Move trap travels, in tiles per second. It moves at this speed the whole way."
    - Field order: trigger, zone, action, offset, speed, delay, size, tile.
    - `action` tooltip: "What the trap does: Vanish (disappears), Appear (hidden until it fires), Move (slides to its end and stays) or Fall (drops until it lands, or out of the map)."
    - Internal accessors; `dataVersion` stays 1.
    - `Reset()` uses `TrapLook.DefaultZoneLocal(this)` (exit-aware).
    - Gizmos skip ignored Traps (on a Checkpoint or a disabled Exit); on an Exit they draw the exit's box outline instead of a tile box.
- **MODIFY `P/Runtime/TrapLook.cs`**:
    - `ClampedOffset(Trap)` (per axis −30..30) and `ClampedSpeed(Trap)`: the only readers of the raw fields, like `ClampedSize`.
    - `IsOnExit(Trap)` (enabled Exit only, F2), `IsOnCheckpoint(Trap)`, `IsOnDisabledExit(Trap)`; `IsIgnored` = on a Checkpoint or a disabled Exit.
    - `WorldBox(Trap)`: the exit's `LevelCollector.ElementBox` when on an Exit, else the snapped tile box.
    - `EndBox(Trap)` = `WorldBox` + clamped offset (Move only; else `WorldBox`).
    - `DefaultZoneLocal(Trap)` from `WorldBox` relative to the position. The `Vector2Int` overload stays for tile traps.
    - `IsOffGrid` and `CoversPaintedTiles` are false on an Exit.
    - `EndCoversPaintedSolid(Trap, Tilemap)` reuses the `CoversPaintedTiles` cell loop over `EndBox`, counting Solid/Climbable/plain tiles only.
    - `ToSpec(Trap, Vector2 origin, int exitIndex)` maps Vanish/Appear/Move/Fall by value (an undefined value → Vanish, as now) and passes offset and speed. On an Exit it builds `TrapSpec.ForExit` with the exit box.
- **MODIFY `P/Runtime/LevelCollector.cs`**:
    - `FindTraps(out ignored)` leaves out Traps on a Checkpoint or a disabled Exit (`TrapLook.IsIgnored`, F2).
    - `EndBoxes(Trap[])` for the include.
- **MODIFY `P/Runtime/Player.cs`**:
    - The include grows by `EndBoxes(traps)` too (Move ends are inside the map).
    - Exit traps get their index with `Array.IndexOf(exits, trap.GetComponent<Exit>())` at play start.
    - **A Trap on a disabled Exit (round 1, F2):** `IsOnExit` is `exit != null && exit.enabled`, at edit time and at play start alike (round 2, F16: not `isActiveAndEnabled`, which is false for every component on a prefab asset, so a student's runaway-exit prefab would read as "turned off" in the inspector). At play start this matches `PlayStartScan.FindActive`: the Trap shares the Exit's object, so a Trap found active has an active object, and every on-exit trap has an index in the exit list. A Trap beside a disabled Exit is ignored like a Checkpoint trap, with the warning `TrapOnDisabledExit`: "This Trap's Exit is turned off, so the Trap does nothing." Never an assert: a student unticking the Exit is an expected case.
- **MODIFY `P/Runtime/PlayerWarnings.cs`**:
    - `TrapOnExitOrCheckpoint` → **`TrapOnCheckpoint`**: "A Trap on a Checkpoint does nothing. Put the Trap on its own object." Update every reference.
    - New `TrapOnDisabledExit`: "This Trap's Exit is turned off, so the Trap does nothing." (round 1, F2); logged once at play start and shown in the inspector.
    - New `TrapEndInGround`: "This Move trap ends inside painted ground. It moves through tiles, so it will stop buried there. Drag its end (the arrow's handle) out of the ground."
    - Logged once at play start for Move traps whose end covers painted Solid (`WarnLevel`).
- **Tests (`TrapLayoutTests`):**
    - `TrapPaths` = the eight settings in the order above; tooltips on all eight.
    - Enum values Vanish 0 / Appear 1 / Move 2 / Fall 3 pinned on both enums.
    - Defaults and Range from `LevelRules`; `dataVersion` 1.
    - **Old data loads:** a Trap JSON with only the six Phase 3 fields (`EditorJsonUtility.FromJsonOverwrite` onto a new Trap) keeps its action/zone/size and gets offset 0 × −3 and speed 8.
    - `ToSpec`:
      - Move with offset (40, 2) → spec offset (30, 2); speed 50 → 30
      - Fall → `Fall`
      - an Exit trap → `ExitIndex`, `Kind` None, `StartBox` = the exit box (sim coordinates)
      - a Checkpoint trap is in `ignored`
    - **`IsOnExit` follows `enabled`** (round 2, F16): on an inactive object (standing in for a prefab asset), `IsOnExit` is true with the Exit enabled and false with it unticked; on an active object an unticked Exit makes the trap ignored with `TrapOnDisabledExit`.
    - `EndBox` and the map include: a Move end 20 tiles right of every painted tile grows the map.
    - `DefaultZoneLocal(trap)` on an Exit with a bottom-pivot sprite = the exit box grown by 1 on every side, relative to the position (F6).
- **Verify:** `[automated]` EditMode green.

### Step 11: Drawing moving traps (interpolation)
- **MODIFY `P/Runtime/PlayerSession.cs`**:
    - Preallocated `previousTrapX/Y`, `currentTrapX/Y` (sim offsets) filled from `level.TrapOffsetX/Y` every `Tick`.
    - On `Current.Teleported` (respawn) and in `Restart`, previous = current for every trap, so a reset trap snaps back and doesn't slide.
    - `Vector2 InterpolatedTrapOffset(int i)` (world units = sim units; struct, no allocation).
- **MODIFY `P/Runtime/Player.cs`**:
    - At play start, cache each trap's base world position after `ApplyPlayLook`.
    - In `Render`, for each trap: position = base + `InterpolatedTrapOffset(i)` (z kept), written only when it differs from the last written value (cached floats).
    - Presence still follows `TrapChanges`. A Gone Fall trap is hidden through `IsTrapPresent`.
    - No allocation, no `GetComponent`.
- **MODIFY `TrapLook.ApplyPlayLook`**: on an Exit, set only `enabled = present` and leave the exit's sprite, draw mode and position alone. Otherwise unchanged.
- **Tests (`PlayerSessionTests`):**
    - Mid-move at fraction 0.5, the offset is halfway between the last two ticks.
    - After a death mid-move, the respawn tick reports previous == current == 0.
    - `Restart` snaps.
- **Verify:** `[automated]` EditMode green (PlayMode coverage at Step 15).

### Step 12: Help-box stories with distance and travel time
- **MODIFY `P/Runtime/HelpTexts.cs`**:
    - Signature: `TrapStory(Trap trap, Tilemap collision, MovementSettings settings)`. The editor passes its cached Collision Tilemap and the scene Player's settings (make `Player.BuildSettings` internal if needed; default settings when there is no Player).
    - **Move:** `"{Slides N tiles right|left / Rises N tiles / Sinks N tiles / Moves N right and M up} in T s after {when}{, D s later}."`
      - T = `TrapMotion.MoveTicks` / 60, shown to 0.1 s (e.g. 5 tiles at 8 → "0.6 s").
      - A zero offset → "Its Offset is 0 × 0, so it doesn't move."
    - **Fall:** `"Falls N tiles in T s after {when}."` from `TrapLook.FallLanding`.
      - Nothing below → "Falls out of the map after {when}."
      - Already on ground → "It's already resting on painted ground, so it won't fall."
    - **Exit:**
      - "The exit runs N tiles right in T s after {when}."
      - "The exit vanishes when {when}."
      - "The exit is hidden until {when}, then appears."
      - "The exit falls N tiles…" / "The exit falls out of the map…"
    - Vanish/Appear sentences, the spike line and the early-zone line are unchanged.
- **MODIFY `P/Runtime/TrapLook.cs`**: `bool FallLanding(Trap trap, Tilemap collision, MovementSettings settings, out int tiles, out int ticks)`.
    - Reads only the column strip under the trap's box, down to the Collision Tilemap's painted bottom, into a small `TileGrid`, using `CollisionMapReader`'s own tile → kind mapping (no second mapping).
    - Calls `TrapMotion.TryLandingRow` / `FallTicks` with `DerivedMovement(settings)`.
    - Editor-time and play-start only; never per frame.
- **Tests (`HelpTextsTests`):**
    - Move right 5 at 8 → "Slides 5 tiles right in 0.6 s after the player enters the zone."; left, up, down, diagonal, with Delay 0.5.
    - A zero offset.
    - Fall 4 tiles at default settings, the time from `FallTicks`, then the same at Gravity 2 (shorter).
    - Fall with nothing below → "Falls out of the map".
    - Each exit sentence; the invariant-culture number (a comma locale still prints "0.6").
- **Verify:** `[automated]` EditMode green.

### Step 13: Trap inspector, Move-end handle and Fall landing outline (editor tools)
- **MODIFY `P/Editor/TrapEditor.cs`**:
    - `OnEnable` adds `offset` and `speed`.
    - Draws trigger; zone (Zone only); action; **offset and speed only when every selected trap's action is Move** (hidden on mixed values, like the zone); delay; **size and tile only when not on an Exit**. At most 8 settings at once.
    - Help boxes:
      - the story with the cached collision and settings
      - `TrapOnCheckpoint` (Warning) for a Trap on a Checkpoint, and `TrapOnDisabledExit` for one beside a disabled Exit, instead of the story
      - `TrapEndInGround` when `TrapLook.EndCoversPaintedSolid`
    - `OnSceneGUI`, for the target trap:
      - **Move:** a line with an arrowhead from the box centre to the `EndBox` centre, plus the end box outline. A `Handles.FreeMoveHandle` (or a `Slider2D`) at the end centre whose drag snaps the offset to whole tiles and clamps it to the range. Undo-recorded ("Move Trap End"). Copy the zone handle's round-then-store pattern.
      - **Fall:** the landing box outline from `TrapLook.FallLanding` (dashed). Nothing below → a short downward arrow off the bottom of the box, labelled "Falls out of the map".
      - The landing result is cached by (`CellMin`, size, action) and invalidated on `Tilemap.tilemapTileChanged`; never recomputed per repaint for an unchanged trap.
      - The zone handle stays as is.
    - Exit traps: the outline and handles use the exit box (`TrapLook.WorldBox`).
- **MODIFY `P/Editor/TrapAuthoring.cs`**:
    - `SyncLook`: `renderer.enabled = trap.Action != TrapAction.Appear` (Phase 3 bug: Move/Fall would be saved hidden). On an Exit, leave the renderer alone entirely; skip only Checkpoint traps as now.
    - `FitZone` uses `TrapLook.DefaultZoneLocal(trap)`.
    - `OnSizeChanged` is unchanged.
- **`TrapGhostGizmo`**: unchanged (Appear only); confirm it skips Exit traps' tile drawing (no tile).
- **Tests (`TrapAuthoringTests`):**
    - `SyncLook`: Move and Fall → enabled, Appear → disabled, Vanish → enabled.
    - On an Exit, the renderer's sprite/drawMode/enabled are unchanged.
    - `FitZone` on an Exit trap = the exit box grown by 1 on every side (F6).
    - The end-handle write path: a helper `TrapAuthoring.SetEnd(Trap, Vector2 worldEndCentre)` that the handle calls rounds to whole tiles, clamps to ±30 and is undoable (`Undo.PerformUndo` restores).
- **Verify:** `[automated]` EditMode green; Step 17 carries the UI.

### Step 14: Play-start warnings
- **MODIFY `Player.WarnLevel`**:
    - `TrapOnCheckpoint` or `TrapOnDisabledExit` for each ignored trap, by its reason.
    - `TrapEndInGround` for Move traps.
    - No over-painted, off-grid, no-tile or scaled warnings for Exit traps (they have no tile box).
- **Tests (`LevelWarningTests`):**
    - a Trap on a Checkpoint → `TrapOnCheckpoint` once, and the checkpoint still works
    - a Trap on an Exit → **no** warning, and the exit moves
    - a Trap on a disabled Exit (Exit component unticked) → `TrapOnDisabledExit` once, no exception, Play runs (F2)
    - a Move end in painted ground → `TrapEndInGround` once
    - a clean moving-trap level logs nothing
- **Verify:** `[automated]` PlayMode green.

### Step 15: PlayMode moving-trap tour and allocation guard
- **MODIFY `TestLevel`**:
    - `AddTrap(...)` gains `Vector2Int? offset = null, float speed = LevelRules.DefaultMoveSpeed`.
    - `AddExitTrap(Exit exit, TrapAction action, ...)`.
- **MODIFY `TrapPlayTests`** (keyboard via `InputTestFixture`, `captureFramerate = 60`). Each run asserts its outcome and saves a picture:
    - **Falling Ceiling:**
      - a Fall Solid 3 × 1 three tiles above the floor, with its zone drawn under it
      - walk in and keep running → survive
      - picture mid-fall: `traps-fallingceiling.png`
      - a second run standing still → crush death
    - **Sliding floor:**
      - ride a Move +6 × 0 over a pit; picture mid-ride: `traps-ride.png`
      - **assert, on every frame of the ride and at fractional frame times** (drive `RunFrame` with 1/90 s): the drawn feet sit on the drawn trap top within 0.001 (no jitter, no gap)
    - **Crusher:** a Move pushing the player into a wall → crush; picture during the beat: `traps-crush.png`.
    - **Fall floor over a pit:** stand on it, it falls, jump off mid-fall onto a ledge; picture: `traps-fallfloor.png`.
    - **Fast Hazard:** a 1 × 1 spike Move at speed 30 across a corridor → death on contact; picture: `traps-fasthazard.png`.
    - **Runaway Exit:** the exit slides away as the player approaches; follow it; win; picture at the end position: `traps-runaway.png`.
    - **Reset snap:** a death mid-move; on the respawn frame the trap is drawn at its start (no in-between frame).
- **MODIFY `PlayerAllocationTests.RunFrameAllocatesNothing`**: the window also covers a Move trap with a rider (per-frame transform writes) and a Fall out of the map. Seen failing with a planted `new Vector3[1]` in the trap render loop; reverted.
- **Verify:**
    - `[automated]` PlayMode green.
    - `[manual]` Look at every new `traps-*.png`:
      - the moving trap is drawn exactly where its outline would be
      - the rider's feet are on the trap
      - the exit is at its end
      - no trap is drawn half a tile off
    - Must be absent: a gap or overlap between rider and trap, a trap drawn at its start after it moved, a visible exit after it fell out of the map.

### Step 16: Trap bench: moving traps (dev)
- **MODIFY `Assets/Dev/Editor/TrapTestSceneBuilder.cs`**:
    - The `TrapDef` table and class gain `Offset` and `Speed`. `MakeTrap` writes `offset` / `speed` through `SerializedObject`.
    - `renderer.enabled = action != Appear` (same bug as `SyncLook`).
    - Widen the layout to about 120 × 18 **(proposal, open question 4)**. The Phase 3 sections stay unchanged on the left; the new sections are appended to the right, and GUIDs stay stable.
    - **New sections:**
      - Falling Ceiling (Fall, zone under it)
      - Sliding floor over a pit (Move +6 × 0, speed 4)
      - Crusher against a wall (Move −4 × 0, 2 × 3)
      - Fall floor over a pit with a ledge to jump to
      - Fast Hazard Move (1 × 1 spikes, speed 30) across a corridor
      - **Runaway Exit** (the bench's one Exit gets a Move trap, +6 × 0, zone 3 tiles before it, ending on a reachable ledge)
    - **Adversarial fixtures:** a Move whose end is inside painted ground (warning); a Trap on the Checkpoint (warning; the checkpoint still works). The Runaway Exit uses the **default** exit zone (box grown by 1 on every side), so the bench shows what a student's first runaway exit does (round 1, F6, F15).
    - **Self-check:**
      - trap count and each trap's action, trigger, kind, offset and speed equal the table
      - exactly one Exit, carrying a Move trap
      - one Checkpoint
      - tile counts equal the ASCII
- **Run:** `Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.TrapTestSceneBuilder.Build -logFile TestResults/trap-scene.log` (6000.6.7f1, dev project closed).
- **Verify:**
    - `[automated]` exit 0.
    - `[manual]` `trap-dev-scene.png`: every section is readable, the new traps sit on the grid, and the moving traps look like the ground.
    - A second run keeps every GUID.

### Step 17: Operator editor check (STOP for the operator)
Run Step 18's suites first if anything changed since Step 15. In the dev project (6000.6.7f1); record the outcome in the phase log.
- **Do:**
    1. Open `TrapTest.unity`. Look at the Scene view without selecting anything, then select each moving trap.
    2. Add a new Trap (Square sprite, Add Component > Daedalus > Trap), pick the ground tile, then switch Action through Vanish, Appear, Move, Fall.
    3. With Move: drag the end handle around, including into painted ground and past 30 tiles; change Speed; Undo twice.
    4. With Fall: move the trap over a pit, then over ground, then paint a tile under it with the Tile Palette.
    5. Select the Runaway Exit's Trap; switch its Action through all four.
    6. Select the Trap on the Checkpoint.
    7. Multi-select a Move and a Fall trap; then two Move traps and edit Speed together.
    8. Press Play, then Stop.
- **Observe:**
    - Offset and Speed appear only for Move; Size and Tile don't appear on the Exit's Trap; never more than 8 settings; every setting has a tooltip.
    - The Move end arrow and outline follow the handle in whole tiles and stop at the range.
    - The story names distance and time and changes as you drag ("Slides 5 tiles right in 0.6 s…").
    - The end-in-ground help box appears and disappears as the end crosses painted ground.
    - The Fall landing outline sits on the first ground below. Over a pit the box says "Falls out of the map". Painting a tile under it updates the outline.
    - The Exit's Trap tells the exit's story; the Checkpoint's Trap shows only its warning.
- **Must be absent:**
    - any error or exception
    - the Move handle on a non-Move trap
    - a landing outline that disagrees with where the trap actually lands in Play
    - Offset/Speed shown on a mixed selection
    - the Exit's sprite changed by switching actions
    - a Data Version field
- Fix what fails, re-run Step 18, and repeat the check.

### Step 18: Run all tests
- `python Tools/unity_tests.py` (6000.0.85f1 and 6000.6.7f1, EditMode and PlayMode). Close the dev project first.
- Fix failures in the code or the test; never weaken an assertion to pass.
- Inspect, from **both** editors: `*-render-smoke.png`, every `*-movement-*.png` and every `*-traps-*.png`, plus `trap-dev-scene.png`. Record what was seen in the phase log.
- Do not continue until all four runs are green with no compiler warnings.

### Step 19: Operator play-check and tuning (STOP for the operator)
In `TrapTest.unity`, at the laptop's normal refresh rate; keyboard, and a gamepad if available.
- **Do:**
    - Ride the sliding floor end to end. Jump on it, jump off it mid-ride, run against its direction.
    - Ride it into the wall at its end (if the layout allows) and stand still.
    - Walk under the Falling Ceiling: first sprint through, then hesitate, then stand at its edge.
    - Stand in front of the crusher; then stand with your edge just under the crusher's or ceiling's corner.
    - Stand on the Fall floor as it drops; jump off at the start, the middle and the last moment.
    - Run at the fast Hazard trap; then time a jump over it.
    - Chase the Runaway Exit.
    - Die mid-move on each trap and watch the reset.
- **Observe and report (what you saw, not whether it matches):**
    - **Carry feel:** does standing on a moving floor feel planted or slippery? Any jitter?
    - **Jumping off a fast trap:** do you expect to keep its speed? Does losing it feel wrong?
    - **Falling Ceiling reaction window:** could you react after it started falling?
    - **Crush fairness:** does every crush look deserved? Does the nudge save you when only your edge was under?
    - **Pull-away:** if a trap drops away under you, does the short hop read as intended?
    - Whether Speed 8 and Offset 0 × −3 are good starting values for a new Move trap.
- **Must be absent:**
    - a death with no visible cause
    - a crush while visibly clear of the trap
    - the player sinking into or hovering above a moving trap
    - a coyote hiccup (a frame of falling) when a falling floor lands
    - a trap moving during the death beat
    - a trap sliding back after respawn
    - grabbing a rope at once after being pushed off it
    - any Console error
- **Tuning:** Claude changes `LevelRules` (speed default, offset default, `CrushNudge`) on the operator's words, one change per replay. Keep a tuning table in the phase log (setting, old, new, why).
- **Gate:** the operator says the moving traps feel right.

### Step 20: Make the tuned values the defaults
- **MODIFY** `LevelRules.cs`, the `LevelRulesTests` pins, the entities.md mirror (Notes), and the bench if a default moved.
- Re-run Step 18 and look at the pictures again.
- **Verify:** `[automated]` all green; `[grep]` no tuned number outside `LevelRules`, its test and entities.md.

### Step 21 (Final): Code review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on every changed file. Ask specifically about:
    - allocations in `LevelStepper.Step` (phase 4, `BoxPush`, `TrapMotion`) and in `RunFrame` / `Render` (trap transforms)
    - every `TileCollision` query seeing boxes (no query failing open), and painted-only reads where the spec says painted (`FallStep`, `StandsFullyOnPainted`)
    - order independence (no index-dependent tie-break anywhere in phase 4)
    - no full cell rebuild on ordinary moving ticks
    - both ends of every wire:
      - `TrapOffsetX/Y` → `PlayerSession` → trap transform
      - Teleported/Restart → trap snap
      - Gone → `TrapChanges` → hidden
      - exit trap → exit box → win
      - offset/speed fields → `ToSpec` → `TrapSpec` → motion
      - `EndBox` → map include
      - `FallLanding` → outline and story
      - `TrapEndInGround` / `TrapOnCheckpoint` → their conditions
      - the rope lockout in `ApplyPush` → both push paths
    - the editor has no gameplay logic (landing, times and end boxes all come from `TrapMotion` / `TrapLook`)
    - enum values, serialized names and `dataVersion` as in this prompt
- Fix any HIGH/MED issues, re-run Step 18, and report the review result to the user.

---

## Completion Criteria

- [ ] `TrapActionKind` / `TrapAction` Move 2, Fall 3; the new `LevelRules` values are pinned and mirrored in entities.md.
- [ ] Moving-box core: `MovingBoxes` attached to `TileGrid`; every collision query sees moving boxes; resting traps are trap cells at exact whole cells; no full cell rebuild on ordinary moving ticks.
- [ ] Tick order: delays → **traps move (advance, riders, pushes, crush/nudge, swept hazard)** → player → triggers → death → checkpoints/exits → safe ground. The death beat freezes traps; every death resets them with a render snap.
- [ ] Move: straight line, constant speed, through painted tiles, exact end, stays. Fall: rise gravity × Gravity, capped at max fall, painted Solid/Climbable landing, through one-way and traps, Gone below the map until a reset.
- [ ] Riding: standing with ground jumps; soft carry; no momentum; pull-away with a 6-tick coyote window; no hiccup on the landing tick; the rider stays on a Fall trap at every Gravity setting.
- [ ] Pushes: leading face only; blocked → nudge ≤ 0.25 → crush; rising into a ceiling crushes; squeeze crushes; same result with the trap order reversed; rope release + lockout on every push (appear push-out included).
- [ ] Moving Hazard: swept check with counted no-skip cases; it never pushes or carries.
- [ ] Every action works on an Exit; a Trap on a Checkpoint is ignored with the new warning.
- [ ] Touch triggers and safe ground use current boxes; zones stay where drawn; traps fire once per life.
- [ ] Trap component: 8 settings in the pinned order; Offset/Speed only for Move; Size/Tile hidden on an Exit; `dataVersion` 1; old data loads with the defaults.
- [ ] Map bounds include Move end boxes.
- [ ] Interpolated drawing of moving traps with rider alignment checked; zero allocation in the step and the frame (planted failures seen).
- [ ] Editor: Move end arrow, outline and snapping handle; Fall landing outline from the shared query; story with distance and time; end-in-ground warning; Undo-safe and multi-object safe.
- [ ] The trap bench builds with its self-check passing; its picture was inspected.
- [ ] All suites green on 6000.0.85f1 and 6000.6.7f1 with no compiler warnings; screenshots inspected.
- [ ] The operator editor check passed.
- [ ] The operator play-check passed; tuned values are in `LevelRules` and entities.md.
- [ ] Code review: no open HIGH/MED.

---

## Notes to GDD

Synced at completion (GDD v0.12 → v0.13). The decisions themselves are already in the GDD. These are only what the build settles beyond them. **(proposal)** = confirmed or changed at Step 19.

1. **Fall gravity (developer reading):** "the player's base gravity" = the player's **rise gravity** (BaseGravity × the Player's Gravity); max fall = the Player's Max Fall Speed. The fall factor 1.75 > 1 keeps a rider on at every Gravity setting. Edit the entities.md § Trap Fixed line and the systems.md Fall bullet to say so.
2. **Tick order (systems.md § Traps, Moving solids; architecture.md § Time Model):** pending delays → **traps move**:
   - every moving trap advances one fixed step from its old state; arrival and landing snap exactly
   - riders are carried Y then X
   - leading-face pushes, combined by value
   - crush (nudge first)
   - moving-Hazard swept check
   - then the player's own X-then-Y move → triggers → death → checkpoints/exits → safe ground
   - A trap fired by its trigger starts moving on the next tick; a trap fired by its Delay moves on the tick the Delay ends.
3. **Riding details (systems.md, Riding):**
   - A trap going down keeps its rider while its descent speed is no more than the rider's ride speed. Ride speed starts at the player's fall speed on landing (0 if standing before the trap moved) and grows by the player's fall gravity up to Max Fall Speed.
   - Pull-away gives 6 ticks of coyote counted from the pull-away tick.
   - A rider standing partly on other ground isn't carried down.
   - With several carriers, the highest top wins.
   - Open question 1 may replace this with "always carried at or below Max Fall Speed".
4. **Crush nudge (systems.md, Crush forgiveness):** the nudge shifts at right angles to the push (up or down for a sideways push), up to 0.25 **(proposal)**, toward the smaller shift (ties up, left, right, down). It applies to every crush, including a rising trap lifting the player into a ceiling. Pushes in opposite directions on one axis crush (squeeze). Open question 2.
5. **Death at once in the trap phase:** a crush or a moving-Hazard hit while traps move ends the tick (the player doesn't step; the death spot is where it happened). Phase 3's appear crush still dies at the death check. Open question 3 may align them.
6. **A push applies the rope re-grab lockout** (0.2 s, as after a rope jump) to every push, appear push-out included (systems.md: "A push lets go of a rope" + "and can't grab it again for 0.2 s").
7. **Move:**
   - a zero offset doesn't move
   - the help-box time is rounded to 0.1 s
   - Move-end handle range ±30 per axis (entities.md already has it)
   - Speed default 8, Offset default 0 × −3 **(proposal)** → confirmed values
8. **Traps on an Exit:**
   - the trap's box is the Exit's box (sprite bounds), not snapped to the grid
   - Size and Tile are hidden on an Exit
   - **the default zone of a Trap on an Exit is the exit box grown by 1 tile on every side** (round 1, F6): the exit's touch uses the contact margin while a zone needs a real overlap, so a zone with the exit's own width lets a player walking in from the side win before the trap fires. A deviation from "one default zone for every action" (that rule is about actions; this one is about the object); the operator confirms it before the build (open question 5)
   - an exit can't be won on the tick its own trap fires
   - a Trap on a disabled Exit does nothing, with a warning (F2)
   - a Touch Vanish exit vanishes as it is touched (triggers before exits)
   - **A Trap on a Checkpoint does nothing, with a warning** (systems.md § Traps; replaces the Phase 3 "Exit or Checkpoint" note)
9. **Deviation:** the jump's ceiling-corner correction slides past cell corners only, not past a moving box's corner.
10. **Moving traps run regardless of the active radius** (not built yet); revisit with `world-systems`.
11. **entities.md § Trap:**
    - serialized names `offset` (Vector2Int), `speed` (float), in the order trigger, zone, action, offset, speed, delay, size, tile
    - add the new `LevelRules` rows (Move offset/speed defaults and ranges, `CrushNudge`)
    - drop "(proposal)" from Move/Fall values once confirmed
12. **architecture.md:**
    - § System Map: sim += `MovingBoxes` (the moving-box layer `TileGrid` carries), `TrapMotion`, `BoxPush`; runtime: `PlayerSession` interpolates traps
    - § Data Flow: the map include grows by Move end boxes
13. **Glossary.md:**
    - flip to **Active**: Moving-box core, Moving solid, Crush, Push-out
    - new **Draft**: Rider (carry), Pull-away, Ride speed, Leading face, Crush nudge, Move end, Landing outline, Runaway Exit
    - extend **Map bounds** with "and every Move trap's end"
14. **CLAUDE.md:** Build/run trap bench line ("a ~120 × 18 troll section with every Phase 3 and 4 trap, a checkpoint, a runaway exit and adversarial fixtures"). **conventions.md § Testing:** the moving-trap bench sections.
15. **Roadmap:** Milestone 2 `traps` complete → §0 row; remove the "Moving traps" item from §1.
16. **decisions.md:** ADR rows for the open-question answers and for notes 1–5 as settled at completion.
17. **framework.md:** `Handles.FreeMoveHandle` / `Slider2D` snapping on 6000.0 vs 6000.6; `Tilemap.tilemapTileChanged`; `EditorJsonUtility` for the old-data test.
18. **Deferred.md (Source = Phase 4):**
    - "`RebuildTrapCells` clears the whole grid on each start/stop/land tick". Trigger: `perf_check` or a profile showing it on a 500 × 200 map. Fix shape: restamp only the changed trap's area plus overlapping resting traps.
    - "Help-box Fall time uses the first Player found". Trigger: a scene with several Players (out of v1 scope) or a report.

**Open questions for the operator:**
1. **Pull-away on a constant-speed Move going down.** Recommended: keep the GDD's literal rule (one short hop, then ride, thanks to the ride speed); judge it at Step 19. Alternative: always carry a rider on a trap going down at or below Max Fall Speed (no hop; simpler; a slight departure from "pulls away").
   - Reviewer recommends: the literal rule, only with the F3 seed fix; the default Move (0 × −3 at 8) is itself a pull-away case, so every new Move trap a student rides shows the hop; judge it first at Step 19 (round 1, F12).
2. **Nudge on sideways pushes.** Recommended: at right angles (up or down) up to 0.25, the same rule for every crush. Alternative: nudge only vertical pushes (ceilings), as the GDD's example reads.
   - Reviewer recommends: right angles for every crush; one rule is easier to explain, and per F10 it only ever affects airborne or riding players (round 1, F13).
3. **Death at once in the trap phase.** Recommended: yes (it avoids stepping a player trapped inside a box). Phase 3's appear crush: leave it as is now (surgical); align it if the operator prefers one order.
   - Reviewer recommends: yes, as recommended; leave Phase 3's appear crush alone (round 1, F14).
4. **Bench size.** Recommended: widen the one bench to about 120 × 18 and keep the Phase 3 sections. Alternative: a second bench scene.
   - Reviewer recommends: one widened bench, Phase 3 sections and GUIDs untouched, with a default-zone runaway exit (round 1, F15).
5. **Default zone of a Trap on an Exit** (round 1, F6). Recommended: the exit box grown by 1 tile on every side, plus no win on the tick the exit's trap fires. Without it, a player walking up to a runaway exit with the default zone often wins before it runs. Alternative: keep "grown up 1" for exits too and rely on the no-win-on-fire-tick rule plus a help-box hint to drag the zone in front (the approach from the side still often wins first).

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. CLAUDE.md has no phase-status line; apply its Notes edits (the Build/run trap bench line).
3. Write `memory/logs/phases/phase4-log.md`: per step, the tuning table, the inspected pictures, the editor and play-check outcomes, and the operator's answers to the open questions.
4. Update `MEMORY.md` current state.
5. Apply the Notes to GDD (GDD v0.13, Changelog, Roadmap §0 row and Milestone 2 closure), decisions.md, framework.md, Glossary flips, and lessons-learned.md if a rule was learned.
6. **Deferred items:** file the Deferred.md entries listed in Notes (Source = Phase 4). Delete any entry whose trigger fired. See CLAUDE.md → Log Discipline.
7. Ask the operator, then commit all phase work as `v0.13: Phase 4 — moving traps: Move and Fall, riding, pushing, crush nudge, runaway exit, Scene-view previews`.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked, and at Steps 17 and 19, which are planned operator stops. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

## What I could run, what I verified, what I could not

**Could run:** a shell on the linked office PC over the connected `daedalus` folder (`cat`, `grep`, `sed`, `git log/status`), plus float32 arithmetic in Python in the cloud container. **Could not run:** Unity, either test suite, the bench generator, or anything in the Editor. Nothing below claims a test result.

**Verified against the code on the device** (working tree, HEAD `e1eaf7a`):
- `LevelStepper.cs` whole: tick order (delays + `ResolveAppeared` :137-145, player step :148, triggers + `ResolveAppeared` :151-171, death :175, checkpoints/exits :182-198, safe ground :201), `ResetTraps` presence rule :270 (`Present = Action == Vanish`, so the doc's "Move/Fall would start absent" is right), `Fire` :288 (`present = Action == Appear`, so the doc's "Move/Fall would act like Vanish" is right), `PushOut` :346-395 (no rope lockout today; right), `RebuildTrapCells` :397, `Inside`/`Touches` :422-434 (Zone = strict hitbox overlap; Touch = hitbox grown by `ContactMargin`).
- `PlayerStepper.cs`: counter rule "set on tick T is active on T+1…T+N" and the end-of-step decrement of counters not set this tick (:204-223); jump check before the move (:235); `Land` zeroes `VelocityY` (:639); walk-off coyote (:469-473); rope grab needs `RopeLockoutTicks == 0` (:396).
- `MovementInternals`: `CoyoteTicks` 6, `RopeLockoutTicks` 12, `CeilingCornerCorrection` 0.25, `FallGravityFactor` 1.75, `BaseGravity` 2·3.5/0.35² = 57.14. `MovementSettings`: Run Speed max 12, Gravity 0.25–2, Max Fall 2–24 (default 12). `LevelRules` constants are `const`, so `[Range(LevelRules.MinMoveSpeed, …)]` compiles.
- `TileCollision.cs`: `Support`'s integer-row early return (:120-124), `TryCornerCorrection` re-checks with `BoxIsFree` (:262), so box-aware `BoxIsFree` covers the corrected spot.
- `Trap.cs` (`[DisallowMultipleComponent]`, `[RequireComponent(SpriteRenderer)]`, six serialized settings + hidden `dataVersion`), `TrapLook.cs` (`IsIgnored` by `GetComponent`, `ToSpec`, `ApplyPlayLook`), `LevelCollector.cs` (`ElementBox` = sprite bounds, unsnapped), `PlayStartScan.FindActive` (keeps `isActiveAndEnabled` only), `Player.StartPlay` (exit boxes read before `ApplyPlayLook`; `BuildSettings` already `internal`), `Player.WarnLevel`, `TrapAuthoring.SyncLook` :61 and the bench :225 (`enabled = Action == Vanish`; the doc's bug claim is right), `PlayerSession` has the `Teleported` snap, `HelpTexts.TrapStory(Trap)`.
- Every MODIFY path in the File Manifest exists.
- systems.md § Traps (Move and Fall bullets), entities.md § Trap, decisions.md tail.
- Numbers recomputed in float32: 5 tiles at 8 → step 0.13333334, 37.499996 → **38** ticks ✓, 38/60 → "0.6 s" ✓; 3×4 at 30 → step 0.5, **10** ✓; 6 at 4 → 89.99999 → 90; 30 at 1 → **1800** (see F8); `MaxMoveSpeed/60` = 0.5 ✓; Fall tick-1 speed 0.952 = RiseGravity/60 ✓; FallGravity 100; `DeathBeatTicks` 24 ✓; Delay 0.5 s → fires in phase 3 of trigger tick + 30 ✓; rope lockout 12+1 set before the player step → blocked T…T+12, free T+13 ✓ (but see F4).
- Pull-away replay (default settings, Move 0×−3 at 8, trapezoid fall): the player lands back on the trap on tick 11 at −12 tiles/s with the trap 1.47 tiles down (before its 23-tick arrival) ✓, provided the ride speed is seeded correctly (F3). Faster-than-max-fall case: 16-tick arrival vs ~2.5 tiles fallen ✓.
- Swept-hazard margins: worst relative step 0.5 (trap) + 0.4 (player at Max Fall 24) = 0.9 per tick vertically and 0.7 horizontally, both under the 1-tile box plus the core, so the end-of-tick static check cannot be skipped by the player's own move ✓.

**Accepted on trust:** that `CollisionMapReader`'s tile → kind mapping can be reused for a small preview grid; Unity Handles snapping behaviour on 6000.0 vs 6000.6 (Notes 17 already files it).

---

## Blocking

### F1: The version numbers collide with v0.12, which already shipped
- **Type:** blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Source ("GDD version: v0.11", "decisions.md, last row"); Notes to GDD preamble ("GDD v0.11 → v0.12"); Post-Phase Cleanup 5 and 7 (commit `v0.12: Phase 4 — …`).
- **Defect:** HEAD is `e1eaf7a v0.12: class gallery …`, and `GDD.md`'s header already reads **v0.12**. Following the doc as written gives a second v0.12 Changelog entry and a second `v0.12:` commit. Also, the last row of decisions.md is now the class gallery's Rust + SQLite row, so "decisions.md, last row (Moving traps)" points at the wrong ADR.
- **Fix:** source v0.12, sync v0.12 → v0.13, commit `v0.13: Phase 4 — …`, and cite the ADR by its date and title rather than by position.

### F2: A Trap on a disabled Exit trips the "can't happen" assert at play start
- **Type:** blocking · **Category:** design · **Severity:** high (a student action breaks Play)
- **Location:** Step 10, `Player.cs` bullets ("A Trap on an Exit that is disabled or outside the exit list is treated as a plain trap: this can't happen, so assert it"); `TrapLook.IsOnExit`.
- **Defect:** `IsOnExit` follows today's `IsIgnored` and checks `GetComponent<Exit>() != null`, which is true for a disabled Exit. `LevelCollector.FindExits` goes through `PlayStartScan.FindActive`, which keeps only `isActiveAndEnabled` components. A student who unticks the Exit component's checkbox (an ordinary way to "turn it off" while testing) leaves the Trap enabled. `Array.IndexOf` then returns −1 and the assert throws at play start. Under CLAUDE.md's error-handling rule that is an expected, recoverable case, not a broken invariant.
- **Fix:** decide what a Trap on a disabled Exit does, and make `IsOnExit` agree with the exit list. The simplest choice is to ignore it like a Checkpoint trap (a disabled exit's runaway does nothing), optionally with a warning. Add a LevelWarningTests case for it.

### F3: The ride-speed seed reads 0, so a sinking floor stutters
- **Type:** blocking · **Category:** design · **Severity:** high (visible jitter on the default Move trap)
- **Location:** Step 5, "Down" ("It starts at the player's downward speed on the tick it landed on the trap"); Notes 3.
- **Defect:** the landing happens inside `stepper.Step` (phase 5), and `Land` sets `VelocityY = 0` (`PlayerStepper.cs:639`). Read after the step, "the player's downward speed on the tick it landed" is 0. So the next tick's ride speed is 0 + FallGravity/60 = 1.67, which is less than 8. The player pulls away again, falls, lands, and pulls away again, hopping all the way down any Move going down faster than 1.67 tiles/s, including the **default** Move (0 × −3 at 8). Step 5's own test ("rides it to the end without another pull-away") would fail, but the doc gives the implementer no correct source to fix it from.
- **Fix:** name the source: LevelStepper stashes the player's `VelocityY` before `stepper.Step` and seeds the ride speed with `max(0, −preStepVy)` when the post-step state is standing on a moving box it wasn't standing on before. In my replay that seed is 12 (tick 11), which carries at 8.

### F6: With the default zone, a runaway exit is reached before it runs
- **Type:** blocking · **Category:** design · **Severity:** high (the headline exit troll fails with defaults)
- **Location:** Step 8 (Touch Vanish only); Step 10 `DefaultZoneLocal(Trap)` ("the exit box grown up 1"); Notes 8; Step 12's exit stories.
- **Defect:** two timings meet here.
  1. `Touches` (the win check) uses the hitbox grown by `ContactMargin` 0.0625, while a Zone trigger needs a strict hitbox overlap (`LevelStepper.cs:426` vs `:433`). The default exit zone has exactly the exit's horizontal extent. A player walking in from the side touches the exit (with the margin) **on or before** the tick their hitbox enters the zone. At Run Speed 6 (0.1 tile/tick) there is often a tick with a gap in (0, 0.0625), so the player wins before the trap fires.
  2. Even on the same tick, a Move/Fall exit fired in the triggers phase only starts moving next tick, while the exit check on this tick still sees the start box, so the player wins. The same holds for every **Touch**-triggered Move/Fall exit: the help box would say "The exit runs 6 tiles right in 0.8 s after the player touches it", but touching it wins.

  Only a hand-dragged zone placed in front works, which is what the bench and every test use ("zone 3 tiles before the exit"), so the suites pass while a student's first runaway exit doesn't run. A default-zone Vanish exit also often fails to vanish (case 1).
- **Fix options:** (a) an exit trap's default zone is the exit box grown by 1 on **every** side (it fires about a tile out, well before the contact margin; at 0.2 tile/tick the exit has started moving before it can be reached); (b) an exit can't be won on the tick its own trap fires or while that trap is fired but not yet moving; (c) a help box or warning for Touch + Move/Fall on an Exit. I'd do (a) + (b), and add tests for a default-zone runaway exit approached from the side at Run Speed 6 and 12.

## Non-blocking

### F4: The +1 rope lockout is wrong for push-outs after the player step
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5 `ApplyPush` ("RopeLockoutTicks + 1. The stepper counts it down on the push tick"); Step 6 rope test ("the same for a Phase 3 Appear push-out").
- **Defect:** the +1 is right only when the push comes **before** `stepper.Step` (phase 4, or a delay-fired Appear at `:145`). A trigger-fired Appear's `PushOut` runs in `ResolveAppeared` at `:171`, after the step, so nothing counts down on the push tick. Its lockout then blocks T+1…T+13 and frees T+14, and the Appear rope test (blocked through +12, free at +13) fails for the common, trigger-fired case.
- **Fix:** `ApplyPush` takes whether the player still steps this tick (+1 only then), and the Appear test covers both a delay-fired and a trigger-fired push-out.

### F5: A trigger-fired Move/Fall leaves a one-tick hole in collision
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low–medium
- **Location:** Step 4 `Fire` + "Start moment" ("At the tick it starts, its cells leave the cell layer and its box enters the box layer"); Step 4 test ("its cells are gone on the start tick").
- **Defect:** `Fire` sets `Motion = Moving` in the triggers phase of tick T. `ResolveAppeared` then calls `RebuildTrapCells`, which stamps "Resting + Present only", so the cells vanish at T. The box only enters at phase 4 of T+1. In between, T's death check misses a just-fired Hazard Move overlapping the core, and at T+1 a delay-fired Appear push-out (phase 3) can push the player into the vacated cells. Phase 4 then adds a box already overlapping the player, and for an axis with dx or dy 0 the leading-face rule's "sign of dy" is 0.
- **Fix:** a fired-but-not-started state that stays stamped until phase 4 (or have `RebuildTrapCells` stamp Moving traps with `MoveTick == 0`), and assert the cells are still there at T.

### F7: A Fall exit whose sprite dips into a ledge falls through it
- **Type:** non-blocking · **Category:** design · **Severity:** medium
- **Location:** Step 2 `TryLandingRow` / `FallStep` ("rows whose top lies in (new bottom, old bottom + ε]"); Step 8 Fall on an exit.
- **Defect:** exit boxes are unsnapped sprite bounds (`LevelCollector.ElementBox`). Students place exits by eye, so a sprite bottom at 2.95 on a ledge whose top is 3 is likely. The row the bottom is inside (top 3) isn't "at or below" the bottom, so the scan skips it. The exit then lands one row lower (buried) or, on a one-row ledge over a pit, falls out of the map, and the preview faithfully shows it. Tile traps are whole cells and unaffected.
- **Fix:** for an exit trap, count a painted Solid/Climbable row the box's bottom is inside as already landed (snap the bottom up to its top), and add an ExitTrapTests case.

### F8: The bit-exact test stops before the move arrives
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 2 tests ("Over 1000 ticks of a 30-tile move at speed 1, the end is bit-exact").
- **Defect:** 30 / (1 × 1/60f) = 1799.9999, so `MoveTicks` = 1800. At tick 1000 the trap is mid-move and there is no end to compare.
- **Fix:** run 1800+ ticks (assert arrival on tick 1800, not 1799).

### F9: After the pull-away tick, `CoyoteTicks` reads 5, not 6
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5 tests ("pulls away on its first moving tick (assert Air, CoyoteTicks 6)").
- **Defect:** LevelStepper sets 6 before the player step. The stepper then decrements every counter its own `SetThisTick` didn't set (`PlayerStepper.cs:204-207`), so the state after `Step` holds 5. The jump window itself (T…T+5, six ticks) and the 6th/7th-tick test are right.
- **Fix:** assert 5 after the tick, or assert the window only.

### F10: The sideways-nudge fixture can't happen on the ground
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6 test "Sideways push with nudge (… a horizontal pusher whose top is 0.2 above the feet …)"; Notes 4; open question 2.
- **Defect:** a horizontal Move starts from whole cells with whole-tile offsets, so its top is always an integer. A player standing on painted ground or a resting trap has integer feet. A pusher "0.2 above the feet" therefore exists only for an airborne player (or one riding a Fall). The fixture as worded can't be built.
- **Fix:** make it a jumping player whose feet are 0.2 below the pusher's top at the push tick. This also scopes open question 2: the sideways nudge only ever helps airborne or riding players.

### F11: Small spec slips
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location / defect:**
  - Step 2: the `FallStep` signature has no rise-gravity or max-fall parameters although the text says they are "passed in as parameters". It also returns `bool` and has `out bool landed`; pick one.
  - Step 4: "`RoundToInt(MinX/MinY)`" in the sim. `Mathf.RoundToInt` is UnityEngine, and the sim has `noEngineReferences`. Say `(int)MathF.Round` (or `MathF.Floor(v + 0.5f)`, matching `TrapLook.CellMin`).
  - Step 4: `Inside(i)` for a **Touch** trigger on a Gone trap has no box. State that it is false.
  - Step 7: a strip passing "0.2 + ε outside the core" is ε outside the **hitbox**, so it never tests the inset. Use ε outside the core (inside the hitbox), mirroring Step 3's ±ε pair.

## Recommendations (open questions; the operator decides)

### F12: Open question 1 (pull-away on a Move going down)
- **Type:** recommendation · **Category:** design
- Keep the GDD's literal rule as recommended, but only with F3 fixed. Note that the **default** Move (0 × −3 at 8) is a pull-away case, so every new Move trap a student rides shows the hop. Make that the first thing judged at Step 19.

### F13: Open question 2 (nudge on sideways pushes)
- **Type:** recommendation · **Category:** design
- Right angles for every crush, as recommended: one rule is easier to explain. Per F10, in practice it only affects airborne or riding players, so the alternative buys little simplicity.

### F14: Open question 3 (death at once in the trap phase)
- **Type:** recommendation · **Category:** design
- Yes, as recommended. Leave Phase 3's appear crush alone (surgical).

### F15: Open question 4 (bench size)
- **Type:** recommendation · **Category:** design
- One widened bench, as recommended. Keep the Phase 3 sections and GUIDs untouched, and add a default-zone runaway exit fixture (F6) beside the hand-zoned one.

---

## Reviewer's view

I would build this after the four blockers. The sim design (moving-box layer, carry Y then X, leading-face push, swept hazard, exact snapping) holds up against the code I traced, and its numbers re-derive. The blockers sit at the seams with existing code. F1 is the version that already shipped. F2 is the enabled-component scan vs `GetComponent`. F3 is `Land` zeroing the speed the ride rule reads. F6 is the contact margin vs strict zone overlap, plus the next-tick start, which together defeat the default runaway exit. F4, F5 and F7–F11 are cheap folds.

---

## Review round 2

## What I could run, what I verified, what I could not

**Could run:** the device shell over the `daedalus` folder (`sed`, `grep`), as in round 1. **Could not run:** Unity, the suites, the bench. Nothing here is a test result.

**Delta read:** `log.md` round 1 dispositions in full, plus every changed passage they point to: Source (v0.12), Step 4 (Starting state, rest-cell rounding, start moment), Step 5 (`ApplyPush` flag, ride-speed seed), Step 8 (no win on the fire tick, new ExitTrapTests), Step 10 (`IsOnExit` / `TrapOnDisabledExit`), Notes 8, open questions 1–5 with my recommendations recorded, and Post-Phase (v0.13).

## Round 1 findings: status

| Id | Was | Now | Checked |
| --- | --- | --- | --- |
| F1 | blocking | **closed** | Source v0.12, sync v0.12 → v0.13, commit `v0.13: Phase 4 — …`, ADR cited by title. |
| F2 | blocking | **closed** | `IsOnExit` is enabled-Exit-only and matches `FindActive`; the assert is gone; a Trap on a disabled Exit is ignored with `TrapOnDisabledExit`; manifest, `FindTraps`, gizmos, `WarnLevel` and tests all updated. One edge left over, see F16. |
| F3 | blocking | **closed** | The seed is the pre-step `VelocityY` (`max(0, −preStepVy)`) when the player newly stands on a moving box. Notes 3 ("fall speed on landing") still reads correctly against it. |
| F6 | blocking | **closed** | (a) the default exit zone is the exit box grown 1 on every side, so it fires 1 − 0.0625 = 0.9375 tile before a touch; (b) no win on the fire tick. Arithmetic re-checked: at Run Speed 6 (0.1/tick) a Move at 8 (0.133/tick) pulls away; at 12 (0.2/tick) the fire gap is in [0.8, 1), and closing at 0.067/tick catches it about 1.5 tiles out, never at the start box. That matches the new test's claim. (a) is correctly raised as open question 5, since it bends "one default zone for every action". |
| F4 | non-blocking | closed | `ApplyPush(…, playerStepsThisTick)`; both Appear paths tested. |
| F5 | non-blocking | closed | The `Starting` state stays stamped until the first phase-4 advance. Checked against the delay-fired same-tick start, zero offset, the rider's "just-started" old box, and the rebuild trigger. No hole is left, and no double rebuild. |
| F7 | non-blocking | closed | A bottom already inside a painted row lands on its top. Tile traps have integer bottoms, so they are unaffected. |
| F8–F11 | non-blocking | closed | As listed in the log. |
| F12–F15 | recommendation | recorded | Beside open questions 1–4. |

**No new seam from the folds** except F16 below. Blocking id set: round 1 {F1, F2, F3, F6} → round 2 {} (converged).

## New

### F16: Prefab assets would read as "Exit turned off" in the inspector
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 10 (`IsOnExit`: "true only for an enabled Exit on an active object (`isActiveAndEnabled` …)"); Step 13's inspector use of `IsOnExit` / `TrapOnDisabledExit`.
- **Defect:** `isActiveAndEnabled` is false for any component on a prefab **asset** (not in a scene), because `activeInHierarchy` is false there. CLAUDE.md expects students to save their own prefabs. Selecting a runaway-exit prefab in the Project window would show the false warning "This Trap's Exit is turned off", show Size/Tile, and tell the tile story instead of the exit's.
- **Fix:** use `exit != null && exit.enabled` for both edit time and play start. At play start it is equivalent, because the Trap shares the Exit's GameObject: if the Trap was found by `FindActive`, the object is active in the hierarchy, so `exit.enabled` matches the exit list. Add a TrapLayoutTests case on a prefab-asset instance (or an inactive object) asserting `IsOnExit` follows `enabled`.

## Late-pushback test

Asked "what do you think?", my answer contains nothing beyond F16 and the recorded recommendations. Two conditions stay true after approval: the play-feel claims (the pull-away hop, crush fairness) are judged only at Step 19, and open question 5 needs the operator's answer before Step 10 builds the exit default zone.

## Reviewer's view

I would build this as written, with F16 folded on close. Every round 1 blocker (F1, F2, F3, F6) is fixed at its root rather than patched in tests, and the folds (the Starting state, the ride seed, the exit zone and fire-tick rule) agree with each other and with the code paths I traced. Approved.
