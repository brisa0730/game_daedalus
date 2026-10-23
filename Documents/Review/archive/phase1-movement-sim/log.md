# Handshake log: phase1-movement-sim

## Opening (2026-10-23T21:02:54+09:00)

**Document:** `Documents/Phases/phase1-movement-sim.md` (Author: Claude Code). Written fresh on 2026-10-23; an earlier uncommitted draft of this phase was discarded by the operator and its handshake residue deleted, so this is a new handshake with no prior rounds.

**What it proposes:** Phase 1 of Milestone 1 (`movement-prototype`, split into `movement-sim` + `movement-runtime`): the pure C# movement sim in `Runtime/Sim/` (tile grid and swept tile collision, run, variable jump with coyote time and buffering, air jumps, one-way platforms and drop-through, wall slide / wall jump, wall climb on climbable tiles with a ledge pop, ladder-style ropes, map bounds, nearest free start), a single source of truth for student-facing defaults and hidden internals, and EditMode scenario, gate-integrity, ability-matrix and allocation tests on both editors. Nothing playable; feel tuning is Phase 2's operator play-check.

**Sources the doc rests on:** `Documents/GDD/` (systems.md § Player, entities.md, architecture.md), `.claude/memory/decisions.md` (rows dated 2026-10-23), `.claude/CLAUDE.md`, the existing sim code and guard tests in `Packages/com.orcapafea.daedalus/`. Companion (not under review): `Documents/Phases/phase2-movement-runtime.md`, which consumes this phase's API.

Nothing is built; this handshake reviews the document only.

## Round 1 dispositions (2026-10-23T21:15:04+09:00)

Verified against the document text (re-read every cited line) and by re-deriving the arithmetic the findings rest on. I did not re-run the Reviewer's tick simulations; its modelled per-cycle numbers are quoted in the doc as "Reviewer's round-1 model" and the tests print the implementation's measured values.

