# Phase 3: Death and Respawn

## Overview

**Where things stand:** You can run and jump through a test level, but nothing can hurt or trick you. A pit just sends you back to the start, there is no goal, and the camera stands still.

**What this phase changes:**
- Spikes and pits kill: a short flash, then the player is back at once.
- The player comes back where they last stood safely, or at the last checkpoint.
- A death counter shows on screen; reaching the exit shows a win screen.
- A new Trap part makes floors that drop away and spikes or blocks that pop out. Every death puts them back.
- The camera follows the player.
- While building, each trap shows what it will do, in one sentence.

**How you'll know it worked:** The operator plays a short level full of tricks, falls for each one, is back in a blink, and reaches the exit.

**Not in this phase:** traps that move or fall (the next phase), and saving progress at checkpoints.

---

## Source
- **GDD sections:**
  - `systems.md` § Combat & Damage (Hazards, Safe ground), § Death and respawn, § World & Level Elements, § Traps, § Camera (minimal part only), § Editor Tools (gizmos, help boxes, warnings)
  - `entities.md` § Player tunables (On hazard), § Placeable elements (Checkpoint, Exit, Trap), § Trap, § Terrains (kind 4)
  - `architecture.md` § System Map, § Time Model, § Data Flow
  - `conventions.md`
- **Decisions:**
  - `decisions.md` rows of 2026-11-02 (troll levels first; one Trap component; death and respawn rules)
  - the 16 rules the operator settled on 2026-11-02 ("all recommended"). These are not in the GDD yet; **this prompt is their home until completion** (Notes to GDD).
- **GDD version:** v0.09.
- **Depends on:** Phase 2 `movement-runtime` (v0.08). Its code is the contract:
  - `TileGrid`, `TileKind`, `TileCollision`, `StartPlacement`
  - `PlayerStepper(grid, ropes, settings)` with `CreateState` / `Step` / `ApplySettings` / `ChangeAbilities`; `PlayerState.Teleported`
  - `FixedStepAccumulator`
  - `Player`, `PlayerSession`, `PlayStart`, `PlayerControls`, `PlayerWarnings`, `CollisionMapReader`, `RopeCollector`, `DaedalusTile` / `TileCollisionKind`
  - the test helpers `AsciiLevel`, `PlayerScenario`, `TestLevel`, `MovementPlayTest`
  - If a name differs from this prompt, the code wins; note it in the phase log.

## File Manifest

Package root `P/` = `Packages/com.orcapafea.daedalus/`. Every new file and folder is committed with its `.meta`.

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `P/Runtime/Sim/TileKind.cs` | `Hazard = 4`; fix the stale "combat-and-enemies" comment |
| MODIFY | `P/Runtime/TileCollisionKind.cs` | `Hazard = 4`; same comment fix |
| MODIFY | `P/Runtime/DaedalusTile.cs` | `ToSimKind` maps Hazard; tooltip mentions Hazard |
| NEW | `P/Runtime/Sim/LevelRules.cs` | The one source for death, respawn and trap numbers |
| NEW | `P/Runtime/Sim/Box.cs` | Float box (min/max) with strict overlap |
| NEW | `P/Runtime/Sim/RespawnRule.cs` | `SafeGround = 0`, `Checkpoint = 1` |
| NEW | `P/Runtime/Sim/LevelPhase.cs` | `Playing`, `Dying`, `Won` |
| NEW | `P/Runtime/Sim/TrapTriggerKind.cs` | `Zone = 0`, `Touch = 1` |
| NEW | `P/Runtime/Sim/TrapActionKind.cs` | `Vanish = 0`, `Appear = 1` (2 Move, 3 Fall reserved for phase 4) |
| NEW | `P/Runtime/Sim/TrapSpec.cs` | One trap in sim cells: box, kind, action, trigger, zone, delay ticks |
| NEW | `P/Runtime/Sim/LevelStepper.cs` | The world step around `PlayerStepper` |
| MODIFY | `P/Runtime/Sim/TileGrid.cs` | Trap-cell layer: `GetPainted`, `SetTrapCell`, `ClearTrapCells` |
| MODIFY | `P/Runtime/Sim/TileCollision.cs` | `CoreTouchesHazard`, `StandsFullyOnPainted` |
| MODIFY | `P/Runtime/Sim/StartPlacement.cs` | A spot whose inset core touches a hazard is not free |
| MODIFY | `P/Runtime/Sim/PlayerStepper.cs` | Fall-out reset removed (pits move to the world step) |
| MODIFY | `P/Runtime/Sim/PlayerState.cs` | `StartX` / `StartY` removed; `Teleported` doc |
| MODIFY | `P/Runtime/Sim/FixedStepAccumulator.cs` | `Reset()` (no allocation on restart) |
| NEW | `P/Runtime/OnHazard.cs` | Serialized enum with Inspector labels |
| NEW | `P/Runtime/PlayerHealthSection.cs` | Health section: `onHazard` |
| MODIFY | `P/Runtime/Player.cs` | Health section; level wiring; include bounds; camera, HUD, burst; play again |
| MODIFY | `P/Runtime/PlayerSession.cs` | Drives `LevelStepper`; exposes phase, deaths, traps, checkpoints |
| MODIFY | `P/Runtime/PlayerControls.cs` | Play-again action; `ClearLatch()` |
| MODIFY | `P/Runtime/PlayerWarnings.cs` | New warning texts; `MovedStart` mentions spikes |
| MODIFY | `P/Runtime/PlayStart.cs` | Doc: every start source re-arms traps and resets the counter |
| NEW | `P/Runtime/PlayStartScan.cs` | Generic play-start `FindObjectsByType` + active filter (the version split in one place) |
| MODIFY | `P/Runtime/RopeCollector.cs` | Uses `PlayStartScan` |
| NEW | `P/Runtime/LevelCollector.cs` | Sorted Trap / Checkpoint / Exit scan, world bounds, sim conversion |
| NEW | `P/Runtime/Checkpoint.cs` | Checkpoint component |
| NEW | `P/Runtime/Exit.cs` | Exit component |
| NEW | `P/Runtime/TrapTrigger.cs` | Serialized enum with labels (Zone 0, Touch 1) |
| NEW | `P/Runtime/TrapAction.cs` | Serialized enum with labels (Vanish 0, Appear 1) |
| NEW | `P/Runtime/Trap.cs` | Trap component |
| NEW | `P/Runtime/TrapLook.cs` | Box math shared by runtime and editor; the Play-mode renderer look |
| NEW | `P/Runtime/HelpTexts.cs` | The trap's story and the Checkpoint / Exit notices, built from the runtime's settings |
| NEW | `P/Runtime/CameraFollow.cs` | Minimal snap follow clamped to the map |
| NEW | `P/Runtime/PlayHud.cs` | uGUI counter and win screen, built in code |
| NEW | `P/Runtime/DeathBurst.cs` | Death-beat blink and burst (pooled) |
| MODIFY | `P/Runtime/AssemblyInfo.cs` | `InternalsVisibleTo("Orcapafea.Daedalus.Editor")` |
| MODIFY | `P/Runtime/Orcapafea.Daedalus.asmdef` | Reference `UnityEngine.UI` |
| MODIFY | `P/package.json` | `com.unity.ugui` 2.0.0 |
| NEW | `P/Editor/TrapAuthoring.cs` | Fit zone, snap to grid, look sync, zone follows Size (Undo-recorded) |
| NEW | `P/Editor/TrapEditor.cs` | Trap inspector: mode-only fields, story, warnings, buttons, zone handles |
| NEW | `P/Editor/TrapGhostGizmo.cs` | Edit-mode faded ghost for Appear traps |
| NEW | `P/Editor/CheckpointEditor.cs` | Notice help boxes (including "no effect while Return to safe ground") |
| NEW | `P/Editor/ExitEditor.cs` | Notice help box |
| MODIFY | `P/Tests/EditMode/Movement/AsciiLevel.cs` | `^` = Hazard |
| NEW | `P/Tests/EditMode/Level/LevelScenario.cs` | Drives the real `LevelStepper.Step` |
| NEW | `P/Tests/EditMode/Level/LevelRulesTests.cs` | Pins every `LevelRules` value |
| NEW | `P/Tests/EditMode/Level/HazardTests.cs` | Non-blocking hazard, inset core, no skip at max speeds |
| NEW | `P/Tests/EditMode/Level/DeathTests.cs` | Pit, beat, counter, input ignored, respawn, Teleported |
| NEW | `P/Tests/EditMode/Level/SafeGroundTests.cs` | Painted only, outside triggers and trap boxes |
| NEW | `P/Tests/EditMode/Level/CheckpointExitTests.cs` | Respawn point, win, death beats exit, play-again lock |
| NEW | `P/Tests/EditMode/Level/TrapTests.cs` | Triggers, Delay, re-arm, reset, painted wins, overlap |
| NEW | `P/Tests/EditMode/Level/TrapPushTests.cs` | Appear push-out and crush; start inside a trap |
| MODIFY | `P/Tests/EditMode/Movement/RunAndFallTests.cs` | Fall-out tests move to `DeathTests`; the stepper alone now falls |
| MODIFY | `P/Tests/EditMode/Movement/StartPlacementTests.cs` | Hazard-aware placement |
| MODIFY | `P/Tests/EditMode/Movement/TileGridTests.cs` | Trap layer; painted wins |
| MODIFY | `P/Tests/EditMode/Movement/StepAllocationTests.cs` | `LevelStepper.Step` zero allocation |
| MODIFY | `P/Tests/EditMode/FixedStepAccumulatorTests.cs` | `Reset()` |
| MODIFY | `P/Tests/EditMode/Runtime/TileAssetTests.cs` | Five kinds; Hazard label; 5 throws |
| MODIFY | `P/Tests/EditMode/Runtime/PlayerLayoutTests.cs` | `health.onHazard`, sections, tooltips (14) |
| MODIFY | `P/Tests/EditMode/Runtime/PlayerSessionTests.cs` | Snap after the beat; restart resets the level |
| MODIFY | `P/Tests/EditMode/Runtime/CollisionMapReaderTests.cs` | Hazard tile read |
| MODIFY | `P/Tests/EditMode/PackageManifestTests.cs` | Expected set += ugui 2.0.0 |
| MODIFY | `P/Tests/EditMode/AssemblyDirectionTests.cs` | The runtime may reference `UnityEngine.UI` |
| NEW | `P/Tests/EditMode/Runtime/TrapLayoutTests.cs` | Trap / Checkpoint / Exit names, defaults, ranges, tooltips, menus, box math |
| NEW | `P/Tests/EditMode/Runtime/HelpTextsTests.cs` | Story sentences per setting combination |
| NEW | `P/Tests/EditMode/Runtime/TrapAuthoringTests.cs` | Fit zone, snap, look sync, zone follows Size, Undo |
| MODIFY | `P/Tests/PlayMode/Movement/TestLevel.cs`, `TestLevelOptions.cs` | `^`, traps / checkpoints / exits, main-camera option |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerPlayTests.cs` | `StartX` use; invalid kind → 5 |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerRenderTests.cs` | Reset snap after the beat |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerWarningTests.cs` | Fall case through the beat |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerAllocationTests.cs` | Window includes death, trap fire, counter change, win, play again |
| MODIFY | `P/Tests/PlayMode/Movement/PlayerControlsTests.cs` | Play-again action |
| NEW | `P/Tests/PlayMode/Traps/TrapPlayTests.cs` | Troll tour with screenshots |
| NEW | `P/Tests/PlayMode/Traps/HudAndWinTests.cs` | Counter, win screen, play again |
| NEW | `P/Tests/PlayMode/Traps/CameraFollowTests.cs` | Follow, clamp, cut |
| NEW | `P/Tests/PlayMode/Traps/LevelWarningTests.cs` | Each new warning once |
| NEW | `Assets/Dev/Editor/TrapTestSceneBuilder.cs` | Generates the trap bench scene |
| NEW (generated) | `Assets/Dev/TrapTest/…` | `TrapTest.unity`, ground / spike / checkpoint / exit sprites and tiles, palette |

Doc edits are listed in `## Notes to GDD`.

---

## Conventions for every step

- **Namespaces:**
  - `Orcapafea.Daedalus.Sim`, `Orcapafea.Daedalus`, `Orcapafea.Daedalus.Editor`
  - `Orcapafea.Daedalus.Dev` (dev code), `Orcapafea.Daedalus.Tests`
  - One primary type per file; nested private helpers are fine (the `SetThisTick` precedent).
- **Sim:**
  - No `UnityEngine`, clock, random, LINQ or static mutable state (`SimPurityTests` scans new files automatically).
  - `LevelStepper.Step` and everything it calls allocate nothing; arrays are sized in the constructor.
- **Frame loop** (`Player.Update` → `RunFrame` and below):
  - no allocations, LINQ, closures, boxing, `GetComponent`, `Find` or logging
  - play-start work in `Start`
  - strings shown on screen are cached at play start
