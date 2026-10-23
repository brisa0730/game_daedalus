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
