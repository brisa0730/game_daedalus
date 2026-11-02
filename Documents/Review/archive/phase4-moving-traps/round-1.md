---
slug: phase4-moving-traps
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-02T18:24:47+09:00
blocking: 4
recommendations: 4
---

# Review round 1: `Documents/Phases/phase4-moving-traps.md`

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
