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