- **Runtime boundaries:**
  - The runtime makes no gameplay decision. It converts the scene into sim data, feeds the sim and draws sim state.
  - Play-mode writes to our own objects (the trap's renderer look, its transform snap, the checkpoint tint, the camera, the HUD objects it creates) are allowed; Unity discards them on Stop.
  - No authored asset is ever written.
- **Editor tools contain no gameplay logic** (Architecture rule 3):
  - The story, the box math and the default zone come from runtime and sim functions the play path also uses.
  - Every authoring write goes through `Undo`.
- **Defaults:**
  - Every number comes from `LevelRules` (or `MovementSettings` / `MovementInternals`); no literal default in runtime or editor code.
  - Presentation-only numbers (burst speed, bump size, dim alpha, ghost alpha) are named consts in their runtime class, marked presentation.
- **Serialized data:**
  - Enum values are explicit and never renumbered; value 0 is the default.
  - New components carry a hidden `dataVersion` = 1.
  - Field names are fixed now.
- **Warnings:**
  - `Debug.LogWarning(text, context)` once at play start, with the object to fix as context.
  - Plain words; never names code.
  - Every text lives in `PlayerWarnings`. Help-box notices live in `HelpTexts`, and the editor shows the same strings.
- **During a step:** run `python Tools/unity_tests.py --editor 6000.6` with the relevant `--platform`; both editors run at Step 17. Close the dev project first.
- **Tests:**
  - Assert through the real entry points (`LevelStepper.Step`, `PlayerSession.Tick`, the `Player` lifecycle).
  - Every precondition is asserted (e.g. "the trap fired"); every breakpoint test asserts both sides.
  - Every new guard is **seen failing** once on a planted change (expectation only), then reverted.
  - Screenshots: `<unity version>-traps-<name>.png`.

---

## Steps

### Step 1: Hazard tile kind (4)
- **MODIFY `P/Runtime/Sim/TileKind.cs`**: add `Hazard = 4`, with a doc line: "Non-blocking; touching it with the inset core kills (`traps`)". The header comment says values are saved and never renumbered. Remove "4 is reserved for hazard (combat-and-enemies)".
- **MODIFY `P/Runtime/TileCollisionKind.cs`**: `Hazard = 4` (label "Hazard"); same comment fix.
- **MODIFY `P/Runtime/DaedalusTile.cs`**:
    - `ToSimKind` maps Hazard → `TileKind.Hazard`; undefined values still throw.
    - Tooltip appends: "Hazard (spikes: touching it kills the player)."
- **Collision is unchanged on purpose.** `TileCollision` tests kinds explicitly, so Hazard neither blocks, supports, nor walls. Re-read `Blocks`, `RowSupport`, `ColumnBlocks`, `IsClimbableAt` and the `LimitClimbUp` loop at execution and confirm each ignores 4. `RopeSegment.SnapTopTo` and `Player.WarnRopeTops` likewise.
- **MODIFY `P/Tests/EditMode/Movement/AsciiLevel.cs`**: `^` = Hazard; update the symbol list in its doc and in `conventions.md` (Notes).
- **Tests:**
    - `TileAssetTests`: the enum has exactly five members, values 0–4. `ToSimKind` maps 0–4 one to one; `(TileCollisionKind)5` throws (replaces the kind-4 throw case).
    - `PlayerPlayTests`' play-start-error case uses `(TileCollisionKind)5`.
    - `TileCollisionTests`: a box sweeps through `^` cells on both axes; `Support` over `^` is None; `WallGap` ignores `^`.
    - `CollisionMapReaderTests`: a Hazard `DaedalusTile` reads as `TileKind.Hazard`.
- **Verify:** `[automated]` EditMode green; `[grep]` no "reserved for hazard" or "combat-and-enemies" left in `TileKind.cs` / `TileCollisionKind.cs`.

### Step 2: `LevelRules`, the one source for death, respawn and trap numbers
- **NEW `P/Runtime/Sim/LevelRules.cs`**: `public static class LevelRules`. Each value is defined only here. Values marked **(proposal)** are tuned at Step 19.
    - `DeathBeatSeconds = 0.4f` **(proposal)** → `DeathBeatTicks` = `(int)(s * 60 + 0.5f)` = 24
    - `HazardInset = 0.2f` **(proposal)**: tiles taken off each side of the player's hitbox to make the hazard core. Core = 0.35 × 1.1.
    - `ContactMargin = 0.0625f` **(proposal)**: tiles the hitbox grows by for "touch"
    - `DefaultZoneGrowUp = 1f` **(proposal)**: the default zone is the trap's box grown upward by this
    - `DefaultTrapDelay = 0f`, `MinTrapDelay = 0f`, `MaxTrapDelay = 3f`
    - `DefaultTrapWidth = 3`, `DefaultTrapHeight = 1`, `MinTrapSize = 1`, `MaxTrapSize = 30` **(proposal)**
    - `WinInputLockSeconds = 1f` → `WinInputLockTicks` = 60
    - `public static int SecondsToTicks(float seconds)`: the same rounding as `MovementInternals`, clamped ≥ 0, non-finite → 0
    - `public static Box DefaultZone(Box trapBox)`: the box with `MaxY + DefaultZoneGrowUp`
- **NEW `P/Runtime/Sim/Box.cs`**:
    - `public readonly struct Box`: `MinX, MinY, MaxX, MaxY` (floats)
    - `Box(float minX, float minY, float maxX, float maxY)` throws if max < min
    - `Overlaps(in Box other)`: strict, positive area (touching edges don't overlap)
    - `Grown(float margin)`, `Contains`
    - `static Box Hitbox(float feetX, float feetY)` (0.75 × 1.5 from `MovementInternals`)
- **Tests (`LevelRulesTests.cs`):**
    - every value and derived tick count pinned
    - `DefaultZone` of (2, 1)–(5, 2) is (2, 1)–(5, 3)
    - `SecondsToTicks(0.5f)` = 30; −1 and NaN → 0
    - `Box.Overlaps`: edge contact false, 0.001 overlap true
- **Verify:** `[automated]` EditMode green.

### Step 3: Hazard queries and hazard-aware start placement
- **MODIFY `P/Runtime/Sim/TileCollision.cs`**:
    - `public static bool CoreTouchesHazard(TileGrid grid, float x, float y)`: true when any cell overlapped (half-open, as elsewhere) by the inset core reads `TileKind.Hazard`. The core is the hitbox shrunk by `HazardInset` per side.
    - `public static bool StandsFullyOnPainted(TileGrid grid, float x, float y, float halfWidth)`:
      - `y` is on an integer row (within the contact epsilon)
      - every column the hitbox covers has a **painted** (`GetPainted`, Step 6) Solid, Climbable or OneWay cell directly below
      - Until Step 6 lands, it reads `Get`; Step 6 switches it.
- **MODIFY `P/Runtime/Sim/StartPlacement.cs`** (doc lines say both rules and why):
    - A spot is free only if `BoxIsFree` **and** `!CoreTouchesHazard`.
    - **A moved spot must also stand on support** (`TileCollision.Support` ≠ None at the candidate; hazards are never support). Without this, a start on a floor-mounted spike strip moves one row up, into mid-air over the spikes, falls back in and dies forever (handshake round 1, F1). No supported spot within the radius → `Found = false`, which raises the existing `NoFreeStart` warning.
    - An **unmoved** start stays as it is, mid-air included (Phase 2 behaviour; a student who places one over a pit can see why).
    - Every Phase 2 `StartPlacementTests` case already moves onto a floor or the slab, so they stay green unchanged; re-run them to confirm.
- **MODIFY `P/Runtime/PlayerWarnings.cs`**: `MovedStart` becomes "The Player started inside a wall or on spikes, so it was moved to the nearest free spot. Move the Player in the Scene view to fix this."
- **Tests (`HazardTests.cs`, using `AsciiLevel` + `TileCollision` directly):**
    - core edges against a `^` cell: 0.2 − ε away → no; 0.2 + ε → yes, on all four sides
    - **no skip** (item 1): a one-row floating `^` strip at `MaxFallSpeed` 24 (0.4 tile/tick) and at wall-jump push 18 t/s against a one-column `^` wall. Sample every tick of the real stepper's fall/flight over 64 sub-tick start offsets and assert the core touched the strip in every case. Assert the count of cases checked.
    - `StandsFullyOnPainted`: centred on a floor → true; one column over a gap → false; on a one-way → true; mid-air → false
- **Tests (`StartPlacementTests`):**
    - a start on a `^` cell moves to the nearest free, non-hazard, **supported** spot; a start beside (not touching) spikes stays
    - a Player placed on a floor-mounted spike strip (floor row f running 3 cells past each end of the strip, spikes row f+1, 4 wide) is moved onto non-hazard ground beside the strip, not one row up; through `LevelScenario` it then lives for 120 idle ticks
    - a moved start with no supported free spot in the radius → `Found` false
    - a tie case where **both** the up and the down candidate stand on support (e.g. a slab with a floor below), so the up-then-left tie-break stays tested now that the Phase 2 slab case wins "up" by eligibility (round 2 side note)
- **Verify:** `[automated]` EditMode green.

### Step 4: The world step: death, beat, respawn, counter (no traps yet)
- **MODIFY `P/Runtime/Sim/PlayerStepper.cs`**:
    - Remove the fall-out reset block. Pits move to the world step, and the stepper alone lets the player fall below row 0.
    - `CreateState` no longer sets start fields.
    - Update the class and `CreateState` docs ("Falling out returns here" goes).
- **MODIFY `P/Runtime/Sim/PlayerState.cs`**:
    - remove `StartX` / `StartY` (now unused)
    - `Teleported` doc: "True only on the tick the world step replaced the state (a respawn), so the renderer snaps."
- **NEW `RespawnRule.cs`** (`SafeGround = 0`, `Checkpoint = 1`) and **NEW `LevelPhase.cs`** (`Playing = 0`, `Dying = 1`, `Won = 2`).
- **NEW `P/Runtime/Sim/LevelStepper.cs`**: `public sealed class LevelStepper`, pure and allocation-free in `Step`.
    - **Constructor:** `(TileGrid grid, RopeSegment[] ropes, TrapSpec[] traps, Box[] checkpoints, Box[] exits, MovementSettings settings, RespawnRule rule)`. `TrapSpec` arrives in Step 6; this step passes an empty array and the field exists. It builds its `PlayerStepper` and allocates every per-element array here.
    - **State it owns** (read-only properties): `Player` (`PlayerState`), `Phase`, `PhaseTicks`, `Deaths`, `DeathX` / `DeathY` (feet at death, Y clamped ≥ 0 for pits), `StartX` / `StartY` (the placed start), `LastPlacement` (`StartPlacement`), `Rule`, `SafeX` / `SafeY`, `ActiveCheckpoint` (−1 = none), `CanPlayAgain`.
    - **`Begin(float feetX, float feetY, Abilities abilities)`**, a start source (play start, play-from-here, play again):
      - resets traps (Step 6), `Deaths` = 0, `ActiveCheckpoint` = −1
      - `StartPlacement.Find` on the reset grid
      - start = safe ground = placed point; phase Playing; player at rest with `Teleported` false
    - **`Step(in PlayerInput input)`**, in this order (document it in a comment like `PlayerStepper.Step`'s):
      1. **Won:** `PhaseTicks++`, return. `CanPlayAgain` = `PhaseTicks >= WinInputLockTicks`.
      2. **Dying:** input ignored. `PhaseTicks--`; at 0 → **respawn**, then return without stepping the player:
         - reset all traps (Step 6)
         - the point is: rule Checkpoint with `ActiveCheckpoint ≥ 0` → that checkpoint's respawn point (Step 5); rule Checkpoint with none → the start; rule SafeGround → `SafeX` / `SafeY`
         - placed through `StartPlacement.Find` on the reset grid
         - `CreateState` with the **current** abilities; `Teleported = true`; phase Playing
      3. Pending trap delays (Step 6).
      4. `Player = stepper.Step(Player, input)`.
      5. Triggers (Step 6).
      6. **Death check:** crushed (Step 6), or `CoreTouchesHazard`, or a pit (`Y + HitboxHeight < 0`, fully below the map). On death:
         - `Deaths++`; phase Dying; `PhaseTicks = DeathBeatTicks`
         - record the death position
         - cancel every pending trap delay
         - return. Exits and checkpoints are not looked at this tick, so **death wins over an exit**.
      7. Checkpoints, then exits (Step 5).
      8. **Safe ground:** if the player is standing and `StandsFullyOnPainted` (plus Step 6's trap conditions) → `SafeX` / `SafeY` = feet.
    - **Pass-throughs:** `ApplySettings(MovementSettings)`, `ChangeAbilities(Abilities)` (applied to `Player`), `SetRule(RespawnRule)`, all live and taking effect on the next tick or death.
    - **The held-jump rule (item 5):** input is ignored for every beat tick and the respawn tick, so a press made during the beat is consumed and lost. `CreateState` clears the buffer; a fresh press is needed.
- **NEW `P/Tests/EditMode/Level/LevelScenario.cs`**: like `PlayerScenario`, but over `LevelStepper` (an `AsciiLevel` plus builder methods for traps, checkpoints and exits; `Tick`, `RunUntil`, `Press` / `Hold`). It records the deaths seen and the phase per tick.
- **Tests:**
    - **`DeathTests`:**
      - walking into a pit → Dying exactly on the tick the hitbox is fully below row 0; 24 beat ticks with the player frozen (X, Y unchanged)
      - respawn on beat tick 24 with `Teleported` true on that tick only, false on the next; `Deaths` 1
      - spikes kill on the first tick the core touches them
      - input during the beat (move, press) changes nothing
      - a jump pressed on the last beat tick and held does not jump after respawn; a fresh press does
      - abilities changed during play survive respawn
      - safe-ground rule returns to the last full-painted standing spot; the checkpoint rule with no checkpoint returns to the start
      - `SetRule` mid-play takes effect at the next death
    - **`RunAndFallTests`:** the two fall-out tests (`FallingOutOfTheBottomResetsToTheStart`, `TeleportedIsSetOnlyOnTheResetTick`) are removed; one is added ("the stepper alone keeps falling below row 0 at max fall speed"); `TeleportedStaysFalseInOrdinaryPlay` is kept. The reset assertions move to `DeathTests`.
    - **`SafeGroundTests`** (this step's part): not recorded mid-air, on a ledge edge with one column over air, on a one-way (recorded: it is painted), or while dying.
- **Verify:** `[automated]` EditMode green; `[grep]` no `StartX` / `StartY` in `PlayerState.cs` or `PlayerStepper.cs` (`LevelStepper` owns them now).

### Step 5: Checkpoints, exits, the win, play again (sim)
- **MODIFY `LevelStepper`:**
    - **One shared touch rule (item 15):** `Touches(Box box)` = `Box.Hitbox(player).Grown(ContactMargin).Overlaps(box)`. It is used by checkpoints, exits and Touch triggers (Step 6). Hazards use the inset core instead.
    - **Checkpoints:** each tick in Playing, the first checkpoint in array order that touches becomes `ActiveCheckpoint`; the latest touch wins over an earlier one. It is recorded under either rule, so a live switch of On hazard behaves.
    - **Respawn point** of checkpoint *i*: its box's bottom centre (x = centre, y = MinY), placed through `StartPlacement.Find`.
    - **Exits:** any exit touching → phase Won, `PhaseTicks` 0; the player is no longer stepped.
    - `CanPlayAgain` as in Step 4. The runtime calls `Begin` with the original start to play again (counter 0, traps reset).
- **Tests (`CheckpointExitTests`):**
    - touching a checkpoint sets the respawn point, and the next death (rule Checkpoint) respawns at its bottom centre
    - a later checkpoint replaces it; re-touching the first sets it back
    - under rule SafeGround a touched checkpoint is recorded but the respawn uses safe ground
    - a checkpoint buried in a wall respawns at the nearest free spot
    - an exit touch → Won; `CanPlayAgain` false for ticks 1–59 and true at 60
    - death and exit on the same tick → Dying, not Won
    - `Begin` after Won → Playing, `Deaths` 0, `ActiveCheckpoint` −1, at the start
    - the touch margin on all four sides of a box (inside margin → touch; margin + ε away → none)
- **Verify:** `[automated]` EditMode green.

### Step 6: Static traps in the sim
- **NEW `TrapTriggerKind.cs`** (`Zone = 0`, `Touch = 1`) and **`TrapActionKind.cs`** (`Vanish = 0`, `Appear = 1`; comment: 2 Move and 3 Fall are reserved for phase 4, never renumber).
- **NEW `TrapSpec.cs`**: `public readonly struct TrapSpec`.
    - Fields: `CellX`, `CellY`, `Width`, `Height` (sim cells); `Kind` (`TileKind.Solid` or `.Hazard`; the constructor throws otherwise); `Action`; `Trigger`; `Zone` (`Box`, sim coordinates); `DelayTicks` (≥ 0).
    - `Box CellBox`.
- **MODIFY `P/Runtime/Sim/TileGrid.cs`**:
    - A second cell array, the **trap layer** (allocated with the grid).
    - `Get` returns the painted kind when it is not None, else the trap-layer kind: **painted tiles stay in charge** (item 11). Outside the grid it is unchanged.
    - New `GetPainted(x, y)`, `SetTrapCell(x, y, kind)` (cells outside the grid are ignored, since traps are clipped) and `ClearTrapCells()`.
    - Class doc updated.
- **MODIFY `TileCollision.StandsFullyOnPainted`** to read `GetPainted`.
- **MODIFY `LevelStepper`:**
    - **Per-trap state** (private nested struct; one array):
      - `Present`: true for Vanish at reset, false for Appear
      - `Armed`, `Fired`, `Pending` (ticks; −1 none)
      - `TrapChanges` (public int, bumped whenever any `Present` changes, so the runtime redraws only on change)
      - `IsTrapPresent(int)` and `TrapCount`
    - **`RebuildTrapCells()`**: clears the layer, then stamps every present trap. Where present traps overlap, Solid beats Hazard. It runs at reset and once per batch of fires in `ResolveAppeared` (changes are rare; cost = total trap area).
    - **Reset** (every `Begin`, every respawn): `Present` initial, `Armed = false`, `Fired = false`, `Pending = −1`, rebuild, and **`TrapChanges++` when any `Present` changed**, so the runtime re-shows a vanished floor after a respawn (round 1, F6). The constructor resets before any placement, so **a start inside a present Solid trap is moved out by the start search** (item 6's start rule).
    - **Trigger test** `Inside(i)`: Zone → hitbox overlaps `Zone`; Touch → `Touches(CellBox)`.
    - **Step 3, delays:** each `Pending > 0` counts down; at 0 → `Fire(i)`.
    - **Step 5, triggers**, for each trap in Playing:
      - not armed → `Armed = !Inside(i)`, never firing on the same evaluation
      - armed, not fired and `Inside(i)` → `Fired = true`; `DelayTicks == 0` → `Fire(i)` now, else `Pending = DelayTicks`
      - **This applies to every start source** (item 3), because every start and respawn resets `Armed`.
    - **`Fire(i)`** only marks: `Present = (Action == Appear)`; `TrapChanges++`; if it is a Solid trap that became present, its index joins this tick's appeared list (a preallocated int array + count). It does **not** push.
    - **`ResolveAppeared()`**, run once after each batch of fires (after step 3's delays and after step 5's triggers): rebuild the cells once, then, if any appeared Solid trap's box overlaps the player's hitbox → **push out** (item 6) against the **bounding box of the appeared Solid traps that overlap the player**, with `BoxIsFree` checked against the whole rebuilt grid. One push per batch, after every fire in it, so the result doesn't depend on the trap array's order (round 1, F3). It runs **only when the batch fired at least one trap**, so a quiet tick rebuilds nothing, and it **clears the appeared list** when done (round 2, F14).
      - Four candidates against that box: up (feet on its top), left (right edge on its left face), right, down (head under its bottom).
      - Order by distance; ties go up, then left, then right, then down **(proposal)**. Take the first where `BoxIsFree` (and the core touches no hazard).
      - Move the player there and zero the velocity component that points into the trap. Leave a rope or cling (Contact Air, `RopeIndex` −1, `WallSide` 0), as phase 4's push will. Not `Teleported`.
      - No free candidate → **crushed** (death in step 6).
    - **Safe ground (item 2):** step 8 also requires the hitbox to be outside every trap's trigger (zone or touch box) **and** not overlapping any trap's `CellBox` **(proposal: the box condition goes beyond item 2; see Notes)**.
    - **Hazard traps** kill through the core rule, because their present cells read Hazard. They never push.
- **Tests (`TrapTests`):**
    - **Drop Floor:** a Vanish Solid 3 × 1 in the floor row with the default zone (x-range = the box's, so the zone fires on the tick the hitbox's leading edge passes the trap's edge). Walking onto it: it fires on that edge-entry tick with the player still supported by the painted floor behind (assert both); the cells are gone the same tick; the player falls on the first tick the hitbox has fully left the painted floor (assert that tick). Falling onto it from above fires on zone entry, before landing. Jumping over it ≥ 1 tile higher does not fire. **Design note for Step 19:** at this default a walking player sees the floor vanish just in front of them, not under them.
    - **Pop Spike:** an Appear Hazard on the floor with the default zone. Walking in: it fires on the edge-entry tick and the player dies on the first tick the inset core overlaps the spike cells, about 0.2 tile further on (2 ticks at full run speed 6, more while still accelerating; 1 at 12); assert the fire tick, that death comes strictly later, and that the core overlapped no spike cell before the death tick. Jumping across with the feet above trap top + 1 never fires; with the feet in the zone but above the spikes, it fires and the player survives.
    - **Touch:** a Vanish on touch fires when stood on (margin) and when pushed against from the side.
    - **Delay:** 0.5 s fires exactly 30 ticks after the trigger tick; a death during the delay cancels it, and after respawn the trap is reset and unfired.
    - **Once per life:** re-entering after firing does nothing until a death.
    - **Re-arm on every start source:** a play start inside a zone doesn't fire until the player leaves and re-enters; same for a checkpoint respawn inside a zone and a safe-ground return.
    - **Every death resets all traps**, including traps far from the death; `TrapChanges` moves on the respawn after a fire.
    - **Painted wins:** a Vanish trap over painted Solid leaves the cells Solid after firing; an Appear Hazard over painted Solid stays Solid.
    - **Overlap:** two present traps on one cell: Solid beats Hazard. Firing order across traps doesn't change the result: two overlapping Solid Appear traps that fire on the same tick around the player, run with the trap array reversed → identical cells, `Present` flags **and player state** (one push against their combined box).
    - **Safe ground:** never recorded inside a zone or touch box, nor inside a trap's box.
- **Tests (`TrapPushTests`):**
    - a hidden block (Solid Appear, Touch) above a jump, start height chosen so the head is inside the box on the fire tick: assert that precondition (hitbox overlapped the cell box on the fire tick), then the push **down** and the upward speed zeroed (head bump)
    - the same block with the head just below the box on the fire tick: no push; the bonk comes from the ordinary ceiling sweep on the next tick
    - an Appear Solid around a standing player with free space above → pushed up onto it
    - with a wall on the near side → pushed the other way
    - in a sealed corridor → crushed: death, beat, `Deaths` + 1
    - pushed while on a rope → lets go
    - a start inside a present Solid trap → placed outside it, and `LastPlacement.Moved` is true
- **Tests (`TileGridTests`):** the trap layer is read only where painted is None; `GetPainted` ignores the layer; `ClearTrapCells` empties it.
- **Verify:** `[automated]` EditMode green.

### Step 7: Zero allocation in the world step
- **MODIFY `StepAllocationTests.cs`**: wrap `LevelStepper.Step` in `Is.Not.AllocatingGCMemory()` over a scripted run that passes through a trap trigger, a Delay, a fire with push-out, a crush death, the beat, a respawn, a checkpoint, an exit and `Begin`. The planted sanity check works as in Phase 1. Seen failing with a used `new int[1]` planted in `Fire`.
- **MODIFY `P/Runtime/Sim/FixedStepAccumulator.cs`**: `public void Reset()` (the fraction goes to 0). **Tests (`FixedStepAccumulatorTests`)**: after `Reset`, `InterpolationFraction` is 0 and `Advance(1/60.0)` returns 1.
- **Verify:** `[automated]` EditMode green, planted failure seen.

### Step 8: Player Health section and session wiring
- **NEW `P/Runtime/OnHazard.cs`**: `public enum OnHazard { [InspectorName("Return to safe ground")] ReturnToSafeGround = 0, [InspectorName("Respawn at checkpoint")] RespawnAtCheckpoint = 1 }`, with a values-are-saved comment. It maps one to one onto `RespawnRule`.
- **NEW `P/Runtime/PlayerHealthSection.cs`**: `[Serializable] public sealed class PlayerHealthSection`.
    - Field: `[SerializeField, Tooltip("What happens when the player touches spikes, falls into a pit or is crushed. Return to safe ground: back to where it last stood safely. Respawn at checkpoint: back to the last checkpoint it touched, or the start.")] OnHazard onHazard;` (default value 0)
    - `internal` property; `internal RespawnRule ToRule()`.
- **MODIFY `Player.cs`**:
    - `[SerializeField] PlayerHealthSection health = new PlayerHealthSection();` after `abilities`. `dataVersion` stays 1: the new field defaults to 0 = the default, and no old value changes.
    - The live path: on a dirty frame, `session.SetRule(health.ToRule())` alongside the settings.
- **MODIFY `PlayerSession.cs`**:
    - It holds a `LevelStepper` instead of a `PlayerStepper`. New constructor: `(CollisionMap map, RopeSegment[] ropes, TrapSpec[] traps, Box[] checkpoints, Box[] exits, MovementSettings settings, RespawnRule rule, PlayStart start)`.
    - `Tick`: `Previous = Current; level.Step(input); Current = level.Player;` and if `Current.Teleported`, `Previous = Current`.
    - `Restart(PlayStart)`: `level.Begin(...)`, snap, `accumulator.Reset()` (no allocation).
    - Exposes `Phase`, `PhaseTicks`, `Deaths`, `DeathFeetWorld`, `CanPlayAgain`, `TrapChanges`, `IsTrapPresent(i)`, `ActiveCheckpoint`, `StartFeetWorld`, `SetRule`.
    - Keeps its name (Notes, open question).
- **MODIFY `PlayStart.cs`** doc: every start (play start, play-from-here, play again) resets the traps and the counter, and re-arms traps only once the player has left their triggers.
- **Tests:**
    - `PlayerLayoutTests`: `VisiblePaths` += `health.onHazard`. The section count list adds Health = 1. Tooltip count 13 → 14. `onHazard` default is `ReturnToSafeGround` (value 0). `InspectorName` labels pinned.
    - `PlayerSessionTests`: the reset-snap test walks into the pit and asserts the snap on the respawn tick (after 24 beat ticks). The restart test also asserts `Deaths` 0 and the traps reset. A live `SetRule` changes the next respawn.
    - `PlayerPlayTests`: the `StartX` / `StartY` use becomes `Session.StartFeetWorld`.
    - `PlayerRenderTests` / `PlayerWarningTests` fall cases: same change (through the beat).
- **Verify:** `[automated]` EditMode + PlayMode green.

### Step 9: Checkpoint and Exit components; the play-start scan
- **NEW `P/Runtime/PlayStartScan.cs`**: `internal static class PlayStartScan`.
    - `T[] FindActive<T>() where T : Behaviour`: one `FindObjectsByType` with the existing `UNITY_6000_6_OR_NEWER` split; keeps `isActiveAndEnabled`.
    - **MODIFY `RopeCollector.FindSorted`** to use it; no behaviour change, and the existing rope tests prove it.
- **NEW `P/Runtime/Checkpoint.cs`**: `[AddComponentMenu("Daedalus/Checkpoint")] [DisallowMultipleComponent] public sealed class Checkpoint : MonoBehaviour`.
    - No visible settings; hidden `dataVersion` = 1.
    - `internal Rect WorldBox`: the SpriteRenderer's world bounds when it has a sprite, else 1 × 1 centred on the position **(proposal)**.
    - Play-mode feedback (item 14): while it is the active checkpoint, its SpriteRenderer is tinted `ReachedTint` (a presentation const, warm gold **(proposal)**); otherwise the authored colour, which is cached at `Awake` and restored.
    - `OnDrawGizmos`: always-on outline of `WorldBox`.
- **NEW `P/Runtime/Exit.cs`**: `[AddComponentMenu("Daedalus/Exit")]`. Same box rule, `dataVersion` 1, outline gizmo.
- **NEW `P/Runtime/LevelCollector.cs`**: `internal static class`.
    - `FindCheckpoints()` / `FindExits()` / `FindTraps()` (Step 12): `PlayStartScan`, then a deterministic sort by world box (min x, min y, then width, height).
    - `TryGetWorldBounds(...)`: the union of every element's world box.
    - `ToBoxes(Rect[] world, Vector2 origin)` → sim `Box[]`.
- **MODIFY `Player.StartPlay`**:
    - The include rect grows by the checkpoint, exit and trap bounds (as ropes do), so every element is inside the map.
    - The session gets the boxes.
    - `Render` sets each checkpoint's tint on change of `ActiveCheckpoint` only (no per-frame writes).
- **Tests:**
    - `TrapLayoutTests` (Checkpoint and Exit part): menu paths, `dataVersion` 1, no visible fields, `WorldBox` with and without a sprite (scale 2, pivot bottom).
    - Sort order: three checkpoints placed out of order come back sorted.
    - A checkpoint to the right of every painted tile grows the map.
    - A disabled one is skipped.
- **Verify:** `[automated]` EditMode green.

### Step 10: uGUI dependency
- **MODIFY `P/package.json`**: `"com.unity.ugui": "2.0.0"`. This is the version the 6000.0.85f1 2D template pins (verified in `com.unity.template.2d-cross-platform-5.1.0.tgz`). uGUI is a built-in package on both editors (6000.0.85f1 → 2.0.0, 6000.6.7f1 → 2.6.0), so newer editors use their own.
- **MODIFY `P/Runtime/Orcapafea.Daedalus.asmdef`**: `references` += `"UnityEngine.UI"`.
- **MODIFY `AssemblyDirectionTests`**: the runtime's references ⊆ {Sim, `Unity.InputSystem`, `UnityEngine.UI`}; the editor-assembly asserts stay.
- **MODIFY `PackageManifestTests.DependenciesAreExactlyTheExpectedSet`**: += ugui 2.0.0.
- **Checks:**
    - After the 6000.0 run, read `Tools/compat/6000.0/Packages/packages-lock.json`: ugui resolves 2.0.0.
    - The wrapper needs no pin (built-in); confirm and record.
    - `UnityEngine.UI.Text` is not `[Obsolete]` on either editor (checked while planning); the warning gate confirms it.
- **Tests:** the planted failure: expected `2.0.1` → red; revert.
- **Verify:** `[automated]` both editors' EditMode green.

### Step 11: Trap component and its Play-mode look
- **NEW `TrapTrigger.cs`**: `{ [InspectorName("Player enters zone")] Zone = 0, [InspectorName("Player touches")] Touch = 1 }`.
- **NEW `TrapAction.cs`**: `{ Vanish = 0, Appear = 1 }`; comment that 2 / 3 are reserved for Move / Fall.
- **NEW `P/Runtime/Trap.cs`**: `[AddComponentMenu("Daedalus/Trap")] [DisallowMultipleComponent] [RequireComponent(typeof(SpriteRenderer))] public sealed class Trap : MonoBehaviour`. Six visible settings, at most 8:
    - `trigger` (`TrapTrigger`, default Zone): "What sets the trap off: the player entering its zone (the yellow box), or touching the trap itself."
    - `zone` (`Rect`, **relative to this object's position**; set in `Reset()` to the default): "The box the player must enter to set the trap off. Drag its handles in the Scene view." Shown only for Zone (Step 13).
    - `action` (`TrapAction`, default Vanish **(proposal)**): "What the trap does: Vanish (disappears) or Appear (hidden until it fires)."
    - `delay` (`[Range(MinTrapDelay, MaxTrapDelay)]`, default `DefaultTrapDelay`): "Seconds between the trigger and the action. 0 = at once."
    - `size` (`Vector2Int`, default 3 × 1): "How many tiles wide and tall the trap is. The object's Scale is ignored." A `Vector2Int` has no `Range`, so a student can type 0 or 40: **every reader goes through `TrapLook.ClampedSize`** (below), never the raw field.
    - `tile` (`TileBase`): "The tile drawn across the trap. A Hazard (spike) tile makes a trap that kills; any other tile makes solid ground. Use a plain tile: Rule Tiles can't be drawn on a trap."
    - hidden `dataVersion` = 1
    - `internal` accessors for tests.
- **NEW `P/Runtime/TrapLook.cs`**: `internal static class`, the box math the runtime and the editor share.
    - `Vector2Int ClampedSize(Trap)`: each axis clamped to `MinTrapSize`–`MaxTrapSize`. The **one** size that `CellMin`, `SnappedCentre`, `ToSpec`, `ApplyPlayLook`, `TrapAuthoring.SyncLook` and the gizmos read, so the drawn box and the sim box can't disagree (round 1, F2; Architecture rule 7).
    - **Anchor (deviation, open question 1):** the box is **centred on the object's position**, where a centre-pivot tile sprite in Tiled mode draws it. `CellMin = FloorToInt(position − size / 2 + 0.5)` per axis in Collision-grid cells (not `RoundToInt`, which rounds half to even and would snap half-tile positions left or right by parity); `SnappedCentre = CellMin + size / 2`; `IsOffGrid` = distance > 0.01. `size` here is `ClampedSize`.
    - `TileKind KindOf(TileBase tile)`: a `DaedalusTile` of kind Hazard → Hazard; anything else, or none → Solid **(proposal)**.
    - `Sprite SpriteOf(TileBase tile)`: `Tile.sprite` for a `Tile` (including `DaedalusTile`); else null (Rule Tiles, animated tiles).
    - `Rect WorldZone(Trap)` = position + `zone`; `Rect DefaultZoneLocal(Vector2Int size)` = `LevelRules.DefaultZone` of the centred box, made relative.
    - `TrapSpec ToSpec(Trap, Vector2 origin)`: delay through `LevelRules.SecondsToTicks`; size through `ClampedSize`; the zone `Rect` **normalised** (min/max swapped when width or height is negative) before it becomes a `Box`, so student-typed data degrades instead of throwing in `Player.Start` (round 1, F8). The throwing `Box` constructor stays for sim-internal invariants only.
    - **`ApplyPlayLook(Trap, SpriteRenderer, bool present)`**, Play mode only:
      - sprite = `SpriteOf(tile)`, `drawMode = Tiled`, `size = ClampedSize`
      - the transform moved to `SnappedCentre` (z kept)
      - `enabled = present`
- **MODIFY `Player`**: `internal Tilemap ResolveCollisionTilemap()` = the `collisionTilemap` field, else `PickCollisionTilemap()`. Play start and the editor inspectors (Step 13) all call it; nothing else picks (round 1, F7).
- **A Trap on an Exit or Checkpoint object is ignored in this phase** (round 1, F9): play start leaves it out of the specs, `SyncLook` and the `componentWasAdded` hook leave the renderer alone, and the inspector shows `PlayerWarnings.TrapOnExitOrCheckpoint` verbatim (Step 14; one string, round 2, F16) Phase 4 gives the Exit case its meaning (it moves the exit), so a phase 3 scene never saves a 3 × 1 solid where the exit stands.
- **MODIFY `Player.StartPlay`**:
    - `LevelCollector.FindTraps()` → specs (+ include bounds) → session.
    - Each trap's look is applied once.
    - `Render` re-applies `enabled = IsTrapPresent(i)` only when `session.TrapChanges` changed. The trap objects are cached in an array at Start; no `GetComponent` per frame.
- **Gizmos** (runtime, like `Rope`):
    - `OnDrawGizmos`: always-on outline of the snapped box. White for Solid, red for Hazard; dashed (or with a corner mark) for Appear, so hidden traps are findable (item 12).
    - `OnDrawGizmosSelected`: the zone box in yellow when the trigger is Zone.
- **Check:** with a spike sprite with transparent pixels imported as **Tight**, does Tiled mode draw or warn wrongly on 6000.0 and 6000.6? Record the result in `framework.md`. Dev sprites import as Full Rect either way, and Step 14's help box tells students to set Mesh Type: Full Rect when the tile's sprite isn't.
- **Tests (`TrapLayoutTests`, Trap part):**
    - field names through `SerializedObject`; defaults from `LevelRules`; Range = Min / Max; every visible field has a tooltip; `dataVersion` 1 hidden; menu path; `RequireComponent`
    - `Reset()` zone = `DefaultZoneLocal(3 × 1)`
    - `CellMin` for positions (1.5, 0.5) size 3 × 1 → (0, 0); off-grid (1.8, 0.5) → (0, 0) and `IsOffGrid`; size 2 × 2 at (1, 1) → (0, 0)
    - `KindOf`: plain → Solid, Hazard `DaedalusTile` → Hazard, OneWay `DaedalusTile` → Solid, null → Solid
    - `ToSpec` with an origin offset; a zone with negative width and height gives the same spec as its normalised Rect
    - `ClampedSize`: size 40 × 1 → a 30-wide spec **and** a 30-wide renderer (`ApplyPlayLook`), both centred on the same cell box; 0 × 1 → 1 × 1 for both
    - half-tile positions snap the same way regardless of parity: x 0.5 and 1.5 with width 2 both snap up (`FloorToInt(v + 0.5)`)
- **Verify:** `[automated]` EditMode green.

### Step 12: Death beat look, follow camera, HUD and win screen
- **NEW `P/Runtime/DeathBurst.cs`** (plain C#, created at Start):
    - 8 pooled child SpriteRenderers on one runtime-made object, using a code-made 2 × 2 px white sprite (16 PPU).
    - During Dying: the player sprite blinks for the first half of the beat, then hides. The particles fly out from `DeathFeetWorld` + 0.75 and fade, positioned from `PhaseTicks` + the interpolation fraction. Speeds and sizes are presentation consts.
    - Hidden in Playing and Won.
- **NEW `P/Runtime/CameraFollow.cs`** (plain C#, item 9):
    - `Camera.main` is captured once in `Start`. This is a play-start lookup, added to the rule-5 exception (Notes).
    - No camera → no follow and **no warning** (a scene without a camera shows nothing anyway).
    - Each `Render`: the camera's x / y = the player's interpolated hitbox centre, clamped so an orthographic view stays inside the map's world rect. The map's width is centred when narrower than the view. z is kept.
    - The respawn cut comes for free: `Teleported` snaps the interpolation.
    - No smoothing, no look-ahead (these stay in `world-systems`); perspective cameras follow unclamped.
- **NEW `P/Runtime/PlayHud.cs`** (plain C#, built in `Start`, item 10):
    - A Canvas in Screen Space – Camera on the followed camera, else Overlay; CanvasScaler at reference 640 × 360. No EventSystem (nothing is clickable).
    - Top-left: a code-made pixel skull `Image` (texture from a small string pattern, point filter) plus a legacy `Text` using `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`.
    - The number text is set **only when `Deaths` changes**, from a string cache "0".."999" built at Start (shows "999" beyond). A scale bump 1.3 → 1 over 0.15 s runs on frame time.
    - **Win screen** (shown when the phase becomes Won): a full-screen dim `Image` (black, alpha 0.6, presentation), "You made it!", "Deaths: N" (cached strings) and, once `CanPlayAgain`, "Press any key to play again".
- **MODIFY `PlayerControls`**:
    - A `PlayAgain` button action bound to `<Keyboard>/anyKey`, `<Gamepad>/buttonSouth`, `buttonEast`, `buttonNorth`, `buttonWest`, `start`; `PlayAgainPressed` is read in `ReadFrame` from `WasPressedThisFrame`.
    - `ClearLatch()` drops a latched jump.
- **MODIFY `Player.RunFrame`**: after the ticks, if `Phase == Won && CanPlayAgain && PlayAgainPressed` → `session.Restart(firstStart)` and `controls.ClearLatch()`. `firstStart` is the start used at play start: play-from-here's, else the placed one. No warnings are logged on this path. Then `Render`, the burst, the camera and the HUD.
- **Tests:**
    - **`HudAndWinTests`** (PlayMode, a `TestLevel` with a tagged main camera):
      - the counter reads 0 at start and 1 after a spike death; text unchanged on non-death frames (same string instance)
      - an exit touch shows the dim and both lines
      - keys pressed for 59 ticks do nothing; a press at ≥ 60 restarts with counter 0, the traps reset and the player at the start
      - holding Space through the restart doesn't jump
      - screenshots `traps-counter.png`, `traps-win.png`
    - **`CameraFollowTests`:** follows the player horizontally across a 60-wide level; clamps at both ends and at the bottom; the respawn frame shows the camera at the respawn point (no in-between frame); no camera tagged → no exception, no warning.
    - **`PlayerControlsTests`:** `PlayAgain` from a letter key and from gamepad Start; `ClearLatch` drops a latched jump.
- **Verify:**
    - `[automated]` PlayMode green.
    - `[manual]` Look at `traps-counter.png` and `traps-win.png`: the skull and number sit in the top-left corner and are readable; the win text is centred over a dimmed level.
    - Must be absent: text clipped, a magenta (missing-material) square, a pink font.

### Step 13: Trap inspector, gizmos, handles and the ghost (editor tools)
- **MODIFY `P/Runtime/AssemblyInfo.cs`**: `InternalsVisibleTo("Orcapafea.Daedalus.Editor")`, so the editor reads `TrapLook` / `HelpTexts` and never re-derives them.
- **NEW `P/Runtime/HelpTexts.cs`**: `internal static class`; plain words, built from the same settings the runtime converts.
    - `TrapStory(Trap)` builds one sentence, for example:
      - "Solid ground until the player enters the zone, then it vanishes."
      - "Hidden until the player touches it, then it appears 0.5 s later."
      - Plus "It's a spike trap: touching it while it's out kills." for Hazard.
      - Plus, when the zone reaches **more than 1 tile beyond the trap's box** on any side other than the default growth (threshold presentation const, **(proposal)**): "The zone reaches 3 tiles left of the trap, so it fires before the player gets there."
    - `CheckpointNotice`: "Touching this checkpoint makes it the place the player comes back to."
    - `CheckpointNoEffect`: "The Player's On hazard is Return to safe ground, so the player comes back where it last stood safely, not here. Set it to Respawn at checkpoint to use checkpoints."
    - `ExitNotice`: "Touching this ends the game with a win screen."
- **NEW `P/Editor/TrapAuthoring.cs`**: `internal static class`; every write is `Undo`-recorded and multi-object safe.
    - `FitZone(Trap)`: zone = `TrapLook.DefaultZoneLocal(size)`.
    - `SnapToGrid(Trap)`: position = `SnappedCentre`.
    - `SyncLook(Trap)`: the SpriteRenderer's sprite = `SpriteOf(tile)`, `drawMode = Tiled`, `size = TrapLook.ClampedSize`, `enabled = action == Vanish`. The saved renderer state is the play-start look; Appear traps are saved disabled.
    - `OnSizeChanged(Trap, Vector2Int oldSize)`: if the zone equalled the old default, it follows the new default **(proposal)**.
    - `ObjectFactory.componentWasAdded` hook → `SyncLook` for a new Trap.
- **NEW `P/Editor/TrapEditor.cs`**: `[CustomEditor(typeof(Trap)), CanEditMultipleObjects]`.
    - Draws trigger; **zone only when the trigger is Zone** (as a read-out plus the "Fit zone around trap" button); action, delay, size, tile.
    - Calls `SyncLook` / `OnSizeChanged` on change.
    - Help boxes: the story (Info). Warnings, using the same `PlayerWarnings` strings Step 14 logs: over painted tiles; off-grid (with a "Snap to grid" button); no tile; a tile that can't be drawn (Rule Tile); a sprite not Full Rect; a Scale or Rotation ≠ identity.
    - `OnSceneGUI`: a `BoxBoundsHandle` for the zone (Zone trigger only), snapping to whole tiles **(proposal)**, Undo-recorded.
    - "Over painted tiles" reads the Collision Tilemap through the scene's Player's `ResolveCollisionTilemap()` (the Player found once in `OnEnable`, as `CheckpointEditor` does), cached; never per repaint. Same Tilemap as play start (round 1, F7).
- **NEW `P/Editor/TrapGhostGizmo.cs`**: `[DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]` for Appear traps, **only while not playing**.
    - Draws the tile's sprite across the box at 35–40 % alpha **(proposal)**, one quad per cell built from the sprite's UVs, with a transparent editor material (`Graphics.DrawMeshNow`).
    - Never in Play or a build (item 12).
- **NEW `CheckpointEditor.cs` / `ExitEditor.cs`**:
    - Notices as Info help boxes.
    - The Checkpoint shows `CheckpointNoEffect` (Warning) when the scene's Player (one `FindObjectsByType<Player>` in `OnEnable`, cached; editor code) has `ReturnToSafeGround`.
- **Tests:**
    - **`HelpTextsTests`:** every trigger × action × kind × delay (0 / 0.5) sentence; the early-zone line on and off at the threshold (both sides).
    - **`TrapAuthoringTests`** (EditMode, editor assembly via its IVT):
      - `FitZone`, `SnapToGrid`, `SyncLook` (Appear → disabled, Vanish → enabled, size and drawMode set)
      - zone follows a size change only when it was default
      - each write is undoable (`Undo.PerformUndo` restores)
      - multi-object `FitZone` on two traps
      - `ObjectFactory.AddComponent<Trap>` gets a SpriteRenderer (RequireComponent) and a synced look
- **Verify:** `[automated]` EditMode green; Step 18 carries the UI.

### Step 14: Play-start warnings (item 14)
- **MODIFY `PlayerWarnings.cs`**, new texts:
    - `TrapOverPaintedTiles`: "This trap sits on tiles painted on the Collision Tilemap. The painted tiles stay in charge, so the trap won't change those cells. Erase the painted tiles under it, or move the trap."
    - `HazardOnDecorationTilemap`: "This Tilemap has Hazard (spike) tiles, but only the Collision Tilemap can hurt the player. Paint spikes on the Collision Tilemap."
    - `TrapOffGrid` **(proposal)**: "This trap isn't lined up with the grid, so it works as if it were snapped to the nearest cells. Click Snap to grid on the trap."
    - `TrapNoTile`: "This trap has no Tile, so you won't see it. Pick a Tile on the trap."
    - `TrapTileNotDrawable`: "This trap's Tile can't be drawn on a trap (Rule Tiles and animated tiles aren't supported). Pick a plain tile."
    - `TrapSpriteNotFullRect`: "This trap's tile picture may look wrong when repeated. In the sprite's import settings, set Mesh Type to Full Rect."
    - `TrapScaled`: "This trap is scaled or rotated, but traps ignore Scale and Rotation. Set them back to 1 and 0 and use Size."
    - `TrapOnExitOrCheckpoint`: "A Trap on an Exit or a Checkpoint comes in a later update, so this one does nothing yet. Put the Trap on its own object."
- **MODIFY `Player.StartPlay`**: each warning is logged once per object at play start, with the Trap (or the Tilemap) as context.
    - **Over painted:** any painted non-None cell inside the trap's box (`GetPainted`).
    - **Hazard on decoration:** the play-start Tilemap scan becomes one shared list (the pick and this check), and each non-Collision Tilemap is read once with `GetTilesBlock` for Hazard `DaedalusTile`s.
    - Full Rect is checked only in the editor (`TextureImporter` is editor-only); there is no runtime warning.
- **Tests (`LevelWarningTests`, PlayMode, counted with the Phase 2 log-handler wrapper):** each text appears exactly once with the right context. A Trap on the Exit object: the warning, no trap cells, and the Exit's sprite untouched. A clean trap level logs nothing. A Hazard tile on Background warns; a Hazard on Collision doesn't.
- **Verify:** `[automated]` PlayMode green.

### Step 15: PlayMode troll tour and allocation guard
- **MODIFY `TestLevel` / `TestLevelOptions`**:
    - `^` → a Hazard `DaedalusTile`
    - `Traps`, `Checkpoints`, `Exits` option lists (position, size, trigger, action, delay, tile kind) built as objects with components before activation
    - `MainCamera` (tag the level camera)
- **NEW `TrapPlayTests`** (keyboard through `InputTestFixture`, `captureFramerate = 60`); each run asserts its outcome and saves a picture:
    - **Drop Floor:** run onto it → it vanishes → pit death → burst mid-beat (`traps-burst.png`) → respawn on safe ground before the trap, and the floor is back (`traps-dropfloor.png`).
    - **Pop Spike:** walk in → death; replay jumping high over it → survives, spikes visible behind (`traps-popspike.png`).
    - **Hidden block:** jump under it → bonk, the block appears, the player lands below (`traps-hiddenblock.png`).
    - **Checkpoint:** touch it, die past it, respawn there (Respawn at checkpoint), tinted (`traps-checkpoint.png`).
    - **Edit-mode look matches Play:** the drawn trap pixels match the neighbouring painted tiles (pixel compare on a seamless tile).
- **MODIFY `PlayerAllocationTests`**:
    - The window also covers a trap fire (renderer enable), a spike death (counter text change), the beat, a respawn, a win screen and play again.
    - The sanity plant stays. Seen failing with a `Deaths.ToString()` planted in `PlayHud`.
- **Verify:**
    - `[automated]` PlayMode green.
    - `[manual]` Look at every `traps-*.png`: the trap is drawn on the grid like the ground around it; the spikes stand on the floor; the burst is centred on the death spot; the counter is readable.
    - Must be absent: a ghost in Play, a visible seam or shift between a Vanish trap and the painted ground next to it, the player drawn during the second half of the beat.

### Step 16: Trap test bench scene (dev)
- **NEW `Assets/Dev/Editor/TrapTestSceneBuilder.cs`**: same pattern as `MovementTestSceneBuilder`.
    - Batch entry `Orcapafea.Daedalus.Dev.TrapTestSceneBuilder.Build`; menu Daedalus Dev → Rebuild Trap Test Scene.
    - Every component setting is written through `SerializedObject` (no IVT for Assembly-CSharp-Editor). Assets are updated in place, so GUIDs stay stable.
- **Assets in `Assets/Dev/TrapTest/`:**
    - a **seamless single-tile ground** (no edge pixels) as a plain `Tile`
    - a spike sprite with transparency as a Hazard `DaedalusTile`
    - checkpoint and exit sprites
    - a palette
    - All sprites: 16 PPU, point filter, **Mesh Type Full Rect**.
- **Layout:** ~60 × 16, ASCII in the builder, sections left to right, with a tagged Main Camera (follow) and a Player with default settings, On hazard Respawn at checkpoint:
    - start inside the zone of a nearby Drop Floor (fixture: start inside a zone)
    - Drop Floor (Vanish, Solid, zone) over a pit
    - Pop Spike (Appear, Hazard, zone) on the floor
    - Fake Floor painted on Background over a pit (no trap, per the guidance)
    - hidden block (Solid, Appear, Touch) above a jump
    - checkpoint, then a painted spike strip, inside a second Drop Floor's zone (fixture: checkpoint inside a zone)
    - **adversarial:** a trap over painted tiles; an off-grid trap (+0.3, 0)
    - exit
- **Self-check:** exactly one Player; the counts of ground, spike and background tiles equal the ASCII counts; 5 traps with the expected actions, triggers and kinds; 1 checkpoint; 1 exit.
- **Picture:** a separate wide camera renders the whole map to `TestResults/trap-dev-scene.png` (2 tiles of margin).
- **Run:** `Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.TrapTestSceneBuilder.Build -logFile TestResults/trap-scene.log` (6000.6.7f1, dev project closed). Also re-run the movement builder (its pits now kill; the layout is unchanged).
- **Verify:**
    - `[automated]` exit 0 on both builders.
    - `[manual]` Look at `trap-dev-scene.png`: each section readable; the Vanish trap indistinguishable from the ground; no seam between the trap and painted ground.
    - A second run keeps every GUID; PNGs are LFS (`git check-attr`).

### Step 17: Run all tests
- `python Tools/unity_tests.py` (6000.0.85f1 and 6000.6.7f1, EditMode and PlayMode). Close the dev project first.
- Fix failures in the code or the test; never weaken an assertion to pass.
- Inspect, from **both** editors: `*-render-smoke.png`, every `*-movement-*.png` and every `*-traps-*.png`, plus both dev-scene pictures. Record what was seen in the phase log.
- Confirm the 6000.0 compat lock file: ugui 2.0.0, Input System 1.11.2.
- Do not continue until all four runs are green with no compiler warnings.

### Step 18: Operator editor check (STOP for the operator)
In the dev project (6000.6.7f1). Record the outcome in the phase log.
- **Do:**
    1. Open `TrapTest.unity`. Look at the Scene view without selecting anything.
    2. Select each trap in turn; hover each setting.
    3. Add a new object: GameObject > 2D Object > Sprites > Square; Add Component > Daedalus > Trap. Pick the ground tile from the bench palette's assets; set Size 4 × 1.
    4. Switch Trigger between the two options; switch Action between Vanish and Appear.
    5. With Trigger = zone, drag the zone handles; press Fit zone around trap; Undo twice.
    6. Move the trap off the grid; press Snap to grid.
    7. Duplicate the trap (Ctrl+D) and move the copy; make a prefab of it and drop a second instance.
    8. Select both copies and edit Delay together.
    9. Set the Tile to the spike tile, then to a Rule Tile if one is available (else skip and note).
    10. Select the Checkpoint with the Player on Return to safe ground, then on Respawn at checkpoint. Select the Exit.
    11. Press Play, then Stop.
- **Observe:**
    - every trap's outline is visible unselected; Appear traps show a faded ghost in the Scene view
    - the zone box appears on the selected Zone trap and follows the handles in whole tiles
    - the zone field and its button show only for "Player enters zone"
    - six settings at most, each with a tooltip
    - the story sentence changes with every setting and reads true
    - the over-painted and off-grid fixtures show their help boxes; Snap to grid fixes the latter
    - the copies keep their zones relative to themselves
    - the Checkpoint shows the no-effect note only under Return to safe ground
    - each play-start warning appears once in the Console, with the object selected on click
- **Must be absent:**
    - any error or exception
    - a ghost in the Game view or in Play
    - a trap drawn half a box away from its outline
    - a Data Version field or any internal number in the Inspector
    - a need to add any other component
    - a Move or Fall option
- Fix what fails, re-run Step 17, and repeat the check.

### Step 19: Operator play-check and tuning (STOP for the operator)
In `TrapTest.unity`, at the laptop's normal refresh rate; keyboard, and a gamepad if available.
- **Do:**
    - walk out of the start and back in
    - fall for each trap; then beat each one on purpose
    - die in the pit after the checkpoint
    - switch On hazard to Return to safe ground during Play and die again
    - touch the exit; press keys at once, then after a second
    - hold Jump through a death
    - jump along the spike strip's edge and graze it
    - stand next to (not on) a Drop Floor
- **Observe and report (what you saw, not whether it matches):**
    - how long the death beat feels before you are back
    - whether each trap fired when you expected (at the default zone a walked-onto Drop Floor vanishes just in front of the player, not under them: say whether that reads as a troll)
    - how close you can get to spikes before dying
    - where you came back after each death
    - the counter's bump
    - the camera on respawn
    - the win screen's wait
- **Must be absent:**
    - a death you can't see the cause of
    - a trap firing again without a death
    - a trap firing at once on respawn
    - coming back on a trap or inside a zone (Return to safe ground)
    - a jump fired by a held Jump on respawn
    - a camera slide on respawn
    - the win screen accepting a key in its first second
    - any Console error
- **Tuning:** Claude changes `LevelRules` on the operator's words ("beat too long", "spikes too strict"), one change per replay. Keep a tuning table in the phase log (setting, old, new, why).
- **Gate:** the operator says the troll bench feels right.

### Step 20: Make the tuned values the defaults
- **MODIFY** `LevelRules.cs`, `LevelRulesTests` pins, `entities.md` mirror (Notes), and the dev bench if a zone default moved.
- Re-run Step 17 and look at the pictures again.
- **Verify:** `[automated]` all green; `[grep]` no tuned number outside `LevelRules`, its test and entities.md.

### Step 21 (Final): Code review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on every changed file. Ask specifically about:
    - per-tick and per-frame allocations (`LevelStepper.Step`, `RunFrame`, the HUD strings, the burst)
    - `FindObjectsByType` only in `PlayStartScan` users, the Tilemap pick / decoration check, and editor `OnEnable`s; `Camera.main` only in `Start`
    - no authored-asset writes; editor writes always Undo-recorded
    - the inspector carries no gameplay logic (story, box and zone come from `HelpTexts` / `TrapLook` / `LevelRules`)
    - both ends of every wire:
      - `TrapChanges` → renderer enable
      - `ActiveCheckpoint` → tint
      - `Deaths` → counter
      - `Phase` Won → win screen → play again → `Begin`
      - `Teleported` → interpolation snap → camera cut
      - `health.onHazard` → `SetRule` (live)
      - each new warning → its condition
      - trap / checkpoint / exit bounds → map include
    - enum values and serialized names as in this prompt; the assembly direction
- Fix any HIGH/MED issues, re-run Step 17, and report the review result to the user.

---

## Completion Criteria

- [ ] `TileKind.Hazard = TileCollisionKind.Hazard = 4`; stale comments fixed. Hazards are non-blocking and kill through the 0.2 inset core; no skip at max fall speed or max push speed (cases counted).
- [ ] `LevelRules` holds every death / respawn / trap number, pinned by tests and mirrored in entities.md.
- [ ] `LevelStepper`:
  - [ ] the tick order is as specified; death wins over an exit
  - [ ] a 24-tick beat with input ignored and pending delays cancelled
  - [ ] respawn with `Teleported` on one tick; no held-jump fire after respawn
  - [ ] the counter counts every death since the start; `Begin` resets everything
  - [ ] zero allocations (planted failure seen)
- [ ] Safe ground: only standing fully on painted non-hazard tiles, outside every trap trigger and trap box. A moved start or respawn lands on non-hazard support, clear of hazards and present Solid traps (a Player placed on a spike strip lives).
- [ ] Checkpoint and Exit: touch (margin) sets the respawn point / wins; checkpoint tint; win lock 60 ticks; play again restarts with counter 0.
- [ ] Trap:
  - [ ] Vanish / Appear, Zone / Touch, Delay, once per life
  - [ ] re-armed only after leaving the trigger, from every start source; every death resets all traps
  - [ ] painted tiles win; Solid beats Hazard
  - [ ] Appear Solid pushes out (ties: up, left, right, down), else crushes
- [ ] Trap, Checkpoint and Exit components: names, enum values, defaults, ranges, tooltips, menus and `dataVersion` 1 pinned; at most 6 visible Trap settings; mode-only zone; centred-box anchor (or the operator's choice).
- [ ] Player Health section `health.onHazard` (0 = Return to safe ground), live in Play.
- [ ] Follow camera: snap, clamp, cut; attached automatically to the main camera.
- [ ] uGUI 2.0.0 declared, the compat lock file shows 2.0.0, guards updated (planted failure seen); HUD and win screen pictures inspected on both editors.
- [ ] Editor: inspector, story, warnings, Fit zone, Snap to grid, zone handles, ghost in Edit mode only; Undo-safe and multi-object safe.
- [ ] Every new warning appears once in plain words; a clean trap level warns about nothing.
- [ ] The trap bench builds with its self-check passing; its picture was inspected; GUIDs stable.
- [ ] All suites green on 6000.0.85f1 and 6000.6.7f1 with no compiler warnings.
- [ ] The operator editor check passed.
- [ ] The operator play-check passed; tuned values are in `LevelRules` and entities.md.
- [ ] Code review: no open HIGH/MED.

---

## Notes to GDD

Synced at completion (GDD v0.09 → v0.10). **(proposal)** marks Claude's fill-ins; the operator confirms them with the completion report.

**Settled 2026-11-02 (operator: "all recommended"); write each into the GDD:**
1. **systems.md § Combat & Damage / § Traps:** hazards (tiles and Hazard traps) are non-blocking and lethal on overlap of the player's inset core (0.2 tile per side **(proposal)**); a one-row strip can't be skipped at max fall speed.
2. **§ Combat & Damage, Safe ground:** recorded only while standing fully on painted non-hazard tiles and outside every trap trigger. Claude adds: and not overlapping any trap's box **(proposal)**.
3. **§ Traps:** the re-arm rule applies to every start source (play start, checkpoint respawn, safe-ground return).
4. **§ Death and respawn:** the respawn point starts where Play began.
5. **§ Death and respawn:** during the beat input is ignored; a held jump doesn't fire on respawn; pending Delays cancel; death wins over an Exit touched the same tick; the camera cuts on respawn.
6. **§ Traps:** a Solid trap that appears on the player pushes it out the shortest free way (ties up, left, right, down **(proposal)**), else crushes. A start inside a visible Solid trap is moved out by the start search.
7. **§ Traps:** the zone is stored relative to the trap's object. Sharing a zone = multi-object editing or same-position copies; **remove "Copy / Paste Component Values"**. The "Fit zone around trap" button; Scene-view handles snap to whole tiles **(proposal)**.
8. **systems.md § Traps and entities.md § Trap, Zone row:** default zone = the trap's box grown **upward by 1 tile** (replaces "grown by 2 tiles"). Phase 4 may give Fall its own default.
9. **§ Camera:** a minimal snap follow clamped to the map, cut on respawn, attached automatically to the main camera, lands in `traps`. Smooth follow and look-ahead stay in `world-systems` (Roadmap §1 text).
10. **§ Death and respawn:**
    - uGUI HUD: a skull and number in the top-left with a bump, counting every death since the start
    - win screen: freeze, dim, "You made it!", "Deaths: N", input ignored 1 s, any key plays again (counter 0, traps reset)
    - CLAUDE.md Project Config: package dependencies += `com.unity.ugui` 2.0.0
11. **§ Traps:** a trap over painted Collision tiles: painted tiles stay in charge, with a warning.
12. **§ Traps, Shape and look:**
    - a SpriteRenderer in Tiled mode with the Tile's sprite; Appear shows a faded ghost (35–40 % **(proposal)**) in Edit mode only
    - Rule Tiles and animated tiles can't be drawn on a trap
    - always-on outline gizmo
    - the box is centred on the object and snaps to whole cells (**deviation**, open question 1)
    - sprites should be Mesh Type Full Rect
13. **§ World & Level Elements:** pits are death only below the map bottom, with no per-column rule. Replace "falling out of the bottom resets to the start (temporary…)" in § Movement rules → Map bounds.
14. **systems.md § Editor Tools:** the warnings (over painted tiles, a hazard on a decoration Tilemap, off-grid **(proposal)**, no tile, a tile that can't be drawn, not Full Rect, scaled or rotated); the Checkpoint no-effect note; the early-zone line in the story; the Checkpoint tints while active **(proposal)**.
15. **§ Traps / § Death and respawn:** one touch rule (hitbox grown by 0.0625 **(proposal)**) for Touch triggers, Checkpoint and Exit; hazards use the inset core.
16. **conventions.md § Testing:** the trap bench `Assets/Dev/TrapTest/` (generated); `AsciiLevel` gains `^` (hazard); screenshots `*-traps-*.png`. CLAUDE.md: Testing table += `TestResults/*-traps-*.png`; Build/run += the trap bench command.

**Other GDD edits:**
- **entities.md § Player tunables:** Health section `health.onHazard` (Return to safe ground 0 / Respawn at checkpoint 1); the field list gains `health`.
- **entities.md § Trap:**
  - Action values Vanish (default **(proposal)**; Move becomes eligible in phase 4) and Appear; Move and Fall are phase 4
  - Delay 0–3 default 0; Size default 3 × 1, range 1–30 **(proposal)**
  - a Tile of any kind other than Hazard makes a Solid trap **(proposal)**
  - serialized names `trigger`, `zone`, `action`, `delay`, `size`, `tile`; enums Zone 0 / Touch 1, Vanish 0 / Appear 1 (2, 3 reserved)
  - the `LevelRules` table: beat 0.4 s / 24 ticks, inset 0.2, margin 0.0625, zone +1 up, win lock 1 s / 60 ticks
- **entities.md § Placeable elements, Checkpoint / Exit:** the box = the sprite's bounds, else 1 × 1 **(proposal)**; respawn at the box's bottom centre; the latest touched checkpoint wins; it is recorded under either On hazard rule.
- **entities.md § Terrains:** `DaedalusTile` kind 4 = Hazard, declared (drop "reserved").
- **systems.md § World & Level Elements:** TileKind line: 4 = Hazard (drop "reserved").
- **entities.md § Authored data:** `Trap`, `Checkpoint`, `Exit` carry `dataVersion` 1.
- **architecture.md:**
  - § System Map: sim += `LevelStepper` (it owns its trap / checkpoint state in place; still pure and allocation-free), `LevelRules`, `TrapSpec`, `Box`, the trap-cell layer in `TileGrid`; runtime += `Checkpoint`, `Exit`, `Trap`, `LevelCollector`, `PlayStartScan`, `TrapLook`, `HelpTexts`, `CameraFollow`, `PlayHud`, `DeathBurst`; editor += the Trap / Checkpoint / Exit inspectors and the ghost gizmo; the runtime references uGUI
  - § Time Model: the death beat in ticks; `Teleported` now marks respawns
  - § Data Flow: the map include grows by traps, checkpoints and exits; every start source resets the level
  - the rule-5 exception gains "the main camera (`Camera.main`) at play start" **(proposal)**
- **CLAUDE.md:** the Architecture rule 5 exception sentence adds the main camera.
- **decisions.md:** ADR rows for items 1–16 as settled (operator, 2026-11-02), and for the anchor once the operator answers open question 1.
- **Glossary.md (Draft):**
  - new: Hazard (tile kind), Death beat, Safe ground, Respawn point, Death counter, Win screen, Checkpoint, Exit, Trigger, Zone, Touch (contact margin), Hazard core, Re-arm, Push-out, Crush, Level stepper, Level rules, Trap cells, Follow camera, HUD
  - extend the existing **Trap** and **Map bounds** entries rather than duplicating them
- **framework.md:** uGUI built-in versions per editor and the `UnityEngine.UI` assembly name; `LegacyRuntime.ttf`; the Tiled SpriteRenderer + Tight mesh result; `DrawGizmo` / `DrawMeshNow` ghost; `ObjectFactory.componentWasAdded`; `BoxBoundsHandle` snapping.
- **Deferred.md:**
  - "Edit-mode trap look syncs only through the Trap inspector / Add Component" (trigger: a student or operator report of a stale look after a prefab override or paste; fix shape: an `ObjectChangeEvents` hook)
  - "No Exit in the scene isn't warned" (trigger: the `editor-tools` validation list)
  - The existing "warnings only in the Console" entry stays open; its fix shape now points at the Trap inspector's help-box pattern for reuse.

**Open questions for the operator:**
1. **Trap anchor. Decided (operator, 2026-11-02): centred box, as recommended; open questions 2–7 accepted as written.** Recommended: the box **centred on the object**, not the bottom-left cell. The trap is drawn by its own SpriteRenderer in Tiled mode, which draws around the sprite's pivot (centre for tile sprites). A bottom-left anchor would need a child object or generated sprites, which loses item 12's prefab / undo / duplicate safety. Costs: odd sizes sit on half-tile positions (Snap to grid button, outline, warning, snap in Play).
   - Reviewer recommends: the centred box. A bottom-left anchor needs a child object or offset sprites, breaking item 12's one-component, Undo- and duplicate-safe shape; the half-tile cost is visible, and F2's rounding fix keeps the snap predictable (round 1, F11).
2. **Trap Action default.** Recommended: Vanish (value 0) for now, since entities.md's default Move doesn't exist until phase 4. Phase 4 decides whether new traps default to Move; saved traps never change.
3. **Proposed numbers** for the play-check:
   - beat 0.4 s, inset 0.2, contact margin 0.0625, zone +1 up, Size 1–30
   - win lock 1 s, ghost alpha 35–40 %
   - push ties up / left / right / down
   - the early-zone threshold > 1 tile
4. **Checkpoint box and feedback.** Recommended: the box is the sprite's bounds (else 1 × 1); only the active checkpoint is tinted; touches are recorded under either On hazard rule.
5. **Safe ground also excludes trap boxes, and a moved start or respawn must stand on non-hazard support away from hazards** (death-loop guards). Claude's additions; recommended. (Round 1 F1: without the support condition the hazard guard only lengthened the loop; folded in Step 3.)
   - Reviewer recommends: yes to both guards, conditional on F1 being folded (it is) (round 1, F12).
6. **`PlayerSession` keeps its name** although it now runs the level. Revisit in `world-systems` when a level runner may take over.
7. **No warning when there is no main camera** (the scene shows nothing anyway). Recommended.

Reviewer recommends: accept questions 2, 4, 6 and 7 as written (Vanish default until Move exists; sprite-bounds checkpoint box, only the active one tinted, recorded under either rule; keep the `PlayerSession` name; no camera warning); question 3's numbers are play-check starting points (round 1, F13).

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. CLAUDE.md has no phase-status line; apply its Notes edits (dependency, rule-5 exception, Testing table, Build/run).
3. Write `memory/logs/phases/phase3-log.md`: per step, the tuning table, the inspected pictures, the fallbacks and checks that fired (Tight mesh, compat lock file), and the editor and play-check outcomes.
4. Update `MEMORY.md` current state.
5. Apply the Notes to GDD (GDD v0.10, Changelog, Roadmap §0 row and Milestone 2 text), decisions.md, framework.md, and lessons-learned.md if a rule was learned.
6. **Deferred items:** file the Deferred.md entries listed in Notes (Source = Phase 3). Delete any entry whose trigger fired. See CLAUDE.md → Log Discipline.
7. Ask the operator, then commit all phase work as `v0.10: Phase 3 — death and respawn: hazards, pits, checkpoint, exit, static traps, HUD, follow camera`.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked, and at Steps 18 and 19, which are planned operator stops. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: phase3-death-and-respawn
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-02T13:50:00+09:00
blocking: 1
recommendations: 3
---

# Round 1: `Documents/Phases/phase3-death-and-respawn.md`

## What I could run, and what I checked

- **Shell:** yes. I used `device_bash` on the operator's office PC (`daedalus` folder) and a cloud clone at the same HEAD (`88160d1`; the device differs only in six modified dev PNGs plus the untracked phase doc and review folder). I read files with `grep` and `sed`. I did not run Unity, the test suites, a build or the dev-scene builders.
- **Read in full:** the whole prompt (800 lines).
- **Checked against code:** `TileKind.cs`, `TileCollisionKind.cs`, `DaedalusTile.ToSimKind` (kind 4 still throws), `TileGrid` (single array; outside reads Solid, below reads None), `TileCollision.Blocks` / `Support` / `WallGap` / `BoxIsFree` (none treat 4 as blocking, so Step 1's "collision unchanged" holds), `StartPlacement.Find`, `PlayerStepper` (fall-out reset at lines 199–205, `CreateState` sets `StartX/StartY` at 80–81, `Step` clears `Teleported` at 93), `PlayerState` (`StartX/StartY` at 45–46), `PlayerSession` (`new FixedStepAccumulator()` on restart; `FixedStepAccumulator` is a `sealed class`, so `Reset()` really does remove an allocation), `Player.StartPlay` / `Restart` / `RunFrame` / `PickCollisionTilemap`, `PlayerControls` jump latch, `RopeCollector.FindSorted` (already filters `isActiveAndEnabled`, so `PlayStartScan` is behaviour-neutral), `AssemblyDirectionTests` (subset asserts at lines 24 and 38), `PlayerLayoutTests` (13 fields, so 13 → 14 is right), `TileAssetTests` (kind-4 throw at line 39), `PlayerPlayTests` (`(TileCollisionKind)4` at 135, `Current.StartX` at 107), and `RunAndFallTests`.
- **Numbers recomputed:** beat `(int)(0.4·60+0.5)` = 24 ✓; core 0.35 × 1.1 ✓; win lock 60 ✓; `SecondsToTicks(0.5)` = 30 ✓; `DefaultZone` (2,1)–(5,2) → (2,1)–(5,3) ✓; Delay 0.5 s fires at T+30 under "Pending > 0 counts down, at 0 fire" ✓; `CanPlayAgain` false for ticks 1–59 and true at 60 ✓; max fall 24 t/s = 0.4 tile/tick ✓; wall-jump push 12 × 1.5 = 18 t/s ✓; `CellMin` cases (1.5, 0.5) / (1.8, 0.5) / 2 × 2 at (1, 1) → (0, 0) ✓; jump take-off speed 2·3.5/0.35 = 20 t/s (0.33 tile/tick), used in F5.
- **Taken on trust (not checkable here):** that uGUI resolves to 2.0.0 on 6000.0.85f1 and is built in on both editors; that `UnityEngine.UI.Text` is not `[Obsolete]`; how Tiled draw mode behaves with Tight meshes; how code-made SpriteRenderers render under the URP 2D renderer without a 2D light. The prompt already schedules a check for each, so none is a finding.

## Findings

### F1: The start-on-spikes guard doesn't stop the death loop it is meant to stop
- **Type:** blocking · **Category:** accuracy · **Severity:** high
- **Location:** Step 3, `StartPlacement` bullet ("Otherwise a start on spikes would die and respawn there forever"); Open question 5; Completion Criteria ("Start and respawn placement avoid hazards").
- **Defect:** `StartPlacement.Find` looks for the nearest *free* cell-centred spot. It never asks for ground underneath, and hazards don't block. Take a Player placed standing on a spike strip that lies on a floor (floor row f, spikes row f+1, feet at f+1). The core touches the spikes, so the start counts as blocked. The nearest spot that is free and hazard-free is one row up (feet at f+2, core bottom at f+2.2). Ties go up, and on a strip wider than one cell, up is also strictly nearest. That spot is mid-air over the spikes. The player falls one tile, the core touches row f+1, and the player dies. Then it respawns there:
  - Under **Respawn at checkpoint** with no checkpoint, it goes back to the start, which is the same placement.
  - Under **Return to safe ground**, `Begin` sets safe ground to the placed point, and the player never stands on painted ground long enough to record a new one.
  
  So the loop still runs forever. It just takes about 30 ticks (24 for the beat plus the fall) instead of 24. The Step 3 test ("moves to the nearest free non-hazard spot") passes, and the claimed guarantee still fails. Open question 5 asks the operator to approve this guard on that false premise.
- **Fix (smallest):** when the start or respawn is *moved*, accept only a candidate that stands on non-hazard support (`Support != None` at the candidate). If no such spot is within the radius, return `Found = false`, which already raises the existing warning. An unmoved start stays as it is: Phase 2 allows mid-air starts, and a student who places one over a pit can see the result. Add a test: a Player placed on a floor-mounted spike strip lands on non-hazard ground and lives for 120 ticks. Reword open question 5 to match.

### F2: Trap look and sim box can disagree when Size is out of range
- **Type:** non-blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 11 (`size` "clamped … on conversion"; `ApplyPlayLook` sets "`size = size`"; `CellMin` uses `size`); Step 13 `SyncLook`.
- **Defect:** a `Vector2Int` field has no `Range`, so a student can type 0 × 1 or 40 × 1.
  - `ToSpec` clamps the size, so the sim gets 1 × 1 or 30 × 1.
  - `ApplyPlayLook` / `SyncLook` draw the raw size, so 0 × 1 draws nothing and 40 × 1 draws 40 tiles.
  - If `CellMin` uses the raw size, the snapped centre differs from the sim box too.
  
  The result is an invisible solid block, or ten tiles of drawn floor with nothing under them. Both ends of this wire must read one clamped size (Architecture rule 7).
- **Fix:** add a `TrapLook.ClampedSize(Trap)` that `CellMin`, `SnappedCentre`, `ToSpec`, `ApplyPlayLook`, `SyncLook` and the gizmo all use. Add a test where size 40 × 1 gives a 30-wide spec and a 30-wide renderer, both centred on the same cell box. Also, `Mathf.RoundToInt` rounds half to even, so half-tile-off positions snap left or right depending on parity. Using `Mathf.FloorToInt(v + 0.5f)` keeps Snap to grid predictable.

### F3: Two traps pushing on the same tick make the reversed-order test unsound
- **Type:** non-blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 6, `Fire(i)` push-out, and the `TrapTests` Overlap bullet ("Firing order across traps doesn't change the result: run the scenario with the trap array reversed and assert identical states").
- **Defect:** push-out runs inside each `Fire(i)` against that trap's box alone. Take two Solid Appear traps with overlapping boxes that fire on the same tick. Firing A pushes the player out of A, where B's cells are still empty. Firing B then pushes the player out of B. With the order reversed, the player ends somewhere else, or one order crushes and the other doesn't. Cell stamping is order-independent because `RebuildTrapCells` stamps everything, but the player's position is not. The sort makes the result deterministic, but the stated property is false. A test built on it either passes by accident (no push in the scenario) or fails.
- **Fix:** either (a) restrict the reversed-order test to the cell layer and the `Present` flags, and say that push results follow the array order; or (b) better, have `Fire` only mark and rebuild, and run push-out once after the tick's fires, against the union of the Solid traps that just became present. Option (b) makes the property true and costs no more.

### F4: Two trap tests expect a firing moment that the zone geometry doesn't produce
- **Type:** non-blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 6, `TrapTests` Drop Floor and Pop Spike bullets.
- **Defect:** the default zone is the trap's box grown up by 1. Its x-range equals the box's, and overlap is strict.
  - **Pop Spike, "walking in dies the same tick":** the zone fires on the first tick the hitbox's leading edge passes the trap's MinX. The core is inset 0.2, so it reaches the spikes 0.2 tiles later: 2 ticks at the default run speed (6 t/s = 0.1 tile/tick), and 1 tick at max run speed. Death is not on the trigger tick.
  - **Drop Floor, "Standing on it fires; the floor is gone the same tick; the player falls next tick":** a walking player fires it with about 0.0001 tiles of hitbox over the trap and 0.75 still over painted floor. `Support` straddles, so the player doesn't fall next tick. The scenario can't start the player standing on it either, because the re-arm rule stops a play start inside the zone from firing.
  
  **Design consequence, for the play-check:** with this default, a Drop Floor vanishes *in front of* a walking player rather than under them. It still trolls, because running at 6 t/s covers the gap faster than a reaction. But the operator should judge it knowingly, and the Step 19 observe list ("whether each trap fired when you expected") is where it will show up.
- **Fix:** write the expectations to the geometry. For Pop Spike, it fires on the edge-entry tick and death follows when the core arrives (assert both ticks). For Drop Floor, it fires on edge entry with the player still supported, and the player falls once the hitbox has fully left the painted floor. Alternatively, enter from above (falling into the zone).

### F5: The hidden-block test needs its start set so the head is inside the box
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6, `TrapPushTests`, first bullet.
- **Defect:** a Touch trigger fires when the hitbox grown by 0.0625 overlaps the box. Rising at up to 0.33 tile/tick, the head on the fire tick can be anywhere from 0.0625 below the box to about 0.27 inside it.
  - Head inside: the push-down path runs, which is what the test means.
  - Head below: there is no push, and the ordinary ceiling sweep bonks the player on the next tick.
  
  The player bonks either way, but the test only covers the push path if its start height is chosen to put the head inside on the fire tick. Otherwise it can pass while push-out is broken.
- **Fix:** add a precondition assert that the hitbox overlapped the cell box on the fire tick (the prompt's own "every precondition is asserted" rule). Also add a second case with the head just below, where the block appears and the bonk comes from the sweep.

### F6: A reset must bump `TrapChanges`, not only `Fire`
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6, the Reset bullet (no mention) versus `TrapChanges` ("bumped whenever any `Present` changes"); Step 11 `Render` redraws only on change.
- **Defect:** the reset bullet lists `Present` initial, `Armed`, `Fired`, `Pending`, rebuild. It doesn't list the counter, and `Fire` is the only place the counter is named. If an implementer bumps only in `Fire`, then after a respawn the sim's Drop Floor is back but its renderer stays disabled: invisible ground (Architecture rule 7).
- **Fix:** say "Reset bumps `TrapChanges` when any `Present` changed". Add an EditMode assert that `TrapChanges` moves on a respawn after a fire. The PlayMode Drop Floor screenshot also covers this, but the EditMode assert catches it earlier.

### F7: The editor's over-painted check reads a different Collision Tilemap from Play
- **Type:** non-blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 13, `TrapEditor` ("reads the Collision Tilemap through `Player.PickCollisionTilemap()`").
- **Defect:** play start uses `collisionTilemap != null ? collisionTilemap : PickCollisionTilemap()` (`Player.cs:156`). `PickCollisionTilemap` alone returns the Tilemap named Collision, else the only Tilemap, else null. Suppose a student has assigned a renamed Collision Tilemap in a scene with several Tilemaps. The inspector shows no over-painted warning, while play start logs one. Or the inspector checks the wrong Tilemap. The editor then repeats play logic, and gets it wrong (Architecture rule 3).
- **Fix:** add an `internal Tilemap ResolveCollisionTilemap()` on `Player` (field first, else pick). Play start and both inspectors call it. The editor already finds the scene's Player for `CheckpointEditor`, so `TrapEditor` can do the same in `OnEnable`.

### F8: Student-typed Rect and Size values can throw at play start
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 2 (`Box` constructor "throws if max < min"); Step 11 `zone` (`Rect`) and `ToSpec`.
- **Defect:** `zone` is a serialized `Rect`. The handles keep it positive, but the Debug inspector, a script or a bad prefab override can make its width or height negative. `ToSpec` then builds a `Box` that throws inside `Player.Start`, so the whole Player is disabled with an exception. CLAUDE.md's error handling puts authored data in the "degrade, warn in plain words" class, not "throw".
- **Fix:** have `TrapLook.ToSpec` normalise the Rect (min/max swap) and clamp Size (see F2). Keep the throwing constructor for sim-internal invariants only.

### F9: A Trap on an Exit or Checkpoint object gets a look that phase 4 will change
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 11 `Trap` (`RequireComponent(SpriteRenderer)`); Step 13 `SyncLook` and the `componentWasAdded` hook; GDD `systems.md` § Traps ("On an Exit, the trap moves the exit instead and has no shape or tile").
- **Defect:** adding a Trap to the Exit object in phase 3 runs `SyncLook`. That replaces the Exit's sprite with the tile's sprite, or with none, and switches it to Tiled. It is an Undo-recorded but silent overwrite of the student's object, and it also creates a 3 × 1 Solid trap where the exit stands. Phase 4 then gives a Trap on an Exit a different meaning (no shape, no tile, moves the exit). A scene saved in phase 3 would silently change behaviour on update, which Architecture rule 4 forbids.
- **Fix (minimal):** while the object also carries an `Exit` or `Checkpoint`, `SyncLook` and play start leave the renderer alone and ignore the Trap. Show a plain-words help box ("A Trap on an Exit comes in a later update"). Add one play-start warning text in `PlayerWarnings`. Alternatively, accept the change and file it in Deferred with the phase 4 trigger.

### F10: Two small bookkeeping errors in the test and grep steps
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 4 Verify; Step 4 `RunAndFallTests` bullet.
- **Defect:**
  - Step 4's `[grep] StartX gone from P/Runtime` can't pass, because Step 4 itself adds `LevelStepper.StartX` / `StartY` (the "State it owns" list).
  - `RunAndFallTests` has two fall-out tests (`FallingOutOfTheBottomResetsToTheStart` at line 200 and `TeleportedIsSetOnlyOnTheResetTick` at 228), not three. `TeleportedStaysFalseInOrdinaryPlay` (258) already exists and is the "replacement" the bullet names.
- **Fix:** change the grep to `PlayerState.StartX|s.StartX` (or "no `StartX` in `PlayerState` / `PlayerStepper`"). Change the test bullet to: two removed, one added (the below-row-0 fall), one kept.

## Recommendations (open questions)

### F11: Open question 1, trap anchor
- **Type:** recommendation · **Category:** design
- **Recommend:** the centred box, as the prompt proposes. A SpriteRenderer in Tiled mode draws around its pivot, so a bottom-left anchor needs a child object or offset sprites. Either one breaks the one-component, Undo- and duplicate-safe shape of item 12. The half-tile cost for odd sizes is real but visible: there is an outline, a Snap button and a warning. F2's rounding fix keeps the snap predictable.

### F12: Open question 5, death-loop guards
- **Type:** recommendation · **Category:** design
- **Recommend:** yes to both guards (no safe ground on or inside a trap box, and start placement avoids hazards), on the condition that F1 is folded. Without F1, the second guard only shortens the loop.

### F13: Open questions 2, 4, 6 and 7
- **Type:** recommendation · **Category:** design
- **Recommend:** accept as written.
  - Q2: Vanish is the right default until Move exists, and saved traps never change.
  - Q4: sprite-bounds box, only the active checkpoint tinted, recorded under either rule.
  - Q6: keep the `PlayerSession` name for now.
  - Q7: no warning for a missing main camera.
  
  Q3's numbers are play-check starting points and need no answer before building.

## Reviewer's view

I would build this prompt once F1 is folded. The world step's tick order, the beat, the re-arm rule and the wiring list are sound, and the numbers I recomputed all hold. F1 is the only finding where the document's stated guarantee is false. F2, F3 and F7 are wiring and seam defects that would produce visible wrong behaviour if built literally, so they are worth folding now rather than finding at Step 17. F4, F5, F6, F8, F9 and F10 are small corrections.

---

## Review round 2

---
slug: phase3-death-and-respawn
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-02T14:02:00+09:00
blocking: 0
recommendations: 0
---

# Round 2 (delta review): `Documents/Phases/phase3-death-and-respawn.md`

## What I could run, and what I checked

- **Shell:** the same as round 1 (`device_bash` on the office PC, and a cloud clone at `88160d1`). No Unity, tests or builds.
- **Delta method:** I diffed the round-1 text against this round's text above `## Review round 1` (`diff --strip-trailing-cr`). There are 21 hunks, and each maps to one disposition in `log.md`. I found nothing changed outside the folds.
- **Re-verified against code:**
  - **F1:** `TileCollision.RowSupport` (`TileCollision.cs:223–236`) counts only `Blocks` kinds and OneWay, so a Hazard row is never support, as the fold assumes. All seven Phase 2 `StartPlacementTests` cases still hold under the support rule:
    - free start: unmoved
    - inside the ground: (2.5, 2), on the slab
    - beside the wall: on the floor
    - both tie cases: left, on the floor; up, on top of the slab
    - below the map: (2.5, 1), on the floor
    - enclosed: not found
    - one-way: unmoved
    
    One side effect, not a defect: the up/down tie case now wins "up" because the mid-air down candidate is no longer eligible, so it stops testing the tie itself.
  - **F1's new floor-mounted strip test:** the start at f+1 on the strip has core overlap, so it is rejected. One row up has Hazard below, so it is unsupported and rejected. The first column clear of the strip, at y = f+1, has a 0.325 gap from the core to the strip and the floor below, so it is accepted. It is within radius 8 for a 4-wide strip ✓.
  - **F2:** `FloorToInt(v + 0.5)` cases. x = 0.5 with width 2 gives −0.5 + 0.5 = 0, centre 1. x = 1.5 gives 1, centre 2. Both snap +0.5 ✓. Under `RoundToInt`, they would snap +0.5 and −0.5 respectively, so the parity claim is real ✓. The round-1 `CellMin` cases are all still (0, 0) ✓, and 0 × 1 clamps to 1 × 1 ✓.
  - **F3:** `ResolveAppeared` pushes once per batch against the bounding box of the appeared Solid traps that overlap the player, with `BoxIsFree` checked on the whole rebuilt grid. That makes player state independent of array order. The reversed-array test now asserts it ✓.
  - **F4:** the rewritten assertions (fire tick, death strictly later, no core overlap before the death tick) hold under the geometry. On the parenthetical tick count, see F15.
  - **F5–F10:** each fold matches its finding. F7's `ResolveCollisionTilemap` matches `Player.cs:156`, and F10's grep no longer collides with `LevelStepper.StartX`.
- **Recommendations F11–F13:** recorded beside their questions as the protocol asks ✓.

## Round-1 findings: status

| Id | Type | Status |
| --- | --- | --- |
| F1 | blocking | **closed**: folded, verified above |
| F2–F10 | non-blocking | closed: folded |
| F11–F13 | recommendation | recorded |

## New findings (all non-blocking, from the folds)

### F14: `ResolveAppeared` should run only for a batch that fired, and clear its list
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6, `Fire(i)` / `ResolveAppeared()` / `RebuildTrapCells()`.
- **Defect:** "run once after each batch of fires (after step 3's delays and after step 5's triggers)" can be read as running twice every tick. That would rebuild the whole trap layer twice per tick (cost = total trap area), which contradicts "changes are rare". The appeared list is called "this tick's", but the push runs per batch. If the list isn't cleared after the delay batch, the trigger batch re-tests those traps. That is harmless after a successful push, but it is unstated.
- **Fix:** "`ResolveAppeared` runs only when the batch fired at least one trap; it clears the appeared list when done."

### F15: The Pop Spike tick count in parentheses is off
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 6, `TrapTests` Pop Spike ("1–2 ticks at run speed 6 depending on where in a tick the edge crossed, 1 at 12").
- **Defect:** at a constant 6 t/s (0.1 tile/tick), the edge-entry tick leaves the edge e ∈ (0, 0.1] past MinX. The core overlaps strictly once the edge is past MinX + 0.2:
  - at T+1 the edge is at e + 0.1 ≤ 0.2, so no overlap;
  - at T+2 it is at e + 0.2 > 0.2, so overlap.
  
  So it is always 2 ticks at full speed 6, except through float rounding exactly at the boundary. It is more than 2 while the player is still accelerating (time to full speed is 0.1 s = 6 ticks). At 12 t/s it is 1 ✓. The assertions don't depend on this number, so only the parenthesis is wrong.
- **Fix:** "2 ticks at full run speed 6 (more while accelerating), 1 at 12".

### F16: The Trap-on-Exit help box and warning are two different strings
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 11's F9 bullet (inspector text "…comes in a later update; this one does nothing yet.") versus Step 14's `TrapOnExitOrCheckpoint` ("…comes in a later update, so this one does nothing yet. Put the Trap on its own object.").
- **Defect:** the prompt's own convention says every warning text lives in `PlayerWarnings` and the editor shows the same strings. As written, there are two texts that will drift.
- **Fix:** the inspector shows `PlayerWarnings.TrapOnExitOrCheckpoint` verbatim.

## Late-pushback test

Would my answer to "what do you think?" add anything not already in these round files? No. My only reservation is the one F4 already put into Step 19: whether a Drop Floor that vanishes in front of the player reads as a troll. The operator's play-check decides that, not this document.

## Reviewer's view

I would build this prompt as written. F1, the only blocker, is folded correctly, and the Phase 2 placement tests survive it. The round-1 seams (F2 size, F3 push order, F7 Tilemap source) are closed in a way that makes their stated properties true rather than merely tested. F14–F16 are wording fixes for the Author to fold on close. Approved.
