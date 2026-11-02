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