- **F1 — accepted.** Re-derived: after the 12-tick lock the return speed is at most air run speed 6 = 0.1 tile/tick < WallJumpReach 0.125, so a press-every-tick script fires before contact. Step 9 test now asserts "fired with the wall within WallJumpReach" (wall coyote / active slide also count) and prints the measured loss; the ≈0.2 expectation is replaced by the Reviewer's model (0.38–0.58).
- **F2 — accepted, option (b).** Re-derived the loop: wall jump gives a full 3.5-tile rise, an air jump a further 3.5, and slide entry refilled the air jump on the same wall, so height grows per cycle. Chose (b) because it implements the operator's 2026-10-23 decision ("no single-wall scaling … keeps only-climbable-tiles-can-be-climbed") while keeping the genre-standard refill in chimneys; (a) would contradict that decision for any player with both abilities, and (c) would contradict the GDD's "resets on … wall". New `LastWallJumpSide`/`LastWallJumpFace` in PlayerState, set on wall jump, cleared on landing/cling/rope grab. Added: the single-wall test runs with DoubleJump off and on; a refill test; the DoubleJump+WallJump ability-matrix row; Notes to GDD and Completion Criteria. Flagged to the operator as Claude's choice, overridable.
- **F3 — accepted.** Re-derived: with vOld = 0 on the jump tick the first move loses 20/2·dt = 0.1667 tile → apex 3.333. Conventions now state impulses set the velocity before integration and vOld is the post-impulse value.
- **F4 — accepted.** Confirmed: tick-order item 1 decremented before use (coyote/wall coyote = 5 airborne ticks) while the buffer decremented after its check. Added one timer convention (a counter set to N on tick T is active T+1…T+N, decremented at a new item 9), named W / L / J once, and restated the coyote, buffer, wall-coyote, lock and gap tests in those terms (coyote W+6 yes / W+7 no; buffer L−5 yes / L−6 no; wall coyote E+6 / E+7; lock J+1…J+12 / J+13). Gap best case updated to ≈5.4 (W+6, per the Reviewer's model).
- **F5 — accepted.** Item 2 now states the support probe updates the standing contact used by items 3–5.
- **F6 — accepted (first remedy).** `StartPlacement.Find` adds the feetY ≥ 0 check itself; `BoxIsFree` keeps no bottom bound, which is what ceiling correction, the rope snap and the ledge pop want. Both places say so.
- **F7 — accepted.** Open question 1 now lists run speed, jump height, max fall, time-to-apex, fall-gravity factor and coyote ticks as levers.
- **F8 — accepted (first remedy).** Confirmed: the snapped hitbox's column holds the top cell, so a Solid/Climbable top blocks the head. Standable rope top limited to OneWay; a rope into solid stops at the head bump, and Phase 2 warns about ropes overlapping solid.
- **F9 — accepted.** Confirmed Phase 2 Step 14's fallback pointed back at Phase 1. Fallback is now `Is.Not.AllocatingGCMemory()` with its own sanity check (unverified on 6000.0 batch mode — proved in Step 13), else code review alone, stated plainly. Phase 2 Step 14 updated to match (that doc is not under review here).
- **F10 — accepted.** Added the chimney gate (3-wide climbed with WallJump and not without; 6-wide not), printing the measured breakpoint; added to Completion Criteria.
- **F11 — accepted (first remedy).** Confirmed the cling rule had no velocity condition. Air cling entry now needs vy ≤ 0 or Up held; tests added (rising past a climbable face doesn't cling; a 3-tile climbable-faced ledge is reached with WallClimb on and off). Re-checked: at the apex the grip (Y+0.75 ≈ 4.25) is above T = 3, so no cling triggers on the way over.
- **F12 — accepted (second remedy).** Confirmed the C# compiler's `<>c.<>9__N_M` caches are static non-readonly. `[CompilerGenerated]` types are skipped on purpose; lambdas in Step stay banned by convention and review.
- **F13 — accepted.** Notes to GDD § Operations marks the Roadmap split and the four decisions.md rows as already applied; at completion only flip the row to Active and add the F2 rule.
- **F14 — accepted.** Grip row is floor(gripY − ContactEpsilon), top edge inclusive; added "hold at T for 60 ticks stays WallCling".
- **F15 — recorded** beside open question 1.
- **F16 — recorded** beside open question 2.

Fold re-verified: PlayerState lists the new fields; Landing, cling and rope grab clear LastWallJumpSide; Completion Criteria and Notes to GDD carry the F2, F10 and F11 rules; the buffer arithmetic (P+1…P+6 ⊇ L+1 ⇔ P ≥ L−5) and the coyote/lock boundaries match the convention. Nothing built.

## Round 2 dispositions (2026-10-23T21:27:12+09:00)

Re-read every cited line; re-derived each defect from the doc's own rules. Modelled numbers are quoted as the Reviewer's.

- **F18 — accepted.** Confirmed: by the F1 reasoning a press-every-tick script fires every wall jump before contact, so it never enters WallSlide and never reaches the refill rule; with the air jump full at the start, cycle 1 gains a whole air jump, so "each start ≥ 0.1 lower" fails on pair 1→2. Step 9 now has Script A (reach, DoubleJump off, unchanged), Script B (the exploit loop: jump only from an active WallSlide, air jump at each apex; asserts the WallSlide precondition, first-cycle gain ≤ 7.0 = at most the starting air jump, later starts ≥ 0.1 lower, and no refill on each later same-face slide) and Script C (the coyote detour, for F17). The DoubleJump+WallJump matrix row uses Script B's inputs and asserts. Re-checked: 7.0 bounds the Reviewer's modelled +6.867, and without (b) Script B gains ≈ +6.9 per cycle, so it fails without the rule.
- **F17 — accepted.** Confirmed PlayerState had WallCoyoteSide but no face. Added `WallCoyoteFace` (set with the side when wall coyote starts); a wall jump from coyote records `WallCoyoteSide`/`WallCoyoteFace` as the last wall; Script C tests that the next same-face slide doesn't refill.
- **F19 — accepted.** Item 2 promotes Air to standing only when vy ≤ 0, matching Landing. Re-checked that a fresh CreateState on the floor (vy = 0) is still promoted on tick 1, so F5's fix holds.
- **F20 — accepted.** Rule (b) is now **(proposal)** in Notes to GDD and Open question 3 with options (a)/(b)/(c), "Claude recommends (b)" and the Reviewer's round-1 recommendation recorded beside it. The decisions.md addition waits for the operator's answer.

Fold re-verified: PlayerState field list, wall-coyote grant, wall-jump recording, item 2, Step 9 scripts, the matrix row, Notes to GDD and Open question 3 all agree. Nothing built.

## Round 3 — approved; close dispositions (2026-10-23T21:31:05+09:00)

- **F21 — accepted.** Confirmed: Script B quoted the round-2 F18 numbers (press-every-tick + apex air jump), not the exploit-loop model the round-2 summary gives for rule (b). Replaced with 0 → 6.875 → 6.297 → 5.719 (+6.875, then −0.578 per cycle). Re-checked both bounds still hold (6.875 ≤ 7.0; 0.578 ≥ 0.1); no assertion changed. The only remaining 6.867/6.489 mentions are inside the appended round-2 review text, which stays verbatim.
- No recommendations open; F15 and F16 recorded in round 1.

**Outcome:** approved as a proposal after 3 rounds (blocking {F1, F2} → {F18} → {}). Nothing is built and no test has run. Conditions carried to implementation: breakpoints and model numbers are the Reviewer's model, the tests print measured values; the allocation probes are unproven on 6000.0 batch mode (Step 13); Open question 3 (air-jump refill rule, written for (b)) is the operator's decision.
