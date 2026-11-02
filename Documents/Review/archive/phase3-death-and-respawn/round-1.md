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
