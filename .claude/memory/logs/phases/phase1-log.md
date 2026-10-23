# Phase 1 log: `movement-sim` (v0.07, 2026-10-23)

Prompt: [phase1-movement-sim.md](../../../../Documents/Phases/archive/phase1-movement-sim.md). Result: 153 EditMode tests green on 6000.0.85f1 and 6000.6.7f1, PlayMode green, no warnings; code review no HIGH, 2 MED fixed.

## Per step
1. **Purity guards.** Reflection test `SimHasNoStaticMutableState` + `System.Linq` in the forbidden scan. **Seen failing:** a planted `internal static int Counter` in `Runtime/Sim/PlantedStatic.cs` failed it with `Orcapafea.Daedalus.Sim.PlantedStatic.Counter` in the message; file and `.meta` removed, green again.
2. **Grid, ropes, ASCII maps.** As specified. `AsciiLevel` also gained `ParseGrid` (no start required) and `Rows(width, height, cell)` for generated maps.
3. **Collision.** As specified. **Deviation:** new `SupportKind` enum (none / solid / one-way-only) for the `Support` probe and the landing result; not in the manifest. Sweeps return through `ref`/`out` rather than result structs.
4. **Settings, internals, derived.** As specified; added `MovementInternals.TickSeconds`, `HitboxHalfWidth`, `GripHeight` (derived consts). Worst case per tick measured 0.617 tile.
5. **Nearest free start.** As specified.
6–11. **Stepper.** Written in one pass on the fixed tick order; each step's tests cover its slice. Implementation choices beyond the prompt (all listed in systems.md as proposals or covered there):
   - Cling and slide **entry** are evaluated post-move; slide/cling **exit by input** at item 4; no entry on a tick where a jump or drop fired.
   - `Land()` also ends the wall-jump lock. The reviewer notes this is required: item 5 freezes vx during the lock, standing too.
   - Rope grabs only from the air or standing; standing, Down grabs only from the one-way top the rope hangs through.
   - Ledge pop checks the pop box is free and supported (the "cell above the run is free" check is implied by the box).
   - Climbing off the bottom of a climbable run lets go without wall coyote.
12. **Gates and matrix.** All gates pass at defaults:
    - **Ledges:** 3 yes / 4 no; with double jump, 6 yes / 8 no; open-air double-jump apex 7.000.
    - **Gaps:** jumped at x 12.900 on W+6; 5 clears (landed x 17.0), 6 doesn't.
    - **Corridor:** 2 yes / 1 no.
    - **Chimney gains per jump by width:** 1: 0.33 · 2: 1.94 · 3: 3.11 · 4: 3.44 · 5: 1.82 · 6: −0.18, so widths 1–5 are climbed and 6 is not. This matches the reviewer's model (up to 5 wide).
    - **Script A** (DoubleJump off): starts 27.522, 26.744, 25.767, 24.789, i.e. a loss of 0.778 / 0.978 / 0.978 per jump. The reviewer's model said 0.38–0.58; the gate (≥ 0.1) holds.
    - **Script B** (exploit loop): starts 17.522, 24.175, 23.197, 22.219, i.e. +6.653, then −0.978 per cycle. Air jumps at slide entry: 1, 0, 0, 0.
    - **Script C** (coyote detour): starts 16.322, 21.244, 18.467, 15.689. Air jumps at slide entry: 1, 0, 0, 0.
    - **Jumps:** apex 3.5000 on tick 21 (gravity 1) and 3.4996 on tick 15 (gravity 2); tap apex 1.2484.
13. **Allocation.** `GC.GetAllocatedBytesForCurrentThread` **does not see allocations on either editor**: a planted `new byte[1024]` gives a delta of 0. Fallback: the Test Framework's `Is.Not.AllocatingGCMemory()`. It works in batch mode on both editors, and its sanity check sees the planted 1 KB. **Seen failing:** an allocation planted in `Step` (`new int[1]`, result used) failed it. A first plant, an unused boxed local, did not register. 672 measured steps over 6 recorded runs covering every branch (asserted). Recorded in `framework.md`.
14. **Runs.** Both editors, both modes, green. On the way, `Tools/unity_tests.py` misreported a compile error as "project is open in another Unity instance", because a compile-error exit leaves `Temp/UnityLockfile` behind. Fixed: compiler errors are checked first.
15. **Code review** (general-purpose agent with the code-review checklist): PASS WITH FLAGS.
    - **MED 1:** on a rope's platform, holding Down grabbed the rope before the jump press, so "hold Down, then jump" rope-jumped instead of dropping. **Fix (proposal):** Down + jump while on a rope lets go without a jump, and the re-grab lockout applies. Test: `HoldingDownThenJumpingOnARopesPlatformLetsGoWithoutAJump`.
    - **MED 2:** climbing down a rope onto a one-way just below its bottom fell through it. **Fix:** `LimitRopeDown` stands the player on it, mirroring the rope-top rule. Test: `ClimbingDownOntoAOneWayUnderTheRopeLandsOnIt`.
    - **LOWs fixed:**
      - no ceiling-corner correction while on a rope (`SweepY`'s flag is now `onRope`);
      - a ground jump clears `WallSide`;
      - `PlayerState.IsStanding` is `readonly`;
      - `TileGrid` names the bad dimension in its exception;
      - test preconditions tightened (the cling while rising; Script A "before contact"; Script B's failure message explains the buffered-apex timing risk; the allocation test asserts the rope jump and the cling jump).
    - **LOW left:** the unreachable `WallSlide && !WallJump` guard in `TryWallJump`, kept as cheap defence.

## For the operator (Phase 2 play-check)
- Every **(proposal)** in systems.md § Movement rules, including the new rope let-go rule from the review.
- Observed: against a wall with wall jump unlocked, a press at the apex is a wall jump (priority), not an air jump. A double-jump ledge is reached by air-jumping away from the wall.
- Open question 1 (gap 5 / 6 until the play-check) and Open question 2 (no `Run` flag) were recommended "yes" / "no flag" by Claude and the Reviewer. They are written into entities.md and systems.md as recommended, still open to change at the play-check.
- Not done in phase 1, by design: the fall-out reset signal for interpolation (Phase 2).

## Post-phase
`CLAUDE.md` has no phase-status line to update, so step 2 of the post-phase cleanup was a no-op. No Deferred entries were filed; nothing in Deferred.md was pending.
